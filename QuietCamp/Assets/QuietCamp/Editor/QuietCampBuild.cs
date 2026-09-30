using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Profile;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace QuietCamp.Editor
{
    /// <summary>
    /// Batchmode entry points for Android APK builds.
    /// Usage:
    ///   Unity -batchmode -projectPath QuietCamp
    ///         -executeMethod QuietCamp.Editor.QuietCampBuild.BuildAndroidDev
    ///         -logFile build.log
    /// A non-zero editor exit code marks the build as failed.
    /// </summary>
    public static class QuietCampBuild
    {
        private const string BuildDir = "Builds/Android";
        private const string DevProfilePath = "Assets/Settings/Build Profiles/Android Development.asset";
        private const string ReleaseProfilePath = "Assets/Settings/Build Profiles/Android Release.asset";

        [MenuItem("Tools/Quiet Camp/Build Android Dev APK")]
        public static void BuildAndroidDev()
            => Build(DevProfilePath, Path.Combine(BuildDir, "QuietCamp-MVP-0.1.0-dev.apk"));

        [MenuItem("Tools/Quiet Camp/Build Android Release APK")]
        public static void BuildAndroidRelease()
            => Build(ReleaseProfilePath, Path.Combine(BuildDir, "QuietCamp-MVP-0.1.0.apk"));

        private static void Build(string profilePath, string outputPath)
        {
            var profile = AssetDatabase.LoadAssetAtPath<BuildProfile>(profilePath);
            if (profile == null)
                Fail($"Build profile missing at {profilePath} — run Tools/Quiet Camp/Setup Project.");

            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length != 3)
                Fail($"Expected 3 build scenes (Boot, MainMenu, Camp), got {scenes.Length}.");
            foreach (var s in scenes)
                if (!File.Exists(s)) Fail($"Scene missing: {s}");

            var catalog = AssetDatabase.LoadAssetAtPath<ScriptableObject>(
                "Assets/QuietCamp/Resources/QuietCamp/AssetCatalog.asset");
            if (catalog == null) Fail("AssetCatalog.asset missing — run content builder first.");

            BuildProfile.SetActiveBuildProfile(profile);
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outputPath,
                target = BuildTarget.Android
            };
            var report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;
            Debug.Log($"[QuietCamp] Build {summary.result}: {summary.totalSize} bytes, " +
                      $"{summary.totalErrors} errors, {summary.totalWarnings} warnings → {outputPath}");
            if (summary.result != BuildResult.Succeeded)
                Fail($"Build failed: {summary.result}, {summary.totalErrors} errors.");
            if (!File.Exists(outputPath) || new FileInfo(outputPath).Length == 0)
                Fail("Build reported success but APK missing or empty.");
            if (UnityEngine.Application.isBatchMode)
                EditorApplication.Exit(0);
        }

        private static void Fail(string message)
        {
            Debug.LogError($"[QuietCamp] {message}");
            if (UnityEngine.Application.isBatchMode)
                EditorApplication.Exit(1);
            else
                throw new InvalidOperationException(message);
        }
    }
}
