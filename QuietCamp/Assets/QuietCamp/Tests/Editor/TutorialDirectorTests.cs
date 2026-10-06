using System;
using System.Linq;
using Newtonsoft.Json;
using NUnit.Framework;
using QuietCamp.Application;
using QuietCamp.Domain;

namespace QuietCamp.Tests
{
    public sealed class TutorialDirectorTests
    {
        static LevelData Level(string id,bool shade=false,bool quiet=false,bool friends=false)
        {
            var guests=friends?new[]{new GuestData{id="a"},new GuestData{id="b"}}:new[]{new GuestData{id="a",shade=shade,quiet=quiet}};
            return new LevelData{id=id,ruleVersion=1,width=6,height=6,entry=new[]{0,0},blocked=Array.Empty<int[]>(),noise=Array.Empty<int[]>(),
                shade=Enumerable.Range(0,36).Select(i=>new[]{i%6,i/6}).ToArray(),guests=guests,
                friends=friends?new[]{new[]{"a","b"}}:Array.Empty<string[]>()};
        }
        static void Place(CampSession session,int x=1,int z=1,int rotation=0,string guest="a")
            =>Assert.IsTrue(session.TryCommit(PlacementCommand.Place(guest,x,z,rotation),out _));
        static void Complete(TutorialDirector tutorial,CampSession session)
        {Assert.IsTrue(session.Check().IsSolved);Assert.IsTrue(tutorial.ReportAction("complete",session));}
        static void First(TutorialDirector tutorial)
        {
            using var session=new CampSession(Level("QC001"));session.Select("a");Assert.IsTrue(tutorial.ReportAction("select",session));
            Place(session);Assert.IsTrue(tutorial.ReportAction("commit",session));Place(session,rotation:1);
            Assert.IsTrue(tutorial.ReportAction("rotate",session));Complete(tutorial,session);
        }
        static void Second(TutorialDirector tutorial)
        {
            using var session=new CampSession(Level("QC002"));Place(session);Place(session,2,1);
            Assert.IsTrue(tutorial.ReportAction("move",session));Assert.IsTrue(session.Undo());Assert.IsTrue(tutorial.ReportAction("undo",session));Complete(tutorial,session);
        }
        static void Rest(TutorialDirector tutorial)
        {
            using(var session=new CampSession(Level("QC003",shade:true))) {Place(session);Complete(tutorial,session);}
            using(var session=new CampSession(Level("QC004",quiet:true))) {Place(session);Complete(tutorial,session);}
            using(var session=new CampSession(Level("QC005",friends:true))) {Place(session);Place(session,3,1,guest:"b");Complete(tutorial,session);}
        }
        [Test] public void NewPlayersSeeMinimalUiAndExistingPlayersKeepTheirControls()
        {
            var fresh=new TutorialDirector(null,false,new ProgressionService(),()=>true);
            Assert.AreEqual("guide.select",fresh.Cue("QC001"));Assert.IsFalse(fresh.RoadmapUnlocked);Assert.IsFalse(fresh.AlbumUnlocked);Assert.IsFalse(fresh.HistoryUnlocked);
            var existing=new TutorialDirector(null,true,new ProgressionService(),()=>true);
            Assert.IsTrue(existing.AllControls);Assert.IsNull(existing.Cue("QC001"));Assert.IsFalse(existing.RewardOwned);
        }
        [Test] public void IntroductionStartsOnlyForANewPlayerAndSurvivesRestart()
        {
            var tutorial = new TutorialDirector(null, false, new ProgressionService(), () => true);
            Assert.IsTrue(tutorial.NeedsIntroduction);
            Assert.IsTrue(tutorial.BeginIntroduction());
            Assert.IsFalse(tutorial.NeedsIntroduction);
            Assert.AreEqual("guide.select", tutorial.Cue("QC001"));
            var restored = JsonConvert.DeserializeObject<TutorialSaveData>(JsonConvert.SerializeObject(tutorial.Save));
            Assert.IsFalse(new TutorialDirector(restored, true, new ProgressionService(), () => true).NeedsIntroduction);
            Assert.IsFalse(new TutorialDirector(null, true, new ProgressionService(), () => true).NeedsIntroduction);
        }
        [Test] public void SkippedAndStartedGuidesNeverForceAnotherIntroduction()
        {
            var tutorial = new TutorialDirector(null, false, new ProgressionService(), () => true);
            tutorial.Skip();
            Assert.IsFalse(tutorial.NeedsIntroduction);
            tutorial.LearnAgain();
            Assert.IsFalse(tutorial.NeedsIntroduction);
            var started = new TutorialDirector(null, false, new ProgressionService(), () => true);
            using var session = new CampSession(Level("QC001"));
            session.Select("a"); started.ReportAction("select", session);
            Assert.IsFalse(started.NeedsIntroduction);
        }
        [Test] public void FailedIntroductionSaveCanBeRetriedWithoutAwardingProgress()
        {
            bool persist = false;
            var tutorial = new TutorialDirector(null, false, new ProgressionService(), () => persist);
            Assert.IsFalse(tutorial.BeginIntroduction());
            Assert.IsTrue(tutorial.NeedsIntroduction);
            Assert.IsFalse(tutorial.RewardOwned);
            persist = true;
            Assert.IsTrue(tutorial.BeginIntroduction());
            Assert.IsEmpty(tutorial.Save.progress.completedSteps);
        }
        [Test] public void ACommittedTentWithoutAReachableEntranceCannotAdvanceTheGuide()
        {
            var tutorial=new TutorialDirector(null,false,new ProgressionService(),()=>true);using var session=new CampSession(Level("QC001"));
            session.Select("a");tutorial.ReportAction("select",session);Place(session,1,0,2);
            Assert.IsFalse(tutorial.ReportAction("commit",session));Assert.AreEqual("guide.place",tutorial.Cue("QC001"));
            Place(session);Assert.IsTrue(tutorial.ReportAction("commit",session));
        }
        [Test] public void SkipOpensAllControlsAndKeepsTheWelcomeGift()
        {
            var tutorial=new TutorialDirector(null,false,new ProgressionService(),()=>true);tutorial.Skip();
            Assert.IsTrue(tutorial.AllControls);Assert.IsTrue(tutorial.RewardOwned,"The pennant is a gift, not a pass reward.");
            using var session=new CampSession(Level("QC005",friends:true));Place(session);Place(session,3,1,guest:"b");session.Check();
            Assert.IsFalse(tutorial.ReportAction("complete",session));
            tutorial.LearnAgain();Assert.AreEqual("QC001",tutorial.CurrentLevelId);Assert.IsTrue(tutorial.AllControls);
            First(tutorial);Second(tutorial);Rest(tutorial);Assert.IsTrue(tutorial.Finished);
            Assert.AreEqual(1,tutorial.Save.progress.claimedRewards.Count(id=>id==TutorialDirector.RewardId),"Finishing after a skip must not duplicate the gift.");
        }
        [Test] public void NavigationUnlocksFromCompletedGladesWithoutDependingOnTheReward()
        {
            var progression=new ProgressionService();var tutorial=new TutorialDirector(null,false,progression,()=>true);
            progression.MarkCompleted("QC001");Assert.IsTrue(tutorial.RoadmapUnlocked);Assert.IsTrue(tutorial.HistoryUnlocked);Assert.IsFalse(tutorial.AlbumUnlocked);
            progression.MarkCompleted("QC002");Assert.IsTrue(tutorial.AlbumUnlocked);Assert.IsTrue(tutorial.HintsUnlocked);Assert.IsFalse(tutorial.RewardOwned);
        }
        [Test] public void EvaluatedStepsSurviveRestartAndGiveExactlyOnePennant()
        {
            var progression=new ProgressionService();int saves=0;var tutorial=new TutorialDirector(null,false,progression,()=>{saves++;return true;});
            First(tutorial);Second(tutorial);tutorial.Skip();var restored=JsonConvert.DeserializeObject<TutorialSaveData>(JsonConvert.SerializeObject(tutorial.Save));
            var resumed=new TutorialDirector(restored,true,progression,()=>{saves++;return true;});Assert.IsTrue(resumed.Skipped);resumed.LearnAgain();
            Assert.AreEqual("QC003",resumed.CurrentLevelId);Rest(resumed);Assert.IsTrue(resumed.Finished);Assert.IsTrue(resumed.RewardOwned);
            Assert.AreEqual(1,resumed.Save.progress.claimedRewards.Count(id=>id==TutorialDirector.RewardId));
            int before=saves;resumed.RetryReward();Assert.AreEqual(before,saves);Assert.AreEqual(TutorialDirector.PennantFlag,progression.CosmeticFlags);
        }
        [Test] public void FinishedGuidesCanBeReplayedWithoutDuplicatingTheGift()
        {
            var progression = new ProgressionService();
            var tutorial = new TutorialDirector(null, false, progression, () => true);
            First(tutorial); Second(tutorial); Rest(tutorial);
            Assert.IsTrue(tutorial.Finished); tutorial.LearnAgain();
            Assert.AreEqual("QC001", tutorial.CurrentLevelId);
            Assert.IsFalse(tutorial.NeedsIntroduction); Assert.IsTrue(tutorial.RewardOwned);
            First(tutorial); Second(tutorial); Rest(tutorial);
            Assert.AreEqual(1, tutorial.Save.progress.claimedRewards.Length);
            Assert.AreEqual(TutorialDirector.PennantFlag, progression.CosmeticFlags);
        }
        [Test] public void FailedSaveRollsBackTheGiftAndAllowsAnIdempotentRetry()
        {
            var progression=new ProgressionService();bool canSave=true;var tutorial=new TutorialDirector(null,false,progression,()=>canSave);
            First(tutorial);Second(tutorial);
            using(var session=new CampSession(Level("QC003",shade:true))) {Place(session);Complete(tutorial,session);}
            using(var session=new CampSession(Level("QC004",quiet:true))) {Place(session);Complete(tutorial,session);}
            canSave=false;using(var session=new CampSession(Level("QC005",friends:true))) {Place(session);Place(session,3,1,guest:"b");Complete(tutorial,session);}
            Assert.IsFalse(tutorial.RewardOwned);Assert.IsEmpty(tutorial.Save.progress.claimedRewards);
            canSave=true;tutorial.RetryReward();Assert.IsTrue(tutorial.RewardOwned);Assert.AreEqual(1,tutorial.Save.progress.claimedRewards.Length);
        }
        [Test] public void AValidWishBeforeTheRealCheckDoesNotCountAsACompletedStage()
        {
            var tutorial=new TutorialDirector(null,false,new ProgressionService(),()=>true);First(tutorial);Second(tutorial);
            using var session=new CampSession(Level("QC003",shade:true));Place(session);
            Assert.IsFalse(tutorial.ReportAction("complete",session));Assert.AreEqual("QC003",tutorial.CurrentLevelId);
            Complete(tutorial,session);Assert.AreEqual("QC004",tutorial.CurrentLevelId);
        }
        [Test] public void GuidedFirstTwoGladesRequireTheirActionsBeforeCheckButSkipAndLegacyDoNot()
        {
            var tutorial=new TutorialDirector(null,false,new ProgressionService(),()=>true);
            Assert.IsFalse(tutorial.CanCompleteLevel("QC001"));Assert.IsTrue(tutorial.CanCompleteLevel("QC003"));
            using(var first=new CampSession(Level("QC001")))
            {
                first.Select("a");tutorial.ReportAction("select",first);Place(first);tutorial.ReportAction("commit",first);
                Assert.IsFalse(tutorial.CanCompleteLevel("QC001"),"A valid layout cannot bypass learning rotation.");
                Place(first,rotation:1);tutorial.ReportAction("rotate",first);Assert.IsTrue(tutorial.CanCompleteLevel("QC001"));Complete(tutorial,first);
            }
            Assert.IsFalse(tutorial.CanCompleteLevel("QC002"));
            using(var second=new CampSession(Level("QC002")))
            {
                Place(second);Place(second,2,1);tutorial.ReportAction("move",second);
                Assert.IsFalse(tutorial.CanCompleteLevel("QC002"),"Moving alone cannot bypass learning undo.");
                second.Undo();tutorial.ReportAction("undo",second);Assert.IsTrue(tutorial.CanCompleteLevel("QC002"));
            }
            var skipped=new TutorialDirector(null,false,new ProgressionService(),()=>true);skipped.Skip();
            Assert.IsTrue(skipped.CanCompleteLevel("QC001"));Assert.IsTrue(skipped.CanCompleteLevel("QC002"));
            var legacy=new TutorialDirector(null,true,new ProgressionService(),()=>true);Assert.IsTrue(legacy.CanCompleteLevel("QC001"));
        }
        [Test] public void MenuIntroShowsOnceAfterLearningAndNeverForMigratedPlayers()
        {
            var fresh=new TutorialDirector(null,false,new ProgressionService(),()=>true);
            Assert.IsFalse(fresh.NeedsMenuIntro,"The intro waits until learning ends or is skipped.");
            fresh.Skip();Assert.IsTrue(fresh.NeedsMenuIntro);
            fresh.MarkMenuIntroSeen();Assert.IsFalse(fresh.NeedsMenuIntro);
            var restored=JsonConvert.DeserializeObject<TutorialSaveData>(JsonConvert.SerializeObject(fresh.Save));
            Assert.IsFalse(new TutorialDirector(restored,false,new ProgressionService(),()=>true).NeedsMenuIntro);
            var legacy=new TutorialDirector(null,true,new ProgressionService(),()=>true);legacy.Skip();
            Assert.IsFalse(legacy.NeedsMenuIntro,"Migrated saves already know the menu.");
        }
        [Test] public void OngoingGuideTeachesANewSignOnceAndRemembersIt()
        {
            var tutorial=new TutorialDirector(null,false,new ProgressionService(),()=>true);tutorial.Skip();
            var shady=Level("QC006",shade:true);
            Assert.AreEqual("shade",tutorial.PendingSign(shady),"A first-seen sign is taught right away.");
            tutorial.ExplainSign("shade");
            Assert.IsNull(tutorial.PendingSign(shady));Assert.IsNull(tutorial.PendingSign(Level("QC007",shade:true)));
            Assert.AreEqual("quiet",tutorial.PendingSign(Level("QC008",quiet:true)),"Other unseen signs still wait for their level.");
            var restored=JsonConvert.DeserializeObject<TutorialSaveData>(JsonConvert.SerializeObject(tutorial.Save));
            Assert.IsNull(new TutorialDirector(restored,false,new ProgressionService(),()=>true).PendingSign(shady),"Explained signs persist across restarts.");
            Assert.IsNull(new TutorialDirector(null,true,new ProgressionService(),()=>true).PendingSign(shady),"Migrated players get no sign popups.");
        }
        [Test] public void GuidedCompletionTeachesThatLevelsSigns()
        {
            var tutorial=new TutorialDirector(null,false,new ProgressionService(),()=>true);
            First(tutorial);Second(tutorial);
            Assert.IsNull(tutorial.PendingSign(Level("QC003",shade:true)),"Guided glades teach through steps, not popups.");
            using(var session=new CampSession(Level("QC003",shade:true))) {Place(session);Complete(tutorial,session);}
            tutorial.Skip();
            Assert.IsNull(tutorial.PendingSign(Level("QC009",shade:true)),"Shade was already taught by the guided glade.");
            Assert.AreEqual("quiet",tutorial.PendingSign(Level("QC009",quiet:true)));
        }
        [Test] public void MenuIntroDismissalRollsBackWhenTheSaveFails()
        {
            var persist=false;var tutorial=new TutorialDirector(null,false,new ProgressionService(),()=>persist);
            tutorial.Skip();Assert.IsTrue(tutorial.NeedsMenuIntro);
            tutorial.MarkMenuIntroSeen();Assert.IsTrue(tutorial.NeedsMenuIntro,"A failed save must re-show the intro.");
            persist=true;tutorial.MarkMenuIntroSeen();Assert.IsFalse(tutorial.NeedsMenuIntro);
        }
    }
}
