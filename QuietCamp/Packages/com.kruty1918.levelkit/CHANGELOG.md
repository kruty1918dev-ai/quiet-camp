# Changelog

## [1.0.0] — 2026-09-30

### Added
- `LevelDocument` universal level model: id/order/schemaVersion, optional grid,
  named cell layers, typed entities, verbatim props.
- `LevelJson` — schema-tolerant parse/write/validate driven by `LevelProfile`,
  with a built-in canonical schema.
- `LevelProfile` (`*.levelprofile.json`) — declarative key mapping so the kit
  adapts to any project's level format; lossless round-trips.
- Sources + `LevelRepository`: `ResourcesLevelSource`, `DirectoryLevelSource`,
  `InlineLevelSource`; ordered frozen lookup with issue reporting.
- `LevelDesignerWindow` (*Tools → Level Kit → Level Designer*) — manual grid
  painter, entity editor, raw props JSON, validation, save/open.
- Scaffold menu: *Assets → Create → Level Kit → Level Profile / Level*.
- EditMode test coverage for canonical + Quiet Camp-style flat formats.
