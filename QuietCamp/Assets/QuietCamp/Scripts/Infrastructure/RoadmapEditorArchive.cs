#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace QuietCamp.Infrastructure
{
    /// <summary>Explicit Editor-only access to deferred world data. None of these
    /// assets live in Resources or are loaded by a player.</summary>
    public static class RoadmapEditorArchive
    {
        public const string Root = "Assets/QuietCamp/Editor/RoadmapArchive";
        public static T Load<T>(string resourcePath) where T : Object
        {
            const string prefix = "QuietCamp/";
            if (!resourcePath.StartsWith(prefix, System.StringComparison.Ordinal))
                throw new System.ArgumentException("Unknown roadmap archive path", nameof(resourcePath));
            var path = Root + "/" + resourcePath.Substring(prefix.Length);
            var extension = typeof(T) == typeof(Shader) ? ".shader"
                : typeof(T) == typeof(TextAsset) ? (path.EndsWith(".css") ? ".txt" : ".json") : ".asset";
            return AssetDatabase.LoadAssetAtPath<T>(path + extension);
        }
    }
}
#endif
