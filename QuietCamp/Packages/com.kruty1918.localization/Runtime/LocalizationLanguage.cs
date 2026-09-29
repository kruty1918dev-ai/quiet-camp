namespace Kruty1918.Localization
{
    /// <summary>
    /// Immutable description of a supported UI language.
    /// Залежності: <see cref="LocalizationService"/> і <see cref="LocalizationCatalog"/>.
    /// </summary>
    public sealed class LocalizationLanguage
    {
        /// <summary>Стабільний ідентифікатор мови (наприклад "en").</summary>
        public string Id { get; }

        /// <summary>Людинозрозуміла назва мови у самій мові (наприклад "English").</summary>
        public string DisplayName { get; }

        /// <summary>Код мови для plural rules / resource loading.</summary>
        public string PluralLanguageCode { get; }

        public LocalizationLanguage(string id, string displayName, string pluralLanguageCode = null)
        {
            Id = id ?? string.Empty;
            DisplayName = displayName ?? id ?? string.Empty;
            PluralLanguageCode = string.IsNullOrWhiteSpace(pluralLanguageCode) ? Id : pluralLanguageCode;
        }
    }
}
