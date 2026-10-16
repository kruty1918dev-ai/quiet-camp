using TMPro;
using UnityEngine;

namespace QuietCamp.Presentation.UI
{
    /// <summary>One font family for every player-facing surface, independent of language.</summary>
    public static class CampTypography
    {
        public const string ResourcePath = "Fonts/NotoSans SDF";
        public static TMP_FontAsset Font => Resources.Load<TMP_FontAsset>(ResourcePath);
    }
}
