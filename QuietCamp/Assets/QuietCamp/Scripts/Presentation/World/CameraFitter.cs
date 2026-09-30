using QuietCamp.Domain;
using UnityEngine;
namespace QuietCamp.Presentation.World
{
    /// <summary>
    /// Orthographic camera per the scene contract: euler (55,225,0), distance 20,
    /// fitted by projecting board bounds onto camera right/up inside the
    /// BoardViewport rect.
    /// </summary>
    public static class CameraFitter
    {
        public static readonly Vector3 Euler = new Vector3(55f, 225f, 0f);
        public const float Distance = 20f;
        public const float Near = 0.1f, Far = 60f;
        public const float FitMargin = 1.08f;
        /// <summary>Extra world-space margin so meadow/decor frame the board.</summary>
        public const float SceneryMargin = 1.9f;

        public static void Configure(Camera camera)
        {
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.nearClipPlane = Near;
            camera.farClipPlane = Far;
            camera.transform.rotation = Quaternion.Euler(Euler);
            camera.transform.position = -camera.transform.forward * Distance;
        }

        /// <summary>Fits ortho size and target so the board bounds fill the viewport.</summary>
        public static void Fit(Camera camera, LevelData level, RectTransform viewport)
        {
            var rect = ViewportRect(viewport);
            // Render scenery across the full display, but fit the board inside the UI viewport.
            camera.rect = new Rect(0f, 0f, 1f, 1f);
            var aspect = camera.aspect;

            var half = new Vector3(level.width / 2f + BoardMath.Overhang + SceneryMargin, 0.4f,
                level.height / 2f + BoardMath.Overhang + SceneryMargin);
            float maxRight = 0f, maxUp = 0f;
            var camT = camera.transform;
            for (var i = 0; i < 8; i++)
            {
                var corner = new Vector3(
                    (i & 1) == 0 ? -half.x : half.x,
                    (i & 2) == 0 ? -half.y : half.y,
                    (i & 4) == 0 ? -half.z : half.z);
                // project onto right/up relative to camera facing the board centre
                var dir = corner;
                var right = Vector3.Dot(dir, camT.right);
                var up = Vector3.Dot(dir, camT.up);
                maxRight = Mathf.Max(maxRight, Mathf.Abs(right));
                maxUp = Mathf.Max(maxUp, Mathf.Abs(up));
            }
            camera.orthographicSize = Mathf.Max(maxRight / (aspect * rect.width),
                maxUp / rect.height) * FitMargin;
            var halfHeight = camera.orthographicSize;
            camera.transform.position = -camT.forward * Distance
                + camT.right * ((1f - 2f * rect.center.x) * halfHeight * aspect)
                + camT.up * ((1f - 2f * rect.center.y) * halfHeight);
        }

        static Rect ViewportRect(RectTransform viewport)
        {
            if (viewport == null) return new Rect(0f, 0f, 1f, 1f);
            var corners = new Vector3[4];
            viewport.GetWorldCorners(corners);
            var bl = RectTransformUtility.WorldToScreenPoint(null, corners[0]);
            var tr = RectTransformUtility.WorldToScreenPoint(null, corners[2]);
            return new Rect(
                bl.x / Screen.width, bl.y / Screen.height,
                Mathf.Max(0.01f, (tr.x - bl.x) / Screen.width),
                Mathf.Max(0.01f, (tr.y - bl.y) / Screen.height));
        }
    }
}
