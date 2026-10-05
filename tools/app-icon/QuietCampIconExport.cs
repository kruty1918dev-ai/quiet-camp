// Run in a disposable Unity project with CozyForegroundMaster.png in Assets,
// plus the game's QuietCampIcon.cs in Assets/Editor. No game scenes are needed.
using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using QuietCamp.Editor;

public static class QuietCampIconExport
{
    const int Size = 1024;
    const string Art = "Assets/QuietCamp/Art/";

    public static void Export()
    {
        var source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (!source.LoadImage(File.ReadAllBytes("Assets/CozyForegroundMaster.png")))
            throw new InvalidOperationException("Cannot load generated master.");
        source.wrapMode = TextureWrapMode.Clamp;
        // Alpha >= 10 excludes barely visible generation noise when measuring
        // the artwork. The exported texture retains the original alpha values.
        int left = source.width, right = 0, bottom = source.height, top = 0;
        var pixels = source.GetPixels32();
        for (int y = 0; y < source.height; y++)
            for (int x = 0; x < source.width; x++)
                if (pixels[y * source.width + x].a >= 10)
                {
                    left = Math.Min(left, x); right = Math.Max(right, x);
                    bottom = Math.Min(bottom, y); top = Math.Max(top, y);
                }
        float cx = (left + right) * .5f, cy = (bottom + top) * .5f;
        float radius = 0;
        for (int y = 0; y < source.height; y++)
            for (int x = 0; x < source.width; x++)
                if (pixels[y * source.width + x].a >= 10)
                    radius = Mathf.Max(radius, Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy)));
        Directory.CreateDirectory(Art + "AppIcon");
        Write(Art + "AppIcon/quietcamp-adaptive-background.png", Render(source, cx, cy, radius, 0, true));
        Write(Art + "AppIcon/quietcamp-adaptive-foreground.png", Render(source, cx, cy, radius, .29f, false));
        Write(Art + "AppIcon.png", Render(source, cx, cy, radius, .44f, true));
        Write(Art + "AppIcon/quietcamp-google-play-512.png", Render(source, cx, cy, radius, .44f, true, 512));
        AssetDatabase.Refresh();
        QuietCampIcon.Apply();
        // Check a second application does not regenerate any artwork.
        var before = File.ReadAllBytes(Art + "AppIcon.png");
        QuietCampIcon.Apply();
        var after = File.ReadAllBytes(Art + "AppIcon.png");
        if (Convert.ToBase64String(before) != Convert.ToBase64String(after))
            throw new InvalidOperationException("Apply unexpectedly changed the icon artwork.");
        Debug.Log("[QuietCampIconExport] Export and repeated icon assignment passed.");
        UnityEngine.Object.DestroyImmediate(source);
    }

    static Texture2D Render(Texture2D source, float cx, float cy, float radius, float fraction, bool opaque, int size = Size)
    {
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var pixels = new Color32[size * size];
        float scale = fraction * size / radius;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                var delta = new Vector2(x + .5f - size * .5f, y + .5f - size * .5f);
                float light = Mathf.Clamp01(1 - delta.magnitude / (size * .7f));
                var bg = Color.Lerp(new Color32(14, 47, 42, 255), new Color32(35, 77, 60, 255), light);
                Color fg = Color.clear;
                if (fraction > 0)
                {
                    float sx = cx + delta.x / scale, sy = cy + delta.y / scale;
                    if (sx >= 0 && sy >= 0 && sx < source.width && sy < source.height)
                        fg = source.GetPixelBilinear((sx + .5f) / source.width, (sy + .5f) / source.height);
                }
                if (opaque)
                {
                    var color = Color.Lerp(bg, fg, fg.a);
                    color.a = 1;
                    pixels[y * size + x] = color;
                }
                else pixels[y * size + x] = fg;
            }
        texture.SetPixels32(pixels);
        texture.Apply();
        return texture;
    }

    static void Write(string path, Texture2D texture)
    {
        File.WriteAllBytes(path, texture.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(texture);
    }
}
