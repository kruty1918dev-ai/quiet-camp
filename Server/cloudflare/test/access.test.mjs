import { test, beforeEach, afterEach } from "node:test";
import assert from "node:assert/strict";
import { createHmac, createHash } from "node:crypto";
import { readFile } from "node:fs/promises";
import { fileURLToPath } from "node:url";
import { Miniflare, convertV4MiniflareOptions } from "miniflare";
import worker from "../src/index.mjs";

const password = "RandomTestCredential1234567890Ab";
const username = "test-review", pepper = Buffer.alloc(32, 73);
const verifier = createHmac("sha256", pepper).update("quietcamp-credential-v1\0"+username+"\0"+password).digest("hex");
const account = JSON.stringify({schema:1,kind:"generated-alnum32-hmac-sha256-v1",username,verifier});
let mf, db;
beforeEach(async () => {
  mf = new Miniflare(convertV4MiniflareOptions({
    modules:[{type:"ESModule",path:fileURLToPath(new URL("../src/index.mjs",import.meta.url))},
      ...['cloud','accounts','account-page'].map(name=>({type:"ESModule",path:fileURLToPath(new URL('../src/'+name+'.mjs',import.meta.url))}))],
    compatibilityDate:"2026-10-23", d1Databases:{ACCESS_DB:"quietcamp-local-only"},
    bindings:{QC_ANDROID_PACKAGE:"com.quietcamp.game",QC_PRO_PRODUCT:"quietcamp_pro",QC_EXPLORE_PUBLISHED:"true",
      QC_ACCESS_ACCOUNT:account,QC_ACCESS_PEPPER:pepper.toString("base64url")}
  }));
  db = await mf.getD1Database("ACCESS_DB");
  const sql = await readFile(new URL("../migrations/0001_complimentary.sql",import.meta.url),"utf8");
  await db.batch(sql.split(";").filter(s=>s.trim()).map(s=>db.prepare(s)));
});
afterEach(async () => { await mf?.dispose(); });
async function call(route, data, headers={}) {
  const response = await mf.dispatchFetch("https://quietcamp.test/v1/access/"+route, {
    method:data===undefined?"GET":"POST", headers:{"Content-Type":"application/json","CF-Connecting-IP":"192.0.2.17",...headers},
    body:data===undefined?undefined:JSON.stringify(data)
  });
  return {status:response.status,headers:response.headers,body:await response.json()};
}
const credentials = () => ({packageName:"com.quietcamp.game",username,password});

test("issued random password grants standard Pro and full content; only hashes reach D1", async () => {
  assert.equal(password.length,32);
  const result = await call("sign-in",credentials());
  assert.equal(result.status,200); assert.equal(result.body.scope,"pro");
  assert.equal(result.body.explorePublishedContent,true); assert.match(result.body.sessionToken,/^[A-Za-z0-9_-]{43}$/);
  assert.equal(result.headers.get("Cache-Control"),"no-store");
  const row = await db.prepare("SELECT * FROM complimentary_session").first();
  assert.equal(row.token_hash,createHash("sha256").update(result.body.sessionToken).digest("hex"));
  assert.ok(!JSON.stringify(row).includes(password)); assert.ok(!JSON.stringify(row).includes(result.body.sessionToken));
  const renewed=await call("refresh",{packageName:"com.quietcamp.game",sessionToken:result.body.sessionToken});
  assert.equal(renewed.status,200); assert.equal(renewed.body.sessionToken,result.body.sessionToken);
});
test("wrong password/name/package, injected scope and tampered tokens cannot grant access",async()=>{
  for(const patch of [{password:"b".repeat(32)},{username:"someone-else"},{packageName:"com.other.game"},{scope:"all"}]) {
    const result=await call("sign-in",{...credentials(),...patch});
    assert.equal(result.status,401); assert.equal(result.body.sessionToken,undefined);
  }
  assert.equal((await call("refresh",{packageName:"com.quietcamp.game",sessionToken:"a".repeat(43)})).status,401);
});
test("atomic D1 rate limiter survives concurrent attempts and ignores forged X-Forwarded-For",async()=>{
  const results=await Promise.all(Array.from({length:16},(_,i)=>call("sign-in",{...credentials(),password:"b".repeat(32)}, {"X-Forwarded-For":"198.51.100."+i})));
  assert.equal(results.filter(r=>r.status===401).length,8);
  assert.equal(results.filter(r=>r.status===429).length,8);
  assert.ok(results.filter(r=>r.status===429).every(r=>Number(r.headers.get("Retry-After"))>0));
  await db.prepare("UPDATE complimentary_limit SET expires=0").run();
  assert.equal((await call("sign-in",credentials())).status,200);
});
test("database errors fail closed without private details, restored database restores access",async()=>{
  const signed=await call("sign-in",credentials());
  assert.equal(signed.status,200);
  await db.prepare("ALTER TABLE complimentary_session RENAME TO unavailable_session").run();
  const result=await call("refresh",{packageName:"com.quietcamp.game",sessionToken:signed.body.sessionToken});
  assert.equal(result.status,503); assert.deepEqual(result.body,{error:"Retry later"});
  await db.prepare("ALTER TABLE unavailable_session RENAME TO complimentary_session").run();
  assert.equal((await call("refresh",{packageName:"com.quietcamp.game",sessionToken:signed.body.sessionToken})).status,200);
});
test("credential/scope rotation and session retention revoke obsolete grants",async()=>{
  const signed=await call("sign-in",credentials());
  assert.equal(signed.status,200);
  await db.prepare("UPDATE complimentary_session SET generation=?").bind("a".repeat(64)).run();
  assert.equal((await call("refresh",{packageName:"com.quietcamp.game",sessionToken:signed.body.sessionToken})).status,401);
  const next=await call("sign-in",credentials());
  assert.equal(next.status,200);
  await db.prepare("UPDATE complimentary_session SET checked=0").run();
  assert.equal((await call("refresh",{packageName:"com.quietcamp.game",sessionToken:next.body.sessionToken})).status,401);
});
test("health queries D1 schema, malformed or oversized input is rejected",async()=>{
  assert.equal((await call("health")).status,200);
  assert.equal((await call("sign-in",[credentials()])).status,400);
  assert.equal((await call("sign-in",{password:"x".repeat(20000)})).status,400);
  await db.prepare("DROP TABLE complimentary_session").run();
  assert.equal((await call("health")).status,503);
});
test("scheduled retention removes expired hashes and preserves active session and limits",async()=>{
  const active=await call("sign-in",credentials()); assert.equal(active.status,200);
  await db.batch([
    db.prepare("INSERT INTO complimentary_session VALUES (?,?,0)").bind("a".repeat(64),"b".repeat(64)),
    db.prepare("INSERT INTO complimentary_limit VALUES ('expired',1,0)")
  ]);
  const pending=[];
  await worker.scheduled({}, {ACCESS_DB:db}, {waitUntil:promise=>pending.push(promise)});
  await Promise.all(pending);
  assert.equal((await db.prepare("SELECT COUNT(*) AS count FROM complimentary_session").first()).count,1);
  assert.equal(await db.prepare("SELECT * FROM complimentary_limit WHERE key='expired'").first(),null);
  assert.equal((await call("refresh",{packageName:"com.quietcamp.game",sessionToken:active.body.sessionToken})).status,200);
});
