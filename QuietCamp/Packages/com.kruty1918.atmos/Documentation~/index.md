# Atmos — Documentation

Atmos is a small toolkit of **procedural atmosphere** pieces for calm casual
games. It has three pillars, all texture-free and data-driven:

1. **Sky** — `Atmos/SkyGradient` shader + `SkySpec` palette + `Sky`/`SkyController` API
2. **Foliage** — `Atmos/FoliageSway` shader + `FoliageSway` material helper
3. **Fire** — `Atmos/FireFlame` + `Atmos/FireGlow` shaders + `Campfire` builder

## The sky pipeline

```
SkySpec ──ApplyTo──> Material (Atmos/SkyGradient) ──Sky.Apply──> RenderSettings.skybox
```

- **Fixed palettes** (a day/evening pair shipped with the game): author `.mat`
  assets in the Sky Designer, then call `Sky.Apply(dayMat)` — zero runtime
  allocation, materials live in your Resources.
- **Runtime-defined or animated skies**: keep `SkySpec` data (component fields,
  JSON files, procedural) and let `Sky.Apply(spec)` or `SkyController`
  create/lerp a runtime material.

`Sky.Apply` also: sets the camera to `Sky` clear flags, applies or
disables fog per spec, and calls `DynamicGI.UpdateEnvironment()` so ambient
light follows the sky.

### SkySpec fields

| Field | Effect |
|---|---|
| `zenith` / `horizon` / `ground` | Three-stop vertical gradient |
| `sunColor`, `sunDir`, `sunSize`, `sunHalo` | Sun disc position/size + soft glow |
| `horizonFalloff` | Tightness of the horizon band |
| `stars` | 0–1 star field intensity (only visible in dark zenith) |
| `fog`, `fogColor`, `fogDensity` | Optional exponential fog applied with the sky |

### Transitions

```csharp
ctrl.TransitionTo("evening", 4f);
```

`SkyController` keeps a single runtime material and lerps every field,
including star intensity — a smooth day→dusk blend.

## Foliage

`FoliageSway.Shared.Apply(go)` swaps each renderer under `go` to
`Atmos/FoliageSway`, preserving its base color and sharing one material per
color. Sway amplitude/frequency are per-material; the shader animates vertices
by height above the object pivot, so roots stay planted.

## Campfire

```csharp
var fire = Campfire.Create(parent);                 // CampfireSpec.Cozy
var bonfire = Campfire.Create(parent, CampfireSpec.Bonfire);
fire.SetBurning(false);   // hide flames/glow/light/embers
```

Creates: `FireGlow` quad, `Flame0..n` crossed quads, `FireParticles` embers,
`FireLight` point light with `FireFlicker`. All spec-driven — colors, radii,
counts, flicker strength.

## Build checklist for device builds

- Add `Atmos/SkyGradient`, `Atmos/FoliageSway`, `Atmos/FireGlow`,
  `Atmos/FireFlame` to *Project Settings → Graphics → Always Included Shaders*,
  or keep them referenced by materials/prefabs that ship in scenes.
- Ember particles use `Universal Render Pipeline/Particles/Unlit` when present,
  else `Sprites/Default`.

## Editor tooling

- **Tools → Atmos → Sky Designer** — live-preview spec editing; save `.mat`/`.json`.
- **Tools → Atmos → Add Sky Controller** — component with day/evening/night presets.
- **Tools → Atmos → Add Campfire** — lit campfire under the selected object.
