---
name: quietcamp-worldops
description: "Edit QuietCamp level visuals through WorldOps: vegetation, landmarks, paths, rivers, weather, Game View capture and visual review. Use for QuietCamp campaign scenes even when the working folder is Moyva; do not apply to Moyva gameplay or unrelated Unity projects."
---

Use the existing WorldOps C# service as the mutation authority for QuietCamp
level presentation. This skill routes to maintained project instructions; it
does not define a second scene generator or authorize additional actions.

Locate the QuietCamp repository (`Documents/GitHub/quiet-camp` on this machine).
Set shell workdir explicitly there when the chat is attached to Moyva. Read its
root AGENTS.md and the relevant section of Design/LevelAuthoring/WorldOps.md.
Read applicable nested AGENTS.md files before changing authoring or tools.

Use `tools/qc_world.py --project <already-open-QA-project> run <recipe.json>`.
Recipes live in QuietCamp/Assets/QuietCamp/Authoring/WorldOps/. For existing
greenery use greenery-pilot.json, then greenery-all.json. The default QA copy
on this machine is C:/Users/Public/qcui-20261006/QuietCamp; verify it is still
the intended open project. Do not start another Editor or a player build.

Observe the service with `status` and `watch`. Inspect actual current Game View
frames with UI and submit a ReviewVerdict bound to the packet/image hashes.
Technical success alone is not artistic acceptance. Conflicts, stale evidence
and unresolved findings must remain explicit; never manufacture approval.

Extend an allow-listed C# operation and focused checks if needed, then use the
same workflow. Do not hand-patch generated campaign/art/draft JSON, hashes or
receipts, or revive legacy writers. Preserve pinned/manual details according
to the explicit selector and keep object-supported overgrowth intact.

Use the service for apply, rollback and source-safe delivery. Ordinary user
workshop actions do not run Git. Retain the repository's existing Unity,
resource, save/progress and unrelated-work restrictions. This skill grants no
permission to message people, change another project or modify other processes.
