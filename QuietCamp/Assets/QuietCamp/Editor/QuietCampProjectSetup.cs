using System;
using System.IO;
using System.Reflection;
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
        // Must be the TMP-owned settings path — TMP_Settings.instance loads
        // exactly one asset by name and validates assetVersion on it.
        private const string TmpSettingsPath = "Assets/TextMesh Pro/Resources/TMP Settings.asset";

        [MenuItem("Tools/Quiet Camp/Setup Project")]
        public static void Run()
        {
            // TMP's Distance Field shaders ship inside a .unitypackage in the
            // ugui package — import it once so font-asset materials resolve.
            EnsureTmpEssentials();
            // TMP_Settings.instance must exist before CreateFontAsset — it reads
            // settings during atlas allocation. Settings first, font second.
            var settings = EnsureTmpSettingsAsset();
            var fontAsset = EnsureTmpFont();
            EnsureTmpSettingsFont(settings, fontAsset);
            KenneyUiImporter.Run();
            EnsureBuildProfiles();
            AssetDatabase.SaveAssets();
            Debug.Log("[QuietCamp] Project setup complete.");
        }

        private static void EnsureTmpEssentials()
        {
            const string marker = "Assets/TextMesh Pro/Shaders/TMP_SDF-Mobile.shader";
            var pkg = UnityEditor.PackageManager.PackageInfo.FindForPackageName("com.unity.ugui");
            var full = pkg != null
                ? Path.Combine(pkg.resolvedPath, "Package Resources", "TMP Essential Resources.unitypackage")
                : null;
            if (AssetDatabase.LoadAssetAtPath<Shader>(marker) == null
                && Shader.Find("TextMeshPro/Mobile/Distance Field") == null
                && full != null && File.Exists(full))
            {
                UnityEditor.AssetPackage.Package.Import(full, false);
                AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            }
            if (Shader.Find("TextMeshPro/Mobile/Distance Field") == null
                && AssetDatabase.LoadAssetAtPath<Shader>(marker) == null)
                Debug.LogError("[QuietCamp] TMP shaders unavailable — TMP text will not render.");
        }

        private static TMP_Settings EnsureTmpSettingsAsset()
        {
            var settings = AssetDatabase.LoadAssetAtPath<TMP_Settings>(TmpSettingsPath);
            if (settings != null) return settings;
            settings = ScriptableObject.CreateInstance<TMP_Settings>();
            AssetDatabase.CreateAsset(settings, TmpSettingsPath);
            return settings;
        }

        private static void EnsureTmpSettingsFont(TMP_Settings settings, TMP_FontAsset fontAsset)
        {
            if (settings == null || fontAsset == null) return;
            var so = new SerializedObject(settings);
            var prop = so.FindProperty("m_defaultFontAsset");
            if (prop != null) { prop.objectReferenceValue = fontAsset; so.ApplyModifiedPropertiesWithoutUndo(); }
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
            var atlas = new Texture2D(asset.atlasWidth, asset.atlasHeight, TextureFormat.Alpha8, false)
            {
                name = "DejaVuSans Atlas",
                hideFlags = HideFlags.HideInHierarchy
            };
            AssetDatabase.AddObjectToAsset(atlas, asset);
            asset.atlasTextures = new[] { atlas };
            var shader = Shader.Find("TextMeshPro/Mobile/Distance Field")
                ?? Shader.Find("TextMeshPro/Distance Field");
            var material = new Material(shader)
            {
                name = "DejaVuSans SDF Material",
                hideFlags = HideFlags.HideInHierarchy
            };
            material.SetTexture(ShaderUtilities.ID_MainTex, atlas);
            AssetDatabase.AddObjectToAsset(material, asset);
            asset.material = material;
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static void EnsureBuildProfiles()
        {
            GUID? android = null;
            foreach (var module in BuildProfile.GetInstalledPlatformModules())
                if (module.displayName.Contains("Android")) { android = module.platformGuid; break; }
            if (android == null)
            {
                Debug.LogWarning("[QuietCamp] Android module not installed — skipping build profiles.");
                return;
            }
            EnsureProfile(android.Value, "Android Development", development: true);
            EnsureProfile(android.Value, "Android Release", development: false);
        }

        /// <summary>
        /// CreateBuildProfile registers its callback as a persistent listener —
        /// the delegate target must be a UnityEngine.Object, so lambdas crash.
        /// A transient ScriptableObject holder carries the config instead.
        /// </summary>
        private sealed class ProfileReady : ScriptableObject
        {
            public bool development;
            public string[] defines;

            public void OnReady(BuildProfile profile)
            {
                var comp = typeof(BuildProfile)
                    .GetField("m_PlatformBuildProfile",
                        BindingFlags.NonPublic | BindingFlags.Instance)
                    ?.GetValue(profile);
                var devField = comp?.GetType().GetField("m_Development",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                devField?.SetValue(comp, development);
                if (defines != null && defines.Length > 0)
                    profile.scriptingDefines = defines;
                EditorUtility.SetDirty(profile);
                DestroyImmediate(this);
            }
        }

        private static void EnsureProfile(GUID platformGuid, string name, bool development)
        {
            var path = $"Assets/Settings/Build Profiles/{name}.asset";
            var profile = AssetDatabase.LoadAssetAtPath<BuildProfile>(path);
            if (profile == null)
            {
                var holder = ScriptableObject.CreateInstance<ProfileReady>();
                holder.development = development;
                holder.defines = development ? new[] { "QC_TEST" } : null;
                BuildProfile.CreateBuildProfile(platformGuid, name, holder.OnReady);
                AssetDatabase.ImportAsset(path);
                profile = AssetDatabase.LoadAssetAtPath<BuildProfile>(path);
                if (profile == null)
                {
                    Debug.LogError($"[QuietCamp] Build profile not created at {path}");
                    return;
                }
            }
            var comp = typeof(BuildProfile)
                .GetField("m_PlatformBuildProfile", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.GetValue(profile);
            if (comp == null)
            {
                Debug.LogWarning($"[QuietCamp] {name}: platform build settings component missing.");
                return;
            }
            FieldInfo devField = null;
            for (var t = comp.GetType(); t != null && devField == null; t = t.BaseType)
                devField = t.GetField("m_Development",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            if (devField == null)
            {
                Debug.LogWarning($"[QuietCamp] {name}: m_Development not found on {comp.GetType().Name} hierarchy.");
                return;
            }
            devField.SetValue(comp, development);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssetIfDirty(profile);
        }
    }
}
