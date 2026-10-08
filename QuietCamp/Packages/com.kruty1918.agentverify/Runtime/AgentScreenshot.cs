using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Kruty1918.AgentVerify
{
    /// <summary>
    /// Deterministic screenshot capture that works in batchmode: renders a
    /// camera into a RenderTexture and writes a PNG — no swapchain needed, so
    /// it also works headless and in EditMode. Returns the path written.
    /// </summary>
    public static class AgentScreenshot
    {
        /// <summary>
        /// Capture to a PNG at <paramref name="outPath"/>.
        /// Interactive play mode default: ScreenCapture — the whole game view
        /// including overlay UI. Batch mode has no real backbuffer, so it
        /// (and edit mode) renders the camera headlessly with overlay
        /// canvases temporarily retargeted onto it.
        /// </summary>
        public static string Capture(string outPath, string cameraName = null,
            int width = 0, int height = 0)
        {
            if (string.IsNullOrEmpty(cameraName) && Application.isPlaying && !Application.isBatchMode)
                return CaptureScreen(outPath);
            return CaptureCamera(outPath, cameraName, width, height, Application.isPlaying);
        }

        /// <summary>Whole game view incl. overlay UI — play mode only; file lands next frame.</summary>
        public static string CaptureScreen(string outPath)
        {
            var dir = Path.GetDirectoryName(outPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            ScreenCapture.CaptureScreenshot(outPath);
            return outPath;
        }

        /// <summary>
        /// Render <paramref name="cameraName"/>'s view (or Camera.main) to a PNG.
        /// width/height 0 = camera pixel size or 1280×720.
        /// When <paramref name="includeOverlayUI"/>, Screen-Space-Overlay
        /// canvases are temporarily retargeted to the camera for the render.
        /// </summary>
        public static string CaptureCamera(string outPath, string cameraName = null,
            int width = 0, int height = 0, bool includeOverlayUI = false)
        {
            var cam = ResolveCamera(cameraName);
            if (cam == null) throw new System.InvalidOperationException(
                $"AgentScreenshot: camera '{cameraName ?? "main"}' not found");

            if (width <= 0) width = cam.pixelWidth > 0 ? cam.pixelWidth : 1280;
            if (height <= 0) height = cam.pixelHeight > 0 ? cam.pixelHeight : 720;

            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            var prev = cam.targetTexture;
            var prevActive = RenderTexture.active;
            var flipped = includeOverlayUI ? RetargetOverlayCanvases(cam) : null;
            try
            {
                cam.targetTexture = rt;
                cam.Render();
                RenderTexture.active = rt;
                var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                tex.Apply();
                var png = tex.EncodeToPNG();
                Object.DestroyImmediate(tex);
                var dir = Path.GetDirectoryName(outPath);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                File.WriteAllBytes(outPath, png);
                return outPath;
            }
            finally
            {
                RestoreCanvases(flipped);
                cam.targetTexture = prev;
                RenderTexture.active = prevActive;
                rt.Release();
                Object.DestroyImmediate(rt);
            }
        }

        /// <summary>Overlay canvases don't go through any camera — retarget them for the shot.</summary>
        static List<(Canvas canvas, RenderMode mode, Camera cam)> RetargetOverlayCanvases(Camera cam)
        {
            var flipped = new List<(Canvas, RenderMode, Camera)>();
            foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (c.renderMode != RenderMode.ScreenSpaceOverlay || !c.isActiveAndEnabled) continue;
                flipped.Add((c, c.renderMode, c.worldCamera));
                c.renderMode = RenderMode.ScreenSpaceCamera;
                c.worldCamera = cam;
            }
            return flipped;
        }

        static void RestoreCanvases(List<(Canvas canvas, RenderMode mode, Camera cam)> flipped)
        {
            if (flipped == null) return;
            foreach (var f in flipped)
            {
                if (f.canvas == null) continue;
                f.canvas.renderMode = f.mode;
                f.canvas.worldCamera = f.cam;
            }
        }

        public static Camera ResolveCamera(string cameraName)
        {
            if (!string.IsNullOrEmpty(cameraName))
            {
                var go = AgentProbe.Find(cameraName);
                var c = go != null ? go.GetComponent<Camera>() : null;
                if (c != null) return c;
                foreach (var cc in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
                    if (cc.name == cameraName) return cc;
                return null;
            }
            var main = Camera.main;
            if (main != null) return main;
            var all = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
            return all.Length > 0 ? all[0] : null;
        }
    }
}
