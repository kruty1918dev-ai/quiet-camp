using System;
using System.Collections.Generic;
using Kruty1918.Tutorials;
using QuietCamp.Domain;

namespace QuietCamp.Application
{
    [Serializable]
    public sealed class TutorialSaveData
    {
        public bool initialized, existingPlayer, controlsUnlocked;
        public TutorialProgress progress = new TutorialProgress();
    }

    /// <summary>Evaluated first five glades. Skip opens ordinary UI; it never awards mastery.</summary>
    public sealed class TutorialDirector
    {
        public const int PennantFlag = 8;
        public const string RewardId = "cosmetic.trail-pennant";
        readonly TutorialSaveData _save;
        TutorialRunner _runner;
        readonly ProgressionService _progression;
        readonly Func<bool> _persist;
        bool _saving;
        public event Action Changed;
        public TutorialSaveData Save => _save;
        public bool Skipped => _runner.Skipped;
        public bool Finished => _runner.Completed;
        public bool RewardOwned => (_progression.CosmeticFlags & PennantFlag) != 0;
        public bool AllControls => _save.existingPlayer || _save.controlsUnlocked || Skipped || Finished;
        public bool RoadmapUnlocked => AllControls || _progression.IsCompleted("QC001");
        public bool AlbumUnlocked => AllControls || _progression.IsCompleted("QC002");
        public bool HistoryUnlocked => AllControls || _progression.IsCompleted("QC001");
        public bool HintsUnlocked => AllControls || _progression.IsCompleted("QC002");
        public bool GuestListUnlocked => AllControls || _progression.IsCompleted("QC002");
        public string CurrentLevelId => _runner.Current == null ? null : _runner.Current.Id.Substring(0, 5);
        public string ActiveKey => _save.existingPlayer ? null : _runner.Current?.TextKey;
        public string Portrait => _runner.Current?.PortraitId ?? "welcome";
        public string Target => _runner.Current?.TargetId;

        public TutorialDirector(TutorialSaveData saved, bool hasExistingPlay, ProgressionService progression, Func<bool> persist)
        {
            _save = saved ?? new TutorialSaveData(); _progression = progression ?? throw new ArgumentNullException(nameof(progression));
            _persist = persist;
            if (!_save.initialized)
            { _save.initialized = true; _save.existingPlayer = hasExistingPlay; _save.controlsUnlocked = hasExistingPlay; }
            BindRunner();
        }
        void BindRunner()
        {
            if (_runner != null) _runner.Changed -= OnChanged;
            _runner = new TutorialRunner(Definition(), _save.progress ?? new TutorialProgress());
            _save.progress = _runner.Progress; _runner.Changed += OnChanged;
        }
        public bool Guiding(string levelId) => !_save.existingPlayer && !Skipped && !Finished && CurrentLevelId == levelId;
        public string Cue(string levelId) => Guiding(levelId) ? ActiveKey : null;
        public bool HasStep(string id) => _runner.IsComplete(id);
        public bool CanCompleteLevel(string levelId)
        {
            if (!Guiding(levelId)) return true;
            return levelId == "QC001" ? HasStep("QC001.rotate")
                : levelId != "QC002" || HasStep("QC002.undo");
        }
        public void Skip()
        {
            if (Skipped || Finished) return;
            _save.controlsUnlocked = true; _save.existingPlayer = false; _runner.Skip();
        }
        public void LearnAgain()
        {
            _save.existingPlayer = false; _save.controlsUnlocked = true;
            if (Skipped) _runner.Resume(); else OnChanged();
        }
        public void RetryReward() { if (_runner.RewardPending) OnChanged(); }
        public bool ReportAction(string actionId, CampSession session)
        {
            if (session == null || !Guiding(session.Level.id)) return false;
            if (actionId == "complete" && !session.IsCompleted) return false;
            var report = RuleEvaluator.Evaluate(session.Level, session.State.Placements, false);
            bool NoIssue(string code) => !report.Issues.Exists(i => i.Code == code);
            bool HasPlaced(Func<GuestData, bool> wish)
            {
                foreach (var guest in session.Level.guests)
                    if (wish(guest) && session.State.Contains(guest.id)) return true;
                return false;
            }
            bool friendsPlaced = session.Level.friends?.Length > 0;
            foreach (var pair in session.Level.friends ?? Array.Empty<string[]>())
                if (pair == null || pair.Length != 2 || !session.State.Contains(pair[0]) || !session.State.Contains(pair[1])) friendsPlaced = false;
            var facts = new Dictionary<string, bool>
            {
                ["level." + session.Level.id] = true,
                ["selected"] = session.SelectedGuestId != null,
                ["placed"] = session.State.Count > 0,
                ["route-valid"] = session.State.Count > 0 && report.CanCommit && NoIssue("path")
                    && report.Routes.Count > 0 && report.Routes.TrueForAll(r => r.Reachable),
                ["shade-valid"] = HasPlaced(g => g.shade) && NoIssue("shade"),
                ["quiet-valid"] = HasPlaced(g => g.quiet) && NoIssue("quiet"),
                ["friends-valid"] = friendsPlaced && NoIssue("friends"),
                ["solved"] = session.IsCompleted && RuleEvaluator.Evaluate(session.Level, session.State.Placements).IsSolved
            };
            return _runner.Observe(new TutorialObservation(actionId, facts));
        }
        void OnChanged()
        {
            if (_saving) return;
            _saving = true;
            try
            {
                var oldFlags = _progression.CosmeticFlags;
                var oldClaims = _save.progress.claimedRewards;
                if (_runner.RewardPending)
                {
                    _progression.CosmeticFlags |= PennantFlag;
                    _runner.ClaimReward(new Grant());
                }
                if (_persist != null && !_persist())
                {
                    _progression.CosmeticFlags = oldFlags;
                    _save.progress.claimedRewards = oldClaims; BindRunner();
                }
            }
            finally { _saving = false; }
            Changed?.Invoke();
        }
        sealed class Grant : ITutorialRewardSink { public bool TryGrant(string id) => id == RewardId; }
        static TutorialDefinition Definition()
        {
            var steps = new List<TutorialStep>();
            void Add(string level, string id, string action, string fact, string target, string portrait)
            {
                steps.Add(new TutorialStep(level + "." + id, "guide." + id, target,
                    new TutorialPredicate(o => o.ActionId == action && o.Has("level." + level) && (fact == null || o.Has(fact))), portrait));
            }
            Add("QC001", "select", "select", "selected", "guest-card", "welcome");
            Add("QC001", "place", "commit", "route-valid", "board", "point");
            Add("QC001", "rotate", "rotate", "route-valid", "rotate", "listen");
            Add("QC001", "first-check", "complete", "solved", "check", "encourage");
            Add("QC002", "move", "move", "route-valid", "board", "point");
            Add("QC002", "undo", "undo", null, "undo", "listen");
            Add("QC002", "shared-route", "complete", "solved", "check", "encourage");
            Add("QC003", "shade", "complete", "shade-valid", "board", "point");
            Add("QC004", "quiet", "complete", "quiet-valid", "board", "listen");
            Add("QC005", "friends", "complete", "friends-valid", "board", "celebrate");
            return new TutorialDefinition("quietcamp.first-camps", 1, steps, RewardId);
        }
    }
}
