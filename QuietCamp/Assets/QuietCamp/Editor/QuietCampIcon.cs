using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace QuietCamp.Editor
{
    /// <summary>
    /// Generates the app icon procedurally (quiet-camp tent on deep forest
    /// green) and assigns it via PlayerSettings so Android/iOS/standalone
    /// builds all carry it. Idempotent — safe to re-run.
    /// </summary>
    public static class QuietCampIcon
    {
        const int Size = 1024;
        const string IconPath = "Assets/QuietCamp/Art/AppIcon.png";

        [MenuItem("QuietCamp/Generate App Icon")]
        public static void Apply()
        {
            var tex = Render();
            var png = tex.EncodeToPNG();
            Object.DestroyImmediate(tex);

            var abs = Path.Combine(Directory.GetCurrentDirectory(), IconPath);
            Directory.CreateDirectory(Path.GetDirectoryName(abs));
            File.WriteAllBytes(abs, png);
            AssetDatabase.ImportAsset(IconPath, ImportAssetOptions.ForceUpdate);
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);

            PlayerSettings.SetIcons(NamedBuildTarget.Android, new[] { icon }, IconKind.Application);
            PlayerSettings.SetIcons(NamedBuildTarget.iOS, new[] { icon }, IconKind.Application);
            PlayerSettings.SetIcons(NamedBuildTarget.Standalone, new[] { icon }, IconKind.Application);
            PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);
            AssetDatabase.SaveAssets();
            Debug.Log($"[QuietCamp] App icon written → {IconPath} and assigned in PlayerSettings.");
        }

        static Texture2D Render()
        {
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            var bg = new Color(0.196f, 0.310f, 0.235f);      // deep forest green
            var bgDark = new Color(0.157f, 0.255f, 0.192f);  // vignette
            var cream = new Color(0.949f, 0.910f, 0.835f);   // tent canvas
            var creamShade = new Color(0.859f, 0.800f, 0.714f);
            var door = new Color(0.294f, 0.208f, 0.141f);    // tent opening
            var pine = new Color(0.098f, 0.220f, 0.149f);    // side trees

            float cx = Size / 2f;
            var px = new Color32[Size * Size];
            for (var y = 0; y < Size; y++)
            {
                for (var x = 0; x < Size; x++)
                {
                    // rounded-square mask + vertical vignette
                    var c = RoundedMask(x, y, Size, 190);
                    var baseCol = Color.Lerp(bg, bgDark, (float)y / Size);
                    // moon halo, upper right — subtle brightness lift only
                    var dm = Mathf.Sqrt((x - Size * 0.74f) * (x - Size * 0.74f)
                        + (y - Size * 0.28f) * (y - Size * 0.28f));
                    var halo = Mathf.Clamp01(1f - dm / (Size * 0.40f)) * 0.10f;
                    baseCol = Color.Lerp(baseCol, new Color(1f, 0.96f, 0.85f), halo);

                    var col = baseCol;

                    // ground line
                    if (y < Size * 0.20f) col = Color.Lerp(col, bgDark, 0.55f);

                    // side pines: simple triangles
                    col = Pine(x, y, cx - 330f, Size * 0.20f, 150f, 300f, pine, col);
                    col = Pine(x, y, cx + 330f, Size * 0.20f, 150f, 300f, pine, col);

                    // tent: big triangle, apex top-center, base at 22% height
                    var tent = Tri(x, y, cx, Size * 0.66f, cx - 300f, Size * 0.22f, cx + 300f, Size * 0.22f);
                    if (tent > 0f)
                    {
                        var shade = Mathf.Clamp01((x - cx) / 300f); // darker right half
                        col = Color.Lerp(Color.Lerp(cream, creamShade, shade), col, 1f - tent);
                    }
                    // tent door: smaller centered triangle
                    var dtri = Tri(x, y, cx, Size * 0.50f, cx - 95f, Size * 0.22f, cx + 95f, Size * 0.22f);
                    if (dtri > 0f) col = Color.Lerp(door, col, 1f - dtri);

                    px[y * Size + x] = Color32.Lerp(Color.clear, col, c);
                }
            }
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }

        static Color Pine(int x, int y, float cx, float baseY, float halfW, float h,
            Color pine, Color cur)
        {
            var t = Tri(x, y, cx, baseY + h, cx - halfW, baseY, cx + halfW, baseY);
            return t > 0f ? Color.Lerp(pine, cur, 1f - t) : cur;
        }

        /// <summary>Barycentric triangle fill, returns coverage 0..1.</summary>
        static float Tri(float px, float py,
            float ax, float ay, float bx, float by, float cx, float cy)
        {
            var d = (by - cy) * (ax - cx) + (cx - bx) * (ay - cy);
            if (Mathf.Abs(d) < 0.001f) return 0f;
            var w1 = ((by - cy) * (px - cx) + (cx - bx) * (py - cy)) / d;
            var w2 = ((cy - ay) * (px - cx) + (ax - cx) * (py - cy)) / d;
            var w3 = 1f - w1 - w2;
            var m = Mathf.Min(w1, Mathf.Min(w2, w3));
            return Mathf.Clamp01(m * 40f); // crisp edge, slight anti-alias
        }

        /// <summary>Rounded-rectangle alpha mask.</summary>
        static float RoundedMask(int x, int y, int s, int r)
        {
            var qx = Mathf.Max(Mathf.Abs(x - s / 2f) - (s / 2f - r), 0f);
            var qy = Mathf.Max(Mathf.Abs(y - s / 2f) - (s / 2f - r), 0f);
            var d = Mathf.Sqrt(qx * qx + qy * qy) - r;
            return Mathf.Clamp01(-d);
        }
    }
}
