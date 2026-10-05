using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace QuietCamp.Editor
{
    /// <summary>The game binds static HTML events directly to C#. Keep the
    /// legacy VM available in Editor, but never ship its obsolete Android SO.</summary>
    public sealed class AndroidHtmlUiBuildFilter : IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        public int callbackOrder => 0;
        public void OnPreprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.Android) return;
            foreach (var plugin in PluginImporter.GetAllImporters())
                if (IsQuickJs(plugin.assetPath)) plugin.SetIncludeInBuildDelegate(_ => false);
        }
        public void OnPostprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.Android) return;
            foreach (var plugin in PluginImporter.GetAllImporters())
                if (IsQuickJs(plugin.assetPath)) plugin.SetIncludeInBuildDelegate(null);
        }
        static bool IsQuickJs(string path) =>
            path.StartsWith("Packages/com.reactunity.quickjs/", StringComparison.Ordinal)
            && Path.GetFileName(path) == "libquickjs.so";
    }
}
