# Frozen campaign authoring

The campaign tool compiles the actual `QuietCamp.Domain` sources in a small
.NET console harness. It does not start Unity, create a player build, package
an APK, or touch player saves.

Prerequisites: Python 3, .NET SDK 6 or later, and the Unity project package
cache containing its existing Newtonsoft.Json assembly.

```bash
python3 tools/regenerate_campaign.py
python3 tools/regenerate_campaign.py --write
python3 tools/check_levels.py --cap 3
```

The first command stages and validates all 30 replacement levels. Every level
must pass structural checks, its frozen witness, and an independent search
through the same Domain solver used by the game. The harness also checks shared
doorways under both rule versions and connected/blocked exterior trails.

`--write` additionally preserves each changed previous level's exact bytes in
`ContentRevisions/<contentHash>.json`. It writes the complete validated set,
lightweight summaries and campaign hash manifest, restoring previous files if
publication fails. IDs and campaign order remain stable. Unity's matching
`LiveCampContent.BuildLive` entry point uses the same recipe.

The Python checker independently enumerates three distinct valid solutions per
level. `--witness-only` performs only the faster structural/witness check; its
reported count of one is not a claim of a unique puzzle solution.

## Rules and compatibility

- Version 1 keeps the original board-only walking graph. Existing albums and
  unfinished arrangements retain their archived rule version and content hash.
- Version 2 permits doors on explicitly authored `exteriorWalkable` cells.
  Footprints still occupy only board cells. Entrances, exit cells and static
  obstacles stay reserved. Shared doors and shared walking lanes are allowed.
- An exterior verge must connect to the walking network through an unoccupied
  board approach; a tent or obstacle can block that approach.
- Weather, scenery, tent fabric motion and fire appearance never modify puzzle
  occupancy, shade/noise masks or the command history.
- `SessionSaveData.levelSnapshot` preserves an unfinished content revision.
  Missing snapshots resolve through hash archives and matching legacy content.

## Seasonal content

| Levels | Season | Environment motifs |
| --- | --- | --- |
| 1–5 | Spring | Daisies, young forest, abandoned roadside |
| 6–10 | Summer | Poppies, stream, guest belongings |
| 11–15 | Summer | Dense forest, lake, wheat, former settlement |
| 16–20 | Autumn | Wet golden foliage, traces of memory |
| 21–25 | Winter | Conifers, shelter and shared warmth |
| 26–30 | Returning spring | Flowers in ruins, repaired markers |

Shore levels are 7, 10, 13, 19, 24 and 29. Shore geometry stays beyond the
playable grid and opposite the authored access sides. The first two clearings
have two guests and no personal wishes; levels 3, 4 and 5 introduce shade,
quiet around a visible fire, and friendship respectively.
