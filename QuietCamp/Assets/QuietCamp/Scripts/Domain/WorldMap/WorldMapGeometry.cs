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
    }
}
