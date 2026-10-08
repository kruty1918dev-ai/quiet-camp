# AgentVerify

**Verification toolkit for AI agents working on Unity games** — and for
anyone who wants a game to be checkable from a command line.

An agent's loop in Unity is usually: change code → compile → *hope it
works*. AgentVerify closes the loop. It gives the agent structured eyes
(scene dumps), a camera (deterministic screenshots), hands (simulated UI
input), ears (console-log capture) and a checklist format (declarative
`expect` commands) — so the agent verifies *what the game actually does
and looks like* and reports machine-readable results instead of guessing.

## What's inside

| Piece | API | Purpose |
|---|---|---|
| Scene probe | `AgentProbe` | find objects by name/path, `Exists`/`IsActive`/`IsVisible`, list interactables, `DescribeJson()` dump |
| Screenshots | `AgentScreenshot` | render any camera to PNG — works headless and in batchmode |
| Input | `AgentInput` | `Click("PlayButton")`, `Tap`, `Drag` through the real EventSystem, `Invoke` escape hatch |
| Log capture | `AgentLog` | `Mark()`/`Since()`/`ErrorCount()` — "did anything error since my mark?" |
| Checks | `AgentCheck.Expect` | structured `exists/active/visible/interactable/text-contains/no-errors` results, no exceptions |
| Command file | `AgentCommands` | a JSON script of commands → results JSON — the primary headless interface |
| Auto-run | `AgentAutoRun` | play-mode bootstrap: if a commands file is staged it runs on scene load |
| Batch entries | `AgentBatch` (editor) | `-executeMethod` entry points for EditMode and PlayMode runs |

## Install

Add to `Packages/manifest.json`:

```json
"com.kruty1918.agentverify": "https://github.com/kruty1918dev-ai/agentverify.git"
```

or a local `file:` path. Requires uGUI (`com.unity.ugui`) for input simulation — already in every UI project.

## The agent loop (headless)

1. Write `agentverify.commands.json` in the project:

```json
{
  "commands": [
    {"cmd": "loadScene", "target": "MainMenu"},
    {"cmd": "wait", "seconds": 1},
    {"cmd": "screenshot", "output": "Temp/shots/menu.png"},
    {"cmd": "summary"},
    {"cmd": "click", "target": "PlayButton"},
    {"cmd": "wait", "seconds": 2},
    {"cmd": "expect", "check": "exists", "target": "CampBoard"},
    {"cmd": "screenshot", "output": "Temp/shots/game.png"},
    {"cmd": "expect", "check": "no-errors"},
    {"cmd": "log", "output": "Temp/log.json"}
  ]
}
```

2. Run Unity once:

```bash
# against the live game (play mode, full pipeline):
Unity.exe -batchmode -projectPath . \
  -executeMethod Kruty1918.AgentVerify.EditorTools.AgentBatch.RunPlayMode

# or against the open editor scene (no play):
Unity.exe -batchmode -projectPath . \
  -executeMethod Kruty1918.AgentVerify.EditorTools.AgentBatch.RunEditMode
```

3. Read `agentverify.results.json`:

```json
{"allOk": false, "results": [
  {"cmd":"click","ok":true,"detail":"clicked"},
  {"cmd":"expect","ok":false,"detail":"not found"}
]}
```

Exit code is `0` when every command succeeded, `2` otherwise, `1` on
setup errors — so CI and agents can branch on it directly. Screenshot
PNG files are written wherever the commands asked.

## Command reference

| cmd | fields | effect |
|---|---|---|
| `loadScene` | `target` | `SceneManager.LoadScene(target)` — name or build path |
| `wait` | `seconds` | realtime wait (coroutine in play mode → time/anims advance) |
| `screenshot` | `output`, `camera?`, `width?`, `height?` | PNG — full game view incl. overlay UI in play mode; `camera` forces a headless render |
| `describe` | `output?` | full scene JSON dump → file or result payload |
| `summary` | — | compact text: scenes, main camera, interactables, object count |
| `click` | `target` | click UI element by name or `Path/To/Object` |
| `tap` | `x`,`y` (0..1) | EventSystem raycast tap at normalized screen pos |
| `drag` | `x`,`y`,`x2`,`y2` | drag between normalized screen positions |
| `invoke` | `target`,`method` | `SendMessage` on the object — game-specific escape hatch |
| `expect` | `check`,`target?`,`contains?` | one assertion; result in `ok`/`detail` |
| `log` | `output?` | captured console entries as JSON |
| `quit` | — | stop processing further commands |

`expect` checks: `exists`, `missing`, `active`, `inactive`, `visible`,
`interactable`, `text-contains` (needs `contains`), `no-errors` (console
clean since session start).

Custom paths via env vars: `AGENTVERIFY_COMMANDS`, `AGENTVERIFY_RESULTS`.

## Programmatic use

```csharp
using Kruty1918.AgentVerify;

var mark = AgentLog.Mark();
AgentInput.Click("PlayButton");
if (!AgentProbe.Exists("CampBoard")) { /* wrong scene */ }
AgentScreenshot.Capture("Temp/game.png");
if (!AgentLog.IsClean(mark)) { /* errors during click */ }
```

Or a checklist without boilerplate:

```csharp
var results = AgentCommands.RunAll(AgentCommands.FromJson(json));
Debug.Log(AgentCommands.ResultsToJson(results));
```

## In-editor use

`Tools → Agent Verify` menu: describe open scene, screenshot the scene
camera, run the command file, write a sample command file.

## Design notes

- **Structured, not exceptional** — every check returns `ok` + `detail`;
  a verification script always runs to the end and reports everything.
- **Headless-first** — screenshots use `Camera.Render` + `RenderTexture`,
  input uses `ExecuteEvents` — no display server, no OS input needed.
- **Zero dependencies** beyond `com.unity.ugui`; no Newtonsoft, no
  test-framework requirement for the runtime API.
- **Same tools for agents and humans** — menu items mirror every agent
  entry point so behaviour stays debuggable.
- Tests live under `Tests/` — add the package to `testables` to run them.

## Samples

`Samples~/Commands/` — ready-made command files: a menu→gameplay smoke
flow and a visual-verification flow. Import via Package Manager →
AgentVerify → Samples, or copy the JSON into `Temp/`.

## License

MIT — see `LICENSE.md`.
