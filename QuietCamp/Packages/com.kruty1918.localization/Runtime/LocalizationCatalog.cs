using System.Collections.Generic;

namespace Kruty1918.Localization
{
    /// <summary>
    /// Immutable resolved snapshot of one language's translation table.
    /// Залежності: створюється <see cref="LocalizationService"/> з JSON preset
    /// (the configured Resources folder) і consume-ується read-only.
    /// Key = source text exactly as written in code; Value = plural-aware forms
    /// (non-plural entries keep the translation in the "other" slot).
    /// </summary>
    public sealed class LocalizationCatalog
    {
        public static readonly LocalizationCatalog Empty = new LocalizationCatalog(
            new LocalizationLanguage("", ""), new Dictionary<string, LocalizationPluralForms>());

        public LocalizationLanguage Language { get; }

        public IReadOnlyDictionary<string, LocalizationPluralForms> Entries { get; }

        public LocalizationCatalog(LocalizationLanguage language,
            IReadOnlyDictionary<string, LocalizationPluralForms> entries)
        {
            Language = language;
            Entries = entries ?? new Dictionary<string, LocalizationPluralForms>();
        }

        public bool TryGet(string key, out LocalizationPluralForms forms)
        {
            forms = null;
            return !string.IsNullOrEmpty(key) && Entries.TryGetValue(key, out forms);
        }
    }
}
