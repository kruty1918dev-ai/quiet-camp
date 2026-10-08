using System;
using System.Collections.Generic;

namespace Kruty1918.LevelGen
{
    /// <summary>
    /// Deterministic RNG wrapper — same seed, same level, every time.
    /// Generation never touches UnityEngine.Random so results are
    /// reproducible across sessions, devices and test runs.
    /// </summary>
    public sealed class GenRng
    {
        readonly Random _r;
        public readonly int Seed;
        public GenRng(int seed) { Seed = seed; _r = new Random(seed); }

        public int Int(int maxExclusive) => _r.Next(maxExclusive);
        public int Int(int minInclusive, int maxExclusive) => _r.Next(minInclusive, maxExclusive);
        public float Float() => (float)_r.NextDouble();
        public bool Chance(float p) => _r.NextDouble() < p;
        public T Pick<T>(IReadOnlyList<T> items) => items[Int(items.Count)];

        /// <summary>Weighted pick: weights aligned with items; returns chosen index.</summary>
        public int WeightedIndex(IReadOnlyList<float> weights)
        {
            float total = 0;
            foreach (var w in weights) total += Math.Max(0, w);
            var roll = Float() * total;
            for (var i = 0; i < weights.Count; i++)
            {
                roll -= Math.Max(0, weights[i]);
                if (roll <= 0) return i;
            }
            return weights.Count - 1;
        }

        public void Shuffle<T>(IList<T> list)
        {
            for (var i = list.Count - 1; i > 0; i--)
            {
                var j = Int(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        /// <summary>Deterministic seed derivation for sub-generators.</summary>
        public int Derive(string tag) => unchecked(Seed * 31 + tag.GetHashCode());
    }
}
