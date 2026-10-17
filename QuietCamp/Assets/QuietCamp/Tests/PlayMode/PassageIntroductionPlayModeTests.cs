using System.Collections;
using System.Linq;
using Kruty1918.InputRouting.API;
using Kruty1918.UIActions.API;
using NUnit.Framework;
using QuietCamp.Application;
using QuietCamp.Domain;
using QuietCamp.Infrastructure;
using QuietCamp.Presentation;
using QuietCamp.Presentation.UI;
using QuietCamp.Presentation.World;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace QuietCamp.Tests
{
    public sealed class PassageIntroductionPlayModeTests
    {
        InputSettings.BackgroundBehavior _background;
        InputSettings.EditorInputBehaviorInPlayMode _editorInput;
        [SetUp] public void InputSetup()
        {
            _background=InputSystem.settings.backgroundBehavior;_editorInput=InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        }
        [TearDown] public void Cleanup()
        {
            QuietCampBootstrap.EditorSaveFactory=null;
            InputSystem.settings.backgroundBehavior=_background;InputSystem.settings.editorInputBehaviorInPlayMode=_editorInput;
        }
        [TestCase(0),TestCase(1),TestCase(2),TestCase(3)]
        public void EvidenceFollowsEveryRealDoorAndDetectsABlockedPassage(int rotation)
        {
            var level=LevelLoader.Load("QC002");using var session=new CampSession(level);
            Assert.IsTrue(session.TryCommit(PlacementCommand.Place("g1",1,1,rotation),out _));session.Select("g1");
            var route=TutorialDirector.PassageIntroductionRoute(session);var door=RuleEvaluator.Door(session.State.Find("g1"));
            Assert.IsTrue(route.Reachable);Assert.AreEqual(door,route.Goal);
            Assert.AreEqual(new Cell(level.entry[0],level.entry[1]),route.Start);
            CollectionAssert.AreEqual(CampWalkability.Path(level,RuleEvaluator.Footprint(session.State.Find("g1")).ToHashSet(),route.Start,door),route.Cells);
            level.blocked=new[]{new[]{door.X,door.Z}};
            route=TutorialDirector.PassageIntroductionRoute(session);
            Assert.IsFalse(route.Reachable);Assert.AreEqual(RouteFailureReason.Blocked,route.Reason);Assert.AreEqual(door,route.Goal);
        }
        [Test] public void StartsAfterARealTentAndPersistsOnlyACompletedLesson()
        {
            using var session=new CampSession(LevelLoader.Load("QC002"));
            var guide=new TutorialDirector(null,false,new ProgressionService(),()=>true);
            Assert.IsFalse(guide.NeedsPassageDemonstration(session));
            session.TryCommit(PlacementCommand.Place("g1",0,0,0),out _);
            Assert.IsTrue(guide.NeedsPassageDemonstration(session));
            guide.MarkPassageDemonstrationSeen();Assert.IsFalse(guide.NeedsPassageDemonstration(session));
            guide.LearnAgain();Assert.IsTrue(guide.NeedsPassageDemonstration(session));
            guide.Skip();Assert.IsFalse(guide.NeedsPassageDemonstration(session));
            var data=new TutorialSaveData();var failure=new TutorialDirector(data,false,new ProgressionService(),()=>false);
            Assert.IsFalse(failure.MarkPassageDemonstrationSeen());Assert.IsFalse(data.passageDemonstrationSeen);
        }
        static IEnumerator LoadCamp(bool reduced)
        {
            Assert.IsTrue(UnityEngine.Application.productName.Contains("QA"));
            IntroductionTapTestSupport.ResetRamBoot();
            QuietCampBootstrap.EditorSaveFactory=()=>
            {QuietCampBootstrap.EditorSaveFactory=null;var save=new SaveAdapter(memoryOnly:true);save.Settings.reducedMotion=reduced;return save;};
            yield return SceneManager.LoadSceneAsync("Boot");yield return PrivacyBootTestSupport.EnterGame();
            var services=QuietCampBootstrap.ServicesRef;Assert.IsTrue(services.Save.MemoryOnly);
            services.Progression.MarkCompleted("QC001");services.PendingLevelId="QC002";
            yield return SceneManager.LoadSceneAsync("Camp");float deadline=Time.realtimeSinceStartup+40;
            while(CampSceneHost.Current?.UiReady!=true&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.IsTrue(CampSceneHost.Current?.UiReady==true);Assert.IsFalse(CampSceneHost.Current.IsIntroducingPassage);
            for(int i=0;i<4;i++)yield return null;
        }
        static IEnumerator TapSite(int x,int z)
        {
            var touch=InputSystem.AddDevice<Touchscreen>();
            try
            {
                var at=(Vector2)Camera.main.WorldToScreenPoint(BoardMath.CellCenter(CampSceneHost.Current.Session.Level,x,z));
                InputSystem.QueueStateEvent(touch,new TouchState{touchId=92,phase=UnityEngine.InputSystem.TouchPhase.Began,position=at});yield return null;
                InputSystem.QueueStateEvent(touch,new TouchState{touchId=92,phase=UnityEngine.InputSystem.TouchPhase.Ended,position=at});yield return null;
            }
            finally{InputSystem.RemoveDevice(touch);}
        }
        [UnityTest] public IEnumerator HighlightsThePlayersOwnRouteThenReturnsTheUnchangedTentAndInput()
        {
            yield return LoadCamp(false);yield return TapSite(0,0);
            var host=CampSceneHost.Current;var services=QuietCampBootstrap.ServicesRef;var route=TutorialDirector.PassageIntroductionRoute(host.Session);
            Assert.IsTrue(host.IsIntroducingPassage);Assert.IsTrue(route.Reachable);int revision=host.Session.State.Revision;
            services.Actions.Execute(new UiActionRequest(new UiActionId("qc.check"),UiActionSource.Button,"Gameplay"));
            Assert.IsFalse(host.IsFinishingCamp);Assert.AreEqual(0,services.Economy.Attempts("QC002"));
            bool sawGuide=false,sawRoute=false;float deadline=Time.realtimeSinceStartup+14;
            bool tapped=false;
            while(host.IsIntroducingPassage&&Time.realtimeSinceStartup<deadline)
            {
                Assert.AreEqual(revision,host.Session.State.Revision);Assert.AreEqual(1,host.Session.State.Count);
                Assert.IsFalse(services.InputPolicy.CanProcess(GameplayInputKind.Placement,new Vector2(Screen.width*.5f,Screen.height*.5f)));
                var surface=Object.FindObjectsByType<HtmlSurface>().Single(s=>s.name=="CampHtml");
                if(surface.Element("path-introduction")!=null)
                {sawGuide=true;Assert.IsNotNull(surface.Element("path-sign"));Assert.IsNotNull(surface.Element("path-pointer"));}
                var nodes=Object.FindObjectsByType<MeshRenderer>().Where(r=>r.name.StartsWith("PathOverlay_")).ToArray();
                if(nodes.Length>0)
                {
                    sawRoute=true;Assert.AreEqual(route.Cells.Length,nodes.Length);
                    foreach(var node in nodes)Assert.IsTrue(route.Cells.Any(c=>Vector3.Distance(node.transform.position,BoardMath.CellCenterWorld(host.Session.Level,c)+Vector3.up*BoardMath.OverlayY)<.001f));
                }
                if(host.CanContinueIntroduction&&!tapped){tapped=true;yield return IntroductionTapTestSupport.Continue(host);}
                yield return null;
            }
            Assert.IsFalse(host.IsIntroducingPassage);Assert.IsTrue(sawGuide);Assert.IsTrue(sawRoute);
            Assert.IsTrue(services.Tutorial.Save.passageDemonstrationSeen);Assert.AreEqual(revision,host.Session.State.Revision);
            for(int i=0;i<4;i++)yield return null;
            Assert.IsTrue(PrivacyBootTestSupport.Find("pause").IsInteractable());
            Assert.IsFalse(Object.FindObjectsByType<MeshRenderer>().Any(r=>r.name.StartsWith("PathOverlay_")));
        }
        [UnityTest] public IEnumerator InvalidDoorIsExplainedAndAnInterruptedReducedMotionLessonReleasesItsLock()
        {
            yield return LoadCamp(true);
            var host=CampSceneHost.Current;
            Assert.IsTrue(host.Session.TryCommit(PlacementCommand.Place("g1",0,0,1),out _));
            var placement=Object.FindAnyObjectByType<PlacementController>();
            Assert.IsTrue(placement.Rotate());
            var route=TutorialDirector.PassageIntroductionRoute(host.Session);
            var guide=QuietCampBootstrap.ServicesRef.Tutorial;
            Assert.IsTrue(host.IsIntroducingPassage,$"needs={guide.NeedsPassageDemonstration(host.Session)} count={host.Session.State.Count} existing={guide.Save.existingPlayer} skipped={guide.Skipped} finished={guide.Finished} seen={guide.Save.passageDemonstrationSeen} ready={host.UiReady} finishing={host.IsFinishingCamp}");
            Assert.IsFalse(route.Reachable);Assert.AreEqual(RouteFailureReason.OutsideTrail,route.Reason);
            float deadline=Time.realtimeSinceStartup+3;HtmlSurface surface=null;
            do
            {
                surface=Object.FindObjectsByType<HtmlSurface>().Single(s=>s.name=="CampHtml");
                if(surface.Element("path-introduction")!=null)break;
                yield return null;
            }while(Time.realtimeSinceStartup<deadline);
            Assert.IsNotNull(surface.Element("path-introduction"));
            Assert.IsFalse(QuietCampBootstrap.ServicesRef.Tutorial.Save.passageDemonstrationSeen);
            yield return SceneManager.LoadSceneAsync("MainMenu");for(int i=0;i<10;i++)yield return null;
            Assert.IsFalse(QuietCampBootstrap.ServicesRef.ContextStack.ActiveContexts.Any(c=>c.ContextId=="PassageIntroduction"));
            Assert.IsTrue(QuietCampBootstrap.ServicesRef.InputPolicy.CanProcess(GameplayInputKind.PointerPan,new Vector2(10,10)));
        }
    }
}
