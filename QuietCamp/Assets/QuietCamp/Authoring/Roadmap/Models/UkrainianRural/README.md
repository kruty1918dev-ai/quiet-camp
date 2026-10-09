# Original Ukrainian rural roadmap kit

Twelve original low-poly models plus two crop LOD meshes for Quiet Camp's roadmap
presentation. The kit is
not a reconstruction of a particular village, electrical installation or event.
Its Ukrainian rural identity comes from the composition of agricultural fields,
forest belts, modest village houses, utility infrastructure, orchards and fences.
These individual motifs are also found elsewhere; none is claimed to be exclusive
to Ukraine.

All geometry is authored by the deterministic Python generator. No third-party
models, photographs, Sketchfab geometry or texture images were copied. Assets are
project-owned original geometry and follow the project's distribution terms;
this file does not grant a separate third-party asset license.

OBJ sources use metres, Y up, and a bottom-ground pivot. `palette.mtl` is an
editable source palette, not a request for independent runtime materials.
`manifest.json` gives exact dimensions, source heights, SHA-256 and triangle caps.
Runtime JSON is `Resources/QuietCamp/roadmap_culture_models.json`: the existing
triangle stream contract, with height 1 and shared per-face RGB colors.

Regenerate from the repository root:

```sh
python3 tools/build_ukrainian_roadmap_models.py
python3 tools/build_ukrainian_roadmap_models.py --check
python3 tools/build_ukrainian_roadmap_models.py --preview
```

`--check` does not write files. It rejects degenerates, invalid normals, nonfinite
coordinates, mismatched stream lengths, duplicate IDs, over-budget meshes, changed
exports and nondeterministic generation. The contact sheet is a software mesh
inspection, not a Unity import, scene integration or mobile-performance proof.

The kit changes roadmap presentation only. It must not add gameplay obstacles,
modify level content hashes, issue entitlements or expose unrevealed campaign
content. Connected power wires belong to the region layout and must be bounded by
the existing chunk system; the tower model itself has no miles-long wire bounds.
