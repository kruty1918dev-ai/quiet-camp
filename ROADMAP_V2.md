# Quiet Camp Roadmap v2 — implementation status

Data-driven world map replacing the level-list roadmap. Target: 300+
main levels plus branches, without keeping the whole world live.

## Architecture

```
campaign.json / monetization.json / bonus_camps.json   (authoring truth)
        │
        ▼
WorldMapBuilder          Infrastructure — composes the snapshot
        │
        ▼
WorldMapData             Domain — Regions → Chunks → Nodes + Branches
        │
        ├── WorldMapValidator   structural + content checks (tests + authoring)
        ├── WorldMapRevealService  visibility derived from ProgressionService
        ├── SeasonTransitions   per-order palette blending across boundaries
        └── WorldStoryCatalog   environmental storytelling, world_story.json
        │
        ▼
MenuScreens / RoadmapGraphic   Presentation — pooled glades, shared materials
```

## Data model

- **WorldMapRegion** — id, act, level range, biome, season, lighting,
  weather bias, vegetation/terrain profiles, hero landmark, motifs,
  `TransitionFrom` + `TransitionSpan`, branch ids.
- **WorldMapNode** — levelId, order, map-local X/Y, state (derived),
  preview identity, story hint, props, branch links.
- **WorldMapChunk** — activation unit; 8 main orders each.
- **WorldMapBranch** — id, type (BonusGlade / MiniTrail /
  StoryJourney), access rule, attach order, teaser depth, hero
  landmark, positioned branch-local nodes.

## Region authoring format

Regions come from `campaign.json` districts (`id`, `act`, `from`,
`to`) plus identity sampled from the first level's environment; story
content comes from `world_story.json`:

```
{ "id": "shores", "storyBeat": "...", "heroLandmark": "...",
  "returningProp": "...", "characterFocus": "...",
  "worldStateBefore": "...", "worldStateAfter": "...",
  "historicalReference": "kakhovka-2023",        // optional
  "referenceConfidence": "none|inspired|verified",
  "roadmapVisibility": "hidden|silhouette|context",
  "levelDetailVisibility": "none|subtle|evident" }
```

New branches are added by authoring a journey in
`monetization.json` or a bonus slot in `bonus_camps.json` — no
navigation or UI code changes needed.

## Reveal rules

`WorldMapRevealService`: completed → current → +2 lookahead → hidden.
Branch anchors appear when inside the horizon; gated branches tease
their landmark only; open branches taper into mist by teaser depth.
`ActiveChunks()` lists the chunks allowed to be live geometry —
verified bounded at 320 levels (tests in `WorldMapScaleTests`).

## Season transitions

`SeasonTransitions.Sample(map, order)` returns a `SeasonBlend`
(From/To/T) per order; `SeasonLook` tables carry the scalar channels
(snow, leaves, bare branches, moisture, ground warmth, fog, particles,
light warmth). The blend zone straddles each region boundary
(`TransitionSpan` nodes on each side, default 2). Verified: no channel
jumps more than 0.6 anywhere in the campaign.

## Validation coverage

`WorldMapValidator` catches: duplicate node/branch ids, unreachable
or impossible branches, teaser depth beyond length, region gaps and
ranges, wrong transition anchors, chunk gaps/ranges/node coverage,
non-monotonic or out-of-region/chunk nodes, node overlap (main↔main
and branch↔main), missing preview ids (warn), missing localization
keys (when supplied a key lookup), season jumps (warn — inputs for
the transition system).

`WorldStoryCatalog.Validate` catches: unknown story ids (warn —
forward-declared regions are pipeline content), verified claims
without a reference, references without a confidence label, and
detail-level content leaking onto the roadmap.

## Historical regions

Three fiction regions are specified and safety-tested:

- `sunfield` — MH17 echo; Buk/9M38-series facts verified; field
  silhouettes only on the map. `REGION_SUNFIELD.md`
- `tidewrack` — Black Sea echo; stranded carrier, grain terminal,
  real warning signage; mines never interactive. `REGION_TIDEWRACK.md`
- `stillwater` — Kakhovka echo; emptied reservoir, flood trace, dam
  face; rescue told through empty shelters. `REGION_STILLWATER.md`

## Verified

- EditMode: WorldMap 9, Reveal 8, Seasons 5, Story 7, Scale 5.
- PlayMode roadmap suites: 6/6 (bonus previews, scroll persistence,
  weather bounds, phone/tablet/ultrawide).

## Still ahead

- Chunk-rendered vertical slice consuming `ActiveChunks()` in the
  live UI (current UI still renders one pooled-glade list).
- Branch visual identities (Firefly Glade / Lighthouse / Station /
  Garden prop sets) and per-branch teaser rendering.
- Screenshot regression set per region/reveal state.
- Benchmark harness reporting frame time / allocations / draw calls
  on device-class hardware (current scale tests are data-level).
