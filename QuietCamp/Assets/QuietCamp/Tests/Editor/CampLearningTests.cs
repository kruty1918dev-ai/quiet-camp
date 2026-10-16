using System;
using System.Linq;
using Newtonsoft.Json;
using NUnit.Framework;
using QuietCamp.Application;
using QuietCamp.Domain;
using QuietCamp.Infrastructure;
using QuietCamp.Presentation.UI;
using QuietCamp.Presentation.World;
using UnityEngine;

namespace QuietCamp.Tests
{
    public sealed class CampLearningTests
    {
        static LearningCurriculum Curriculum()=>LearningCurriculum.Read(Resources.Load<TextAsset>("QuietCamp/learning_curriculum").text);
        static LevelData Simple(string id)=>new LevelData{id=id,width=6,height=6,ruleVersion=1,entry=new[]{0,0},
            blocked=Array.Empty<int[]>(),shade=Array.Empty<int[]>(),noise=Array.Empty<int[]>(),friends=Array.Empty<string[]>(),guests=new[]{new GuestData{id="a"}}};
        static void Place(CampSession s,int x=1,int rotation=0)
        {Assert.IsTrue(s.TryCommit(PlacementCommand.Place("a",x,1,rotation),out _));}
        [Test] public void TwentyLessonsFollowTheActualCampaignAndNeverInvalidateWitnesses()
        {
            var c=Curriculum();CollectionAssert.AreEqual(LevelLoader.CampaignLevelIds().Take(20),c.lessons.Select(l=>l.levelId));
            foreach(var lesson in c.lessons)
            {
                var source=LevelLoader.Load(lesson.levelId);
                var level=JsonConvert.DeserializeObject<LevelData>(JsonConvert.SerializeObject(source));
                var witness=JsonConvert.SerializeObject(level.witness);c.Pace(level);
                Assert.IsEmpty(LevelContentValidator.Validate(level),level.id);
                Assert.IsTrue(RuleEvaluator.Evaluate(level,level.witness).IsSolved,level.id);
                Assert.AreEqual(witness,JsonConvert.SerializeObject(level.witness));
                var once=JsonConvert.SerializeObject(level);c.Pace(level);Assert.AreEqual(once,JsonConvert.SerializeObject(level),"Pacing must be idempotent.");
                int number=Array.IndexOf(c.lessons,lesson)+1;
                if(number<c.quietFrom)Assert.IsFalse(level.guests.Any(g=>g.quiet));
                if(number<c.friendsFrom)Assert.IsEmpty(level.friends);
                if(number<c.accessFrom)Assert.IsEmpty(level.accessPoints);
            }
        }
        [Test] public void FreshGuideProgressesThroughTwentyLevelsAndRestoresGeneratedIds()
        {
            var c=Curriculum();var progress=new ProgressionService();
            var guide=new TutorialDirector(null,false,progress,()=>true,c);
            for(int i=0;i<20;i++)
            {
                string id=c.lessons[i].levelId;Assert.AreEqual(id,guide.CurrentLevelId);
                using var s=new CampSession(Simple(id));s.Select("a");guide.ReportAction("select",s);
                Place(s);guide.ReportAction("commit",s);
                if(i==0){Place(s,rotation:1);Assert.IsTrue(guide.ReportAction("rotate",s));}
                if(i==1){Place(s,2);guide.ReportAction("move",s);s.Undo();guide.ReportAction("undo",s);}
                Assert.IsTrue(guide.CanCompleteLevel(id));s.Check();Assert.IsTrue(guide.ReportAction("complete",s));
                if(i==11)
                {
                    var saved=JsonConvert.DeserializeObject<TutorialSaveData>(JsonConvert.SerializeObject(guide.Save));
                    guide=new TutorialDirector(saved,true,progress,()=>true,c);
                    Assert.AreEqual(c.lessons[12].levelId,guide.CurrentLevelId);
                }
                Assert.AreEqual(i==19,guide.Finished);
            }
            Assert.AreEqual(1,guide.Save.progress.claimedRewards.Count(id=>id==TutorialDirector.RewardId));
        }
        [Test] public void LegacyGuideKeepsItsInProgressStepInsteadOfRestartingLearning()
        {
            var saved=new TutorialSaveData{initialized=true};
            var guide=new TutorialDirector(saved,false,new ProgressionService(),()=>true,Curriculum());
            Assert.AreEqual(2,guide.Save.curriculumVersion);
            Assert.AreEqual("guide.select",guide.Cue("QC001"));
        }
        [Test] public void MigrationKeepsLearnedActionsAndMovesToTheCorrectPacedLesson()
        {
            var save=new TutorialSaveData{initialized=true};
            save.progress.flowId="quietcamp.first-camps";save.progress.version=1;
            save.progress.completedSteps=new[]{"QC001.select","QC001.place","QC001.rotate","QC001.first-check",
                "QC002.move","QC002.undo","QC002.shared-route","QC003.shade"};
            var guide=new TutorialDirector(save,false,new ProgressionService(),()=>true,Curriculum());
            Assert.AreEqual("QC004",guide.CurrentLevelId);Assert.AreEqual("guide.practice.place",guide.Cue("QC004"));
        }
        [Test] public void ReplayingAFinishedLegacyGuideUpgradesToTheTwentyCampCurriculum()
        {
            var save=new TutorialSaveData{initialized=true};save.progress.flowId="quietcamp.first-camps";save.progress.version=1;
            save.progress.completedSteps=new[]{"QC001.select","QC001.place","QC001.rotate","QC001.first-check",
                "QC002.move","QC002.undo","QC002.shared-route","QC003.shade","QC004.quiet","QC005.friends"};
            var guide=new TutorialDirector(save,true,new ProgressionService(),()=>true,Curriculum());Assert.IsTrue(guide.Finished);
            guide.LearnAgain();Assert.IsFalse(guide.Finished);Assert.AreEqual(2,guide.Save.curriculumVersion);
            Assert.AreEqual("QC001",guide.CurrentLevelId);
        }
        [TestCase(0)][TestCase(1)][TestCase(2)][TestCase(3)]
        public void PlacementSitesMatchCanonicalEvaluationIncludingTheOldFootprint(int rotation)
        {
            using var s=new CampSession(LevelLoader.Load("QC010"));s.Restore(s.Level.witness,s.Level.guests[0].id);
            var sites=PlacementGuidance.Sites(s,s.SelectedGuestId,rotation);
            Assert.AreEqual((s.Level.width-1)*(s.Level.height-1),sites.Length);
            foreach(var site in sites)
            {
                var report=s.Preview(PlacementCommand.Place(s.SelectedGuestId,site.X,site.Z,rotation));
                var expected=!report.CanCommit?PlacementGuidance.Fit.Blocked:report.Issues.Count>0?PlacementGuidance.Fit.UnmetWish:PlacementGuidance.Fit.Ready;
                Assert.AreEqual(expected,site.Status);
            }
            Assert.IsTrue(sites.Any(p=>p.Status==PlacementGuidance.Fit.Blocked));
        }
        [Test] public void EveryTeachingWitnessOffersAGreenSiteForEachUnplacedGuest()
        {
            foreach(var lesson in Curriculum().lessons)
            {
                using var session=new CampSession(LevelLoader.Load(lesson.levelId));
                foreach(var pose in session.Level.witness)
                {
                    var site=PlacementGuidance.Sites(session,pose.guestId,pose.rotation).Single(s=>s.X==pose.x&&s.Z==pose.z);
                    Assert.AreEqual(PlacementGuidance.Fit.Ready,site.Status,lesson.levelId+"/"+pose.guestId);
                }
            }
        }
        [Test] public void ThirdFailureGivesAFreeHintWithoutSpendingTheHintInventory()
        {
            var economy=new CampEconomy(new EconomySaveData(),new EconomyRules(),()=>true);
            for(int i=0;i<3;i++)
            {
                Assert.AreEqual(EconomyResult.Applied,economy.RecordCheck("QC001",false));
                Assert.AreEqual(i==2,economy.RecoveryHintPending("QC001"));
            }
            Assert.AreEqual(2,economy.Lives);Assert.AreEqual(3,economy.Hints);
            Assert.AreEqual(EconomyResult.Applied,economy.UseRecoveryHint("QC001"));
            Assert.AreEqual(EconomyResult.NoHints,economy.UseRecoveryHint("QC001"));
            Assert.AreEqual(3,economy.Hints);
            economy.RecordCheck("QC001",true);Assert.IsFalse(economy.RecoveryHintPending("QC001"));
        }
        [Test] public void LearningRetriesAreFreeAndFailedWritesDoNotAdvanceTheCounter()
        {
            bool persist=true;var economy=new CampEconomy(new EconomySaveData(),new EconomyRules(),()=>persist);
            economy.RecordCheck("QC003",false,true);economy.RecordCheck("QC003",false,true);
            persist=false;Assert.AreEqual(EconomyResult.SaveFailed,economy.RecordCheck("QC003",false,true));
            Assert.IsFalse(economy.RecoveryHintPending("QC003"));Assert.AreEqual(5,economy.Lives);
            persist=true;economy.RecordCheck("QC003",false,true);Assert.IsTrue(economy.RecoveryHintPending("QC003"));
        }
        [Test] public void CompletingThenShowingCareDoesNotReopenTheTent()
        {
            var go=new GameObject("Completion tent");
            try
            {
                TentStoryVisual.Attach(go,Simple("QC001"),"a",()=>false);
                var story=go.GetComponent<TentStoryVisual>();story.Close(true);
                Assert.AreEqual(1,story.Closure);story.ShowCare();Assert.IsTrue(story.Cared);
                Assert.AreEqual(1,story.Closure);story.ResetCare();story.ShowCare();Assert.AreEqual(1,story.Closure);
            }
            finally{UnityEngine.Object.DestroyImmediate(go);}
        }
        [Test] public void QuietShadeAndFriendDoorDistanceChangeRecommendedSites()
        {
            var level=Simple("test");level.guests[0].shade=level.guests[0].quiet=true;
            level.shade=Enumerable.Range(0,24).Select(i=>new[]{i%4,i/4}).ToArray();level.noise=new[]{new[]{5,5}};
            using(var s=new CampSession(level))
            {
                var sites=PlacementGuidance.Sites(s,"a",0);
                Assert.AreEqual(PlacementGuidance.Fit.Ready,sites.Single(p=>p.X==1&&p.Z==1).Status);
                Assert.AreEqual(PlacementGuidance.Fit.UnmetWish,sites.Single(p=>p.X==3&&p.Z==3).Status);
            }
            level=Simple("friends");level.guests=new[]{new GuestData{id="a"},new GuestData{id="b"}};level.friends=new[]{new[]{"a","b"}};
            using(var s=new CampSession(level))
            {
                Assert.IsTrue(s.TryCommit(PlacementCommand.Place("b",3,1,2),out _));
                Assert.AreEqual(PlacementGuidance.Fit.Ready,PlacementGuidance.Sites(s,"a",2).Single(p=>p.X==1&&p.Z==1).Status);
                Assert.AreEqual(PlacementGuidance.Fit.UnmetWish,PlacementGuidance.Sites(s,"a",0).Single(p=>p.X==1&&p.Z==1).Status);
            }
        }
        [Test] public void PointerHoldAndReleaseChangeTheActualCardGraphic()
        {
            var go=new GameObject("Guest card",typeof(RectTransform),typeof(UnityEngine.UI.Image),typeof(UnityEngine.UI.Button));
            var host=new GameObject("Placement");
            try
            {
                var button=go.GetComponent<UnityEngine.UI.Button>();button.targetGraphic=go.GetComponent<UnityEngine.UI.Image>();
                var input=go.AddComponent<GuestCardInput>();input.Configure(host.AddComponent<PlacementController>(),"a");
                var pointer=new UnityEngine.EventSystems.PointerEventData(null){pointerId=-1};
                Assert.AreEqual(Color.white,button.targetGraphic.color);input.OnPointerDown(pointer);
                Assert.Less(button.targetGraphic.color.grayscale,.7f);input.OnPointerUp(pointer);
                Assert.AreEqual(Color.white,button.targetGraphic.color);
            }
            finally{UnityEngine.Object.DestroyImmediate(go);UnityEngine.Object.DestroyImmediate(host);}
        }
    }
}
