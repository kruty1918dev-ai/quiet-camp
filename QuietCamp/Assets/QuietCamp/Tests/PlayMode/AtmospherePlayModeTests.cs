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

        // Rendering all phases and changing scenes can exceed the default
        // three-minute limit on a cold shader cache or a busy editor host.
        [UnityTest, Timeout(600000)]
        public IEnumerator FourPhasesRenderAndSceneReloadReleasesScenery()
        {
            // The project's existing composition root lives in Boot.
            yield return SceneManager.LoadSceneAsync("Boot");
            yield return PrivacyBootTestSupport.EnterGame();
            yield return null; yield return null;
            yield return SceneManager.LoadSceneAsync("Camp");
            for (int i = 0; i < 60 && (CampSceneHost.Current == null || CampSceneHost.Current.Atmosphere == null); i++)
                yield return null;
            Assert.IsNotNull(CampSceneHost.Current?.Atmosphere);
            var host = CampSceneHost.Current;
            var camera = Camera.main;
            host.Session.DebugApplyWitness();
            yield return new WaitForSeconds(.5f);
            Assert.AreEqual(new Rect(0, 0, 1, 1), camera.rect);
            var folder = Path.Combine(UnityEngine.Application.dataPath, "../Screenshots/scenery");
            Directory.CreateDirectory(folder);
            foreach (var phase in new[] { "morning", "noon", "evening", "night" })
            {
                host.SetAtmospherePhase(phase);
                yield return new WaitForSeconds(4f);
                Assert.AreEqual(phase, host.Atmosphere.PhaseId);
                Assert.IsNull(camera.transform.Find("ForestBackdrop"), "Scenery must use the camp perspective.");
                Assert.IsNull(camera.transform.Find("NearFoliage"), "Foreground must not be a screen overlay.");
                Assert.IsTrue(host.Atmosphere.ProtectedViewport.Contains(
                    (Vector2)camera.WorldToViewportPoint(Vector3.zero)));
                var meadow = Object.FindFirstObjectByType<MeadowSurface>();
                Assert.IsNotNull(meadow);
                Assert.IsTrue(meadow.GetComponent<Renderer>().sharedMaterial.shader.isSupported);
                var before = CountLoop("crickets_loop");
                host.SetAtmospherePhase(phase);
                Assert.AreEqual(before, CountLoop("crickets_loop"), "Repeated phase must not duplicate a loop.");
                Capture(camera, host.Atmosphere, Path.Combine(folder, phase + ".png"));
            }
            Capture(camera, host.Atmosphere, Path.Combine(folder, "night-narrow.png"), 720, 1600);
            Capture(camera, host.Atmosphere, Path.Combine(folder, "night-wide.png"), 1200, 1600);
            yield return SceneManager.LoadSceneAsync("MainMenu");
            yield return null;
            // The menu owns its own living camp diorama now — reload must
            // release the camp's scenery so exactly one atmosphere remains.
            Assert.AreEqual(1,
                Object.FindObjectsByType<CampAtmosphere>(FindObjectsSortMode.None).Length,
                "Camp atmosphere leaked into the menu scene.");
            Assert.AreEqual(0, CountLoop("crickets_loop"), "Night loop leaked into menu.");
            var menuAtmosphere = Object.FindFirstObjectByType<CampAtmosphere>();
            Assert.AreEqual(1, Object.FindObjectsByType<MeadowSurface>(FindObjectsSortMode.None).Length);
            Assert.IsNull(Camera.main.transform.Find("ForestBackdrop"));
            yield return new WaitForSeconds(4f);
            Capture(Camera.main, menuAtmosphere, Path.Combine(folder, "menu.png"));
            UnityEngine.UI.Button start = null;
            foreach (var button in Object.FindObjectsByType<UnityEngine.UI.Button>(FindObjectsSortMode.None))
                if (button.name == "<button #continue>" || button.name == "<button #start>") start = button;
            Assert.IsNotNull(start, "Menu must expose the canonical start/continue action.");
            start.onClick.Invoke();
            var deadline = Time.realtimeSinceStartup + 15;
            while ((CampSceneHost.Current == null || CampSceneHost.Current.Atmosphere == null)
                && Time.realtimeSinceStartup < deadline) yield return null;
            yield return null;
            Assert.AreEqual(1, Object.FindObjectsByType<CampAtmosphere>(FindObjectsSortMode.None).Length);
            Assert.IsNull(Camera.main.transform.Find("ForestBackdrop"));
            Assert.AreEqual(1, Object.FindObjectsByType<MeadowSurface>(FindObjectsSortMode.None).Length);
        }

        static int CountLoop(string clip)
        {
            var count = 0;
            foreach (var source in Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None))
                if (source.isPlaying && source.loop && source.clip != null && source.clip.name == clip) count++;
            return count;
        }

        static void AssertGroundCoversCamera(Camera camera)
        {
            var meadow = Object.FindFirstObjectByType<MeadowSurface>();
            var bounds = meadow.GetComponent<MeshFilter>().sharedMesh.bounds;
            var plane = new Plane(Vector3.up, new Vector3(0, MeadowSurface.GroundY, 0));
            for (var i = 0; i < 4; i++)
            {
                var ray = camera.ViewportPointToRay(new Vector3(i & 1, (i >> 1) & 1, 0));
                Assert.IsTrue(plane.Raycast(ray, out var distance));
                var point = meadow.transform.InverseTransformPoint(ray.GetPoint(distance));
                Assert.Less(Mathf.Abs(point.x), bounds.extents.x, "Ground leaves an exposed backdrop at a screen corner.");
                Assert.Less(Mathf.Abs(point.z), bounds.extents.z, "Ground leaves an exposed backdrop at a screen corner.");
            }
        }

        static void Capture(Camera camera, CampAtmosphere atmosphere, string path, int width = 768, int height = 1366)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            var previous = camera.targetTexture;
            var active = RenderTexture.active;
            var rt = new RenderTexture(width, height, 24);
            var image = new Texture2D(width, height, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = rt;
                atmosphere.RefreshLayout();
                AssertGroundCoversCamera(camera);
                camera.Render();
                RenderTexture.active = rt;
                image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
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
