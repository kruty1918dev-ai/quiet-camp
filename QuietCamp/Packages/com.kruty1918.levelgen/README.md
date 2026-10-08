# LevelGen — Universal Level Generator

A **JSON-driven, seeded level generator** for casual, hybrid-casual and
hyper-casual games: runners, puzzles, tower defense, mazes, board layouts.
Levels are described by *recipes* — an ordered pipeline of declarative
generation steps — and produced deterministically from a seed.

The package is **standalone**: it ships its own `GenLevel` model and JSON
format. If your project also uses
[LevelKit](https://github.com/kruty1918dev-ai/level-kit), an automatic bridge
(`KRUTY1918_LEVELKIT` define) lets generated content flow through your
existing LevelKit profile/adapter pipeline.

## How it works

```
recipe.json ──> LevelGenerator.Generate(recipe, seed)
                    │
                    ├─ attempt 1: run step pipeline in a seeded context
                    │   fill → noise → scatter → path → place → require …
                    ├─ a "require" step fails? → roll seed, retry
                    └─ success → GenLevel { grid + named layers + entities + props }
```

- **Deterministic** — same recipe + seed = same level, on every device.
- **Declarative** — designers edit JSON, not code.
- **Extensible** — `LevelGenerator.Register("my_step", () => new MyStep())` adds
  game-specific steps alongside the built-in library.

## Built-in steps

| Step | Purpose |
|---|---|
| `fill`, `rect`, `border` | Fill a layer (all / rectangle / edge ring) |
| `noise` | Organic blobs via smoothed value noise + threshold |
| `scatter` | N random cells, `minDist`, `on`/`avoid` masks, `strict` |
| `symmetry`, `invert` | Mirror a mask / take its complement |
| `path` | Biased random-walk path between cells or edges — TD lanes, runner tracks |
| `connect` | Join mask components with corridors |
| `maze` | Recursive-backtracker maze (`mode: walls|path`) |
| `rooms` | BSP rooms + corridors (`room0..n` layers) |
| `segments` | Weighted segment bands along an axis — the runner/hyper-casual track |
| `cells_to_entities` | Layer cells → entities (limit, props) |
| `place` | Entities with mask constraints, spacing, per-kind `each` lists |
| `prop` | Set an arbitrary level prop |
| `require` | Declarative checks: `maskCount`, `connected`, `distance`, `entityCount`, `notOverlap`, `cellIn` — fail → retry |

## Quick start

```csharp
using Kruty1918.LevelGen;

var recipe = GenRecipe.FromJson(File.ReadAllText("runner_track.json"));
var result = LevelGenerator.Generate(recipe, seed: levelIndex);

if (result.Ok)
{
    var level = result.Level;
    foreach (var c in level.Layer("obstacles")) SpawnObstacle(c.X, c.Y);
    foreach (var e in level.Entities)     if (e.kind == "coin") SpawnCoin(e.x, e.y);
    var speed = level.Props.Value<float>("speed");
}
else Debug.LogWarning(string.Join("\n", result.Issues));
```

Save / share results:

```csharp
File.WriteAllText("level_42.json", GenLevelJson.Write(result.Level));
```

Optional LevelKit integration (when `com.kruty1918.levelkit` is installed):

```csharp
var doc = LevelKitBridge.GenerateToDocument(recipe, seed);   // → LevelDocument
var levelData = myAdapter.ToLevelData(doc);                   // your game's domain
```

## Editor playground

**Tools → LevelGen → Generator Playground** — load/paste a recipe, pick a
seed, generate, preview every layer as colored cells (entities = white dots),
save the result as JSON. Great for tuning densities and retry budgets.

## Recipes in this package

Import **Ready-made Recipes** from the package *Samples* tab:

- `runner_track.json` — 7×60 segmented track, obstacles/coins, strict counts
- `puzzle_board.json` — symmetric noise-painted 8×8 board with targets
- `td_lane.json` — winding lane + tower spots + entry/exit props
- `maze_dungeon.json` — maze walls, floor, loot, entry/exit
- `campsite_board.json` — calm 6×6 campsite (blocked/shade/fire/tents) — the
  recipe [Quiet Camp](https://github.com/kruty1918dev-ai/quiet-camp) uses for
  endless levels

## Recipe anatomy

```jsonc
{
  "name": "td_lane",
  "width": 14, "height": 9,
  "seed": 500,                 // base seed; + request seed + attempt
  "seedStrategy": "increment", // or "random" — per-attempt reseed
  "maxAttempts": 25,           // retry budget when require steps fail
  "props": { "genre": "tower-defense" },     // copied to every level
  "steps": [
    { "type": "path", "layer": "lane", "from": [0,4], "to": [13,4], "winding": 0.65 },
    { "type": "require", "check": "connected", "layer": "lane" }
  ]
}
```

Generate per-level variety at runtime with `Generate(recipe, levelIndex)` —
the request seed shifts the recipe seed, so each index is a new deterministic
level.

## Design notes

- `System.Random` only — no `UnityEngine.Random`, no engine dependence in the
  generation path (runs in tests, tools, servers).
- Steps fail via `GenFailedException` (retry) vs `GenRecipeException` (fatal —
  bad recipe).
- `ctx.Vars` lets steps pass data (e.g. the carved path's ordered cells are in
  `Vars["lastPath"]`, room centers in `Vars["rooms"]`).

## License

MIT — see [LICENSE.md](LICENSE.md).
