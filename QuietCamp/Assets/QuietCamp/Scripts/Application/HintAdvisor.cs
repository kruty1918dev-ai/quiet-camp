using System.Collections.Generic;
using System.Linq;
using QuietCamp.Domain;

namespace QuietCamp.Application
{
    /// <summary>
    /// Turns the rule report into one concrete, board-visible suggestion:
    /// which guest to act on, the exact pose to try and the cells to light up.
    /// Pure data in/out — depends only on LevelData/RuleEvaluator/CampSolver,
    /// so the same advisor can drive any HUD or a different game of this type.
    /// Kinds: Place (unplaced guest → best legal pose), Move (a guest breaking
    /// rules → a better pose, solver-backed), Done (nothing to suggest).
    /// </summary>
    public sealed class HintAdvisor
    {
        public enum Kind { Place, Move, Done }

        public sealed class Suggestion
        {
            public Kind Type;
            public string GuestId;
            public Placement Move;
            public Cell[] Cells;
            public string TextKey;
        }

        static readonly string[] IssueOrder = { "missing", "path", "shade", "quiet", "friends" };

        readonly LevelData _level;

        public HintAdvisor(LevelData level) => _level = level;

        /// <summary>First failing rule in canonical order — the "what's wrong"
        /// line for a check toast.</summary>
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

        /// <summary>One suggestion for the current layout. preferredGuestId
        /// (e.g. the selected guest) wins when it is the one needing action.</summary>
        public Suggestion Suggest(IReadOnlyList<Placement> placements, string preferredGuestId = null)
        {
            var report = RuleEvaluator.Evaluate(_level, placements, requireAll: true);
            if (report.IsSolved)
                return new Suggestion { Type = Kind.Done, TextKey = "rule.ok" };

            var placed = new HashSet<string>((placements ?? new List<Placement>())
                .Where(p => p != null).Select(p => p.guestId));
            var unplaced = _level.guests.Where(g => !placed.Contains(g.id)).ToList();

            if (unplaced.Count > 0)
            {
                var guest = unplaced.FirstOrDefault(g => g.id == preferredGuestId) ?? unplaced[0];
                var move = BestPose(placements, guest.id, except: null);
                return new Suggestion
                {
                    Type = Kind.Place,
                    GuestId = guest.id,
                    Move = move,
                    Cells = Highlight(move),
                    TextKey = "hint.place",
                };
            }

            // Everyone placed but rules broken: aim at the guest named by the
            // first soft issue, or the first guest when issues lack a guest id.
            var culprit = report.Issues.Where(i => !i.Hard && i.GuestId != null)
                .Select(i => i.GuestId).FirstOrDefault()
                ?? preferredGuestId ?? _level.guests[0].id;
            var current = placements.FirstOrDefault(p => p != null && p.guestId == culprit);
            var better = BestPose(placements, culprit, current);
            if (better != null)
                return new Suggestion
                {
                    Type = Kind.Move,
                    GuestId = culprit,
                    Move = better,
                    Cells = Highlight(better),
                    TextKey = "hint.move",
                };

            // Local search failed — ask the budgeted solver for any consistent step.
            var solver = new CampSolver(_level, placements, budgetSeconds: 0.1);
            var move2 = solver.SuggestMove(out var gid);
            if (move2 != null && gid != null)
                return new Suggestion
                {
                    Type = Kind.Move,
                    GuestId = gid,
                    Move = move2,
                    Cells = Highlight(move2),
                    TextKey = "hint.move",
                };
            return new Suggestion { Type = Kind.Done, TextKey = "hint.stuck" };
        }

        /// <summary>Pose with the fewest soft issues for guestId, or null when
        /// nothing commitable exists (or beats the pose in `except`).</summary>
        Placement BestPose(IReadOnlyList<Placement> placements, string guestId, Placement except)
        {
            Placement best = null;
            var bestIssues = except != null ? IssueCount(placements, guestId, except) : int.MaxValue;
            for (var x = 0; x < _level.width - 1; x++)
            for (var z = 0; z < _level.height - 1; z++)
            for (var q = 0; q < 4; q++)
            {
                var candidate = new Placement { guestId = guestId, x = x, z = z, rotation = q };
                if (Same(candidate, except)) continue;
                var issues = IssueCount(placements, guestId, candidate);
                if (issues < 0 || issues >= bestIssues) continue;
                bestIssues = issues;
                best = candidate;
            }
            return best;
        }

        /// <summary>Soft issue count for a hypothetical pose; -1 when hard-invalid.</summary>
        int IssueCount(IReadOnlyList<Placement> placements, string guestId, Placement pose)
        {
            var next = new PlacementCommand { GuestId = guestId, After = pose }.Apply(placements);
            var report = RuleEvaluator.Evaluate(_level, next, requireAll: false);
            return report.CanCommit ? report.Issues.Count : -1;
        }

        static bool Same(Placement a, Placement b)
            => a != null && b != null && a.x == b.x && a.z == b.z && a.rotation == b.rotation;

        static Cell[] Highlight(Placement move)
        {
            if (move == null) return null;
            var cells = RuleEvaluator.Footprint(move).ToList();
            cells.Add(RuleEvaluator.Door(move));
            return cells.ToArray();
        }
    }
}
