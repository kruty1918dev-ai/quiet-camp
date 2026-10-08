# LevelGen — Documentation

LevelGen turns **JSON recipes into deterministic levels**. A recipe is an
ordered pipeline of steps; each step mutates a `GenLevel` (grid + named cell
layers + entity list + free props); `require` steps reject attempts, and the
engine retries with a fresh seed.

## Core concepts

### GenLevel

```csharp
public class GenLevel {
    int width, height, seed, attempts; string recipe;
    Dictionary<string, HashSet<GenCell>> Layers;   // "walls", "coins", "track"…
    List<GenEntity> Entities;                      // {kind, x, y, rotation, props}
    JObject Props;                                 // difficulty, speed, lighting…
}
```

Games keep their own domain model; map layers/entities in one adapter.

### The pipeline

Each entry in `steps` is `{"type": "…", …params}`. Step ids come from the
registry — the built-ins plus whatever a game registers:

```csharp
LevelGenerator.Register("spawn_boss", () => new BossStep());
```

### Seeds & retries

```
attemptSeed = recipe.seed + request.seed + attempt-1   // seedStrategy "increment"
attemptSeed = rng()                                    // seedStrategy "random"
```

Same inputs → same output. Retry happens only on `GenFailedException`
(require failures, strict placement shortfalls). `GenRecipeException`
(unknown step, missing params) is fatal.

### Common step params

| Param | Meaning |
|---|---|
| `layer` | Target mask layer name |
| `on` | Cell must be on one of these layers |
| `avoid` | Cell must not be on any of these layers |
| `minDist` | Manhattan distance to already-placed cells/entities |
| `strict` | Fail the attempt instead of placing fewer |
| `count`, `limit`, `density`, `weight` | Numbers |

## Step reference

See README's table or the source under `Runtime/Steps/` — every step is a
small `IGenStep` implementation reading its JObject params.

## Output formats

**GenLevel JSON** (`GenLevelJson.Write/Parse`) — canonical, lossless:

```json
{ "generator":"levelgen", "recipe":"td_lane", "width":14, "height":9, "seed":507,
  "layers": { "lane": [[0,4],[1,4],…] },
  "entities": [ {"kind":"tower_spot","x":3,"y":2} ],
  "props": { "entry":[0,4], "exit":[13,4] } }
```

**LevelKit bridge** (auto-compiled with `com.kruty1918.levelkit` present):
`LevelKitBridge.ToDocument` / `GenerateToDocument` map layers→layers,
entities→entities, props→props — so generated levels can go through a
project's LevelKit profile + adapter, get serialized with `LevelJson.Write`,
and be edited in the Level Designer window.

## Runtime sources of recipes

Recipes are plain JSON — load them however your project loads text:
`TextAsset` from Resources, `File.ReadAllText`, streaming assets, or remote
config. One practical pattern: a `Recipes/` Resources folder plus
`GenRecipe.FromJson(asset.text)`.

## Extending

- **Custom step**: implement `IGenStep.Apply(ctx, params)`, register with
  `LevelGenerator.Register`. Use `ctx.Rng` for all randomness, `ctx.Vars` to
  share data between steps.
- **Custom check**: a `require` step is just a step that throws
  `GenFailedException` — custom checks can be registered the same way.
