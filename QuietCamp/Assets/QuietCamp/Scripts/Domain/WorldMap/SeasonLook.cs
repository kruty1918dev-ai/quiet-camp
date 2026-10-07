using System;
using System.Collections.Generic;

namespace QuietCamp.Domain
{
    /// <summary>Scalar description of a season's look — the renderer reads one
    /// of these per node/chunk and maps it onto palette, vegetation weights,
    /// particles and light. Blending is plain lerp: cheap and data-driven.</summary>
    public sealed class SeasonLook
    {
        public float Snow;          // 0..1 ground cover
        public float Leaves;        // 0..1 foliage density
        public float BareBranches;  // 0..1 leafless-tree weight
        public float Moisture;      // 0..1 wet ground / mud
        public float GroundWarmth;  // 0..1 warm↔cold ground tint
        public float Fog;           // 0..1 atmospheric haze density
        public float Particles;     // 0..1 pollen/leaves/snow in the air
        public float LightWarmth;   // 0..1 lighting colour temperature

        public static SeasonLook Blend(SeasonLook a, SeasonLook b, float t)
            => new SeasonLook
            {
                Snow = Lerp(a.Snow, b.Snow, t), Leaves = Lerp(a.Leaves, b.Leaves, t),
                BareBranches = Lerp(a.BareBranches, b.BareBranches, t),
                Moisture = Lerp(a.Moisture, b.Moisture, t),
                GroundWarmth = Lerp(a.GroundWarmth, b.GroundWarmth, t),
                Fog = Lerp(a.Fog, b.Fog, t), Particles = Lerp(a.Particles, b.Particles, t),
                LightWarmth = Lerp(a.LightWarmth, b.LightWarmth, t),
            };
        static float Lerp(float a, float b, float t) => a + (b - a) * t;

        /// <summary>Largest single-channel change — continuity checks use it to
        /// prove there is no hard theme switch between neighbours.</summary>
        public float MaxDelta(SeasonLook other)
        {
            var d = Math.Abs(Snow - other.Snow);
            d = Math.Max(d, Math.Abs(Leaves - other.Leaves));
            d = Math.Max(d, Math.Abs(BareBranches - other.BareBranches));
            d = Math.Max(d, Math.Abs(Moisture - other.Moisture));
            d = Math.Max(d, Math.Abs(GroundWarmth - other.GroundWarmth));
            d = Math.Max(d, Math.Abs(Fog - other.Fog));
            d = Math.Max(d, Math.Abs(Particles - other.Particles));
            return Math.Max(d, Math.Abs(LightWarmth - other.LightWarmth));
        }
    }

    /// <summary>Predefined segment profiles — one row per season the content
    /// authors. "thaw" is winter draining into spring: high moisture, retreating
    /// snow, still-bare trees.</summary>
    public static class SeasonLooks
    {
        static readonly Dictionary<string, SeasonLook> Table = new Dictionary<string, SeasonLook>(StringComparer.Ordinal)
        {
            ["spring"] = new SeasonLook { Snow = 0f, Leaves = .5f, BareBranches = .1f, Moisture = .7f, GroundWarmth = .5f, Fog = .25f, Particles = .3f, LightWarmth = .55f },
            ["summer"] = new SeasonLook { Snow = 0f, Leaves = 1f, BareBranches = 0f, Moisture = .3f, GroundWarmth = .9f, Fog = .1f, Particles = .2f, LightWarmth = .85f },
            ["autumn"] = new SeasonLook { Snow = 0f, Leaves = .7f, BareBranches = .2f, Moisture = .5f, GroundWarmth = .6f, Fog = .35f, Particles = .5f, LightWarmth = .65f },
            ["winter"] = new SeasonLook { Snow = 1f, Leaves = .05f, BareBranches = .9f, Moisture = .3f, GroundWarmth = .15f, Fog = .5f, Particles = .6f, LightWarmth = .3f },
            ["thaw"] = new SeasonLook { Snow = .35f, Leaves = .15f, BareBranches = .7f, Moisture = .9f, GroundWarmth = .3f, Fog = .45f, Particles = .15f, LightWarmth = .45f },
        };

        public static SeasonLook For(string seasonId)
            => seasonId != null && Table.TryGetValue(seasonId, out var p) ? p : Table["summer"];
        public static bool Has(string seasonId) => seasonId != null && Table.ContainsKey(seasonId);
    }
}
