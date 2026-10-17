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
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace QuietCamp.Tests
{
    public sealed class ShadeIntroductionPlayModeTests
    {
        [TearDown] public void ClearFactory()=>QuietCampBootstrap.EditorSaveFactory=null;
        static IEnumerator LoadShade(bool reduced)
        {
            Assert.IsTrue(UnityEngine.Application.productName.Contains("QA"));
            IntroductionTapTestSupport.ResetRamBoot();
            QuietCampBootstrap.EditorSaveFactory=()=>
            {
                QuietCampBootstrap.EditorSaveFactory=null;
                var save=new SaveAdapter(memoryOnly:true);save.Settings.reducedMotion=reduced;return save;
            };
            yield return SceneManager.LoadSceneAsync("Boot");
            yield return PrivacyBootTestSupport.EnterGame();
            var services=QuietCampBootstrap.ServicesRef;Assert.IsTrue(services.Save.MemoryOnly);
            services.Tutorial.Skip();services.Progression.MarkCompleted("QC001");services.Progression.MarkCompleted("QC002");
            services.PendingLevelId="QC003";
            yield return SceneManager.LoadSceneAsync("Camp");
            float deadline=Time.realtimeSinceStartup+45;
            while(CampSceneHost.Current?.UiReady!=true&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.IsTrue(CampSceneHost.Current?.UiReady==true);
            Assert.AreEqual("QC003",CampSceneHost.Current.Session.Level.id);
        }
        static bool WorldAllowed()=>QuietCampBootstrap.ServicesRef.InputPolicy.CanProcess(
            GameplayInputKind.Placement,new Vector2(Screen.width*.5f,Screen.height*.5f));

        [Test] public void FreshShadeBoardIsIntroducedButResumedAndUnshadedBoardsAreNot()
        {
            using var shade=new CampSession(LevelLoader.Load("QC003"));
            Assert.IsTrue(TutorialDirector.ShouldIntroduceShade(shade));
            shade.DebugApplyWitness();Assert.IsFalse(TutorialDirector.ShouldIntroduceShade(shade));
            using var plain=new CampSession(LevelLoader.Load("QC001"));
            Assert.IsFalse(TutorialDirector.ShouldIntroduceShade(plain));
        }

        [UnityTest] public IEnumerator RevealsRealRuleCellsShowsOstapAndSymbolThenUnlocksPlacement()
        {
            yield return LoadShade(false);
            var host=CampSceneHost.Current;var services=QuietCampBootstrap.ServicesRef;
            Assert.IsTrue(host.IsIntroducingShade);Assert.IsFalse(WorldAllowed());Assert.IsFalse(host.GameplayActive);
            Assert.IsFalse(services.ContextStack.IsActionAllowedByContext(new UiActionId("qc.rotate")));
            services.Actions.Execute(new UiActionRequest(new UiActionId("qc.check"),UiActionSource.Programmatic,"Gameplay"));
            Assert.IsFalse(host.IsFinishingCamp);Assert.AreEqual(0,services.Economy.Attempts("QC003"));
            bool sawGuide=false,sawZone=false;float deadline=Time.realtimeSinceStartup+14;
            bool tapped=false;
            while(host.IsIntroducingShade&&Time.realtimeSinceStartup<deadline)
            {
                Assert.IsFalse(WorldAllowed());Assert.AreEqual(0,host.Session.State.Count);
                var surface=Object.FindObjectsByType<HtmlSurface>(FindObjectsSortMode.None).Single(s=>s.name=="CampHtml");
                if(surface.Element("shade-introduction")!=null)
                {
                    sawGuide=true;Assert.IsNotNull(surface.Element("shade-sign"));
                    Assert.IsNotNull(surface.Element("shade-pointer"));Assert.IsNotNull(surface.Element("guide-portrait"));
                    Assert.IsFalse(surface.GetComponent<CanvasGroup>().interactable);
                }
                var nodes=Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None).Where(r=>r.name.StartsWith("ShadeRule_"));
                if(nodes.Any())
                {
                    sawZone=true;
                    var expected=host.Session.Level.shade.Select(c=>BoardMath.CellCenterWorld(host.Session.Level,new Cell(c[0],c[1]))+Vector3.up*BoardMath.OverlayY).ToArray();
                    foreach(var node in nodes)Assert.IsTrue(expected.Any(p=>Vector3.Distance(p,node.transform.position)<.001f));
                }
                if(host.CanContinueIntroduction&&!tapped){tapped=true;yield return IntroductionTapTestSupport.Continue(host);}
                yield return null;
            }
            Assert.IsFalse(host.IsIntroducingShade);Assert.IsTrue(sawGuide);Assert.IsTrue(sawZone);Assert.IsTrue(WorldAllowed());
            Assert.Contains("shade",services.Tutorial.Save.explainedSigns);
            for(int i=0;i<3;i++)yield return null;
            Assert.IsNotNull(PrivacyBootTestSupport.Find("guest-card"));
            Assert.IsTrue(PrivacyBootTestSupport.Find("guest-card").IsInteractable());
            Assert.IsFalse(Object.FindObjectsByType<MeshRenderer>().Any(r=>r.name.StartsWith("ShadeRule_")));
        }

        [UnityTest] public IEnumerator ReducedMotionKeepsTheExplanationAndSceneExitReleasesItsInputLock()
        {
            yield return LoadShade(true);
            var host=CampSceneHost.Current;Assert.IsTrue(host.IsIntroducingShade);Assert.IsFalse(WorldAllowed());
            float deadline=Time.realtimeSinceStartup+4;HtmlSurface surface=null;
            do
            {
                surface=Object.FindObjectsByType<HtmlSurface>().Single(s=>s.name=="CampHtml");
                if(surface.Element("shade-introduction")!=null)break;
                yield return null;
            }while(Time.realtimeSinceStartup<deadline);
            Assert.IsNotNull(surface.Element("shade-introduction"));
            yield return new WaitForSecondsRealtime(.4f);
            Assert.IsTrue(host.IsIntroducingShade,"Reduced motion must retain reading time.");
            yield return SceneManager.LoadSceneAsync("MainMenu");
            for(int i=0;i<10;i++)yield return null;
            Assert.IsFalse(QuietCampBootstrap.ServicesRef.ContextStack.ActiveContexts.Any(c=>c.ContextId=="ShadeIntroduction"));
            Assert.IsTrue(QuietCampBootstrap.ServicesRef.InputPolicy.CanProcess(GameplayInputKind.PointerPan,new Vector2(10,10)));
        }
    }
}
