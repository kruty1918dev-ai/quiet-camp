# QuietCamp automation

Level presentation changes go through `qc_world.py`, backed by the shared C#
WorldOps service. Do not add direct campaign/art/draft JSON writers. Recipes are
in `QuietCamp/Assets/QuietCamp/Authoring/WorldOps/`; the documented all-level
greenery operation is `greenery-all.json` (pilot: `greenery-pilot.json`).

Extend an allow-listed C# operation and focused checks for missing behavior.
Review actual current Game View frames and submit a hash-bound verdict before
publication. Technical success is not artistic acceptance. See
`Design/LevelAuthoring/WorldOps.md` and the root AGENTS.md for Unity/session rules.
