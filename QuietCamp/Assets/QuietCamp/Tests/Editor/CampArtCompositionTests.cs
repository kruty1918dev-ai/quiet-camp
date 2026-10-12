using System;
using System.IO;
using Newtonsoft.Json;
using NUnit.Framework;
using QuietCamp.Domain;
using QuietCamp.Infrastructure;
using System.Linq;

namespace QuietCamp.Tests
{
    public sealed class CampArtCompositionTests
    {
        [Test] public void AuthoredViewKeepsThePublishedPuzzleAndWitness()
        {
            var level = LevelLoader.Load("QC007");
            string before = JsonConvert.SerializeObject(level);
            var art = CampArtDefinition.Load(level);
            Assert.NotNull(art);
            Assert.AreEqual(6, art.models.Length);
            Assert.IsTrue(RuleEvaluator.Evaluate(level, level.witness).IsSolved);
            Assert.AreEqual(before, JsonConvert.SerializeObject(level));
        }
        [Test] public void DecorationCannotIntroduceABoardObstacle()
        {
            var level = LevelLoader.Load("QC007");
            var art = CampArtDefinition.Load(level);
            art.instances[0].x = 0; art.instances[0].z = 0;
            Assert.Throws<InvalidDataException>(() => art.Validate(level));
        }
        [Test] public void InvalidMeshCannotReachTheRenderer()
        {
            var level = LevelLoader.Load("QC007");
            var art = CampArtDefinition.Load(level);
            art.models[0].vertices[0] = float.NaN;
            Assert.Throws<InvalidDataException>(() => art.Validate(level));
        }
        [Test] public void OtherLevelsUseTheirExistingPresentation()
            => Assert.IsNull(CampArtDefinition.Load(LevelLoader.Load("QC001")));

        [TestCase(4, 12, 73)]
        [TestCase(12, 4, 74)]
        [TestCase(6, 8, 12007)]
        [TestCase(10, 10, 12009)]
        public void OneProfileProducesSupportedReproducibleCompositions(int width, int depth, int seed)
        {
            var level = new LevelData { id = "generic-host", width = width, height = depth };
            var first = CampCompositionGenerator.Generate(level, "riparian-clearing", seed, out var plan);
            var again = CampCompositionGenerator.Generate(level, "riparian-clearing", seed, out _);
            Assert.IsTrue(plan.Ok);
            Assert.IsTrue(plan.review.passed);
            Assert.AreEqual(8, plan.attempts.Length);
            Assert.AreEqual(plan.attempts.Where(a => a.qualityPassed).Max(a => a.score), plan.review.score);
            Assert.AreEqual(JsonConvert.SerializeObject(first), JsonConvert.SerializeObject(again));
            Assert.GreaterOrEqual(first.instances.Count(i => i.id.StartsWith("woodland-frame/", StringComparison.Ordinal)), 12);
            Assert.AreEqual(1, first.instances.Count(i => i.id == "crossing/bridge"));
            Assert.IsTrue(first.instances.All(i => Math.Abs(i.x) >= width * .5f + .2f || Math.Abs(i.z) >= depth * .5f + .2f));
        }
        [Test] public void ChangingSeedChangesPlacementWithoutChangingThePuzzle()
        {
            var level = LevelLoader.Load("QC007"); var before = JsonConvert.SerializeObject(level);
            var first = CampCompositionGenerator.Generate(level, "riparian-clearing", 12007, out _);
            var second = CampCompositionGenerator.Generate(level, "riparian-clearing", 19, out _);
            Assert.AreNotEqual(JsonConvert.SerializeObject(first.instances), JsonConvert.SerializeObject(second.instances));
            Assert.AreEqual(before, JsonConvert.SerializeObject(level));
        }
        [Test] public void LevelReferenceContainsOnlyAProfileAndSeed()
        {
            var text = UnityEngine.Resources.Load<UnityEngine.TextAsset>("QuietCamp/ArtCompositions/QC007").text;
            var document = Newtonsoft.Json.Linq.JObject.Parse(text);
            CollectionAssert.AreEquivalent(new[] { "profile", "seed" }, document.Properties().Select(p => p.Name).ToArray());
        }
    }
}
