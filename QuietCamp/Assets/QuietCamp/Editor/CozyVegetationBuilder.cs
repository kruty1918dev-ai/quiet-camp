using System;
using System.IO;
using QuietCamp.Presentation.World;
using UnityEditor;
using UnityEngine;

namespace QuietCamp.Editor
{
    /// <summary>Focused import: leaves existing scenes, prefabs, materials and audio untouched.</summary>
    public static class CozyVegetationBuilder
    {
        const string Destination = "Assets/QuietCamp/Resources/QuietCamp/CozyVegetationLibrary.asset";
        [MenuItem("Quiet Camp/Build Cozy Vegetation Library")]
        public static void Build()
        {
            string[] ids = { "plant_bushSmall", "plant_bushDetailed", "grass_leafs", "grass_leafsLarge", "flower_purpleA", "flower_yellowB" };
            var library = AssetDatabase.LoadAssetAtPath<CozyVegetationLibrary>(Destination);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<CozyVegetationLibrary>();
                AssetDatabase.CreateAsset(library, Destination);
            }
            var entries = new CozyVegetationLibrary.Plant[ids.Length];
            var meshDirectory = "Assets/QuietCamp/Art/CozyVegetation";
            Directory.CreateDirectory(meshDirectory);AssetDatabase.Refresh();
            for (int i = 0; i < ids.Length; i++)
            {
                var path = "Assets/ThirdParty/KenneyNature/Models/" + ids[i] + ".obj";
                var importer = (ModelImporter)AssetImporter.GetAtPath(path);
                importer.isReadable = true;importer.addCollider = false;importer.SaveAndReimport();
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var filter = model.GetComponentInChildren<MeshFilter>();
                var source = filter.sharedMesh;
                var mesh = UnityEngine.Object.Instantiate(source);mesh.name = ids[i] + " normalized";
                var vertices = mesh.vertices;var bounds = mesh.bounds;
                float scale = 1 / Mathf.Max(.001f, bounds.size.y);
                var origin = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
                for (int v = 0; v < vertices.Length; v++) vertices[v] = (vertices[v] - origin) * scale;
                mesh.vertices = vertices;mesh.RecalculateBounds();
                var meshPath = meshDirectory + "/" + ids[i] + ".asset";
                var existing = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                if (existing == null) AssetDatabase.CreateAsset(mesh, meshPath);
                else { EditorUtility.CopySerialized(mesh, existing);UnityEngine.Object.DestroyImmediate(mesh);mesh = existing;EditorUtility.SetDirty(mesh); }
                var slots = model.GetComponentInChildren<MeshRenderer>().sharedMaterials;
                var colors = new Color[mesh.subMeshCount];
                for (int s = 0; s < colors.Length; s++)
                {
                    var name = s < slots.Length && slots[s] != null ? slots[s].name : "grass";
                    colors[s] = name.IndexOf("Purple", StringComparison.OrdinalIgnoreCase) >= 0
                        ? new Color(.72f,.49f,.68f) : name.IndexOf("Yellow", StringComparison.OrdinalIgnoreCase) >= 0
                        ? new Color(.94f,.74f,.38f) : new Color(.35f,.55f,.24f);
                }
                entries[i] = new CozyVegetationLibrary.Plant { id = ids[i], mesh = mesh, colors = colors,
                    kind = i < 2 ? CozyVegetationLibrary.PlantKind.Shrub : i < 4 ? CozyVegetationLibrary.PlantKind.Leaf : CozyVegetationLibrary.PlantKind.Flower };
            }
            library.plants = entries;EditorUtility.SetDirty(library);AssetDatabase.SaveAssets();
            Debug.Log("[QuietCamp] Cozy vegetation: six CC0 species imported into normalized mesh library.");
        }
    }
}
