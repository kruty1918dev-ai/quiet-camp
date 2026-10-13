using UnityEditor;

namespace QuietCamp.Editor.LevelAuthoring
{
    public sealed class AuthoringPropImports : AssetPostprocessor
    {
        static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] oldPaths)
        {
            foreach (var path in imported)
                if (path == AuthoringPropCatalog.CatalogPath || path == AuthoringPropCatalog.ModelPath)
                { LevelAuthoringPalette.ResetCache(); break; }
        }
    }
}
