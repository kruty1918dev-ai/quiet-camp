using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using NUnit.Framework;
using QuietCamp.Application;
using QuietCamp.Domain;
using QuietCamp.Infrastructure;

namespace QuietCamp.Tests
{
    public sealed class WalkingNetworkTests
    {
        static LevelData Empty(int version=1)=>new LevelData {schemaVersion=1,ruleVersion=version,id="walking-test",width=6,height=6,
            entry=new[]{5,5},blocked=Array.Empty<int[]>(),shade=Array.Empty<int[]>(),noise=Array.Empty<int[]>(),friends=Array.Empty<string[]>(),
            guests=new[]{new GuestData{id="a",assetId="tent_smallOpen",nameKey="guest.a"},new GuestData{id="b",assetId="tent_smallOpen",nameKey="guest.b"}}};
        [TestCase(1)][TestCase(2)]
        public void TwoTentsShareOneOpenDoorAndWalkingLane(int version)
        {
            var level=Empty(version);level.friends=new[]{new[]{"a","b"}};
            var tents=new[]{new Placement{guestId="a",x=0,z=0},new Placement{guestId="b",x=2,z=1,rotation=3}};
            Assert.AreEqual(RuleEvaluator.Door(tents[0]),RuleEvaluator.Door(tents[1]));
            var report=RuleEvaluator.Evaluate(level,tents);Assert.IsTrue(report.IsSolved);
            Assert.AreEqual(2,report.Routes.Count);Assert.IsTrue(report.Routes.All(r=>r.Reachable));
            Assert.AreEqual(1,RuleEvaluator.Path(level,new HashSet<Cell>(),RuleEvaluator.Door(tents[0]),RuleEvaluator.Door(tents[1])).Count);
        }
        [Test]
        public void ExteriorDoorRequiresAnAuthoredConnectedTrailAndKeepsFootprintInside()
        {
            var level=Empty(2);level.exteriorWalkable=Enumerable.Range(0,6).Select(x=>new[]{x,-1}).ToArray();
            var tents=new[]{new Placement{guestId="a",x=0,z=0,rotation=2},new Placement{guestId="b",x=3,z=3}};
            level.witness=tents;Assert.IsEmpty(LevelContentValidator.Validate(level));
            var report=RuleEvaluator.Evaluate(level,tents);Assert.IsTrue(report.IsSolved);
            var route=report.Routes.Single(r=>r.GuestId=="a");Assert.AreEqual(new Cell(0,-1),route.Goal);
            Assert.IsTrue(route.Cells.Any(c=>!RuleEvaluator.Inside(level,c)));
            level.ruleVersion=1;
            Assert.IsTrue(RuleEvaluator.Evaluate(level,tents).Issues.Any(i=>i.Code=="path"&&i.GuestId=="a"));
            level.ruleVersion=2;tents[0].rotation=3;
            Assert.AreEqual(RouteFailureReason.OutsideTrail,RuleEvaluator.Evaluate(level,tents).Routes.Single(r=>r.GuestId=="a").Reason);
            tents[0].x=-1;
            Assert.IsFalse(RuleEvaluator.Evaluate(level,tents).CanCommit);
        }
        [Test]
        public void ExteriorGatewayCannotEscapeThroughATentOrStaticObstacle()
        {
            var level=Empty(2);level.exteriorWalkable=new[]{new[]{0,-1},new[]{1,-1},new[]{2,-1}};
            level.blocked=new[]{new[]{2,0}};
            var report=RuleEvaluator.Evaluate(level,new[]{new Placement{guestId="a",x=0,z=0,rotation=2}},false);
            var route=report.Routes.Single();Assert.IsFalse(route.Reachable);
            Assert.AreEqual(RouteFailureReason.Disconnected,route.Reason);Assert.IsNotEmpty(route.BlockingCells);
        }
        [Test]
        public void RouteEvidenceShowsTheOccupiedDoorAndValidatorRejectsInvalidExteriorTopology()
        {
            var level=Empty(2);var occupied=new HashSet<Cell>{new Cell(1,2)};
            var route=CampWalkability.ExplainRoute(level,occupied,new Cell(5,5),new Cell(1,2),"a");
            Assert.AreEqual(RouteFailureReason.Blocked,route.Reason);Assert.Contains(new Cell(1,2),route.BlockingCells);
            level.exteriorWalkable=new[]{new[]{-2,-2},new[]{-2,-2},new[]{0,0},new[]{-3,0},Array.Empty<int>()};
            var errors=LevelContentValidator.Validate(level);
            foreach(var error in new[]{"exterior:duplicate","exterior:inside","exterior:bounds","exterior:shape"})Assert.Contains(error,errors);
            level.exteriorWalkable=new[]{new[]{-2,-2}};
            Assert.Contains("exterior:disconnected",LevelContentValidator.Validate(level));
        }
        [Test]
        public void SolverAndEvaluatorUseTheSameExteriorGraph()
        {
            var level=Empty(2);level.exteriorWalkable=Enumerable.Range(0,6).Select(x=>new[]{x,-1}).ToArray();
            var outside=new Placement{guestId="a",x=0,z=0,rotation=2};
            var solver=new CampSolver(level,new[]{outside},2);var status=solver.Step();
            while(status==CampSolver.Status.Searching)status=solver.Step(.05);
            Assert.AreEqual(CampSolver.Status.Solved,status);Assert.IsTrue(RuleEvaluator.Evaluate(level,solver.Solution).IsSolved);
            Assert.IsTrue(solver.Solution.Any(p=>!RuleEvaluator.Inside(level,RuleEvaluator.Door(p))));
        }
        [Test]
        public void IncrementalSuggestionPreservesAlreadyPlacedDoorRoutes()
        {
            var level=Empty();var original=new Placement{guestId="a",x=0,z=0};
            var solver=new CampSolver(level,new[]{original},2);
            var next=solver.SuggestMove(out var guest);Assert.AreEqual("b",guest);Assert.NotNull(next);
            var report=RuleEvaluator.Evaluate(level,new[]{original,next},false);
            Assert.IsTrue(report.CanCommit);Assert.IsFalse(report.Issues.Any(i=>i.Code=="path"));
        }
        [Test]
        public void AdapterRoundTripKeepsEnvironmentAndExteriorTrailExactly()
        {
            var level=LevelLoader.Load("gen:qc_camp:9");
            var copy=QuietCampLevelAdapter.ToLevelData(QuietCampLevelAdapter.ToDocument(level));
            Assert.AreEqual(JsonConvert.SerializeObject(level.environment),JsonConvert.SerializeObject(copy.environment));
            Assert.AreEqual(JsonConvert.SerializeObject(level.exteriorWalkable),JsonConvert.SerializeObject(copy.exteriorWalkable));
            Assert.AreEqual(2,copy.ruleVersion);Assert.IsEmpty(LevelContentValidator.Validate(copy));
        }
        [Test]
        public void ContentUpdateRestoresAnUnfinishedArchivedVersionWithoutReinterpretingIt()
        {
            var old=CampContent.Legacy("GEN001");
            var saved=new SessionSaveData{levelId="GEN001",contentHash=old.contentHash,ruleVersion=old.ruleVersion,placements=old.witness};
            var restored=CampContent.SessionLevel(saved,"gen:qc_camp:1");
            Assert.AreEqual("gen:qc_camp:1",restored.id);Assert.AreEqual(old.contentHash,restored.contentHash);
            Assert.AreEqual(1,restored.ruleVersion);Assert.IsTrue(RuleEvaluator.Evaluate(restored,saved.placements).IsSolved);
            Assert.AreNotEqual(LevelLoader.Load(restored.id).contentHash,restored.contentHash);
            saved.levelSnapshot=CampContent.Snapshot(restored);
            var roundTrip=JsonConvert.DeserializeObject<SessionSaveData>(JsonConvert.SerializeObject(saved));
            Assert.AreEqual(restored.contentHash,CampContent.SessionLevel(roundTrip,"gen:qc_camp:1").contentHash);
            // Explicitly choosing another level must not replay the old session.
            Assert.AreEqual("QC001",CampContent.SessionLevel(roundTrip,"QC001").id);
            Assert.AreEqual("gen:qc_camp:1",CampContent.UnfinishedLevelId(roundTrip,LevelLoader.MvpLevelIds()));
            roundTrip.placements=Array.Empty<Placement>();Assert.IsNull(CampContent.UnfinishedLevelId(roundTrip,LevelLoader.MvpLevelIds()));
        }
        [Test]
        public void FrozenCampaignMatchesTheSeasonalStoryAndWaterTable()
        {
            var ids=LevelLoader.MvpLevelIds();var seasons=new[]{"spring","summer","summer","autumn","winter","spring"};
            var water=new HashSet<int>{7,10,13,19,24,29};
            for(int i=0;i<ids.Count;i++)
            {
                var level=LevelLoader.Load(ids[i]);Assert.AreEqual(2,level.ruleVersion);Assert.AreEqual(seasons[i/5],level.environment.seasonId);
                Assert.AreEqual(water.Contains(i+1),level.environment.shore!=null,level.id);
                Assert.AreEqual(CampContent.CalculateHash(level),level.contentHash,level.id+" hash");
                Assert.AreEqual(JsonConvert.SerializeObject(level.environment),JsonConvert.SerializeObject(CampContent.Summary(level.id).environment));
            }
            var first=LevelLoader.Load(ids[0]);Assert.AreEqual(2,first.guests.Length);Assert.IsTrue(first.guests.All(g=>!g.shade&&!g.quiet));
        }
        [Test]
        public void ShoreCannotCoverAnAccessTrailOrThePlayableBoard()
        {
            var level=Empty(2);level.environment=new EnvironmentCompositionData {biomeId="shore",seasonId="summer",weatherId="clear",treeDensity=.5f,
                shore=new ShorelineData{kind="lake",side="front",width=7,offset=9.5f}};
            Assert.Contains("shore:trail",LevelContentValidator.Validate(level));
            level.environment.shore.side="left";level.environment.shore.offset=4;
            Assert.Contains("shore:clearance",LevelContentValidator.Validate(level));
            level.environment.shore.offset=9.5f;Assert.IsEmpty(LevelContentValidator.Validate(level));
        }
    }
}
