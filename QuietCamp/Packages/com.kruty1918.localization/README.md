# Kruty1918 Localization

![UPM package](https://img.shields.io/badge/UPM-package-blue)
![version](https://img.shields.io/github/v/tag/kruty1918dev-ai/com.kruty1918.localization?label=version&sort=semver)

JSON-catalog localization runtime for Unity.

## Install (Unity Package Manager)

Package Manager → **+** → **Add package from git URL**:

```
https://github.com/kruty1918dev-ai/com.kruty1918.localization.git
```

or in `Packages/manifest.json`:

```json
"com.kruty1918.localization": "https://github.com/kruty1918dev-ai/com.kruty1918.localization.git#v0.1.0"
```

## Features

- `ILocalizationService` / `LocalizationService` — language switching, `T`/`TF`/`TN`
  lookups with plural-form support (`one`/`few`/`many`/`other`), persisted selection.
- `LocalizationCatalog` / `LocalizationLanguage` / `LocalizationPluralForms` — resolved snapshots.
- `LocalizationFontService` — creates a dynamic-atlas TMP fallback font and wires it
  into `TMP_Settings.fallbackFontAssets` and registered primary fonts.

The host supplies everything game-specific via options objects:

```csharp
var service = new LocalizationService(new LocalizationOptions
{
    Languages = new[] { new LocalizationLanguage("en", "English"), ... },
    DefaultLanguageId = "en",
    CatalogResourceFolder = "MyLocales",           // Resources/MyLocales/{id}.json
    PersistFilePath = ".../language.txt",
    SystemLanguageMap = lang => "en",
});
var fonts = new LocalizationFontService(new LocalizationFontOptions
{
    FontResourcePath = "Fonts/MyFont",
    CharsetForLanguage = id => null,
});
```

Catalog JSON shape: `{ "entries": { "Key": "text" | {"one":..,"few":..,"many":..,"other":..} } }`.
Lookup keys are the source text as written in code; missing entries fall back to the key.

No dependency on any host-game sources, scenes or assets.

## Releasing / updating

`main` is wired to CI that auto-tags releases: bump `"version"` in
`package.json`, push to `main`, and the `UPM release` workflow tags
`v<version>` automatically. Consumers pinned to a tag
(`...git#v0.1.0`) upgrade by changing the tag in `manifest.json`;
consumers on `...git` (HEAD) get the latest `main` on next resolve.
