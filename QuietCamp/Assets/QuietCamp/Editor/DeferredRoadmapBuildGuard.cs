using System;
using System.Collections.Generic;
using System.Linq;
using QuietCamp.Infrastructure;
using QuietCamp.Presentation.World;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace QuietCamp.Editor
{
    /// <summary>Fail closed if an import or authoring tool restores deferred map
    /// assets to player roots. Runs on every build target without moving files.</summary>
    public sealed class DeferredRoadmapBuildGuard : IPreprocessBuildWithReport
    {
        public int callbackOrder => -1000;
        public void OnPreprocessBuild(BuildReport report) => ValidatePlayerDependencies();

        public static string[] ValidatePlayerDependencies()
        {
            var roots = AssetDatabase.GetAllAssetPaths().Where(path =>
                !path.Contains("/Editor/") && path.Contains("/Resources/")
                && !AssetDatabase.IsValidFolder(path)).Concat(
                EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path)).ToArray();
            var dependencies = AssetDatabase.GetDependencies(roots, true);
            var forbidden = dependencies.Where(IsDeferredAsset).Distinct().ToArray();
            if (forbidden.Length > 0)
                throw new BuildFailedException("Deferred roadmap entered player dependencies:\n" + string.Join("\n", forbidden));
            return dependencies;
        }

        public static bool IsDeferredAsset(string path)
            => path.StartsWith(RoadmapEditorArchive.Root + "/", StringComparison.Ordinal)
                || path.Contains("/Resources/QuietCamp/CinematicRoadmap/")
                || path.Contains("/Resources/QuietCamp/Roadmaps/")
                || new[] { "roadmap_regions.json", "roadmap_culture_models.json", "RoadmapLit.shader",
                    "RoadmapMotes.shader", "CampMap.css.txt" }.Any(name => path.EndsWith("/" + name, StringComparison.Ordinal));

        [MenuItem("Quiet Camp/Cinematic Roadmap/Check Player Exclusion (No Build)")]
        static void Check() => UnityEngine.Debug.Log("[QuietCamp] Deferred roadmap excluded from "
            + ValidatePlayerDependencies().Length + " player dependencies; no build performed.");
    }
}
