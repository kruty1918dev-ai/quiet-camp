# Atmos Samples — Sky Presets & Demo

- `sky.day.json` / `sky.evening.json` / `sky.night.json` — ready-made `SkySpec`
  presets. Load with `SkySpec.FromJson(textAsset.text)` or drop them into a
  `SkyController`.
- `AtmosDemo.cs` — a self-contained demo component: swaying grass primitives,
  a campfire, and number-key sky transitions (1 = day, 2 = evening + fire,
  3 = night).

Quick try: create an empty GameObject in a scene, add `AtmosDemo`, press Play.
