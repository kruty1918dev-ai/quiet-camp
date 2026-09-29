# Kruty1918 UI Actions

![UPM package](https://img.shields.io/badge/UPM-package-blue)
![version](https://img.shields.io/github/v/tag/kruty1918dev-ai/com.kruty1918.ui-actions?label=version&sort=semver)

Game-agnostic UI action routing for Unity: an action router with
duplicate-registration checks, a context stack with layers/priorities, hotkey
bindings with conflict detection, and escape routing. Game projects supply
their own action-id catalog and default keymap.

## Install (Unity Package Manager)

Package Manager → **+** → **Add package from git URL**:

```
https://github.com/kruty1918dev-ai/com.kruty1918.ui-actions.git
```

or in `Packages/manifest.json`:

```json
"com.kruty1918.ui-actions": "https://github.com/kruty1918dev-ai/com.kruty1918.ui-actions.git#v0.1.0"
```

## Layout

| Folder | Contents |
|---|---|
| `Runtime/` | Router, context stack, hotkey service, escape router |
| `Runtime/API/` | Contracts and value types |

## API surface

| Type | Purpose |
|---|---|
| `IUiActionRouter` | Dispatch `UiActionRequest` to registered handlers |
| `IUiActionHandler` | Handles one action id; routing checks duplicate registrations |
| `IUiContextStack` | Layered contexts (gameplay / panel / modal) with priorities — topmost wins |
| `IUiHotkeyService` | Key → action bindings with conflict detection and trigger modes |
| `IUiEscapeRouter` | Deterministic Escape handling across stacked UI layers |
| `IUiActionJournal` | Optional journal of dispatched actions (audit/debug) |
| `IUiActionFeedbackSink` | Feedback seam for accepted/rejected actions |
| `UiActionRequest` / `UiActionResult` | Request/response value types (`UiActionStatus`, `UiActionReason`, `UiActionSource`) |

## Model

- Actions are dispatched against the active context stack — a modal layer
  shadows gameplay hotkeys automatically.
- Hotkey conflicts and duplicate handler registrations are rejected loudly
  at registration time, not at press time.

## Dependencies

- `com.unity.inputsystem`
- `com.unity.ugui`
- `com.kruty1918.input-context`

## Releasing / updating

`main` is wired to CI that auto-tags releases: bump `"version"` in
`package.json`, push to `main`, and the `UPM release` workflow tags
`v<version>` automatically. Consumers pinned to a tag
(`...git#v0.1.0`) upgrade by changing the tag in `manifest.json`;
consumers on `...git` (HEAD) get the latest `main` on next resolve.
