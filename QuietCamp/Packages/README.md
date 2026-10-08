# Quiet Camp packages

[← Project](../../README.md) · [Development](../../docs/development.md) · [Project map](../../docs/project-map.md)

![Shared data and presentation architecture](../../docs/images/diagrams/project-map.svg)

The [manifest](manifest.json) and [lock](packages-lock.json) are the package source of truth. Keep the pinned Unity and custom Git revisions when opening a fresh checkout. Embedded packages live here; they do not require sibling repositories or a copied Unity Library.

The four restored upstream packages retain their real source, Unity GUIDs, licenses and revision records:

| Embedded package | Provenance |
| --- | --- |
| `com.kruty1918.agentverify` | [Upstream revision and license](com.kruty1918.agentverify/VENDORED.md). |
| `com.kruty1918.atmos` | [Upstream revision and license](com.kruty1918.atmos/VENDORED.md). |
| `com.kruty1918.levelgen` | [Upstream revision and license](com.kruty1918.levelgen/VENDORED.md). |
| `com.kruty1918.levelkit` | [Upstream revision and license](com.kruty1918.levelkit/VENDORED.md). |

Run `python3 tools/verify_portability.py` from the repository root to check manifest/lock agreement, contained local dependencies and path case collisions. This static check does not replace Unity's first package resolution/import.
