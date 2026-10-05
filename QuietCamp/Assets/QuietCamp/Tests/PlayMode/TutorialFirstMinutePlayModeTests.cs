using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
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
using UnityEngine.UI;

namespace QuietCamp.Tests
{
    /// <summary>Production save, HTML and host events; only QA product saves are touched.</summary>
    public sealed class TutorialFirstMinutePlayModeTests
    {
        SaveAdapter _save; string _progress,_session,_settings,_album;
        Mouse _mouse; InputSettings.BackgroundBehavior _background;
        InputSettings.EditorInputBehaviorInPlayMode _editorInput;
        [UnitySetUp] public IEnumerator FreshTutorialSave()
        {
            Assert.IsTrue(UnityEngine.Application.productName.Contains("QA"),"Use isolated QA saves.");
            yield return DrainPreviousUiAndServices();
            _save=new SaveAdapter();_save.Load(out _);
            _progress=JsonUtility.ToJson(_save.Progress);_session=JsonUtility.ToJson(_save.Session);
            _settings=JsonUtility.ToJson(_save.Settings);_album=JsonUtility.ToJson(_save.Album);
            _save.Progress=new ProgressSaveData{tutorial=new TutorialSaveData{initialized=true}};
            _save.Session=new SessionSaveData();_save.Album=new AlbumSaveData();
            _save.Settings.language="de";_save.Settings.textScale=1.3f;_save.Settings.reducedMotion=true;
            Assert.IsTrue(_save.Save());
            _background=InputSystem.settings.backgroundBehavior;_editorInput=InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            _mouse=InputSystem.AddDevice<Mouse>();
        }
        [UnityTearDown] public IEnumerator RestoreQaSave()
        {
            if(_mouse!=null){InputSystem.RemoveDevice(_mouse);_mouse=null;}
            InputSystem.settings.backgroundBehavior=_background;InputSystem.settings.editorInputBehaviorInPlayMode=_editorInput;
            yield return DrainPreviousUiAndServices();
            if(_save!=null)
            {
                _save.Progress=JsonUtility.FromJson<ProgressSaveData>(_progress);_save.Session=JsonUtility.FromJson<SessionSaveData>(_session);
                _save.Settings=JsonUtility.FromJson<SettingsSaveData>(_settings);_save.Album=JsonUtility.FromJson<AlbumSaveData>(_album);
                Assert.IsTrue(_save.Save());
            }
        }
        static IEnumerator DrainPreviousUiAndServices()
        {
            // Dispose the previous fixture's settings surface before replacing
            // the save. Its deferred flush must not overwrite the fresh tutorial
            // with the earlier fixture's unlocked progress during scene unload.
            foreach(var surface in Object.FindObjectsByType<HtmlSurface>())surface.FlushSettingsSave();
            foreach(var host in Object.FindObjectsByType<MenuSceneHost>())Object.Destroy(host.gameObject);
            foreach(var host in Object.FindObjectsByType<CampSceneHost>())Object.Destroy(host.gameObject);
            foreach(var boot in Object.FindObjectsByType<QuietCampBootstrap>())Object.Destroy(boot.gameObject);
            yield return null;
        }
        static IEnumerator Frames(int count=12){for(int i=0;i<count;i++)yield return null;}
        static Button Button(string id)=>PrivacyBootTestSupport.Find(id);
        static void Tap(string id)=>PrivacyBootTestSupport.Tap(Button(id));
        static IEnumerator ColdBoot()
        {
            foreach(var boot in Object.FindObjectsByType<QuietCampBootstrap>())Object.Destroy(boot.gameObject);
            yield return null;yield return SceneManager.LoadSceneAsync("Boot");yield return PrivacyBootTestSupport.EnterGame();yield return Frames(20);
        }
        static IEnumerator Camp(string id)
        {
            QuietCampBootstrap.ServicesRef.PendingLevelId=id;yield return SceneManager.LoadSceneAsync("Camp");
            float until=Time.realtimeSinceStartup+30;
            while(CampSceneHost.Current?.IsReady!=true&&Time.realtimeSinceStartup<until)yield return null;
            Assert.IsTrue(CampSceneHost.Current?.IsReady==true);yield return Frames(20);
        }
        static HtmlSurface Hud()=>Object.FindObjectsByType<HtmlSurface>().Single(s=>s.name=="CampHtml");
        static IEnumerator Shot(string name)=>(IEnumerator)typeof(ScreenshotPlayModeTest)
            .GetMethod("Shot",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{Path.Combine(Directory.GetCurrentDirectory(),"Screenshots/TutorialFirstMinute"),name});
        void Pointer(Vector2 position,bool down)
        {_mouse.MakeCurrent();InputSystem.QueueStateEvent(_mouse,new MouseState{position=position}.WithButton(MouseButton.Left,down));}

        [UnityTest,Timeout(240000)] public IEnumerator RealHostActionsPersistTheGuideAndSkipKeepsEssentialSettings()
        {
            if(UnityEngine.Application.isBatchMode)Assert.Ignore("Requires rendered Game View.");
            typeof(ScreenshotPlayModeTest).GetMethod("SetGameViewSize",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{720,1600});
            yield return ColdBoot();var services=QuietCampBootstrap.ServicesRef;
            Assert.NotNull(Button("settings"));Assert.IsNull(Button("levels"));Assert.IsNull(Button("album"));
            Tap("settings");yield return Frames();
            foreach(var category in new[]{"sound","comfort","privacy"})Assert.NotNull(Button("set-cat-"+category));
            Assert.IsNull(Button("set-cat-extras"));Tap("back");yield return Frames();
            yield return Camp("QC001");services=QuietCampBootstrap.ServicesRef;
            Assert.NotNull(Button("pause"));Assert.IsNull(Button("guests"));Assert.IsNull(Button("undo"));Assert.IsNull(Button("hint"));
            Assert.AreEqual("guide.select",services.Tutorial.Cue("QC001"));
            Tap("pause");yield return Frames();Tap("settings");yield return Frames();
            Assert.NotNull(Button("set-cat-sound"));Assert.NotNull(Button("set-cat-comfort"));Assert.NotNull(Button("set-cat-privacy"));
            Tap("back");yield return Frames();Tap("resume");yield return Frames();
            var controller=Object.FindAnyObjectByType<PlacementController>();var outside=new Vector2(-100,-100);
            Pointer(outside,true);yield return null;
            Assert.IsTrue(controller.BeginCardDrag("g1",0,outside));yield return Frames(4);
            Assert.IsTrue(services.Tutorial.HasStep("QC001.select"),"Starting the first card drag is an actual selection even when g1 was preselected.");
            Assert.IsTrue(controller.HasPreview);Assert.NotNull(Hud().Element("tutorial"));Assert.NotNull(Hud().Element("placement-status"));
            Assert.IsFalse(Button("guest-card").interactable,"A guide remount must preserve release-click suppression while a card is being dragged.");
            Assert.AreEqual("guide.place",services.Tutorial.Cue("QC001"));yield return Shot("01-guide-during-invalid-drag-de130");
            controller.Cancel();Pointer(outside,false);yield return Frames(4);
            Assert.NotNull(Hud().Element("tutorial"));Assert.IsFalse(services.Tutorial.HasStep("QC001.place"));
            Assert.IsTrue(Button("guest-card").interactable,"Cancelling returns control to the card.");

            // Recreate the service graph from the production save, then use real host events.
            yield return ColdBoot();yield return Camp("QC001");services=QuietCampBootstrap.ServicesRef;
            Assert.AreEqual("guide.place",services.Tutorial.Cue("QC001"));var host=CampSceneHost.Current;
            var first=host.Session.Level.witness[0];var second=host.Session.Level.witness[1];
            Assert.IsTrue(host.Session.TryCommit(PlacementCommand.Place(first.guestId,first.x,first.z,first.rotation),out _));yield return Frames();
            Assert.IsTrue(services.Tutorial.HasStep("QC001.place"));
            Assert.IsTrue(host.Session.TryCommit(PlacementCommand.Place(second.guestId,second.x,second.z,second.rotation),out _));yield return Frames();
            Assert.NotNull(Button("check"));Assert.IsFalse(Button("check").interactable,"A complete layout must still teach rotation before checking.");
            services.Actions.Execute(new UiActionRequest(new UiActionId("qc.check"),UiActionSource.Button,"Gameplay"));yield return Frames();
            Assert.IsFalse(host.Session.IsCompleted,"The host must also reject a check routed outside the disabled button.");
            Assert.AreEqual("guide.rotate",services.Tutorial.Cue("QC001"));
            Assert.NotNull(Button("rotate"));Tap("rotate");yield return Frames();
            Assert.IsTrue(services.Tutorial.HasStep("QC001.rotate"));
            Assert.IsTrue(host.Session.TryCommit(PlacementCommand.Place(first.guestId,first.x,first.z,first.rotation),out _));
            yield return Frames();Assert.IsTrue(Button("check").interactable);
            Tap("check");yield return Frames(20);
            Assert.IsTrue(host.Session.IsCompleted);Assert.IsTrue(services.Tutorial.HasStep("QC001.first-check"));
            Assert.IsTrue(services.Tutorial.RoadmapUnlocked);Assert.IsTrue(services.Tutorial.HistoryUnlocked);Assert.IsFalse(services.Tutorial.RewardOwned);

            yield return Camp("QC002");services=QuietCampBootstrap.ServicesRef;Assert.NotNull(Button("undo"));Assert.IsNull(Button("hint"));
            Tap("guide-skip");yield return Frames();Tap("guide-skip-confirm");yield return Frames();
            Assert.IsTrue(services.Tutorial.Skipped);Assert.IsTrue(services.Tutorial.AllControls);Assert.IsFalse(services.Tutorial.RewardOwned);
            Assert.NotNull(Button("guests"));Assert.NotNull(Button("hint"));Assert.NotNull(Button("pause"));Assert.IsNull(Hud().Element("tutorial"));
            yield return Shot("02-skipped-guide-unlocks-controls-de130");
            host=CampSceneHost.Current;
            foreach(var pose in host.Session.Level.witness)
                Assert.IsTrue(host.Session.TryCommit(PlacementCommand.Place(pose.guestId,pose.x,pose.z,pose.rotation),out _));
            yield return Frames();Assert.IsTrue(Button("check").interactable);Tap("check");yield return Frames();
            Assert.IsTrue(host.Session.IsCompleted);Assert.IsFalse(services.Tutorial.RewardOwned,"Finishing after skip must not grant the evaluated tutorial gift.");
        }
    }
}
