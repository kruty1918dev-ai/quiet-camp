using System;
using Newtonsoft.Json.Linq;

namespace Kruty1918.LevelKit
{
    /// <summary>
    /// Integer cell coordinate inside a level grid. XZ convention: X grows
    /// right, Z grows up — matching Unity's XZ-plane board layout.
    /// </summary>
    public readonly struct CellPos : IEquatable<CellPos>
    {
        public readonly int X;
        public readonly int Z;

        public CellPos(int x, int z) { X = x; Z = z; }

        public bool Equals(CellPos other) => X == other.X && Z == other.Z;
        public override bool Equals(object obj) => obj is CellPos other && Equals(other);
        public override int GetHashCode() => unchecked(X * 397 ^ Z);
        public override string ToString() => $"({X},{Z})";

        /// <summary>Parses a JSON pair: [x, z]. Returns false for anything else.</summary>
        public static bool TryParse(JToken token, out CellPos pos)
        {
            pos = default;
            if (token is JArray arr && arr.Count >= 2
                && arr[0].Type == JTokenType.Integer && arr[1].Type == JTokenType.Integer)
            {
                pos = new CellPos((int)arr[0], (int)arr[1]);
                return true;
            }
            return false;
        }

        public JArray ToJson() => new JArray(X, Z);
    }
}
