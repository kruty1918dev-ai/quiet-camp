using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using Kruty1918.Atmos;
using Newtonsoft.Json;
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
    public class MenuBackdropPlayModeTests
    {
        static IEnumerator Frames(int count) { while (count-- > 0) yield return null; }
        static IEnumerator ColdBoot()
        {
            foreach (var old in Object.FindObjectsByType<QuietCampBootstrap>()) Object.Destroy(old.gameObject);
            yield return Frames(3);
            yield return SceneManager.LoadSceneAsync("Boot");
            yield return PrivacyBootTestSupport.EnterGame();
            var deadline = Time.realtimeSinceStartup + 25;
            while (!(Object.FindAnyObjectByType<QuietCampBootstrap>()?.StartupReady ?? false) && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(MenuSceneHost.Current?.UiReady ?? false);
            yield return Frames(10);
        }
        static void Size(int width, int height) => typeof(ScreenshotPlayModeTest).GetMethod("SetGameViewSize", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { width, height });
        static IEnumerator Shot(string name) => (IEnumerator)typeof(ScreenshotPlayModeTest).GetMethod("Shot", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { Path.Combine(Directory.GetCurrentDirectory(), "Screenshots/MenuBackdrops"), name });
        [UnityTest] public IEnumerator ColdLaunchChangesGladeAndMenuReloadKeepsItWithoutChangingProgress()
        {
            yield return ColdBoot();
            var first = MenuSceneHost.Current.BackgroundLevel.id;
            var services = QuietCampBootstrap.ServicesRef;
            var session = JsonConvert.SerializeObject(services.Save.Session);
            var progress = JsonConvert.SerializeObject(services.Save.Progress);
            yield return SceneManager.LoadSceneAsync("MainMenu"); yield return Frames(20);
            Assert.AreEqual(first, MenuSceneHost.Current.BackgroundLevel.id);
            Assert.AreEqual(session, JsonConvert.SerializeObject(services.Save.Session));
            Assert.AreEqual(progress, JsonConvert.SerializeObject(services.Save.Progress));
            for (int launch = 0; launch < 3; launch++)
            {
                var previous = MenuSceneHost.Current.BackgroundLevel.id;
                yield return ColdBoot();
                Assert.AreNotEqual(previous, MenuSceneHost.Current.BackgroundLevel.id);
                var saved = new SaveAdapter(); Assert.IsTrue(saved.Load(out _));
                Assert.AreEqual(MenuSceneHost.Current.BackgroundLevel.id, saved.Settings.lastMenuBackdropId);
                Assert.AreEqual(session, JsonConvert.SerializeObject(saved.Session));
                Assert.AreEqual(progress, JsonConvert.SerializeObject(saved.Progress));
            }
        }
        [UnityTest] public IEnumerator RealMeadowPinesEveningAndNightBackdropsMatchGameplayAndFrameBothDisplays()
        {
            if (UnityEngine.Application.isBatchMode) Assert.Ignore("Requires rendered Game View");
            Size(720, 1600); yield return ColdBoot();
            var services = QuietCampBootstrap.ServicesRef; services.ReducedMotion = true; services.Settings.quality = 2;
            var catalog = AtmosphereCatalog.Load();
            foreach (var id in new[] { "QC001", "gen:qc_camp:4", "gen:qc_camp:14", "QC005" })
            {
                var level = LevelLoader.Load(id); var before = JsonConvert.SerializeObject(level);
                typeof(GameServices).GetProperty("MenuBackdrop").SetValue(services, level);
                yield return SceneManager.LoadSceneAsync("MainMenu"); yield return Frames(25);
                var host = MenuSceneHost.Current; Assert.IsTrue(host.UiReady);
                Assert.AreEqual(id, host.BackgroundLevel.id);
                var atmosphere = Object.FindObjectsByType<CampAtmosphere>().Single();
                Assert.AreEqual(catalog.Resolve(id, level.lighting).Id, atmosphere.PhaseId);
                var world = SceneManager.GetActiveScene().GetRootGameObjects().Single(go => go.name == "World");
                Assert.AreEqual(level.width * level.height, world.GetComponentsInChildren<Transform>().Count(t => t.name.StartsWith("Cell_")));
                Assert.AreEqual(level.noise.Length > 0, world.GetComponentsInChildren<FireVisual>().Length > 0);
                Assert.IsFalse(world.GetComponentsInChildren<Collider>().Any(c => c.enabled));
                Assert.AreEqual(MenuDiorama.DecorativePlacements(level).Length, world.GetComponentsInChildren<TentCloth>().Length);
                Assert.IsNotNull(world.GetComponentInChildren<VisibleForestFloor>());
                Assert.IsTrue(Object.FindObjectsByType<Button>().Any(b => b.name == "<button #continue>" || b.name == "<button #start>"));
                foreach (var size in new[] { new Vector2Int(720, 1600), new Vector2Int(1280, 800) })
                {
                    Size(size.x, size.y); yield return Frames(20);
                    yield return Shot(id.Replace(':', '_') + "_" + size.x + "x" + size.y);
                    Assert.AreEqual(size.x, Screen.width); Assert.AreEqual(size.y, Screen.height);
                    Assert.IsTrue(atmosphere.ProtectedViewport.Contains((Vector2)Camera.main.WorldToViewportPoint(Vector3.zero)));
                }
                Assert.AreEqual(before, JsonConvert.SerializeObject(level));
            }
        }
    }
}
