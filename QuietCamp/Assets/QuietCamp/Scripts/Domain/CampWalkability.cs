using System;
using System.Collections.Generic;
using System.Linq;

namespace QuietCamp.Domain
{
    public enum RouteFailureReason { None, OutsideTrail, Blocked, Disconnected }

    public sealed class RouteEvidence
    {
        public string GuestId, AccessPointId;
        public Cell Start, Goal;
        public RouteFailureReason Reason;
        public Cell[] Cells = Array.Empty<Cell>(), BlockingCells = Array.Empty<Cell>();
        public bool Reachable => Reason == RouteFailureReason.None;
    }

    /// <summary>One finite walking graph for evaluation, solver and overlays.
    /// Version 1 keeps its original grid; version 2 adds authored trail cells.
    /// A doorway is shared walking space, never an occupied tent cell.</summary>
    public static class CampWalkability
    {
        static readonly Cell[] Delta = { new Cell(0, 1), new Cell(1, 0), new Cell(0, -1), new Cell(-1, 0) };
        public static bool Contains(LevelData level, Cell cell)
        {
            if (RuleEvaluator.Inside(level, cell)) return true;
            if (level.ruleVersion != 2) return false;
            foreach(var p in level.exteriorWalkable ?? Array.Empty<int[]>())
                if(p != null && p.Length == 2 && p[0] == cell.X && p[1] == cell.Z) return true;
            return false;
        }
        public static bool IsWalkable(LevelData level, Cell cell)
        {
            if(!Contains(level,cell))return false;
            foreach(var p in level.blocked ?? Array.Empty<int[]>())
                if(p != null && p.Length == 2 && p[0] == cell.X && p[1] == cell.Z)return false;
            return true;
        }

        public static List<Cell> Path(LevelData level, HashSet<Cell> occupied, Cell start, Cell goal)
        {
            if (!Contains(level, start) || !Contains(level, goal) || occupied.Contains(start) || occupied.Contains(goal)) return null;
            var queue = new Queue<Cell>(); var previous = new Dictionary<Cell, Cell>();
            queue.Enqueue(start); previous[start] = start;
            while (queue.Count > 0)
            {
                var cell = queue.Dequeue();
                if (cell.Equals(goal))
                {
                    var route = new List<Cell> { cell };
                    while (!cell.Equals(start)) { cell = previous[cell]; route.Add(cell); }
                    route.Reverse(); return route;
                }
                foreach (var delta in Delta)
                {
                    var next = new Cell(cell.X + delta.X, cell.Z + delta.Z);
                    if (!Contains(level, next) || occupied.Contains(next) || previous.ContainsKey(next)) continue;
                    previous[next] = cell; queue.Enqueue(next);
                }
            }
            return null;
        }

        public static RouteEvidence ExplainRoute(LevelData level, HashSet<Cell> occupied, Cell start, Cell goal,
            string guestId = null, string accessPointId = null)
        {
            var evidence = new RouteEvidence { GuestId = guestId, AccessPointId = accessPointId, Start = start, Goal = goal };
            if (!Contains(level, start) || !Contains(level, goal))
            { evidence.Reason = RouteFailureReason.OutsideTrail; evidence.Cells = new[] { goal }; return evidence; }
            var path = Path(level, occupied, start, goal);
            if (path != null) { evidence.Cells = path.ToArray(); return evidence; }
            evidence.Reason = occupied.Contains(start) || occupied.Contains(goal)
                ? RouteFailureReason.Blocked : RouteFailureReason.Disconnected;
            // A failed route retains a concrete attempted lane and its blockers.
            // This runs once per changed placement, never per rendering frame.
            var attempted = Path(level, new HashSet<Cell>(), start, goal);
            evidence.Cells = attempted?.ToArray() ?? new[] { goal };
            evidence.BlockingCells = evidence.Cells.Where(occupied.Contains).ToArray();
            return evidence;
        }
    }
}
