using System;
using System.Collections.Generic;

namespace Kruty1918.Localization
{
    /// <summary>
    /// Canonical runtime localization boundary for all visible UI text.
    /// Key convention: the lookup key is the source text exactly as written in code
    /// (English or Ukrainian). T() returns the source text unchanged when the active
    /// language has no entry, so untranslated strings degrade gracefully.
    /// Залежності: implement-иться <see cref="LocalizationService"/>, інжектується у
    /// view controllers/state/bridge/read models; єдиний mutation path для мови.
    /// </summary>
    public interface ILocalizationService
    {
        /// <summary>Id активної мови (наприклад "en", "uk").</summary>
        string CurrentLanguageId { get; }

        /// <summary>Активна мова.</summary>
        LocalizationLanguage CurrentLanguage { get; }

        /// <summary>Усі підтримувані мови у стабільному порядку.</summary>
        IReadOnlyList<LocalizationLanguage> SupportedLanguages { get; }

        /// <summary>Індекс активної мови у <see cref="SupportedLanguages"/> або -1.</summary>
        int CurrentLanguageIndex { get; }

        /// <summary>Подія після фактичної зміни мови; UI має перерендеритись.</summary>
        event Action LanguageChanged;

        /// <summary>Локалізує source text; повертає source якщо перекладу немає.</summary>
        string T(string key);

        /// <summary>Локалізує й форматує: string.Format-плейсхолдери {0},{1}… у перекладі.</summary>
        string TF(string key, params object[] args);

        /// <summary>Локалізує plural-форму за кількістю (one/few/many/other).</summary>
        string TN(string oneKey, string pluralKey, int count);

        /// <summary>
        /// Застосовує мову за id. Повертає false для невідомого id.
        /// Persistence виконується всередині; LanguageChanged спрацьовує лише при реальній зміні.
        /// </summary>
        bool TrySetLanguage(string languageId);
    }
}
