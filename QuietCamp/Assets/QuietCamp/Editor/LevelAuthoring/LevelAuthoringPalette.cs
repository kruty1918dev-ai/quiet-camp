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
        public AuthoringPropDescriptor prop;
    }
    public static class LevelAuthoringPalette
    {
        public const string Folder = "Assets/QuietCamp/Editor/LevelAuthoring/Palette";
        sealed class Library { public CampArtDefinition.Model[] models; }
        sealed class PackedProjectModel { public string id; public float[] data; public int[] colors; }
        static AuthoringPaletteEntry[] _entries;
        public static AuthoringPaletteEntry[] Entries => _entries ?? (_entries = Build());
        public static AuthoringPaletteEntry Get(string id) => Entries.FirstOrDefault(e => e.id == id) ?? throw new InvalidOperationException("Невідомий об’єкт: " + id);
        public static void ResetCache() { _entries = null; AuthoringPropCatalog.ResetCache(); }
        public static void EnsureDocumentModels(IEnumerable<CampArtDefinition.Model> models)
        {
            var entries = Entries.ToList();
            foreach (var model in models.Where(m => !entries.Any(e => e.id == m.id)))
                AddModel(entries, model, "Декор", "Деталь наявного рівня", model.shading == "foliage");
            _entries = entries.ToArray();
        }
        static AuthoringPaletteEntry[] Build()
        {
            System.IO.Directory.CreateDirectory(Folder);
            AssetDatabase.Refresh();
            var result = new List<AuthoringPaletteEntry>();
            var library = JsonConvert.DeserializeObject<Library>(Resources.Load<TextAsset>("QuietCamp/ArtCompositions/CampModelLibrary").text);
            foreach (var model in library.models)
            {
                string category = model.id.Contains("tree") || model.id.Contains("willow") ? "Дерева" : model.id.Contains("reed") ? "Кущі" : model.id.Contains("sunflower") ? "Квіти" : model.id.Contains("fence") ? "Паркани" : "Містки";
                AddModel(result, model, category, Label(model.id), false);
            }
            const string ruralLibrary = "Assets/QuietCamp/Authoring/Roadmap/Models/EnvironmentKit/models.json";
            if (System.IO.File.Exists(ruralLibrary))
            {
                var sourceModels = JsonConvert.DeserializeObject<PackedProjectModel[]>(System.IO.File.ReadAllText(ruralLibrary)).ToList();
                const string originalGeometry = "Assets/QuietCamp/Editor/RoadmapArchive/roadmap_culture_models.json";
                if (System.IO.File.Exists(originalGeometry))
                    sourceModels.AddRange(JsonConvert.DeserializeObject<PackedProjectModel[]>(System.IO.File.ReadAllText(originalGeometry)).Where(m => m.id == "ua_wheat_patch"));
                foreach (var source in sourceModels)
                {
                    if (result.Any(e => e.id == source.id) || source.id.EndsWith("_lod", StringComparison.Ordinal)) continue;
                    if (source.data == null || source.colors == null || source.data.Length != source.colors.Length * 18)
                        throw new InvalidOperationException("Некоректна модель палітри: " + source.id);
                    int count = source.colors.Length * 3;
                    var model = new CampArtDefinition.Model { id = source.id, shading = "solid", vertices = new float[count * 3], normals = new float[count * 3], colors = new int[count] };
                    for (int i = 0; i < count; i++)
                    {
                        Array.Copy(source.data, i * 6, model.vertices, i * 3, 3);
                        Array.Copy(source.data, i * 6 + 3, model.normals, i * 3, 3);
                        model.colors[i] = source.colors[i / 3];
                    }
                    AddModel(result, model, Category(model.id), Label(model.id), model.id.Contains("field_weeds"));
                }
            }
            var propModels = JsonConvert.DeserializeObject<Library>(System.IO.File.ReadAllText(AuthoringPropCatalog.ModelPath)).models;
            foreach (var prop in AuthoringPropCatalog.Entries)
            {
                var model = propModels.Single(m => m.id == prop.id);
                AddModel(result, model, prop.category, prop.label, false, true);
                result[result.Count - 1].prop = prop;
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
                    plant.kind == CozyVegetationLibrary.PlantKind.Flower ? "Квіти" : plant.kind == CozyVegetationLibrary.PlantKind.Shrub ? "Кущі" : "Трава", Label(plant.id), true);
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
                result.Add(new AuthoringPaletteEntry { id = item.assetId, label = item.assetId.StartsWith("custom_") ? item.prefab.name : Label(item.assetId), category = item.assetId.Contains("tree") ? "Дерева" : item.assetId.Contains("stone") ? "Каміння" : "Декор", prefab = prefab });
            }
            AssetDatabase.SaveAssets(); return result.ToArray();
        }
        static int Rgb(Color color) => Mathf.RoundToInt(color.r * 255) << 16 | Mathf.RoundToInt(color.g * 255) << 8 | Mathf.RoundToInt(color.b * 255);
        static string Label(string id)
        {
            switch(id)
            {
                case "ua_orchard_tree": return "Садове дерево";
                case "ua_young_willow": return "Молода верба";
                case "ua_reed_clump": return "Очерет";
                case "ua_sunflower_patch": return "Соняхи";
                case "ua_wheat_patch": return "Пшениця";
                case "ua_wattle_fence": return "Тин";
                case "ua_plank_bridge": return "Дощатий місток";
                case "ua_abandoned_house": return "Старенька хата";
                case "ua_forester_hut": return "Лісова хатина";
                case "ua_barn": return "Комора";
                case "ua_chicken_coop": return "Курник";
                case "ua_beehive": return "Вулик";
                case "ua_well_sweep": return "Криниця-журавель";
                case "ua_picket_fence_damaged": return "Старий штахетник";
                case "ua_valley_house": return "Сільська хата";
                case "ua_valley_well": return "Криниця";
                case "ua_valley_fence": return "Дерев’яний паркан";
                case "ua_valley_gate": return "Ворота";
                case "ua_power_pylon_rusted": return "Стара опора";
                case "ua_power_pylon_fallen": return "Повалена опора";
                case "ua_bus_shelter_mosaic": return "Зупинка з мозаїкою";
                case "ua_hydro_service_building": return "Невелика технічна будівля";
                case "ua_mooring_post": return "Причальний стовпчик";
                case "ua_chicken": return "Курка";
                case "ua_field_weeds": return "Польові трави";
                case "ua_poplar": return "Тополя";
                case "ua_village_wood_pole": return "Дерев’яний стовп";
                case "ua_village_concrete_pole": return "Бетонний стовп";
                case "ua_village_broken_pole": return "Похилений стовп";
                case "ua_valley_footbridge": return "Пішохідний місток";
                case "ua_valley_bridge_remnants": return "Старі дошки містка";
                case "ua_pond_spillway_ruin": return "Кам’яний водозлив";
                case "ua_valley_fence_broken_0": return "Паркан з просвітом";
                case "ua_valley_fence_broken_1": return "Старий паркан — секція";
                case "ua_valley_fence_broken_2": return "Низький паркан";
                case "ua_dam_breached": return "Берегова кам’яна кладка";
                case "ua_valley_poplar": return "Висока тополя";
                case "ua_valley_willow": return "Прибережна верба";
                case "ua_valley_orchard": return "Садове дерево — варіант";
                case "grass_leafs": return "Листяна трава";
                case "grass_leafsLarge": return "Велика листяна трава";
                case "plant_bushSmall": return "Низький кущ";
                case "plant_bushDetailed": return "Пишний кущ";
                case "flower_purpleA": return "Лілові квіти";
                case "flower_yellowA": case "flower_yellowB": return "Жовті квіти";
                case "tree_default": return "Листяне дерево";
                case "tree_pineRoundA": return "Сосна";
                case "stone_largeA": return "Великий камінь";
                case "stump_round": return "Пень";
                case "campfire_stones": return "Кам’яне вогнище";
                case "log": return "Колода";
                case "log_stack": return "Складені колоди";
                case "grass": return "Пучок трави";
                case "sign": return "Вказівник";
                default: return id.Replace('_',' ');
            }
        }
        static string Category(string id)
        {
            if (id.Contains("willow") || id.Contains("poplar") || id.Contains("orchard")) return "Дерева";
            if (id.Contains("reed")) return "Кущі";
            if (id.Contains("weeds") || id.Contains("wheat")) return "Трава";
            if (id.Contains("sunflower")) return "Квіти";
            if (id.Contains("fence") || id.Contains("gate")) return "Паркани";
            if (id.Contains("bridge")) return "Містки";
            if (id.Contains("house") || id.Contains("hut") || id.Contains("barn") || id.Contains("coop") || id.Contains("building")) return "Будівлі";
            return "Декор";
        }
        public static void RegisterPrefab(GameObject prefab)
        {
            string path = AssetDatabase.GetAssetPath(prefab);
            if (string.IsNullOrEmpty(path) || !path.StartsWith("Assets/", StringComparison.Ordinal) || !PrefabUtility.IsPartOfPrefabAsset(prefab))
                throw new ArgumentException("Перетягніть prefab із вікна Project.");
            string guid = AssetDatabase.AssetPathToGUID(path), id = "custom_" + guid;
            var catalog = AssetCatalog.Load();
            if (catalog.Entries.Any(e => e.assetId == id)) return;
            const string registryPath = "Assets/QuietCamp/Authoring/LevelObjects.json";
            var registry = System.IO.File.Exists(registryPath)
                ? JsonConvert.DeserializeObject<Dictionary<string, string>>(System.IO.File.ReadAllText(registryPath))
                : new Dictionary<string, string>();
            registry[id] = guid;
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(registryPath));
            System.IO.File.WriteAllText(registryPath, JsonConvert.SerializeObject(registry, Formatting.Indented));
            var serialized = new SerializedObject(catalog); var entries = serialized.FindProperty("_entries");
            entries.arraySize++; var entry = entries.GetArrayElementAtIndex(entries.arraySize - 1);
            entry.FindPropertyRelative("assetId").stringValue = id;
            entry.FindPropertyRelative("prefab").objectReferenceValue = prefab;
            entry.FindPropertyRelative("role").stringValue = "decor";
            entry.FindPropertyRelative("logicalFootprint").stringValue = "none";
            serialized.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(catalog); AssetDatabase.SaveAssets();
            ResetCache();
        }
        static void AddModel(List<AuthoringPaletteEntry> result, CampArtDefinition.Model model, string category, string label, bool small, bool refresh = false)
        {
            string file = new string(model.id.Select(c => char.IsLetterOrDigit(c) || c == '_' ? c : '_').ToArray());
            string path = Folder + "/" + file + ".prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null && refresh)
            {
                string signature;
                using (var hash = System.Security.Cryptography.SHA256.Create())
                    signature = Convert.ToBase64String(hash.ComputeHash(System.Text.Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(model))));
                var importer = AssetImporter.GetAtPath(path);
                if (importer.userData != signature)
                {
                    var mesh = prefab.GetComponent<MeshFilter>().sharedMesh;
                    mesh.Clear(); PopulateMesh(mesh, model); EditorUtility.SetDirty(mesh);
                    importer.userData = signature; importer.SaveAndReimport();
                }
            }
            if (prefab == null)
            {
                var mesh = new Mesh { name = model.id, indexFormat = IndexFormat.UInt32 };
                PopulateMesh(mesh, model);
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
        static void PopulateMesh(Mesh mesh, CampArtDefinition.Model model)
        {
            int count = model.colors.Length;
            mesh.vertices = Enumerable.Range(0, count).Select(i => new Vector3(model.vertices[i * 3], model.vertices[i * 3 + 1], model.vertices[i * 3 + 2])).ToArray();
            mesh.normals = Enumerable.Range(0, count).Select(i => new Vector3(model.normals[i * 3], model.normals[i * 3 + 1], model.normals[i * 3 + 2])).ToArray();
            mesh.colors = model.colors.Select(c => QualitySettings.activeColorSpace == ColorSpace.Linear ? CampArtDefinition.Color(c).linear : CampArtDefinition.Color(c)).ToArray();
            mesh.triangles = Enumerable.Range(0, count).ToArray(); mesh.RecalculateBounds();
        }
    }
}
