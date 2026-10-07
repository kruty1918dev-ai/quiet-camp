using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using NUnit.Framework;
using QuietCamp.Application;
using QuietCamp.Domain;
using QuietCamp.Infrastructure;

namespace QuietCamp.Tests
{
    /// <summary>The 300+ contract: the pipeline must build, validate and
    /// reveal a release-scale world without hand-authoring or runaway cost.
    /// Synthetic content exercises the real builder, not a mock.</summary>
    public class WorldMapScaleTests
    {
        const int LevelCount = 320;

        static WorldMapData SyntheticWorld()
        {
            var ids = Enumerable.Range(1, LevelCount).Select(i => "gen:synthetic:" + i).ToList();
            // 16 regions of 20 — seasons deliberately cycle incl. jumps the
            // transition system must absorb.
            var seasons = new[] { "spring", "summer", "autumn", "winter", "thaw" };
            var districts = Enumerable.Range(0, 16).Select(r => new DistrictDefinition
            {
                id = "region" + r, act = r / 4 + 1, from = r * 20 + 1, to = r * 20 + 20,
            }).ToList();
            var slots = Enumerable.Range(0, 20).Select(i => new BonusCampDefinition
            {
                id = "glade" + i, levelId = "gen:bonus:" + i, afterLevel = 10 + i * 15,
                titleKey = "t", descriptionKey = "d", requiredCompletions = 10 + i * 15, theme = "pond",
            }).ToList();
            var journeys = Enumerable.Range(0, 12).Select(i => new JourneyDefinition
            {
                id = "trail" + i, published = true, requiredCompletions = 20 + i * 20,
                titleKey = "t", descriptionKey = "d",
                levelIds = Enumerable.Range(0, i % 3 == 0 ? 10 : 4)
                    .Select(j => "gen:trail" + i + ":" + j).ToArray(),
            }).ToList();
            return WorldMapBuilder.Build(ids, districts, slots, journeys,
                id => new LevelSummary { id = id, environmentPreset = "meadow", lighting = "day" });
        }

        [Test] public void BuilderScalesPast300Levels()
        {
            var sw = Stopwatch.StartNew();
            var map = SyntheticWorld();
            sw.Stop();
            Assert.AreEqual(LevelCount, map.Nodes.Length);
            Assert.AreEqual(16, map.Regions.Length);
            Assert.AreEqual(LevelCount / WorldMapBuilder.ChunkSize, map.Chunks.Length);
            Assert.AreEqual(32, map.Branches.Length); // 20 glades + 12 trails
            Assert.Less(sw.ElapsedMilliseconds, 5000, "build must stay interactive-scale");
            foreach (var branch in map.Branches)
                Assert.AreEqual(branch.NodeIds.Length, branch.Nodes.Length);
        }

        [Test] public void ValidationStaysCorrectAtScale()
        {
            var issues = WorldMapValidator.Validate(SyntheticWorld())
                .Where(i => !i.StartsWith("warn:")).ToList();
            Assert.IsEmpty(issues, string.Join(";", issues.Take(10)));
        }

        [Test] public void RevealKeepsGeometryBoundedAtScale()
        {
            var map = SyntheticWorld();
            var progress = new ProgressionService();
            var reveal = new WorldMapRevealService(map, progress, null);
            Assert.AreEqual(1, reveal.ActiveChunks().Count);
            // Halfway through: only chunks up to the horizon may be live —
            // never the whole world.
            for (var i = 1; i <= 160; i++) progress.MarkCompleted("gen:synthetic:" + i);
            var reveal2 = new WorldMapRevealService(map, progress, null);
            var active = reveal2.ActiveChunks();
            // Horizon = order 161 + lookahead 2 = 163 → chunks 0..20.
            var expected = (163 + WorldMapBuilder.ChunkSize - 1) / WorldMapBuilder.ChunkSize;
            Assert.AreEqual(expected, active.Count);
            Assert.Less(active.Count, map.Chunks.Length, "far chunks stay data-only");
            Assert.GreaterOrEqual(active.Count, 1);
            // Hidden future may not leak: order beyond horizon is not visible.
            var h = new WorldMapRevealService(map, progress, null).Horizon;
            Assert.AreEqual(WorldNodeState.Hidden,
                new WorldMapRevealService(map, progress, null).State("gen:synthetic:" + (h + 2)));
        }

        [Test] public void SeasonSweepStaysContinuousAtScale()
        {
            var map = SyntheticWorld();
            var prev = SeasonTransitions.Sample(map, 1).Palette;
            for (var o = 2; o <= map.Nodes.Length; o++)
            {
                var cur = SeasonTransitions.Sample(map, o).Palette;
                Assert.LessOrEqual(prev.MaxDelta(cur), .6f, "palette jump at order " + o);
                prev = cur;
            }
        }

        [Test] public void ValidatorStillFlagsBadWorldsAtScale()
        {
            var map = SyntheticWorld();
            map.Nodes[100].Y = map.Nodes[99].Y;                                  // monotonic break
            map.Nodes[100].X = map.Nodes[99].X;                                  // → overlap too
            var issues = WorldMapValidator.Validate(map);
            Assert.IsTrue(issues.Any(i => i.StartsWith("worldmap.node.monotonic")));
            Assert.IsTrue(issues.Any(i => i.StartsWith("worldmap.node.overlap")));
        }
    }
}
