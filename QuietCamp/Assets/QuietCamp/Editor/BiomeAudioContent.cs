using UnityEditor;
using UnityEngine;
using QuietCamp.Infrastructure;

namespace QuietCamp.Editor
{
    public static class BiomeAudioContent
    {
        public const string Folder = "Assets/QuietCamp/Audio/Generated/Biomes/";
        [MenuItem("QuietCamp/Import Biome Audio")]
        public static void Import()
        {
            AssetDatabase.Refresh();
            var catalog = QuietCampAudioCatalog.Load();
            if (catalog == null) throw new System.InvalidOperationException("Audio catalog missing.");
            var serialized = new SerializedObject(catalog);
            foreach (var name in new[]{"forest","meadow","autumn","winter","water"})
                SoundscapeAudioContent.Add(serialized,"ambience.biome."+name,name,1,true,name=="water",name=="water"?.12f:.08f,Folder);
            foreach (var name in new[]{"snow","leaves","mud","soil","grass"})
                SoundscapeAudioContent.Add(serialized,"sfx.surface."+name,name,3,false,true,.13f,Folder);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog); AssetDatabase.SaveAssets();
        }
    }
}
