# Composition diagnostics

Every semantic error blocks publication. `entityId` identifies source intent; `sourceKind` selects the document, templates or assets catalog; `sourcePointer` is a JSON pointer into that source. `relatedIds` identifies colliding children or broken connections. `candidates` records rejected placements without silently publishing fragments.

| Code | Meaning and repair |
|---|---|
| `schema` | Unsupported version or null/malformed collections. Repair the source structure. JSON Schema also rejects unknown properties. |
| `duplicate-id`, `duplicate-world-id`, `duplicate-role` | An identity is reused within a source/world/template. Assign a stable unique ID; do not rename existing progression nodes. |
| `invalid-anchor`, `invalid-zone`, `invalid-placement`, `invalid-template`, `invalid-role`, `invalid-asset` | Invalid dimensions/transforms. Use finite metric values and positive bounds. |
| `invalid-budget`, `instance-budget` | Limits are invalid/exceeded. Reduce detail or split authoring; do not raise runtime caps. |
| `missing-template`, `missing-zone`, `missing-node`, `missing-asset`, `missing-role-asset` | Reference cannot be resolved. Repair the intention/catalog. A mandatory child cannot disappear independently. |
| `orphan-role`, `optional-parent`, `dependency-cycle` | Ownership is broken, depends on an optional parent, or cycles. Correct the template graph. |
| `fence-without-owner` | Boundary asset has no owner. Attach it to the house/context. An intentional remnant needs an explicitly ruined template and foundation. |
| `missing-gate`, `gate-without-boundary` | Entrance role is absent or not on a declared fence boundary. Correct gate position and boundary roles. |
| `role-overlap` | Template asset footprints intersect. Inspect related child IDs and move/resize the roles. Ground cover and canopy-to-canopy overlaps are intentionally allowed. |
| `disconnected-entrance` | Entrance has no named road/path target. Assign `entranceConnectsTo`; proximity alone is insufficient. |
| `ensemble-does-not-fit` | No whole-parcel candidate passed zone, clearing, corridor, occupancy and slope checks. Move/enlarge the zone or anchor; inspect candidate reasons. |
| `unsupported-approach` | Parcel may fit, but its complete entrance route crosses unsupported ground. Move the ensemble or author a supported crossing. No yard children are emitted. |
| `invalid-route`, `invalid-route-kind`, `invalid-interpolation` | Malformed corridor or bent power-span interpolation. Correct control points; power/distribution spans are linear. |
| `missing-route-continuation`, `disconnected-route-continuation`, `incompatible-route-network` | Named neighboring route is missing, has a different endpoint, or is another network type. Match endpoints after applying region offsets. |
| `missing-support`, `incompatible-support`, `missing-conductor-sockets` | Support model is missing/wrong type/has no wire sockets. A tree or village pole cannot substitute for a transmission pylon. |
| `invalid-span`, `unsupported-pylon` | Span spacing cannot be met or support intersects reserved/unsupported ground. Edit the geographic route, not generated support transforms. |
| `missing-service-target` | Distribution network lacks a valid served ensemble. Do not join it to the transmission circuit. |
| `invalid-season`, `invalid-seasonal-variant` | Unknown season or mandatory asset unsupported by that season. Correct region/asset seasonal metadata. |
| `missing-reveal-owner`, `unsupported-landmark` | Landmark is not bound to progression or its footprint is invalid. Give it an explicit reveal owner and supported placement. |

Candidate reasons such as `zone-boundary`, `anchor-distance`, `occupied-parcel`, `parcel-slope`, `approach-crosses-parcel` and corridor/clearing exclusions explain search rejection. They are not accepted fallback placements.

Native bake errors (missing model/LOD, incomplete immutable resource, source/catalog changed during bake, chunk >15 MiB, disk allowance) retain the prior publication. Fix the dependency and bake again. Do not replace a malformed native resource in place under the same immutable hash.

Native runtime `StreamFault` is an observable load/revision/budget error. The renderer avoids retry/log storms for the same window. It does not claim success through a generated substitute. Investigate the bake manifest and resource binding; automated readiness checks must fail when this property is non-null.

Semantic tests do not decide artistic quality. After diagnostics clear, inspect actual native images and compare projected entity IDs, parcel/connection overlays, season continuity and metrics.
