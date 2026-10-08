# Region compiler and regression fixtures

Run from repository root. Uses the actual portable domain/compiler/validator/catalog and the cached Newtonsoft assembly; no Unity launch, player build, solver or save access.

```sh
dotnet build tools/roadmap-pipeline/RoadmapPipeline.csproj -m:1 -p:UseSharedCompilation=false
dotnet run --no-build --project tools/roadmap-pipeline/RoadmapPipeline.csproj -- --export
```

`--export` reads author-owned `Assets/QuietCamp/Authoring/Roadmap/*.json`, rebakes derived props and writes resources plus synthetic QA fixtures. Without it, checks only. Current main/journey puzzle JSON is read-only. If no authoring exists, creates the initial draft once. Subsequent exports preserve layout, branches, historical metadata and story props. Do not delete authoring files to regenerate an established route.

Checks main summary parity, model/localization/preview references, validation failures, reveal bounds, region-window budgets, authoring preservation and shoreline exclusion. Generates a 360-node/36-branch dataset, a separate 180-node story route, and four historical/coastal visual profiles outside runtime Resources. The story benchmark uses its own synthetic IDs; neither dataset enters the playable catalog.

Planner timings are .NET CPU timings, not Unity frame time. Zero-allocation assertions cover only warmed pure window/slot planners and climate sampling, not the Unity renderer. Native counters come from `RoadmapBenchmarkHarness`/PlayMode tests, and mobile performance requires separately authorized device profiling.

See [pipeline documentation](../../Design/Roadmap/PRODUCTION-PIPELINE-UA.md). Host resource rules apply. Build intermediates live under `/tmp/quietcamp-roadmap-pipeline`, not the game project.

The 3D foundation adds a validated seven-level slice and `benchmark-world360.json`. `RoadmapCompiler.BuildWorld` emits explicit chunks (up to six nodes); regions may contain multiple chunks. The native renderer is selected by `presentation: "world3d"`. The main campaign now selects world3d, retains all thirty puzzle IDs, and uses the authored seven-level opening; separate legacy journey catalogs remain supported. See [foundation format and native test workflow](../../Design/Roadmap/FOUNDATION-3D-UA.md).

## MH17-inspired design contract

```sh
dotnet run --no-build --project tools/roadmap-pipeline/RoadmapPipeline.csproj -- --mh17-design
```

Reads the [unpublished region draft](../../Design/Roadmap/Regions/quiet-field/region-draft.json), story catalog, planned asset contracts and draft labels. Uses the real validators/compiler to check structure, one compound roadmap landmark, exclusion of level-only clues, claim/source bindings, care/snapshot declarations and progression restart/replay. Eight negative cases reject evidence leakage, duplicate landmarks, unsupported claims, the wrong weapon reference, invented serials and falsely approved publication/check states.

Declared asset IDs, navigation labels and future preview identities are used only for draft shape validation. A separate check against **actual** model catalogs must fail with missing assets at this stage. This expected release block is part of the audit output, not a passed production content check. No playable levels, solver witnesses, artwork review, renderer/inspection behavior or mobile performance are certified. The command does not export or modify campaign resources. See [design and factual boundaries](../../Design/Roadmap/MH17-REGION-UA.md).

## Black Sea-inspired design contract

```sh
dotnet run --no-build --project tools/roadmap-pipeline/RoadmapPipeline.csproj -- --coast-design
```

Reads the [unpublished eight-level coastal draft](../../Design/Roadmap/Regions/grain-coast/region-draft.json), source-bound story catalog, planned assets, labels and [ship topology reference](../../Design/Roadmap/Regions/grain-coast/ship-topology-draft.json). Real validators/compiler check roadmap/detail separation, one ship landmark, warning/care declarations, metadata snapshots and bounded progress windows. Fourteen negative cases cover disclosure, hazard/publication flags and layered placement constraints.

The offline layered graph is a **design reference**, not production rule version 3 or an independent puzzle solver. It checks a declared arrangement with surface-specific occupancy/shade, headroom, explicit stairs and alternate routes. The real flat evaluator must reject its collapsed equivalent as overlap, documenting the runtime work still required. The early ship teaser is also a presentation contract, not an implemented renderer.

Actual model-catalog checks remain blocked by missing planned artwork; the eight puzzles/witnesses are not authored. No campaign resources, saves, Unity assets or player builds are modified. See [design, sources and implementation gates](../../Design/Roadmap/BLACK-SEA-REGION-UA.md) and [audit results](../../TestResults/black-sea-region-design-2026-10-07/REPORT_UA.md).

## Kakhovka-inspired design contract

```sh
dotnet run --no-build --project tools/roadmap-pipeline/RoadmapPipeline.csproj -- --water-memory-design
```

Reads the [nine-node unpublished region](../../Design/Roadmap/Regions/water-memory/region-draft.json), story/source/asset contracts, draft navigation labels and [crossing topology reference](../../Design/Roadmap/Regions/water-memory/crossing-topology-draft.json). Real validators/compiler check one hydro landmark, one stranded-boat owner, map/detail separation, fictional evidence boundaries, bounded progress windows and metadata snapshots. Regional contracts separate drained upstream shore from downstream flood traces; these are authoring checks, not a rendered geography review.

The offline graph uses production tent footprint/door shapes on three dry surface masks. Two explicit bridges provide alternate routes over a river gap; one ramp is the only path to the higher terrace. Removing both bridges disconnects the far bank, and removing the ramp disconnects the terrace. It does not implement runtime v3, weighted-route rules, physical bridge safety or an independent solver. Care cannot change puzzle topology, restore the dam, depict dead animals or turn river water into drinking water.

Planned models, nine playable puzzles and early within-region landmark presentation remain release blockers. The command checks only and does not export into campaign resources, access player saves or launch Unity/build a player. See [design and primary sources](../../Design/Roadmap/KAKHOVKA-REGION-UA.md) and [QA](../../TestResults/kakhovka-region-design-2026-10-07/REPORT_UA.md).

## Targeted main rollout

```sh
dotnet run --no-build --project tools/roadmap-pipeline/RoadmapPipeline.csproj -- --export-main
```

Runs the portable contracts, then writes only the main world presentation resource. Keeps puzzles, summaries, journeys, historical fixtures and saves unchanged. Validates retained cache slots before new identities are assigned, including reverse traversal and 10,000 allocation-free window changes.
