using System.Collections;
using System.Linq;
using Kruty1918.InputRouting.API;
using Kruty1918.UIActions.API;
using NUnit.Framework;
using QuietCamp.Infrastructure;
using QuietCamp.Presentation;
using QuietCamp.Presentation.World;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace QuietCamp.Tests
{
    public sealed class CampFinishPlayModeTests
    {
        [TearDown] public void ClearFactory() => QuietCampBootstrap.EditorSaveFactory=null;

        static IEnumerator LoadCamp(bool reduced=false)
        {
            Assert.IsTrue(UnityEngine.Application.productName.Contains("QA"));
            IntroductionTapTestSupport.ResetRamBoot();
            QuietCampBootstrap.EditorSaveFactory=()=>
            {
                QuietCampBootstrap.EditorSaveFactory=null;
                var save=new SaveAdapter(memoryOnly:true); save.Settings.reducedMotion=reduced; return save;
            };
            yield return SceneManager.LoadSceneAsync("Boot");
            yield return PrivacyBootTestSupport.EnterGame();
            var services=QuietCampBootstrap.ServicesRef;
            Assert.IsTrue(services.Save.MemoryOnly);
            services.Tutorial.Skip(); services.PendingLevelId="QC001";
            yield return SceneManager.LoadSceneAsync("Camp");
            float deadline=Time.realtimeSinceStartup+45;
            while(CampSceneHost.Current?.UiReady!=true&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.IsTrue(CampSceneHost.Current?.UiReady==true);
            for(int i=0;i<5;i++)yield return null;
        }
        static void Check()=>QuietCampBootstrap.ServicesRef.Actions.Execute(
            new UiActionRequest(new UiActionId("qc.check"),UiActionSource.Button,"Gameplay"));
        static CanvasGroup[] Groups()=>Object.FindObjectsByType<CanvasGroup>(FindObjectsSortMode.None)
            .Where(g=>g.name=="CampHtml"||g.name=="CampOverlay").ToArray();
        static bool WorldAllowed()=>QuietCampBootstrap.ServicesRef.InputPolicy.CanProcess(
            GameplayInputKind.Placement,new Vector2(Screen.width*.5f,Screen.height*.5f));

        [UnityTest] public IEnumerator FailedCheckLocksImmediatelyIgnoresDuplicatesAndRestoresAfterFade()
        {
            yield return LoadCamp();
            var host=CampSceneHost.Current; var services=QuietCampBootstrap.ServicesRef;
            int lives=services.Economy.Lives;
            Check();
            Assert.IsTrue(host.IsFinishingCamp); Assert.IsFalse(host.GameplayActive); Assert.IsFalse(WorldAllowed());
            var groups=Groups(); Assert.AreEqual(2,groups.Length);
            Assert.IsTrue(groups.All(g=>!g.interactable));
            var pause=PrivacyBootTestSupport.Find("pause"); Assert.IsNotNull(pause); Assert.IsFalse(pause.IsInteractable());
            Check();Check();
            Assert.AreEqual(lives-1,services.Economy.Lives,"Repeated checks cannot consume more lives during the animation.");
            bool fading=false; float deadline=Time.realtimeSinceStartup+3;
            while(host.IsFinishingCamp&&Time.realtimeSinceStartup<deadline)
            {
                Assert.IsFalse(WorldAllowed()); Assert.IsTrue(groups.All(g=>!g.interactable));
                fading|=groups.Any(g=>g.alpha>0&&g.alpha<.99f);
                yield return null;
            }
            Assert.IsFalse(host.IsFinishingCamp); Assert.IsTrue(fading);
            Assert.IsTrue(groups.All(g=>g.interactable&&g.alpha>.999f));
            Assert.IsTrue(PrivacyBootTestSupport.Find("pause").IsInteractable()); Assert.IsTrue(WorldAllowed());
        }

        [UnityTest] public IEnumerator SuccessfulCheckHidesBothSurfacesUntilTheNextLevel()
        {
            yield return LoadCamp();
            var host=CampSceneHost.Current;host.Session.DebugApplyWitness();
            Check();
            Assert.IsTrue(host.Session.IsCompleted); Assert.IsTrue(host.IsFinishingCamp);
            var groups=Groups(); Assert.AreEqual(2,groups.Length); Assert.IsTrue(groups.All(g=>!g.interactable));
            Check();
            yield return new WaitForSecondsRealtime(.65f);
            Assert.IsTrue(groups.All(g=>g.alpha<.001f&&!g.interactable));
            Assert.IsFalse(WorldAllowed());
            Assert.IsFalse(Object.FindObjectsByType<Button>(FindObjectsSortMode.None).Any(b=>
                groups.Any(g=>b.transform.IsChildOf(g.transform))&&b.IsInteractable()));
            float deadline=Time.realtimeSinceStartup+45;
            while((CampSceneHost.Current==host||CampSceneHost.Current?.UiReady!=true)&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.IsNotNull(CampSceneHost.Current); Assert.AreNotSame(host,CampSceneHost.Current);
            Assert.AreEqual("QC002",CampSceneHost.Current.Session.Level.id);
            Assert.IsFalse(CampSceneHost.Current.IsFinishingCamp);
        }

        [UnityTest] public IEnumerator ReducedMotionStillLocksAndReleasesAfterAFailedCheck()
        {
            yield return LoadCamp(true);
            var host=CampSceneHost.Current;Check(); Assert.IsFalse(WorldAllowed());
            float deadline=Time.realtimeSinceStartup+2;
            while(host.IsFinishingCamp&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.IsFalse(host.IsFinishingCamp); Assert.IsTrue(WorldAllowed());
            Assert.IsTrue(Groups().All(g=>g.alpha>.999f&&g.interactable));
        }
    }
}
