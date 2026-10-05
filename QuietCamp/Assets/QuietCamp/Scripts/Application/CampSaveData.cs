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
        // A content update cannot reinterpret an unfinished arrangement.
        public LevelData levelSnapshot;
    }

    /// <summary>Progress payload qc.progress.v1: completed ids, last level, cosmetics.</summary>
    [Serializable]
    public sealed class ProgressSaveData
    {
        public string[] completedIds = new string[0];
        public string lastLevelId;
        public int cosmeticFlags;
        public TutorialSaveData tutorial;
    }

    /// <summary>Settings payload qc.settings.v1.</summary>
    [Serializable]
    public sealed class SettingsSaveData
    {
        public string language = "uk";
        public float master = 1f, music = 1f, ambience = 1f, effects = 1f;
        public bool reducedMotion, haptics = true, highContrast;
        /// <summary>Relaxed pace: tweens run ~1.6× slower, gentle feel.</summary>
        public bool calmMode;
        public float textScale = 1f, scrollSensitivity = 12f;
        public int quality;
        /// <summary>Fixed display pose: portrait, landscape left/right, inverted portrait. Old saves default to portrait.</summary>
        public int orientation;
        /// <summary>Exclude the previous launch's menu glade when choosing the next backdrop.</summary>
        public string lastMenuBackdropId;
        // Old saves deserialize with collection off. A policy version change
        // requires a fresh choice; consent is independent of advertising.
        public bool analyticsConsent;
        public int analyticsPolicyVersion;
        public string analyticsPublicationRevision;
        // Local acknowledgement receipt, never consent to optional processing.
        public string privacyAcknowledgementRevision, privacyAcknowledgementHash;
        public string privacyAcknowledgementLanguage, privacyAcknowledgedUtc;
        public bool privacyAcknowledgementDraft;
        public bool optionalVideoBonuses = true;
        public bool fireflyLantern;
        public bool trailPennant = true;
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
            public string contentHash, lighting;
            public LevelData levelSnapshot;
            public string journeyId;
            public int storyRevision;
            public bool cared;
            public string[] rewardIds = new string[0];
        }
    }
}
