using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Kruty1918.UIActions.API;
using QuietCamp.Presentation;
using QuietCamp.Presentation.UI;
using QuietCamp.Presentation.World;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using TMPro;

namespace QuietCamp.Tests
{
    public sealed class SettingsLegalPlayModeTests
    {
        bool _async;
        [SetUp] public void Setup()
        {
            Assert.IsTrue(UnityEngine.Application.productName.Contains("QA"), "Use an isolated QA project and saves");
            _async = UnityEditor.ShaderUtil.allowAsyncCompilation; UnityEditor.ShaderUtil.allowAsyncCompilation = false;
        }
        [TearDown] public void Cleanup() => UnityEditor.ShaderUtil.allowAsyncCompilation = _async;
        const BindingFlags Hidden = BindingFlags.NonPublic | BindingFlags.Static;
        static IEnumerator Frames(int n = 12) { for (int i = 0; i < n; i++) yield return null; }
        static void Size(int w, int h) => typeof(ScreenshotPlayModeTest).GetMethod("SetGameViewSize", Hidden).Invoke(null, new object[] { w, h });
        static IEnumerator Shot(string name) => (IEnumerator)typeof(ScreenshotPlayModeTest).GetMethod("Shot", Hidden).Invoke(null,
            new object[] { Path.Combine(Directory.GetCurrentDirectory(), "Screenshots/MenuRefresh"), name });
        static Button Find(string id) => Object.FindObjectsByType<Button>().FirstOrDefault(b => b.name == "<button #" + id + ">");
        static void Tap(string id)
        {
            var button = Find(id); Assert.NotNull(button, id); Assert.IsTrue(button.interactable, id);
            var rect = (RectTransform)button.transform;
            var pointer = new PointerEventData(EventSystem.current) { position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center)) };
            var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, hits);
            Assert.IsTrue(hits.Count > 0 && (hits[0].gameObject.transform == rect || hits[0].gameObject.transform.IsChildOf(rect)), "Covered control: " + id + " at=" + pointer.position + " screen=" + Screen.width + "x" + Screen.height + " top=" + (hits.Count > 0 ? hits[0].gameObject.name : "none") + " groups=" + string.Join(",", button.GetComponentsInParent<CanvasGroup>().Select(g => g.name + ":" + g.blocksRaycasts + ":" + g.alpha)));
            Assert.That(pointer.position.x, Is.InRange(1f, Screen.width - 1f)); Assert.That(pointer.position.y, Is.InRange(1f, Screen.height - 1f));
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler);
        }
        static IEnumerator Reveal(string id)
        {
            var button = Find(id); Assert.NotNull(button, id);
            var scroll = button.GetComponentInParent<ScrollRect>();
            if (scroll != null && scroll.content != null && scroll.viewport != null)
            {
                Canvas.ForceUpdateCanvases();
                var rect = (RectTransform)button.transform;
                var centre = scroll.viewport.InverseTransformPoint(rect.TransformPoint(rect.rect.center));
                var position = scroll.content.anchoredPosition;
                position.y += scroll.viewport.rect.center.y - centre.y;
                scroll.content.anchoredPosition = position; scroll.velocity = Vector2.zero;
                yield return Frames(4);
            }
        }
        static HtmlSurface Overlay() => Object.FindObjectsByType<HtmlSurface>().First(s => s.name == "MenuOverlay");
        static void MainMenuReadable()
        {
            var surface = Object.FindObjectsByType<HtmlSurface>().First(s => s.name == "MenuHtml");
            Readable(surface);
            var brand = surface.Element("menu-brand"); var actions = surface.Element("menu-actions");
            var a = new Vector3[4]; var b = new Vector3[4]; brand.GetWorldCorners(a); actions.GetWorldCorners(b);
            Assert.Greater(a[0].y, b[1].y, "Wordmark and main actions overlap");
            foreach (var id in new[] { "continue", "levels", "album", "settings" }) Assert.IsTrue(Find(id).interactable);
        }
        static void Readable(HtmlSurface surface)
        {
            foreach (var text in surface.GetComponentsInChildren<TMP_Text>())
            {
                if (!text.gameObject.activeInHierarchy || text.rectTransform.rect.width <= 0) continue;
                text.ForceMeshUpdate();
                Assert.LessOrEqual(text.GetRenderedValues().x, text.rectTransform.rect.width + 3, "Text overflow: " + text.text);
                Assert.LessOrEqual(text.GetRenderedValues().y, text.rectTransform.rect.height + 3, "Text vertically clipped: " + text.text);
                Assert.IsFalse(text.text.Contains("settings.summary.")); Assert.IsFalse(text.text.Contains("legal."));
            }
        }
        static IEnumerator Boot()
        {
            yield return SceneManager.LoadSceneAsync("Boot");
            yield return PrivacyBootTestSupport.EnterGame();
            var deadline = Time.realtimeSinceStartup + 90;
            while (Object.FindAnyObjectByType<QuietCampBootstrap>()?.StartupReady != true && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(Object.FindAnyObjectByType<QuietCampBootstrap>().StartupReady);
            yield return Frames(20);
        }
        [UnityTest, Timeout(900000)] public IEnumerator SettingsNavigationAndLegalPlaceholdersAcrossPhoneAndWideScreens()
        {
            if (UnityEngine.Application.isBatchMode) Assert.Ignore("Requires rendered Game View");
            Size(720, 1600); yield return Boot(); var services = QuietCampBootstrap.ServicesRef;
            services.ReducedMotion = true; services.Settings.textScale = 1.3f;
            var completed = services.Progression.CompletedCount;
            foreach (var size in new[] { new Vector2Int(720,1600), new Vector2Int(1080,1920), new Vector2Int(1280,800), new Vector2Int(2560,1080) })
            {
                Size(size.x, size.y); yield return Frames();
                foreach (var language in new[] { "uk", "en", "de" })
                {
                    services.Localization.TrySetLanguage(language);
                    foreach (var surface in Object.FindObjectsByType<HtmlSurface>()) surface.Refresh(); yield return Frames();
                    MainMenuReadable();
                    if(language == "uk") yield return Shot("menu_uk130_" + size.x + "x" + size.y);
                    Tap("settings"); yield return Frames();
                    Assert.IsNull(Find("continue"), "Settings should own the screen"); Assert.IsNull(Find("reset"));
                    Assert.NotNull(Find("language-uk")); Tap("language-" + language); yield return Frames(); Readable(Overlay());
                    if (language == "uk" || language == "de") yield return Shot("settings_root_" + language + "130_" + size.x + "x" + size.y);
                    foreach (var category in new[] { "sound", "comfort", "look", "extras", "privacy" })
                    {
                        yield return Reveal("set-cat-" + category); Tap("set-cat-" + category); yield return Frames(); Readable(Overlay());
                        Assert.NotNull(Find("settings-close"));
                        if (category == "comfort")
                        {
                            yield return Reveal("settings-advanced"); Tap("settings-advanced"); yield return Frames(); Assert.NotNull(Overlay().Element("settings.scroll"));
                            yield return Reveal("settings-advanced"); Tap("settings-advanced"); yield return Frames(); Assert.IsNull(Overlay().Element("settings.scroll"));
                        }
                        if (category == "privacy")
                        {
                            foreach (var section in new[] { "data", "analytics", "documents" }) Assert.NotNull(Find("privacy-section-" + section));
                            Assert.IsNull(Find("analytics-consent")); Assert.IsNull(Find("reset"));
                            if (language == "uk" || language == "de") yield return Shot("settings_privacy_" + language + "130_" + size.x + "x" + size.y);
                            Tap("privacy-section-documents"); yield return Frames(); Readable(Overlay());
                            foreach (var id in new[] { "privacy-policy", "legal-support", "legal-terms" })
                            { Assert.NotNull(Find(id)); Assert.IsFalse(Find(id).interactable, "Unpublished link must be disabled: " + id); }
                            yield return Reveal("legal-licenses"); Tap("legal-licenses"); yield return Frames();
                            Assert.IsTrue(Overlay().GetComponentsInChildren<TMP_Text>().Any(t => t.text.Contains("Bitstream")));
                            yield return Reveal("legal-licenses"); Tap("legal-licenses"); yield return Frames();
                            Tap("back"); yield return Frames(); Assert.NotNull(Find("privacy-section-data"));
                            Tap("privacy-section-analytics"); yield return Frames(); Readable(Overlay());
                            Assert.IsNull(Find("account-delete"));
                            var toggle = Overlay().GetComponentsInChildren<Toggle>().Single(); Assert.IsFalse(toggle.interactable);
                            yield return Reveal("privacy-details"); Tap("privacy-details"); yield return Frames();
                            yield return Reveal("privacy-details"); Tap("privacy-details"); yield return Frames();
                            yield return Reveal("analytics-clear"); Tap("analytics-clear"); yield return new WaitForSecondsRealtime(4);
                            Overlay().Element("sheet-scroll").GetComponent<ScrollRect>().verticalNormalizedPosition = 1; yield return Frames();
                            Assert.AreEqual(completed, services.Progression.CompletedCount);
                            Assert.IsFalse(services.Analytics.Collecting);
                            if (language == "uk" || language == "de") yield return Shot("settings_analytics_" + language + "130_" + size.x + "x" + size.y);
                            Tap("back"); yield return Frames();
                            Tap("privacy-section-data"); yield return Frames(); Readable(Overlay());
                            Assert.NotNull(Find("privacy-delete-request")); Assert.IsFalse(Find("privacy-delete-request").interactable);
                            yield return Reveal("reset"); Tap("reset"); yield return Frames();
                            yield return Reveal("reset-cancel"); Tap("reset-cancel"); yield return Frames(); Assert.IsNull(Find("reset-confirm"));
                            Assert.AreEqual(completed, services.Progression.CompletedCount);
                            if (language == "de") yield return Shot("settings_data_bottom_de130_" + size.x + "x" + size.y);
                            Tap("back"); yield return Frames(); Assert.NotNull(Find("privacy-section-data"));
                        }
                        else if ((category == "comfort" || category == "look") && language == "de")
                            yield return Shot("settings_" + category + "_de130_" + size.x + "x" + size.y);
                        Tap("back"); yield return Frames(); Assert.IsNull(Find("settings-close"));
                    }
                    Tap("set-cat-sound"); yield return Frames();
                    if (size.x > size.y * 1.1f) { yield return Reveal("set-cat-look"); Tap("set-cat-look"); yield return Frames(); Assert.NotNull(Find("orientation-0")); }
                    Tap("settings-close"); yield return Frames(); Assert.NotNull(Find("continue"));
                    if (language == "de") yield return Shot("menu_de130_" + size.x + "x" + size.y);
                    if (language == "uk")
                    {
                        Tap("levels"); yield return Frames(30); Assert.NotNull(Overlay().Element("roadmap-scroll"));
                        Tap("back"); yield return Frames(); Assert.NotNull(Find("continue"));
                        Tap("album"); yield return Frames(30); Assert.IsNull(Find("continue")); Assert.NotNull(Find("back"));
                        Tap("back"); yield return Frames(); MainMenuReadable();
                    }
                }
            }
            services.Settings.textScale = 1; services.Localization.TrySetLanguage("uk");
        }
        [UnityTest] public IEnumerator GameplaySettingsCloseReturnsToPauseWithoutChangingPlacements()
        {
            if (UnityEngine.Application.isBatchMode) Assert.Ignore("Requires rendered Game View");
            Size(1080,1920); yield return Boot(); var services = QuietCampBootstrap.ServicesRef;
            services.ReducedMotion = true; services.PendingLevelId = "QC_TEST";
            yield return SceneManager.LoadSceneAsync("Camp"); yield return Frames(60);
            var session = CampSceneHost.Current.Session; session.DebugApplyWitness(); yield return Frames();
            var before = session.State.Placements.Select(p => p.Copy()).ToArray();
            Tap("pause"); yield return Frames(); Tap("settings"); yield return Frames();
            Tap("set-cat-privacy"); yield return Frames(); Assert.IsNull(Find("reset"));
            Tap("privacy-section-data"); yield return Frames(); Assert.IsNull(Find("local-data-erase"));
            services.Actions.Execute(new UiActionRequest(new UiActionId("qc.back"), UiActionSource.Button, "Gameplay"));
            yield return Frames(); Assert.NotNull(Find("privacy-section-data"));
            services.Actions.Execute(new UiActionRequest(new UiActionId("qc.back"), UiActionSource.Button, "Gameplay"));
            yield return Frames(); Assert.IsNull(Find("settings-close")); Assert.NotNull(Find("set-cat-sound"));
            Tap("set-cat-privacy"); yield return Frames();
            Tap("settings-close"); yield return Frames(); Assert.NotNull(Find("resume"));
            Tap("resume"); yield return Frames(); Assert.NotNull(Find("pause"));
            Assert.AreEqual(before.Length, session.State.Placements.Count);
            for (int i=0; i<before.Length; i++) { var after = session.State.Placements[i]; Assert.AreEqual(before[i].x, after.x); Assert.AreEqual(before[i].z, after.z); }
        }
    }
}
