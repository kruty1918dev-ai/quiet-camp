# Region "stillwater" — Kakhovka-inspired reservoir region

Fiction region in the Quiet Camp world. Echoes, without copying, the
destruction of the Kakhovka Hydroelectric Power Plant dam on
6 June 2023 and what it did to the land it fed.

## Verified context this region rests on

Checked against the UN Ukraine assessment, the PAX preliminary
environmental risk assessment, and Science (Shumilova et al.):

- The dam breached in the early morning of 6 June 2023. More than
  18 km³ of water drained within days; the reservoir — the country's
  largest by volume — was largely lost.
- Downstream: catastrophic flooding (peak 5.6 m at Kherson), more
  than 80 settlements affected, tens of thousands evacuated.
- Upstream: the emptied reservoir bed — a mature aquatic ecosystem —
  desiccated almost overnight; young riparian vegetation is now
  recolonising the silt.
- Animals were evacuated by residents and rescuers in improvised
  carriers and boats; many were lost. The region shows survival and
  rescue — never dead animals.
- Infrastructure: broken crossings, waterlogged sheds, damaged
  roads, exposed utility structures, and the dam face itself.

## What the region is in the game

A three-stage region the road crosses: former shoreline → flood
trace → dam area. Wide emptiness, then damage, then scale.

## Roadmap composition

- An unnaturally wide riverbed zone — pale silt flats where a great
  water used to sit; a shoreline that no longer touches water.
- A stranded boat far from the current water.
- Dead and recovering tree lines, a damaged road.
- Far ahead, the silhouette of a massive broken concrete hydro
  structure — a landmark, never a label.
- `roadmapVisibility: silhouette` — nothing on the map explains the
  absence; the geometry does.

## Level composition — three stages

### Stage A — Former Shore

- Mud lines, debris arcs, a boat resting far above the water.
- Household objects carried by water, half-buried in silt.
- Young vegetation already colonising the bed.

### Stage B — Flood Trace

- Broken fences, damaged outbuildings, water marks on walls.
- Improvised animal carriers and empty enclosures, feeding bowls —
  the story of rescue told through what was left behind.
- Rescue boat traces: rope on a post, a cleared path to high ground.
- **No dead animals.** Loss and rescue read through empty spaces and
  rebuilt shelters.

### Stage C — Dam Area

- Massive broken concrete and exposed engineering detail.
- Old utility structures; the new, smaller water course below.
- Vegetation returning between the slabs.

## Gameplay

- Pronounced elevation difference between stages; damaged crossings;
  multiple routes between elevation shelves.
- Temporary bridges and access-point mechanics — the level's
  entrances and exits move between high and low ground.

## After completion

People do not rebuild the dam. They:

- make a safe crossing over the new water course;
- mark a temporary route across the flats;
- leave water and food caches;
- set up a camp for whoever passes next;
- repair one small piece of local infrastructure.

The arc is damage → survival → adaptation → life.

## Lighting and ambience

- Flat grey-green light over the flats; the dam face pale against it.
- Ambience: wind over open silt, sparse insects, a distant generator
  hum near the dam work area.
- Young willow and poplar shoots as the returning-vegetation motif.

## Performance plan

- Dam face is one baked silhouette mesh on the horizon; no interior.
- Silt flats reuse flat terrain pieces with a desaturated silt
  palette and crack decal texture.
- Flood debris and carriers are shared props from the standard
  library; no unique high-poly assets.

## Content safety invariants (tested)

- `stillwater` entry: `roadmapVisibility: silhouette`,
  `levelDetailVisibility: evident`.
- `referenceConfidence: verified`, `historicalReference:
  kakhovka-2023`.
- No animal-carcass props, no flood gore; rescue and survival props
  only — enforced by the story safety tests.
