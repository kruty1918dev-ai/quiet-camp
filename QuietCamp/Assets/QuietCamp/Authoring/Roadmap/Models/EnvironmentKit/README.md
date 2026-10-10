# Environment kit — authoring only

31 original project-authored meshes, 5,308 triangles including LODs. These are
not Sketchfab downloads or derivatives of the waiting Sketchfab candidates.
Existing `build_ukrainian_roadmap_models.py` palette and primitives are reused.
No copied third-party mosaic artwork, texture atlas, flag, trident or text.

OBJ/MTL files are generator outputs; edit `tools/roadmap-assets/build_environment_kit.py` and regenerate. The source meshes in metres, Y up; `models.json` is a
height-normalized flat triangle stream for the Editor model library. Each mesh's
metric height, complete bounds, triangle budget and source hash are recorded in
`manifest.json`. `Composition/assets.json` carries collision/placement metadata,
the standing transmission tower's conductor anchors and bridge approach sockets.

```sh
python tools/roadmap-assets/build_environment_kit.py --register --preview
python tools/roadmap-assets/build_environment_kit.py --check
```

The kit loads only under `UNITY_EDITOR`; it is outside Resources. Model streams
participate in the native baker's source and affected-chunk hashes. The cinematic pilot uses selected meshes after a native bake; the historical
Main catalog is preserved. Native captures and Game View verification are recorded
separately in the current pilot gallery. No player build or mobile FPS measurement
is included.

The whole-lot, roadside-stop, damaged wire and dam hydrology requirements are
tracked in [the model matrix](../../../../../../../Design/Roadmap/Assets/MODEL-MATRIX-UA.md).
Having a mesh does not validate its placement. In particular the dam needs a
compound support/terrain recipe around its open breach before placement; its
open central channel is not a traversable bridge. The fictional pond embankment,
spillway, collapsed house corner, plain well and connected perimeter modules are
described in [the style research](../../../../../../../Design/Roadmap/StyleCoherence/2026-10-10/RESEARCH-UA.md). Fallen pylon has no live
conductor anchors. Chicken is local-yard/gameplay detail rather than a large map
landmark. The sweep well/ruined hut are alternatives, not features to duplicate
at every level.
