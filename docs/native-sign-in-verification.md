# Native account integration — 2026-10-24

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
prove a native chooser, production website↔game accountId, Firebase traffic,
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
