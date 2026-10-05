using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Compilation;

namespace QuietCamp.Editor
{
    /// <summary>Import/remove SDKs without manual symbols. Resolve actual public
    /// types from loaded assemblies; do not initialize providers in the Editor.</summary>
    [InitializeOnLoad]
    public static class OptionalGoogleSdkDetection
    {
        static OptionalGoogleSdkDetection() => EditorApplication.delayCall += Refresh;
        [MenuItem("QuietCamp/Refresh Optional Google SDKs")]
        public static void Refresh()
        {
            var dlls = CompilationPipeline.GetPrecompiledAssemblyPaths(CompilationPipeline.PrecompiledAssemblySources.UserAssembly);
            var sourceFiles = CompilationPipeline.GetAssemblies(AssembliesType.Player).SelectMany(a => a.sourceFiles).Where(File.Exists).ToArray();
            bool firebase = dlls.Any(p => File.Exists(p) && Path.GetFileName(p) == "Firebase.Analytics.dll")
                && Has("Firebase.Analytics.FirebaseAnalytics") && Has("Firebase.FirebaseApp") && Has("Firebase.Analytics.ConsentType")
                && ConsentApiAvailable();
            bool ads = (sourceFiles.Any(p => Path.GetFileName(p) == "RewardedAd.cs") || dlls.Any(p => File.Exists(p) && Path.GetFileName(p).StartsWith("GoogleMobileAds")))
                && Has("GoogleMobileAds.Api.RewardedAd") && Has("GoogleMobileAds.Ump.Api.ConsentInformation");
            foreach (var target in new[] { NamedBuildTarget.Android, NamedBuildTarget.Standalone })
            {
                var old = PlayerSettings.GetScriptingDefineSymbols(target);
                var symbols = new HashSet<string>(old.Split(';').Where(s => !string.IsNullOrWhiteSpace(s)));
                Set(symbols, "QC_FIREBASE_ANALYTICS", firebase);
                Set(symbols, "QC_GOOGLE_REWARDED", ads);
                var next = string.Join(";", symbols.OrderBy(s => s));
                if (new HashSet<string>(old.Split(';')).SetEquals(next.Split(';'))) continue;
                PlayerSettings.SetScriptingDefineSymbols(target, next);
            }
        }
        static bool ConsentApiAvailable()
        {
            var types = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("Firebase.Analytics.ConsentType", false)).FirstOrDefault(t => t != null);
            return types != null && new[] { "AnalyticsStorage", "AdStorage", "AdUserData", "AdPersonalization" }.All(n => Enum.GetNames(types).Contains(n));
        }
        static bool Has(string name)
        {
            if (AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetType(name, false) != null)) return true;
            // Unity imports managed plug-ins lazily. Their namespaces can be
            // installed without the assembly having been loaded by game code.
            foreach (var path in CompilationPipeline.GetPrecompiledAssemblyPaths(CompilationPipeline.PrecompiledAssemblySources.UserAssembly))
            {
                var file = Path.GetFileName(path);
                if (!File.Exists(path) || (!file.StartsWith("Firebase.") && !file.StartsWith("GoogleMobileAds"))) continue;
                try
                {
                    var assembly = System.Reflection.Assembly.LoadFrom(path);
                    if (assembly.GetType(name, false) != null) return true;
                }
                catch { /* Incompatible/unavailable plug-in stays disabled. */ }
            }
            return false;
        }
        static void Set(HashSet<string> symbols, string name, bool available) { if (available) symbols.Add(name); else symbols.Remove(name); }
    }
    sealed class OptionalGoogleImportWatcher : AssetPostprocessor
    {
        static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] from)
        {
            if (imported.Concat(deleted).Concat(moved).Concat(from).Any(p => p.Contains("Firebase") || p.Contains("GoogleMobileAds")))
                EditorApplication.delayCall += OptionalGoogleSdkDetection.Refresh;
        }
    }
}
