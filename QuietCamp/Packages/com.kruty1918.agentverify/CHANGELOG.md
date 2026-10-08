# Changelog

## 1.0.0 — 2025-09-30

Initial release.

- `AgentProbe` — scene inspection: name/path lookup, active/visible,
  interactable list, JSON scene dump, one-screen summary
- `AgentScreenshot` — headless `Camera.Render` → PNG capture
- `AgentInput` — simulated UI click/tap/drag via EventSystem + SendMessage
  escape hatch
- `AgentLog` — bounded console-log capture with marks and error counts
- `AgentCheck` — structured expectations (exists/active/visible/
  interactable/text-contains/no-errors) returning ok+detail, never throwing
- `AgentCommands` — JSON command file runner: loadScene, wait, screenshot,
  describe, summary, click, tap, drag, invoke, expect, log, quit
- `AgentAutoRun` — play-mode bootstrap executing a staged commands file
- `AgentBatch` (editor) — `-executeMethod` entry points for EditMode and
  PlayMode headless runs; exits 0/2 by results
- `Tools → Agent Verify` menu mirroring the agent entry points
- Tests, Documentation~, Samples~
