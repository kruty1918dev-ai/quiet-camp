using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
#if UNITY_ANDROID
using UnityEditor.Android;
#endif

namespace QuietCamp.Editor
{
    /// <summary>Assigns the authored cozy artwork without regenerating or overwriting it.</summary>
    public static class QuietCampIcon
    {
        const string IconPath = "Assets/QuietCamp/Art/AppIcon.png";
        const string BackgroundPath = "Assets/QuietCamp/Art/AppIcon/quietcamp-adaptive-background.png";
        const string ForegroundPath = "Assets/QuietCamp/Art/AppIcon/quietcamp-adaptive-foreground.png";

        [MenuItem("Tools/Quiet Camp/Apply App Icon")]
        public static void Apply()
        {
            var icon = LoadIcon(IconPath);
            PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);
#if UNITY_ANDROID
            var background = LoadIcon(BackgroundPath);
            var foreground = LoadIcon(ForegroundPath);
            // Unity stores the background first, then the transparent foreground.
            SetAndroidIcons(AndroidPlatformIconKind.Adaptive, background, foreground);
#endif
#if UNITY_IOS
            foreach (var kind in PlayerSettings.GetSupportedIconKinds(NamedBuildTarget.iOS))
            {
                var slots = PlayerSettings.GetPlatformIcons(NamedBuildTarget.iOS, kind);
                foreach (var slot in slots) slot.SetTexture(icon);
                PlayerSettings.SetPlatformIcons(NamedBuildTarget.iOS, kind, slots);
            }
#endif
            AssetDatabase.SaveAssets();
            Debug.Log("[QuietCamp] Cozy app icon assigned in PlayerSettings.");
        }

        static Texture2D LoadIcon(string path)
        {
            AssetDatabase.ImportAsset(path);
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (icon == null)
                throw new BuildFailedException($"[QuietCamp] App icon missing: {path}");
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer.mipmapEnabled || importer.textureCompression != TextureImporterCompression.Uncompressed
                || importer.wrapMode != TextureWrapMode.Clamp)
            {
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.SaveAndReimport();
            }
            return icon;
        }
#if UNITY_ANDROID
        static void SetAndroidIcons(PlatformIconKind kind, params Texture2D[] textures)
        {
            var slots = PlayerSettings.GetPlatformIcons(NamedBuildTarget.Android, kind);
            foreach (var slot in slots) slot.SetTextures(textures);
            PlayerSettings.SetPlatformIcons(NamedBuildTarget.Android, kind, slots);
        }
#endif
    }

    // Covers build profiles, the standard Build window and batch builds alike.
    public sealed class QuietCampIconBuildPreprocessor : IPreprocessBuildWithReport
    {
        public int callbackOrder => -1000;
        public void OnPreprocessBuild(BuildReport report) => QuietCampIcon.Apply();
    }
}
