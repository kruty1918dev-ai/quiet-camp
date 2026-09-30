using System;
using System.Collections.Generic;
using System.Linq;
using QuietCamp.Domain;
namespace QuietCamp.Application
{
    /// <summary>
    /// Three-level hint flow: explain the first broken rule, highlight a legal
    /// area for one guest, then reveal one solver-confirmed move. The solver runs
    /// in budgeted slices; Timeout is a distinct status, never "no solution".
    /// A move hint is valid only for the revision it was computed on.
    /// </summary>
    public sealed class HintService
    {
        public enum Stage { Explain, Area, Move }
        public enum SearchStatus { Idle, Searching, Ready, Unsatisfiable, Timeout }

        static readonly string[] IssueOrder = { "missing", "path", "shade", "quiet", "friends" };

        readonly LevelData _level;
        CampSolver _solver;
        Placement _move;
        int _revision;
        Placement[] _origin = new Placement[0];

        public Stage NextStage { get; private set; } = Stage.Explain;
        public SearchStatus Status { get; private set; } = SearchStatus.Idle;
        public Placement Move => Status == SearchStatus.Ready ? _move?.Copy() : null;
        public int MoveRevision => _revision;

        public HintService(LevelData level) => _level = level;

        /// <summary>First failing rule in canonical order for the current report.</summary>
        public string Explain(RuleReport report)
        {
            if (report == null || report.Issues.Count == 0) return "rule.ok";
            foreach (var code in IssueOrder)
            {
                var issue = report.Issues.FirstOrDefault(i => i.Code == code);
                if (issue != null) return "rule." + issue.Code;
            }
            return "rule." + report.Issues[0].Code;
        }

        /// <summary>Anchor cells where the guest can legally sit (static rules + occupancy).</summary>
        public Cell[] AreaFor(CampSession session, string guestId)
        {
            var result = new List<Cell>();
            if (session == null || string.IsNullOrEmpty(guestId)) return result.ToArray();
            for (var x = 0; x < _level.width; x++)
            for (var z = 0; z < _level.height; z++)
            for (var q = 0; q < 1; q++)
            {
                var candidate = new Placement { guestId = guestId, x = x, z = z, rotation = q };
                var command = new PlacementCommand { GuestId = guestId, After = candidate };
                var report = session.Preview(command);
                if (report.CanCommit) result.Add(new Cell(x, z));
            }
            return result.ToArray();
        }

        /// <summary>Starts (or restarts) the background solve from the current state.</summary>
        public void BeginMoveSearch(CampSession session)
        {
            if (session == null) return;
            _solver = new CampSolver(_level, session.State.Placements, budgetSeconds: 0.25);
            _origin = session.State.Snapshot();
            _revision = session.State.Revision;
            _move = null;
            Status = SearchStatus.Searching;
        }

        /// <summary>Advances the solver by one work slice; call every frame while Searching.</summary>
        public void Pump(double sliceSeconds = 0.002)
        {
            if (Status != SearchStatus.Searching || _solver == null) return;
            switch (_solver.Step(sliceSeconds))
            {
                case CampSolver.Status.Solved:
                    _move = PickMove(_solver.Solution);
                    Status = SearchStatus.Ready;
                    break;
                case CampSolver.Status.Unsatisfiable:
                    Status = SearchStatus.Unsatisfiable;
                    break;
                case CampSolver.Status.Timeout:
                    Status = SearchStatus.Timeout;
                    break;
            }
        }

        public void AdvanceStage()
        {
            if (NextStage == Stage.Explain) NextStage = Stage.Area;
            else if (NextStage == Stage.Area) NextStage = Stage.Move;
        }

        public void Reset()
        {
            NextStage = Stage.Explain;
            Status = SearchStatus.Idle;
            _solver = null;
            _move = null;
        }

        Placement PickMove(Placement[] solution)
        {
            if (solution == null) return null;
            // Prefer a guest that is not yet placed; otherwise first differing pose.
            var fresh = solution.FirstOrDefault(p => p != null && _origin.All(o => o.guestId != p.guestId));
            if (fresh != null) return fresh;
            return solution.FirstOrDefault(p => p != null
                && _origin.All(o => o.guestId != p.guestId
                    || o.x != p.x || o.z != p.z || o.rotation != p.rotation));
        }
    }
}
