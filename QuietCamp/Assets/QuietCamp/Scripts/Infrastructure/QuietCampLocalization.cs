using System.Collections.Generic;
using System.IO;
using Kruty1918.Localization;
using UnityEngine;
namespace QuietCamp.Infrastructure
{
    /// <summary>
    /// Builds the project localization service: uk default, en/de supported,
    /// system-language mapping and persisted selection under persistentDataPath.
    /// </summary>
    public static class QuietCampLocalization
    {
        public static ILocalizationService Create()
            => new LocalizationService(new LocalizationOptions
            {
                Languages = new List<LocalizationLanguage>
                {
                    new LocalizationLanguage("uk", "Українська"),
                    new LocalizationLanguage("en", "English"),
                    new LocalizationLanguage("de", "Deutsch"),
                },
                DefaultLanguageId = "uk",
                CatalogResourceFolder = "QuietCampLocales",
                PersistFilePath = Path.Combine(UnityEngine.Application.persistentDataPath, "qc_language.txt"),
                SystemLanguageMap = language =>
                    language == SystemLanguage.Ukrainian ? "uk"
                    : language == SystemLanguage.German ? "de"
                    : "en",
            });
    }
}
