# Region "tidewrack" — Black Sea-inspired coastal region

Fiction region in the Quiet Camp world. Echoes, without copying, the
closure of the north-western Black Sea to civilian shipping since
2022: drifting mines, stranded merchant ships, and damaged grain
infrastructure.

## Verified context this region rests on

Checked against reporting by Reuters, USNI News, MARAD advisories and
the Dutch Safety Board coverage of the area:

- Since February 2022 the Odesa-area ports — Ukraine's main grain
  export loading points — were blockaded; dozens of merchant vessels
  were stranded in or near Ukrainian ports for months or years.
- Anchored naval mines broke loose in storms and drifted along the
  coast or washed ashore; Ukrainian authorities repeatedly warned
  coastal communities, and mines reached beaches as far as Romania.
- Grain terminals and port infrastructure were hit and damaged;
  the humanitarian "grain corridor" operated under naval-escort
  uncertainty.
- Beach warning signage ("Обережно! Міни" — "Caution! Mines") is a
  documented reality of the Ukrainian Black Sea coast — the region
  uses it as the real sign it is.

## What the region is in the game

A coast strip the road follows for a stretch: lagoon reeds, a silted
marina, a grain terminal on the far shore, a lighthouse on the point,
and one big ship sitting where no ship should rest.

## Roadmap composition

- Road hugs a low dune line; pale sand, dark reed beds, flat water.
- A long pier reaching out; far beyond it, the silhouette of a bulk
  carrier sitting visibly too low — aground, abandoned mid-voyage.
- Grain elevator silhouette inland; loading booms frozen mid-lift.
- `roadmapVisibility: silhouette` — the ship reads as one shape; no
  deck detail, no name, no flags.
- Mine presence is shown exactly as it is in life: warning signs and
  taped-off beach access, never a visible mine.

## Level composition

The region's flagship level is the stranded ship area — the first
Quiet Camp map with real multi-elevation geometry:

- Board on the beach beside the ship's bow; a second raised play area
  on the pier deck; a third small shelf on the ship's gangway — the
  puzzle uses height difference, damaged crossings and multiple
  routes between the levels.
- Peripheral storytelling, all quiet:
  - the "Обережно! Міни" sign standing naturally at the taped-off
    beach entrance — a real sign in a real place;
  - shipping pallets and grain spilled from a torn bag by the silo;
  - a harbour master's logbook left open on the pier;
  - laundry still hanging on the ship's rail;
  - a rescue ladder and life rings — signs of how people coped,
    never of how anyone was lost.
- **Mines are never props.** No mine objects, no "defuse" mechanic,
  no collectible ordnance — only the signs, the tape, the stayed-away
  beach.

## Story sequence

1. **The road reaches the sea** — open water, reeds, and the ship
   too big for where it sits.
2. **Under the hull** — the level works around the pier and gangway;
   spilled grain and the open logbook tell who left in a hurry.
3. **After completion** — the travellers mark a safe path down to the
   pier, hang a lantern, and leave a sheltered camp above the
   tideline. Nobody sails the ship away; the shore is simply safer
   to stand on.

## Lighting and ambience

- Overcast maritime light; low sun through haze; wet sand reflect.
- Ambience: low surf, reed rattle, a distant buoy bell; gulls sparse.
- The ship creaks once — one groan, never an alarm.

## Performance plan

- Ship is one baked low-poly mesh with vertex-colour rust staining —
  no interior geometry.
- Beach, pier and gangway elevations reuse the standard board/ramp
  pieces; the level is the multi-elevation test case for the
  generator.
- Reeds instanced; two particle systems max (sea haze, gulls).

## Content safety invariants (tested)

- `tidewrack` story entry: `roadmapVisibility: silhouette`,
  `levelDetailVisibility: evident`.
- `referenceConfidence: verified`, `historicalReference:
  black-sea-2022`.
- No prop id containing "mine" (as ordnance), "bomb", "torpedo" or
  weapon vocabulary may appear in any roadmap or level prop list —
  the sign prop is `sign.mines` (a civil warning sign) and it is the
  only allowed token, enforced by tests.
