using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using QuietCamp.Application;
using QuietCamp.Infrastructure;
using QuietCamp.Presentation;
using QuietCamp.Presentation.UI;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace QuietCamp.Tests
{
    public sealed class BootPrivacyPlayModeTests
    {
        SaveAdapter _save; string _originalSettings;
        [SetUp] public void Setup()
        {
            Assert.IsTrue(UnityEngine.Application.productName.Contains("QA"), "Use isolated QA project and saves.");
            _save = new SaveAdapter(); _save.Load(out _); _originalSettings = JsonUtility.ToJson(_save.Settings);
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            foreach (var boot in Object.FindObjectsByType<QuietCampBootstrap>()) Object.Destroy(boot.gameObject);
            yield return null;
            JsonUtility.FromJsonOverwrite(_originalSettings, _save.Settings); _save.Save();
        }
        static IEnumerator Frames(int count = 8) { for (int i = 0; i < count; i++) yield return null; }
        static void Size(int w, int h) => typeof(ScreenshotPlayModeTest).GetMethod("SetGameViewSize", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { w, h });
        static IEnumerator Shot(string name) => (IEnumerator)typeof(ScreenshotPlayModeTest).GetMethod("Shot", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null,
            new object[] { Path.Combine(Directory.GetCurrentDirectory(), "Screenshots/BootPrivacy"), name });
        void ClearReceipt(string language = "uk", float scale = 1)
        { _save.Settings.privacyAcknowledgementHash = null; _save.Settings.privacyAcknowledgementRevision = null;
            _save.Settings.analyticsConsent = false; _save.Settings.language = language; _save.Settings.textScale = scale; Assert.IsTrue(_save.Save()); }
        static IEnumerator ColdBoot()
        {
            foreach (var boot in Object.FindObjectsByType<QuietCampBootstrap>()) Object.Destroy(boot.gameObject);
            yield return null; yield return SceneManager.LoadSceneAsync("Boot");
        }
        static IEnumerator WaitPanel()
        {
            float deadline = Time.realtimeSinceStartup + 60;
            while ((PrivacyBootTestSupport.Find("boot-policy-ack") == null || !PrivacyBootTestSupport.Find("boot-policy-ack").interactable) && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.NotNull(BootPrivacyPanel.Current); Assert.IsNull(BootPrivacyPanel.Current.MountError);
            Assert.NotNull(PrivacyBootTestSupport.Find("boot-policy-ack")); yield return Frames();
        }
        [UnityTest, Timeout(240000)] public IEnumerator RefusalReturnsOnNextLaunchAndAcceptancePersistsWithoutAnalytics()
        {
            Size(720,1600); ClearReceipt(); yield return ColdBoot(); yield return WaitPanel();
            Assert.IsNull(QuietCampBootstrap.ServicesRef, "Optional service graph must not exist before acknowledgement.");
            Assert.AreEqual("Boot", SceneManager.GetActiveScene().name); Assert.IsFalse(PrivacyBootTestSupport.Find("boot-policy-source").interactable);
            yield return Shot("01-first-run-draft");
            PrivacyBootTestSupport.Tap(PrivacyBootTestSupport.Find("boot-policy-exit")); yield return Frames();
            Assert.IsTrue(BootPrivacyPanel.Current.Declined); Assert.IsNull(QuietCampBootstrap.ServicesRef);
            var refused = new SaveAdapter(); refused.Load(out _); Assert.IsNull(refused.Settings.privacyAcknowledgementHash);
            yield return ColdBoot(); yield return WaitPanel();
            string hash = BootPrivacyPanel.Current.Document.ContentHash;
            PrivacyBootTestSupport.Tap(PrivacyBootTestSupport.Find("boot-policy-ack")); yield return PrivacyBootTestSupport.EnterGame();
            Assert.AreEqual(hash, QuietCampBootstrap.ServicesRef.Settings.privacyAcknowledgementHash);
            Assert.IsFalse(QuietCampBootstrap.ServicesRef.Settings.analyticsConsent);
            Assert.IsFalse(QuietCampBootstrap.ServicesRef.Analytics.Collecting);
            yield return ColdBoot();
            float deadline = Time.realtimeSinceStartup + 60;
            while (Object.FindAnyObjectByType<QuietCampBootstrap>()?.StartupReady != true && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(Object.FindAnyObjectByType<QuietCampBootstrap>().StartupReady, "Unchanged receipt should skip the panel.");
            Assert.IsNull(BootPrivacyPanel.Current);
        }
        [UnityTest, Timeout(240000)] public IEnumerator DraftIsReadableOnPhoneAndWideScreensWithApprovedLocales()
        {
            foreach (var size in new[] { new Vector2Int(720,1600), new Vector2Int(2560,1080) })
            foreach (string language in new[] { "uk", "en", "de" })
            {
                Size(size.x,size.y); ClearReceipt(language, 1.3f); yield return ColdBoot(); yield return WaitPanel();
                Assert.AreEqual(language, BootPrivacyPanel.Current.Document.Language);
                foreach (var text in BootPrivacyPanel.Current.GetComponentsInChildren<TMP_Text>())
                {
                    text.ForceMeshUpdate(); Assert.LessOrEqual(text.GetRenderedValues().x, text.rectTransform.rect.width + 3, "Horizontal overflow: " + text.text);
                    Assert.LessOrEqual(text.GetRenderedValues().y, text.rectTransform.rect.height + 3, "Vertical overflow: " + text.text);
                    var button = text.GetComponentInParent<Button>();
                    if (button != null) Assert.LessOrEqual(text.rectTransform.rect.width, ((RectTransform)button.transform).rect.width + 3, "Button label escapes its parent: " + text.text);
                }
                PrivacyBootTestSupport.Tap(PrivacyBootTestSupport.Find("boot-policy-full")); yield return Frames();
                Assert.IsTrue(BootPrivacyPanel.Current.GetComponentsInChildren<TMP_Text>().Any(t => t.text.Contains("QuietCamp")));
                yield return Shot("full-" + size.x + "x" + size.y + "-" + language);
                PrivacyBootTestSupport.Tap(PrivacyBootTestSupport.Find("boot-policy-short" ) ?? PrivacyBootTestSupport.Find("boot-policy-full")); yield return Frames();
                PrivacyBootTestSupport.Tap(PrivacyBootTestSupport.Find("boot-policy-exit")); yield return Frames();
                Assert.IsTrue(BootPrivacyPanel.Current.Declined);
            }
        }
        [UnityTest] public IEnumerator ChangedContentRequiresNewAcknowledgementBeforeServices()
        {
            Size(1080,1920); ClearReceipt();
            PrivacyAcknowledgement.Record(_save.Settings, "draft-2026-10-04-1", "different-content", "uk", true); Assert.IsTrue(_save.Save());
            yield return ColdBoot(); yield return WaitPanel();
            Assert.IsNull(QuietCampBootstrap.ServicesRef); Assert.IsFalse(BootPrivacyPanel.Current.Accepted);
        }
    }
}
