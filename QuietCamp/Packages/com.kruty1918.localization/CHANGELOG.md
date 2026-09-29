# Changelog

## 0.1.0 — 2026-09-22

- Initial extraction from `Kruty1918.Moyva.Shared.Localization` (Moyva project).
- `LocalizationService` now takes `LocalizationOptions` (language table, catalog
  Resources folder, persistence path, system-language map) instead of hard-coded
  Moyva values; the `internal LocalizationService(string persistPath)` test seam is
  replaced by `LocalizationOptions.PersistFilePath`.
- `LocalizationFontService` now takes `LocalizationFontOptions` (font resource path,
  per-language warmup charset, fallback asset name).
