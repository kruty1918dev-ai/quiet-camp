# com.kruty1918.adaptive-performance

![UPM package](https://img.shields.io/badge/UPM-package-blue)
![version](https://img.shields.io/github/v/tag/kruty1918dev-ai/com.kruty1918.adaptive-performance?label=version&sort=semver)

Reusable adaptive-performance primitives: frame-time budget monitoring with
percentile snapshots and startup resource prewarm.

## Install (Unity Package Manager)

Package Manager → **+** → **Add package from git URL**:

```
https://github.com/kruty1918dev-ai/com.kruty1918.adaptive-performance.git
```

or in `Packages/manifest.json`:

```json
"com.kruty1918.adaptive-performance": "https://github.com/kruty1918dev-ai/com.kruty1918.adaptive-performance.git#v0.1.0"
```

## API surface

| Type | Purpose |
|---|---|
| `IFrameBudgetMonitorService` | Frame-time budget monitoring with percentile snapshots (p50/p95/p99) |
| `IStartupPrewarmService` | Startup resource prewarm (shaders, meshes) before first interactable frame |
| `IScenePreActivationInitializer` | Work that must finish before a scene activates |
| `FrameBudgetSettings` / `FrameTimeSnapshot` | Config and snapshot value types |
| `PrewarmSettings` | Prewarm configuration |

## Model

Reusable UPM package extracted from Moyva. No game-specific dependencies;
compose via your own installer/DI.

## Releasing / updating

`main` is wired to CI that auto-tags releases: bump `"version"` in
`package.json`, push to `main`, and the `UPM release` workflow tags
`v<version>` automatically. Consumers pinned to a tag
(`...git#v0.1.0`) upgrade by changing the tag in `manifest.json`;
consumers on `...git` (HEAD) get the latest `main` on next resolve.
