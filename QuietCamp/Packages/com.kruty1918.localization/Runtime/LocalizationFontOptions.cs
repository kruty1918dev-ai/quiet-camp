using System;

namespace Kruty1918.Localization
{
    /// <summary>
    /// Host-supplied options for <see cref="LocalizationFontService"/>:
    /// which raw <c>Font</c> resource provides the dynamic fallback atlas and
    /// which extra characters each language needs beyond ASCII.
    /// </summary>
    public sealed class LocalizationFontOptions
    {
        /// <summary>Resources path of the source TTF/OTF font (e.g. "Fonts/LiberationSans").</summary>
        public string FontResourcePath = string.Empty;

        /// <summary>
        /// Optional per-language extra charset used to pre-warm the dynamic atlas.
        /// Argument is the language id; return the characters to add or null.
        /// </summary>
        public Func<string, string> CharsetForLanguage;

        /// <summary>Name given to the runtime-created fallback TMP font asset.</summary>
        public string FallbackAssetName = "LocalizationFallback (Dynamic)";
    }
}
