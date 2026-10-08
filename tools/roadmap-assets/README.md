# Roadmap model kit and matrix

From the repository root, Python 3.11+; install the versions in `requirements.txt`
into an isolated Python environment. NumPy/Pillow handle source inspection;
fast-simplification is the QEM implementation, not a Unity package dependency.

```sh
python tools/roadmap-assets/build_environment_kit.py --register --preview
python tools/roadmap-assets/optimize_ship.py --preview
python tools/roadmap-assets/inventory.py
python tools/roadmap-assets/verify.py
python tools/build_ukrainian_roadmap_models.py --check
python tools/roadmap-assets/build_environment_kit.py --check
python tools/roadmap-assets/optimize_ship.py --check
python tools/roadmap-assets/inventory.py --check
dotnet build tools/roadmap-pipeline/RoadmapPipeline.csproj -m:1 -p:UseSharedCompilation=false
dotnet run --no-build --project tools/roadmap-pipeline/RoadmapPipeline.csproj -- --composition validate
dotnet run --no-build --project tools/roadmap-pipeline/RoadmapPipeline.csproj -- --composition compose --dry-run
python tools/scene-composition/test_cli.py
```

Run checks sequentially. `--check` never writes. The original base exporter owns
only its `UkrainianRural` folder, preserving the pre-existing `Models.meta` GUID.
Kit registration updates only authoring asset metadata. Shape/material exports
are deterministic and source meshes remain editable. Matrix inventory covers all
five roadmap mesh streams and raw geometry files under Assets; FBX triangle
counts are left unknown pending native import.

`optimize_ship.py` accepts only the existing credited donor SHA-256. It retains
the original source GLB/CC BY attribution, welds common vertices before QEM to
avoid color-boundary cracks, maps donor face colors to five project colors and
exports all LODs with one origin/height. Full and coarse detail are reduced;
the smallest LOD deliberately uses more triangles than the old grid-clustered
silhouette to preserve the hull. No new Sketchfab download is implied.

`sketchfab-candidates.json` records public source research and the actual download
blocker. Most candidates still require exact license-version and shape review
after download. Do not import candidate geometry based on a title alone. Verify
commercial use and repository redistribution, preserve author/source/license,
retain a source hash, and register only the adapted asset's actual bounds/LOD.
The original procedural fallbacks are explicitly labeled separately.

The new kit is Editor-only. Source and affected-chunk hashes include its model
streams and field/LOD dependencies. Runtime publication requires native Bake
Main/Bake Staging and actual scene inspection under AGENTS.md. No player build or
fresh Unity launch is part of this workflow. The software contact sheets inspect
real triangles but do not establish native appearance, performance or seasonal QA.

See [the Ukrainian matrix](../../Design/Roadmap/Assets/MODEL-MATRIX-UA.md).
