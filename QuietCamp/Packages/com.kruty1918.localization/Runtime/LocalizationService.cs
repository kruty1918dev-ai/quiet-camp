using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Kruty1918.Localization
{
    /// <summary>
    /// Canonical runtime localization service (plain C#, project-scope singleton).
    /// Loads JSON catalogs from <see cref="LocalizationOptions.CatalogResourceFolder"/>
    /// via <c>Resources.Load&lt;TextAsset&gt;</c> (Load -> Validate -> Freeze -> Consume),
    /// persists the selected language id to a small file,
    /// and raises <see cref="LanguageChanged"/> exactly once per real language switch.
    /// Catalog value shapes: <c>"key": "text"</c> or
    /// <c>"key": {"one":..,"few":..,"many":..,"other":..}</c> for plurals.
    /// </summary>
    public sealed class LocalizationService : ILocalizationService
    {
        private readonly LocalizationOptions _options;
        private readonly List<LocalizationLanguage> _languages = new List<LocalizationLanguage>();
        private readonly Dictionary<string, LocalizationCatalog> _catalogs =
            new Dictionary<string, LocalizationCatalog>(StringComparer.OrdinalIgnoreCase);
        private readonly string _persistPath;
        private int _activeIndex;

        public event Action LanguageChanged;

        /// <summary>Завантажує каталоги з Resources і persisted id з диска.</summary>
        public LocalizationService(LocalizationOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            if (_options.Languages == null || _options.Languages.Count == 0)
                throw new ArgumentException("LocalizationOptions.Languages must not be empty.");

            _persistPath = _options.PersistFilePath;
            for (int i = 0; i < _options.Languages.Count; i++)
            {
                LocalizationLanguage language = _options.Languages[i];
                _languages.Add(language);
                _catalogs[language.Id] = LoadCatalog(language.Id);
            }
            _activeIndex = ResolveInitialIndex(ReadPersistedId());
        }

        public string CurrentLanguageId => CurrentLanguage.Id;
        public LocalizationLanguage CurrentLanguage => _languages[_activeIndex];
        public IReadOnlyList<LocalizationLanguage> SupportedLanguages => _languages;
        public int CurrentLanguageIndex => _activeIndex;
        private LocalizationCatalog Active => _catalogs[CurrentLanguageId];

        public string T(string key)
        {
            if (string.IsNullOrEmpty(key)) return string.Empty;
            // 1: Шукаємо plural-aware запис за повним source text.
            if (Active.TryGet(key, out var forms))
            {
                string simple = forms.ForCategory(PluralCategory.Other);
                if (!string.IsNullOrEmpty(simple)) return simple;
            }
            // 2: Fallback — повертаємо source text як є.
            return key;
        }

        public string TF(string key, params object[] args)
        {
            string template = T(key);
            if (args == null || args.Length == 0) return template;
            try
            {
                return string.Format(CultureInfo.CurrentCulture, template, args);
            }
            catch (FormatException)
            {
                return template;
            }
        }

        public string TN(string oneKey, string pluralKey, int count)
        {
            // 1: Категорія множини активної мови (one/few/many/other).
            PluralCategory category = CategoryOf(CurrentLanguage.PluralLanguageCode, count);
            // 2: Каталог може містити повний plural-набір під singular key.
            if (Active.TryGet(oneKey, out var forms))
            {
                string localized = forms.ForCategory(category);
                if (!string.IsNullOrEmpty(localized)) return localized;
            }
            // 3: Source fallback: one -> singular source, решта -> plural source.
            return category == PluralCategory.One ? oneKey : pluralKey;
        }

        public bool TrySetLanguage(string languageId)
        {
            int index = IndexOf(languageId);
            if (index < 0) return false;
            if (index == _activeIndex) return true;
            _activeIndex = index;
            Persist(languageId);
            LanguageChanged?.Invoke();
            return true;
        }

        private int ResolveInitialIndex(string persistedId)
        {
            int index = IndexOf(persistedId);
            if (index >= 0) return index;
            index = IndexOf(MapSystemLanguage(Application.systemLanguage));
            return index >= 0 ? index : IndexOf(_options.DefaultLanguageId);
        }

        private string MapSystemLanguage(SystemLanguage language)
        {
            if (_options.SystemLanguageMap != null)
            {
                string mapped = _options.SystemLanguageMap(language);
                if (!string.IsNullOrWhiteSpace(mapped))
                    return mapped;
            }
            return _options.DefaultLanguageId;
        }

        private int IndexOf(string languageId)
        {
            if (string.IsNullOrWhiteSpace(languageId)) return -1;
            for (int i = 0; i < _languages.Count; i++)
                if (string.Equals(_languages[i].Id, languageId.Trim(), StringComparison.OrdinalIgnoreCase))
                    return i;
            return -1;
        }

        private LocalizationCatalog LoadCatalog(string languageId)
        {
            var language = new LocalizationLanguage(languageId, languageId);
            TextAsset asset = Resources.Load<TextAsset>(
                $"{_options.CatalogResourceFolder}/{languageId}");
            if (asset == null || string.IsNullOrWhiteSpace(asset.text))
                return new LocalizationCatalog(language, new Dictionary<string, LocalizationPluralForms>());

            var entries = new Dictionary<string, LocalizationPluralForms>(StringComparer.Ordinal);
            try
            {
                JObject root = JObject.Parse(asset.text);
                JToken entriesToken = root["entries"];
                if (entriesToken is JObject entriesObject)
                {
                    foreach (var property in entriesObject.Properties())
                        entries[property.Name] = LocalizationPluralForms.FromJson(property.Value);
                }
            }
            catch (Exception exception)
            {
                Debug.LogError($"[Localization] Failed to parse catalog '{languageId}': {exception.Message}");
            }
            return new LocalizationCatalog(language, entries);
        }

        private string ReadPersistedId()
        {
            try
            {
                if (!string.IsNullOrEmpty(_persistPath) && File.Exists(_persistPath))
                    return File.ReadAllText(_persistPath).Trim();
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[Localization] Could not read persisted language: {exception.Message}");
            }
            return null;
        }

        private void Persist(string languageId)
        {
            try
            {
                if (string.IsNullOrEmpty(_persistPath)) return;
                File.WriteAllText(_persistPath, languageId);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[Localization] Could not persist language '{languageId}': {exception.Message}");
            }
        }

        internal enum PluralCategory { One, Few, Many, Other }

        private static PluralCategory CategoryOf(string languageCode, int count)
        {
            int n = Math.Abs(count);
            switch (languageCode)
            {
                case "uk":
                case "ru":
                case "be":
                case "hr":
                case "sr":
                {
                    int mod10 = n % 10;
                    int mod100 = n % 100;
                    if (mod10 == 1 && mod100 != 11) return PluralCategory.One;
                    if (mod10 >= 2 && mod10 <= 4 && (mod100 < 12 || mod100 > 14)) return PluralCategory.Few;
                    return PluralCategory.Many;
                }
                default:
                    return n == 1 ? PluralCategory.One : PluralCategory.Other;
            }
        }
    }

    /// <summary>
    /// Plural-form set for one catalog key. Non-plural entries store the text in Other.
    /// </summary>
    public sealed class LocalizationPluralForms
    {
        private readonly string _one;
        private readonly string _few;
        private readonly string _many;
        private readonly string _other;

        private LocalizationPluralForms(string one, string few, string many, string other)
        {
            _one = one;
            _few = few;
            _many = many;
            _other = other;
        }

        public static LocalizationPluralForms FromJson(JToken token)
        {
            if (token == null) return new LocalizationPluralForms(null, null, null, null);
            if (token.Type == JTokenType.String)
                return new LocalizationPluralForms(null, null, null, token.Value<string>());
            if (token is JObject forms)
                return new LocalizationPluralForms(
                    forms.Value<string>("one"), forms.Value<string>("few"),
                    forms.Value<string>("many"), forms.Value<string>("other"));
            return new LocalizationPluralForms(null, null, null, token.ToString());
        }

        internal string ForCategory(LocalizationService.PluralCategory category)
        {
            switch (category)
            {
                case LocalizationService.PluralCategory.One:
                    return _one ?? _other;
                case LocalizationService.PluralCategory.Few:
                    return _few ?? _other;
                case LocalizationService.PluralCategory.Many:
                    return _many ?? _other;
                default:
                    return _other;
            }
        }
    }
}
