using System.Collections.Generic;
using System.Linq;
namespace QuietCamp.Domain
{
    /// <summary>
    /// Structural validation for authored LevelData before it reaches gameplay.
    /// Returns every problem found; an empty list means the map is usable.
    /// </summary>
    public static class LevelContentValidator
    {
        public static List<string> Validate(LevelData l)
        {
            var errors = new List<string>();
            if (l == null) { errors.Add("level:null"); return errors; }
            if (l.schemaVersion != 1) errors.Add("schemaVersion:" + l.schemaVersion);
            if (l.ruleVersion != 1) errors.Add("ruleVersion:" + l.ruleVersion);
            if (string.IsNullOrWhiteSpace(l.id)) errors.Add("id:empty");
            if (l.width < 4 || l.width > 8 || l.height < 4 || l.height > 8)
                errors.Add($"size:{l.width}x{l.height}");
            if (l.entry == null || l.entry.Length != 2) errors.Add("entry:shape");
            else if (!RuleEvaluator.Inside(l, new Cell(l.entry[0], l.entry[1]))) errors.Add("entry:outside");

            ValidateMask(l, l.blocked, "blocked", errors);
            ValidateMask(l, l.shade, "shade", errors);
            ValidateMask(l, l.noise, "noise", errors);
            if (l.entry != null && l.blocked != null
                && l.blocked.Any(c => c[0] == l.entry[0] && c[1] == l.entry[1]))
                errors.Add("entry:blocked");

            var ids = new HashSet<string>();
            if (l.guests == null || l.guests.Length < 2 || l.guests.Length > 6)
                errors.Add("guests:count");
            else
                foreach (var g in l.guests)
                {
                    if (g == null || string.IsNullOrWhiteSpace(g.id)) { errors.Add("guest:id"); continue; }
                    if (!ids.Add(g.id)) errors.Add("guest:dup:" + g.id);
                    if (string.IsNullOrWhiteSpace(g.assetId)) errors.Add("guest:asset:" + g.id);
                    if (string.IsNullOrWhiteSpace(g.nameKey)) errors.Add("guest:nameKey:" + g.id);
                }

            if (l.friends != null)
                foreach (var pair in l.friends)
                {
                    if (pair == null || pair.Length != 2) { errors.Add("friends:shape"); continue; }
                    if (pair[0] == pair[1]) errors.Add("friends:self:" + pair[0]);
                    if (!ids.Contains(pair[0]) || !ids.Contains(pair[1]))
                        errors.Add($"friends:unknown:{pair[0]},{pair[1]}");
                }

            if (l.witness != null)
            {
                var seen = new HashSet<string>();
                foreach (var p in l.witness)
                {
                    if (p == null || !ids.Contains(p.guestId)) { errors.Add("witness:guest"); continue; }
                    if (!seen.Add(p.guestId)) errors.Add("witness:dup:" + p.guestId);
                    if (p.rotation < 0 || p.rotation > 3) errors.Add("witness:rotation:" + p.guestId);
                }
                if (errors.Count == 0 && !RuleEvaluator.Evaluate(l, l.witness).IsSolved)
                    errors.Add("witness:unsolved");
            }
            return errors;
        }

        static void ValidateMask(LevelData l, int[][] mask, string name, List<string> errors)
        {
            if (mask == null) { errors.Add(name + ":null"); return; }
            var seen = new HashSet<Cell>();
            foreach (var c in mask)
            {
                if (c == null || c.Length != 2) { errors.Add(name + ":shape"); continue; }
                var cell = new Cell(c[0], c[1]);
                if (!RuleEvaluator.Inside(l, cell)) errors.Add($"{name}:outside:{cell}");
                if (!seen.Add(cell)) errors.Add($"{name}:dup:{cell}");
            }
        }
    }
}
