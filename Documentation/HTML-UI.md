# Quiet Camp HTML UI

All player-facing screens mount through [Moyva UnityHTML](https://github.com/kruty1918dev-ai/com.kruty1918.moyva.unityhtml). The project pins the published package at `ed36a7ce864f595432033da6f6b75c43ec92e7d9` in `Packages/manifest.json`; native event routing is supplied by that dependency rather than an embedded game-local package. ReactUnity dependencies retain their compatible Git pins. No npm build or browser runtime is required.

## Authoring

`QuietCamp/Assets/QuietCamp/Resources/QuietCamp/Html/` contains the menu, gameplay and sheet HTML shells and `Camp.css.txt`. The `.txt` suffix is deliberate: Unity imports CSS as a TextAsset. Dynamic guest, level, album and settings rows are escaped HTML composed by their presenters. `HtmlSurface` owns the native HTML host, reconciliation, fonts, responsive layout, reduced motion and disposal. Event attributes bind directly to the registered C# Click/Number/Toggle callbacks through `NativeEventResolver`; game surfaces never initialize QuickJS or Jint. Unknown expressions and script tags fail explicitly. The default package host still supports JavaScript for other consumers.

Palette follows the existing menu illustration and `Design/ImmersiveUI`: forest `#243e35`, cream `#f3efe3`, muted ink `#526756`, warm selection `#fff1dc`. The menu and gameplay forest share native world-space scenery, ground and lighting. The board, tents, placement previews and foliage scene transition remain native rendering elements.

## Screen coverage

- Boot splash and main menu.
- Level selection, album and demo completion.
- Shared settings: music, ambience, effects, scroll, reduced motion, calm mode, contrast, haptics, language and text size. Progress reset requires an in-game confirmation.
- Camp HUD: hint and pause at the top; a full-width guest card with a tent preview, name and wish icons below a separate row for guests, undo, redo and check. Redo appears when available; check appears once all tents are placed. The card selects a guest; holding for 200 ms or moving 8 dp lifts a 3D tent card. A board tap remains available.
- Guest list opens on demand. The selected tent has one anchored name with rotate/remove controls; the chip follows its animated position without rebuilding HTML every frame.
- One rule issue after checking, a live placement explanation, tutorial messages, pause, hint, completion and in-game settings. Completion stays near the bottom without dimming the camp and waits for an explicit Next tap.
- Persistent notifications.

All game actions continue through the existing action router and `CampSession`. Sheets keep their gameplay input lease through the release frame; HTML containers have no full-screen raycast graphic outside modals. Localized content updates without rebuilding the owning scene. Stable ids preserve control identity and scroll position during reconciliation. The package's native slider subparts receive the camp palette and larger thumb/value text in `HtmlSurface` because those internal elements are outside its CSS tree.

`QcUi` supplies native mount roots, resource sprites and compatibility tests. If a document fails three mount attempts, `HtmlRecoveryControls` presents its registered actions with ordinary uGUI controls. Repeated failed refreshes replace the recovery panel without losing the buttons; a successful retry removes it. Failed mounts do not emit `Mounted`. A zero-sized startup canvas waits for display layout without consuming retries.

## Shared appearance and motion

Menu, gameplay, settings, pause, level selection, album and completion use the same forest primary actions, parchment cards, circular icon controls, gold selection borders and typography from `Camp.css.txt`. Native slider and switch thumbs share a neutral circle and the same palette through `CampUiTheme`.

`CampMotion` owns timing: press 120 ms, release 200 ms, change 240 ms, entry 300 ms and exit 200 ms. Calm mode stretches these by 1.6. Buttons react through `CampControlFeedback`; switch thumbs interpolate between their states, and slider thumbs respond while dragging. DOTween runs UI feedback on unscaled time, cancels replaced tweens, and kills them when targets disappear. Actions dispatch immediately. Reduced motion snaps transforms and preserves exit callbacks.

HTML declares motion roles for header/footer, brand, sheet, sheet body, tutorial, selected-tent chips and notifications. Settings category changes replace only the keyed body. Sheets close after the sheet's own exit callback; notifications fade out before their completion callback. CSS does not animate those transforms independently.

Tent appearance, movement, rotation, selection, removal and preview snapping use the same rhythm. Retiring tents immediately lose their colliders; committed logical state stays independent of animation. Preview movement uses a short 65 ms retarget and a narrow cell hysteresis band to keep dragging responsive without border flicker. A tap selects an existing tent; a drag starts only after 8 dp. Releasing, canceling or interrupting a drag ends its actual pointer capture before clearing the pointer id. Failed drops clear the preview and preserve the committed layout. Route and footprint tiles are pooled and tinted with MaterialPropertyBlock; routes include the candidate footprint and the open entry cell.

Slider changes update audio/settings immediately and debounce disk writes for 250 ms; release and surface teardown flush the latest value. Native control themes run after reconciliation/layout, rather than rewriting every slider each frame.

## Menu composition

The menu leaves the centre open for the campfire and tents. A modest cream wordmark floats over a full-frame atmospheric gradient; it has no separate dark card. Settings has one corner control, and levels/album have one compact parchment destination each below the primary action. Stable ids and the shared motion roles are retained.

The menu camera fits the clearing between 12% and 72% of the safe-area height with a 52-degree pitch, leaving the camp above the bottom navigation. The world uses olive/sage foliage, warm grey stone, brown wood and terracotta canvas, consistently in menu and gameplay. `MvpContentBuilder.CampColor` preserves that authored palette when importing the source models again.

## Verification

The PlayMode control test mounts real HTML, changes a slider and toggle, and checks that a second reconciliation preserves the button without doubling callbacks. The screenshot/navigation test visits menu settings, levels, camp, pause, in-game settings and completion using pointer events and checks that the center of the gameplay screen is not intercepted by HTML. It configures a portrait Game view before capturing the real Unity screens.

Run in Unity 6000.6.2f1:

```sh
Unity -force-glcore -projectPath QuietCamp -runTests -testPlatform PlayMode \
  -testFilter 'QuietCamp.Tests.UiShaderPlayModeTests;QuietCamp.Tests.ScreenshotPlayModeTest' \
  -testResults TestResults/html-ui-playmode.xml -logFile BuildLogs/html-ui-playmode.log
```

Android device and IL2CPP validation must be performed separately from editor tests.

Before the native event repair, Android used QuickJS while the Linux Editor used
Jint. The game's `link.xml`
preserves UnityHTML, ReactUnity/UGUI, Yoga, QuickJS value converters and the
reflection-discovered `ReflectBindDelegateGen` templates. Without these rules,
IL2CPP can leave the template type with zero methods, so the JavaScript host
cannot bind callbacks. This defect was found in the Android build even though
the Linux Editor UI tests passed. See the
[Android UI regression report](../TestResults/android-ui-2026-10-03/REPORT_UA.md).

That retention-only repair did not resolve the reported phone issue. Game
surfaces now use the same C# event path in Editor and Android. The
`AndroidHtmlUiBuildFilter` excludes the unused `libquickjs.so` from Android
players through Unity's [plugin build inclusion API](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/PluginImporter.SetIncludeInBuildDelegate.html);
the original dependency remains available for Editor and package compatibility.
The newer evidence is in the [native UI recovery report](../TestResults/android-ui-recovery-2026-10-03/REPORT_UA.md).

After every Android IL2CPP build, audit the actual stripped DLLs and generated
native callback bodies; checking the Editor DLLs cannot detect this failure:

```sh
python3 tools/verify_android_ui.py /tmp/quietcamp-live-build/QuietCamp \
  --unity-editor /home/oleks/Unity/Hub/Editor/6000.6.2f1/Editor
```

The audit requires the installed .NET SDK and Unity's bundled Mono.Cecil. A
nonzero exit rejects the build. It also verifies the game's click/slider/switch
bridge and JavaScript-facing ReactInterop methods. Runtime checks on a phone
remain necessary; this audit validates IL2CPP retention and AOT generation.

Visual capture requires a rendered Game View (run without `-batchmode`). It captures the composed screen and waits for the PNG before navigating, preserving overlay canvas order. The visual suite also checks narrow portrait placement/undo/redo/completion, duplicate guest labels, tutorial separation, and uk/en/de at 130% text size. Batch runs skip those visual tests; other PlayMode tests remain batch-compatible.

## Live Camp update, 2026-10-02

UnityHTML 0.1.1 uses the native `switch` element. Game callbacks use `Globals.campUi` because `Globals.ui` belongs to package navigation. Model reconciliation is silent; the label and knob dispatch through one native Toggle. No game thumb is added to a package switch.

The menu gradient lives under CanvasRoot, separately from safe-area content. Level selection is a full-screen native-mesh roadmap inside one vertical HTML scroll; lightweight `level_summaries.json` supplies challenge icons without loading levels or invoking a solver. Scroll state lives in GameServices and returns when navigating back.

AlbumDiorama owns one active world and reuses the scene camera. The HTML viewport accepts drag rotation; snapshot miniatures use UI meshes and allocate no extra cameras. Switching releases board materials, atmosphere, fog and bird pools. New completions save an immutable level snapshot, hash and lighting; old albums use LegacyLevels.

Visual checks use `LiveCampVisualTests`, both portrait sizes (720×1600 and 1080×1920), uk/en/de and 130% text. Test projects use a separate productName and save directory. Current results are in `TestResults/live-camp-2026-10-02/REPORT_UA.md`.
