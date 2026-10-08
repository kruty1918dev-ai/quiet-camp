# Level Kit — Documentation

See [README.md](../README.md) for the full guide. Summary:

## Pipeline

`author (designer) -> JSON file -> LevelJson.Parse (+profile) -> LevelDocument -> game adapter`

## Canonical schema v1

| Key | Type | Notes |
|---|---|---|
| `schemaVersion` | int ≥ 1 | required |
| `id` | string | required, unique per repository |
| `order` | int | sort key |
| `width`/`height` | int | optional grid dims |
| `layers` | object | `name → [[x,z],...]` cell sets |
| `entities` | array | `{id, kind, x?, z?, rotation?, ...extra}` |
| `props` | object | arbitrary extras, verbatim |

## Profile key map

| Profile field | Meaning |
|---|---|
| `gameId` | display name in tooling |
| `idKey`, `orderKey`, `schemaVersionKey` | top-level key names |
| `widthKey`, `heightKey` | grid dims keys; `""` disables grid |
| `layersKey` | wrapper key for layers (`"layers"`); `""` = flat keys |
| `entitiesKey` | canonical entity array key; `""` = none |
| `propsKey` | extras wrapper; `""` = capture unknown top-level keys |
| `layers[]` | `{name, key, multiple, color}` — `key:""` = inside layersKey |
| `entityKinds[]` | `{kind, arrayKey, idField, writeFields[], color}` |

## Designer

*Tools → Level Kit → Level Designer* — pick a profile, paint layers, edit
entities + props, validate, save. Files written via `LevelJson.Write` always
match the project's format.

## QC-style profile example

See `Samples~/QuietCamp/quietcamp.levelprofile.json` and
`Samples~/QuietCamp/QC_SAMPLE.json`.
