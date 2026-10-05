using NUnit.Framework;
using QuietCamp.Domain;
using QuietCamp.Infrastructure;

namespace QuietCamp.Tests.Editor
{
    /// <summary>
    /// Generated-level path: "gen:<recipe>:<index>" ids resolve through the
    /// LevelGen recipe + CampSolver pipeline into valid, solvable LevelData.
    /// </summary>
    public class GeneratedLevelsTests
    {
        [Test]
        public void GeneratedId_Parses()
        {
            Assert.IsTrue(GeneratedCampSource.IsGeneratedId("gen:qc_camp:3"));
            Assert.IsTrue(GeneratedCampSource.TryParseId("gen:qc_camp:12", out var r, out var i));
            Assert.AreEqual("qc_camp", r);
            Assert.AreEqual(12, i);
            Assert.IsFalse(GeneratedCampSource.IsGeneratedId("QC001"));
        }

        [Test]
        public void Solver_SolvesAuthoredLevel()
        {
            var level = LevelLoader.Load("QC001");
            var solver = new CampSolver(level, System.Array.Empty<Placement>(), 5.0);
            var s = solver.Step();
            while (s == CampSolver.Status.Searching) s = solver.Step(0.05);
            Assert.AreEqual(CampSolver.Status.Solved, s);
            Assert.IsTrue(RuleEvaluator.Evaluate(level, solver.Solution).IsSolved);
        }

        [Test]
        public void Load_GeneratedLevel_IsValidAndSolved()
        {
            var level = LevelLoader.Load("gen:qc_camp:1");
            Assert.IsNotNull(level);
            Assert.AreEqual(4, level.width);
            Assert.AreEqual(6, level.height);
            Assert.IsNotNull(level.witness);
            Assert.AreEqual(level.guests.Length, level.witness.Length);
            Assert.IsTrue(RuleEvaluator.Evaluate(level, level.witness).IsSolved);
        }

        [Test]
        public void GeneratedLevels_AreDeterministicPerIndex()
        {
            var a = GeneratedCampSource.Generate("qc_camp", 5);
            var b = GeneratedCampSource.Generate("qc_camp", 5);
            Assert.AreEqual(a.id, b.id);
            Assert.AreEqual(a.contentHash,b.contentHash);
            Assert.IsEmpty(LevelContentValidator.Validate(a));
            Assert.IsTrue(RuleEvaluator.Evaluate(a,a.witness).IsSolved);
            Assert.AreEqual(a.blocked.Length, b.blocked.Length);
            Assert.AreEqual(a.guests.Length, b.guests.Length);
        }

        [Test]
        public void CampaignList_AppendsGeneratedIds()
        {
            var ids = LevelLoader.MvpLevelIds();
            Assert.AreEqual("QC001", ids[0]);
            Assert.AreEqual("QC010", ids[9]);
            Assert.AreEqual("gen:qc_camp:1", ids[10]);
            Assert.AreEqual(30, ids.Count);
        }
    }
}
