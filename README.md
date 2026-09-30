# Quiet Camp — «Тихий кемпінг»

Offline mobile puzzle game in Unity. Calm campsite composition: place guest
tents on a grid while respecting path, shade, quiet and friend rules.

- **Unity**: 6000.6.2f1 (URP 17.6.0, Input System 1.20.0, uGUI 2.6.0,
  Newtonsoft JSON 3.2.2, DOTween 1.2.760, Unity Test Framework 1.8.0)
- **Platform**: Android, portrait, IL2CPP, ARM64, OpenGLES3,
  Min API 26, linear color space
- **Bundle id**: `com.kruty1918.quietcamp`, version `0.1.0` (code 1)
- **Project folder**: `QuietCamp/`

## Open the project

1. Install Unity 6000.6.2f1 via Unity Hub with modules: Android Build
   Support, Android SDK & NDK Tools, OpenJDK.
2. In Unity Hub: **Add project from disk** → select `QuietCamp/`.
3. First open regenerates `Library/` and fills in default importer
   settings on some `.meta` files — commit the result.
4. Run **Tools → Quiet Camp → Setup Project** once (idempotent):
   imports TMP essentials, creates the dynamic `DejaVuSans SDF` font
   asset and TMP Settings, and the two Android build profiles.
5. Run **Tools → Demigiant → DOTween Utility Panel → Setup** if DOTween
   asks for setup.

## Architecture

Layered assemblies under `Assets/QuietCamp/Scripts/`:

| Assembly | Responsibility |
|---|---|
| `QuietCamp.Domain` | Pure C# rules: `BoardState`, `RuleEvaluator`, `CampSolver`, `CommandHistory`, `LevelContentValidator`. No UnityEngine logic. |
| `QuietCamp.Application` | `CampSession` (single mutation authority), `PlacementCommand`, `HintService`, `ProgressionService`, `TutorialDirector`, `CampEvent`, `qc.*.v1` save DTOs |
| `QuietCamp.Infrastructure` | `LevelLoader` (JSON from `Resources` via the LevelKit codec + `QuietCampLevelAdapter`), `AssetCatalog`, `QuietCampAudioCatalog`, `SaveAdapter` (modular save-system), `QuietCampLocalization` |
| `QuietCamp.Presentation` | `QuietCampBootstrap` (persistent composition root), `ScreenRouter`, `QcActionHandler`, uGUI screens via `QcUi`, `BoardRenderer`, `TentPresenter`, `PlacementController` (touch+mouse), `CameraFitter`, `DecorSpawner`, `FireFx` |

Rules of thumb:

- JSON under `Resources/QuietCamp/` is the level source of truth —
  parsed by the universal
  [LevelKit](https://github.com/kruty1918dev-ai/level-kit) codec
  (`com.kruty1918.levelkit`, local `file:` dependency) with the Quiet
  Camp format profile, adapted to typed `LevelData`, validated, then
  consumed as plain C#. Levels are authored by hand in **Tools →
  Level Kit → Level Designer** (grid painter, entity editing,
  validation, JSON save/load) — not auto-generated.
- UI and input never mutate board state directly; everything goes
  through `CampSession` commands.
- World layers 8–12: board, tents, obstacles, decor, rule overlays.

## Content

| Path | Contents |
|---|---|
| `Assets/QuietCamp/Scenes` | `Boot` → `MainMenu` → `Camp` (reusable, JSON-driven) |
| `Assets/QuietCamp/Resources/QuietCamp/Levels` | 60 campaign maps + `QC_TEST` |
| `Assets/QuietCamp/Resources/QuietCampLocales` | uk / en / de catalogs |
| `Assets/QuietCamp/Prefabs/Models` | Normalized wrappers for the 12 kit models |
| `Assets/QuietCamp/Settings/URP` | QC_Low / QC_Balanced / QC_High pipeline assets |
| `Assets/ThirdParty` | Kenney models/UI, audio packs, DOTween |
| `Packages` | manifest + 9 embedded `com.kruty1918.*` UPM packages (audio, save-system, localization, input-context, ui-actions, ui-foundation, notifications, runtime-diagnostics, adaptive-performance) + `com.kruty1918.levelkit` (sibling repo `../level-kit`, universal level authoring/JSON toolkit) |

## Build profiles

Created by the setup step under `Assets/Settings/Build Profiles/`:

- **Android Development** — development build, `QC_TEST` define.
- **Android Release** — development off; sign with a local keystore
  kept **outside** this repo.

## Verification

Batch commands (from repo root):

```bat
"%UNITY_6000%\Editor\Unity.exe" -batchmode -nographics -quit ^
  -projectPath QuietCamp -executeMethod QuietCamp.Editor.QuietCampProjectSetup.Run

"%UNITY_6000%\Editor\Unity.exe" -batchmode -projectPath QuietCamp ^
  -runTests -testPlatform EditMode -testResults TestResults_EditMode.xml
```

Current status and QA notes: `QA_REPORT_UA.md`.
Test instructions for the device: `README_TEST_UA.md`.

## License notes

Kenney assets are CC0. DOTween is licensed per Demigiant terms. Audio
bundle licenses are kept alongside the packs in `Assets/ThirdParty`.
