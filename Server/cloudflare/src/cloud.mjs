// Optional account/cloud API. Firebase Auth verifies social identity; D1 owns progress.
// This module never grants Pro or imports the player's privacy/purchase state.
const PREFIX = '/v1/cloud';
const SESSION_AGE = 90 * 86400;
const MAX_PAYLOAD = 768 * 1024;
const PROVIDERS = {google: 'google.com', facebook: 'facebook.com'};
const POST_ROUTES = new Set(['/login-start','/login-poll','/login-cancel','/login-complete','/delete-federated','/status','/read','/write','/sign-out','/delete']);
const encoder = new TextEncoder();
const now = () => Math.floor(Date.now() / 1000);
const random = () => Array.from(crypto.getRandomValues(new Uint8Array(32)), b => b.toString(16).padStart(2, '0')).join('');
const hash = async value => Array.from(new Uint8Array(await crypto.subtle.digest('SHA-256', encoder.encode(value))), b => b.toString(16).padStart(2, '0')).join('');
const reply = (body, status = 200) => new Response(JSON.stringify(body), {status, headers: {
  'Content-Type': 'application/json', 'Cache-Control': 'no-store', 'X-Content-Type-Options': 'nosniff',
  'Referrer-Policy': 'no-referrer'
}});
class Failure extends Error { constructor(status, code) { super(code); this.status = status; } }
const check = (condition, status = 400, code = 'invalid_request') => { if (!condition) throw new Failure(status, code); };
const tokenFormat = value => typeof value === 'string' && /^[a-f0-9]{64}$/.test(value);
const enabled = env => env.QC_CLOUD_ENABLED === 'true' && env.QC_CLOUD_SCHEMA === '3';
const providerList = env => [...new Set((env.QC_CLOUD_PROVIDERS || '').split(',').map(p=>p.trim()).filter(p => Object.hasOwn(PROVIDERS,p)))];
function webConfig(env) {
  try {
    const c = JSON.parse(env.QC_FIREBASE_WEB_CONFIG || '{}');
    check(typeof c.apiKey === 'string' && /^AIza[\w-]{35}$/.test(c.apiKey) && /^[a-z0-9-]+$/.test(c.projectId || ''), 503, 'not_configured');
    check(c.authDomain === c.projectId + '.firebaseapp.com' && /^1:\d+:web:[a-f0-9]+$/.test(c.appId || ''), 503, 'not_configured');
    return {apiKey: c.apiKey, projectId: c.projectId, authDomain: c.authDomain, appId: c.appId};
  } catch (error) { if (error instanceof Failure) throw error; throw new Failure(503, 'not_configured'); }
}
async function body(request, limit = 16384) {
  check((request.headers.get('Content-Type') || '').split(';')[0] === 'application/json');
  // Bound the stream itself, including requests without Content-Length.
  const reader = request.body?.getReader(); check(reader);
  let length = 0; const chunks = [];
  while (true) {
    const part = await reader.read(); if (part.done) break;
    length += part.value.length;
    if (length > limit) { await reader.cancel(); throw new Failure(413, 'too_large'); }
    chunks.push(part.value);
  }
  try {
    const bytes = new Uint8Array(length); let offset = 0;
    for (const chunk of chunks) { bytes.set(chunk, offset); offset += chunk.length; }
    const value = JSON.parse(new TextDecoder('utf-8', {fatal:true,ignoreBOM:false}).decode(bytes));
    check(value && !Array.isArray(value) && typeof value === 'object'); return value;
  } catch (error) { if (error instanceof Failure) throw error; throw new Failure(400, 'invalid_request'); }
}
async function session(request, env) {
  const token = (request.headers.get('Authorization') || '').replace(/^Bearer /, '');
  check(tokenFormat(token), 401, 'signed_out');
  const row = await env.ACCESS_DB.prepare(`SELECT a.*,s.token_hash FROM cloud_session s
    JOIN cloud_account a ON a.id=s.account_id WHERE s.token_hash=? AND s.checked>=?`)
    .bind(await hash(token), now() - SESSION_AGE).first();
  check(row && row.firebase_uid && Object.hasOwn(PROVIDERS,row.provider), 401, 'signed_out');
  await env.ACCESS_DB.batch([
    env.ACCESS_DB.prepare('UPDATE cloud_session SET checked=? WHERE token_hash=?').bind(now(), row.token_hash),
    env.ACCESS_DB.prepare('UPDATE cloud_account SET touched=? WHERE id=?').bind(now(), row.id)
  ]);
  return row;
}
async function issue(env, accountId, rawToken = random(), oldTokenHash = null) {
  const insert=env.ACCESS_DB.prepare('INSERT INTO cloud_session(token_hash,account_id,checked) VALUES (?,?,?)')
    .bind(await hash(rawToken), accountId, now());
  if(oldTokenHash) await env.ACCESS_DB.batch([insert,env.ACCESS_DB.prepare('DELETE FROM cloud_session WHERE token_hash=?').bind(oldTokenHash)]);
  else await insert.run();
  const account = await env.ACCESS_DB.prepare('SELECT provider FROM cloud_account WHERE id=?').bind(accountId).first();
  return {accountId, sessionToken: rawToken, provider: account.provider};
}
async function rate(request, env, route) {
  check(typeof env.QC_CLOUD_RATE_PEPPER === 'string' && env.QC_CLOUD_RATE_PEPPER.length >= 32, 503, 'not_configured');
  const key = await crypto.subtle.importKey('raw', encoder.encode(env.QC_CLOUD_RATE_PEPPER), {name:'HMAC',hash:'SHA-256'},false,['sign']);
  const digest = Array.from(new Uint8Array(await crypto.subtle.sign('HMAC',key,encoder.encode('cloud\0'+(request.headers.get('CF-Connecting-IP') || 'unknown')))), b=>b.toString(16).padStart(2,'0')).join('');
  const count = await env.ACCESS_DB.prepare(`INSERT INTO complimentary_limit(key,count,expires) VALUES (?,1,?)
    ON CONFLICT(key) DO UPDATE SET count=CASE WHEN expires<=? THEN 1 ELSE count+1 END,
    expires=CASE WHEN expires<=? THEN excluded.expires ELSE expires END RETURNING count`)
    .bind('cloud:'+route+':'+digest,now()+600,now(),now()).first();
  check(count.count <= (route === 'login-start' ? 10 : 240), 429, 'rate_limited');
}
function validateSave(value) {
  check(value && Object.getPrototypeOf(value) === Object.prototype && value.schemaVersion === 2);
  check(Object.keys(value).every(k => ['schemaVersion','progress','session','album','memories','resources'].includes(k)));
  check(value.progress && Array.isArray(value.progress.completedIds) && value.progress.completedIds.length <= 10000);
  check(value.progress.completedIds.every(id => typeof id === 'string' && id.length <= 160));
  check(value.session && value.album && value.memories && Array.isArray(value.album.entries));
  check(value.resources && ['lives','hints','currency','adLivesUsed','adHintsUsed'].every(k=>Number.isInteger(value.resources[k]) && value.resources[k]>=0 && value.resources[k]<=2147483647));
  check(Array.isArray(value.resources.attempts) && value.resources.attempts.length<=10000);
  check(Object.keys(value.resources).every(k=>['initialized','lives','hints','currency','adLivesUsed','adHintsUsed','adDay','lifeRecoveryAnchorUtc','attempts'].includes(k)));
  check(value.album.entries.length <= 10000);
  const payload = JSON.stringify(value);
  check(encoder.encode(payload).length <= MAX_PAYLOAD, 413, 'too_large');
  // Defense in depth: these credentials/permissions are never portable progress.
  check(!/"(?:sessionToken|purchaseToken|complimentaryToken|analyticsConsent|advertisingConsent|crashReportingConsent|pro|ownedIds)"\s*:/.test(payload));
  return payload;
}
export async function verifyFirebase(idToken, env, fetcher = fetch) {
  check(typeof idToken === 'string' && idToken.length < 8192, 401, 'identity_rejected');
  const config = webConfig(env); let claims;
  try { claims = JSON.parse(atob(idToken.split('.')[1].replace(/-/g,'+').replace(/_/g,'/'))); }
  catch { throw new Failure(401, 'identity_rejected'); }
  check(claims.aud === config.projectId && claims.iss === 'https://securetoken.google.com/' + config.projectId,
    401, 'identity_rejected');
  check(typeof claims.sub === 'string' && claims.sub.length > 0 && claims.sub.length <= 128
    && Object.values(PROVIDERS).includes(claims.firebase?.sign_in_provider),401,'identity_rejected');
  check(Number.isInteger(claims.exp) && claims.exp > now() && Number.isInteger(claims.iat) && claims.iat <= now()+30
    && Number.isInteger(claims.auth_time) && claims.auth_time <= now()+30 && claims.auth_time >= now()-600,
    401, 'reauthenticate');
  // Google verifies the signature, project, disabled/revoked user and token. A decoded UID is NEVER trusted alone.
  const response = await fetcher('https://identitytoolkit.googleapis.com/v1/accounts:lookup?key='+encodeURIComponent(config.apiKey), {
    method:'POST', headers:{'Content-Type':'application/json'}, body:JSON.stringify({idToken})
  });
  check(response.ok, response.status >= 500 || response.status === 429 ? 503 : 401, 'identity_rejected');
  const user = (await response.json()).users?.[0];
  check(user && !user.disabled && typeof user.localId === 'string' && user.localId === claims.sub,
    401, 'identity_rejected');
  check(!user.validSince || claims.auth_time >= Number(user.validSince),401,'reauthenticate');
  return {uid:user.localId, provider:claims.firebase?.sign_in_provider, idToken};
}
async function deleteAccount(env, id) {
  await env.ACCESS_DB.batch([
    env.ACCESS_DB.prepare('DELETE FROM cloud_login WHERE account_id=?').bind(id),
    env.ACCESS_DB.prepare('DELETE FROM cloud_save WHERE account_id=?').bind(id),
    env.ACCESS_DB.prepare('DELETE FROM cloud_session WHERE account_id=?').bind(id),
    env.ACCESS_DB.prepare('DELETE FROM cloud_account WHERE id=?').bind(id)
  ]);
}
export async function cleanupCloud(env) {
  // Disabled feature still allows housekeeping after a later deactivation.
  await env.ACCESS_DB.batch([
    env.ACCESS_DB.prepare('DELETE FROM cloud_login WHERE expires<?').bind(now()),
    env.ACCESS_DB.prepare('DELETE FROM cloud_handoff WHERE expires<?').bind(now()),
    env.ACCESS_DB.prepare('DELETE FROM cloud_session WHERE checked<?').bind(now()-SESSION_AGE),
    env.ACCESS_DB.prepare('DELETE FROM cloud_account WHERE firebase_uid IS NULL AND touched<?').bind(now()-180*86400)
  ]);
}
export async function handleCloud(request, env) {
  const url = new URL(request.url);
  try {
    check(url.protocol === 'https:',400,'https_required');
    check(enabled(env),503,'cloud_unavailable');
    check(env.ACCESS_DB,503,'cloud_unavailable');
    const route = url.pathname.slice(PREFIX.length);
    if (route === '/login' || route === '/delete-account') {
      check(request.method === 'GET'); return authPage(env, route === '/delete-account');
    }
    check(!url.search,400,'invalid_request');
    if (route === '/options') {
      check(request.method === 'GET'); if(providerList(env).length) webConfig(env);
      return reply({providers:providerList(env)});
    }
    check(request.method === 'POST',405,'method_not_allowed');
    check(POST_ROUTES.has(route),404,'not_found');
    await rate(request,env,route.slice(1));
    const data = await body(request,route === '/write' ? MAX_PAYLOAD+65536 : 16384);
    if (route === '/login-start') {
      webConfig(env);
      check(providerList(env).includes(data.provider) && ['signin','link'].includes(data.mode));
      const ticket=random(), pollSecret=random();
      // A short-lived login attempt is not an anonymous game/cloud account.
      await env.ACCESS_DB.prepare('INSERT INTO cloud_handoff(ticket_hash,poll_hash,provider,mode,expires) VALUES (?,?,?,?,?)')
        .bind(await hash(ticket),await hash(pollSecret),data.provider,data.mode,now()+600).run();
      return reply({ticket,pollSecret,url:url.origin+PREFIX+'/login#'+ticket+':'+data.provider});
    }
    if (route === '/login-cancel') {
      check(tokenFormat(data.ticket));
      if (data.pollSecret !== undefined) {
        check(tokenFormat(data.pollSecret));
        await env.ACCESS_DB.prepare('DELETE FROM cloud_handoff WHERE ticket_hash=? AND poll_hash=?')
          .bind(await hash(data.ticket),await hash(data.pollSecret)).run();
      } else {
        check(request.headers.get('Origin') === url.origin,403,'origin_rejected');
        // A browser can only withdraw an unfinished attempt, never erase an authenticated session.
        await env.ACCESS_DB.prepare('DELETE FROM cloud_handoff WHERE ticket_hash=? AND claimed=0')
          .bind(await hash(data.ticket)).run();
      }
      return reply({cancelled:true});
    }
    if (route === '/login-complete' || route === '/delete-federated') {
      // Same-origin browser operation, never a permissive cross-origin CORS endpoint.
      check(request.headers.get('Origin') === url.origin,403,'origin_rejected');
      const identity = await verifyFirebase(data.idToken,env);
      if (route === '/delete-federated') {
        check(data.confirm === true,400,'confirmation_required');
        const account = await env.ACCESS_DB.prepare('SELECT id FROM cloud_account WHERE firebase_uid=?').bind(identity.uid).first();
        // Delete game data first. If Firebase fails, the same browser identity can retry
        // removing its Auth record even after the D1 account is already gone.
        await env.ACCESS_DB.prepare('DELETE FROM cloud_handoff WHERE firebase_uid=?').bind(identity.uid).run();
        if (account) await deleteAccount(env,account.id);
        const response = await fetch('https://identitytoolkit.googleapis.com/v1/accounts:delete?key='+encodeURIComponent(webConfig(env).apiKey),{
          method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({idToken:identity.idToken})
        });
        check(response.ok,503,'deletion_retry');
        return reply({deleted:true});
      }
      check(tokenFormat(data.ticket));
      const login = await env.ACCESS_DB.prepare('SELECT * FROM cloud_handoff WHERE ticket_hash=? AND expires>? AND claimed=0')
        .bind(await hash(data.ticket),now()).first();
      check(login && PROVIDERS[login.provider] === identity.provider,401,'identity_rejected');
      const updated = await env.ACCESS_DB.prepare('UPDATE cloud_handoff SET firebase_uid=?,claimed=1 WHERE ticket_hash=? AND claimed=0 RETURNING ticket_hash')
        .bind(identity.uid,login.ticket_hash).first();
      check(updated,409,'login_used'); return reply({complete:true});
    }
    if (route === '/login-poll') {
      check(tokenFormat(data.ticket) && tokenFormat(data.pollSecret));
      const login=await env.ACCESS_DB.prepare('SELECT * FROM cloud_handoff WHERE ticket_hash=? AND poll_hash=? AND expires>?')
        .bind(await hash(data.ticket),await hash(data.pollSecret),now()).first();
      check(login,410,'login_expired');
      if(!login.firebase_uid) return reply({pending:true},202);
      const consumed=await env.ACCESS_DB.prepare('DELETE FROM cloud_handoff WHERE ticket_hash=? RETURNING ticket_hash').bind(login.ticket_hash).first();
      check(consumed,410,'login_expired');
      let account=await env.ACCESS_DB.prepare('SELECT id,provider FROM cloud_account WHERE firebase_uid=?').bind(login.firebase_uid).first();
      // Late guest linking must never switch to, overwrite or merge an existing profile.
      check(login.mode!=='link' || !account,409,'identity_already_linked');
      let created=false;
      if(!account) {
        const id=crypto.randomUUID();
        const inserted=await env.ACCESS_DB.prepare(`INSERT INTO cloud_account(id,firebase_uid,provider,created,touched)
          VALUES (?,?,?,?,?) ON CONFLICT(firebase_uid) DO NOTHING RETURNING id`)
          .bind(id,login.firebase_uid,login.provider,now(),now()).first();
        check(inserted || login.mode!=='link',409,'identity_already_linked');
        account=inserted || await env.ACCESS_DB.prepare('SELECT id FROM cloud_account WHERE firebase_uid=?').bind(login.firebase_uid).first();
        created=!!inserted;
      }
      // A freshly verified supported provider may reauthenticate the same Firebase UID.
      // Preserve its account/save; do not merge identities or enable legacy X sessions.
      await env.ACCESS_DB.prepare('UPDATE cloud_account SET provider=? WHERE id=?').bind(login.provider,account.id).run();
      return reply({...await issue(env,account.id),created});
    }
    const account = await session(request,env);
    if (route === '/status') return reply({accountId:account.id,provider:account.provider});
    if (route === '/sign-out') {
      await env.ACCESS_DB.prepare('DELETE FROM cloud_session WHERE token_hash=?').bind(account.token_hash).run(); return reply({signedOut:true});
    }
    if (route === '/delete') throw new Failure(409,'reauthenticate_in_browser');
    if (route === '/read') {
      const row=await env.ACCESS_DB.prepare('SELECT revision,payload,updated FROM cloud_save WHERE account_id=?').bind(account.id).first();
      return reply(row ? {revision:row.revision,snapshot:JSON.parse(row.payload),updated:row.updated} : {revision:0,snapshot:null,updated:0});
    }
    if (route === '/write') {
      check(Number.isSafeInteger(data.expectedRevision) && data.expectedRevision>=0);
      const payload=validateSave(data.snapshot);
      const row=await env.ACCESS_DB.prepare(`INSERT INTO cloud_save(account_id,revision,payload,updated)
        SELECT ?,1,?,? WHERE ?=0
        ON CONFLICT(account_id) DO UPDATE SET revision=revision+1,payload=excluded.payload,updated=excluded.updated
        WHERE revision=? RETURNING revision`)
        .bind(account.id,payload,now(),data.expectedRevision,data.expectedRevision).first();
      // Nonzero expected revision requires UPDATE, since INSERT SELECT would produce no row.
      if (!row && data.expectedRevision>0) {
        const updated=await env.ACCESS_DB.prepare('UPDATE cloud_save SET revision=revision+1,payload=?,updated=? WHERE account_id=? AND revision=? RETURNING revision')
          .bind(payload,now(),account.id,data.expectedRevision).first();
        check(updated,409,'save_conflict'); return reply({revision:updated.revision});
      }
      check(row,409,'save_conflict'); return reply({revision:row.revision});
    }
    throw new Failure(404,'not_found');
  } catch(error) {
    return reply({error:error instanceof Failure ? error.message : 'retry_later'},error instanceof Failure ? error.status : 503);
  }
}

function authPage(env, deletion) {
  const config = JSON.stringify(webConfig(env)).replace(/</g,'\\u003c');
  const providers = JSON.stringify(providerList(env));
  const nonce = random();
  const html = `<!doctype html><html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<meta name="robots" content="noindex,nofollow"><meta name="theme-color" content="#132e25">
<title>${deletion ? 'Delete your account' : 'Connect your progress'} — Quiet Camp</title>
<style nonce="${nonce}">
:root{color-scheme:dark;--bg:#132e25;--card:#1b382e;--cream:#f6efdF;--muted:#bdcbbd;--line:#395348;--accent:#ddbf86;--danger:#f1b6a8}
*{box-sizing:border-box}[hidden]{display:none!important}body{margin:0;background:var(--bg);color:var(--cream);font:16px/1.6 system-ui,-apple-system,BlinkMacSystemFont,"Segoe UI",sans-serif;min-height:100vh;background-image:radial-gradient(ellipse at 90% 0%,#2c493c 0,transparent 55%)}a{color:inherit;text-underline-offset:4px}a:hover{color:var(--accent)}a:focus-visible,button:focus-visible{outline:3px solid var(--accent);outline-offset:5px}button{font:inherit;cursor:pointer}button:disabled{cursor:wait;opacity:.6}header,footer{max-width:1120px;margin:auto;padding:28px 32px;display:flex;align-items:center;justify-content:space-between;gap:20px}.brand{display:inline-flex;align-items:center;gap:12px;text-decoration:none;font-family:Georgia,serif;font-size:24px}.brand svg{width:35px;height:35px;color:var(--accent)}.back{font-size:14px;color:var(--muted);text-decoration:none}main{max-width:1040px;margin:36px auto 64px;padding:0 32px;display:grid;grid-template-columns:1fr 1fr;gap:72px;align-items:start}.eyebrow{color:var(--accent);text-transform:uppercase;letter-spacing:.18em;font-size:11px;font-weight:650;margin:0 0 18px}h1{font:normal clamp(42px,5vw,64px)/1.08 Georgia,serif;letter-spacing:-.035em;margin:0 0 24px}p{margin:0 0 18px}.intro{color:var(--muted);font-size:17px;max-width:400px}.details{margin-top:38px;border-top:1px solid var(--line);padding-top:24px}.details h2{font-size:14px;margin:0 0 14px;font-weight:600}.details ul{padding:0;list-style:none;margin:0}.details li{position:relative;padding-left:22px;margin:12px 0;color:var(--muted);font-size:14px}.details li:before{content:'✓';position:absolute;left:0;color:var(--accent)}.details .removal:before{content:'−';color:var(--danger)}.card{border:1px solid var(--line);border-radius:24px;padding:32px;background:var(--card);box-shadow:0 24px 70px #061a142b}.steps{display:flex;gap:24px;margin-bottom:30px;color:var(--muted);font-size:12px}.step{display:flex;align-items:center;gap:8px}.step span{display:grid;place-items:center;width:24px;height:24px;border:1px solid var(--line);border-radius:50%;font-size:11px}.step.current{color:var(--cream)}.step.current span{background:var(--cream);border-color:var(--cream);color:var(--bg)}h2{font:normal 26px/1.2 Georgia,serif;margin:0 0 12px}.card-copy{font-size:14px;color:var(--muted)}.provider{width:100%;display:flex;align-items:center;justify-content:center;gap:12px;border:1px solid #e5e4de;border-radius:10px;min-height:54px;padding:12px 18px;margin-top:12px;background:#faf9f5;color:#24362e;font-weight:600;font-size:14px}.provider:hover{background:#e9eddf}.provider svg{width:20px;height:20px;flex:none}.status{font-size:13px;color:var(--muted);margin:20px 0 0;overflow-wrap:anywhere}.status[data-tone=error]{color:#ffc5b7}.status[data-tone=success]{color:#d0e8b8}.status:focus{outline:none}.identity{font-size:14px;color:var(--muted);overflow-wrap:anywhere;margin-bottom:20px}.warning{border-left:2px solid var(--danger);padding:3px 0 3px 16px;font-size:14px;color:var(--muted);margin:24px 0}.warning strong{display:block;color:var(--cream);margin-bottom:4px}.danger{width:100%;min-height:54px;padding:13px 16px;border:1px solid var(--danger);border-radius:10px;background:var(--danger);color:#42251f;font-size:14px;font-weight:650}.danger:hover{background:#ffd0c4}.secondary{display:block;margin:18px auto 0;padding:4px;background:none;border:0;color:var(--muted);font-size:13px;text-decoration:underline;text-underline-offset:4px;min-height:36px}.secure{border-top:1px solid var(--line);margin:26px 0 0;padding-top:18px;color:var(--muted);font-size:12px;display:flex;gap:9px;align-items:flex-start}.secure svg{flex:none;width:16px;height:20px}.success-mark{display:grid;place-items:center;width:48px;height:48px;border:1px solid #6f8c68;border-radius:50%;color:#d0e8b8;font-size:24px;margin:0 0 24px}.return-link{display:inline-block;margin-top:12px;color:var(--accent);font-size:14px}footer{border-top:1px solid var(--line);color:var(--muted);font-size:12px;flex-wrap:wrap}footer nav{display:flex;gap:22px;flex-wrap:wrap}footer a{text-decoration:none}noscript p{color:var(--danger);padding-top:12px}@media(max-width:760px){header,footer{padding:22px 24px}main{grid-template-columns:1fr;gap:32px;margin:24px auto 40px;padding:0 24px}.brand{font-size:22px}.back{font-size:12px}h1{font-size:44px}.intro{max-width:none}.details{margin-top:26px;padding-top:20px}.card{padding:26px 24px;border-radius:20px}.steps{margin-bottom:24px}}@media(prefers-reduced-motion:no-preference){button,a{transition:background .15s,color .15s}}
</style></head><body>
<header><a class="brand" href="https://oleksiy-havryliuk.de/quiet-camp"><svg viewBox="0 0 40 40" fill="none" aria-hidden="true"><path d="M4 31 20 7l16 24H4Z" stroke="currentColor" stroke-width="1.6"/><path d="m14 31 6-12 6 12M20 7v12M6 35h28" stroke="currentColor" stroke-width="1.6"/></svg>Quiet Camp</a><a class="back" href="https://oleksiy-havryliuk.de/quiet-camp">← Back to the game</a></header>
<main><section aria-labelledby="page-title"><p class="eyebrow">Your account · Your choice</p><h1 id="page-title">${deletion ? 'A little space.<br>A fresh start.' : 'Your camp,<br>wherever you go.'}</h1>
<p class="intro">${deletion ? 'Delete your Quiet Camp cloud account and saved cloud progress. Sign in with your usual provider to get started.' : 'Connect your Quiet Camp progress with Google or Facebook, then return to your game.'}</p>
<div class="details"><h2>${deletion ? 'What happens when you delete?' : 'Keep your progress close'}</h2><ul>${deletion ? '<li class="removal">Your Quiet Camp cloud account and cloud save are permanently removed.</li><li>Progress stored on your devices stays on those devices. Manage it separately in the game.</li><li>Your Google or Facebook account stays active.</li>' : '<li>Use the same sign-in method you use in the game.</li><li>Your sign-in is only used for your Quiet Camp cloud account.</li><li>This does not enable advertising or analytics.</li>'}</ul></div></section>
<section class="card" aria-label="${deletion ? 'Delete your account' : 'Connect your account'}">
${deletion ? '<div class="steps" aria-label="Deletion steps"><div id="step-signin" class="step current" aria-current="step"><span>1</span>Verify account</div><div id="step-confirm" class="step"><span>2</span>Confirm deletion</div></div>' : ''}
<div id="signin"><h2>${deletion ? 'Delete your account' : 'Continue to Quiet Camp'}</h2><p class="card-copy">${deletion ? 'First, verify the account you want to delete. Choose the provider you used in Quiet Camp.' : 'Choose your sign-in provider to securely connect your progress.'}</p><div id="buttons"></div></div>
<div id="confirmation" hidden><h2>Confirm deletion</h2><p id="identity" class="identity"></p><div class="warning"><strong>This is permanent.</strong>Your cloud account and saved cloud progress will be deleted. You cannot undo this action.</div><button class="danger" id="delete" hidden disabled>Delete my cloud account &amp; progress</button><button class="secondary" id="change-account">Use a different account</button></div>
<div id="complete" hidden><div class="success-mark" aria-hidden="true">✓</div><h2 id="complete-title"></h2><p class="card-copy" id="complete-copy"></p><a class="return-link" href="https://oleksiy-havryliuk.de/quiet-camp">Return to Quiet Camp →</a></div>
<p id="status" class="status" role="status" aria-live="polite" tabindex="-1">Preparing secure sign-in…</p>
<p class="secure"><svg viewBox="0 0 20 24" fill="none" aria-hidden="true"><rect x="4" y="10" width="12" height="10" rx="2" stroke="currentColor"/><path d="M6 10V7a4 4 0 0 1 8 0v3m-4 4v2" stroke="currentColor" stroke-linecap="round"/></svg>Sign in securely with your provider. Quiet Camp never receives your Google or Facebook password.</p>
<noscript><p>Enable JavaScript to sign in, or contact <a href="mailto:privacy@oleksiy-havryliuk.de">privacy@oleksiy-havryliuk.de</a> for help with account deletion.</p></noscript>
</section></main><footer><span>Quiet Camp · Kruty1918 Games</span><nav aria-label="Account help"><a href="https://oleksiy-havryliuk.de/quiet-camp/account">Account help</a><a href="https://oleksiy-havryliuk.de/quiet-camp/privacy">Privacy policy</a><a href="mailto:privacy@oleksiy-havryliuk.de">Contact</a></nav></footer>
<script type="module" nonce="${nonce}">
const allowed=${providers}, deletion=${JSON.stringify(deletion)}, parts=location.hash.slice(1).split(':'), ticket=parts[0], selected=parts[1];
history.replaceState(null,'',location.pathname);
const el=id=>document.getElementById(id), status=el('status'), buttons=el('buttons'), remove=el('delete');
const validTicket=/^[a-f0-9]{64}$/.test(ticket) && allowed.includes(selected);
let identity=null,finished=false,busy=false;
function message(text,tone=''){status.textContent=text;status.dataset.tone=tone;}
function lock(value){busy=value;for(const b of document.querySelectorAll('button'))b.disabled=value;remove.disabled=value||!identity;}
function reset(){identity=null;el('confirmation').hidden=true;remove.hidden=true;remove.disabled=true;el('signin').hidden=false;if(deletion){el('step-signin').classList.add('current');el('step-signin').setAttribute('aria-current','step');el('step-confirm').classList.remove('current');el('step-confirm').removeAttribute('aria-current');}}
function complete(title,copy){identity=null;el('signin').hidden=true;el('confirmation').hidden=true;remove.hidden=true;remove.disabled=true;el('complete-title').textContent=title;el('complete-copy').textContent=copy;el('complete').hidden=false;document.querySelector('.steps')?.setAttribute('hidden','');message('All done.','success');status.focus();}
async function send(path,data){const r=await fetch('/v1/cloud/'+path,{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify(data),credentials:'omit'});if(!r.ok)throw Error('request_failed');return r.json();}
addEventListener('pagehide',()=>{if(!deletion && validTicket && !finished)navigator.sendBeacon('/v1/cloud/login-cancel',new Blob([JSON.stringify({ticket})],{type:'application/json'}));});
try{
 const [{initializeApp},{getAuth,setPersistence,inMemoryPersistence,signInWithPopup,signOut,GoogleAuthProvider,FacebookAuthProvider}]=await Promise.all([import('https://www.gstatic.com/firebasejs/13.1.0/firebase-app.js'),import('https://www.gstatic.com/firebasejs/13.1.0/firebase-auth.js')]);
 const auth=getAuth(initializeApp(${config}));await setPersistence(auth,inMemoryPersistence);
 const factories={google:()=>{const p=new GoogleAuthProvider();p.setCustomParameters({prompt:'select_account'});return p;},facebook:()=>new FacebookAuthProvider()};
 const labels={google:'Google',facebook:'Facebook'};
 const icons={google:'<svg viewBox="0 0 24 24" aria-hidden="true"><path fill="#4285F4" d="M21.6 12.23c0-.71-.06-1.39-.18-2.05H12v3.87h5.38a4.6 4.6 0 0 1-2 3.02v2.51h3.24c1.9-1.75 2.98-4.33 2.98-7.35Z"/><path fill="#34A853" d="M12 22c2.7 0 4.96-.9 6.62-2.42l-3.24-2.51c-.9.6-2.04.97-3.38.97-2.6 0-4.8-1.76-5.59-4.12H3.07v2.59A10 10 0 0 0 12 22Z"/><path fill="#FBBC05" d="M6.41 13.92a6 6 0 0 1 0-3.84V7.49H3.07a10 10 0 0 0 0 9.02l3.34-2.59Z"/><path fill="#EA4335" d="M12 5.96c1.47 0 2.79.5 3.82 1.49l2.87-2.87A9.6 9.6 0 0 0 12 2a10 10 0 0 0-8.93 5.49l3.34 2.59C7.2 7.72 9.4 5.96 12 5.96Z"/></svg>',facebook:'<svg viewBox="0 0 24 24" aria-hidden="true"><path fill="#1877F2" d="M24 12a12 12 0 1 0-13.88 11.85v-8.38H7.08V12h3.04V9.36c0-3.01 1.79-4.68 4.53-4.68 1.31 0 2.69.24 2.69.24v2.95h-1.52c-1.49 0-1.95.93-1.95 1.88V12h3.32l-.53 3.47h-2.79v8.38A12 12 0 0 0 24 12Z"/></svg>'};
 message(deletion?'Choose your sign-in provider.':validTicket?'Only continue if Quiet Camp on your device shows this code: '+ticket.slice(0,8).toUpperCase():'Start sign-in from Quiet Camp, then return here.');
 if(deletion&&!allowed.length)message('Sign-in is temporarily unavailable. Contact privacy@oleksiy-havryliuk.de for help.','error');
 for(const provider of allowed.filter(p=>deletion||validTicket&&p===selected)){
  const b=document.createElement('button');b.className='provider';b.innerHTML=icons[provider];b.append(document.createTextNode('Continue with '+labels[provider]));buttons.appendChild(b);
  b.onclick=async()=>{if(busy)return;lock(true);message('Opening '+labels[provider]+' sign-in…');try{
   const result=await signInWithPopup(auth,factories[provider]());identity=result.user;
   if(deletion){el('identity').textContent=identity.email||identity.displayName||'Your '+labels[provider]+' account';el('signin').hidden=true;el('confirmation').hidden=false;remove.hidden=false;el('step-signin').classList.remove('current');el('step-signin').removeAttribute('aria-current');el('step-confirm').classList.add('current');el('step-confirm').setAttribute('aria-current','step');message('Account verified. Review the details before deleting.');}
   else{await send('login-complete',{ticket,idToken:await identity.getIdToken(true)});finished=true;await signOut(auth).catch(()=>{});complete('You’re connected.','Sign-in completed. Return to Quiet Camp to continue your game.');}
  }catch(e){reset();await signOut(auth).catch(()=>{});
   const cancelled=['auth/popup-closed-by-user','auth/cancelled-popup-request','auth/user-cancelled'].includes(e.code);
   if(cancelled&&!deletion){await send('login-cancel',{ticket}).catch(()=>{});finished=true;buttons.replaceChildren();}
   message(cancelled?(deletion?'Sign-in cancelled. No account data was deleted. Choose a provider to try again.':'Sign-in cancelled. Return to Quiet Camp; your local progress is safe.'):e.code==='auth/popup-blocked'?'Your browser blocked the sign-in window. Tap the provider button again to retry.':e.code==='auth/account-exists-with-different-credential'?'This email already uses another provider. Choose your original sign-in method.':'Sign-in did not complete. Please try again; your local progress is safe.',cancelled?'':'error');
  }finally{lock(false);}};
 }
 el('change-account').onclick=async()=>{if(busy)return;lock(true);reset();try{await signOut(auth);message('Choose the account you want to delete.');}catch{message('Could not sign out. Refresh this page and try again.','error');}finally{lock(false);}};
 remove.onclick=async()=>{
  if(busy||!identity||!confirm('Permanently delete your Quiet Camp cloud account and cloud progress? This cannot be undone.'))return;
  lock(true);message('Deleting your cloud account…');try{
   await send('delete-federated',{idToken:await identity.getIdToken(true),confirm:true});await signOut(auth).catch(()=>{});complete('Your account is deleted.','Your Quiet Camp cloud account and cloud progress were deleted. Progress stored on your devices and your Google or Facebook account were not erased.');
  }catch{reset();await signOut(auth).catch(()=>{});message('Deletion could not be confirmed. Sign in again to retry, or contact privacy@oleksiy-havryliuk.de.','error');}finally{lock(false);}
 };
}catch{reset();message('Secure sign-in could not load. Refresh this page to retry, or contact privacy@oleksiy-havryliuk.de.','error');}
</script></body></html>`;
  return new Response(html,{headers:{'Content-Type':'text/html;charset=utf-8','Cache-Control':'no-store','Referrer-Policy':'no-referrer',
    'X-Content-Type-Options':'nosniff','Content-Security-Policy':`default-src 'none'; script-src 'nonce-${nonce}' https://www.gstatic.com https://apis.google.com; style-src 'nonce-${nonce}'; connect-src 'self' https://identitytoolkit.googleapis.com https://securetoken.googleapis.com https://www.googleapis.com; frame-src https://${webConfig(env).authDomain}; base-uri 'none'; frame-ancestors 'none'; form-action 'none'`}});
}
