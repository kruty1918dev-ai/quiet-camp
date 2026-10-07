using System.Linq;
using NUnit.Framework;
using QuietCamp.Application;
using QuietCamp.Domain;
using QuietCamp.Infrastructure;

namespace QuietCamp.Tests
{
    /// <summary>The environmental storytelling contract: the map shows context,
    /// the level shows evidence, and nothing fabricated pretends to be fact.</summary>
    public class WorldStoryTests
    {
        static WorldMapData Map()
            => WorldMapBuilder.Build(
                LevelLoader.MvpLevelIds(), LevelLoader.Districts(),
                BonusCampCatalog.Slots, MonetizationConfiguration.Load().Catalog().Journeys,
                CampContent.Summary);

        [Test] public void EveryRegionHasAStory()
        {
            var map = Map();
            var catalog = WorldStoryCatalog.Current;
            foreach (var region in map.Regions)
            {
                var story = catalog.For(region.Id);
                Assert.NotNull(story, region.Id + " needs a story entry");
                Assert.IsNotEmpty(story.storyBeat, region.Id);
                Assert.IsNotEmpty(story.heroLandmark, region.Id);
            }
        }

        [Test] public void CatalogHonoursTheVisibilityContract()
        {
            var issues = WorldStoryCatalog.Validate(Map(), WorldStoryCatalog.Current);
            Assert.IsEmpty(issues, string.Join(";", issues));
        }

        [Test] public void ValidationCatchesBrokenStoryData()
        {
            var broken = new WorldStoryCatalog();
            var field = typeof(WorldStoryCatalog).GetField("_byId",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var table = (System.Collections.Generic.Dictionary<string, StoryMetadata>)field.GetValue(broken);
            table["ghost"] = new StoryMetadata { id = "ghost" };                                    // unknown id
            table["shores"] = new StoryMetadata { id = "shores", roadmapVisibility = "detail" };    // detail on the map
            table["farms"] = new StoryMetadata { id = "farms", historicalReference = "x" };          // reference without confidence
            table["villages"] = new StoryMetadata { id = "villages", referenceConfidence = "verified" }; // verified without reference
            var issues = WorldStoryCatalog.Validate(Map(), broken);
            Assert.IsTrue(issues.Any(i => i == "worldstory.unknown:ghost"));
            Assert.IsTrue(issues.Any(i => i == "worldstory.roadmapDetail:shores"));
            Assert.IsTrue(issues.Any(i => i == "worldstory.unlabeled:farms"));
            Assert.IsTrue(issues.Any(i => i == "worldstory.unverified:villages"));
        }

        [Test] public void StoryTellsThroughTheLandmarkNotTheText()
        {
            // Cultural cues live in props and places, not slogans: no entry may
            // carry a political text field — only objects and states.
            var catalog = WorldStoryCatalog.Current;
            var map = Map();
            foreach (var region in map.Regions)
            {
                var story = catalog.For(region.Id);
                Assert.IsTrue(string.IsNullOrEmpty(story.historicalReference) || story.Confidence != ReferenceConfidence.None,
                    region.Id + " needs a confidence label when it references history");
                Assert.AreNotEqual(StoryVisibility.Detail, story.Roadmap, region.Id + " leaks detail to the map");
            }
        }
    }
}
