using System;

namespace QuietCamp.Application
{
    /// <summary>Navigation while the roadmap is deferred. Uses existing campaign
    /// order and access decisions without altering progress or save data.</summary>
    public static class DirectCampNavigation
    {
        public static string ContinueTarget(JourneyCatalog catalog, ProgressionService progress,
            Func<string, bool> permitted, string unfinishedId)
        {
            if (!string.IsNullOrEmpty(unfinishedId) && !progress.IsCompleted(unfinishedId)
                && permitted(unfinishedId)) return unfinishedId;
            var main = catalog.Find("main");
            if (main == null || !main.published) return null;
            var target = progress.ContinueTarget(main.levelIds);
            return target != null && permitted(target) ? target : null;
        }

        public static string NextTarget(JourneyCatalog catalog, ProgressionService progress,
            Func<string, bool> permitted, string completedId)
        {
            if (!progress.IsCompleted(completedId)) return null;
            var journey = catalog.ForLevel(completedId);
            if (journey == null || !journey.published) return null;
            var target = progress.NextAfter(completedId, journey.levelIds);
            return target != null && permitted(target) ? target : null;
        }
    }
}
