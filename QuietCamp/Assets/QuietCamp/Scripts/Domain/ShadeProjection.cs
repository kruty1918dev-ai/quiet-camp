using System;
using System.Collections.Generic;

namespace QuietCamp.Domain
{
    public static class ShadeProjection
    {
        public static int[][] Cells(LevelData level)
        {
            var cells = new List<int[]>();
            for (var x = 0; x < level.width; x++) for (var z = 0; z < level.height; z++)
            {
                foreach (var canopy in level.canopies ?? Array.Empty<ShadeCanopyData>())
                {
                    var dx = (x + .5f - canopy.x) / canopy.radiusX;
                    var dz = (z + .5f - canopy.z) / canopy.radiusZ;
                    if (dx * dx + dz * dz <= 1f) { cells.Add(new[] { x, z }); break; }
                }
            }
            return cells.ToArray();
        }
    }
}
