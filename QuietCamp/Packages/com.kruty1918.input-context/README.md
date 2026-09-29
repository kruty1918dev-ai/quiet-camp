# Kruty1918 Input Context

![UPM package](https://img.shields.io/badge/UPM-package-blue)
![version](https://img.shields.io/github/v/tag/kruty1918dev-ai/com.kruty1918.input-context?label=version&sort=semver)

Gameplay input routing policy for Unity: pointer-over-UI detection,
per-pointer capture, global input-blocking leases and text-input focus checks —
all behind the `IGameplayInputPolicy` contract. Keeps world input and UI input
from fighting each other.

## Install (Unity Package Manager)

Package Manager → **+** → **Add package from git URL**:

```
https://github.com/kruty1918dev-ai/com.kruty1918.input-context.git
```

or in `Packages/manifest.json`:

```json
"com.kruty1918.input-context": "https://github.com/kruty1918dev-ai/com.kruty1918.input-context.git#v0.1.0"
```

## Layout

| Folder | Contents |
|---|---|
| `Runtime/` | `IGameplayInputPolicy` contract and default implementation |

## API surface

| Type | Purpose |
|---|---|
| `IGameplayInputPolicy` | Answers "is gameplay input allowed right now?" — pointer-over-UI, captured pointers, blocking leases, focused text fields |
| `GameplayInputKind` | Input categories the policy distinguishes |

## Model

- Gameplay code asks the policy instead of duplicating
  `EventSystem.IsPointerOverGameObject` checks.
- Blocking leases are ref-counted — several UI layers can hold the world
  input lock simultaneously without trampling each other.

## Dependencies

- `com.unity.ugui`

## Releasing / updating

`main` is wired to CI that auto-tags releases: bump `"version"` in
`package.json`, push to `main`, and the `UPM release` workflow tags
`v<version>` automatically. Consumers pinned to a tag
(`...git#v0.1.0`) upgrade by changing the tag in `manifest.json`;
consumers on `...git` (HEAD) get the latest `main` on next resolve.
