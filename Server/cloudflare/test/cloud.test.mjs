import {test,beforeEach,afterEach} from 'node:test';
import assert from 'node:assert/strict';
import {readFile} from 'node:fs/promises';
import {fileURLToPath} from 'node:url';
import {spawnSync} from 'node:child_process';
import {Miniflare,convertV4MiniflareOptions} from 'miniflare';
import {cleanupCloud} from '../src/cloud.mjs';
const origin='https://quietcamp.test';
let mf,db;
const token=(uid='firebase-one',provider='google.com',overrides={})=>
  'eyJhbGciOiJSUzI1NiJ9.'+Buffer.from(JSON.stringify({aud:'quiet-camp',iss:'https://securetoken.google.com/quiet-camp',
    sub:uid,exp:Math.floor(Date.now()/1000)+3600,iat:Math.floor(Date.now()/1000),auth_time:Math.floor(Date.now()/1000),
    firebase:{sign_in_provider:provider},...overrides})).toString('base64url')+'.accepted-by-identity-test-double';
beforeEach(async()=>{
 mf=new Miniflare(convertV4MiniflareOptions({
  modules:['index','cloud'].map(name=>({type:'ESModule',path:fileURLToPath(new URL('../src/'+name+'.mjs',import.meta.url))})),
  compatibilityDate:'2026-10-23',d1Databases:{ACCESS_DB:'cloud-test-only'},
  bindings:{QC_CLOUD_ENABLED:'true',QC_CLOUD_SCHEMA:'3',QC_CLOUD_PROVIDERS:'google,facebook',
    QC_CLOUD_RATE_PEPPER:'test-only-32-byte-rate-key-never-a-production-secret',
    QC_FIREBASE_WEB_CONFIG:JSON.stringify({apiKey:'AIza'+'a'.repeat(35),projectId:'quiet-camp',authDomain:'quiet-camp.firebaseapp.com',appId:'1:123456:web:abcdef'})},
  outboundService:async request=>{
    const data=await request.json();
    if(new URL(request.url).pathname.endsWith('accounts:delete')) return Response.json({});
    if(!data.idToken?.endsWith('.accepted-by-identity-test-double')) return Response.json({error:'invalid'}, {status:400});
    const claims=JSON.parse(Buffer.from(data.idToken.split('.')[1],'base64url'));
    return Response.json({users:[{localId:claims.sub==='uid-mismatch'?'another-user':claims.sub,disabled:claims.sub==='disabled',
      ...(claims.sub==='revoked'?{validSince:Math.floor(Date.now()/1000)}:{})}]});
  }
 }));
 db=await mf.getD1Database('ACCESS_DB');
 for(const migration of ['0001_complimentary.sql','0002_cloud_saves.sql','0003_linked_accounts_only.sql']) {
   const sql=await readFile(new URL('../migrations/'+migration,import.meta.url),'utf8');
   await db.batch(sql.split(';').filter(s=>s.trim()).map(s=>db.prepare(s)));
 }
});
afterEach(async()=>{await mf?.dispose();});
async function call(path,data={},session,extra={}) {
 const response=await mf.dispatchFetch(origin+'/v1/cloud/'+path,{method:path==='options'?'GET':'POST',
  headers:{'Content-Type':'application/json','CF-Connecting-IP':'192.0.2.4',...(session?{Authorization:'Bearer '+session}:{}),...extra},
  body:path==='options'?undefined:JSON.stringify(data)});
 return {status:response.status,body:await response.json()};
}

const snapshot=(ids=['QC001'])=>({schemaVersion:2,progress:{completedIds:ids,lastLevelId:ids[0],cosmeticFlags:0},
 session:{placements:[]},album:{entries:[]},memories:{version:1,rewardIds:[]},
 resources:{initialized:true,lives:5,hints:3,currency:12,adLivesUsed:0,adHintsUsed:0,attempts:[]}});
async function begin(provider='google',mode='signin') {
 const r=await call('login-start',{provider,mode});assert.equal(r.status,200);return r.body;
}
async function authenticate(parts,uid='firebase-one',provider='google.com') {
 return call('login-complete',{ticket:parts.ticket,idToken:token(uid,provider)},undefined,{Origin:origin});
}
async function finish(parts) {return call('login-poll',{ticket:parts.ticket,pollSecret:parts.pollSecret});}
async function login(uid='firebase-one',mode='signin',provider='google') {
 const parts=await begin(provider,mode);
 assert.equal((await authenticate(parts,uid,{google:'google.com',facebook:'facebook.com'}[provider])).status,200);
 return finish(parts);
}
async function account(uid='firebase-one') {const r=await login(uid);assert.equal(r.status,200);return r.body;}
test('local guests cannot create anonymous cloud accounts or upload progress',async()=>{
 assert.equal((await call('anonymous',{cloudOptIn:true})).status,404);
 assert.equal((await call('write',{expectedRevision:0,snapshot:snapshot()})).status,401);
 const started=await begin();assert.ok(started.url.includes('#'));
 assert.equal((await db.prepare('SELECT count(*) n FROM cloud_account').first()).n,0);
 assert.equal((await db.prepare('SELECT count(*) n FROM cloud_session').first()).n,0);
 assert.equal((await db.prepare('SELECT count(*) n FROM cloud_save').first()).n,0);
 assert.equal((await call('options')).body.anonymousCloud,undefined);
});
test('linked accounts isolate resources and reject credentials, consent and Pro in progress',async()=>{
 const first=await account(),second=await account('firebase-two');
 assert.equal((await call('write',{expectedRevision:0,snapshot:snapshot()},first.sessionToken)).status,200);
 assert.equal((await call('read',{},first.sessionToken)).body.snapshot.resources.currency,12);
 assert.equal((await call('read',{},second.sessionToken)).body.snapshot,null);
 const row=await db.prepare('SELECT token_hash FROM cloud_session LIMIT 1').first();assert.notEqual(row.token_hash,first.sessionToken);
 for(const bad of [{...snapshot(),settings:{}},{...snapshot(),resources:{...snapshot().resources,pro:true}},{...snapshot(),progress:{completedIds:[],sessionToken:'secret'}}])
  assert.equal((await call('write',{expectedRevision:1,snapshot:bad},first.sessionToken)).status,400);
});
test('atomic revisions prevent simultaneous writes and resource duplication',async()=>{
 const a=await account();
 await call('write',{expectedRevision:0,snapshot:snapshot()},a.sessionToken);
 const results=await Promise.all([['QC001','QC002'],['QC001','QC003']].map(ids=>call('write',{expectedRevision:1,snapshot:snapshot(ids)},a.sessionToken)));
 assert.deepEqual(results.map(r=>r.status).sort(),[200,409]);
 assert.equal((await call('write',{expectedRevision:0,snapshot:snapshot()},a.sessionToken)).status,409);
 assert.equal((await call('read',{},a.sessionToken)).body.revision,2);
});
test('ordinary sign-in recovers an existing account without replacing saved progress',async()=>{
 const a=await account();await call('write',{expectedRevision:0,snapshot:snapshot(['QC001','QC002'])},a.sessionToken);
 const returned=await login();assert.equal(returned.status,200);assert.equal(returned.body.created,false);
 assert.equal(returned.body.accountId,a.accountId);
 assert.deepEqual((await call('read',{},returned.body.sessionToken)).body.snapshot.progress.completedIds,['QC001','QC002']);
});
test('late linking refuses an occupied account and a new account accepts the complete local snapshot',async()=>{
 const occupied=await account();await call('write',{expectedRevision:0,snapshot:snapshot(['QC009'])},occupied.sessionToken);
 const collision=await login('firebase-one','link');assert.equal(collision.status,409);
 assert.deepEqual((await call('read',{},occupied.sessionToken)).body.snapshot.progress.completedIds,['QC009']);
 const linked=await login('new-identity','link');assert.equal(linked.status,200);assert.equal(linked.body.created,true);
 const local=snapshot(['QC001','QC002','QC003']);local.resources.currency=77;local.memories.rewardIds=['care'];
 assert.equal((await call('write',{expectedRevision:0,snapshot:local},linked.body.sessionToken)).status,200);
 assert.deepEqual((await call('read',{},linked.body.sessionToken)).body.snapshot,local);
});
test('Google and Facebook identities are verified and X is rejected',async()=>{
 assert.deepEqual((await call('options')).body.providers,['google','facebook']);
 assert.equal((await call('login-start',{provider:'x',mode:'signin'})).status,400);
 for(const [provider,uid] of [['google','one'],['facebook','two']]) {
  const a=await login(uid,'signin',provider);assert.equal(a.status,200);assert.equal(a.body.provider,provider);
 }
 const start=await begin();
 assert.equal((await authenticate(start,'x-user','twitter.com')).status,401);
 for(const [idToken,originValue] of [[token(),'https://attacker.test'],[token('wrong','google.com',{aud:'wrong-project'}),origin],
  [token('forged').replace('accepted-by-identity-test-double','forged'),origin],[token('disabled'),origin],
  [token('expired','google.com',{exp:0}),origin],[token('old','google.com',{auth_time:0}),origin],
  [token('issuer','google.com',{iss:'https://attacker.test'}),origin],[token('','google.com'),origin],
  [token('uid-mismatch'),origin],[token('revoked','google.com',{auth_time:Math.floor(Date.now()/1000)-60}),origin]]) {
  assert.ok([401,403].includes((await call('login-complete',{ticket:start.ticket,idToken},undefined,{Origin:originValue})).status));
 }
});
test('unknown routes cannot create arbitrary rate-limit buckets',async()=>{
 assert.equal((await call('arbitrary-route')).status,404);
 assert.equal((await db.prepare('SELECT count(*) n FROM complimentary_limit').first()).n,0);
});
test('client cancellation requires the poll secret and prevents late OAuth from creating a profile',async()=>{
 const start=await begin();
 await call('login-cancel',{ticket:start.ticket,pollSecret:'a'.repeat(64)});
 assert.equal((await finish(start)).status,202);
 assert.equal((await call('login-cancel',{ticket:start.ticket,pollSecret:start.pollSecret})).status,200);
 assert.equal((await finish(start)).status,410);
 assert.equal((await authenticate(start)).status,401);
 assert.equal((await db.prepare('SELECT count(*) n FROM cloud_account').first()).n,0);
 assert.equal((await db.prepare('SELECT count(*) n FROM cloud_session').first()).n,0);
});
test('browser close/cancellation is same-origin and cannot revoke an authorized handoff',async()=>{
 const start=await begin();
 assert.equal((await call('login-cancel',{ticket:start.ticket},undefined,{Origin:'https://attacker.test'})).status,403);
 assert.equal((await finish(start)).status,202);
 assert.equal((await call('login-cancel',{ticket:start.ticket},undefined,{Origin:origin})).status,200);
 assert.equal((await finish(start)).status,410);
 const complete=await begin();await authenticate(complete);
 await call('login-cancel',{ticket:complete.ticket},undefined,{Origin:origin});
 assert.equal((await finish(complete)).status,200);
});
test('supported reauthentication preserves a legacy account and its save while X sessions are retired',async()=>{
 const original=await account();
 await call('write',{expectedRevision:0,snapshot:snapshot(['QC007'])},original.sessionToken);
 await db.prepare("UPDATE cloud_account SET provider='x' WHERE id=?").bind(original.accountId).run();
 assert.equal((await call('read',{},original.sessionToken)).status,401);
 const renewed=await login();
 assert.equal(renewed.body.accountId,original.accountId);
 assert.equal(renewed.body.created,false);
 assert.equal(renewed.body.provider,'google');
 assert.deepEqual((await call('read',{},renewed.body.sessionToken)).body.snapshot.progress.completedIds,['QC007']);
});
test('browser auth uses only supported providers and explicitly handles cancelled popups',async()=>{
 const result=await mf.dispatchFetch(origin+'/v1/cloud/login');const html=await result.text();
 assert.equal(result.status,200);
 assert.ok(!html.includes('TwitterAuthProvider'));
 assert.ok(html.includes('auth/popup-closed-by-user')&&html.includes('login-cancel')&&html.includes('pagehide'));
 const script=html.match(/<script type="module" nonce="[a-f0-9]+">([\s\S]*?)<\/script>/)[1];
 assert.ok(script.includes('validTicket'));
 const syntax=spawnSync(process.execPath,['--input-type=module','--check'],{input:script,encoding:'utf8',windowsHide:true});
 assert.equal(syntax.status,0,syntax.stderr);
});
test('deletion page keeps destructive actions hidden and disabled and retains strict response headers',async()=>{
 const response=await mf.dispatchFetch(origin+'/v1/cloud/delete-account');
 assert.equal(response.status,200);
 const html=await response.text();
 assert.match(html,/<button class="danger" id="delete" hidden disabled>/);
 assert.match(html,/\[hidden\]\{display:none!important\}/);
 assert.match(html,/<noscript>/);
 const nonce=html.match(/<style nonce="([a-f0-9]+)">/)[1];
 assert.ok(response.headers.get('Content-Security-Policy').includes("'nonce-"+nonce+"'"));
 assert.ok(!response.headers.get('Content-Security-Policy').includes('unsafe-'));
 assert.equal(response.headers.get('Cache-Control'),'no-store');
 assert.equal(response.headers.get('Referrer-Policy'),'no-referrer');
});
test('handoff requires its independent poll secret, is single use and expires',async()=>{
 const start=await begin();await authenticate(start);
 assert.equal((await call('login-poll',{ticket:start.ticket,pollSecret:'a'.repeat(64)})).status,410);
 assert.equal((await finish(start)).status,200);assert.equal((await finish(start)).status,410);
 const expired=await begin();await db.prepare('UPDATE cloud_handoff SET expires=0').run();
 assert.equal((await finish(expired)).status,410);
});
test('two simultaneous attempts to link one identity create only one profile',async()=>{
 const first=await begin('google','link'),second=await begin('google','link');
 await authenticate(first);await authenticate(second);
 const result=await Promise.all([finish(first),finish(second)]);
 assert.deepEqual(result.map(x=>x.status).sort(),[200,409]);
 assert.equal((await db.prepare('SELECT count(*) n FROM cloud_account').first()).n,1);
});
test('sign-out revokes only that device session and keeps its account data',async()=>{
 const a=await account(),b=(await login()).body;
 await call('write',{expectedRevision:0,snapshot:snapshot()},a.sessionToken);
 assert.equal((await call('sign-out',{},a.sessionToken)).status,200);
 assert.equal((await call('read',{},a.sessionToken)).status,401);
 assert.equal((await call('read',{},b.sessionToken)).status,200);
});
test('account deletion requires fresh provider verification and invalidates pending handoffs',async()=>{
 const a=await account();const pending=await begin();await authenticate(pending);
 assert.equal((await call('delete',{confirm:true},a.sessionToken)).status,409);
 assert.equal((await call('delete-federated',{idToken:token(),confirm:false},undefined,{Origin:origin})).status,400);
 assert.equal((await call('delete-federated',{idToken:token(),confirm:true},undefined,{Origin:origin})).status,200);
 assert.equal((await call('read',{},a.sessionToken)).status,401);
 assert.equal((await finish(pending)).status,410);
 assert.equal((await call('delete-federated',{idToken:token(),confirm:true},undefined,{Origin:origin})).status,200);
});
test('session expiry and login-start rate limits fail closed',async()=>{
 const a=await account();await db.prepare('UPDATE cloud_session SET checked=0').run();
 assert.equal((await call('read',{},a.sessionToken)).status,401);
 for(let i=1;i<10;i++) await begin();
 assert.equal((await call('login-start',{provider:'google',mode:'signin'})).status,429);
});
test('cleanup expires unfinished sign-ins and sessions while preserving linked cloud saves',async()=>{
 const a=await account();await call('write',{expectedRevision:0,snapshot:snapshot()},a.sessionToken);
 await begin();await db.batch([db.prepare('UPDATE cloud_handoff SET expires=0'),db.prepare('UPDATE cloud_session SET checked=0')]);
 await cleanupCloud({ACCESS_DB:db});
 assert.equal((await db.prepare('SELECT count(*) n FROM cloud_handoff').first()).n,0);
 assert.equal((await db.prepare('SELECT count(*) n FROM cloud_session').first()).n,0);
 assert.equal((await db.prepare('SELECT count(*) n FROM cloud_save').first()).n,1);
});
