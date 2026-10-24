# Shared account API

Base https://quietcamp-access.oleksiy-havryliuk.workers.dev/v1/cloud.
Requires SQL migration 0004 and QC_ACCOUNT_PORTAL_ENABLED=true. Cloud protocol
remains 3, progress schema 2. HTTPS, JSON, no wildcard CORS.

GET /options retains providers and adds accountPortal/nativeSignIn booleans.

## Native client

POST /native/sign-in:

    {"provider":"google","mode":"signin","idToken":"FRESH_FIREBASE_ID_TOKEN"}

Provider google/facebook must match an enabled, Google-verified Firebase sign-in
provider. Project quiet-camp, auth_time within 10 minutes, not merely a refreshed
old session. Raw Google ID tokens and Facebook access tokens are not accepted.
Mode signin opens/creates that UID's profile. Mode link is explicit guest linking
and rejects ANY existing profile, including an empty website-created profile.
It is not Google↔Facebook provider linking.

Success retains the legacy login-poll session contract, with an extra profile:

    {"accountId":"uuid","sessionToken":"64-lowercase-hex","provider":"google",
     "created":false,"profile":{"accountId":"uuid","displayName":"Camper",
     "email":null,"providers":["google"],"created":1792830000,
     "lastActivity":1792830000,"cloudUpdated":null,"hasCloudSave":false,
     "isAdmin":false,"serverTime":1792830000}}

The opaque session uses existing bearer /read /write /status /sign-out. /status
also adds profile. Persist using CloudCredentialStore/Keystore, not saves/logs/
URLs/PlayerPrefs. Do not copy Firebase refresh tokens into game saves.

A website-created profile returns created:false with possibly no save. /read
then returns {revision:0,snapshot:null,updated:0}. Use normal signin plus existing
read/restore/conflict/CAS logic, not another UID, a fake created:true or blind
cloud overwrite. Reject late guest merging/resource addition as before.
Cancel/dispose/failed credential persistence must revoke any late-issued session.
Do not forge Origin to call old login-complete: native has its own token boundary.

POST /native/delete:

    {"idToken":"FRESH_FIREBASE_ID_TOKEN","confirm":true}

Target is the freshly verified identity, not a client-supplied accountId/UID.
No old game session is required, allowing retries after partial deletion has
revoked all sessions. Game MUST match reauthenticated identity to its displayed
account before calling this destructive endpoint. Success {deleted:true} removes
cloud account/save/profile, all web/game sessions/handoffs and Firebase Auth
record, not social accounts/local progress/purchases/complimentary-Pro identity.

Errors: 400 confirmation_required/provider_unavailable; 401 identity_rejected/
reauthenticate; 409 identity_already_linked/deletion_pending; 429 rate_limited;
503 deletion_retry/account_portal_unavailable/retry_later. deletion_retry means
D1 game data was removed but provider cleanup needs a fresh-auth retry. A minimal
UID tombstone blocks concurrent resurrection; daily cleanup removes completed
markers after 24h. Pending failures remain blocked until resolved. On stale 401
stop sync, clear credentials, keep local progress and require explicit new login.

## Website and owner directory

GET /account?lang=en|uk|de and /admin?lang=... are nonce-CSP, noindex/no-store
HTML. Firebase loads on interaction with in-memory persistence. No credentials
in URLs or localStorage. Same-origin JSON POST routes:

- /web/sign-in {provider,idToken,expectedAccountId?}: returns profile/created,
  rotates __Host-qc_account HttpOnly Secure SameSite=Strict cookie, Path=/,
  Max-Age=3600. expectedAccountId protects reauth/linking against account swaps.
- /web/session {}: own profile or 401.
- /web/profile {displayName}: 1–40 Unicode characters, NFC, no controls/bidi
  overrides. Other identity/role fields are ignored. Response profile.
- /web/sign-out {}: ends this browser session only; clears cookie.
- /web/delete {idToken,confirm:true}: fresh identity must match any current web
  profile; fresh-proof cookie-less retries are allowed after session revocation.
- /admin/accounts {query?,cursor?}: owner Firebase UID allow-list checked on
  EVERY request, exact ID/UID/email/name search, keyset pagination (50 entries).
  Returns only minimal account fields/save timestamps/device-session count.
  No save payloads, passwords, session tokens or token hashes.

Website provider linking uses Firebase linkWithPopup on an already confirmed
current user, then server-verified metadata refresh. No merge based on matching
email and no automatic merging of occupied Firebase identities. No password
registration or unlink-last-provider flow is offered.

Old game browser login/pairing and deletion form remain compatible after SQL
migration. Old browser login now enriches minimal verified profile metadata too.
