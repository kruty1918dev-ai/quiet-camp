# Quiet Camp — «Тихий кемпінг»

Offline mobile puzzle game in Unity. Calm campsite composition: place guest tents on a grid while respecting path, shade, quiet and friend rules.

- **Unity**: 6000.6.2f1 (URP 17.6.0, Input System 1.20.0, uGUI 2.6.0, Newtonsoft JSON 3.2.2)
- **Platform**: Android, portrait. Bundle id `com.kruty1918.quietcamp`
- **Project folder**: `QuietCamp/`

## Open the project

1. Install Unity 6000.6.2f1 via Unity Hub with modules: Android Build Support, Android SDK & NDK Tools, OpenJDK.
2. In Unity Hub: **Add project from disk** → select `QuietCamp/` (contains `Assets`, `Packages`, `ProjectSettings`).
3. First open regenerates `Library/` and fills in default importer settings on some `.meta` files — commit the result.
4. Run **Tools → Quiet Camp → Setup Project** once: creates the dynamic TMP font asset from `DejaVuSans.ttf`, TMP settings, and the two Android build profiles.
5. Run **Tools → Demigiant → DOTween Utility Panel → Setup** if DOTween asks for setup.

## Structure

| Path | Contents |
|---|---|
| `Assets/QuietCamp` | Scenes (Boot, MainMenu, Camp), URP settings (QC_Low/Balanced/High), audio mixer, scripts, levels JSON |
| `Assets/QuietCamp/Resources/QuietCamp/Levels` | 60 campaign maps + QC_TEST |
| `Assets/ThirdParty` | Kenney models/UI, audio packs, DOTween (Plugins) |
| `Packages` | manifest + 9 local `com.kruty1918.*` UPM packages |

## Build profiles

Two Android profiles are created by the setup step:

- **Android Development** — APK, development build, `QC_TEST` define; for on-device testing.
- **Android Release** — AAB, development off; sign with a local keystore kept outside this repo.

## License notes

Kenney assets are CC0. DOTween is licensed per Demigiant terms. Audio bundle licenses are kept alongside the packs in `Assets/ThirdParty`.
