# Native account integration â€” 2026-10-24

The native account application boundary accepts a fresh Firebase identity proof,
then exchanges it at `native/sign-in`. It retains the existing credential store,
read/restore/conflict and CAS pipeline. A website-created empty profile uses the
normal read and revision-zero write; guest linking remains a separate operation.
Returning guests signing into an existing saved profile receive a conflict choice.

Deletion requires explicit confirmation and a fresh matching Firebase UID. Legacy
browser credentials verify accountId through a temporary native session, which is
revoked without applying its save. A durable identity marker blocks sign-in after
partial or uncertain deletion, permitting fresh-auth retry without an old bearer.
Local progress is retained. Sign-in never enables optional tracking purposes.

Portable verification:

```sh
dotnet run --project tools/privacy-probe/Probe.csproj -- --test=QuietCamp.Tests.NativeCloudAccountTests,QuietCamp.Tests.CloudSaveTests,QuietCamp.Tests.Editor.AndroidPrivacyBuildTests --workers=1 --noresult
```

The initial application-contract milestone passed 63 tests. These use the actual
application code with deterministic identity/network/storage doubles. They do not
prove a native chooser, production websiteâ†”game accountId, Firebase traffic,
Android Keystore, or device deletion. Android bridge/API compilation and device
evidence are separate checks. No player build is authorized.

The Android bridge now compiles with Java 17 against the actual Android 35,
AndroidX Credentials 1.6.0 and googleid 1.2.1 APIs. The Unity optional adapter
compiles against the original Firebase Auth 14.0.0 DLL, matching the installed
Core suite. SDK detection registers it only for Android players; Editor keeps
browser authentication. The full portable privacy probe passed 212 tests with
one worker, including native-account, existing cloud-save and manifest contracts.

```sh
python tools/check_native_auth_bridge.py --android-player <Unity-AndroidPlayer-directory>
python tools/verify_monetization.py --google-sdk-check --player-source-check --android-source-check
dotnet run --project tools/privacy-probe/Probe.csproj -- --workers=1 --noresult
```

The Java API check compiles only the bridge's class files in ignored Temp. The
C# check uses cached Unity references and the real SDK DLLs without importing
Unity, resolving a full Gradle player, packaging or deploying anything.

Auth always starts with an interactive Google credential and new Firebase
sign-in, then requests `TokenAsync(true)`. The Firebase client session is signed
out after obtaining the proof; only the Worker credential persists in the
existing Keystore/noBackupFilesDir store. Firebase Auth may transiently write
its own SharedPreferences during sign-in. Android backup and device-transfer
rules exclude SharedPreferences, preserving file-based game saves. Final merged
manifest, on-device files/traffic, process death and transport behavior still
require Android evidence. No Auth startup grants Analytics or Crashlytics consent.

The bridge uses the official explicit-button
[GetSignInWithGoogleOption flow](https://developer.android.com/identity/sign-in/credential-manager-siwg-implementation),
the [stable Credentials release](https://developer.android.com/jetpack/androidx/releases/credentials),
and [Firebase's Unity credential exchange](https://firebase.google.com/docs/auth/unity/google-signin).

The supplied refreshed Firebase file passed project, project-number, package,
Android App ID, Android OAuth/Play SHA-1 and Web OAuth checks and was installed
at ignored `QuietCamp/Assets/google-services.json`. The same Firebase object was
updated in ignored `Config/owner.local.json`; validate/readiness/prepare/apply
preserved every other owner value and all review flags. OAuth metadata now
survives the generator's explicit allow-list. Fourteen owner-config tests and
six policy-sync tests passed. Neither ignored file is committed.

The account view chooses native Google only when both the SDK and server
`nativeSignIn` capability are present. Editor and Facebook use the browser;
Google also has an explicit browser fallback after a native failure. Ordinary
sign-in opens an existing profile; guest linking is separately labelled.
Settings show escaped profile name/email/providers, the account portal and
confirmed native deletion/retry, while local erasure remains separate. The
existing local simplified startup/UMP edits are preserved; only native
integration hunks are staged from files that were already modified.

Existing signed APK evidence (read-only `apksigner verify --print-certs` and
`aapt2 dump badging`, no build):

- File: `QuietCamp/Builds/Android/QuietCamp-session-logs.apk`.
- APK SHA-256: `06f00f5b78390b0ed3322f039d2d340e2d15726d4a0d656b9a67741d24b8e9cc`.
- Package: `com.quietcamp.game`.
- Activity: `com.unity3d.player.UnityPlayerGameActivity`.
- Certificate SHA-1: `47:99:10:6F:0A:C2:DE:97:0B:C2:DD:3C:6D:9D:51:12:A8:21:EF:3C`.
- Certificate SHA-256: `79:DD:C9:BB:E4:E6:63:1E:60:34:6E:37:F4:7C:F9:3C:72:FB:FC:B7:21:12:F2:22:C2:E7:76:1E:B3:20:C3:BB`.
- Derived Meta Key Hash: `R5kQbwrC3pcLwt08bZ1REqgh7zw=`.

Register these certificate fingerprints on the existing Firebase Android app
for direct installations signed by **this existing APK's certificate**, alongside
the already registered Play SHA-1. This is evidence about the existing artifact,
not a promise that a future owner keystore will use the same signer. Three older
MVP APKs have the same certificate but their obsolete package is
`com.kruty1918.quietcamp`; do not register that package for this integration.
No signing key or keystore was generated, replaced or modified.

Reproduce for the current-package artifact:

```sh
python tools/inspect_android_signing.py --apk QuietCamp/Builds/Android/QuietCamp-session-logs.apk --android-player <Unity-AndroidPlayer-directory>
```

Native Meta is not activated: only App ID `1476186057735345` is supplied. Missing
inputs are the supported Meta Unity/Android SDK, its **Client Token**, and console
Android registration for package `com.quietcamp.game`, activity
`com.unity3d.player.UnityPlayerGameActivity` and the key hash above (plus the
Play-signed key hash for Play distribution). After installation the SDK's login
activity/provider manifest entries must be merged and Firebase Facebook provider
configuration verified. The Meta App Secret stays in Firebase Console. Auto Init,
App Events and advertiser-ID collection are explicitly disabled in the privacy
manifest. Existing browser Facebook code is retained; no production Facebook
login was performed by this change.

Remaining Android acceptance: full Gradle dependency/import/manifest validation;
physical Credential Manager UI and lifecycle/cancel/background behavior; an
actual fresh Firebase-to-Worker exchange and website?game accountId comparison;
empty website profile read/CAS; save restore and simultaneous-device conflict;
offline/process restart and credential erasure; switching Google users;
confirmed deletion, retry/pending and no automatic account resurrection. Use QA
accounts and current signing registration. No production account was deleted,
no device/ADB session was accessed, and no player build was launched.

A read-only production `/options` request with an explicit client User-Agent
returned HTTP 200, providers google/facebook and both `accountPortal:true` and
`nativeSignIn:true`. An initial generic urllib request received HTTP 403 from
the edge; this was not an authenticated account test. Native and Editor current
sources compiled using cached Unity references. The staged native integration
was also compiled separately from the unrelated local startup/privacy edits,
against actual SDK APIs, before commit.

The final staged-source portable run passed 215 tests with one worker. Its
count differs from the local-worktree run because pre-existing, uncommitted
privacy tests are intentionally not bundled with the native integration.
