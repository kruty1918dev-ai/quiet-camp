# Roadmap audit harness

Read-only investigation of the current roadmap. It changes neither campaign nor saves and launches no Unity player build.

Run from the repository root:

```sh
python3 tools/roadmap-audit/resource_audit.py
nice -n10 ionice -c2 -n7 dotnet build tools/roadmap-audit/RoadmapAudit.csproj -m:1 -p:UseSharedCompilation=false
nice -n10 ionice -c2 -n7 dotnet run --no-build --project tools/roadmap-audit/RoadmapAudit.csproj
```

Requires .NET 10 and the project's cached Newtonsoft DLL at the path in the csproj. Outputs/intermediates go to `/tmp/quietcamp-roadmap-audit`; JSON evidence goes to `TestResults/roadmap-audit-2026-10-07`. Check host resource instructions before compiling.

`Program.cs` compiles the **actual** roadmap orchestration, layout, scroll binding, domain, progression, journey access and bonus access source. Unity APIs, drawing and scene generation are deliberately replaced with `PlatformDoubles.cs`. Reflection invokes the actual private lifecycle methods.

It measures control-flow operation counts on 30/300/1000 in-memory summaries and reproduces current behavior: ordinal pool churn, reduced-motion mesh invalidation, scroll binding failure after disable/reconfigure, and non-atomic initialization after an injected scene error. Assertions describe today's defective behavior; **these are characterization tests, not acceptance tests asserting that bugs should remain**. Replace expectations with corrected contracts during migration.

The Python script validates current frozen-resource ordering, summary parity, model structure, finite vertices and object model references. Its three-region lease exercise is a **design feasibility simulation**, not a test of an implemented chunk renderer. It records source SHA-256 and the inspected Git HEAD for repeatability.

Neither tool measures native UI layout/rebuild cost, actual scene generation, snow traversal cost, vertex uploads, shader compilation, GPU overdraw, allocation bytes, native object ownership, frame time or mobile FPS. Native Unity/Profiler verification remains necessary. Synthetic IDs never enter the game catalog and no save files are used.
