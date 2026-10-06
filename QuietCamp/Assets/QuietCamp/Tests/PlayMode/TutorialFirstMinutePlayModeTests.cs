using System.Collections;
using System.Collections.Generic;
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
        SaveAdapter _save; string _progress,_session,_settings,_album,_economy,_entitlements,_purchases,_memories;
        Mouse _mouse; InputSettings.BackgroundBehavior _background;
        InputSettings.EditorInputBehaviorInPlayMode _editorInput;
        [UnitySetUp] public IEnumerator FreshTutorialSave()
        {
            Assert.IsTrue(UnityEngine.Application.productName.Contains("QA"),"Use isolated QA saves.");
            yield return DrainPreviousUiAndServices();
            _save=new SaveAdapter();_save.Load(out _);
            _progress=JsonUtility.ToJson(_save.Progress);_session=JsonUtility.ToJson(_save.Session);
            _settings=JsonUtility.ToJson(_save.Settings);_album=JsonUtility.ToJson(_save.Album);
            _economy=JsonUtility.ToJson(_save.Economy);_entitlements=JsonUtility.ToJson(_save.Entitlements);
            _purchases=JsonUtility.ToJson(_save.Purchases);_memories=JsonUtility.ToJson(_save.Memories);
            _save.Progress=new ProgressSaveData{tutorial=new TutorialSaveData{initialized=true}};
            _save.Session=new SessionSaveData();_save.Album=new AlbumSaveData();
            _save.Economy=new EconomySaveData();_save.Entitlements=new EntitlementSaveData();
            _save.Purchases=new PurchaseSaveData();_save.Memories=new MemorySaveData();
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
                _save.Economy=JsonUtility.FromJson<EconomySaveData>(_economy);_save.Entitlements=JsonUtility.FromJson<EntitlementSaveData>(_entitlements);
                _save.Purchases=JsonUtility.FromJson<PurchaseSaveData>(_purchases);_save.Memories=JsonUtility.FromJson<MemorySaveData>(_memories);
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
            Assert.IsTrue(CampSceneHost.Current?.IsReady==true);
            Assert.AreEqual(id, CampSceneHost.Current?.Session?.Level.id, "The fixture must enter an accessible camp, not the access-recovery menu.");
            yield return Frames(20);
        }
        static HtmlSurface Hud()=>Object.FindObjectsByType<HtmlSurface>().Single(s=>s.name=="CampHtml");
        static IEnumerator Shot(string name)=>(IEnumerator)typeof(ScreenshotPlayModeTest)
            .GetMethod("Shot",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{Path.Combine(Directory.GetCurrentDirectory(),"Screenshots/TutorialFirstMinute"),name});
        void Pointer(Vector2 position,bool down)
        {_mouse.MakeCurrent();InputSystem.QueueStateEvent(_mouse,new MouseState{position=position}.WithButton(MouseButton.Left,down));}

        [UnityTest, Timeout(360000)] public IEnumerator PlayerFacingScreensAreReadableAndUseExplicitActions()
        {
            if (UnityEngine.Application.isBatchMode) Assert.Ignore("Requires rendered Game View.");
            yield return ColdBoot();
            var services = QuietCampBootstrap.ServicesRef;
            foreach (var size in new[] { new Vector2Int(720, 1600), new Vector2Int(1600, 720) })
            foreach (var language in new[] { "uk", "en", "de" })
            foreach (var scale in new[] { 1f, 1.3f })
            {
                typeof(ScreenshotPlayModeTest).GetMethod("SetGameViewSize", BindingFlags.Static | BindingFlags.NonPublic)
                    .Invoke(null, new object[] { size.x, size.y });
                services.Localization.TrySetLanguage(language); services.Settings.textScale = scale;
                foreach (var surface in Object.FindObjectsByType<HtmlSurface>()) surface.Refresh();
                yield return Frames(20);
                var guide = Hud().Element("tutorial");
                var portrait = Hud().Element("guide-portrait");
                Assert.NotNull(guide); Assert.NotNull(portrait);
                Assert.GreaterOrEqual(portrait.rect.width, 160f, "The guide must be visibly larger than the old 100–120px portrait.");
                foreach (var text in guide.GetComponentsInChildren<TMPro.TMP_Text>())
                {
                    text.ForceMeshUpdate();
                    Assert.IsFalse(text.isTextOverflowing, language + " guide text clips at " + scale);
                }
                var corners = new Vector3[4]; guide.GetWorldCorners(corners);
                foreach (var corner in corners)
                {
                    var point = RectTransformUtility.WorldToScreenPoint(null, corner);
                    Assert.That(point.x, Is.InRange(-2f, Screen.width + 2f));
                    Assert.That(point.y, Is.InRange(-2f, Screen.height + 2f));
                }
                yield return Shot("guide-" + language + "-" + size.x + "x" + size.y + "-" + Mathf.RoundToInt(scale * 100));
            }
            typeof(ScreenshotPlayModeTest).GetMethod("SetGameViewSize", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] { 720, 1600 });
            services.Localization.TrySetLanguage("uk"); services.Settings.textScale = 1f; services.Tutorial.Skip();
            yield return Camp("QC001");
            var host = CampSceneHost.Current;
            var status = string.Format(services.Localization.T("economy.status"), services.Economy.Lives, services.Economy.Hints);
            Assert.IsFalse(Hud().GetComponentsInChildren<TMPro.TMP_Text>().Any(text => text.text == status));
            yield return Shot("13-normal-gameplay");
            Tap("pause"); yield return Frames();
            Assert.NotNull(Button("resume")); Assert.NotNull(Button("settings")); Assert.NotNull(Button("menu"));
            Assert.IsNull(Button("economy")); Assert.IsNull(Button("pause-hint"));
            yield return Shot("14-simple-pause");
            Tap("settings"); yield return Frames();
            Assert.IsNull(Button("set-cat-extras")); Assert.NotNull(Button("help-supplies"));
            yield return Shot("15-settings");
            Tap("help-supplies"); yield return Frames();
            Assert.IsNull(Button("purchase-restore")); Assert.IsNull(Button("life-ad"));
            Assert.IsFalse(Object.FindObjectsByType<Button>().Any(button => button.gameObject.activeInHierarchy && button.name.Contains("#buy-")));
            yield return Shot("16-contextual-help");
            Assert.AreEqual(EconomyResult.Applied, services.Economy.CreditVerified("ui-review-embers", 100));
            yield return Frames();
            int currency = services.Economy.Currency, hints = services.Economy.Hints;
            Tap("exchange-hint"); yield return Frames(); Tap("exchange-hint"); yield return Frames();
            Assert.AreEqual(currency, services.Economy.Currency, "Repeated selection must never spend currency.");
            Tap("exchange-cancel"); yield return Frames(); Assert.AreEqual(currency, services.Economy.Currency);
            Tap("exchange-hint"); yield return Frames(); Tap("exchange-confirm"); yield return Frames();
            Assert.AreEqual(hints + 1, services.Economy.Hints);
            Assert.AreEqual(currency - services.Economy.HintCost, services.Economy.Currency);
            Tap("back"); yield return Frames(); Tap("back"); yield return Frames(); Tap("resume"); yield return Frames();
            host.Session.DebugApplyWitness(); yield return Frames();
            Assert.NotNull(Button("check"));
            Assert.IsTrue(Button("check").GetComponentsInChildren<TMPro.TMP_Text>().Any(text => text.text == services.Localization.T("hud.ready")));
            yield return Shot("17-finish-camp");
            Tap("pause"); yield return Frames(); Tap("menu");
            float deadline = Time.realtimeSinceStartup + 45;
            while ((SceneManager.GetActiveScene().name != "MainMenu" || MenuSceneHost.Current?.UiReady != true) && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.AreEqual("MainMenu", SceneManager.GetActiveScene().name); yield return Frames(30);
            Assert.NotNull(Button("continue")); Assert.NotNull(Button("levels")); Assert.NotNull(Button("album"));
            Assert.IsNull(Button("journeys")); Assert.IsNull(Button("economy"));
            yield return Shot("18-uncluttered-menu");
            Tap("levels"); yield return Frames(); Assert.NotNull(Button("journeys"));
            yield return Shot("19-story-map");
            Tap("journeys"); yield return Frames(); Assert.NotNull(Button("journey-main"));
            Assert.IsNull(Button("journey-lighthouse")); Assert.IsNull(Button("journey-garden")); Assert.IsNull(Button("journey-station"));
            yield return Shot("20-story-library");
        }

        [UnityTest,Timeout(240000)] public IEnumerator RealHostActionsPersistTheGuideAndSkipKeepsEssentialSettings()
        {
            if(UnityEngine.Application.isBatchMode)Assert.Ignore("Requires rendered Game View.");
            typeof(ScreenshotPlayModeTest).GetMethod("SetGameViewSize",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{720,1600});
            yield return ColdBoot();var services=QuietCampBootstrap.ServicesRef;
            Assert.AreEqual("Camp", SceneManager.GetActiveScene().name, "New players must enter the first guided camp automatically.");
            Assert.AreEqual("QC001", CampSceneHost.Current.Session.Level.id);
            Assert.IsFalse(services.Tutorial.NeedsIntroduction);
            Assert.IsNull(Hud().Element("economy-status"));
            Assert.IsNull(Button("levels"));Assert.IsNull(Button("album"));
            yield return Shot("00-automatic-introduction-de130");
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
            Assert.IsTrue(services.Tutorial.Skipped);Assert.IsTrue(services.Tutorial.AllControls);Assert.IsTrue(services.Tutorial.RewardOwned,"The pennant is a welcome gift — skipping never takes it.");
            Assert.NotNull(Button("guests"));Assert.NotNull(Button("hint"));Assert.NotNull(Button("pause"));Assert.IsNull(Hud().Element("tutorial"));
            yield return Shot("02-skipped-guide-unlocks-controls-de130");
            host=CampSceneHost.Current;
            foreach(var pose in host.Session.Level.witness)
                Assert.IsTrue(host.Session.TryCommit(PlacementCommand.Place(pose.guestId,pose.x,pose.z,pose.rotation),out _));
            yield return Frames();Assert.IsTrue(Button("check").interactable);Tap("check");yield return Frames();
            Assert.IsTrue(host.Session.IsCompleted);Assert.IsTrue(services.Tutorial.RewardOwned);
            var claimsJson=JsonUtility.ToJson(services.Tutorial.Save);
            Assert.AreEqual(1,claimsJson.Split(new[]{TutorialDirector.RewardId},System.StringSplitOptions.None).Length-1,
                "Finishing after skip must not grant the gift twice.");
        }

        [UnityTest,Timeout(240000)] public IEnumerator GuideExplainsMistakesAndFinishedLearningReturnsToMenu()
        {
            if(UnityEngine.Application.isBatchMode)Assert.Ignore("Requires rendered Game View.");
            typeof(ScreenshotPlayModeTest).GetMethod("SetGameViewSize",BindingFlags.Static|BindingFlags.NonPublic)
                .Invoke(null,new object[]{720,1600});
            // Resume a guide that already passed the first four glades.
            _save.Progress.tutorial=JsonUtility.FromJson<TutorialSaveData>(
                "{\"initialized\":true,\"introductionSeen\":true,\"progress\":{\"flowId\":\"quietcamp.first-camps\","
                +"\"completedSteps\":[\"QC001.select\",\"QC001.place\",\"QC001.rotate\",\"QC001.first-check\","
                +"\"QC002.move\",\"QC002.undo\",\"QC002.shared-route\",\"QC003.shade\",\"QC004.quiet\"]}}");
            _save.Progress.completedIds=new[]{"QC001","QC002","QC003","QC004"};
            Assert.IsTrue(_save.Save());
            yield return ColdBoot();yield return Camp("QC005");
            var services=QuietCampBootstrap.ServicesRef;
            Assert.IsTrue(services.Tutorial.Guiding("QC005"));
            Assert.AreEqual("guide.friends",services.Tutorial.Cue("QC005"));
            var host=CampSceneHost.Current;var level=host.Session.Level;

            // A committable pose that still breaks a wish makes the mentor explain it.
            string badCode=null;Placement bad=null;
            foreach(var guest in level.guests)
            {
                for(var x=0;x<level.width&&bad==null;x++)for(var z=0;z<level.height&&bad==null;z++)
                for(var r=0;r<4&&bad==null;r++)
                {
                    var candidate=new Placement{guestId=guest.id,x=x,z=z,rotation=r};
                    var probe=new List<Placement>(host.Session.State.Placements){candidate};
                    var report=RuleEvaluator.Evaluate(level,probe,false);
                    var issue=report.Issues.Find(i=>i.Code=="path"||i.Code=="shade"||i.Code=="quiet"||i.Code=="friends");
                    if(report.CanCommit&&issue!=null){bad=candidate;badCode=issue.Code;}
                }
                if(bad!=null)break;
            }
            Assert.NotNull(bad,"QC005 must offer a legal commit that still breaks a wish.");
            Assert.IsTrue(host.Session.TryCommit(PlacementCommand.Place(bad.guestId,bad.x,bad.z,bad.rotation),out _));
            yield return Frames(8);
            var guideText=string.Join(" ",Hud().Element("tutorial")
                .GetComponentsInChildren<TMPro.TMP_Text>().Select(t=>t.text));
            Assert.IsTrue(guideText.Contains(services.Localization.T("guide.mistake."+badCode)),
                "The mentor must explain the broken wish ("+badCode+"), got: "+guideText);
            yield return Shot("03-mentor-explains-mistake");
            Assert.IsTrue(host.Session.Undo(),"Undo must return the board to the guided step.");
            yield return Frames(4);

            foreach(var pose in level.witness)
                Assert.IsTrue(host.Session.TryCommit(PlacementCommand.Place(pose.guestId,pose.x,pose.z,pose.rotation),out _));
            yield return Frames();Tap("check");yield return Frames(20);
            Assert.IsTrue(host.Session.IsCompleted);Assert.IsTrue(services.Tutorial.Finished);
            Assert.IsTrue(services.Tutorial.RewardOwned,"The last guided glade grants the pennant.");
            float deadline=Time.realtimeSinceStartup+25;
            while(SceneManager.GetActiveScene().name!="MainMenu"&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.AreEqual("MainMenu",SceneManager.GetActiveScene().name,
                "A finished tutorial must return the player to the menu automatically.");
            yield return Frames(20);
            Assert.IsTrue(services.Tutorial.NeedsMenuIntro,"Fresh finishers get one menu orientation card.");
            Assert.NotNull(Button("menu-guide-ok"));yield return Shot("04-menu-orientation");
            Tap("menu-guide-ok");yield return Frames(6);
            Assert.IsFalse(services.Tutorial.NeedsMenuIntro);Assert.IsNull(Button("menu-guide-ok"));
        }
    }
}
