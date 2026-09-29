# com.kruty1918.ui-foundation

![UPM package](https://img.shields.io/badge/UPM-package-blue)
![version](https://img.shields.io/github/v/tag/kruty1918dev-ai/com.kruty1918.ui-foundation?label=version&sort=semver)

Reusable UI foundation: a stripe-wipe scene-transition overlay, canvas-group
motion tweening, and a runtime TMP tooltip service with pointer/select
triggers. Host supplies the canvas-scale policy and drives the services from
its composition root.

## Install (Unity Package Manager)

Package Manager → **+** → **Add package from git URL**:

```
https://github.com/kruty1918dev-ai/com.kruty1918.ui-foundation.git
```

or in `Packages/manifest.json`:

```json
"com.kruty1918.ui-foundation": "https://github.com/kruty1918dev-ai/com.kruty1918.ui-foundation.git#v0.1.0"
```

## API surface

| Type | Purpose |
|---|---|
| `ISceneTransitionService` / `ISceneTransitionLease` | Stripe-wipe overlay around scene loads; the lease holds the wipe open until the next scene is ready |
| `IUiMotionService` | Canvas-group fade/slide tweening with reduced-motion support |
| `IUiReducedMotionSource` | Host-supplied accessibility flag the tweens honor |
| `IUiTooltipService` | Runtime TMP tooltips with pointer/select triggers |

## Model

Reusable UPM package extracted from Moyva. No game-specific dependencies;
compose via your own installer/DI.

## Dependencies

- `com.unity.ugui`

## Releasing / updating

`main` is wired to CI that auto-tags releases: bump `"version"` in
`package.json`, push to `main`, and the `UPM release` workflow tags
`v<version>` automatically. Consumers pinned to a tag
(`...git#v0.1.0`) upgrade by changing the tag in `manifest.json`;
consumers on `...git` (HEAD) get the latest `main` on next resolve.
