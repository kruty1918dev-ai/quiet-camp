using System.Collections;
using System.IO;
using NUnit.Framework;
using QuietCamp.Presentation.World;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace QuietCamp.Tests
{
    public class AtmospherePlayModeTests
    {
        [UnityTearDown]
        public IEnumerator ReleaseBootstrap()
        {
            foreach (var boot in Object.FindObjectsByType<QuietCamp.Presentation.QuietCampBootstrap>(FindObjectsSortMode.None))
                Object.Destroy(boot.gameObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator FourPhasesRenderAndSceneReloadReleasesLayers()
        {
            // The project's existing composition root lives in Boot.
            yield return SceneManager.LoadSceneAsync("Boot");
            yield return null; yield return null;
            yield return SceneManager.LoadSceneAsync("Camp");
            for (int i = 0; i < 60 && (CampSceneHost.Current == null || CampSceneHost.Current.Atmosphere == null); i++)
                yield return null;
            Assert.IsNotNull(CampSceneHost.Current?.Atmosphere);
            var host = CampSceneHost.Current;
            var camera = Camera.main;
            Assert.AreEqual(new Rect(0, 0, 1, 1), camera.rect);
            var folder = Path.Combine(UnityEngine.Application.dataPath, "../../Temp/ai/atmosphere/screenshots");
            Directory.CreateDirectory(folder);
            foreach (var phase in new[] { "morning", "noon", "evening", "night" })
            {
                host.SetAtmospherePhase(phase);
                yield return null;
                Assert.AreEqual(phase, host.Atmosphere.PhaseId);
                Assert.IsNotNull(camera.transform.Find("ForestBackdrop"));
                var near = camera.transform.Find("NearFoliage");
                Assert.IsNotNull(near);
                Assert.IsNull(near.GetComponent<Collider>(), "Scenery must never intercept placement rays.");
                Assert.IsTrue(host.Atmosphere.ProtectedViewport.Contains(
                    (Vector2)camera.WorldToViewportPoint(Vector3.zero)));
                Assert.IsTrue(near.GetComponent<Renderer>().sharedMaterial.shader.isSupported);
                var before = CountLoop("crickets_loop");
                host.SetAtmospherePhase(phase);
                Assert.AreEqual(before, CountLoop("crickets_loop"), "Repeated phase must not duplicate a loop.");
                Capture(camera, host.Atmosphere, Path.Combine(folder, phase + ".png"));
            }
            camera.transform.Find("DistantForest").gameObject.SetActive(false);
            Capture(camera, host.Atmosphere, Path.Combine(folder, "night-no-rear.png"));
            camera.transform.Find("NearFoliage").gameObject.SetActive(false);
            Capture(camera, host.Atmosphere, Path.Combine(folder, "night-back-only.png"));
            camera.transform.Find("DistantForest").gameObject.SetActive(true);
            camera.transform.Find("NearFoliage").gameObject.SetActive(true);
            yield return SceneManager.LoadSceneAsync("MainMenu");
            yield return null;
            Assert.IsNull(Object.FindFirstObjectByType<CampAtmosphere>());
            Assert.AreEqual(0, CountLoop("crickets_loop"), "Night loop leaked into menu.");
            UnityEngine.UI.Button start = null;
            foreach (var button in Object.FindObjectsByType<UnityEngine.UI.Button>(FindObjectsSortMode.None))
                if (button.name == "Btn_menu_start" || button.name == "Btn_menu_continue") start = button;
            Assert.IsNotNull(start, "Menu must expose the canonical start/continue action.");
            start.onClick.Invoke();
            var deadline = Time.realtimeSinceStartup + 15;
            while ((CampSceneHost.Current == null || CampSceneHost.Current.Atmosphere == null)
                && Time.realtimeSinceStartup < deadline) yield return null;
            yield return null;
            Assert.AreEqual(1, Object.FindObjectsByType<CampAtmosphere>(FindObjectsSortMode.None).Length);
            Assert.IsNotNull(Camera.main.transform.Find("ForestBackdrop"));
        }

        static int CountLoop(string clip)
        {
            var count = 0;
            foreach (var source in Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None))
                if (source.isPlaying && source.loop && source.clip != null && source.clip.name == clip) count++;
            return count;
        }

        static void Capture(Camera camera, CampAtmosphere atmosphere, string path)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            var previous = camera.targetTexture;
            var active = RenderTexture.active;
            var rt = new RenderTexture(768, 1366, 24);
            var image = new Texture2D(768, 1366, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = rt;
                atmosphere.RefreshLayout();
                camera.Render();
                RenderTexture.active = rt;
                image.ReadPixels(new Rect(0, 0, 768, 1366), 0, 0);
                image.Apply();
                var pixels = image.GetPixels32();
                int pink = 0;
                for (int i = 0; i < pixels.Length; i += 17)
                    if (pixels[i].r > 180 && pixels[i].b > 180 && pixels[i].g < 90) pink++;
                Assert.Less((float)pink / (pixels.Length / 17), .005f, "Missing shader rendered magenta.");
                File.WriteAllBytes(path, image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = previous;
                RenderTexture.active = active;
                rt.Release();
                Object.Destroy(rt); Object.Destroy(image);
                atmosphere.RefreshLayout();
            }
        }
    }
}
