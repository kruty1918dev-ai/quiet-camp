using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using QuietCamp.Presentation;
using QuietCamp.Presentation.World;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace QuietCamp.Tests
{
    /// <summary>
    /// Captures real gameplay screenshots into QuietCamp/Screenshots/.
    /// Runs as a PlayMode test so the batchmode editor pumps frames.
    /// </summary>
    public class ScreenshotPlayModeTest
    {
        const int W = 1080, H = 1920;

        [UnityTest]
        public IEnumerator CaptureMenuAndCamp()
        {
            var outDir = System.IO.Path.Combine(Directory.GetCurrentDirectory(), "Screenshots");
            Directory.CreateDirectory(outDir);

            yield return SceneManager.LoadSceneAsync("Boot", LoadSceneMode.Single);
            // Bootstrap redirects Boot→MainMenu only on its first Start;
            // if it already exists (shared play session), go directly.
            yield return Settle(10);
            if (SceneManager.GetActiveScene().name != "MainMenu")
                yield return SceneManager.LoadSceneAsync("MainMenu", LoadSceneMode.Single);
            yield return WaitForActiveScene("MainMenu", 20f);
            yield return Settle(60);
            DumpUi();
            Shot(outDir, "01_main_menu");

            yield return SceneManager.LoadSceneAsync("Camp", LoadSceneMode.Single);
            yield return WaitForActiveScene("Camp", 20f);
            yield return new WaitUntil(() => CampSceneHost.Current != null);
            yield return Settle(60);
            Shot(outDir, "02_camp_day");

            Assert.IsTrue(File.Exists(System.IO.Path.Combine(outDir, "01_main_menu.png")));
            Assert.IsTrue(File.Exists(System.IO.Path.Combine(outDir, "02_camp_day.png")));
        }

        static IEnumerator WaitForActiveScene(string name, float timeout)
        {
            var deadline = Time.realtimeSinceStartup + timeout;
            while (SceneManager.GetActiveScene().name != name)
            {
                if (Time.realtimeSinceStartup > deadline)
                    Assert.Fail($"Scene {name} did not become active.");
                yield return null;
            }
        }

        static IEnumerator Settle(int frames)
        {
            for (var i = 0; i < frames; i++) yield return null;
        }

        static void DumpUi()
        {
            foreach (var c in Object.FindObjectsByType<Canvas>())
                Debug.Log($"[QC-UI] canvas {c.name} enabled={c.enabled} mode={c.renderMode} order={c.sortingOrder}");
            foreach (var t in Object.FindObjectsByType<TMPro.TMP_Text>())
            {
                var r = t.rectTransform.rect;
                Debug.Log($"[QC-UI] tmp {TmpPath(t.transform)} text='{t.text}' active={t.gameObject.activeInHierarchy} enabled={t.enabled} font={(t.font?t.font.name:"null")} size={t.fontSize} rect={r.width}x{r.height}");
            }
        }

        static string TmpPath(Transform t)
        {
            var s = t.name;
            while (t.parent != null) { t = t.parent; s = t.name + "/" + s; }
            return s;
        }

        static void Shot(string dir, string name)
        {
            var cam = Camera.main
                ?? Object.FindObjectsByType<Camera>().FirstOrDefault();
            Assert.IsNotNull(cam, "No camera for screenshot " + name);

            var canvases = Object.FindObjectsByType<Canvas>();
            var prevModes = canvases.Select(c => c.renderMode).ToArray();
            var prevCam = canvases.Select(c => c.worldCamera).ToArray();
            foreach (var c in canvases)
            {
                c.renderMode = RenderMode.ScreenSpaceCamera;
                c.worldCamera = cam;
                c.planeDistance = 1f;
            }

            var rt = new RenderTexture(W, H, 24);
            var prevTarget = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            cam.targetTexture = prevTarget;
            rt.Release();

            for (var i = 0; i < canvases.Length; i++)
            {
                canvases[i].renderMode = prevModes[i];
                canvases[i].worldCamera = prevCam[i];
            }

            var path = System.IO.Path.Combine(dir, name + ".png");
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            Debug.Log($"[QuietCamp] Screenshot → {path}");
        }
    }
}
