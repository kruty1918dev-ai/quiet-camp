using System;
using System.Collections.Generic;
using Kruty1918.Tutorials;
using QuietCamp.Domain;

namespace QuietCamp.Application
{
    [Serializable]
    public sealed class TutorialSaveData
    {
        public bool initialized, existingPlayer, controlsUnlocked, introductionSeen, menuIntroSeen;
        public TutorialProgress progress = new TutorialProgress();
        /// <summary>Wish signs the ongoing guide has already explained once.</summary>
        public string[] explainedSigns = Array.Empty<string>();
        /// <summary>World districts whose intro the guide has already shown once.</summary>
        public string[] seenDistricts = Array.Empty<string>();
    }

    /// <summary>Evaluated first five glades plus an ongoing sign guide.
    /// Skip opens ordinary UI and keeps the welcome pennant — the guide
    /// keeps explaining new signs whenever a glade first uses them.</summary>
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
        public bool NeedsIntroduction => !_save.introductionSeen && !_save.existingPlayer && !Skipped && !Finished
            && _save.progress.completedSteps.Length == 0;
        public bool BeginIntroduction()
        {
            if (!NeedsIntroduction) return true;
            _save.introductionSeen = true;
            if (_persist == null || _persist()) return true;
            _save.introductionSeen = false;
            return false;
        }
        public bool AllControls => _save.existingPlayer || _save.controlsUnlocked || Skipped || Finished;
        /// <summary>One-time menu coachmark for fresh players — shown after
        /// the guided camps end or are skipped, never for migrated saves.</summary>
        public bool NeedsMenuIntro => !_save.menuIntroSeen && !_save.existingPlayer
            && (Skipped || Finished || _save.controlsUnlocked);
        public void MarkMenuIntroSeen()
        {
            if (_save.menuIntroSeen) return;
            _save.menuIntroSeen = true;
            if (_persist == null || _persist()) { Changed?.Invoke(); return; }
            _save.menuIntroSeen = false;
        }
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
            { _save.initialized = true; _save.existingPlayer = hasExistingPlay; _save.controlsUnlocked = hasExistingPlay; _save.menuIntroSeen = hasExistingPlay; }
            // Migrated players already met every sign that exists today; a
            // sign introduced by a future update still gets explained to them.
            if (_save.explainedSigns == null) _save.explainedSigns = Array.Empty<string>();
            if (_save.existingPlayer && _save.explainedSigns.Length == 0)
                _save.explainedSigns = (string[])KnownSigns.Clone();
            if (_save.seenDistricts == null) _save.seenDistricts = Array.Empty<string>();
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
        /// <summary>First sign this glade introduces that the player was never
        /// taught — null during guided steps (those already teach their sign)
        /// and when nothing in the level is new. Works for skippers and for
        /// signs added by future updates.</summary>
        public string PendingSign(LevelData level)
        {
            if (level == null || Guiding(level.id)) return null;
            var explained = _save.explainedSigns ?? Array.Empty<string>();
            foreach (var sign in SignsIn(level))
                if (Array.IndexOf(explained, sign) < 0) return sign;
            return null;
        }
        /// <summary>Marks a sign as shown; persists with rollback.</summary>
        public void ExplainSign(string sign)
        {
            var old = _save.explainedSigns;
            if (sign == null || (old != null && Array.IndexOf(old, sign) >= 0)) return;
            _save.explainedSigns = new List<string>(old ?? Array.Empty<string>()) { sign }.ToArray();
            if (_persist != null && !_persist()) { _save.explainedSigns = old; return; }
            Changed?.Invoke();
        }
        /// <summary>Whether this district's intro was already narrated once.</summary>
        public bool DistrictSeen(string districtId)
        {
            var seen = _save.seenDistricts;
            return districtId != null && seen != null && Array.IndexOf(seen, districtId) >= 0;
        }
        /// <summary>Marks a district intro as shown; persists with rollback.</summary>
        public void MarkDistrictSeen(string districtId)
        {
            if (districtId == null || DistrictSeen(districtId)) return;
            var old = _save.seenDistricts;
            _save.seenDistricts = new List<string>(old ?? Array.Empty<string>()) { districtId }.ToArray();
            if (_persist != null && !_persist()) { _save.seenDistricts = old; return; }
            Changed?.Invoke();
        }
        /// <summary>Teachable wish signs, in the order the guided glades
        /// introduce them — a mechanic from a future update extends this list.</summary>
        static readonly string[] KnownSigns = { "shade", "quiet", "friends" };
        static List<string> SignsIn(LevelData level)
        {
            var signs = new List<string>();
            if (level?.guests != null)
                foreach (var g in level.guests)
                    if (g != null && g.shade) { signs.Add("shade"); break; }
            if (level?.guests != null)
                foreach (var g in level.guests)
                    if (g != null && g.quiet) { signs.Add("quiet"); break; }
            if (level?.friends != null && level.friends.Length > 0) signs.Add("friends");
            return signs;
        }
        void LearnLevelSigns(LevelData level)
        {
            var old = _save.explainedSigns;
            var list = new List<string>(old ?? Array.Empty<string>());
            foreach (var sign in SignsIn(level)) if (!list.Contains(sign)) list.Add(sign);
            if (list.Count == (old?.Length ?? 0)) return;
            _save.explainedSigns = list.ToArray();
            if (_persist != null && !_persist()) _save.explainedSigns = old;
        }
        public void Skip()
        {
            if (Skipped || Finished) return;
            _save.introductionSeen = true; _save.controlsUnlocked = true; _save.existingPlayer = false; _runner.Skip();
            // The pennant is a welcome gift — an ongoing guide can't be failed.
            GrantStarterReward();
        }
        void GrantStarterReward()
        {
            var claims = _save.progress.claimedRewards ?? Array.Empty<string>();
            if (Array.IndexOf(claims, RewardId) >= 0) return;
            var oldFlags = _progression.CosmeticFlags; var oldClaims = _save.progress.claimedRewards;
            _progression.CosmeticFlags |= PennantFlag;
            _save.progress.claimedRewards = new List<string>(claims) { RewardId }.ToArray();
            BindRunner();
            if (_persist != null && !_persist())
            { _progression.CosmeticFlags = oldFlags; _save.progress.claimedRewards = oldClaims; BindRunner(); return; }
            Changed?.Invoke();
        }
        public void LearnAgain()
        {
            _save.existingPlayer = false; _save.controlsUnlocked = true; _save.introductionSeen = true;
            if (Finished)
            {
                _save.progress = new TutorialProgress { flowId = _save.progress.flowId, claimedRewards = _save.progress.claimedRewards };
                BindRunner(); OnChanged();
            }
            else if (Skipped) _runner.Resume(); else OnChanged();
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
            var observed = _runner.Observe(new TutorialObservation(actionId, facts));
            // A taught sign stays taught — later glades don't re-explain it.
            if (observed && actionId == "complete") LearnLevelSigns(session.Level);
            return observed;
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
