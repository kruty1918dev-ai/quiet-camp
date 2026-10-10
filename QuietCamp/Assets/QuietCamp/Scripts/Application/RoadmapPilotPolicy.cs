using System;
using System.Collections.Generic;

namespace QuietCamp.Application
{
    /// <summary>Presentation slice; never trims the campaign or persisted progress.</summary>
    public static class RoadmapPilotPolicy
    {
        public const int Count = 5;
        public static readonly IReadOnlyList<string> LevelIds = Array.AsReadOnly(new[] { "QC001", "QC002", "QC003", "QC004", "QC005" });
        public static int Index(string id)
        { for (int i = 0; i < Count; i++) if (LevelIds[i] == id) return i; return -1; }
        public static bool Contains(string id) => Index(id) >= 0;
        public static int Frontier(Func<string, bool> completed)
        { for (int i = 0; i < Count; i++) if (!completed(LevelIds[i])) return i; return Count - 1; }
        public static bool Finished(Func<string, bool> completed)
        { foreach (var id in LevelIds) if (!completed(id)) return false; return true; }
        public static bool CanPlay(string id, Func<string, bool> completed, Func<string, bool> permitted)
            => Contains(id) && Index(id) <= Frontier(completed) && permitted(id);
    }
}
