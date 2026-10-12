using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using QuietCamp.Infrastructure;
using QuietCamp.Presentation.World;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace QuietCamp.Editor.LevelAuthoring
{
    public sealed class AuthoringPaletteEntry
    {
        public string id, label, category;
        public GameObject prefab;
        public CampArtDefinition.Model model;
        public bool smallPlant;
    }
    public static class LevelAuthoringPalette
    {
        public const string Folder = "Assets/QuietCamp/Editor/LevelAuthoring/Palette";
        sealed class Library { public CampArtDefinition.Model[] models; }
        static AuthoringPaletteEntry[] _entries;
        public static AuthoringPaletteEntry[] Entries => _entries ?? (_entries = Build());
        public static AuthoringPaletteEntry Get(string id) => Entries.FirstOrDefault(e => e.id == id) ?? throw new InvalidOperationException("Невідомий об’єкт: " + id);
        public static void ResetCache() => _entries = null;
        static AuthoringPaletteEntry[] Build()
        {
            System.IO.Directory.CreateDirectory(Folder);
            AssetDatabase.Refresh();
            var result = new List<AuthoringPaletteEntry>();
            var library = JsonConvert.DeserializeObject<Library>(Resources.Load<TextAsset>("QuietCamp/ArtCompositions/CampModelLibrary").text);
            foreach (var model in library.models)
            {
                string category = model.id.Contains("tree") || model.id.Contains("willow") ? "Дерева" : model.id.Contains("reed") ? "Кущі" : model.id.Contains("sunflower") ? "Квіти" : model.id.Contains("fence") ? "Паркани" : "Містки";
                AddModel(result, model, category, model.id.Replace("ua_", "").Replace('_', ' '), false);
            }
            var plants = CozyVegetationLibrary.Load();
            foreach (var plant in plants.plants.Where(p => p.mesh != null && p.mesh.isReadable))
            {
                var vertices = new List<float>(); var normals = new List<float>(); var colors = new List<int>();
                var points = plant.mesh.vertices; var sourceNormals = plant.mesh.normals;
                for (int slot = 0; slot < plant.mesh.subMeshCount; slot++) foreach (int index in plant.mesh.GetTriangles(slot))
                {
                    var point = points[index]; var normal = sourceNormals.Length > index ? sourceNormals[index] : Vector3.up;
                    vertices.AddRange(new[] { point.x, point.y, point.z }); normals.AddRange(new[] { normal.x, normal.y, normal.z });
                    var color = plant.colors[Math.Min(slot, plant.colors.Length - 1)]; colors.Add(Rgb(color));
                }
                AddModel(result, new CampArtDefinition.Model { id = "cover_" + plant.id, shading = "foliage", vertices = vertices.ToArray(), normals = normals.ToArray(), colors = colors.ToArray() },
                    plant.kind == CozyVegetationLibrary.PlantKind.Flower ? "Квіти" : plant.kind == CozyVegetationLibrary.PlantKind.Shrub ? "Кущі" : "Трава", plant.id, true);
            }
            var blade = new CampArtDefinition.Model { id = "cover_blades", shading = "foliage",
                vertices = new[] { -.02f, 0, 0, 0, .17f, 0, .02f, 0, 0, 0, 0, -.02f, 0, .14f, 0, 0, 0, .02f },
                normals = Enumerable.Repeat(new[] { 0f, 0, 1 }, 6).SelectMany(v => v).ToArray(), colors = Enumerable.Repeat(0x809D52, 6).ToArray() };
            AddModel(result, blade, "Трава", "Дрібна трава", true);
            foreach (var item in AssetCatalog.Load().Entries.Where(e => e.prefab != null && !e.assetId.StartsWith("tent", StringComparison.Ordinal)))
            {
                string path = Folder + "/" + item.assetId + ".prefab";
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                {
                    var copy = UnityEngine.Object.Instantiate(item.prefab); copy.name = item.assetId;
                    var marker = copy.AddComponent<LevelAuthoringAsset>(); marker.stableId = item.assetId; marker.baseScale = copy.transform.localScale;
                    foreach (var collider in copy.GetComponentsInChildren<Collider>()) UnityEngine.Object.DestroyImmediate(collider);
                    prefab = PrefabUtility.SaveAsPrefabAsset(copy, path); UnityEngine.Object.DestroyImmediate(copy);
                }
                result.Add(new AuthoringPaletteEntry { id = item.assetId, label = item.assetId.Replace('_', ' '), category = item.assetId.Contains("tree") ? "Дерева" : item.assetId.Contains("stone") ? "Каміння" : "Декор", prefab = prefab });
            }
            AssetDatabase.SaveAssets(); return result.ToArray();
        }
        static int Rgb(Color color) => Mathf.RoundToInt(color.r * 255) << 16 | Mathf.RoundToInt(color.g * 255) << 8 | Mathf.RoundToInt(color.b * 255);
        static void AddModel(List<AuthoringPaletteEntry> result, CampArtDefinition.Model model, string category, string label, bool small)
        {
            string file = new string(model.id.Select(c => char.IsLetterOrDigit(c) || c == '_' ? c : '_').ToArray());
            string path = Folder + "/" + file + ".prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                var mesh = new Mesh { name = model.id, indexFormat = IndexFormat.UInt32 };
                int count = model.colors.Length;
                mesh.vertices = Enumerable.Range(0, count).Select(i => new Vector3(model.vertices[i * 3], model.vertices[i * 3 + 1], model.vertices[i * 3 + 2])).ToArray();
                mesh.normals = Enumerable.Range(0, count).Select(i => new Vector3(model.normals[i * 3], model.normals[i * 3 + 1], model.normals[i * 3 + 2])).ToArray();
                mesh.colors = model.colors.Select(c => QualitySettings.activeColorSpace == ColorSpace.Linear ? CampArtDefinition.Color(c).linear : CampArtDefinition.Color(c)).ToArray();
                mesh.triangles = Enumerable.Range(0, count).ToArray(); mesh.RecalculateBounds();
                AssetDatabase.CreateAsset(mesh, Folder + "/" + file + ".asset");
                var material = new Material(Resources.Load<Shader>("QuietCamp/CampArtLit")); material.SetColor("_ArtAmbient", new Color(.35f, .4f, .29f));
                material.SetFloat("_ArtGrass", model.shading == "foliage" ? 1 : 0); AssetDatabase.CreateAsset(material, Folder + "/" + file + ".mat");
                var go = new GameObject(model.id); go.AddComponent<MeshFilter>().sharedMesh = mesh; go.AddComponent<MeshRenderer>().sharedMaterial = material;
                var marker = go.AddComponent<LevelAuthoringAsset>(); marker.stableId = model.id; marker.packedModel = true; marker.smallPlant = small;
                if (small && model.id != "cover_blades") go.transform.localScale = Vector3.one * CozyVegetationLibrary.GroundcoverScale(category == "Квіти" ? CozyVegetationLibrary.PlantKind.Flower : category == "Кущі" ? CozyVegetationLibrary.PlantKind.Shrub : CozyVegetationLibrary.PlantKind.Leaf, mesh.bounds, .18f);
                marker.baseScale = Vector3.one;
                prefab = PrefabUtility.SaveAsPrefabAsset(go, path); UnityEngine.Object.DestroyImmediate(go);
            }
            result.Add(new AuthoringPaletteEntry { id = model.id, label = label, category = category, prefab = prefab, model = model, smallPlant = small });
        }
    }
}
