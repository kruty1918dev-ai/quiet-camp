using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;

namespace QuietCamp.Editor.LevelAuthoring
{
    [InitializeOnLoad]
    public static class PwbInstallation
    {
        const string Symbol = "QUIETCAMP_PWB";
        static PwbInstallation() => EditorApplication.update += ConnectWhenReady;
        static void ConnectWhenReady()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            EditorApplication.update -= ConnectWhenReady; Configure();
        }
        public static void Configure()
        {
            if (EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode) return;
            var target = NamedBuildTarget.FromBuildTargetGroup(EditorUserBuildSettings.selectedBuildTargetGroup);
            var defines = PlayerSettings.GetScriptingDefineSymbols(target).Split(';').Where(s => !string.IsNullOrEmpty(s)).ToList();
            bool installed = File.Exists("Assets/PluginMaster/DesignTools/Editor/PrefabWorldBuilder/Scripts/PWBData.cs");
            // Package layouts can change; the required public controller is the reliable installation marker.
            installed |= File.Exists("Assets/PluginMaster/DesignTools/Editor/PrefabWorldBuilder/Scripts/Tools/ToolController.cs");
            string before = string.Join(";", defines);
            void Set(string symbol, bool present) { if (present && !defines.Contains(symbol)) defines.Add(symbol); else if (!present) defines.Remove(symbol); }
            Set(Symbol, installed);
            Set("QUIETCAMP_ODIN", File.Exists("Assets/Plugins/Sirenix/Assemblies/Sirenix.OdinInspector.Editor.dll"));
            Set("QUIETCAMP_RIVER_TERRAIN", File.Exists("Assets/WaterGeometryTools/RiverModeler/Runtime/RiverModeler.cs")
                && Directory.Exists("Packages/com.jbooth.microverse.splines"));
            string after = string.Join(";", defines);
            if (before != after) PlayerSettings.SetScriptingDefineSymbols(target, after);
        }
        [MenuItem("Quiet Camp/Майстерня/Підключити встановлений PWB")]
        static void Connect() => Configure();
    }
}
