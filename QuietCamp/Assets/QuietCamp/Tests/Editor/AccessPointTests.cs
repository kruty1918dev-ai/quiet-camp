using System;
using System.Linq;
using Newtonsoft.Json;
using NUnit.Framework;
using QuietCamp.Domain;
using QuietCamp.Infrastructure;

namespace QuietCamp.Tests
{
    public class AccessPointTests
    {
        static LevelData Level()=>new LevelData{schemaVersion=1,ruleVersion=1,id="access-test",width=6,height=6,
            entry=new[]{0,0},blocked=Array.Empty<int[]>(),shade=Array.Empty<int[]>(),noise=Array.Empty<int[]>(),friends=Array.Empty<string[]>(),
            guests=new[]{new GuestData{id="a",assetId="tent_smallOpen",nameKey="guest.a"},new GuestData{id="b",assetId="tent_smallOpen",nameKey="guest.b"}}};
        [Test] public void OldContentAndAccessRoundTripRemainCompatible()
        {
            var level=Level();level.witness=null;Assert.IsEmpty(LevelContentValidator.Validate(level));
            level.accessPoints=new[]{new AccessPointData{id="out",kind="exit",x=5,z=2}};
            level.witness=new[]{new Placement{guestId="a",x=0,z=2,rotation=1},new Placement{guestId="b",x=3,z=0}};
            var copy=QuietCampLevelAdapter.ToLevelData(QuietCampLevelAdapter.ToDocument(level));
            Assert.AreEqual(JsonConvert.SerializeObject(level.accessPoints),JsonConvert.SerializeObject(copy.accessPoints));
            Assert.IsEmpty(LevelContentValidator.Validate(copy));
        }
        [Test] public void AccessCannotBeOccupiedAndExitRouteCannotBeCut()
        {
            var l=Level();l.accessPoints=new[]{new AccessPointData{id="out",kind="exit",x=5,z=3}};
            Assert.IsFalse(RuleEvaluator.Evaluate(l,new[]{new Placement{guestId="a",x=4,z=3}},false).CanCommit);
            l.blocked=Enumerable.Range(0,6).Select(z=>new[]{3,z}).ToArray();
            var report=RuleEvaluator.Evaluate(l,Array.Empty<Placement>(),false);
            Assert.IsTrue(report.CanCommit);Assert.IsTrue(report.Issues.Any(i=>i.Code=="path"));
            var solver=new CampSolver(l,Array.Empty<Placement>(),3);var status=solver.Step();while(status==CampSolver.Status.Searching)status=solver.Step(.05);
            Assert.AreEqual(CampSolver.Status.Unsatisfiable,status);
        }
        [Test] public void ValidatorRejectsMalformedBlockedAndDuplicatePoints()
        {
            var l=Level();l.witness=null;l.accessPoints=new[]{new AccessPointData{id="x",kind="wrong",x=6,z=0},new AccessPointData{id="x",kind="exit",x=0,z=0}};
            var errors=LevelContentValidator.Validate(l);
            foreach(var code in new[]{"access:kind","access:outside","access:id","access:duplicate"})Assert.Contains(code,errors);
            l.entry=Array.Empty<int>();Assert.DoesNotThrow(()=>LevelContentValidator.Validate(l));
        }
        [Test] public void EveryCampaignAccessIsReachableWithARealSolverSolution()
        {
            int exits=0;var edges=new System.Collections.Generic.HashSet<int>();
            foreach(var id in LevelLoader.MvpLevelIds())
            {
                var l=LevelLoader.Load(id);Assert.IsEmpty(LevelContentValidator.Validate(l),id);
                exits+=l.accessPoints.Count(p=>p.kind=="exit");
                edges.Add(l.entry[1]==0?0:l.entry[0]==l.width-1?1:l.entry[1]==l.height-1?2:3);
                var solver=new CampSolver(l,Array.Empty<Placement>(),5);var s=solver.Step();while(s==CampSolver.Status.Searching)s=solver.Step(.05);
                Assert.AreEqual(CampSolver.Status.Solved,s,id);Assert.IsTrue(RuleEvaluator.Evaluate(l,solver.Solution).IsSolved,id);
            }
            Assert.Greater(exits,5);Assert.GreaterOrEqual(edges.Count,3);
        }
        [TestCase(1)][TestCase(5)][TestCase(11)][TestCase(20)]
        public void GeneratedAccessUsesTheSameRules(int index)
        {
            var l=GeneratedCampSource.Generate("qc_camp",index);
            Assert.IsEmpty(LevelContentValidator.Validate(l));
            Assert.IsTrue(RuleEvaluator.Evaluate(l,l.witness).IsSolved);
        }
        [Test] public void HashArchiveRestoresPreUpgradeAlbumWithoutSnapshot()
        {
            var archives=UnityEngine.Resources.LoadAll<UnityEngine.TextAsset>("QuietCamp/ContentRevisions");
            Assert.GreaterOrEqual(archives.Length,30);
            foreach(var asset in archives)
            {
                var old=JsonConvert.DeserializeObject<LevelData>(asset.text);
                var entry=new QuietCamp.Application.AlbumSaveData.Entry{levelId=old.id,contentHash=old.contentHash,placements=old.witness};
                var restored=CampContent.AlbumLevel(entry);
                Assert.AreEqual(old.contentHash,restored.contentHash);
                Assert.IsTrue(RuleEvaluator.Evaluate(restored,entry.placements).IsSolved);
            }
            Assert.IsNull(CampContent.Revision("../invalid"));
        }
    }
}
