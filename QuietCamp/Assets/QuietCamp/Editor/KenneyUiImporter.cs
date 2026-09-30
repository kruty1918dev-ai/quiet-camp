using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
namespace QuietCamp.Editor
{
    /// <summary>
    /// Imports the bundled Kenney UI PNGs as uGUI sprites and mirrors the
    /// runtime-needed subset into Resources/QuietCamp/UI. Idempotent —
    /// run as part of QuietCampProjectSetup or standalone.
    /// </summary>
    public static class KenneyUiImporter
    {
        const string Src = "Assets/ThirdParty/KenneyUI/PNG";
        const string Dst = "Assets/QuietCamp/Resources/QuietCamp/UI";

        // Sprites the runtime actually loads — mirrored under Resources.
        static readonly string[] Needed =
        {
            "Green/Default/button_rectangle_depth_flat.png",
            "Green/Default/button_rectangle_flat.png",
            "Green/Default/button_square_depth_flat.png",
            "Green/Default/icon_checkmark.png",
            "Green/Default/icon_circle.png",
            "Green/Default/icon_outline_circle.png",
            "Grey/Default/button_rectangle_depth_flat.png",
            "Grey/Default/button_square_depth_flat.png",
            "Grey/Default/icon_cross.png",
            "Red/Default/button_rectangle_depth_flat.png",
            "Red/Default/icon_cross.png",
            "Extra/Default/input_rectangle.png",
            "Extra/Default/divider.png",
            "Extra/Default/icon_repeat_dark.png",
            "Extra/Default/icon_play_light.png",
            "Extra/Default/icon_arrow_up_dark.png",
            "Extra/Default/icon_arrow_down_dark.png",
            "Grey/Default/slide_horizontal_grey.png",
            "Green/Default/slide_horizontal_color.png",
            "Grey/Default/slide_hangle.png",
            "Grey/Default/check_square_grey.png",
            "Grey/Default/check_square_grey_checkmark.png",
        };

        [MenuItem("QuietCamp/Import UI Sprites")]
        public static void Run()
        {
            var reimported = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Src }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!(AssetImporter.GetAtPath(path) is TextureImporter ti)) continue;
                var needsSprite = ti.textureType != TextureImporterType.Sprite;
                var name = System.IO.Path.GetFileNameWithoutExtension(path);
                var needsSlice = name.StartsWith("button_") || name.StartsWith("input_");
                var border = needsSlice ? new Vector4(26f, 26f, 26f, 26f) : Vector4.zero;
                if (!needsSprite && ti.spriteBorder == border) continue;
                ti.textureType = TextureImporterType.Sprite;
                ti.spriteImportMode = SpriteImportMode.Single;
                ti.alphaIsTransparency = true;
                if (needsSlice) ti.spriteBorder = border;
                ti.SaveAndReimport();
                reimported++;
            }

            EnsureFolderRecursive(Dst);
            foreach (var rel in Needed)
            {
                var src = $"{Src}/{rel}";
                var dst = $"{Dst}/{rel}";
                var dir = System.IO.Path.GetDirectoryName(dst)?.Replace('\\', '/');
                if (dir != null) EnsureFolderRecursive(dir);
                if (AssetDatabase.LoadAssetAtPath<Sprite>(dst) == null
                    && !AssetDatabase.CopyAsset(src, dst))
                    Debug.LogWarning($"[QuietCamp] UI sprite copy failed: {src}");
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[QuietCamp] UI sprites imported ({reimported} reimported), {Needed.Length} mirrored to Resources");
        }

        static void EnsureFolderRecursive(string dir)
        {
            var parts = dir.Split('/');
            for (var i = 2; i <= parts.Length; i++)
            {
                var parent = string.Join("/", parts, 0, i - 1);
                var current = string.Join("/", parts, 0, i);
                if (!AssetDatabase.IsValidFolder(current))
                    AssetDatabase.CreateFolder(parent, parts[i - 1]);
            }
        }
    }
}
