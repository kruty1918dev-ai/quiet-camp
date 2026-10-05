using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using Kruty1918.Audio;
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
    public class CozySystemsPlayModeTests
    {
        static IEnumerator Frames(int count) { for (int i = 0; i < count; i++) yield return null; }
        static void Size(int width, int height) => typeof(ScreenshotPlayModeTest).GetMethod("SetGameViewSize", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { width, height });
        static IEnumerator Shot(string name) => (IEnumerator)typeof(ScreenshotPlayModeTest).GetMethod("Shot", BindingFlags.Static | BindingFlags.NonPublic)
            .Invoke(null, new object[] { Path.Combine(Directory.GetCurrentDirectory(), "Screenshots/CozySystems"), name });
        static void Tap(string id)
        {
            var button = Object.FindObjectsByType<Button>().FirstOrDefault(b => b.name == "<button #" + id + ">" && b.gameObject.activeInHierarchy);
            Assert.IsNotNull(button, "Missing button " + id); Assert.IsTrue(button.interactable); button.onClick.Invoke();
        }

        [UnityTest]
        public IEnumerator BootRevealsUsableNativeMenuAndPrivacyInThreeLanguages()
        {
            if (UnityEngine.Application.isBatchMode) Assert.Ignore("Requires rendered Game View");
            Size(720, 1600);
            foreach (var old in Object.FindObjectsByType<QuietCampBootstrap>()) Object.Destroy(old.gameObject);
            yield return Frames(2);
            yield return SceneManager.LoadSceneAsync("Boot");
            yield return PrivacyBootTestSupport.EnterGame();
            Assert.IsNotNull(Object.FindAnyObjectByType<BootCampView>(), "The first frame needs a native game-owned boot view.");
            yield return Shot("01_boot_720x1600");
            var deadline = Time.realtimeSinceStartup + 20;
            while (!Object.FindAnyObjectByType<QuietCampBootstrap>().StartupReady && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(MenuSceneHost.Current.UiReady);
            Assert.IsNull(Object.FindAnyObjectByType<BootCampView>(), "Boot must release its canvas after the real UI is usable.");
            Assert.AreEqual(1, Object.FindObjectsByType<AudioListener>().Count(l => l.enabled));
            Assert.AreEqual(1, Object.FindObjectsByType<UnityEngine.EventSystems.EventSystem>().Length);
            var services = QuietCampBootstrap.ServicesRef;
            Assert.IsFalse(services.Analytics.Available); Assert.IsFalse(services.Ads.IsReady);
            Tap("settings"); yield return Frames(20);
            Tap("set-cat-privacy"); yield return Frames(10);
            foreach (var language in new[] { "uk", "en", "de" })
            {
                services.Localization.TrySetLanguage(language); services.Settings.textScale = 1.3f;
                LocalizedLabel.TextScale = 1.3f; Size(language == "uk" ? 720 : 1080, language == "uk" ? 1600 : 1920);
                yield return Frames(15);
                var consent = Object.FindObjectsByType<Toggle>().FirstOrDefault(t => t.name.Contains("analytics-consent"));
                Assert.IsNotNull(consent); Assert.IsFalse(consent.interactable); Assert.IsFalse(consent.isOn);
                yield return Shot("02_privacy_" + language + "_130");
            }
            Tap("back"); yield return Frames(5); Tap("set-cat-extras"); yield return Frames(10);
            yield return Shot("03_optional_decorations_de_130");
            Assert.IsFalse(Object.FindObjectsByType<Button>().Any(b => b.name.Contains("video-bonus")), "An unconfigured release must not offer an unusable/fake ad.");
            services.Settings.textScale = 1; LocalizedLabel.TextScale = 1;
            services.Localization.TrySetLanguage("uk"); services.ReducedMotion = false; Size(720, 1600);
        }

        [UnityTest]
        public IEnumerator ContextParticlesRespondToActionsWindRainQualityAndReducedMotion()
        {
            if (UnityEngine.Application.isBatchMode) Assert.Ignore("Requires rendered Game View");
            Size(720, 1600);
            if (QuietCampBootstrap.ServicesRef == null)
            {
                yield return SceneManager.LoadSceneAsync("Boot");
            yield return PrivacyBootTestSupport.EnterGame();
                var deadline = Time.realtimeSinceStartup + 20;
                while (!Object.FindAnyObjectByType<QuietCampBootstrap>().StartupReady && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.IsTrue(Object.FindAnyObjectByType<QuietCampBootstrap>().StartupReady);
            }
            var services = QuietCampBootstrap.ServicesRef;
            services.Settings.quality = 3; services.ReducedMotion = false; services.Settings.fireflyLantern = true;
            services.PendingLevelId = "QC_TEST";
            yield return SceneManager.LoadSceneAsync("Camp"); yield return Frames(30);
            var host = CampSceneHost.Current;
            Assert.IsNotNull(host); Assert.IsTrue(host.IsReady);
            var effects = host.GetComponent<CampFeedbackEffects>();
            effects.Placement(Vector3.zero); effects.Lift(Vector3.one);
            yield return Frames(2); Assert.Greater(effects.TotalParticles, 0);
            yield return Shot("04_placement_and_lantern");
            Assert.IsNotNull(GameObject.Find("FireflyLantern"));
            var weather = host.Atmosphere.Weather;
            weather.Advance(76, 2, false, Vector2.right);
            for (int i = 0; i < 100; i++) weather.Advance(.04f, 2, false, Vector2.right);
            Assert.Greater(weather.RainAmount, .1f);
            yield return Shot("05_rain_and_fire");
            foreach (var system in weather.GetComponentsInChildren<ParticleSystem>()) Assert.LessOrEqual(system.particleCount, system.main.maxParticles);
            host.SetAtmospherePhase("evening");
            host.Session.DebugApplyWitness();
            yield return new WaitForSeconds(4);
            var embers = host.Atmosphere.Particles.transform.Find("FireEmbers").GetComponent<ParticleSystem>();
            Assert.IsTrue(host.Atmosphere.RainShelter.Extinguished, "Rain must quench the campfire.");
            Assert.AreEqual(0,embers.emission.rateOverTime.constant);
            Assert.Greater(host.Atmosphere.RainShelter.InteriorWarmth,.2f);
            yield return Shot("06_rain_quenched_fire_warm_tents");
            services.ReducedMotion = true; yield return Frames(5);
            Assert.AreEqual(0, effects.ActiveParticles);
            foreach (var system in weather.GetComponentsInChildren<ParticleSystem>()) Assert.AreEqual(0, system.particleCount);
            var random = JsonUtility.ToJson(UnityEngine.Random.state);
            services.Audio.Play("sfx.tent.lift"); services.Audio.Play("sfx.tent.remove");
            Assert.AreEqual(random, JsonUtility.ToJson(UnityEngine.Random.state), "Foley must not perturb gameplay RNG.");
            services.ReducedMotion = false; services.Settings.quality = 0; services.Settings.fireflyLantern = false;
            yield return SceneManager.LoadSceneAsync("MainMenu"); yield return Frames(10);
        }

        [UnityTest]
        public IEnumerator BootReloadKeepsServicesAndMusicAndEventuallyRestoresMenu()
        {
            if (QuietCampBootstrap.ServicesRef == null) { yield return SceneManager.LoadSceneAsync("Boot");
            yield return PrivacyBootTestSupport.EnterGame(); yield return Frames(100); }
            var services = QuietCampBootstrap.ServicesRef;
            yield return SceneManager.LoadSceneAsync("Boot");
            yield return PrivacyBootTestSupport.EnterGame();
            float deadline = Time.realtimeSinceStartup + 20;
            while ((SceneManager.GetActiveScene().name != "MainMenu" || MenuSceneHost.Current == null || !MenuSceneHost.Current.UiReady) && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.AreSame(services, QuietCampBootstrap.ServicesRef);
            Assert.IsTrue(MenuSceneHost.Current.UiReady); Assert.AreEqual(1, Object.FindObjectsByType<QuietCampBootstrap>().Length);
            var music = Object.FindObjectsByType<AudioSource>().Where(s => s.clip != null && s.clip.name == "clearing_music" && s.isPlaying).ToArray();
            Assert.AreEqual(1, music.Length, "Reload must not duplicate or drop the persistent music bed.");
        }
        [Test]
        public void CozyAudioVariantsAreMonoFoleyAndStreamingStereoMusic()
        {
            var catalog = QuietCampAudioCatalog.Load();
            foreach (var key in new[] { "sfx.tent.lift", "sfx.tent.settle", "sfx.tent.remove", "placement.rotate" })
            { Assert.IsTrue(catalog.TryGet(key, out var definition)); Assert.AreEqual(3, definition.Variants.Length); Assert.IsTrue(definition.Variants.All(c => c != null && c.channels == 1)); }
            Assert.IsTrue(catalog.TryGet("music.clearing", out var music)); Assert.IsTrue(music.Loop); Assert.AreEqual(AudioBus.Music, music.Bus);
            Assert.AreEqual(2, music.Clip.channels); Assert.AreEqual(AudioClipLoadType.Streaming, music.Clip.loadType);
        }
    }
}
