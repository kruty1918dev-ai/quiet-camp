# Kruty1918 Notifications

![UPM package](https://img.shields.io/badge/UPM-package-blue)
![version](https://img.shields.io/github/v/tag/kruty1918dev-ai/com.kruty1918.notifications?label=version&sort=semver)

Gameplay notification queue for Unity: dedup keys, hold durations, FIFO
presentation through an `IGameplayNotificationPresenter` contract, plus a
default TMP/CanvasGroup toast presenter (DOTween-accelerated when available).

## Install (Unity Package Manager)

Package Manager → **+** → **Add package from git URL**:

```
https://github.com/kruty1918dev-ai/com.kruty1918.notifications.git
```

or in `Packages/manifest.json`:

```json
"com.kruty1918.notifications": "https://github.com/kruty1918dev-ai/com.kruty1918.notifications.git#v0.1.0"
```

## Layout

| Folder | Contents |
|---|---|
| `Runtime/` | Queue service, contracts, default toast presenter |
| `Runtime/API/` | `IGameplayNotificationService`, request/kind contracts |

## API surface

| Type | Purpose |
|---|---|
| `IGameplayNotificationService` | `Show` / `Clear` — enqueue notifications for the player |
| `IGameplayNotificationPresenter` | Presentation seam — swap the default toast for custom UI |
| `GameplayNotificationRequest` | Message + kind + hold duration + dedup key |
| `GameplayNotificationKind` | Info / warning / error-style severities |
| `GameplayNotificationStream` | Static stream so non-DI callers can still publish |

## Model

- Notifications with the same `DedupKey` collapse instead of spamming.
- `HoldDuration` controls how long a notification stays visible.
- The service is engine-agnostic; visuals live behind the presenter contract.

## Dependencies

- `com.unity.ugui` (default presenter uses CanvasGroup/TMP)

## Releasing / updating

`main` is wired to CI that auto-tags releases: bump `"version"` in
`package.json`, push to `main`, and the `UPM release` workflow tags
`v<version>` automatically. Consumers pinned to a tag
(`...git#v0.1.0`) upgrade by changing the tag in `manifest.json`;
consumers on `...git` (HEAD) get the latest `main` on next resolve.
