using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using QuietCamp.Application;
using QuietCamp.Domain;
using QuietCamp.Infrastructure;
using UnityEngine;

namespace QuietCamp.Tests
{
    public sealed class AdaptiveTutorialTests
    {
        static TutorialDirector Guide()=>new TutorialDirector(null,false,new ProgressionService(),()=>true,
            LearningCurriculum.Read(Resources.Load<TextAsset>("QuietCamp/learning_curriculum").text));
        static LevelData Simple()=>new LevelData {id="QC001",width=6,height=6,ruleVersion=1,entry=new[]{5,5},
            blocked=Array.Empty<int[]>(),shade=Array.Empty<int[]>(),noise=Array.Empty<int[]>(),friends=Array.Empty<string[]>(),
            guests=new[]{new GuestData{id="a"},new GuestData{id="b"}}};
        static Placement Pose(string id,int x,int z,int r=0)=>new Placement{guestId=id,x=x,z=z,rotation=r};

        [Test]
        public void EveryOpeningLayoutIsAcceptedOrExplainedWithoutRequiringTheWitness()
        {
            var level=LevelLoader.Load("QC001");var guide=Guide();using var session=new CampSession(level);
            int total=0,solved=0;var distinct=new HashSet<string>();var rotations=new int[4];
            for(int ax=0;ax<level.width-1;ax++)for(int az=0;az<level.height-1;az++)
            for(int bx=0;bx<level.width-1;bx++)for(int bz=0;bz<level.height-1;bz++)
            for(int ar=0;ar<4;ar++)for(int br=0;br<4;br++)
            {
                var poses=new[]{Pose(level.guests[0].id,ax,az,ar),Pose(level.guests[1].id,bx,bz,br)};
                var report=RuleEvaluator.Evaluate(level,poses);
                session.Restore(poses,poses[0].guestId);var cue=guide.Cue(session);total++;
                if(report.IsSolved)
                {
                    Assert.AreEqual("guide.adaptive.finish",cue);solved++;rotations[ar]++;
                    distinct.Add(ax+","+az+"/"+bx+","+bz);
                }
                else Assert.IsTrue(cue.StartsWith("rule."),"An actual defect must have an explanation: "+cue);
                Assert.IsFalse(cue=="guide.rotate"||cue=="guide.place","A static lesson must not contradict a live board.");
            }
            Assert.AreEqual((level.width-1)*(level.height-1)*(level.width-1)*(level.height-1)*16,total);
            Assert.IsTrue(rotations.All(n=>n>0));Assert.Greater(distinct.Count,10);
            Debug.Log($"[AdaptiveTutorial] Opening: {total} complete layouts; {solved} valid; {distinct.Count} distinct footprint assignments; all four door directions valid.");
        }

        [Test]
        public void EveryTeachingCandidateUsesActualRouteEvidenceIncludingBordersAndOtherTents()
        {
            int checkedCount=0;var guide=Guide();
            foreach(var id in LevelLoader.CampaignLevelIds().Take(20))
            {
                var level=LevelLoader.Load(id);using var session=new CampSession(level);
                foreach(var guest in level.guests)
                foreach(bool crowded in new[]{false,true})
                {
                    session.Restore(crowded?level.witness.Where(p=>p.guestId!=guest.id):Array.Empty<Placement>(),guest.id);
                    for(int x=-1;x<level.width;x++)for(int z=-1;z<level.height;z++)for(int r=0;r<4;r++)
                    {
                        var report=session.Preview(PlacementCommand.Place(guest.id,x,z,r));
                        var feedback=PlacementGuidance.Explain(level,report,guest.id);checkedCount++;
                        if(!feedback.IsIssue)
                            Assert.IsEmpty(report.Issues.Where(i=>i.Code!="missing"&&i.Code!="friends-missing"));
                        else if(feedback.Key.StartsWith("rule.path"))
                        {
                            Assert.IsTrue(report.CanCommit);
                            Assert.IsTrue(report.Routes.Any(route=>route.GuestId==feedback.GuestId&&!route.Reachable),id+" invented a route failure.");
                        }
                        if(!report.CanCommit)Assert.IsFalse(feedback.Key.StartsWith("rule.path"),"Hard-invalid geometry cannot diagnose a door.");
                    }
                }
                // Restore actual content, then advance the same guide to this lesson's successor.
                session.Restore(level.witness,level.guests[0].id);
                Assert.AreEqual("guide.adaptive.finish",guide.Cue(session),id);
                Assert.IsTrue(session.Check().IsSolved);Assert.IsTrue(guide.ReportAction("complete",session));
            }
            Debug.Log($"[AdaptiveTutorial] First twenty camps: {checkedCount} candidate poses, all four rotations, empty and occupied contexts, including out-of-bounds anchors.");
        }

        [TestCase(0)][TestCase(1)][TestCase(2)][TestCase(3)]
        public void AnOutsideDoorWorksOnlyWhenAnAuthoredTrailActuallyReachesIt(int rotation)
        {
            var level=Simple();var p=rotation==0?Pose("a",1,4,0):rotation==1?Pose("a",4,1,1)
                :rotation==2?Pose("a",1,0,2):Pose("a",0,1,3);
            var report=RuleEvaluator.Evaluate(level,new[]{p},false);
            Assert.AreEqual("rule.path.outside-trail",PlacementGuidance.Explain(level,report,"a").Key);
            level.ruleVersion=2;var ring=new List<int[]>();
            for(int i=-1;i<=6;i++){ring.Add(new[]{i,-1});ring.Add(new[]{i,6});ring.Add(new[]{-1,i});ring.Add(new[]{6,i});}
            level.exteriorWalkable=ring.ToArray();report=RuleEvaluator.Evaluate(level,new[]{p},false);
            Assert.IsTrue(report.Routes.Single().Reachable);Assert.IsFalse(PlacementGuidance.Explain(level,report,"a").IsIssue);
        }

        [Test]
        public void SharedDoorwaysAndRoutesAreValidWithoutFacingAPrescribedDirection()
        {
            var level=Simple();using var session=new CampSession(level);
            var a=Pose("a",0,0,1);var b=Pose("b",2,1,2);
            Assert.AreEqual(RuleEvaluator.Door(a),RuleEvaluator.Door(b));
            session.Restore(new[]{a,b},"a");Assert.IsTrue(RuleEvaluator.Evaluate(level,session.State.Placements).IsSolved);
            Assert.AreEqual("guide.adaptive.finish",Guide().Cue(session));
        }

        [Test]
        public void FixingADoorUndoRedoAndSelectingAnotherGuestImmediatelyChangesTheAdvice()
        {
            using var session=new CampSession(Simple());var guide=Guide();
            Assert.IsTrue(session.TryCommit(PlacementCommand.Place("a",1,4,0),out _));
            Assert.AreEqual("rule.path.outside-trail",guide.Cue(session));
            Assert.IsTrue(session.TryCommit(PlacementCommand.Place("a",1,4,2),out _));
            Assert.AreEqual("guide.adaptive.next",guide.Cue(session));
            Assert.IsTrue(session.Undo());Assert.AreEqual("rule.path.outside-trail",guide.Cue(session));
            Assert.IsTrue(session.Redo());session.Select("b");Assert.AreEqual("guide.adaptive.next",guide.Cue(session));
            Assert.IsTrue(session.TryCommit(PlacementCommand.Place("b",3,1,0),out _));
            Assert.AreEqual("guide.adaptive.finish",guide.Cue(session));
        }

        [Test]
        public void ABlockedOrDisconnectedDoorNamesTheAffectedTentNotTheValidSelectedTent()
        {
            var level=Simple();var a=Pose("a",0,0,0);var b=Pose("b",0,2,1);
            var report=RuleEvaluator.Evaluate(level,new[]{a,b},false);
            var feedback=PlacementGuidance.Explain(level,report,"b");
            Assert.AreEqual("rule.path.blocked",feedback.Key);Assert.AreEqual("a",feedback.GuestId);
            level=Simple();level.blocked=Enumerable.Range(0,6).Select(x=>new[]{x,3}).ToArray();
            report=RuleEvaluator.Evaluate(level,new[]{Pose("a",0,0,0)},false);
            Assert.AreEqual("rule.path.disconnected",PlacementGuidance.Explain(level,report,"a").Key);
        }

        [Test]
        public void AllSixteenShadeMasksAndTheQuietBoundaryUseTheActualFootprint()
        {
            var level=Simple();var p=Pose("a",1,1);level.guests[0].shade=true;
            var footprint=RuleEvaluator.Footprint(p);
            for(int mask=0;mask<16;mask++)
            {
                level.shade=footprint.Where((c,i)=>(mask&(1<<i))!=0).Select(c=>new[]{c.X,c.Z}).ToArray();
                Assert.AreEqual(mask!=15,PlacementGuidance.Explain(level,RuleEvaluator.Evaluate(level,new[]{p},false),"a").Key=="rule.shade");
            }
            level.guests[0].shade=false;level.guests[0].quiet=true;
            level.noise=new[]{new[]{4,1}};Assert.AreEqual("rule.quiet",PlacementGuidance.Explain(level,RuleEvaluator.Evaluate(level,new[]{p},false),"a").Key);
            level.noise=new[]{new[]{5,1}};Assert.IsFalse(PlacementGuidance.Explain(level,RuleEvaluator.Evaluate(level,new[]{p},false),"a").IsIssue);
        }

        [Test]
        public void UnplacedFriendsAreANextStepAndPlacedFriendsUseTheThreeStepWalkingDistance()
        {
            var level=Simple();level.friends=new[]{new[]{"a","b"}};var a=Pose("a",0,0,1);
            Assert.IsFalse(PlacementGuidance.Explain(level,RuleEvaluator.Evaluate(level,new[]{a}),"a").IsIssue);
            var witnessed=new HashSet<int>();
            for(int x=0;x<5;x++)for(int z=0;z<5;z++)for(int r=0;r<4;r++)
            {
                var b=Pose("b",x,z,r);var poses=new[]{a,b};var report=RuleEvaluator.Evaluate(level,poses);
                if(!report.CanCommit||report.Routes.Any(route=>!route.Reachable))continue;
                var occupied=new HashSet<Cell>(poses.SelectMany(RuleEvaluator.Footprint));
                var path=CampWalkability.Path(level,occupied,RuleEvaluator.Door(a),RuleEvaluator.Door(b));
                if(path==null||path.Count-1!=3&&path.Count-1!=4)continue;
                int distance=path.Count-1;witnessed.Add(distance);
                Assert.AreEqual(distance>3,PlacementGuidance.Explain(level,report,"b").Key=="rule.friends");
            }
            CollectionAssert.AreEquivalent(new[]{3,4},witnessed);
        }

        [Test]
        public void CombinedWishesDoNotDisappearWhenOnlyOneOfThemHasBeenFixed()
        {
            var level=Simple();var p=Pose("a",1,1);level.guests[0].shade=true;level.guests[0].quiet=true;
            level.noise=new[]{new[]{4,1}};using var session=new CampSession(level);session.Restore(new[]{p},"a");
            var guide=Guide();Assert.AreEqual("rule.shade",guide.Cue(session));
            level.shade=RuleEvaluator.Footprint(p).Select(c=>new[]{c.X,c.Z}).ToArray();
            Assert.AreEqual("rule.quiet",guide.Cue(session));
            level.noise=Array.Empty<int[]>();Assert.AreEqual("guide.adaptive.next",guide.Cue(session));
        }

        [Test]
        public void RemovingRestoringCompletingAndSkippingNeverLeavesAnObsoleteInstruction()
        {
            var guide=Guide();using var session=new CampSession(Simple());var poses=new[]{Pose("a",0,0),Pose("b",3,1)};
            session.Select("a");guide.ReportAction("select",session);
            session.Restore(new[]{poses[0]},"a");guide.ReportAction("commit",session);
            Assert.AreEqual("guide.rotate",guide.ActiveKey,"Reach the old optional rotation step before testing removal.");
            session.Restore(poses,"a");Assert.AreEqual("guide.adaptive.finish",guide.Cue(session));
            Assert.IsTrue(session.TryCommit(PlacementCommand.Remove("b"),out _));Assert.AreEqual("guide.adaptive.next",guide.Cue(session));
            Assert.IsTrue(session.TryCommit(PlacementCommand.Remove("a"),out _));Assert.AreEqual("guide.adaptive.start",guide.Cue(session));
            session.Restore(poses,"b");Assert.AreEqual("guide.adaptive.finish",guide.Cue(session));
            Assert.IsTrue(session.Check().IsSolved);Assert.IsNull(guide.Cue(session));
            guide.Skip();using var another=new CampSession(Simple());Assert.IsNull(guide.Cue(another));
        }

        [Test]
        public void ReservedEntrancesAndHardInvalidPosesAreNotMisreportedAsDoorProblems()
        {
            var level=Simple();level.accessPoints=new[]{new AccessPointData{id="gate",x=2,z=2}};
            foreach(var p in new[]{Pose("a",4,4),Pose("a",1,1)})
                Assert.AreEqual("rule.entry",PlacementGuidance.Explain(level,RuleEvaluator.Evaluate(level,new[]{p},false),"a").Key);
            foreach(int r in new[]{-1,4})
                Assert.AreEqual("rule.rotation",PlacementGuidance.Explain(level,RuleEvaluator.Evaluate(level,new[]{Pose("a",0,0,r)},false),"a").Key);
            Assert.AreEqual("rule.guest-id",PlacementGuidance.Explain(level,RuleEvaluator.Evaluate(level,new[]{Pose("unknown",0,0)},false)).Key);
        }
    }
}
