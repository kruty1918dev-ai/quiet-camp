using System;
using System.Collections.Generic;

namespace QuietCamp.Domain
{
    public static class CampAccess
    {
        public static IEnumerable<Cell> Points(LevelData level)
        {
            yield return new Cell(level.entry[0], level.entry[1]);
            foreach (var point in level.accessPoints ?? Array.Empty<AccessPointData>())
                yield return new Cell(point.x, point.z);
        }
        public static bool IsReserved(LevelData level, Cell cell)
        {
            foreach (var point in Points(level)) if (point.Equals(cell)) return true;
            return false;
        }
        public static bool RoutesOpen(LevelData level, HashSet<Cell> occupied)
        {
            var origin = new Cell(level.entry[0], level.entry[1]);
            foreach (var point in level.accessPoints ?? Array.Empty<AccessPointData>())
                if (RuleEvaluator.Path(level, occupied, origin, new Cell(point.x, point.z)) == null) return false;
            return true;
        }
    }
}
