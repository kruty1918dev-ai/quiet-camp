# Changelog

## [1.0.0] — 2025-01

Initial release, extracted from Quiet Camp's visual layer.

- `Atmos/SkyGradient` skybox shader: zenith/horizon/ground gradient, sun disc + halo, night stars
- `Atmos/FoliageSway`, `Atmos/FireGlow`, `Atmos/FireFlame` stylized shaders
- `SkySpec` data model with `Day`/`Evening`/`Night` presets, `Lerp`, JSON serialization
- `Sky` static API — apply materials or specs to the scene
- `SkyController` component — named skies + smooth animated transitions
- `FoliageSway` helper — one-call material swap with per-color sharing
- `Campfire` builder + `CampfireSpec` (`Cozy`/`Bonfire`), `FireVisual`, `FireFlicker`
- Editor: Sky Designer window (live preview, save .mat/.json), Tools → Atmos menu
- Samples: sky presets JSON + demo snippet
