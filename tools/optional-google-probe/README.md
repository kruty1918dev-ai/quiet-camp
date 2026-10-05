# Optional Google adapter contract probe

Run with .NET 8 (also bundled with this project's Unity Editor):

```sh
DOTNET_CLI_TELEMETRY_OPTOUT=1 dotnet run \
  --project tools/optional-google-probe/Probe.csproj \
  --artifacts-path /tmp/quietcamp-google-probe
```

The project compiles the game's actual guarded adapter files with deterministic
SDK API doubles defined in `Program.cs`. It checks pending-task ownership,
reward versus dismissal, cancellation/disposal, offline behavior and the
Firebase consent boundary. It has no SDK, network, account or player saves.

Passing these scenarios does not establish API compatibility with an imported
Google SDK version, native Android behavior, mediation callback order or legal
compliance. Validate those on a device after configuring the real integrations,
as described in [the implementation guide](../../Documentation/COZY-SYSTEMS-UA.md).
