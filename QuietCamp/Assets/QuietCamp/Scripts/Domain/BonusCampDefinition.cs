using System;

namespace QuietCamp.Domain
{
    /// <summary>A side route, separate from the ordered campaign. Empty levelId reserves a preview only.</summary>
    [Serializable] public sealed class BonusCampDefinition
    {
        public string id, titleKey, descriptionKey, theme, levelId;
        public int afterLevel, requiredCompletions = 10;
        public bool requiresPremium;
    }
}
