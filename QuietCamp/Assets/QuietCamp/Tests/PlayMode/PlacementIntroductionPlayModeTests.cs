using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Kruty1918.InputRouting.API;
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
    public sealed class PlacementIntroductionPlayModeTests
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
        static TutorialDirector Guide(TutorialSaveData data=null,bool existing=false,System.Func<bool> persist=null)
            =>new TutorialDirector(data,existing,new ProgressionService(),persist??(()=>true));

        [Test] public void DemonstrationIsOncePerGuideAndNeverInterruptsResumedOrSkippedPlay()
        {
            var guide=Guide();using var session=new CampSession(LevelLoader.Load("QC001"));
            Assert.IsTrue(guide.NeedsPlacementDemonstration(session));
            Assert.IsTrue(session.TryCommit(PlacementCommand.Place("g1",0,0,0),out _));
            Assert.IsFalse(guide.NeedsPlacementDemonstration(session));
            session.Undo();Assert.IsTrue(guide.NeedsPlacementDemonstration(session));
            guide.MarkPlacementDemonstrationSeen();Assert.IsFalse(guide.NeedsPlacementDemonstration(session));
            guide.LearnAgain();Assert.IsTrue(guide.NeedsPlacementDemonstration(session));
            guide.Skip();Assert.IsFalse(guide.NeedsPlacementDemonstration(session));
            Assert.IsFalse(Guide(existing:true).NeedsPlacementDemonstration(session));
            using var other=new CampSession(LevelLoader.Load("QC003"));Assert.IsFalse(Guide().NeedsPlacementDemonstration(other));
        }
        [Test] public void FailedPersistenceDoesNotMarkTheDemonstrationSeen()
        {
            var save=new TutorialSaveData();var guide=Guide(save,persist:()=>false);
            Assert.IsFalse(guide.MarkPlacementDemonstrationSeen());Assert.IsFalse(save.placementDemonstrationSeen);
        }

        [UnityTest] public IEnumerator FirstCampShowsTheGestureWithoutPlacingThenAcceptsThePlayersOwnDrag()
        {
            Assert.IsTrue(UnityEngine.Application.productName.Contains("QA"));
            IntroductionTapTestSupport.ResetRamBoot();
            QuietCampBootstrap.EditorSaveFactory=()=>{QuietCampBootstrap.EditorSaveFactory=null;return new SaveAdapter(memoryOnly:true);};
            yield return SceneManager.LoadSceneAsync("Boot");yield return PrivacyBootTestSupport.EnterGame();
            var services=QuietCampBootstrap.ServicesRef;services.PendingLevelId="QC001";
            yield return SceneManager.LoadSceneAsync("Camp");
            float deadline=Time.realtimeSinceStartup+40;
            while(CampSceneHost.Current?.UiReady!=true&&Time.realtimeSinceStartup<deadline)yield return null;
            var host=CampSceneHost.Current;Assert.IsTrue(host?.UiReady==true);Assert.IsTrue(host.IsIntroducingPlacement);
            double readyAt=-1;bool tapped=false;
            bool sawGuide=false,sawDemo=false,landed=false;var positions=new HashSet<int>();deadline=Time.realtimeSinceStartup+14;
            while(host.IsIntroducingPlacement&&Time.realtimeSinceStartup<deadline)
            {
                Assert.AreEqual(0,host.Session.State.Count);Assert.IsFalse(host.Session.CanUndo);
                Assert.IsFalse(services.InputPolicy.CanProcess(GameplayInputKind.Placement,new Vector2(Screen.width*.5f,Screen.height*.5f)));
                var surface=Object.FindObjectsByType<HtmlSurface>().Single(s=>s.name=="CampHtml");
                sawGuide|=surface.Element("placement-introduction")!=null;
                if(host.CanContinueIntroduction&&!tapped)
                {
                    if(readyAt<0)readyAt=Time.unscaledTimeAsDouble;
                    if(Time.unscaledTimeAsDouble-readyAt>1.4)
                    {tapped=true;yield return IntroductionTapTestSupport.Continue(host);}
                }
                var demo=surface.Element("placement-demo");
                if(demo!=null){sawDemo=true;positions.Add(Mathf.RoundToInt(demo.position.x));}
                if(demo!=null&&host.PlacementDemonstrationStep==2)
                {
                    var cells=Object.FindObjectsByType<MeshRenderer>().Where(r=>r.name.StartsWith("HintOverlay_")).ToArray();
                    Assert.Greater(cells.Length,0);
                    var target=cells.Aggregate(Vector3.zero,(sum,c)=>sum+c.transform.position)/cells.Length;
                    var screen=(Vector2)Camera.main.WorldToScreenPoint(target);
                    var actual=RectTransformUtility.WorldToScreenPoint(null,demo.TransformPoint(demo.rect.center));
                    landed|=Vector2.Distance(actual,screen)<6;
                }
                yield return null;
            }
            Assert.IsFalse(host.IsIntroducingPlacement);Assert.IsTrue(sawGuide);Assert.IsTrue(sawDemo);Assert.Greater(positions.Count,2);
            Assert.IsTrue(landed,"The rendered pointer must actually reach the highlighted destination.");
            Assert.IsTrue(tapped,"A lesson must remain until an explicit fresh tap.");
            Assert.IsTrue(services.Tutorial.Save.placementDemonstrationSeen);
            for(int i=0;i<3;i++)yield return null;
            Assert.IsTrue(PrivacyBootTestSupport.Find("guest-card").IsInteractable());
            var controller=Object.FindAnyObjectByType<PlacementController>();
            var touch=InputSystem.AddDevice<Touchscreen>();
            try
            {
                var start=(Vector2)Camera.main.WorldToScreenPoint(BoardMath.CellCenter(host.Session.Level,0,0));
                InputSystem.QueueStateEvent(touch,new TouchState{touchId=81,phase=UnityEngine.InputSystem.TouchPhase.Began,position=start});
                yield return new WaitForSecondsRealtime(.25f);
                Assert.IsTrue(controller.HasPreview);Assert.Greater(controller.TouchLiftPixels,0);
                var target=(Vector2)Camera.main.WorldToScreenPoint(BoardMath.CellCenter(host.Session.Level,1,0))-Vector2.up*controller.TouchLiftPixels;
                InputSystem.QueueStateEvent(touch,new TouchState{touchId=81,phase=UnityEngine.InputSystem.TouchPhase.Ended,position=target});
                yield return null;
                Assert.AreEqual(1,host.Session.State.Count);Assert.AreEqual(1,host.Session.State.Find("g1").x);Assert.AreEqual(0,host.Session.State.Find("g1").z);
                Assert.AreEqual("guide.adaptive.next",services.Tutorial.Cue(host.Session));
            }
            finally{controller.Cancel();InputSystem.RemoveDevice(touch);}
        }
    }
}
