using System;

namespace Kruty1918.LevelGen
{
    /// <summary>Integer grid cell. Y is the second grid axis (z in 3D games).</summary>
    [Serializable]
    public readonly struct GenCell : IEquatable<GenCell>
    {
        public readonly int X, Y;
        public GenCell(int x, int y) { X = x; Y = y; }
        public bool Equals(GenCell b) => X == b.X && Y == b.Y;
        public override bool Equals(object o) => o is GenCell b && Equals(b);
        public override int GetHashCode() => unchecked(X * 397 ^ Y);
        public override string ToString() => $"({X},{Y})";
        public int ManhattanTo(GenCell b) => Math.Abs(X - b.X) + Math.Abs(Y - b.Y);
    }
}
