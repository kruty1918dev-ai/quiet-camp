using System;
using QuietCamp.Domain;
namespace QuietCamp.Application
{
    /// <summary>Session payload qc.session.v1: in-progress layout for one level.</summary>
    [Serializable]
    public sealed class SessionSaveData
    {
        public string levelId, contentHash, selectedGuestId;
        public int ruleVersion = 1;
        public Placement[] placements = new Placement[0];
    }

    /// <summary>Progress payload qc.progress.v1: completed ids, last level, cosmetics.</summary>
    [Serializable]
    public sealed class ProgressSaveData
    {
        public string[] completedIds = new string[0];
        public string lastLevelId;
        public int cosmeticFlags;
    }

    /// <summary>Settings payload qc.settings.v1.</summary>
    [Serializable]
    public sealed class SettingsSaveData
    {
        public string language = "uk";
        public float master = 1f, music = 1f, ambience = 1f, effects = 1f;
        public bool reducedMotion, haptics = true, highContrast;
        public float textScale = 1f, scrollSensitivity = 12f;
        public int quality;
    }

    /// <summary>Album payload qc.album.v1: completed camp dioramas, creation order.</summary>
    [Serializable]
    public sealed class AlbumSaveData
    {
        public Entry[] entries = new Entry[0];

        [Serializable]
        public sealed class Entry
        {
            public string levelId, cosmeticId;
            public int order;
            public Placement[] placements = new Placement[0];
        }
    }
}
