import { handleCloud, cleanupCloud } from './cloud.mjs';
// Explicit complimentary access, identical for everyone issued an account.
// Passwords/tokens never enter URLs, logs, D1 or the client configuration.
const encode = new TextEncoder();
const TOKEN = /^[A-Za-z0-9_-]{43}$/;
const PASSWORD = /^[A-Za-z0-9]{32}$/;
const LEASE_SECONDS = 86400, SESSION_SECONDS = 90 * 86400;
const MAX_BODY = 16384;

function json(status, value, headers = {}) {
  return Response.json(value, { status, headers: {
    "Cache-Control": "no-store", "X-Content-Type-Options": "nosniff", ...headers
  }});
}
function denied() { return json(401, { error: "Access denied" }); }
function unavailable() { return json(503, { error: "Retry later" }); }
function hex(bytes) { return Array.from(new Uint8Array(bytes), b => b.toString(16).padStart(2, "0")).join(""); }
async function digest(value) { return hex(await crypto.subtle.digest("SHA-256", encode.encode(value))); }
function bytesFromHex(value) {
  if (typeof value !== "string" || !/^[a-f0-9]{64}$/.test(value)) throw Error("Invalid account configuration");
  return Uint8Array.from(value.match(/../g), v => parseInt(v, 16));
}
function randomToken() {
  const data = crypto.getRandomValues(new Uint8Array(32));
  return btoa(String.fromCharCode(...data)).replace(/\+/g, "-").replace(/\//g, "_").replace(/=+$/, "");
}
async function configuration(env) {
  const account = JSON.parse(env.QC_ACCESS_ACCOUNT);
  if (account.schema !== 1 || account.kind !== "generated-alnum32-hmac-sha256-v1"
    || typeof account.username !== "string" || !/^[A-Za-z0-9_-]{1,100}$/.test(account.username)
    || !TOKEN.test(env.QC_ACCESS_PEPPER) || env.QC_ANDROID_PACKAGE !== "com.quietcamp.game"
    || env.QC_PRO_PRODUCT !== "quietcamp_pro" || !["true", "false"].includes(env.QC_EXPLORE_PUBLISHED) || !env.ACCESS_DB)
    throw Error("Access disabled");
  const expected = bytesFromHex(account.verifier);
  const rawKey = Uint8Array.from(atob(env.QC_ACCESS_PEPPER.replace(/-/g, "+").replace(/_/g, "/") + "="), c => c.charCodeAt(0));
  const key = await crypto.subtle.importKey("raw", rawKey, { name: "HMAC", hash: "SHA-256" }, false, ["sign", "verify"]);
  const generation = await digest([account.kind, account.username, account.verifier, env.QC_EXPLORE_PUBLISHED].join(":"));
  return { account, expected, key, generation };
}
async function allowed(db, key, maximum, now) {
  // A single SQLite statement is atomic across concurrent requests/isolates.
  const result = await db.prepare(`INSERT INTO complimentary_limit(key,count,expires) VALUES (?1,1,?2)
    ON CONFLICT(key) DO UPDATE SET count=CASE WHEN expires<=?3 THEN 1 ELSE MIN(count+1,1000000000) END,
    expires=CASE WHEN expires<=?3 THEN ?2 ELSE expires END RETURNING count,expires`)
    .bind(key, now + 600, now).first();
  if (!result) throw Error("Limit unavailable");
  return result.count <= maximum ? null : json(429, { error: "Retry later" }, { "Retry-After": String(Math.max(1, result.expires - now)) });
}
async function limited(request, env, config, route, now) {
  const address = request.headers.get("CF-Connecting-IP");
  if (!address || address.length > 64) throw Error("Trusted peer missing");
  const peer = hex(await crypto.subtle.sign("HMAC", config.key, encode.encode("quietcamp-peer-v1\0" + address)));
  const signIn = route === "sign-in";
  const limit = await allowed(env.ACCESS_DB, (signIn ? "signin:" : "refresh:") + peer, signIn ? 8 : 120, now);
  if (limit) return limit;
  return signIn ? allowed(env.ACCESS_DB, "signin:global", 200, now) : null;
}
async function readBody(request) {
  if ((request.headers.get("Content-Type") || "").split(";")[0].trim().toLowerCase() !== "application/json") throw Error("JSON required");
  const announced = request.headers.get("Content-Length");
  if (announced && (!/^\d+$/.test(announced) || Number(announced) > MAX_BODY)) throw Error("Body too large");
  const reader = request.body?.getReader();
  if (!reader) throw Error("Body required");
  const chunks = []; let size = 0;
  try {
    while (true) {
      const { done, value } = await reader.read();
      if (done) break;
      size += value.byteLength;
      if (size > MAX_BODY) { await reader.cancel(); throw Error("Body too large"); }
      chunks.push(value);
    }
  } finally { reader.releaseLock(); }
  const bytes = new Uint8Array(size); let offset = 0;
  for (const chunk of chunks) { bytes.set(chunk, offset); offset += chunk.length; }
  const value = JSON.parse(new TextDecoder("utf-8", { fatal: true }).decode(bytes));
  if (!value || typeof value !== "object" || Array.isArray(value)) throw Error("Object required");
  return value;
}
function exactKeys(value, keys) {
  const actual = Object.keys(value);
  return actual.length === keys.length && keys.every(k => Object.hasOwn(value, k));
}
function grant(env, token, now) {
  return json(200, { packageName: env.QC_ANDROID_PACKAGE, productId: env.QC_PRO_PRODUCT,
    scope: "pro", state: "Granted", explorePublishedContent: env.QC_EXPLORE_PUBLISHED === "true",
    sessionToken: token, validUntilUtc: new Date((now + LEASE_SECONDS) * 1000).toISOString() });
}
async function signIn(env, config, data, now) {
  if (!exactKeys(data, ["packageName", "username", "password"]) || data.packageName !== env.QC_ANDROID_PACKAGE
    || typeof data.username !== "string" || !/^[A-Za-z0-9_-]{1,100}$/.test(data.username)
    || typeof data.password !== "string" || !PASSWORD.test(data.password)) return denied();
  const valid = await crypto.subtle.verify("HMAC", config.key, config.expected,
    encode.encode("quietcamp-credential-v1\0" + data.username + "\0" + data.password));
  if (!valid) return denied();
  const token = randomToken();
  await env.ACCESS_DB.prepare("INSERT INTO complimentary_session(token_hash,generation,checked) VALUES (?,?,?)")
    .bind(await digest(token), config.generation, now).run();
  return grant(env, token, now);
}
async function refresh(env, config, data, now) {
  if (!exactKeys(data, ["packageName", "sessionToken"]) || data.packageName !== env.QC_ANDROID_PACKAGE
    || typeof data.sessionToken !== "string" || !TOKEN.test(data.sessionToken)) return denied();
  const result = await env.ACCESS_DB.prepare("UPDATE complimentary_session SET checked=? WHERE token_hash=? AND generation=? AND checked>=? RETURNING token_hash")
    .bind(now, await digest(data.sessionToken), config.generation, now - SESSION_SECONDS).first();
  return result ? grant(env, data.sessionToken, now) : denied();
}
export default {
  async fetch(request, env) {
    const url = new URL(request.url);
    if (url.pathname === '/v1/cloud' || url.pathname.startsWith('/v1/cloud/')) return handleCloud(request, env);
    if (url.protocol !== "https:" && url.hostname !== "localhost") return json(400, { error: "HTTPS required" });
    if (url.search) return json(400, { error: "Request rejected" });
    const route = url.pathname.match(/^\/v1\/access\/(sign-in|refresh|health)$/)?.[1];
    if (!route) return json(404, { error: "Not found" });
    if (request.method !== (route === "health" ? "GET" : "POST")) return json(405, { error: "Method not allowed" });
    try {
      const config = await configuration(env);
      if (route === "health") {
        const row = await env.ACCESS_DB.prepare("SELECT COUNT(*) AS count FROM sqlite_master WHERE type='table' AND name IN ('complimentary_session','complimentary_limit')").first();
        return row?.count === 2 ? json(200, { service: "quietcamp-complimentary-access", schemaVersion: 1 }) : unavailable();
      }
      const now = Math.floor(Date.now() / 1000);
      const limit = await limited(request, env, config, route, now);
      if (limit) return limit;
      let data;
      try { data = await readBody(request); } catch { return json(400, { error: "Request rejected" }); }
      return await (route === "sign-in" ? signIn(env, config, data, now) : refresh(env, config, data, now));
    } catch { return unavailable(); }
  },
  async scheduled(_controller, env, ctx) {
    if (env.QC_CLOUD_SCHEMA === '3') ctx.waitUntil(cleanupCloud(env));
    const now = Math.floor(Date.now() / 1000);
    ctx.waitUntil(env.ACCESS_DB.batch([
      env.ACCESS_DB.prepare("DELETE FROM complimentary_limit WHERE expires<=?").bind(now),
      env.ACCESS_DB.prepare("DELETE FROM complimentary_session WHERE checked<?").bind(now - SESSION_SECONDS)
    ]));
  }
};
