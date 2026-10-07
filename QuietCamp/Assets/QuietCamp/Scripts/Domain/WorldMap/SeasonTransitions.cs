using System;

namespace QuietCamp.Domain
{
    /// <summary>What a single main-path order looks like seasonally:
    /// the two seasons being blended and the position inside the blend.</summary>
    public readonly struct SeasonBlend
    {
        public readonly string From;   // season of the region being left (== To when settled)
        public readonly string To;     // season of the region being entered
        public readonly float T;       // 0 = pure From, 1 = pure To
        public SeasonBlend(string from, string to, float t) { From = from; To = to; T = t; }
        public SeasonLook Palette => SeasonLook.Blend(SeasonLooks.For(From), SeasonLooks.For(To), T);
        public string Dominant => T < .5f ? From : To;
    }

    /// <summary>Turns the region-season table into a continuous seasonal
    /// journey. A transition zone straddles every region boundary: the last
    /// <c>TransitionSpan</c> main orders of the outgoing region plus the first
    /// <c>TransitionSpan</c> of the incoming one blend between the two season
    /// palettes, so no node is ever a hard theme switch.</summary>
    public static class SeasonTransitions
    {
        public const int DefaultSpan = 2;

        public static SeasonBlend Sample(WorldMapData map, int order)
        {
            var regions = map?.Regions;
            if (regions == null || regions.Length == 0 || order < 1) return new SeasonBlend("summer", "summer", 0f);
            var ri = RegionIndexFor(map, order);
            var region = regions[ri];
            // Head of this region may still be blending in from the previous one.
            if (ri > 0)
            {
                var prev = regions[ri - 1];
                var spanIn = Span(region, order);
                var spanOut = Span(prev, order);
                var zoneStart = prev.To - spanOut + 1;
                var zoneEnd = region.From + spanIn - 1;
                if (order <= zoneEnd && order >= zoneStart)
                    return new SeasonBlend(prev.Season, region.Season, ZoneT(order, zoneStart, zoneEnd));
            }
            // Tail of this region may already be blending toward the next one.
            if (ri < regions.Length - 1)
            {
                var next = regions[ri + 1];
                var spanOut = Span(region, order);
                var spanIn = Span(next, order);
                var zoneStart = region.To - spanOut + 1;
                var zoneEnd = next.From + spanIn - 1;
                if (order >= zoneStart && order <= zoneEnd)
                    return new SeasonBlend(region.Season, next.Season, ZoneT(order, zoneStart, zoneEnd));
            }
            return new SeasonBlend(region.Season, region.Season, 0f);
        }

        static int RegionIndexFor(WorldMapData map, int order)
        {
            for (var i = 0; i < map.Regions.Length; i++)
                if (order >= map.Regions[i].From && order <= map.Regions[i].To) return i;
            return order < map.Regions[0].From ? 0 : map.Regions.Length - 1;
        }

        static int Span(WorldMapRegion region, int order)
        {
            var length = region.To - region.From + 1;
            var span = region.TransitionSpan > 0 ? region.TransitionSpan : DefaultSpan;
            return Math.Max(1, Math.Min(span, length));
        }

        static float ZoneT(int order, int zoneStart, int zoneEnd)
            => zoneEnd <= zoneStart ? 1f : (float)(order - zoneStart) / (zoneEnd - zoneStart);
    }
}
