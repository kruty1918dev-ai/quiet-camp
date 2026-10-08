# AgentVerify

Verification toolkit for AI agents (and humans) working on Unity games.

## Concepts

An agent cannot see the game. AgentVerify gives it five senses plus a
checklist format:

1. **Probe** (`AgentProbe`) — what's in the scene. `Find` by name or
   `Root/Child/Path`, `Exists`, `IsActive`, `IsVisible` (renderer enabled,
   on screen, or UI graphic with alpha), `Interactables()` (enabled
   Selectables), `DescribeJson()` (full dump), `Summary()` (quick text).
2. **Screenshot** (`AgentScreenshot`) — play mode default is
   `ScreenCapture` (whole game view *including* Screen-Space-Overlay UI,
   which camera renders cannot see); a named `camera`, or edit mode, uses
   `Camera.Render` into a RenderTexture — headless, no swapchain needed.
3. **Input** (`AgentInput`) — `Click(name)`, `Tap(px)`, `Drag(a,b)` go
   through `ExecuteEvents` / `EventSystem.RaycastAll`, so they trigger the
   same handlers a real user would. `Invoke(name, method)` is a
   `SendMessage` escape hatch.
4. **Log** (`AgentLog`) — hooks `Application.logMessageReceived`,
   bounded buffer, `Mark()` + `Since(mark)` + `ErrorCount(mark)` +
   `IsClean(mark)`.
5. **Check** (`AgentCheck.Expect`) — named assertions that never throw:
   each returns `{ok, detail}` so a script reports *all* outcomes.

## The commands file — agent interface

`agentverify.commands.json` → `agentverify.results.json`
(paths overridable via `AGENTVERIFY_COMMANDS` / `AGENTVERIFY_RESULTS`).

Execution:
- `RunAllAsync` coroutine in play mode — `wait` uses
  `WaitForSecondsRealtime` so animation/scene loads actually progress; a
  frame passes between commands so UI layout settles.
- `RunAll` synchronous in edit mode — `wait` becomes a real sleep.
- `AgentAutoRun` hooks `RuntimeInitializeOnLoadMethod(AfterSceneLoad)` —
  if the commands file exists when play starts, it runs the script and
  writes results; in batchmode it quits with code 0/2.
- `AgentBatch.RunPlayMode` / `RunEditMode` are `-executeMethod` entry
  points that manage the whole flow and exit code.

## Checks

`exists` `missing` `active` `inactive` `visible` `interactable`
`text-contains` (Text or TMP via reflection) `no-errors` (no
Error/Exception/Assert captured this session).

Unknown commands and unknown checks produce `ok:false` results — never
exceptions — so a bad script reports cleanly instead of crashing the run.

## Recipes

**"Does the menu render?"**
`screenshot` + `expect exists MainCamera` + `expect no-errors`.

**"Does the button flow into gameplay?"**
`click PlayButton` → `wait 2` → `expect exists <board root>` →
`screenshot`.

**"Did my change break anything visually?"**
baseline `screenshot`/`describe` → make change → same commands → compare
PNGs and dumps.

**Regression suite in CI**
one commands file per flow; loop over them; exit code gates the job.

## Limits

- `loadScene` needs the scene in Build Settings (or an additive-loaded
  path resolvable by `SceneManager`).
- UI input requires an `EventSystem` and `GraphicRaycaster` — standard
  for uGUI projects.
- `visible` is a heuristic (bounds in viewport + enabled renderer /
  non-transparent graphic), not pixel-perfect occlusion.
- `text-contains` reads `UnityEngine.UI.Text` directly; TMP text is read
  via reflection so the package has no TMP dependency.
