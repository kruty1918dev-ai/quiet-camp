using System;
using System.Collections.Generic;
namespace QuietCamp.Application
{
    /// <summary>
    /// Sequential level unlock, first-completion stamps and cosmetic unlocks.
    /// Replaying a completed map never grants a second primary reward.
    /// </summary>
    public sealed class ProgressionService
    {
        public const int Cosmetic1Count = 6;
        public const int Cosmetic2Count = 12;

        readonly HashSet<string> _completed = new HashSet<string>(StringComparer.Ordinal);
        public string LastLevelId;
        public int CosmeticFlags;

        public IReadOnlyCollection<string> CompletedIds => _completed;
        public int CompletedCount => _completed.Count;

        public bool IsCompleted(string levelId) => levelId != null && _completed.Contains(levelId);

        /// <summary>Level is playable when it is first or its predecessor is completed.</summary>
        public bool IsUnlocked(string levelId, IReadOnlyList<string> orderedIds)
        {
            var index = IndexOf(orderedIds, levelId);
            if (index < 0) return false;
            if (index == 0) return true;
            return _completed.Contains(orderedIds[index - 1]);
        }

        /// <summary>Records completion; returns true only for the first completion.</summary>
        public bool MarkCompleted(string levelId)
        {
            LastLevelId = levelId;
            var first = _completed.Add(levelId);
            if (first)
            {
                if (_completed.Count >= Cosmetic1Count) CosmeticFlags |= 1;
                if (_completed.Count >= Cosmetic2Count) CosmeticFlags |= 2;
            }
            return first;
        }

        /// <summary>Next unfinished level in order, else the level after current.</summary>
        public string ContinueTarget(IReadOnlyList<string> orderedIds)
        {
            if (orderedIds == null || orderedIds.Count == 0) return null;
            foreach (var id in orderedIds)
                if (!_completed.Contains(id) && IsUnlocked(id, orderedIds)) return id;
            return orderedIds[orderedIds.Count - 1];
        }

        public string NextAfter(string levelId, IReadOnlyList<string> orderedIds)
        {
            var index = IndexOf(orderedIds, levelId);
            return index >= 0 && index + 1 < orderedIds.Count ? orderedIds[index + 1] : null;
        }

        public void Restore(IEnumerable<string> completedIds, string lastLevelId, int cosmeticFlags)
        {
            _completed.Clear();
            if (completedIds != null)
                foreach (var id in completedIds)
                    if (!string.IsNullOrWhiteSpace(id)) _completed.Add(id);
            LastLevelId = lastLevelId;
            CosmeticFlags = cosmeticFlags;
        }

        static int IndexOf(IReadOnlyList<string> ids, string levelId)
        {
            if (ids == null || levelId == null) return -1;
            for (var i = 0; i < ids.Count; i++)
                if (ids[i] == levelId) return i;
            return -1;
        }
    }
}
