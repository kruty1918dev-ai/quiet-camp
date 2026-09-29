# com.kruty1918.runtime-diagnostics

![UPM package](https://img.shields.io/badge/UPM-package-blue)
![version](https://img.shields.io/github/v/tag/kruty1918dev-ai/com.kruty1918.runtime-diagnostics?label=version&sort=semver)

Reusable runtime diagnostics: health-check aggregation service, pluggable
health reporters, and global async/unhandled error logging.

## Install (Unity Package Manager)

Package Manager → **+** → **Add package from git URL**:

```
https://github.com/kruty1918dev-ai/com.kruty1918.runtime-diagnostics.git
```

or in `Packages/manifest.json`:

```json
"com.kruty1918.runtime-diagnostics": "https://github.com/kruty1918dev-ai/com.kruty1918.runtime-diagnostics.git#v0.1.0"
```

## API surface

| Type | Purpose |
|---|---|
| `IHealthCheckService` | Aggregates registered reporters into an overall health verdict |
| `IHealthReporter` | One probe per subsystem (network, services, content) |
| `HealthStatus` | Healthy / degraded / unhealthy verdict |

## Model

Reusable UPM package extracted from Moyva. No game-specific dependencies;
compose via your own installer/DI. Unobserved task exceptions and unhandled
errors are captured globally so "silent" async failures still reach the log.

## Releasing / updating

`main` is wired to CI that auto-tags releases: bump `"version"` in
`package.json`, push to `main`, and the `UPM release` workflow tags
`v<version>` automatically. Consumers pinned to a tag
(`...git#v0.1.0`) upgrade by changing the tag in `manifest.json`;
consumers on `...git` (HEAD) get the latest `main` on next resolve.
