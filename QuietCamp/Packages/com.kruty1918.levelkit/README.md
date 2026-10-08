# Level Kit

Універсальний інструментарій для казуальних ігор з рівнями: **редактор рівнів** для ручного створення мап, **толерантний JSON-парсер** і **профіль формату**, який адаптує бібліотеку під будь-який проєкт. Пакет нічого не знає про конкретну гру — семантику клітин і сутностей описує профіль вашого проєкту.

*A universal level-authoring and parsing toolkit for casual, level-based Unity games: a hand-authoring designer window, a schema-tolerant JSON codec, and a reusable format profile that adapts the kit to any project's level files.*

## Install

**Git URL** (recommended):

```
https://github.com/kruty1918dev-ai/level-kit.git
```

Add in Unity Package Manager → *Add package from git URL…*, or in `Packages/manifest.json`:

```json
"com.kruty1918.levelkit": "https://github.com/kruty1918dev-ai/level-kit.git"
```

**Local path** (development):

```json
"com.kruty1918.levelkit": "file:../level-kit"
```

Requires Unity 2022.3+ and `com.unity.nuget.newtonsoft-json` (pulled automatically).

## Concepts

| Concept | Meaning |
|---|---|
| `LevelDocument` | In-memory level: id, order, optional grid, named cell **layers**, typed **entities**, and **props** (every unknown field, preserved verbatim) |
| `LevelProfile` | Declarative map of where those concepts live in *your* JSON: key names, single/multi-cell layers, per-kind entity arrays |
| `LevelJson` | `Parse(json, profile)` → `LevelResult` (document + diagnostics); `Write(doc, profile)` → JSON back in your format |
| `LevelRepository` | Ordered, frozen lookup over parsed levels from `Resources`, a directory, or inline strings |
| `LevelDesignerWindow` | Editor window (*Tools → Level Kit → Level Designer*) for manual authoring |

## Quick start (runtime)

```csharp
using Kruty1918.LevelKit;

// One-off parse of a shipped level file.
var result = LevelJson.Parse(levelTextAsset.text, MyGame.Profile);
if (!result.Ok) Debug.LogError(string.Join("\n", result.Issues));
var level = result.Document;

var blocked = level.Layer("blocked").Cells;
var guests  = level.EntitiesOf("guest");
var music   = level.Prop<string>("music");

// Whole set, ordered by "order".
var repo = LevelRepository.FromSource(
    new ResourcesLevelSource("MyGame/Levels"), MyGame.Profile);
var first = repo.All[0];
```

## Format profiles — the universal part

Your game keeps *its own* JSON layout; the profile tells the kit where the common concepts are. Profiles are plain `*.levelprofile.json` assets — the designer discovers them automatically.

Example — Quiet Camp profile (flat top-level keys, single-cell entry, two entity arrays):

```json
{
    "gameId": "quiet-camp",
    "layersKey": "",
    "entitiesKey": "",
    "propsKey": "",
    "layers": [
        {"name":"entry",   "key":"entry",   "multiple":false, "color":"#e3c04a"},
        {"name":"blocked", "key":"blocked", "multiple":true,  "color":"#8899aa"},
        {"name":"shade",   "key":"shade",   "multiple":true,  "color":"#4a6b57"},
        {"name":"noise",   "key":"noise",   "multiple":true,  "color":"#c98f4a"}
    ],
    "entityKinds": [
        {"kind":"guest",   "arrayKey":"guests",  "idField":"id",
         "writeFields":["id","nameKey","assetId","shade","quiet"]},
        {"kind":"witness", "arrayKey":"witness", "idField":"guestId",
         "writeFields":["guestId","x","z","rotation"]}
    ]
}
```

Against this profile, `blocked`, `shade`, `noise` parse as cell layers, `entry` as a single `[x,z]` pair, `guests`/`witness` as typed entities, and everything else (`friends`, `lighting`, `contentHash`, …) lands in `Props` verbatim — so `Parse → Write` is **lossless**.

### Canonical schema (no profile)

```json
{
    "schemaVersion": 1,
    "id": "level_001",
    "order": 1,
    "width": 6, "height": 6,
    "layers":   { "blocked": [[0,0]], "entry": [[5,5]] },
    "entities": [ {"id":"e1","kind":"spawn","x":1,"z":2,"rotation":0,"team":"a"} ],
    "props":    { "music": "calm" }
}
```

## Level Designer (editor)

*Tools → Level Kit → Level Designer*

- **Profile picker** — every `*.levelprofile.json` in the project is offered.
- **Grid painter** — left-click paints the selected layer, right-click erases; single-cell layers (`entry`) replace, multi-cell layers toggle.
- **Entities** — kind dropdown from the profile, id / x / z / rotation, plus free-form JSON data per entity.
- **Extra props** — raw JSON object for anything project-specific (`friends`, `lighting`, `tutorialKey`, …).
- **Validate** — schema/grid/duplicate checks via `LevelJson.Validate`.
- **Save** — writes through `LevelJson.Write`, so output matches your game's format exactly.

Create new profiles via *Assets → Create → Level Kit → Level Profile*.

## API surface

```
LevelJson.Parse(string|JObject, LevelProfile) → LevelResult {Document, Issues, Ok}
LevelJson.Write(LevelDocument, LevelProfile, indented) → string
LevelJson.Validate(LevelDocument, LevelProfile) → List<LevelIssue>
LevelProfile.FromJson(string) → LevelProfile
LevelRepository.FromSource(ILevelSource, LevelProfile)
  sources: ResourcesLevelSource(folder) | DirectoryLevelSource(path) | InlineLevelSource
LevelDocument: Id, Order, SchemaVersion, Width, Height,
               Layer(name), Entities, EntitiesOf(kind), Props / Prop<T>()
LevelEntity:   Id, Kind, X, Z, Rotation, HasPosition, Data / Get<T>(key)
```

## Design rules

- **No hidden state** — parse is pure; repository is a frozen snapshot.
- **Lossless** — unknown fields are never dropped; they round-trip through `Props`.
- **Game-agnostic** — cell semantics and entity kinds are data (profile), not code.
- **No codegen / ScriptableObject mirror** — JSON is the single source of truth.
- **No `AssetDatabase` in runtime** — runtime sources read Resources or plain files.

## Known limitations

- The designer has no Unity-undo integration for document edits yet.
- Profiles map *flat* top-level keys; deeply nested level formats need a custom adapter (parse via `LevelJson` canonical + map, or extend `LevelProfile`).
- `DirectoryLevelSource` works in editor/standalone; on device prefer `ResourcesLevelSource` or copy files into `persistentDataPath` first.

## License

MIT — see [LICENSE.md](LICENSE.md).
