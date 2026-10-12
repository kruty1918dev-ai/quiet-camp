using System;
using System.IO;
using Newtonsoft.Json;
using NUnit.Framework;
using QuietCamp.Domain;
using QuietCamp.Infrastructure;

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
    }
}
