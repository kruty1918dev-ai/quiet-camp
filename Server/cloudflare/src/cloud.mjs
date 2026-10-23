// Optional account/cloud API. Firebase Auth verifies social identity; D1 owns progress.
// This module never grants Pro or imports the player's privacy/purchase state.
const PREFIX = '/v1/cloud';
const SESSION_AGE = 90 * 86400;
const MAX_PAYLOAD = 768 * 1024;
const PROVIDERS = {google: 'google.com', facebook: 'facebook.com', x: 'twitter.com'};
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
const providerList = env => [...new Set((env.QC_CLOUD_PROVIDERS || '').split(',').map(p=>p.trim()).filter(p => PROVIDERS[p]))];
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
    const value = JSON.parse(new TextDecoder('utf-8', {fatal:true}).decode(bytes));
    check(value && !Array.isArray(value) && typeof value === 'object'); return value;
  } catch (error) { if (error instanceof Failure) throw error; throw new Failure(400, 'invalid_request'); }
}
async function session(request, env) {
  const token = (request.headers.get('Authorization') || '').replace(/^Bearer /, '');
  check(tokenFormat(token), 401, 'signed_out');
  const row = await env.ACCESS_DB.prepare(`SELECT a.*,s.token_hash FROM cloud_session s
    JOIN cloud_account a ON a.id=s.account_id WHERE s.token_hash=? AND s.checked>=?`)
    .bind(await hash(token), now() - SESSION_AGE).first();
  check(row && row.firebase_uid, 401, 'signed_out');
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
    if(route === '/anonymous') throw new Failure(404,'not_found');
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
      let account=await env.ACCESS_DB.prepare('SELECT id FROM cloud_account WHERE firebase_uid=?').bind(login.firebase_uid).first();
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
  const providers=JSON.stringify(providerList(env));
  const nonce=random();
  const html=`<!doctype html><html lang="en"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>Quiet Camp account</title><style nonce="${nonce}">body{font:18px system-ui;max-width:34rem;margin:4rem auto;padding:1rem;background:#eff3ec;color:#17342b}button{font:inherit;padding:1rem;margin:.5rem 0;display:block;width:100%}p{line-height:1.6}</style>
<h1>Quiet Camp</h1><p>${deletion ? 'Delete your cloud account and cloud progress. Your progress on this device is handled separately in the game.' : 'Sign in to connect your cloud progress. This does not enable advertising or analytics.'}</p>
<div id="buttons"></div><p id="status" role="status">Choose your sign-in provider.</p>
<button id="delete" hidden>Delete my cloud account and progress</button>
<p><a href="https://oleksiy-havryliuk.de/quiet-camp/privacy">Privacy policy</a></p>
<script type="module" nonce="${nonce}">
import {initializeApp} from 'https://www.gstatic.com/firebasejs/13.1.0/firebase-app.js';
import {getAuth,setPersistence,inMemoryPersistence,signInWithPopup,signOut,GoogleAuthProvider,FacebookAuthProvider,TwitterAuthProvider} from 'https://www.gstatic.com/firebasejs/13.1.0/firebase-auth.js';
const auth=getAuth(initializeApp(${config})); await setPersistence(auth,inMemoryPersistence);
const allowed=${providers}, deletion=${JSON.stringify(deletion)}, parts=location.hash.slice(1).split(':'), ticket=parts[0], selected=parts[1];
history.replaceState(null,'',location.pathname);
const status=document.getElementById('status'), buttons=document.getElementById('buttons');
if(!deletion) status.textContent='Only continue if Quiet Camp on your device shows this code: '+ticket.slice(0,8).toUpperCase();
const factories={google:()=>new GoogleAuthProvider(),facebook:()=>new FacebookAuthProvider(),x:()=>new TwitterAuthProvider()};
const labels={google:'Google',facebook:'Facebook',x:'X'};
let identity=null;
async function send(path,data){const r=await fetch('/v1/cloud/'+path,{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify(data),credentials:'omit'});if(!r.ok)throw Error('request_failed');return r.json();}
for(const provider of allowed.filter(p=>deletion||p===selected)) {
 const b=document.createElement('button');b.textContent='Continue with '+labels[provider];buttons.appendChild(b);
 b.onclick=async()=>{b.disabled=true;try{
  const result=await signInWithPopup(auth,factories[provider]());identity=result.user;
  if(deletion){status.textContent='Signed in. Confirm below to permanently delete cloud progress.';document.getElementById('delete').hidden=false;}
  else {await send('login-complete',{ticket,idToken:await identity.getIdToken(true)});await signOut(auth);status.textContent='Sign-in completed. Return to Quiet Camp.';buttons.replaceChildren();}
 }catch(e){status.textContent=e.code==='auth/account-exists-with-different-credential'?'This email already uses another provider. Choose the original sign-in method in the game.':'Sign-in did not complete. Return to the game and retry; your local progress is safe.';}finally{b.disabled=false;}};
}
document.getElementById('delete').onclick=async()=>{
 if(!identity||!confirm('Permanently delete your Quiet Camp cloud account and cloud progress?'))return;
 try{await send('delete-federated',{idToken:await identity.getIdToken(true),confirm:true});await signOut(auth);identity=null;buttons.replaceChildren();document.getElementById('delete').hidden=true;status.textContent='Cloud account deleted. Local progress on your devices was not erased.';}
 catch{status.textContent='Deletion did not complete. Sign in again and retry, or contact privacy@oleksiy-havryliuk.de.';}
};
</script></html>`;
  return new Response(html,{headers:{'Content-Type':'text/html;charset=utf-8','Cache-Control':'no-store','Referrer-Policy':'no-referrer',
    'X-Content-Type-Options':'nosniff','Content-Security-Policy':`default-src 'none'; script-src 'nonce-${nonce}' https://www.gstatic.com https://apis.google.com; style-src 'nonce-${nonce}'; connect-src 'self' https://identitytoolkit.googleapis.com https://securetoken.googleapis.com https://www.googleapis.com; frame-src https://${webConfig(env).authDomain}; base-uri 'none'; frame-ancestors 'none'; form-action 'none'`}});
}
