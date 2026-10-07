using System;
using System.Linq;
using NUnit.Framework;
using QuietCamp.Domain;
using QuietCamp.Infrastructure;
using QuietCamp.Application;

namespace QuietCamp.Tests
{
    /// <summary>Season blending: the world reads as one place because every
    /// region boundary is a gradual palette sweep, never a theme switch.</summary>
    public class SeasonTransitionTests
    {
        static WorldMapData Map(params (string id, int from, int to, string season)[] regions)
        {
            var data = new WorldMapData();
            data.Regions = regions.Select(r => new WorldMapRegion
            {
                Id = r.id, From = r.from, To = r.to, Season = r.season,
                TransitionFrom = null,
            }).ToArray();
            for (var i = 1; i < data.Regions.Length; i++) data.Regions[i].TransitionFrom = data.Regions[i - 1].Id;
            data.Nodes = Enumerable.Range(1, regions[^1].to)
                .Select(o => new WorldMapNode { LevelId = "l" + o, Order = o, Y = o, Main = true }).ToArray();
            return data;
        }

        [Test] public void SummerBlendsIntoAutumnAcrossTheBoundary()
        {
            var map = Map(("a", 1, 10, "summer"), ("b", 11, 20, "autumn"));
            // Well inside a region: pure season.
            Assert.AreEqual(0f, SeasonTransitions.Sample(map, 5).T);
            Assert.AreEqual("summer", SeasonTransitions.Sample(map, 5).From);
            Assert.AreEqual("autumn", SeasonTransitions.Sample(map, 17).From);
            Assert.AreEqual(0f, SeasonTransitions.Sample(map, 17).T);
            // The zone straddles the boundary — T crosses 0.5 at the seam.
            var tail = SeasonTransitions.Sample(map, 9);
            var head = SeasonTransitions.Sample(map, 11);
            Assert.AreEqual("summer", tail.From); Assert.AreEqual("autumn", tail.To);
            Assert.AreEqual("summer", head.From); Assert.AreEqual("autumn", head.To);
            Assert.Less(tail.T, .5f); Assert.Greater(head.T, .5f);
        }

        [Test] public void BlendIsMonotonicThroughTheZone()
        {
            var map = Map(("a", 1, 10, "summer"), ("b", 11, 20, "autumn"));
            float last = -1;
            // Zone = last 2 of the outgoing region + first 2 of the incoming.
            for (var o = 9; o <= 12; o++)
            {
                var t = SeasonTransitions.Sample(map, o).T;
                Assert.GreaterOrEqual(t, last, "order " + o);
                last = t;
            }
            // Past the zone the season is settled — dominant never regresses.
            Assert.AreEqual("autumn", SeasonTransitions.Sample(map, 13).Dominant);
            Assert.AreEqual("summer", SeasonTransitions.Sample(map, 8).Dominant);
        }

        [Test] public void LateAutumnWinterThawChainsWithoutJumps()
        {
            var map = Map(("a", 1, 8, "autumn"), ("b", 9, 16, "winter"), ("c", 17, 24, "thaw"));
            var p = SeasonTransitions.Sample(map, 12).Palette;
            Assert.Greater(p.Snow, .9f, "mid-winter is snowbound");
            var tail = SeasonTransitions.Sample(map, 8);
            Assert.AreEqual("autumn", tail.From); Assert.AreEqual("winter", tail.To);
            var melt = SeasonTransitions.Sample(map, 17);
            Assert.AreEqual("winter", melt.From); Assert.AreEqual("thaw", melt.To);
            var thaw = SeasonTransitions.Sample(map, 22).Palette;
            Assert.Greater(thaw.Moisture, .8f, "thaw is the wettest state");
        }

        [Test] public void NoNeighbourhoodJumpExceedsAPaletteStep()
        {
            // The whole real campaign: consecutive main orders never change any
            // palette channel by more than the transition step.
            var map = WorldMapBuilder.Build(
                LevelLoader.MvpLevelIds(), LevelLoader.Districts(),
                BonusCampCatalog.Slots, MonetizationConfiguration.Load().Catalog().Journeys,
                CampContent.Summary);
            var prev = SeasonTransitions.Sample(map, 1).Palette;
            for (var o = 2; o <= map.Nodes.Length; o++)
            {
                var cur = SeasonTransitions.Sample(map, o).Palette;
                Assert.LessOrEqual(prev.MaxDelta(cur), .6f,
                    "palette jump at order " + o + " (" + SeasonTransitions.Sample(map, o).From + "→" + SeasonTransitions.Sample(map, o).To + ")");
                prev = cur;
            }
        }

        [Test] public void EveryRealSeasonHasAPalette()
        {
            var map = WorldMapBuilder.Build(
                LevelLoader.MvpLevelIds(), LevelLoader.Districts(),
                BonusCampCatalog.Slots, MonetizationConfiguration.Load().Catalog().Journeys,
                CampContent.Summary);
            foreach (var region in map.Regions)
                Assert.IsTrue(SeasonLooks.Has(region.Season), region.Id + " season " + region.Season);
        }
    }
}
