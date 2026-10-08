using System.Collections.Generic;

namespace Kruty1918.LevelKit
{
    /// <summary>
    /// A named set of cells on the level grid: "blocked", "shade", "noise",
    /// "spawn", "entry" — the semantics are defined by the consuming game,
    /// the kit only stores and serializes positions.
    /// </summary>
    public sealed class LevelLayer
    {
        public string Name;
        public readonly List<CellPos> Cells = new List<CellPos>();

        public LevelLayer(string name) { Name = name; }

        public bool Contains(CellPos pos) => Cells.Contains(pos);

        /// <summary>Adds the cell unless already present. Returns true if added.</summary>
        public bool Add(CellPos pos)
        {
            if (Cells.Contains(pos)) return false;
            Cells.Add(pos);
            return true;
        }

        public bool Remove(CellPos pos) => Cells.Remove(pos);

        /// <summary>Replaces content with exactly this cell (single-cell layers).</summary>
        public void SetSingle(CellPos pos)
        {
            Cells.Clear();
            Cells.Add(pos);
        }
    }
}
