#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using QuietCamp.Infrastructure;
using QuietCamp.Presentation;
using QuietCamp.Presentation.UI;
using QuietCamp.Presentation.World;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace QuietCamp.Tests
{
    /// <summary>Rendered documentation capture; requires a separate QA product/save identity.</summary>
    public sealed class GithubDocumentationCaptureTests
    {
        readonly ScreenshotPlayModeTest _input = new ScreenshotPlayModeTest();
        [SetUp] public void Input() => _input.SyntheticInput();
        [TearDown] public void RestoreInput() => _input.RestoreInput();
        static readonly BindingFlags PrivateStatic = BindingFlags.Static | BindingFlags.NonPublic;
        static readonly BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        static MenuScreens Menu => (MenuScreens)typeof(MenuSceneHost).GetField("_screens", PrivateInstance)
            .GetValue(Object.FindAnyObjectByType<MenuSceneHost>());
        static IEnumerator Settle(int frames = 45) { for (int i = 0; i < frames; i++) yield return null; }
        static void Size(int w, int h) => typeof(ScreenshotPlayModeTest).GetMethod("SetGameViewSize", PrivateStatic)
            .Invoke(null, new object[] { w, h });
        static void Click(string id) => typeof(ScreenshotPlayModeTest).GetMethod("ClickButton", PrivateStatic)
            .Invoke(null, new object[] { "<button #" + id + ">" });
        static bool Exists(string id) => Object.FindObjectsByType<Button>(FindObjectsSortMode.None)
            .Any(b => b.gameObject.activeInHierarchy && b.name == "<button #" + id + ">");
        static IEnumerator Shot(string name)
        {
            string directory = Path.Combine(Directory.GetCurrentDirectory(), "Screenshots", "Documentation2026-10-08");
            yield return (IEnumerator)typeof(ScreenshotPlayModeTest).GetMethod("Shot", PrivateStatic)
                .Invoke(null, new object[] { directory, name });
            var services = QuietCampBootstrap.ServicesRef;
            var camera = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).FirstOrDefault(c => c.enabled);
            var context = JObject.Parse(UnityEditor.SessionState.GetString("QcDocsCapture.Context", "{}"));
            var metadata = new JObject {
                ["kind"] = "native-unity-game-view", ["capturedUtc"] = DateTime.UtcNow.ToString("o"),
                ["unityVersion"] = UnityEngine.Application.unityVersion,
                ["width"] = Screen.width, ["height"] = Screen.height,
                ["scene"] = SceneManager.GetActiveScene().name,
                ["levelId"] = CampSceneHost.Current?.Session?.Level?.id,
                ["qualityProfile"] = QualitySettings.names[QualitySettings.GetQualityLevel()],
                ["graphicsDevice"] = SystemInfo.graphicsDeviceType.ToString(),
                ["cameraOrthographic"] = camera != null && camera.orthographic,
                ["cameraEuler"] = camera == null ? null : JToken.FromObject(new[] { camera.transform.eulerAngles.x, camera.transform.eulerAngles.y, camera.transform.eulerAngles.z }),
                ["qaProduct"] = UnityEngine.Application.productName,
                ["syntheticProgress"] = true, ["fixture"] = "GithubDocumentationCaptureTests",
                ["source"] = context,
                ["realStoreConnected"] = false, ["devicePerformanceMeasured"] = false
            };
            File.WriteAllText(Path.Combine(directory, name + ".json"), metadata.ToString());
            Debug.Log("[QC-DOCS] Captured " + name);
        }

        [UnityTest]
        public IEnumerator CaptureMenusPanelsAndSeasonalCamps()
        {
            if (UnityEngine.Application.isBatchMode) Assert.Ignore("Requires rendered Game View.");
            Assert.That(UnityEngine.Application.productName, Does.Contain("DocsQA"));
            Size(720, 1600);
            yield return SceneManager.LoadSceneAsync("Boot");
            yield return Settle(60);
            yield return Shot("00-boot-policy");
            yield return PrivacyBootTestSupport.EnterGame();
            var services = QuietCampBootstrap.ServicesRef;
            services.Tutorial.Skip();
            services.Progression.Restore(LevelLoader.MvpLevelIds().Take(5).ToArray(), "QC006", services.Progression.CosmeticFlags);
            services.Localization.TrySetLanguage("uk"); services.Settings.language = "uk";
            services.Settings.master = 0; services.Audio?.SetBusVolume(Kruty1918.Audio.AudioBus.Master, 0);
            services.Settings.quality = 3; services.EffectiveQuality = 2; QualitySettings.SetQualityLevel(2, true);
            var adaptive = Object.FindAnyObjectByType<AdaptiveCampQuality>(); if (adaptive != null) adaptive.enabled = false;
            yield return SceneManager.LoadSceneAsync("MainMenu"); yield return Settle(90);
            yield return Shot("01-main-menu");
            Click("settings"); yield return Settle(); yield return Shot("02-settings-home");
            foreach (var category in new[] { "sound", "comfort", "look", "privacy", "extras" })
            {
                if (!Exists("set-cat-" + category)) continue;
                Click("set-cat-" + category); yield return Settle(); yield return Shot("settings-" + category);
                if (category == "privacy")
                    foreach (var section in new[] { "data", "analytics", "documents" })
                    {
                        Click("privacy-section-" + section); yield return Settle(); yield return Shot("privacy-" + section);
                        Click("back"); yield return Settle();
                    }
                Click("back"); yield return Settle();
            }
            Menu.Show("Economy"); yield return Settle(); yield return Shot("03-economy");
            yield return SceneManager.LoadSceneAsync("MainMenu"); yield return Settle();
            Click("levels"); yield return Settle(90); yield return Shot("04-roadmap");
            Click("journeys"); yield return Settle(); yield return Shot("05-journeys");
            Click("journey-garden"); yield return Settle(60); yield return Shot("06-journey-preview");
            yield return SceneManager.LoadSceneAsync("MainMenu"); yield return Settle();
            Click("levels"); yield return Settle(60); Click("bonus-5"); yield return Settle(); yield return Shot("07-bonus-preview");
            services.PendingLevelId = "QC007";
            yield return SceneManager.LoadSceneAsync("Camp"); yield return Settle(90);
            var host = CampSceneHost.Current; Assert.IsTrue(host.IsReady);
            yield return Shot("08-gameplay-guest-card");
            Click("guests"); yield return Settle(); yield return Shot("09-guest-list");
            Click("back"); yield return Settle();
            host.Session.DebugApplyWitness(); yield return Settle(); yield return Shot("10-gameplay-ready");
            Click("pause"); yield return Settle(); yield return Shot("11-pause");
            Click("signs"); yield return Settle(); yield return Shot("12-wish-legend");
            Click("back"); yield return Settle();
            Click("settings"); yield return Settle(); yield return Shot("13-camp-settings");
            Click("back"); yield return Settle(); Click("resume"); yield return Settle();
            Click("check"); yield return Settle(150); Assert.IsTrue(host.Session.IsCompleted);
            yield return Shot("14-completion");
            yield return SceneManager.LoadSceneAsync("MainMenu"); yield return Settle(60);
            Click("album"); yield return Settle(90); yield return Shot("15-album");
            Click("album-stay"); yield return Settle(); yield return Shot("16-album-quiet");
            Size(1280, 800);
            var ids = new[] { "QC001", "QC007", "gen:qc_camp:9", "gen:qc_camp:14" };
            var seasons = new[] { "spring", "summer", "autumn", "winter" };
            for (int i = 0; i < ids.Length; i++)
            {
                services.PendingLevelId = ids[i]; yield return SceneManager.LoadSceneAsync("Camp"); yield return Settle(90);
                host = CampSceneHost.Current; Assert.IsTrue(host.IsReady);
                host.Session.DebugApplyWitness(); yield return Settle(60);
                Assert.IsTrue(QuietCamp.Domain.RuleEvaluator.Evaluate(host.Session.Level, host.Session.State.Placements).IsSolved);
                yield return Shot("season-" + seasons[i]);
            }
        }
    }
}

#endif
