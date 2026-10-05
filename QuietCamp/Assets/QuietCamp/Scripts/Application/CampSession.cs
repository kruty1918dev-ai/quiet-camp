using System;
using System.Collections.Generic;
using QuietCamp.Domain;
namespace QuietCamp.Application
{
    /// <summary>
    /// Single mutation authority for one level attempt: selection, place/move/
    /// rotate/remove, undo/redo, validation and completion. Hard-invalid commits
    /// are rejected before history is touched; soft issues commit and surface
    /// through the report. One ordered event stream feeds HUD, audio and saves.
    /// </summary>
    public sealed class CampSession : IDisposable
    {
        readonly CommandHistory _history = new CommandHistory();
        int _revision;
        bool _completed;

        public LevelData Level { get; }
        public BoardState State { get; private set; } = BoardState.Empty;
        public string SelectedGuestId { get; private set; }
        public bool IsCompleted => _completed;
        public Func<bool> CompletionPersistence { get; set; }
        public bool CanUndo { get; private set; }
        public bool CanRedo { get; private set; }

        public event Action<CampEvent> Evented;
        public event Action<ComfortAction> ActionRecorded;

        public CampSession(LevelData level)
        {
            Level = level ?? throw new ArgumentNullException(nameof(level));
        }

        void Emit(CampEventKind kind, string guestId = null, RuleReport report = null)
            => Evented?.Invoke(new CampEvent(kind, guestId, report, _revision));

        public void Select(string guestId)
        {
            if (SelectedGuestId == guestId) return;
            SelectedGuestId = guestId;
            Emit(CampEventKind.SelectionChanged, guestId);
        }

        /// <summary>Hypothetical evaluation of a candidate pose, old footprint excluded.</summary>
        public RuleReport Preview(PlacementCommand command)
        {
            var next = command.Apply(State.Placements);
            return RuleEvaluator.Evaluate(Level, next, requireAll: false);
        }

        /// <summary>Applies a placement command. Rejects hard-invalid, unchanged and completed layouts.</summary>
        public bool TryCommit(PlacementCommand command, out RuleReport report)
        {
            if (_completed) { report = LastReport(); return false; }
            var previous = State.Find(command.GuestId);
            var next = command.Apply(State.Placements);
            report = RuleEvaluator.Evaluate(Level, next, requireAll: false);
            if (!report.CanCommit) return false;
            if (!_history.Commit(Level, next)) return false;
            ApplyCurrent(command.GuestId);
            ActionRecorded?.Invoke(command.After == null ? ComfortAction.Remove : previous == null ? ComfortAction.Place
                : previous.x != command.After.x || previous.z != command.After.z ? ComfortAction.Move : ComfortAction.Rotate);
            Emit(CampEventKind.BoardCommitted, command.GuestId);
            Emit(CampEventKind.RulesChanged, report: report);
            return true;
        }

        public bool Undo()
        {
            if (_completed || !_history.Undo()) return false;
            ApplyCurrent(SelectedGuestId);
            ActionRecorded?.Invoke(ComfortAction.Undo);
            Emit(CampEventKind.BoardCommitted);
            Emit(CampEventKind.RulesChanged, report: LastReport());
            return true;
        }

        public bool Redo()
        {
            if (_completed || !_history.Redo()) return false;
            ApplyCurrent(SelectedGuestId);
            ActionRecorded?.Invoke(ComfortAction.Redo);
            Emit(CampEventKind.BoardCommitted);
            Emit(CampEventKind.RulesChanged, report: LastReport());
            return true;
        }

        /// <summary>Full validation against the current layout; completion only via this.</summary>
        public RuleReport Check(Func<bool> persistCompletion = null)
        {
            ActionRecorded?.Invoke(ComfortAction.Check);
            var report = RuleEvaluator.Evaluate(Level, State.Placements, requireAll: true);
            if (report.IsSolved && !_completed)
            {
                var persist = persistCompletion ?? CompletionPersistence;
                if (persist != null && !persist()) return report;
                _completed = true;
                Emit(CampEventKind.LevelCompleted, report: report);
            }
            Emit(CampEventKind.RulesChanged, report: report);
            return report;
        }

        /// <summary>Loads a saved layout; undo history is intentionally cleared.</summary>
        public void Restore(IEnumerable<Placement> saved, string selectedGuestId)
        {
            _completed = false;
            _history.Restore(saved ?? Array.Empty<Placement>());
            SelectedGuestId = selectedGuestId;
            ApplyCurrent(SelectedGuestId);
            Emit(CampEventKind.BoardCommitted);
            Emit(CampEventKind.RulesChanged, report: LastReport());
        }

        /// <summary>Debug-only shortcut used by QC_TEST tooling, never by gameplay.</summary>
        public void DebugApplyWitness()
        {
            if (Level.witness == null) return;
            _history.Restore(Level.witness);
            ApplyCurrent(SelectedGuestId);
            Emit(CampEventKind.BoardCommitted);
            Emit(CampEventKind.RulesChanged, report: LastReport());
        }

        RuleReport LastReport() => RuleEvaluator.Evaluate(Level, State.Placements, requireAll: true);

        void ApplyCurrent(string keepSelection)
        {
            _revision++;
            State = new BoardState(_history.Current, _revision);
            CanUndo = _history.CanUndo;
            CanRedo = _history.CanRedo;
            if (keepSelection != null && !LevelHasGuest(keepSelection))
                SelectedGuestId = null;
        }

        bool LevelHasGuest(string id)
        {
            foreach (var g in Level.guests) if (g.id == id) return true;
            return false;
        }

        public void Dispose() { Evented = null; ActionRecorded = null; CompletionPersistence = null; }
    }
}
