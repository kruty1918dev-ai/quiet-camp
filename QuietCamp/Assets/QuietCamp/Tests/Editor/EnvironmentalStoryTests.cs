using RoadmapSceneGenerator = QuietCamp.Presentation.UI.Prepared.RoadmapSceneGenerator;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using QuietCamp.Application;
using QuietCamp.Domain;
using QuietCamp.Infrastructure;
using QuietCamp.Presentation.UI;
using QuietCamp.Presentation.World;
using UnityEngine;
using UnityEngine.TestTools;

namespace QuietCamp.Tests
{
    public sealed class EnvironmentalStoryTests
    {
        static EnvironmentalStoryData Story() => JsonConvert.DeserializeObject<EnvironmentalStoryData>(File.ReadAllText(
            Path.Combine(UnityEngine.Application.dataPath, "QuietCamp/Tests/Fixtures/Roadmap/environmental-story.json")));
        static LevelData Level(string id)
        {
            var level = CampContent.Snapshot(LevelLoader.Load(id));
            level.environmentalStory = RoadmapStoryCompiler.ForLevel(Story(), id);
            return level;
        }

        [Test] public void LevelKitAndAlbumSnapshotRetainBothViewScopesAndNarrative()
        {
            var level = Level("QC001");
            var copy = QuietCampLevelAdapter.ToLevelData(QuietCampLevelAdapter.ToDocument(level));
            Assert.AreEqual(JsonConvert.SerializeObject(level.environmentalStory), JsonConvert.SerializeObject(copy.environmentalStory));
            Assert.AreEqual(JsonConvert.SerializeObject(level.environmentalStory), JsonConvert.SerializeObject(CampContent.Snapshot(level).environmentalStory));
            Assert.IsEmpty(LevelContentValidator.Validate(copy));
            var plain = CampContent.Snapshot(LevelLoader.Load("QC001"));
            Assert.IsNull(JObject.FromObject(plain)["environmentalStory"]);
            var previous = CampContent.CalculateHash(plain);
            plain.environmentalStory = null;
            Assert.AreEqual(previous, CampContent.CalculateHash(plain));
        }

        [Test] public void MapCannotRenderLevelOnlyDetailAndReusedPropsResetSilhouette()
        {
            var level = Level("QC001"); var summary = CampContent.Summary(level.id);
            var world = RoadmapCompiler.BakeWorld(summary, level.environmentalStory);
            Assert.IsTrue(world.props.Any(p => p.storyId == "wayside:shelter"));
            Assert.IsFalse(world.props.Any(p => p.storyId == "wayside:board"));
            var scene = RoadmapSceneGenerator.FromBaked(summary, world, AtmosphereCatalog.Load());
            var silhouette = scene.Props.Single(p => p.StorySilhouette);
            Assert.AreEqual("story_trail_shelter", silhouette.Asset);
            var same = RoadmapSceneGenerator.FromBaked(summary, RoadmapCompiler.BakeWorld(summary), AtmosphereCatalog.Load(), reusable: scene);
            Assert.IsFalse(same.Props.Any(p => p.StorySilhouette));
        }

        [Test] public void GameplayDetailsStayScenicAndCareReplacesRatherThanDuplicates()
        {
            var level = Level("QC002"); Mesh before = null, after = null;
            try
            {
                before = EnvironmentalStoryVisual.Compose(level, false); after = EnvironmentalStoryVisual.Compose(level, true);
                Assert.NotNull(before); Assert.NotNull(after);
                Assert.Greater(after.bounds.size.y, before.bounds.size.y);
                Assert.AreEqual(before.vertexCount, after.vertexCount); // Replacement at same place, not doubled geometry.
                Assert.IsTrue(EnvironmentComposer.CanScenicBounds(level, before.bounds));
                Assert.IsTrue(EnvironmentComposer.CanScenicBounds(level, after.bounds));
                Assert.Less(after.vertexCount, 60000);
            }
            finally { if (before != null) Object.DestroyImmediate(before); if (after != null) Object.DestroyImmediate(after); }
        }

        [Test] public void UnsafeStoryPropCannotOccupyPuzzleOrChangeRules()
        {
            var level = Level("QC002"); var witness = JsonConvert.SerializeObject(level.witness);
            var prop = level.environmentalStory.beats[0].props[0]; prop.level.x = prop.level.z = 0;
            LogAssert.Expect(LogType.Error, "[EnvironmentalStory] Unsafe/overlapping detail: crossing:old");
            var mesh = EnvironmentalStoryVisual.Compose(level, false);
            Assert.IsNull(mesh); Assert.AreEqual(witness, JsonConvert.SerializeObject(level.witness));
            Assert.IsTrue(RuleEvaluator.Evaluate(level, level.witness, true).IsSolved);
        }

        [Test] public void WindUsesPerPropRootsWhileInfrastructureRemainsRigid()
        {
            var level = Level("QC002"); var prop = level.environmentalStory.beats[0].props[0];
            prop.category = "nature"; prop.motif = "field"; prop.level.assetId = "grass_leafsLarge"; prop.level.sway = true;
            Mesh moving = null, rigid = null;
            try
            {
                moving = EnvironmentalStoryVisual.Compose(level, false); rigid = EnvironmentalStoryVisual.Compose(level, true);
                var roots = new System.Collections.Generic.List<Vector4>(); moving.GetUVs(1, roots);
                Assert.AreEqual(moving.vertexCount, roots.Count); Assert.IsTrue(roots.All(r => r.w == .6f && r.x == -13));
                roots.Clear(); rigid.GetUVs(1, roots); Assert.IsTrue(roots.All(r => r.w < 0));
            }
            finally { if (moving != null) Object.DestroyImmediate(moving); if (rigid != null) Object.DestroyImmediate(rigid); }
        }

        [Test] public void ProgressOnlyChangesAppearanceWithoutRevealingNewEvidence()
        {
            var level = Level("QC002"); var summary = CampContent.Summary(level.id);
            var world = RoadmapCompiler.BakeWorld(summary, level.environmentalStory);
            var scene = RoadmapSceneGenerator.FromBaked(summary, world, AtmosphereCatalog.Load(), completed: false);
            Assert.AreEqual(.6f, scene.Props.Single(p => p.Asset == "log_stack" && p.Position.x == -13).Height);
            var after = RoadmapSceneGenerator.FromBaked(summary, world, AtmosphereCatalog.Load(), reusable: scene, completed: true);
            Assert.AreEqual(.8f, after.Props.Single(p => p.Asset == "log_stack" && p.Position.x == -13).Height);
            Assert.IsTrue(after.StoryCompleted);
        }
    }
}
