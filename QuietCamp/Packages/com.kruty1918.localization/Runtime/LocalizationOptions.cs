using System;
using System.Collections.Generic;
using UnityEngine;

namespace Kruty1918.Localization
{
    /// <summary>
    /// Host-supplied options for <see cref="LocalizationService"/>:
    /// the language table, catalog Resources folder, persistence file and
    /// the SystemLanguage -> language-id mapping are owned by the game, not
    /// by this package.
    /// </summary>
    public sealed class LocalizationOptions
    {
        /// <summary>Supported languages in stable display order. Required, non-empty.</summary>
        public IReadOnlyList<LocalizationLanguage> Languages = Array.Empty<LocalizationLanguage>();

        /// <summary>Fallback language id used when nothing else resolves.</summary>
        public string DefaultLanguageId = string.Empty;

        /// <summary>
        /// Resources folder containing one <c>{id}.json</c> TextAsset per language.
        /// </summary>
        public string CatalogResourceFolder = string.Empty;

        /// <summary>
        /// Absolute file path where the selected language id is persisted.
        /// Null/empty disables persistence.
        /// </summary>
        public string PersistFilePath = string.Empty;

        /// <summary>
        /// Optional mapping from <see cref="Application.systemLanguage"/> to a language id.
        /// Return null to fall back to <see cref="DefaultLanguageId"/>.
        /// </summary>
        public Func<SystemLanguage, string> SystemLanguageMap;
    }
}
