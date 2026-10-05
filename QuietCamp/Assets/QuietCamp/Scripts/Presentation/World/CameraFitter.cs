using QuietCamp.Domain;
using UnityEngine;
namespace QuietCamp.Presentation.World
{
    /// <summary>
    /// Orthographic camera per the scene contract: euler (58,225,0), distance 20,
    /// fitted by projecting board bounds onto camera right/up inside the
    /// BoardViewport rect.
    /// </summary>
    public static class CameraFitter
    {
        public static readonly Vector3 Euler = new Vector3(58f, 225f, 0f);
        public const float Distance = 20f;
        public const float Near = 0.1f, Far = 60f;
        public const float FitMargin = 1.0f;
        /// <summary>Extra world-space margin so meadow/decor frame the board.
        /// Kept tight — the board must read as the scene's main subject and
        /// stay legible on small phone screens.</summary>
        public const float SceneryMargin = 0.3f;

        public static void Configure(Camera camera, Vector3? euler = null)
        {
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.nearClipPlane = Near;
            camera.farClipPlane = Far;
            camera.transform.rotation = Quaternion.Euler(euler ?? Euler);
            camera.transform.position = -camera.transform.forward * Distance;
        }

        /// <summary>Fits ortho size and target so the board bounds fill the viewport.</summary>
        public static void Fit(Camera camera, LevelData level, RectTransform viewport)
        {
            if (camera == null || level == null) return;
            // Scene adapter: full forest render, reserved gameplay region from UI.
            camera.rect = new Rect(0f, 0f, 1f, 1f);
            var adaptation = camera.GetComponent<Kruty1918.GameplayViewport.GameplayViewport>();
            if (adaptation == null) adaptation = camera.gameObject.AddComponent<Kruty1918.GameplayViewport.GameplayViewport>();
            adaptation.ContentViewport = viewport;
            adaptation.Refresh();
            var frame = adaptation.Frame;
            if (!frame.IsValid) return;
            camera.aspect = frame.RenderAspect;
            var bounds = new Bounds(Vector3.zero, new Vector3(
                level.width + 2 * (BoardMath.Overhang + SceneryMargin), .8f,
                level.height + 2 * (BoardMath.Overhang + SceneryMargin)));
            if (!Kruty1918.GameplayViewport.GameplayCameraMath.TryOrthographic(bounds, camera.transform.rotation,
                camera.aspect, frame.NormalizedGameplay, Distance, FitMargin, out var pose, camera.nearClipPlane)) return;
            camera.orthographicSize = pose.OrthographicSize;
            camera.transform.position = pose.Position;
            camera.farClipPlane = Mathf.Max(camera.farClipPlane, pose.RequiredFarClip);
        }
    }
}
