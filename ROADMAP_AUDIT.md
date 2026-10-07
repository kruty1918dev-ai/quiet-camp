# Quiet Camp Roadmap — Technical Audit (2026-10-07)

Audit of the current Level Path / Roadmap implementation before the redesign.
Every claim below is backed by code references or by failures observed in the
rendered PlayMode suite this week.

## 1. System inventory

| Concern | Implementation |
|---|---|
| Level path DOM | `MenuScreens.Levels()` builds one HTML string: `<scroll id="roadmap-scroll">` → `<view class="roadmap">` (~57,000 px tall at 110 levels + 21 bonus gaps) with a `<view id="roadmap-art">` placeholder, 110 `.map-stop` buttons, 15 `.map-district` labels, 21 `.map-bonus-stop` buttons and `branch-*` nodes. `MenuScreens.cs:249-330` |
| Roadmap geometry | `RoadmapGraphic` — a single `MaskableGraphic` stretched over the whole content rect. `OnPopulateMesh` paints terrain bands, the main path ribbon and every visible bonus clearing. `RoadmapGraphic.cs:152-186` |
| Mini-dioramas | `RoadmapGladeGraphic` slots pooled by `RoadmapGraphic.UpdatePool` — only in-view scenes get a live slot. `RoadmapGraphic.cs:95-111` |
| Scene data | `RoadmapSceneGenerator.Generate(LevelSummary)` — deterministic prop/scene model per level; called eagerly for **all 270** `CampContent.Summaries` in `EnsureData`. `RoadmapGraphic.cs:50-62` |
| Weather/season | `Scene.Advance` lerps `CampWeatherTimeline` state for **all 270 scenes every frame**; `RoadmapWeatherGraphic` repaints mist/rain/snow/fireflies every 80–180 ms. `RoadmapGraphic.cs:72-88`, `RoadmapWeatherGraphic.cs:20-24` |
| Scroll | Native `ScrollRect` on `roadmap-scroll`; `MenuMapBinding` persists `GameServices.LevelMapScroll` and restores it (now waiting for valid content layout). `MenuMapBinding.cs` |
| Map preview | `BonusCampPreviewGraphic` (33 lines) paints one clearing in `BonusPreview`; bonus dioramas also drawn inline via `BonusClearing`. `RoadmapGraphic.cs:224-244` |
| Progress/unlock | `GameServices.CanStart`, `Progression.IsCompleted`, `JourneyAccess.ContinueTarget`, `BonusCampAccess.Evaluate` — queried per node while building the HTML. `MenuScreens.cs:257-293` |
| Navigation | `CampSceneHost` sets `PendingMenuScreen="Levels"` on level entry; `MenuSceneHost` re-opens the map on return. `MenuSceneHost.cs:87-90` |

## 2. Dependency map

```
Data        LevelLoader.MvpLevelIds/Districts · CampContent.Summaries (lazy JSON) ·
            BonusCampCatalog.Slots · JourneyCatalog · AtmosphereCatalog ·
            RoadmapModelLibrary (lazy JSON, Expand() allocates all mesh arrays)
Layout      RoadmapLayout (MainY/BonusY/BranchX/Height) · CSS .roadmap/.map-stop
Render      RoadmapGraphic (one mesh) → RoadmapGladeGraphic pool →
            RoadmapWeatherGraphic · RoadmapPainter primitives
Interaction HtmlCallbacks.Bind("level-N"/"bonus-N") · ScrollRect · CampControlFeedback
Progress    ProgressionService · BonusCampAccess · JourneyAccessService
UI          MenuScreens (HTML) · HtmlSurface/UnityHtmlHost mount · MenuMapBinding
Animation   Scene.Advance weather lerp · CampMotion data-motion attrs ·
            canopy shader _RoadmapTime (material anim, not per-node)
Preview     BonusCampPreviewGraphic · BonusClearing inline dioramas ·
            JourneyPreview sheet
```

## 3. Confirmed instability (observed, not hypothesized)

1. **Stale-DOM mount race** — `HtmlSurface.Mounted` can fire while the old DOM
   subtree is still live; `MountMapArt` attaches `RoadmapGraphic` to a doomed
   `roadmap-art`, the subtree is replaced and the mesh silently disappears.
   Logged live in QA (`art=True` → `graphics=0`). Mitigated with a 90-frame
   `MapArtWatch` retry — a workaround, not a fix. `MenuScreens.cs:191-237`
2. **Scroll restore clamp** — `MenuMapBinding` applied the saved normalized
   position before content layout was valid; Unity clamped it to `1.0`.
   Now waits for `contentH>viewH`, but the full-suite run still shows one
   failure — restore timing is not fully deterministic.
3. **NRE in preview terrain** — bonus clearings build `LevelData` without
   `entry`; `CampAccess.Points` indexed `entry[0]` → crash inside
   `SeasonProfile.SnowDepth` during `BonusClearing`. Fixed by a guard, but the
   class of bug (preview data not satisfying domain invariants) remains.
4. **Resume-path coupling** — entering a level sets `PendingMenuScreen="Levels"`;
   the next menu boot opens the roadmap directly. Tests that assumed
   Main→Levels flow broke. Navigation depends on hidden session state.

## 4. Confirmed performance risks

| Risk | Evidence | Severity |
|---|---|---|
| Eager generation of all scenes | `EnsureData` loops all 270 `Summaries` → `Generate` (props, `LevelData` clone, `SeasonProfile`, `MeasureExtent` 8-corner bounds per prop) on first map open — one large main-thread spike. Also `Summaries` itself is a synchronous `Resources.Load`+`DeserializeObject` and `RoadmapModelLibrary` parses + expands every baked mesh. | **High** — cold open; grows linearly to 300+ |
| Full-mesh repaint while scrolling | `OnPopulateMesh` repaints terrain bands + 1,962 path ribbon segments + bonus clearings for the whole visible span every frame the rect moves (`LateUpdate` sets `SetVerticesDirty` on any `_visible` change). `Terrain(y)` linear-scans scene indexes per 72 px band. | **High** — per-frame during scroll |
| Weather pass over all scenes | `RoadmapWeatherGraphic.OnPopulateMesh` iterates 270 scenes (mist/sunbeam/snow/rain checks) every 80–180 ms; `Scene.Advance` iterates all 270 every frame. | Medium — scales with campaign size |
| Glade slot regeneration | each `Configure` on reassignment repaints a full mini-diorama mesh; a fast fling regenerates many slots per second. | Medium |
| DOM size | ~160+ native elements (110 buttons + districts + 21 bonus + branch nodes + icons) instantiated per mount; rebuilt on every `Refresh`/language change. | Medium |
| O(n²) lookups | `Levels()` calls `CampContent.Summary(id)` → `Summaries.FirstOrDefault` per level (~15 k comparisons at 270 summaries). | Low |
| Per-call allocations | `BonusClearing` creates `new RoadmapPainter(RoadmapModelLibrary.Load())` per visible bonus slot per repaint; static `BonusScenes` dict never evicts. | Low |
| Vertex ceiling | `DrawSurroundings` bails at `vh.currentVertCount>46000` — near uGUI practical limits; truncation silently drops models (visible as pop-in). | Medium |

## 5. Visual / rendering risks

- Single 46 k-vertex canvas mesh — driver-visible rebuild cost each dirty frame;
  uGUI canvas rebuild batches the whole graphic.
- Path ribbons are alpha-blended strips along the full route — Z-ordering is
  element order only; any future 3D/prop overlap needs a sorting strategy.
- `BonusScenes` static cache + `RoadmapModelLibrary._shared` persist across
  sessions and tests — cross-test state contamination is possible.
- No reveal/fog system exists at all — every node, bonus and branch is emitted
  in the DOM regardless of progress; "locked" is only a CSS class + click guard.
  The whole campaign is scrollable and inspectable.
- Season per scene is discrete (`Level.environment.seasonId`); adjacent scenes
  can hard-switch autumn→winter — the "set of separate scenes" problem the user
  described is inherent in the data model.

## 6. Keep / rewrite / delete / legacy

**A) Keep (reuse as-is or with light refactor)**
- `RoadmapSceneGenerator` — deterministic `LevelSummary`→scene; ideal input for
  lazy chunk generation. Add `entry`-safe preview terrain path.
- `RoadmapPainter` primitives, `RoadmapModelLibrary` baked meshes,
  `AtmosphereCatalog`, `CampWeatherTimeline`, `SeasonProfile`.
- `RoadmapLayout` math — becomes chunk layout input.
- `RoadmapGladeGraphic` pooling concept — the existing viewport pool proves
  virtualization works; promote it to the primary mechanism.
- `MenuMapBinding` scroll persistence semantics (post-hardening).
- Data sources: `LevelLoader.MvpLevelIds/Districts`, `BonusCampCatalog`,
  `JourneyCatalog`, `monetization.json` journey access rules.

**B) Rewrite**
- `RoadmapGraphic` → `WorldMapController` + N `WorldMapChunkGraphic`
  (one canvas mesh per ~8–10-level region; only current±1 chunks populated).
- `MenuScreens.Levels()` → data-driven emit; node buttons become part of chunk
  scenes (visual world) with a virtualized interactive layer.
- `RoadmapWeatherGraphic` → per-chunk pass driven by chunk activation, not a
  global every-100 ms sweep.
- `OnOverlayMounted`/`MountMapArt` → deterministic mount: HTML must not own the
  art element's lifetime; attach art to a stable native container.

**C) Delete**
- `MapArtWatch` retry loop (superseded by deterministic mount).
- `BonusScenes` static dict (move to chunk-scoped cache with eviction).
- Per-repaint `new RoadmapPainter` in `BonusClearing`.

**D) Isolate as legacy**
- Nothing to quarantine yet; the roadmap has exactly one consumer. Keep the
  current `RoadmapGraphic` path behind a feature flag (`roadmap.v2`) during
  migration so fallback stays playable until parity is proven.

## 7. Chunk-based feasibility — confirmed viable

- `UpdatePool` already proves in-view-only activation works for dioramas.
- Scene generation is pure data (`LevelSummary` + seed) — chunks can generate
  lazily and cache; nothing requires all 270 scenes resident.
- Path layout is a pure function of index — any chunk can compute its slice.
- Remaining coupling to cut: terrain bands/path ribbons/bonus arcs live in the
  shared mesh → move them into per-chunk graphics; `Scene.Advance` must be
  gated to active chunks (trivial: iterate pooled set, not `_scenes`).
- DOM buttons for 300+ levels need virtualization too — either pooled HTML
  button slots or native `map-node` hit areas owned by chunks.

## 8. Target architecture

```
WorldMapData (JSON, generated/frozen)
 → WorldMapRegion { id, range, biome, season, lighting, weatherBias,
                    vegetation, terrain, heroLandmark, motifs,
                    transitionProfile, branches, revealRules }
   → WorldMapChunk { index, nodeIds, branchAnchors, storyObjects }
     → WorldMapNode { levelId, order, pos, state, preview, hint, props, links }
     → WorldMapBranch { id, type, attach, visibility, access, teaserDepth, nodes }

Runtime
 WorldMapController
   · owns ScrollRect mapping world→screen, progress→reveal
   · activates current±1 chunks; dormant chunks = data only
 WorldMapChunkGraphic (MaskableGraphic, pooled)
   · terrain + path + props + weather for its region; bounded verts
 WorldMapNodeLayer (pooled native buttons for visible nodes only)
 WorldMapRevealService — ProgressionService-derived visibility
   (completed / current / +1–2 / teaser / hidden-by-atmosphere)
```

## 9. Migration plan

1. Extract `WorldMapData` from `LevelLoader`/`BonusCampCatalog`/`JourneyCatalog`
   + `level_summaries` (no visual change; unit tests on data integrity).
2. Split `RoadmapGraphic` into chunk graphics behind `roadmap.v2` flag; keep
   glade pool; gate `Advance` to active chunks. Verify against the existing
   6-test rendered roadmap suite.
3. Virtualize node DOM (pooled button slots bound to visible nodes).
4. Replace `Mounted`+watch with a stable native art container owned by the
   controller; remove `MapArtWatch`.
5. Reveal service (progression-derived) + atmospheric hide visuals.
6. Region/season transition profiles (data-driven blend weights).
7. Benchmark scene: synthetic 300-level dataset, perf counters in tests
   (active graphics, vertex counts, allocations per scroll frame).

## 10. Definition-of-done trace

- Why it freezes: cold-open eager 270-scene generation + synchronous JSON/model
  loads (§4.1); full-mesh repaint per scroll frame (§4.2); weather pass over
  all scenes (§4.3).
- Why it breaks visually: stale-DOM mount race, scroll clamp before layout,
  preview data violating domain invariants (§3).
- Reusable: generator/painter/model library/season+weather data — all of §6.A.
- Scalable path: chunk activation already half-implemented in the glade pool;
  §7 shows the remaining coupling is small.
