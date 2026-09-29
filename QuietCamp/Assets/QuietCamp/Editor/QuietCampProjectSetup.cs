using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.Build.Profile;
using UnityEngine;

namespace QuietCamp.Editor
{
    /// <summary>
    /// One-shot editor setup for assets that cannot be authored as raw YAML:
    /// the dynamic TMP font asset, TMP project settings, and Android build profiles.
    /// Idempotent — safe to re-run. Invoke via Tools/Quiet Camp/Setup Project.
    /// </summary>
    public static class QuietCampProjectSetup
    {
        private const string FontPath = "Assets/QuietCamp/Resources/Fonts/DejaVuSans.ttf";
        private const string FontAssetPath = "Assets/QuietCamp/Resources/Fonts/DejaVuSans SDF.asset";
        private const string TmpSettingsPath = "Assets/QuietCamp/Resources/TMP Settings.asset";
        private const string BuildProfileDir = "Assets/QuietCamp/Settings/Build Profiles";

        [MenuItem("Tools/Quiet Camp/Setup Project")]
        public static void Run()
        {
            var fontAsset = EnsureTmpFont();
            EnsureTmpSettings(fontAsset);
            EnsureBuildProfiles();
            AssetDatabase.SaveAssets();
            Debug.Log("[QuietCamp] Project setup complete.");
        }

        private static TMP_FontAsset EnsureTmpFont()
        {
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            if (existing != null) return existing;
            var font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
            if (font == null) { Debug.LogError($"[QuietCamp] Font missing: {FontPath}"); return null; }
            var asset = TMP_FontAsset.CreateFontAsset(font);
            asset.name = "DejaVuSans SDF";
            asset.atlasPopulationMode = AtlasPopulationMode.Dynamic;
            AssetDatabase.CreateAsset(asset, FontAssetPath);
            return asset;
        }

        private static void EnsureTmpSettings(TMP_FontAsset fontAsset)
        {
            var settings = AssetDatabase.LoadAssetAtPath<TMP_Settings>(TmpSettingsPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<TMP_Settings>();
                AssetDatabase.CreateAsset(settings, TmpSettingsPath);
            }
            if (fontAsset != null)
            {
                var so = new SerializedObject(settings);
                var prop = so.FindProperty("m_defaultFontAsset");
                if (prop != null) { prop.objectReferenceValue = fontAsset; so.ApplyModifiedPropertiesWithoutUndo(); }
            }
        }

        private static void EnsureBuildProfiles()
        {
            Directory.CreateDirectory(BuildProfileDir);
            GUID? android = null;
            foreach (var module in BuildProfile.GetInstalledPlatformModules())
                if (module.displayName.Contains("Android")) { android = module.platformGuid; break; }
            if (android == null) { Debug.LogWarning("[QuietCamp] Android module not installed — skipping build profiles."); return; }

            EnsureProfile(android.Value, Path.Combine(BuildProfileDir, "Android Development.asset"), development: true);
            EnsureProfile(android.Value, Path.Combine(BuildProfileDir, "Android Release.asset"), development: false);
        }

        private static void EnsureProfile(GUID platformGuid, string path, bool development)
        {
            if (AssetDatabase.LoadAssetAtPath<BuildProfile>(path) != null) return;
            BuildProfile.CreateBuildProfile(platformGuid, path, profile =>
            {
                if (development) profile.scriptingDefines = new[] { "QC_TEST" };
                var so = new SerializedObject(profile);
                var dev = so.FindProperty("m_Development") ?? so.FindProperty("m_DevelopmentBuild");
                if (dev != null) { dev.boolValue = development; so.ApplyModifiedPropertiesWithoutUndo(); }
            });
        }
    }
}
