# Optional Google adapter contract probe

Run with .NET 8 (also bundled with this project's Unity Editor):

```sh
dotnet build tools/optional-google-probe/Probe.csproj --artifacts-path QuietCamp/Temp/ai/google-probe
dotnet QuietCamp/Temp/ai/google-probe/bin/Probe/debug/Probe.dll
```

The project compiles the game's actual guarded adapter files with deterministic
SDK API doubles defined in `Program.cs`. It also compiles the actual Google
configuration and publication gate. It checks sample-ID selection, development-only
activation, publisher-unit consent, pending-task ownership,
reward versus dismissal, cancellation/disposal, offline behavior and the
Firebase consent boundary, shared Firebase dependency initialization, separate Crashlytics choice, previous-session report eligibility, withdrawal and failed consent-save behavior. It has no SDK, network, account or player saves.

Passing these scenarios does not establish API compatibility with an imported
Google SDK version, native Android behavior, mediation callback order or legal
compliance. Validate those on a device after configuring the real integrations,
as described in [the implementation guide](../../Documentation/COZY-SYSTEMS-UA.md).
