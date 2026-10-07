using System;
using System.Collections.Generic;

namespace QuietCamp.Domain
{
    /// <summary>Pure path math for the world road. Same constants the legacy
    /// RoadmapLayout used, expressed without UnityEngine so chunks, tests and
    /// authoring tools can share one layout authority.</summary>
    public static class WorldMapGeometry
    {
        public const int Step = 420, CentreY = 210, BonusStep = 500;

        public static float NodeX(int index) => .5f + (float)Math.Sin(index * .85f) * .21f;

        /// <param name="gaps">bonus gaps with afterLevel &lt;= index — callers pass
        /// a count from the bonus slot list, keeping this class data-pure.</param>
        public static float MainY(int index, int gaps) => index * Step + CentreY + gaps * BonusStep;

        public static float Height(int mainCount, int gaps) => mainCount * Step + 200 + gaps * BonusStep;

        /// <summary>Chunk bounds in map-local px for a [firstOrder,lastOrder] run.</summary>
        public static void ChunkBounds(WorldMapChunk chunk, int gapsBefore, out float yTop, out float yBottom)
        {
            yTop = MainY(chunk.FirstOrder - 1, gapsBefore) - CentreY;
            yBottom = MainY(chunk.LastOrder - 1, gapsBefore) + Step;
        }

        /// <summary>A branch physically leaves the road: it departs sideways
        /// from the anchor and keeps climbing gently. Side alternates so
        /// consecutive branches do not stack on the same shoulder.</summary>
        public static void BranchPoint(float anchorX, float anchorY, int index, float side, out float x, out float y)
        {
            var reach = .20f + index * .11f + (float)Math.Sin(index * 1.7f) * .04f;
            x = Math.Max(.08f, Math.Min(.92f, anchorX + side * reach));
            y = anchorY + Step * .5f * (index + 1);
        }

        /// <summary>Which shoulder a branch takes — deterministic by id so the
        /// same journey always grows the same direction.</summary>
        public static float BranchSide(string branchId, int attachOrder)
        {
            var h = branchId != null ? branchId.GetHashCode() : 0;
            return ((h ^ attachOrder) & 1) == 0 ? 1f : -1f;
        }
    }
}
