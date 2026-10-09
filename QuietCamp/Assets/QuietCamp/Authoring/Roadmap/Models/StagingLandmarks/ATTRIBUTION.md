# Staging landmark attribution

These anonymous fictional diorama assets are unpublished visual studies, not reconstructions of a real aircraft, ship, wreck site or event. No military evidence, flags, serials or invented historical markings are added.

- **Airplane**, Poly by Google: https://poly.pizza/m/a3XrQkLNna9 — **CC BY 3.0**, https://creativecommons.org/licenses/by/3.0/. Retrieved 2026-10-08. Original GLB retained as `airplane-source.glb`. Changes: split into separated front and rear sections, metric diorama scaling, baked/restrained weathered material colors, preserved UVs, no livery text transferred. This donor is not a certified Boeing 777 reconstruction.
- **Low Poly Cargo Ship**, Javier_Fernandez: https://sketchfab.com/3d-models/low-poly-cargo-ship-4c22cbaf01c1427f8ab60b3a07b1b32c — **CC BY 4.0**, https://creativecommons.org/licenses/by/4.0/. Derivative source: https://github.com/bilawalsidhu/gods-eye-view/blob/main/public/models/README.md. Retrieved file contains matching author/source/license in glTF asset extras. Original derivative GLB retained as `ship-source.glb`. Changes: metric scaling, muted weathering and vertex-color baking, UV preservation. It is a cargo donor, not an exact approved bulk-carrier model.

Hashes, geometry dimensions and triangle counts are in `manifest.json`. Licenses do not imply endorsement. These assets and their obligations remain separate from the future generic composition UPM package.

2026-10-08 environment-kit change: the **existing** cargo GLB (no new download) is
restyled by `tools/roadmap-assets/optimize_ship.py`. Welded QEM produces
1,100 / 650 / 300 triangles, replacing 2,384 / 1,366 / 181. The smallest LOD has
more triangles to keep a stable hull/mast silhouette. All three use the same donor
origin and height, five existing project palette colors, flat normals and no UV or
texture payload. Combined ship LOD triangles fall from 3,931 to 2,050. Original
GLB and CC BY attribution remain intact. Native visual acceptance is pending.
