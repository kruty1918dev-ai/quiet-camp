# Semantic scene composition

Cinematic pilot, 2026-10-10: new continuous-world sources live in
`Composition/CinematicPilot`; `CinematicRoadmapBaker` publishes five native chunks
for QC001–QC005. The projected map and `UI/Prepared` renderer have been retired.
Native capture and gameplay acceptance are pending; historical galleries retain
 their original dates. See [the pilot status](../../Design/Roadmap/CinematicPilot/2026-10-10/README.md).

An AI-friendly, deterministic authoring workflow for connected places, prepared native meshes and bounded streaming. The first integration is Quiet Camp's roadmap. The portable `QuietCamp.Composition` engine has no Unity/game/progression dependencies; release it as a separate UPM only after the integration is validated.

## Quick start

Requires the installed .NET SDK used by `RoadmapPipeline.csproj` and Python 3 with `jsonschema`. `compare` also uses Pillow; rebuilding staging models also uses NumPy. The versions exercised on this host are recorded in the QA report. Do not regenerate source recipes with the historical migration script.

From the repository root, one verification job at a time:

```sh
dotnet build tools/roadmap-pipeline/RoadmapPipeline.csproj -m:1 -p:UseSharedCompilation=false
dotnet run --no-build --project tools/roadmap-pipeline/RoadmapPipeline.csproj -- --composition validate
dotnet run --no-build --project tools/roadmap-pipeline/RoadmapPipeline.csproj -- --composition inspect r0-homestead-0
dotnet run --no-build --project tools/roadmap-pipeline/RoadmapPipeline.csproj -- --composition compose --dry-run
```

`inspect` returns the entity, children, source filename and exact file SHA-256. Put a merge patch in a separate JSON file, then:

```sh
dotnet run --no-build --project tools/roadmap-pipeline/RoadmapPipeline.csproj -- --composition patch r0-homestead-0 --expected-hash HASH_FROM_INSPECT --patch-file /tmp/my-composition-patch.json
```

The command rejects identity/schema changes, stale files, unknown fields and invalid placements before replacing the source. Arrays replace arrays; objects merge recursively. Never patch `Resources` or the generated transforms. Example patch: `{"fenceVariant":"picket"}`.

`bake` prepares portable placements and diagnostics; it **does not publish native runtime meshes**. Open Unity using the repository's host/ADB rules and choose **Quiet Camp → Composition → Bake Main** for final native resources. The runtime catalog changes only after all chunks validate. Failed baking retains the previous published catalog. **Preview** opens actual native mesh/shader inspection; Load/Frame accepts an entity ID, and overlays identify footprints and connections. Capture exports PNG and an ID sidecar. A fresh Editor is never silently launched by the CLI. No command builds a player.

`compare <before-sidecar.json> <after-sidecar.json>` compares scene identities and metrics. Keep matching PNG files beside sidecars for visual review; numeric comparison cannot decide beauty.

## Data ownership

`Assets/QuietCamp/Authoring/Roadmap/Composition/manifest.json` references compact region documents, `assets.json` and `templates.json`. Positions are metres: X right, Z forward, Y up. Region Z is local; the game adapter adds the region offset and projects onto scroll. Stable asset dimensions describe their chosen diorama size, including footprint; role height overrides are explicit. `sourceHeight` and pivot describe original geometry. `placementClass` is `solid`, `canopy`, `groundcover` or `boundary`; power supports additionally declare `supportKind` and conductor sockets. A role parent denotes semantic ownership; role coordinates remain local to the ensemble, rather than accumulating parent transforms. Source models retain their original metre dimensions/provenance.

- Region source: nodes, zones, routes, ensemble intentions, landmarks and budgets.
- Template source: role transforms, mandatory/optional dependencies, parcel dimensions and entrances.
- Asset catalog: footprint, bounds, sockets, LOD, wind, seasonal compatibility and source provenance.
- Generated output: placement identities, accepted relationships, diagnostics and native chunk assets.
- Runtime: three nearby chunk leases, shader motion, progression-derived reveal and native controls.

Legacy `node.world` data remains historical presentation data. The cinematic pilot uses authored world coordinates. It is not a puzzle solution or source authoring. Gameplay JSON, save IDs and content hashes never change during composition.

## Composition rules

Composition resolves all documents in manifest order before chunk ownership. Neighbor-region clearings, landmarks and accepted parcels participate in admission. Duplicate world IDs and mismatched declared road continuations are errors.

An ensemble is accepted as a unit. Its mandatory roles are not independently clipped. The planner searches at most 64 stable candidates keyed by entity ID and seed. An invalid parcel produces `ensemble-does-not-fit`, rather than half a house/yard. Optional roles may be absent only when required roles don't depend on them.

A homestead owns its boundary, gate, well and orchard. A gate lies on a fence boundary and gets a route to an explicit road. A roadside stop has its own entrance and road connection. An isolated damaged fence needs an explicitly ruined template with a foundation/context owner.

Roads/path corridors own entrances. Power/distribution corridors own supports and spans; distribution routes need service targets. Straight spans remain straight; geographic control points change the route. Conductors use the accepted support anchors, not a guessed next strip. `nextRoute` joins named networks at an identical endpoint. Power and distribution support types cannot substitute for each other. Terrain support, parcel clearance and routes are hard constraints. Entrances are tested along the complete approach before any yard children are admitted; a rejected approach causes another candidate search. Oriented asset footprints also reject colliding mandatory template roles.

Landscape areas currently use compact rectangular zones, with irregular procedural vegetation/edge variation filled offline. Fields take precedence over forest zones. Short cover, forest clusters and accents use separate budgets; route and entrance exclusions protect readability. Seasons are sampled continuously through the game adapter and never change ownership.

## Diagnostics and iteration

Important error codes: `duplicate-id`, `invalid-zone`, `missing-zone`, `missing-template`, `missing-role-asset`, `orphan-role`, `optional-parent`, `dependency-cycle`, `gate-without-boundary`, `disconnected-entrance`, `ensemble-does-not-fit`, `invalid-span`, `unsupported-pylon`, `missing-service-target`, `missing-reveal-owner`, `instance-budget`.

Errors carry entity IDs, `sourceKind`, JSON pointers and related IDs where relevant. See [the diagnostic reference](DIAGNOSTICS.md). Fix the intention/template, validate, dry-run, bake, then inspect native images. Semantic validity does not certify the artwork. Record both rejected candidates and passed render checks honestly.

## Native output and compatibility

The published catalog holds a separate immutable resource string and source revision for each chunk in `bakedBindings`. Portable placement hashes include the executing compiler assembly and relevant game inputs; they are distinct from native output hashes. Local edits can reuse unaffected native chunk assets; baker, geometry, shader, asset and climate dependencies invalidate affected outputs. The catalog has no direct references that eagerly load the whole campaign. Chunks contain native indexed meshes with UVs, vertex colors, baked local occlusion and identical main/shadow wind masks. Each chunk owns nodes and branches; meshes are prepared offline. Current chunk granularity is at most three main nodes to keep cache/uploads small. This does not alter progress order.

Native resources have catalog revision/source hash and conservative bounds. Runtime rejects stale resources, releases resources leaving the nearby window and releases canceled async requests when they complete. Requests share a reference-counted native lease, preventing an old cancellation callback from unloading resources acquired during immediate reentry. Low/High mesh variants are baked offline; identical variants share mesh subassets. Caps: 3 chunks, 1 camera/RT, 6 materials, 32 renderers, a conservative 64 MiB presentation estimate (unique mesh payloads + one color/depth RT + 1 MiB metadata reserve). Each chunk is capped at 15 MiB, and the RT at 2 Mi pixels (estimated 16 MiB). These are estimates, not measurements of Unity/driver/global UI memory. Unity load/upload frame costs must still be measured; async loading alone doesn't guarantee a 2 ms frame budget.

Legacy region/model exporters refuse to overwrite a published native composition. Use Bake Main for updates. Return scroll keeps the node identity and resets an offset after a layout revision change. Legacy catalogs remain supported.

## Verification

Portable contracts test unit admission, gates/roads, supports, ID ownership, deterministic compilation, local-edit stability and negative cases. Native gallery tests cold runtime without model JSON, seasons, projection, orientation, cache/chunk caps and zoom. Screenshot review and device performance remain separate acceptance gates. Follow AGENTS.md; player builds require a fresh user request.


## Working examples

`examples/spring-homestead.json`, `summer-field.json` and `winter-corridor.json` are complete region recipes using the main asset/template catalogs. They show owned yards with approaches, connected field/forest zones and a geographic power corridor. They are reference snapshots; production edits belong to the manifest's region files.

`inspect r0-homestead-0/house` resolves a generated child to its patchable owner. Template IDs and asset IDs can also be inspected and patched. Use `--expected-hash` from the exact file returned by inspect; do not reuse a hash from another source file. Bounds/connection overlays and projected IDs let an observed defect become a small source patch.

The first native bake compiles all 30 main levels. Staging aircraft/ship studies use **Bake Staging Landmarks**, followed by the corresponding Preview gallery. Their original assets, adaptation hashes and licenses are in `Authoring/Roadmap/Models/StagingLandmarks/ATTRIBUTION.md`; they are anonymous fictional studies and never unlock historical puzzles. A staging water zone is a composition placeholder, not finished reflective water.

## Acceptance and current activation

See [the Ukrainian usage guide and activation status](USAGE_UA.md). Until the first native bake and visual review pass, the published campaign retains its legacy catalog. Passing portable contracts or compiler checks does not activate the new visuals or prove native streaming, visual quality, draw calls or mobile FPS.

The native `CompositionRoadmapPlayModeTests` gallery must run before its 360-node benchmark. The benchmark aliases real 30-level meshes across 360 logical nodes/60 regions; it stresses bounded residency and navigation, rather than producing 360 distinct artworks. Separate existing navigation/reveal/branch tests remain required. The static preview draws the same native meshes but is not a replacement for Game View, progression or touch QA.

Immutable old hash directories are retained to protect rollback. Before any separately authorized player build, identify unreferenced revisions and quarantine them outside `Resources`; leaving all historical native resources there would increase package size. No automatic destructive pruning or player build is part of this tool.
