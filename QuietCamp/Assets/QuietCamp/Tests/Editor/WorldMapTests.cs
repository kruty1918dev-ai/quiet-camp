using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using QuietCamp.Application;
using QuietCamp.Domain;
using QuietCamp.Infrastructure;

namespace QuietCamp.Tests
{
    /// <summary>The data-driven world model: every roadmap visual derives from
    /// WorldMapData, so its integrity is what guarantees the map can scale to
    /// 300+ levels without hand-editing UI.</summary>
    public class WorldMapTests
    {
        static WorldMapData Build()
            => WorldMapBuilder.Build(
                LevelLoader.MvpLevelIds(), LevelLoader.Districts(),
                BonusCampCatalog.Slots, MonetizationConfiguration.Load().Catalog().Journeys,
                CampContent.Summary);

        [Test] public void WorldMapCoversEveryMainLevelExactlyOnce()
        {
            var data = Build();
            Assert.AreEqual(LevelLoader.MvpLevelIds().Count, data.Nodes.Length);
            for (var i = 0; i < data.Nodes.Length; i++)
            {
                Assert.IsTrue(data.Nodes[i].Main);
                Assert.AreEqual(i + 1, data.Nodes[i].Order);
                Assert.AreEqual(LevelLoader.MvpLevelIds()[i], data.Nodes[i].LevelId);
            }
        }

        [Test] public void RegionsAreDistrictsAndChunksAreContiguous()
        {
            var data = Build();
            Assert.AreEqual(15, data.Regions.Length);
            Assert.AreEqual("glades", data.Regions[0].Id);
            Assert.AreEqual("haven", data.Regions[^1].Id);
            // 110 levels / 8 per chunk = 14 chunks covering the whole road.
            Assert.AreEqual((110 + WorldMapBuilder.ChunkSize - 1) / WorldMapBuilder.ChunkSize, data.Chunks.Length);
            Assert.AreEqual(1, data.Chunks[0].FirstOrder);
            Assert.AreEqual(110, data.Chunks[^1].LastOrder);
        }

        [Test] public void BranchesCarryTypeAccessAndAnchors()
        {
            var data = Build();
            Assert.IsTrue(data.Branches.Any(b => b.Type == WorldBranchType.BonusGlade && b.NodeIds.Length == 1));
            var memories = data.Branches.First(b => b.Id == "memories");
            Assert.AreEqual(WorldBranchType.MiniTrail, memories.Type);
            Assert.AreEqual(WorldBranchAccess.Progression, memories.Access);
            Assert.AreEqual(3, memories.NodeIds.Length);
            var resort = data.Branches.First(b => b.Id == "resort");
            Assert.AreEqual(WorldBranchType.StoryJourney, resort.Type);
            Assert.GreaterOrEqual(resort.NodeIds.Length, 8);
            // Unpublished journeys never appear on the map — the lighthouse is
            // staging content until release approval.
            Assert.IsFalse(data.Branches.Any(b => b.Id == "lighthouse"));
            // Bonus slots already became BonusGlade branches; the synthetic
            // "bonus.*" journey mirrors must not duplicate them.
            Assert.IsFalse(data.Branches.Any(b => b.Id.StartsWith("bonus.")));
            foreach (var branch in data.Branches)
                Assert.IsTrue(data.Nodes[branch.AttachOrder - 1].BranchLinks.Contains(branch.Id),
                    branch.Id + " must hang off its anchor node");
        }

        [Test] public void BranchesPhysicallyLeaveTheRoad()
        {
            var data = Build();
            foreach (var branch in data.Branches)
            {
                Assert.AreEqual(branch.NodeIds.Length, branch.Nodes.Length, branch.Id);
                var anchor = data.Nodes[branch.AttachOrder - 1];
                // A teaser never shows more nodes than its type allows:
                // bonus = one stop, trail = a silhouette of the second,
                // journey = the opening pair.
                Assert.LessOrEqual(branch.TeaserDepth, 2, branch.Id);
                for (var i = 0; i < branch.Nodes.Length; i++)
                {
                    var node = branch.Nodes[i];
                    Assert.IsFalse(node.Main);
                    Assert.Greater(node.Y, anchor.Y, branch.Id + " climbs past its anchor");
                    if (i > 0)
                    {
                        Assert.Greater(node.Y, branch.Nodes[i - 1].Y, branch.Id + " monotonic");
                        Assert.Greater(System.Math.Abs(node.X - anchor.X), .15f, branch.Id + " departs sideways");
                    }
                }
            }
        }

        [Test] public void NodePositionsFollowTheRoad()
        {
            var data = Build();
            for (var i = 1; i < data.Nodes.Length; i++)
                Assert.Greater(data.Nodes[i].Y, data.Nodes[i - 1].Y, "node " + i);
            foreach (var node in data.Nodes)
            {
                Assert.Greater(node.X, 0); Assert.Less(node.X, 1);
            }
        }

        [Test] public void ValidatorAcceptsTheRealWorld()
        {
            var issues = WorldMapValidator.Validate(Build());
            // warn:* entries are inputs to the transition-zone stage, not errors.
            var errors = issues.Where(i => !i.StartsWith("warn:")).ToList();
            Assert.IsEmpty(errors, string.Join(";", errors));
        }

        [Test] public void ValidatorReportsSeasonGapsForTheTransitionStage()
        {
            var warnings = WorldMapValidator.Validate(Build()).Where(i => i.StartsWith("warn:")).ToList();
            // The frozen campaign jumps e.g. summer→winter at "embers" — these are
            // precisely the seams the transition-zone system must absorb.
            Assert.IsNotEmpty(warnings);
            Assert.IsTrue(warnings.All(w => w.StartsWith("warn:worldmap.region.season:")));
        }

        [Test] public void ValidatorCatchesBrokenWorlds()
        {
            var data = Build();
            data.Nodes[5].LevelId = data.Nodes[4].LevelId;                       // duplicate id
            data.Branches[0].AttachOrder = data.Nodes.Length + 40;               // unreachable
            data.Branches[1].RequiredCompletions = data.Nodes.Length + 1;        // impossible unlock
            data.Regions[2].From += 1;                                           // region gap
            data.Chunks[3].FirstOrder += 1;                                      // chunk gap
            var issues = WorldMapValidator.Validate(data);
            Assert.IsTrue(issues.Any(i => i.StartsWith("worldmap.node.duplicate")));
            Assert.IsTrue(issues.Any(i => i.StartsWith("worldmap.branch.unreachable")));
            Assert.IsTrue(issues.Any(i => i.StartsWith("worldmap.branch.impossible")));
            Assert.IsTrue(issues.Any(i => i.StartsWith("worldmap.region.gap")));
            Assert.IsTrue(issues.Any(i => i.StartsWith("worldmap.chunk.gap")));
        }

        [Test] public void RegionsCarryIdentitySampledFromRealContent()
        {
            var data = Build();
            for (var i = 1; i < data.Regions.Length; i++)
                Assert.AreEqual(data.Regions[i - 1].Id, data.Regions[i].TransitionFrom);
            foreach (var region in data.Regions)
            {
                Assert.IsNotEmpty(region.Season, region.Id + " season");
                Assert.IsNotEmpty(region.Biome, region.Id + " biome");
                Assert.IsNotEmpty(region.Lighting, region.Id + " lighting");
            }
            // Adjacent regions may legitimately differ in season — transitions are
            // a rendering concern; the data only has to expose both endpoints.
            Assert.AreEqual("spring", data.Regions[0].Season); // glades opens in spring
        }
    }
}
