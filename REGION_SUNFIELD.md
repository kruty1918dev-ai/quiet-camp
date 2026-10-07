# Region "sunfield" — MH17-inspired environmental region

Fiction region in the Quiet Camp world. Echoes, without copying, the
destruction of Malaysia Airlines flight MH17 over farmland in eastern
Ukraine on 17 July 2014.

## Verified facts this region rests on

Checked against the Dutch Safety Board final report and the JIT /
Openbaar Ministerie publications:

- The aircraft was a Boeing 777-200 on a scheduled passenger flight;
  298 occupants, all killed.
- A Buk TELAR launched a missile of the **9M38 series** (warhead
  **9N314M**), which detonated left of and above the cockpit. The
  forward section was penetrated by high-energy fragments; the
  aeroplane broke up in the air.
- Wreckage was distributed over roughly 50 km² in six sites near
  Hrabove, Rozsypne and Petropavlivka — open farmland, sunflower
  fields, tree lines.
- Recovery was carried out months later by Dutch-led missions with
  OSCE, the State Emergency Service of Ukraine and local residents.
- **Not used:** S-300 (unrelated system), invented slogans or serial
  markings, any graphic depiction of victims.

## What the region is in the game

A long sunflower-field region the road crosses once. From the map it
reads as a wide cultivated field, a strange pale shape in the grass,
and quiet. Nothing on the map explains it.

## Roadmap composition

- Wide field zone; road bends around two bright specks far apart.
- Two aircraft-section silhouettes only: a forward-section shape near
  a tree line and a tail/rear section further into the field. Dark
  shapes in high grass, partly swallowed by vegetation.
- Sunflowers and tall grass; an old farm track; one tree line.
- `roadmapVisibility: silhouette` — never detail. No missile
  fragment, no markings, no flags, no text, no "Russia", no
  explanatory popup on the map.

## Level composition

- The camp puzzle sits between the two large sections: board in a
  mown/cleared strip, wreckage shapes forming the far backdrop and
  one near edge.
- Peripheral storytelling, all age-appropriate and quiet:
  - weathered luggage and cabin fragments at the field edge;
  - a few small foreign-metal fragments whose geometry an attentive
    player can match to 9N314M/9M38-series forms — carried only as
    shape, never labelled in-game;
  - small Cyrillic technical markings allowed on at most one or two
    small fragments — plausible manufacturing marks only, never
    invented slogans;
  - memorial traces: a small cleared rectangle, stones, field
    flowers, a jar candle.
- No gore, no bodies, no spectacle. The tone is a field that
  remembers, not a crash reenactment.

## Story sequence (three beats, all environmental)

1. **Arrival:** sunflowers, wind, two pale shapes. The player
   registers "something large and wrong" without text.
2. **Between the pieces:** the puzzle works around the wreckage;
   luggage and memorial traces at the edges carry the scale of the
   loss.
3. **After completion:** a small quiet clearing — stones, flowers, a
   lamp — appears beside the road. The aircraft is not repaired; the
   place is cared for.

## Lighting and ambience

- Flat late-afternoon light, warm but slightly desaturated; long
  shadows across the rows.
- Ambience: wind in sunflower heads, distant birds, no music cue
  until completion — then a single low sustained note.
- Weather bias low: this scene plays in calm air.

## Camera rules

- Map camera: the two sections read as small bright forms in a big
  field — scale told by contrast, not zoom.
- Level camera stays at standard board distance; never dollies into
  wreckage; memorial elements sit at screen edges.

## Performance plan

- Wreckage silhouettes are two shared baked meshes; field vegetation
  uses the existing instanced sunflower/grass sets.
- No dynamic debris, no particle fire; one candle flame sprite at the
  memorial after completion.
- Region renders inside normal chunk budgets — nothing exceeds the
  shared-material/model-library limits.

## Content safety invariants (tested)

- `sunfield` story entry: `roadmapVisibility` is `silhouette` and
  `levelDetailVisibility` is `evident` — context on the map, evidence
  only inside the level.
- `referenceConfidence: verified`, `historicalReference: mh17` —
  the technical facts above are what the region may echo; anything
  else must be invented-free.
- No prop id referencing weapons, missiles or military text may
  appear in the region's roadmap-visible prop list — enforced by the
  story validation tests.
