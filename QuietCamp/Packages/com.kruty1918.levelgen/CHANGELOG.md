# Changelog

## [1.0.0] — 2025-01

Initial release.

- Seeded, retry-capable generation engine (`LevelGenerator`, `GenRecipe`, `GenResult`)
- `GenLevel` model: grid + named cell layers + entities + free-form props
- 16 built-in steps: fill, rect, border, noise, scatter, symmetry, invert,
  path, connect, maze, rooms, segments, cells_to_entities, place, prop, require
- Declarative requirement checks: maskCount, connected, distance,
  entityCount, notOverlap, cellIn
- Custom step registry for game-specific generators
- `GenLevelJson` canonical serialization
- Optional LevelKit bridge (`KRUTY1918_LEVELKIT` auto-define)
- Editor: Generator Playground window (recipe → seeded preview → save JSON)
- Samples: runner, puzzle, tower-defense, maze, campsite recipes
