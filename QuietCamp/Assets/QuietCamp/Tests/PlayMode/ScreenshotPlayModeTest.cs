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
            var menuMagenta = Shot(outDir, "01_main_menu");

            // Navigate to the Settings screen via the real button — exercises
            // the same action path as a user tap.
            ClickButton("Btn_menu_settings");
            yield return Settle(30);
            var settingsMagenta = Shot(outDir, "02_menu_settings");

            ClickButton("Btn_action_back"); // Settings → Main
            yield return Settle(20);
            ClickButton("Btn_menu_levels");
            yield return Settle(30);
            var levelsMagenta = Shot(outDir, "03_menu_levels");

            yield return SceneManager.LoadSceneAsync("Camp", LoadSceneMode.Single);
            yield return WaitForActiveScene("Camp", 20f);
            yield return new WaitUntil(() => CampSceneHost.Current != null);
            yield return Settle(60);
            var campMagenta = Shot(outDir, "04_camp_day");

            // Pause modal → shot; then in-game settings modal → shot.
            ClickButton("Btn_qc_pause_button");
            yield return Settle(30);
            var pauseMagenta = Shot(outDir, "05_camp_pause");

            ClickModalButton("PausePanel", "Btn_menu_settings");
            yield return Settle(30);
            var campSettingsMagenta = Shot(outDir, "06_camp_settings");

            var names = new[] { "01_main_menu", "02_menu_settings", "03_menu_levels",
                "04_camp_day", "05_camp_pause", "06_camp_settings" };
            foreach (var n in names)
                Assert.IsTrue(File.Exists(System.IO.Path.Combine(outDir, n + ".png")),
                    "Missing screenshot " + n);
            // Missing shaders render as pure magenta — catch stripped-shader
            // regressions (e.g. URP Lit/Unlit absent from Always Included).
            var mags = new (string name, float v)[]
            {
                ("menu", menuMagenta), ("settings", settingsMagenta),
                ("levels", levelsMagenta), ("camp", campMagenta),
                ("pause", pauseMagenta), ("camp settings", campSettingsMagenta)
            };
            foreach (var m in mags)
                Assert.Less(m.v, 0.02f, $"{m.name} screenshot is magenta — missing shader.");
        }

        static void ClickButton(string goName)
        {
            var btn = FindButton(null, goName);
            Assert.IsNotNull(btn, $"Button '{goName}' not found");
            btn.onClick.Invoke();
        }

        static void ClickModalButton(string panelName, string goName)
        {
            var panel = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
                .FirstOrDefault(t => t.name == panelName && t.gameObject.activeInHierarchy);
            Assert.IsNotNull(panel, $"Modal panel '{panelName}' not active");
            var btn = FindButton(panel, goName);
            Assert.IsNotNull(btn, $"Button '{goName}' not found under {panelName}");
            btn.onClick.Invoke();
        }

        static UnityEngine.UI.Button FindButton(Transform root, string name)
        {
            var buttons = root == null
                ? Object.FindObjectsByType<UnityEngine.UI.Button>(FindObjectsSortMode.None)
                : root.GetComponentsInChildren<UnityEngine.UI.Button>(true);
            return buttons.FirstOrDefault(b => b.name == name && b.gameObject.activeInHierarchy);
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

        static float Shot(string dir, string name)
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

            var magenta = MagentaFraction(tex);

            for (var i = 0; i < canvases.Length; i++)
            {
                canvases[i].renderMode = prevModes[i];
                canvases[i].worldCamera = prevCam[i];
            }

            var path = System.IO.Path.Combine(dir, name + ".png");
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            Debug.Log($"[QuietCamp] Screenshot → {path} (magenta={magenta:P2})");
            return magenta;
        }

        static float MagentaFraction(Texture2D tex)
        {
            var px = tex.GetPixels32();
            var hits = 0;
            for (var i = 0; i < px.Length; i += 7) // sample every 7th pixel
            {
                var c = px[i];
                if (c.r > 180 && c.b > 180 && c.g < 90) hits++;
            }
            return (float)hits / (px.Length / 7f);
        }
    }
}
