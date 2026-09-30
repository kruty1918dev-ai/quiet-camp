using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
namespace QuietCamp.Domain
{
    /// <summary>
    /// Incremental MRV/backtracking solver over the same RuleEvaluator used by
    /// commit and completion. Initial placements stay pinned; the search assigns
    /// the remaining guests. Step() runs in work slices so the UI stays responsive;
    /// the overall budget produces Timeout — a distinct status, not "no solution".
    /// </summary>
    public sealed class CampSolver
    {
        public enum Status { Searching, Solved, Unsatisfiable, Timeout }

        sealed class Frame
        {
            public string GuestId;
            public List<Placement> Candidates;
            public int Next;
        }

        readonly LevelData _level;
        readonly Dictionary<string, GuestData> _guests;
        readonly HashSet<Cell> _shaded;
        readonly HashSet<Cell> _noise;
        readonly Cell _entry;
        readonly List<Placement> _assigned;
        readonly int _pinnedCount;
        readonly Stack<Frame> _frames = new Stack<Frame>();
        readonly double _budgetSeconds;
        readonly Stopwatch _watch = new Stopwatch();
        Status _status = Status.Searching;
        Placement[] _solution;

        public int Nodes { get; private set; }

        public CampSolver(LevelData level, IEnumerable<Placement> initial, double budgetSeconds = 0.25)
        {
            _level = level ?? throw new ArgumentNullException(nameof(level));
            _budgetSeconds = Math.Max(0.01, budgetSeconds);
            _shaded = Cells(level.shade);
            _noise = Cells(level.noise);
            _entry = new Cell(level.entry[0], level.entry[1]);
            _guests = level.guests.ToDictionary(g => g.id);
            _assigned = (initial ?? Enumerable.Empty<Placement>())
                .Where(p => p != null && _guests.ContainsKey(p.guestId))
                .Select(p => p.Copy()).ToList();
            _pinnedCount = _assigned.Count;
            if (_pinnedCount != (initial?.Count() ?? 0)
                || !RuleEvaluator.Evaluate(_level, _assigned, false).CanCommit)
                _status = Status.Unsatisfiable;
            _watch.Start();
        }

        public Status CurrentStatus => _status;

        /// <summary>Snapshot of the solved layout; valid only when Status==Solved.</summary>
        public Placement[] Solution
            => _status == Status.Solved ? _solution.Select(p => p.Copy()).ToArray() : null;

        /// <summary>Advance the search for at most sliceSeconds. Safe to call every frame.</summary>
        public Status Step(double sliceSeconds = 0.002)
        {
            if (_status != Status.Searching) return _status;
            double deadline = _watch.Elapsed.TotalSeconds + sliceSeconds;
            while (_status == Status.Searching)
            {
                if (Nodes % 32 == 0 && _watch.Elapsed.TotalSeconds > deadline) return _status;
                if (_watch.Elapsed.TotalSeconds > _budgetSeconds) { _status = Status.Timeout; return _status; }
                Iterate();
            }
            return _status;
        }

        void Iterate()
        {
            if (_assigned.Count == _guests.Count)
            {
                var report = RuleEvaluator.Evaluate(_level, _assigned);
                if (report.IsSolved)
                {
                    _solution = _assigned.Select(p => p.Copy()).ToArray();
                    _status = Status.Solved;
                }
                else Backtrack();
                return;
            }

            if (_frames.Count == _assigned.Count - _pinnedCount)
            {
                // Depth boundary: pick the unassigned guest with fewest legal poses.
                var chosen = PickMrv(out var candidates);
                if (chosen == null) { Backtrack(); return; }
                _frames.Push(new Frame { GuestId = chosen, Candidates = candidates, Next = 0 });
            }

            var frame = _frames.Peek();
            if (frame.Next >= frame.Candidates.Count) { _frames.Pop(); return; }
            var candidate = frame.Candidates[frame.Next++];
            _assigned.Add(candidate);
            Nodes++;
        }

        void Backtrack()
        {
            if (_frames.Count == 0) { _status = Status.Unsatisfiable; return; }
            _frames.Pop();
            _assigned.RemoveAt(_assigned.Count - 1);
        }

        string PickMrv(out List<Placement> best)
        {
            string chosen = null;
            best = null;
            foreach (var g in _level.guests)
            {
                if (IsAssigned(g.id)) continue;
                var list = ConsistentCandidates(g);
                if (best == null || list.Count < best.Count)
                {
                    best = list;
                    chosen = g.id;
                    if (list.Count == 0) break;
                }
            }
            return chosen;
        }

        bool IsAssigned(string guestId) => _assigned.Any(p => p.guestId == guestId);

        /// <summary>Statically-legal poses additionally consistent with current occupancy.</summary>
        List<Placement> ConsistentCandidates(GuestData g)
        {
            var result = new List<Placement>();
            for (var x = 0; x < _level.width; x++)
            for (var z = 0; z < _level.height; z++)
            for (var q = 0; q < 4; q++)
            {
                var p = new Placement { guestId = g.id, x = x, z = z, rotation = q };
                if (Consistent(g, p)) result.Add(p);
            }
            return result;
        }

        bool Consistent(GuestData g, Placement p)
        {
            var footprint = RuleEvaluator.Footprint(p);
            var occupied = Occupied();
            foreach (var c in footprint)
            {
                if (!RuleEvaluator.Inside(_level, c) || occupied.Contains(c)) return false;
            }
            // Static soft constraints prune immediately — they never improve by waiting.
            if (g.shade && footprint.Any(c => !_shaded.Contains(c))) return false;
            if (g.quiet && _noise.Any(n => footprint.Any(c => Math.Abs(c.X - n.X) + Math.Abs(c.Z - n.Z) <= 2)))
                return false;
            occupied.UnionWith(footprint);
            var door = RuleEvaluator.Door(p);
            if (!RuleEvaluator.Inside(_level, door) || occupied.Contains(door)) return false;
            if (RuleEvaluator.Path(_level, occupied, _entry, door) == null) return false;
            if (_level.friends != null)
                foreach (var pair in _level.friends)
                {
                    string partner = pair[0] == g.id ? pair[1] : pair[1] == g.id ? pair[0] : null;
                    if (partner == null) continue;
                    var partnerPlacement = _assigned.FirstOrDefault(a => a.guestId == partner);
                    if (partnerPlacement == null) continue;
                    var path = RuleEvaluator.Path(_level, occupied, door, RuleEvaluator.Door(partnerPlacement));
                    if (path == null || path.Count - 1 > 3) return false;
                }
            return true;
        }

        HashSet<Cell> Occupied()
        {
            var set = Cells(_level.blocked);
            set.Add(_entry);
            foreach (var p in _assigned)
                foreach (var c in RuleEvaluator.Footprint(p)) set.Add(c);
            return set;
        }

        static HashSet<Cell> Cells(int[][] a)
            => new HashSet<Cell>((a ?? Array.Empty<int[]>()).Select(c => new Cell(c[0], c[1])));

        /// <summary>
        /// One suggested move for a guest from the current partial state: the first
        /// consistent candidate of the MRV guest. Cheap — no full solution required.
        /// </summary>
        public Placement SuggestMove(out string guestId)
        {
            guestId = PickMrv(out var list);
            if (guestId == null || list.Count == 0) return null;
            return list[0].Copy();
        }
    }
}
