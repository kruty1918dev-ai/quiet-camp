using System;
using System.Linq;
using QuietCamp.Application;
using QuietCamp.Domain;

namespace QuietCamp.Infrastructure
{
    public sealed class CampCompletionService
    {
        readonly SaveAdapter _save;
        readonly ProgressionService _progress;
        readonly CampMemoryService _memories;
        readonly JourneyCatalog _catalog;
        public CampCompletionService(SaveAdapter save, ProgressionService progress, CampMemoryService memories, JourneyCatalog catalog)
        { _save = save; _progress = progress; _memories = memories; _catalog = catalog; }
        public bool Complete(CampSession session, string lighting)
        {
            if (session == null || !RuleEvaluator.Evaluate(session.Level, session.State.Placements).IsSolved) return false;
            var level = session.Level;
            var journey = _catalog.ForLevel(level.id);
            if (journey == null) return false;
            var ids = _progress.CompletedIds.ToArray(); var last = _progress.LastLevelId; var flags = _progress.CosmeticFlags;
            var sessionBefore = _save.Session; var albumBefore = _save.Album.entries; var progressBefore = _save.Progress;
            var rewardIds = _memories.Data.rewardIds;
            try
            {
                _progress.MarkCompleted(level.id);
                _memories.Earn(journey.id, level.id);
                var entries = (_save.Album.entries ?? Array.Empty<AlbumSaveData.Entry>()).Where(e => e != null && e.levelId != level.id).ToList();
                var previous = (_save.Album.entries ?? Array.Empty<AlbumSaveData.Entry>()).FirstOrDefault(e => e?.levelId == level.id);
                entries.Add(new AlbumSaveData.Entry
                {
                    levelId = level.id, journeyId = journey.id, placements = session.State.Snapshot(),
                    levelSnapshot = CampContent.Snapshot(level), lighting = lighting ?? level.lighting, contentHash = level.contentHash,
                    order = previous?.order ?? entries.Count, cosmeticId = (_progress.CosmeticFlags & 1) != 0 ? "fabric.b" : "fabric.a",
                    cared = true, storyRevision = 1, rewardIds = new[] { _memories.RewardId(journey.id, level.id) }
                });
                _save.Album.entries = entries.OrderBy(e => e.order).ToArray();
                _save.Session = new SessionSaveData();
                _save.Progress = new ProgressSaveData { completedIds = _progress.CompletedIds.ToArray(), lastLevelId = _progress.LastLevelId,
                    cosmeticFlags = _progress.CosmeticFlags, tutorial = progressBefore.tutorial };
                if (_save.Save()) return true;
            }
            catch { }
            _progress.Restore(ids, last, flags); _save.Progress = progressBefore;
            _save.Session = sessionBefore; _save.Album.entries = albumBefore; _memories.Data.rewardIds = rewardIds;
            return false;
        }
    }
}
