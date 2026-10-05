using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using QuietCamp.Application;
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
    public sealed class LocalDataPrivacyPlayModeTests
    {
        bool _async;
        [SetUp] public void Setup()
        {
            if (UnityEngine.Application.productName != "QuietCampLiveQA20261002")
                Assert.Ignore("Destructive flow uses the isolated QA project only");
            _async = UnityEditor.ShaderUtil.allowAsyncCompilation; UnityEditor.ShaderUtil.allowAsyncCompilation = false;
        }
        [TearDown] public void Cleanup() => UnityEditor.ShaderUtil.allowAsyncCompilation = _async;
        const BindingFlags Hidden = BindingFlags.NonPublic | BindingFlags.Static;
        static IEnumerator Frames(int n = 12) { for (int i = 0; i < n; i++) yield return null; }
        static void Size(int w, int h) => typeof(ScreenshotPlayModeTest).GetMethod("SetGameViewSize", Hidden).Invoke(null, new object[] { w, h });
        static IEnumerator Shot(string name) => (IEnumerator)typeof(ScreenshotPlayModeTest).GetMethod("Shot", Hidden).Invoke(null,
            new object[] { Path.Combine(Directory.GetCurrentDirectory(), "Screenshots/LegalData"), name });
        static Button Find(string id) => Object.FindObjectsByType<Button>().FirstOrDefault(b => b.name == "<button #" + id + ">" && b.gameObject.activeInHierarchy);
        static HtmlSurface Overlay() => Object.FindObjectsByType<HtmlSurface>().Single(s => s.name == "MenuOverlay");
        static void Tap(string id)
        {
            var button = Find(id); Assert.NotNull(button, id); Assert.IsTrue(button.interactable, id);
            var rect = (RectTransform)button.transform;
            var pointer = new PointerEventData(EventSystem.current) { position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center)) };
            var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, hits);
            Assert.IsTrue(hits.Count > 0 && (hits[0].gameObject.transform == rect || hits[0].gameObject.transform.IsChildOf(rect)), "Covered: " + id);
            Assert.That(pointer.position.x, Is.InRange(1f, Screen.width - 1f)); Assert.That(pointer.position.y, Is.InRange(1f, Screen.height - 1f));
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler);
        }
        static IEnumerator Reveal(string id)
        {
            var button = Find(id); Assert.NotNull(button, id);
            var scroll = button.GetComponentInParent<ScrollRect>();
            Canvas.ForceUpdateCanvases();
            var rect = (RectTransform)button.transform;
            var centre = scroll.viewport.InverseTransformPoint(rect.TransformPoint(rect.rect.center));
            var position = scroll.content.anchoredPosition;
            position.y += scroll.viewport.rect.center.y - centre.y;
            scroll.content.anchoredPosition = position; scroll.velocity = Vector2.zero;
            yield return Frames(4);
        }
        static void Readable()
        {
            foreach (var text in Overlay().GetComponentsInChildren<TMP_Text>())
            {
                if (text.rectTransform.rect.width <= 0) continue;
                text.ForceMeshUpdate();
                Assert.LessOrEqual(text.GetRenderedValues().x, text.rectTransform.rect.width + 3, text.text);
                Assert.LessOrEqual(text.GetRenderedValues().y, text.rectTransform.rect.height + 3, text.text);
                Assert.IsFalse(text.text.StartsWith("legal."), text.text);
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
        [UnityTest, Timeout(480000)] public IEnumerator InformationConfirmationCancellationAndRealErasure()
        {
            if (UnityEngine.Application.isBatchMode) Assert.Ignore("Requires rendered Game View");
            Size(720, 1600); yield return Boot(); var services = QuietCampBootstrap.ServicesRef;
            services.Progression.Restore(new[] { "QC001" }, "QC002", CozyRewardService.LanternFlag);
            services.Save.Album.entries = new[] { new AlbumSaveData.Entry { levelId = "QC001", order = 1 } };
            services.Save.Session.levelId = "QC002";
            var settings = services.Settings;
            settings.analyticsConsent = true; settings.analyticsPublicationRevision = "old-policy";
            services.ReducedMotion = true; settings.textScale = 1.3f; services.Save.Save();
            foreach (var size in new[] { new Vector2Int(720,1600), new Vector2Int(1280,800) })
            {
                Size(size.x, size.y); yield return Frames();
                foreach (var language in new[] { "uk", "en", "de" })
                {
                    services.Localization.TrySetLanguage(language);
                    foreach (var surface in Object.FindObjectsByType<HtmlSurface>()) surface.Refresh(); yield return Frames();
                    Tap("settings"); yield return Frames(); yield return Reveal("set-cat-privacy"); Tap("set-cat-privacy"); yield return Frames();
                    Tap("privacy-section-data"); yield return Frames();
                    yield return Reveal("legal-data-information"); Tap("legal-data-information"); yield return Frames(); Readable();
                    yield return Shot("information_" + language + "130_" + size.x + "x" + size.y);
                    yield return Reveal("legal-data-information"); Tap("legal-data-information"); yield return Frames();
                    yield return Reveal("local-data-erase"); Tap("local-data-erase"); yield return Frames(); Readable();
                    yield return Reveal("local-data-erase-confirm"); yield return Shot("erase_confirm_" + language + "130_" + size.x + "x" + size.y);
                    yield return Reveal("local-data-erase-cancel"); Tap("local-data-erase-cancel"); yield return Frames();
                    Assert.AreEqual(1, services.Progression.CompletedCount); Assert.AreEqual(1, services.Save.Album.entries.Length);
                    Assert.AreEqual(CozyRewardService.LanternFlag, services.Progression.CosmeticFlags);
                    Assert.AreEqual("old-policy", settings.analyticsPublicationRevision);
                    Tap("settings-close"); yield return Frames();
                }
            }
            // Real deletion through the same UI, including a recoverable backup and interrupted write.
            var path = Path.Combine(UnityEngine.Application.persistentDataPath, "saves", "slot00.mvs");
            File.Copy(path, path + ".bak", true); File.Copy(path, path + ".tmp", true);
            Tap("settings"); yield return Frames(); yield return Reveal("set-cat-privacy"); Tap("set-cat-privacy"); yield return Frames();
            Tap("privacy-section-data"); yield return Frames();
            yield return Reveal("local-data-erase"); Tap("local-data-erase"); yield return Frames();
            yield return Reveal("local-data-erase-confirm"); Tap("local-data-erase-confirm"); yield return Frames();
            Assert.AreSame(settings, services.Settings, "Services must retain their settings reference");
            Assert.AreEqual(0, services.Progression.CompletedCount); Assert.AreEqual(0, services.Progression.CosmeticFlags);
            Assert.IsEmpty(services.Save.Album.entries); Assert.IsNull(services.Save.Session.levelId);
            Assert.IsFalse(settings.analyticsConsent); Assert.IsFalse(services.Analytics.Collecting);
            Assert.AreEqual(1, settings.textScale); Assert.IsTrue(string.IsNullOrEmpty(settings.analyticsPublicationRevision));
            foreach (var suffix in new[] { "", ".bak", ".tmp" }) Assert.IsFalse(File.Exists(path + suffix), suffix);
            yield return Shot("erase_done");
            // Subsequent writes and reload cannot recover the deleted player's layout/cosmetics.
            services.Save.Save(); var reload = new QuietCamp.Infrastructure.SaveAdapter(); Assert.IsTrue(reload.Load(out _));
            Assert.IsEmpty(reload.Progress.completedIds); Assert.IsEmpty(reload.Album.entries); Assert.AreEqual(0, reload.Progress.cosmeticFlags);
            Tap("settings-close"); yield return Frames(); Assert.NotNull(Find("continue"));
            services.PendingLevelId = "QC001"; yield return SceneManager.LoadSceneAsync("Camp"); yield return Frames(60);
            Tap("pause"); yield return Frames(); Tap("settings"); yield return Frames();
            yield return Reveal("set-cat-privacy"); Tap("set-cat-privacy"); yield return Frames();
            Tap("privacy-section-data"); yield return Frames();
            Assert.IsNull(Find("local-data-erase"), "Destructive game reset is offered in the main menu only");
            var request = services.EraseLocalGameData(); yield return new WaitUntil(() => request.IsCompleted);
            Assert.IsFalse(request.Result); Assert.NotNull(CampSceneHost.Current.Session);
        }
    }
}
