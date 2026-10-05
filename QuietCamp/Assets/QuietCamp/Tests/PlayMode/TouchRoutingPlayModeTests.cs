using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using QuietCamp.Presentation;
using QuietCamp.Presentation.World;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace QuietCamp.Tests
{
    /// <summary>Send actual Input System touch states: never invoke onClick or ExecuteEvents.</summary>
    public class TouchRoutingPlayModeTests
    {
        Touchscreen _screen;
        InputSettings.BackgroundBehavior _background;
#if UNITY_EDITOR
        InputSettings.EditorInputBehaviorInPlayMode _editorInput;
#endif
        static IEnumerator Frames(int count) { for(int i=0;i<count;i++)yield return null; }
        [UnitySetUp] public IEnumerator SetUp()
        {
            _background=InputSystem.settings.backgroundBehavior;
#if UNITY_EDITOR
            _editorInput=InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            _screen=InputSystem.AddDevice<Touchscreen>();yield return null;
        }
        [UnityTearDown] public IEnumerator TearDown()
        {
            if(_screen!=null)InputSystem.RemoveDevice(_screen);
            InputSystem.settings.backgroundBehavior=_background;
#if UNITY_EDITOR
            InputSystem.settings.editorInputBehaviorInPlayMode=_editorInput;
#endif
            yield return null;
        }
        static Button Button(string id)=>Object.FindObjectsByType<Button>().FirstOrDefault(b=>b.name=="<button #"+id+">"&&b.gameObject.activeInHierarchy);
        IEnumerator Tap(string id,int finger=1)
        {
            var button=Button(id);Assert.NotNull(button,id);
            var rect=(RectTransform)button.transform;
            var position=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center));
            InputSystem.QueueStateEvent(_screen,new TouchState{touchId=finger,position=position,phase=UnityEngine.InputSystem.TouchPhase.Began});
            yield return Frames(2);
            InputSystem.QueueStateEvent(_screen,new TouchState{touchId=finger,position=position,phase=UnityEngine.InputSystem.TouchPhase.Ended});
            yield return Frames(3);
        }
        static IEnumerator Boot()
        {
            yield return SceneManager.LoadSceneAsync("Boot");
            yield return PrivacyBootTestSupport.EnterGame();
            var deadline = Time.realtimeSinceStartup + 90;
            while(Object.FindAnyObjectByType<QuietCampBootstrap>()?.StartupReady != true && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(Object.FindAnyObjectByType<QuietCampBootstrap>().StartupReady);
            yield return Frames(20);
        }
        [UnityTest, Timeout(240000)] public IEnumerator TouchNavigatesSettingsTopicsAndChangesSwitchOnce()
        {
            Assert.IsTrue(UnityEngine.Application.productName.Contains("QA"), "Use isolated QA saves");
            typeof(ScreenshotPlayModeTest).GetMethod("SetGameViewSize",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{720,1600});
            yield return Boot(); var services = QuietCampBootstrap.ServicesRef; services.ReducedMotion = true;
            yield return Tap("settings"); yield return Tap("set-cat-comfort");
            var surface = Object.FindObjectsByType<QuietCamp.Presentation.UI.HtmlSurface>().First(s => s.name == "MenuOverlay");
            var before = services.Settings.highContrast;
            var rect = surface.Element("settings.contrast"); var position = RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center));
            InputSystem.QueueStateEvent(_screen,new TouchState{touchId=1,position=position,phase=UnityEngine.InputSystem.TouchPhase.Began}); yield return Frames(2);
            InputSystem.QueueStateEvent(_screen,new TouchState{touchId=1,position=position,phase=UnityEngine.InputSystem.TouchPhase.Ended}); yield return Frames(5);
            Assert.AreEqual(!before, services.Settings.highContrast, "One touch must change the switch once");
            services.Settings.highContrast = before;
            yield return Tap("back"); yield return Tap("set-cat-privacy"); yield return Tap("privacy-section-documents");
            Assert.NotNull(Button("privacy-policy")); Assert.IsFalse(Button("privacy-policy").interactable);
            yield return Tap("back"); Assert.NotNull(Button("privacy-section-data"));
            yield return Tap("back"); Assert.NotNull(Button("set-cat-sound"));
            yield return Tap("set-cat-sound"); yield return Tap("settings-close"); Assert.NotNull(Button("continue"));
        }
        [UnityTest] public IEnumerator TouchCanOpenSettingsStartCampAndPauseAfterSceneChange()
        {
            typeof(ScreenshotPlayModeTest).GetMethod("SetGameViewSize",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{1080,2400});
            yield return Boot();
            Assert.AreEqual("MainMenu",SceneManager.GetActiveScene().name);
            var module=EventSystem.current.GetComponent<InputSystemUIInputModule>();Assert.NotNull(module);Assert.IsTrue(module.enabled);
            Assert.NotNull(module.point?.action,"UI point action missing");Assert.NotNull(module.leftClick?.action,"UI click action missing");
            Assert.IsTrue(module.point.action.enabled);Assert.IsTrue(module.leftClick.action.enabled);
            Assert.IsTrue(module.point.action.controls.Any(c=>c.device==_screen),"UI point does not listen to the touchscreen");
            Assert.IsTrue(module.leftClick.action.controls.Any(c=>c.device==_screen),"UI press does not listen to the touchscreen");
            Debug.Log("[TouchQA] point="+module.point.action+" click="+module.leftClick.action+" modules="+Object.FindObjectsByType<EventSystem>().Length);
            var services=QuietCampBootstrap.ServicesRef;services.ReducedMotion=true;
            yield return Tap("settings");Assert.NotNull(Button("back"),"Touch failed to open settings");
            yield return Tap("back");yield return Tap("continue");
            float deadline=Time.realtimeSinceStartup+12;
            while((SceneManager.GetActiveScene().name!="Camp"||CampSceneHost.Current?.IsReady!=true)&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.AreEqual("Camp",SceneManager.GetActiveScene().name,"Touch failed to start gameplay");yield return Frames(30);
            yield return Tap("pause");Assert.NotNull(Button("resume"),"Touch failed to pause after scene change");
            yield return Tap("resume");Assert.IsNull(Button("resume"));
        }
    }
}
