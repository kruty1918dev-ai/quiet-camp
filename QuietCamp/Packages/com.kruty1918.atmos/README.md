# Atmos — Sky & Stylized Shaders

A small Unity package for **calm casual games**: a procedural gradient skybox
with sun and stars, vertex-sway foliage, and a one-call procedural campfire.
Everything is texture-free, cheap on mobile, and driven by data — swap a
`SkySpec` or `CampfireSpec` and the whole mood changes.

Used in production by [Quiet Camp](https://github.com/kruty1918dev-ai/quiet-camp).

## Features

| Piece | What it does |
|---|---|
| `Atmos/SkyGradient` shader | Zenith→horizon→ground gradient skybox, soft sun disc + halo, optional twinkling stars |
| `SkySpec` | Serializable sky palette (colors, sun, stars, fog) + `Day`/`Evening`/`Night` presets, `Lerp`, JSON round-trip |
| `Sky` | `Apply(material)` / `Apply(spec)` — skybox + camera clear flags + fog + GI refresh in one call |
| `SkyController` | Named skies on a component; `Set("evening")` or smooth `TransitionTo("night", 3f)` |
| `Atmos/FoliageSway` shader + `FoliageSway` helper | Gentle vertex breeze for grass/flowers; shared materials per color |
| `Atmos/FireFlame`, `Atmos/FireGlow` shaders | Texture-free animated flames and radial ground glow |
| `Campfire` + `CampfireSpec` | `Campfire.Create(parent)` builds glow + flames + embers + flicker light; `Cozy`/`Bonfire` presets |
| Sky Designer window | Tools → Atmos → Sky Designer — live-preview a spec in the scene, save as `.mat` or `.json` |

## Installation

**Via git URL** (Package Manager → *Add package from git URL*):

```
https://github.com/kruty1918dev-ai/atmos.git
```

**Via manifest** (`Packages/manifest.json`):

```json
"com.kruty1918.atmos": "https://github.com/kruty1918dev-ai/atmos.git"
```

or a local checkout: `"com.kruty1918.atmos": "file:../atmos"`.

Requires Unity 2021.3+. Works with URP and Built-in (shaders are plain CG
passes; ember particles prefer URP Particles/Unlit and fall back to
Sprites/Default). For device builds, add the four `Atmos/*` shaders to
**Always-Included Shaders** or reference them from materials in scenes.

## Quick start

```csharp
using Kruty1918.Atmos;

// Instant sky — no assets needed, material is created at runtime.
Sky.Apply(SkySpec.Evening, Camera.main);

// Your own palette.
var spec = SkySpec.FromJson(jsonText.text);
Sky.Apply(spec);
```

```csharp
// Day → dusk transitions from a component.
var ctrl = gameObject.AddComponent<SkyController>();
ctrl.skies.Add(new SkyController.NamedSky { name = "day",     spec = SkySpec.Day });
ctrl.skies.Add(new SkyController.NamedSky { name = "evening", spec = SkySpec.Evening });
ctrl.Set("day");
ctrl.TransitionTo("evening", duration: 4f);   // smooth lerp, incl. stars fading in
```

```csharp
// A burning campfire under any transform; toggle it with the returned handle.
var fire = Campfire.Create(firePit.transform);          // CampfireSpec.Cozy
var big  = Campfire.Create(pit.transform, CampfireSpec.Bonfire);
fire.SetBurning(false);   // unlit fire ring by day
```

```csharp
// Grass and flowers breathe in the wind — one call per decor prefab.
FoliageSway.Shared.Apply(grassInstance);
```

## Authoring skies

**Tools → Atmos → Sky Designer** opens a window where every field of the
`SkySpec` is editable with **live preview** on the scene skybox. Save the
result either as a `.mat` (assign to `RenderSettings.skybox` or materials)
or as `.json` (load into `SkySpec.FromJson` / `SkyController`).

**Tools → Atmos → Add Sky Controller** drops a ready component with the
three built-in presets; **Add Campfire** spawns a lit fire under the
current selection.

## Samples

Import **Sky Presets & Demo Snippets** from the package's *Samples* tab:
`sky.day.json`, `sky.evening.json`, `sky.night.json` plus `AtmosDemo.cs`
showing the full API.

## Design notes

- Texture-free: the sky, flames and glow are pure math — tiny builds, no imports.
- Mobile-cheap: one overdraw background pass for the sky, vertex anim only for foliage.
- Data-driven: `SkySpec` and `CampfireSpec` are plain serializable classes — keep them
  in components, JSON files or generate them procedurally.
- No singletons, no hidden state: `Sky` is stateless; caches live behind
  explicit handles (`FoliageSway.Shared`, `SkyController.CurrentMaterial`).

## License

MIT — see [LICENSE.md](LICENSE.md).
