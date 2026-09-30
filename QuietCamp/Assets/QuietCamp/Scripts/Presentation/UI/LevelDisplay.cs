using Kruty1918.Localization;
using QuietCamp.Infrastructure;
namespace QuietCamp.Presentation.UI
{
    /// <summary>
    /// Player-facing level names. Technical ids (QC001, gen:qc_camp:7,
    /// QC_TEST) stay in data, saves and routing — the UI always shows a
    /// localized "Glade NN" style title instead.
    /// </summary>
    public static class LevelDisplay
    {
        /// <summary>"Галявина 07" for path levels; a localized test/dev label
        /// for the QA level; a neutral fallback for unknown ids.</summary>
        public static string Title(string levelId, ILocalizationService loc)
        {
            if (levelId == null) return loc.T("level.unknown");
            if (IsTest(levelId)) return loc.T("level.test");
            var ids = LevelLoader.MvpLevelIds();
            for (var i = 0; i < ids.Count; i++)
            {
                if (ids[i] == levelId)
                    return loc.TF("level.meadow", i + 1);
            }
            return loc.T("level.unknown");
        }

        /// <summary>1-based position on the level path; -1 when absent.</summary>
        public static int Ordinal(string levelId)
        {
            var ids = LevelLoader.MvpLevelIds();
            for (var i = 0; i < ids.Count; i++)
                if (ids[i] == levelId) return i + 1;
            return -1;
        }

        /// <summary>The QA/development level — album entries keep their save
        /// but are labelled as a test camp, not as campaign content.</summary>
        public static bool IsTest(string levelId)
        {
            var testId = LevelLoader.TestLevelId();
            return levelId != null && (levelId == testId || levelId == "QC_TEST");
        }
    }
}
