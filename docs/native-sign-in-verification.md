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
