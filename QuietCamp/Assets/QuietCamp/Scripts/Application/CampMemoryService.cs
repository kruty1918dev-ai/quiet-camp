using System;
using System.Linq;

namespace QuietCamp.Application
{
    [Serializable] public sealed class MemorySaveData
    {
        public int version = 1;
        public string[] rewardIds = Array.Empty<string>();
    }
    public sealed class CampMemoryService
    {
        readonly MemorySaveData _data;
        public MemorySaveData Data => _data;
        public CampMemoryService(MemorySaveData data)
        {
            _data = data ?? throw new ArgumentNullException(nameof(data));
            if (data.version != 1) throw new ArgumentException("Unsupported memory version");
            data.rewardIds ??= Array.Empty<string>();
        }
        public string RewardId(string journeyId, string levelId) => journeyId + ":" + levelId + ":care";
        public bool Earn(string journeyId, string levelId)
        {
            if (string.IsNullOrWhiteSpace(journeyId) || string.IsNullOrWhiteSpace(levelId)) return false;
            var id = RewardId(journeyId, levelId);
            if (Array.IndexOf(_data.rewardIds, id) >= 0) return false;
            _data.rewardIds = _data.rewardIds.Append(id).ToArray();
            return true;
        }
    }
}
