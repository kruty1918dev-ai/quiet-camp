using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using QuietCamp.Presentation;
using QuietCamp.Presentation.UI;
using QuietCamp.Presentation.World;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace QuietCamp.Tests
{
    public class NativeHtmlUiPlayModeTests
    {
        const BindingFlags Hidden = BindingFlags.NonPublic | BindingFlags.Instance;
        static IEnumerator Frames(int count) { for (var i = 0; i < count; i++) yield return null; }
        static Button Button(string id) => Object.FindObjectsByType<Button>()
            .FirstOrDefault(b => b.name == "<button #" + id + ">" && b.gameObject.activeInHierarchy);
        static void Tap(string id)
        {
            var button = Button(id); Assert.NotNull(button, id); Assert.IsTrue(button.interactable, id);
            var rect = (RectTransform)button.transform;
            Assert.Greater(rect.rect.width, 20, "Button has no layout: " + id);
            Assert.Greater(rect.rect.height, 20, "Button has no layout: " + id);
            var pointer = new PointerEventData(EventSystem.current) { position =
                RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center)) };
            var hits = new System.Collections.Generic.List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            Assert.IsTrue(hits.Count > 0 && (hits[0].gameObject.transform == rect ||
                hits[0].gameObject.transform.IsChildOf(rect)), "Button is covered: " + id);
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler);
        }
        static void AssertNoScript(HtmlSurface surface)
        {
            Assert.IsTrue(surface.IsMounted, surface.LastMountError);
            var host = typeof(HtmlSurface).GetField("_host", Hidden).GetValue(surface);
            var context = host.GetType().GetField("_context", Hidden).GetValue(host);
            if (context == null)
            {
                Assert.AreEqual(0,surface.GetComponentsInChildren<Selectable>().Length,"Only an intentionally empty layer may omit its document context.");
                Assert.AreEqual(0,surface.GetComponentsInChildren<TMPro.TMP_Text>().Length);
                return;
            }
            Assert.IsNull(context.GetType().GetProperty("Script").GetValue(context),
                "Game UI must not initialize a JavaScript VM");
        }
        static IEnumerator Shot(string name) => (IEnumerator)typeof(ScreenshotPlayModeTest)
            .GetMethod("Shot", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] {
                Path.Combine(Directory.GetCurrentDirectory(), "Screenshots/AndroidUiRecovery"), name });

        [UnityTest]
        public IEnumerator RealMenuStartsGameWithNativeEventsAndWorkingPause()
        {
            if (UnityEngine.Application.isBatchMode) Assert.Ignore("Requires a rendered Game View");
            typeof(ScreenshotPlayModeTest).GetMethod("SetGameViewSize", BindingFlags.NonPublic | BindingFlags.Static)
                .Invoke(null, new object[] { 720, 1600 });
            yield return SceneManager.LoadSceneAsync("Boot");
            yield return PrivacyBootTestSupport.EnterGame(); yield return Frames(120);
            if (SceneManager.GetActiveScene().name != "MainMenu")
                yield return SceneManager.LoadSceneAsync("MainMenu");
            yield return Frames(45);
            var services = QuietCampBootstrap.ServicesRef;
            services.Localization.TrySetLanguage("uk"); services.ReducedMotion = true;
            yield return Frames(10);
            foreach (var surface in Object.FindObjectsByType<HtmlSurface>()) AssertNoScript(surface);
            yield return Shot("01_menu_720x1600");
            var screens = (MenuScreens)typeof(MenuSceneHost).GetField("_screens", Hidden).GetValue(MenuSceneHost.Current);
            screens.Show("Settings"); yield return Frames(20);
            foreach (var category in new[] { "sound", "comfort", "look", "extras", "privacy" })
            {
                if (Button("set-cat-"+category) == null) continue; // Optional gifts require ownership.
                Tap("set-cat-"+category); yield return Frames(15);
                AssertNoScript(screens.Overlay);
                var detail = screens.Overlay.Element("sheet-scroll"); Assert.NotNull(detail);
                Assert.Greater(detail.rect.width,100); Assert.Greater(detail.rect.height,100);
                Tap("back"); yield return Frames(15);
                Assert.NotNull(Button("language-uk"),"Category back must restore the settings overview.");
            }
            Tap("back"); yield return Frames(20); Assert.AreEqual("Main",screens.Current);
            screens.Show("Journeys"); yield return Frames(20);
            AssertNoScript(screens.Overlay);
            Tap("journey-main"); yield return Frames(20);
            Assert.NotNull(screens.Overlay.Element("journey-viewport"));
            AssertNoScript(screens.Overlay);
            Tap("back"); yield return Frames(20);
            Assert.AreEqual("Journeys", screens.Current);
            Tap("back"); yield return Frames(20);
            Assert.AreEqual("Main", screens.Current);
            Tap("continue");
            for (var i = 0; i < 600 && (SceneManager.GetActiveScene().name != "Camp" ||
                CampSceneHost.Current == null || !CampSceneHost.Current.IsReady); i++) yield return null;
            Assert.AreEqual("Camp", SceneManager.GetActiveScene().name);
            yield return Frames(40);
            foreach (var surface in Object.FindObjectsByType<HtmlSurface>()) AssertNoScript(surface);
            Assert.NotNull(Button("guests"));
            Assert.IsTrue(Button("guest-card") != null || Button("check") != null, "Missing gameplay dock");
            yield return Shot("02_gameplay_720x1600");
            Tap("pause"); yield return Frames(15); Assert.NotNull(Button("resume"));
            yield return Shot("03_pause_720x1600");
            Tap("resume"); yield return Frames(15); Assert.IsNull(Button("resume"));

            typeof(ScreenshotPlayModeTest).GetMethod("SetGameViewSize", BindingFlags.NonPublic | BindingFlags.Static)
                .Invoke(null, new object[] { 1080, 1920 });
            services.Localization.TrySetLanguage("de"); services.Settings.textScale = 1.3f;
            foreach (var surface in Object.FindObjectsByType<HtmlSurface>()) surface.Refresh();
            yield return Frames(20); yield return Shot("04_gameplay_de_130_1080x1920");
        }

        [UnityTest]
        public IEnumerator NativeSwitchLabelDisabledAndRemountEmitOnce()
        {
            var canvas = new GameObject("SwitchDisplay", typeof(RectTransform), typeof(Canvas));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            HtmlSurface surface = null; var on = false; var disabled = false; var changes = 0;
            surface = HtmlSurface.Create(canvas.transform, "SwitchSurface", null, () =>
            {
                surface.Callbacks.BindToggle("wind", value => { on = value; changes++; });
                return "<view class='app'><label id='caption' for='#wind'>Вітер</label>"
                    + "<switch id='wind' checked='" + on.ToString().ToLowerInvariant()
                    + "' disabled='" + disabled.ToString().ToLowerInvariant()
                    + "' onChange=\"Globals.campUi.Toggle('wind', event)\" /></view>";
            });
            try
            {
                yield return Frames(3); AssertNoScript(surface); Assert.AreEqual(0, changes);
                var toggle = surface.GetComponentInChildren<Toggle>(); Assert.NotNull(toggle);
                for (var i = 0; i < 10; i++) ExecuteEvents.Execute(surface.Element("caption").gameObject,
                    new PointerEventData(EventSystem.current), ExecuteEvents.pointerClickHandler);
                Assert.AreEqual(10, changes); Assert.IsFalse(on);
                surface.Refresh(); yield return Frames(3);
                Assert.AreSame(toggle, surface.GetComponentInChildren<Toggle>());
                Assert.AreEqual(10, changes, "Reconciliation must be silent");
                surface.ExtraCss = ".app switch { width: 180px; height: 72px; }";
                surface.Refresh(); yield return Frames(3);
                toggle = surface.GetComponentInChildren<Toggle>(); Assert.NotNull(toggle);
                Assert.AreEqual(10, changes, "CSS remount must be silent");
                Assert.AreEqual(180, surface.Element("wind").rect.width, 1);
                ExecuteEvents.Execute(surface.Element("caption").gameObject,
                    new PointerEventData(EventSystem.current), ExecuteEvents.pointerClickHandler);
                Assert.AreEqual(11, changes); Assert.IsTrue(on);
                var knob = surface.Element("wind").Find("Knob") as RectTransform;
                Assert.NotNull(knob); Assert.AreEqual(knob.rect.width, knob.rect.height, .1f);
                disabled = true; surface.Refresh(); yield return Frames(3);
                ExecuteEvents.Execute(surface.Element("caption").gameObject,
                    new PointerEventData(EventSystem.current), ExecuteEvents.pointerClickHandler);
                Assert.AreEqual(11, changes, "Disabled label must not change the switch");
                Assert.IsTrue(toggle.isOn);
            }
            finally { Object.Destroy(canvas); }
            yield return null;
        }

        [UnityTest]
        public IEnumerator ZeroSizedStartupWaitsForLayoutThenMounts()
        {
            var canvas = new GameObject("DelayedDisplay", typeof(RectTransform), typeof(Canvas));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var surface = HtmlSurface.Create(canvas.transform, "DelayedSurface", null,
                () => "<view class='app'><text>Ready</text></view>");
            var rect = (RectTransform)surface.transform;
            rect.anchorMin = rect.anchorMax = Vector2.one * .5f; rect.sizeDelta = Vector2.zero;
            try
            {
                yield return Frames(3); Assert.IsFalse(surface.IsMounted);
                Assert.IsNull(surface.LastMountError, "Zero display size must not consume mount retries");
                rect.sizeDelta = new Vector2(600, 400); yield return Frames(3);
                AssertNoScript(surface);
            }
            finally { Object.Destroy(canvas); }
            yield return null;
        }

        [UnityTest]
        public IEnumerator MountFailureShowsNativeActionThenCanRecover()
        {
            var canvas = new GameObject("RecoveryDisplay", typeof(RectTransform), typeof(Canvas));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            HtmlSurface surface = null; var clicks = 0; var mounts = 0;
            surface = HtmlSurface.Create(canvas.transform, "RecoverySurface", null,
                () => "<view class='app'>" + HtmlUi.Button(surface, "continue", "Почати", () => clicks++) + "</view>");
            surface.Mounted += () => mounts++;
            var host = typeof(HtmlSurface).GetField("_host", Hidden).GetValue(surface);
            var resolver = host.GetType().GetProperty("NativeEventResolver");
            var working = resolver.GetValue(host);
            resolver.SetValue(host, new Func<string, Delegate>(_ => throw new InvalidOperationException("Forced UI failure")));
            for (var i = 0; i < 3; i++) LogAssert.Expect(LogType.Error, new Regex("Forced UI failure"));
            try
            {
                yield return new WaitForSecondsRealtime(2f);
                Assert.AreEqual(0, mounts, "Failed mount must not emit Mounted");
                Assert.IsFalse(surface.IsMounted);
                var recovery = surface.transform.Find("UiRecovery"); Assert.NotNull(recovery);
                recovery.GetComponentInChildren<Button>().onClick.Invoke();
                Assert.AreEqual(1, clicks, "Recovery must use the real game action once");
                LogAssert.Expect(LogType.Error, new Regex("Forced UI failure"));
                yield return Frames(4);
                recovery = surface.transform.Find("UiRecovery");
                Assert.NotNull(recovery, "Repeated failure must not destroy the only usable buttons");
                Assert.IsTrue(recovery.gameObject.activeInHierarchy);
                resolver.SetValue(host, working); surface.Refresh(); yield return Frames(4);
                AssertNoScript(surface); Assert.AreEqual(1, mounts);
                Assert.IsNull(surface.transform.Find("UiRecovery"));
                surface.GetComponentInChildren<Button>().onClick.Invoke(); Assert.AreEqual(2, clicks);
            }
            finally { Object.Destroy(canvas); }
            yield return null;
        }
    }
}
