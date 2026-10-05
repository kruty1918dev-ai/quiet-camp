using System;
using QuietCamp.Application;
using QuietCamp.Domain;

namespace QuietCamp.Infrastructure
{
    public enum CampAttemptResult { Completed, FailedAttempt, NoLives, SaveFailed }
    public sealed class CampAttemptService
    {
        readonly SaveAdapter _save;
        readonly CampEconomy _economy;
        readonly CampCompletionService _completion;
        public CampAttemptService(SaveAdapter save, CampEconomy economy, CampCompletionService completion)
        { _save = save; _economy = economy; _completion = completion; }
        public CampAttemptResult Check(CampSession session, string lighting)
        {
            if (session.IsCompleted) return CampAttemptResult.Completed;
            if (!_economy.CanCheck) return CampAttemptResult.NoLives;
            var solved = RuleEvaluator.Evaluate(session.Level, session.State.Placements, true).IsSolved;
            var previous = _save.Session;
            _save.Session = new SessionSaveData
            {
                levelId = session.Level.id, contentHash = session.Level.contentHash, ruleVersion = session.Level.ruleVersion,
                levelSnapshot = CampContent.Snapshot(session.Level), selectedGuestId = session.SelectedGuestId,
                placements = solved ? session.State.Snapshot() : Array.Empty<Placement>()
            };
            var outcome = _economy.RecordCheck(session.Level.id, solved);
            if (outcome == EconomyResult.Applied && _economy.IsPro && !_save.Save()) outcome = EconomyResult.SaveFailed;
            if (outcome != EconomyResult.Applied) { _save.Session = previous; return CampAttemptResult.SaveFailed; }
            if (!solved)
            {
                session.Restore(Array.Empty<Placement>(), session.SelectedGuestId);
                return CampAttemptResult.FailedAttempt;
            }
            session.Check(() => _completion.Complete(session, lighting));
            return session.IsCompleted ? CampAttemptResult.Completed : CampAttemptResult.SaveFailed;
        }
    }
}
