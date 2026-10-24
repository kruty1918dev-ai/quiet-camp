// Shared website/native account operations. No passwords, admin keys, save
// payloads or session tokens are exposed by the owner directory.
import {accountPage} from './account-page.mjs';
export const ACCOUNT_POST_ROUTES = ['/web/sign-in','/web/session','/web/profile','/web/sign-out','/web/delete','/admin/accounts','/native/sign-in','/native/delete'];
export const portalEnabled = env => env.QC_ACCOUNT_PORTAL_ENABLED === 'true';
const cookieName = '__Host-qc_account';
const webAge = 3600;
const owners = env => (env.QC_ADMIN_FIREBASE_UIDS || '').split(',').map(x=>x.trim()).filter(Boolean);
const cookie = (value,age=webAge) => `${cookieName}=${value}; Path=/; Max-Age=${age}; Secure; HttpOnly; SameSite=Strict`;
const supported = {'google.com':'google','facebook.com':'facebook'};

export function verifiedProfile(user,provider) {
  const providers=[...new Set((user.providerUserInfo || []).map(x=>supported[x.providerId]).filter(Boolean))];
  const primary=supported[provider]; if(primary&&!providers.includes(primary))providers.push(primary);
  return {displayName:typeof user.displayName==='string'?Array.from(user.displayName.trim()).slice(0,40).join(''):'',
    email:typeof user.email==='string'&&user.email.length<=320?user.email:null,providers};
}
export async function pendingDeletion(env,uid) {
  return !!await env.ACCESS_DB.prepare('SELECT firebase_uid FROM cloud_deletion WHERE firebase_uid=?').bind(uid).first();
}
export async function saveProfile(env,accountId,profile,now) {
  await env.ACCESS_DB.prepare(`INSERT INTO cloud_profile(account_id,display_name,email,providers,updated) VALUES (?,?,?,?,?)
    ON CONFLICT(account_id) DO UPDATE SET email=excluded.email,providers=excluded.providers,updated=excluded.updated`)
    .bind(accountId,profile.displayName,profile.email,JSON.stringify(profile.providers),now()).run();
}
export async function publicProfile(env,account,now) {
  const row=await env.ACCESS_DB.prepare(`SELECT p.display_name,p.email,p.providers,s.updated AS synced,s.revision
    FROM cloud_account a LEFT JOIN cloud_profile p ON p.account_id=a.id LEFT JOIN cloud_save s ON s.account_id=a.id
    WHERE a.id=?`).bind(account.id).first();
  let providers;try{providers=JSON.parse(row?.providers || '[]');}catch{providers=[];}
  if(!providers.length)providers=[account.provider];
  return {accountId:account.id,displayName:row?.display_name || '',email:row?.email || null,
    providers,created:account.created,lastActivity:account.touched,cloudUpdated:row?.synced || null,
    hasCloudSave:!!row?.revision,isAdmin:owners(env).includes(account.firebase_uid),serverTime:now()};
}
async function webSession(request,env,{check,hash,now}) {
  const values=(request.headers.get('Cookie') || '').split(';').map(x=>x.trim()).filter(x=>x.startsWith(cookieName+'='));
  check(values.length===1,401,'signed_out'); const token=values[0].slice(cookieName.length+1);
  check(/^[a-f0-9]{64}$/.test(token),401,'signed_out');
  const account=await env.ACCESS_DB.prepare(`SELECT a.*,w.token_hash,w.expires FROM cloud_web_session w
    JOIN cloud_account a ON a.id=w.account_id WHERE w.token_hash=? AND w.expires>?`)
    .bind(await hash(token),now()).first();
  check(account&&account.firebase_uid&&supported[{google:'google.com',facebook:'facebook.com'}[account.provider]],401,'signed_out');
  return account;
}
function sameOrigin(request,env,{check}) {
  // The portal is hosted by this Worker; no cross-origin credentialed API.
  check(request.headers.get('Origin')===new URL(request.url).origin,403,'origin_rejected');
  check(!request.headers.get('Sec-Fetch-Site')||['same-origin','none'].includes(request.headers.get('Sec-Fetch-Site')),403,'origin_rejected');
}
async function provision(env,identity,mode,d) {
  const {check,now}=d;check(!await pendingDeletion(env,identity.uid),409,'deletion_pending');
  let account=await env.ACCESS_DB.prepare('SELECT * FROM cloud_account WHERE firebase_uid=?').bind(identity.uid).first();
  check(mode!=='link'||!account,409,'identity_already_linked'); let created=false;
  if(!account){
    const inserted=await env.ACCESS_DB.prepare(`INSERT INTO cloud_account(id,firebase_uid,provider,created,touched)
      SELECT ?,?,?,?,? WHERE NOT EXISTS (SELECT 1 FROM cloud_deletion WHERE firebase_uid=?)
      ON CONFLICT(firebase_uid) DO NOTHING RETURNING id`)
      .bind(crypto.randomUUID(),identity.uid,supported[identity.provider],now(),now(),identity.uid).first();
    check(inserted||mode!=='link',409,'identity_already_linked');created=!!inserted;
    account=await env.ACCESS_DB.prepare('SELECT * FROM cloud_account WHERE firebase_uid=?').bind(identity.uid).first();
  }
  await env.ACCESS_DB.prepare('UPDATE cloud_account SET provider=?,touched=? WHERE id=?')
    .bind(supported[identity.provider],now(),account.id).run();
  account.provider=supported[identity.provider];account.touched=now();
  await saveProfile(env,account.id,identity.profile,now);
  return {account,created};
}
export async function eraseIdentity(env,identity,d) {
  const {now,check,webConfig,deleteAccount}=d;
  // Tombstone is written before any removal. A failed provider request is
  // retryable using fresh identity proof, even though all old sessions are gone.
  await env.ACCESS_DB.prepare('INSERT INTO cloud_deletion(firebase_uid,requested) VALUES (?,?) ON CONFLICT(firebase_uid) DO NOTHING')
    .bind(identity.uid,now()).run();
  await env.ACCESS_DB.prepare('DELETE FROM cloud_handoff WHERE firebase_uid=?').bind(identity.uid).run();
  const account=await env.ACCESS_DB.prepare('SELECT id FROM cloud_account WHERE firebase_uid=?').bind(identity.uid).first();
  if(account)await deleteAccount(env,account.id);
  const response=await fetch('https://identitytoolkit.googleapis.com/v1/accounts:delete?key='+encodeURIComponent(webConfig(env).apiKey),{
    method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({idToken:identity.idToken})});
  check(response.ok,503,'deletion_retry');
  await env.ACCESS_DB.prepare('UPDATE cloud_deletion SET completed=? WHERE firebase_uid=?').bind(now(),identity.uid).run();
}
export function accountDocument(env,admin,lang,d){
  d.check(portalEnabled(env),503,'account_portal_unavailable');
  return accountPage(d.webConfig(env),d.providerList(env),admin,lang,d.random());
}
export async function handleAccountRoute(route,request,env,data,d){
  const {check,now,reply,verifyFirebase,random,hash,issue}=d;
  check(portalEnabled(env),503,'account_portal_unavailable');
  const web=route.startsWith('/web/')||route.startsWith('/admin/');if(web)sameOrigin(request,env,d);
  if(route==='/web/sign-in'||route==='/native/sign-in'){
    check(['google','facebook'].includes(data.provider)&&d.providerList(env).includes(data.provider),400,'provider_unavailable');
    const identity=await verifyFirebase(data.idToken,env);
    check(supported[identity.provider]===data.provider,401,'identity_rejected');
    if(web&&data.expectedAccountId!==undefined){
      check(typeof data.expectedAccountId==='string'&&data.expectedAccountId.length<=64);
      const expected=await env.ACCESS_DB.prepare('SELECT firebase_uid FROM cloud_account WHERE id=?').bind(data.expectedAccountId).first();
      check(expected?.firebase_uid===identity.uid,409,'account_mismatch');
    }
    const mode=route==='/web/sign-in'?'signin':data.mode;
    check(['signin','link'].includes(mode));
    const {account,created}=await provision(env,identity,mode,d);
    if(!web)return reply({...await issue(env,account.id),created,profile:await publicProfile(env,account,now)});
    // Rotate this browser's session without logging other devices out.
    const previous=await webSession(request,env,d).catch(()=>null);
    const token=random();
    await env.ACCESS_DB.batch([
      env.ACCESS_DB.prepare('DELETE FROM cloud_web_session WHERE expires<=?').bind(now()),
      ...(previous?[env.ACCESS_DB.prepare('DELETE FROM cloud_web_session WHERE token_hash=?').bind(previous.token_hash)]:[]),
      env.ACCESS_DB.prepare('INSERT INTO cloud_web_session(token_hash,account_id,expires) VALUES (?,?,?)')
        .bind(await hash(token),account.id,now()+webAge)
    ]);
    const response=reply({profile:await publicProfile(env,account,now),created});response.headers.set('Set-Cookie',cookie(token));return response;
  }
  if(route==='/web/delete'||route==='/native/delete'){
    check(data.confirm===true,400,'confirmation_required');
    const identity=await verifyFirebase(data.idToken,env);
    if(web){const current=await webSession(request,env,d).catch(()=>null);check(!current||current.firebase_uid===identity.uid,409,'account_mismatch');}
    await eraseIdentity(env,identity,d);
    const response=reply({deleted:true});if(web)response.headers.set('Set-Cookie',cookie('',0));return response;
  }
  const account=await webSession(request,env,d);
  if(route==='/web/session')return reply({profile:await publicProfile(env,account,now)});
  if(route==='/web/sign-out'){
    await env.ACCESS_DB.prepare('DELETE FROM cloud_web_session WHERE token_hash=?').bind(account.token_hash).run();
    const response=reply({signedOut:true});response.headers.set('Set-Cookie',cookie('',0));return response;
  }
  if(route==='/web/profile'){
    check(typeof data.displayName==='string');const name=data.displayName.trim().normalize('NFC');
    check(Array.from(name).length>=1&&Array.from(name).length<=40&&!/[\u0000-\u001f\u007f-\u009f\u202a-\u202e\u2066-\u2069]/u.test(name),400,'invalid_name');
    await env.ACCESS_DB.prepare(`INSERT INTO cloud_profile(account_id,display_name,providers,updated) VALUES (?,?,?,?)
      ON CONFLICT(account_id) DO UPDATE SET display_name=excluded.display_name,updated=excluded.updated`)
      .bind(account.id,name,JSON.stringify([account.provider]),now()).run();
    return reply({profile:await publicProfile(env,account,now)});
  }
  if(route==='/admin/accounts'){
    check(owners(env).includes(account.firebase_uid),403,'admin_required');
    check(data.query===undefined||typeof data.query==='string');const query=(data.query||'').trim();check(query.length<=320);
    let cursor={created:9007199254740991,id:'~'};
    if(data.cursor){try{cursor=JSON.parse(atob(data.cursor));}catch{check(false);}
      check(Number.isSafeInteger(cursor.created)&&cursor.created>=0&&typeof cursor.id==='string'&&cursor.id.length<=64);}
    const rows=await env.ACCESS_DB.prepare(`SELECT a.id,a.firebase_uid,a.provider,a.created,a.touched,
      p.display_name,p.email,p.providers,s.updated AS cloud_updated,s.revision,
      (SELECT count(*) FROM cloud_session c WHERE c.account_id=a.id AND c.checked>=?) AS device_sessions
      FROM cloud_account a LEFT JOIN cloud_profile p ON p.account_id=a.id LEFT JOIN cloud_save s ON s.account_id=a.id
      WHERE a.firebase_uid IS NOT NULL AND (?='' OR a.id=? OR a.firebase_uid=? OR p.email=? OR p.display_name=?)
      AND (a.created<? OR (a.created=? AND a.id<?)) ORDER BY a.created DESC,a.id DESC LIMIT 51`)
      .bind(now()-90*86400,query,query,query,query,query,cursor.created,cursor.created,cursor.id).all();
    const results=rows.results.slice(0,50).map(row=>({...row,providers:JSON.parse(row.providers||'[]'),has_cloud_save:!!row.revision}));
    const last=results.at(-1);return reply({accounts:results,nextCursor:rows.results.length>50?btoa(JSON.stringify({created:last.created,id:last.id})):null});
  }
  check(false,404,'not_found');
}
