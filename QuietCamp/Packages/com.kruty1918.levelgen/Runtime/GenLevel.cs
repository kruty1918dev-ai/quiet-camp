using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Kruty1918.LevelGen
{
    /// <summary>
    /// The generated level: a rectangular grid plus named cell masks (layers),
    /// a flat entity list, and arbitrary top-level props. This is deliberately
    /// game-agnostic — a runner reads a "coins" layer, a puzzle reads "blocks",
    /// a campsite game reads "shade". Games map layers/entities to their own
    /// domain types in one small adapter.
    /// </summary>
    [Serializable]
    public class GenLevel
    {
        public int width, height;
        public int seed;
        public string recipe = "";
        public int attempts = 1;

        /// <summary>Named cell masks: layer name -> set of cells.</summary>
        public readonly Dictionary<string, HashSet<GenCell>> Layers =
            new Dictionary<string, HashSet<GenCell>>();

        public readonly List<GenEntity> Entities = new List<GenEntity>();

        /// <summary>Free-form level metadata (difficulty, lighting, speed...).</summary>
        public JObject Props = new JObject();

        public bool InBounds(GenCell c) => c.X >= 0 && c.Y >= 0 && c.X < width && c.Y < height;

        public HashSet<GenCell> Layer(string name, bool create = true)
        {
            if (!Layers.TryGetValue(name, out var set) && create)
                Layers[name] = set = new HashSet<GenCell>();
            return set;
        }

        public bool Has(string layer, GenCell c) =>
            Layers.TryGetValue(layer, out var s) && s.Contains(c);

        public bool AnyLayer(IEnumerable<string> layers, GenCell c)
        {
            if (layers == null) return false;
            foreach (var l in layers) if (Has(l, c)) return true;
            return false;
        }

        public IEnumerable<GenCell> AllCells()
        {
            for (var y = 0; y < height; y++)
                for (var x = 0; x < width; x++)
                    yield return new GenCell(x, y);
        }
    }
}
