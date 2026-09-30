using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Kruty1918.Audio;
using Newtonsoft.Json.Linq;
using QuietCamp.Infrastructure;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Rendering;

namespace QuietCamp.Editor
{
    /// <summary>
    /// One-shot, idempotent content build: MTL -> URP Simple Lit materials,
    /// OBJ -> normalized wrapper prefabs, catalogs -> AssetCatalog/AudioCatalog
    /// ScriptableObjects. Re-running never duplicates assets. Invoke via
    /// Tools/Quiet Camp/Build MVP Content or -executeMethod in batch mode.
    /// </summary>
    public static class MvpContentBuilder
    {
        const string AssetCatalogJson = "Assets/QuietCamp/Resources/QuietCamp/asset_catalog.json";
        const string AudioCatalogJson = "Assets/QuietCamp/Resources/QuietCamp/audio_catalog.json";
        const string MaterialDir = "Assets/QuietCamp/Materials";
        const string PrefabDir = "Assets/QuietCamp/Prefabs/Models";
        const string CatalogAssetPath = "Assets/QuietCamp/Resources/QuietCamp/AssetCatalog.asset";
        const string AudioCatalogAssetPath = "Assets/QuietCamp/Resources/QuietCamp/AudioCatalog.asset";
        const string MixerPath = "Assets/QuietCamp/Audio/QC_Master.mixer";
        const int TentSelectableLayer = 9;

        [MenuItem("Tools/Quiet Camp/Build MVP Content")]
        public static void Run()
        {
            EnsureFolders();
            var materials = BuildMaterials();
            var entries = BuildPrefabs(materials);
            BuildAssetCatalog(entries);
            BuildAudioCatalog();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[QuietCamp] MVP content built: {entries.Count} prefab entries.");
        }

        static void EnsureFolders()
        {
            foreach (var dir in new[] { MaterialDir, "Assets/QuietCamp/Prefabs", PrefabDir })
                if (!AssetDatabase.IsValidFolder(dir))
                {
                    var parent = Path.GetDirectoryName(dir).Replace('\\', '/');
                    AssetDatabase.CreateFolder(parent, Path.GetFileName(dir));
                }
        }

        // ─── Materials ────────────────────────────────────────────────────────

        sealed class CatalogModel
        {
            public string name, prefab, sourceUnity, logicalFootprint, role;
            public float uniformScaleOBJ;
            public int visualYaw;
        }

        static List<CatalogModel> ReadModels()
        {
            var root = JObject.Parse(File.ReadAllText(AssetCatalogJson));
            var list = new List<CatalogModel>();
            foreach (var m in root["models"])
            {
                list.Add(new CatalogModel
                {
                    name = m.Value<string>("name"),
                    prefab = m.Value<string>("prefab"),
                    sourceUnity = m.Value<string>("sourceUnity"),
                    logicalFootprint = m.Value<string>("logicalFootprint"),
                    role = m.Value<string>("role"),
                    uniformScaleOBJ = m.Value<float>("uniformScaleOBJ"),
                    visualYaw = m.Value<int?>("visualYaw") ?? 0,
                });
            }
            return list;
        }

        static Dictionary<string, Material> BuildMaterials()
        {
            var result = new Dictionary<string, Material>(StringComparer.Ordinal);
            var shader = Shader.Find("Universal Render Pipeline/Simple Lit");
            if (shader == null) { Debug.LogError("[QuietCamp] URP Simple Lit shader not found."); return result; }

            foreach (var mtlPath in Directory.GetFiles("Assets/ThirdParty/KenneyNature/Models", "*.mtl"))
            {
                string current = null;
                foreach (var raw in File.ReadAllLines(mtlPath))
                {
                    var line = raw.Trim();
                    if (line.StartsWith("newmtl", StringComparison.Ordinal))
                    {
                        current = line.Substring(6).Trim();
                    }
                    else if (current != null && line.StartsWith("Kd", StringComparison.Ordinal))
                    {
                        var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length >= 4
                            && float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var r)
                            && float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var g)
                            && float.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var b))
                        {
                            var color = new Color(r, g, b);
                            result[current] = EnsureMaterial(current, color, shader);
                            current = null;
                        }
                    }
                }
            }
            return result;
        }

        static Material EnsureMaterial(string name, Color color, Shader shader)
        {
            var path = $"{MaterialDir}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
            }
            material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.05f);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0f);
            EditorUtility.SetDirty(material);
            return material;
        }

        // ─── Prefabs ──────────────────────────────────────────────────────────

        static List<AssetCatalog.Entry> BuildPrefabs(Dictionary<string, Material> materials)
        {
            var entries = new List<AssetCatalog.Entry>();
            foreach (var model in ReadModels())
            {
                var modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(model.sourceUnity);
                if (modelAsset == null)
                {
                    Debug.LogError($"[QuietCamp] Missing model asset {model.sourceUnity}");
                    continue;
                }
                var sourceRenderer = modelAsset.GetComponentInChildren<MeshRenderer>();
                var sourceFilter = modelAsset.GetComponentInChildren<MeshFilter>();
                if (sourceRenderer == null || sourceFilter == null || sourceFilter.sharedMesh == null)
                {
                    Debug.LogError($"[QuietCamp] Model {model.name} has no mesh.");
                    continue;
                }

                var root = new GameObject(model.name);
                try
                {
                    var visual = new GameObject("VisualCenter");
                    visual.transform.SetParent(root.transform, false);
                    var filter = visual.AddComponent<MeshFilter>();
                    filter.sharedMesh = sourceFilter.sharedMesh;
                    var renderer = visual.AddComponent<MeshRenderer>();
                    renderer.sharedMaterials = MapMaterials(sourceRenderer.sharedMaterials, materials);
                    var bounds = sourceFilter.sharedMesh.bounds;
                    var scale = model.uniformScaleOBJ;
                    visual.transform.localScale = new Vector3(scale, scale, scale);
                    visual.transform.localEulerAngles = new Vector3(0f, model.visualYaw, 0f);
                    visual.transform.localPosition = new Vector3(
                        -bounds.center.x * scale, -bounds.min.y * scale, -bounds.center.z * scale);

                    if (model.logicalFootprint == "2x2")
                    {
                        root.layer = TentSelectableLayer;
                        var door = new GameObject("DoorMarker");
                        door.transform.SetParent(root.transform, false);
                        door.transform.localPosition = new Vector3(0.5f, 0f, 1.5f);
                        var collider = root.AddComponent<BoxCollider>();
                        collider.size = new Vector3(1.8f, 1.4f, 1.8f);
                        collider.center = new Vector3(0f, 0.7f, 0f);
                    }

                    var prefabPath = model.prefab;
                    PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                    var saved = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                    entries.Add(new AssetCatalog.Entry
                    {
                        assetId = model.name,
                        prefab = saved,
                        logicalFootprint = model.logicalFootprint,
                        role = model.role,
                        visualYaw = model.visualYaw,
                    });
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(root);
                }
            }
            return entries;
        }

        static Material[] MapMaterials(Material[] imported, Dictionary<string, Material> materials)
        {
            var mapped = new Material[imported.Length];
            for (var i = 0; i < imported.Length; i++)
            {
                var key = imported[i] != null ? imported[i].name : null;
                if (key != null && key.EndsWith(" (Instance)", StringComparison.Ordinal))
                    key = key.Substring(0, key.Length - " (Instance)".Length);
                if (key != null && materials.TryGetValue(key, out var material))
                    mapped[i] = material;
                else
                {
                    Debug.LogWarning($"[QuietCamp] No MTL material for '{key}' — fallback gray.");
                    mapped[i] = EnsureMaterial("fallback", new Color(0.7f, 0.7f, 0.7f),
                        Shader.Find("Universal Render Pipeline/Simple Lit"));
                }
            }
            return mapped;
        }

        static void BuildAssetCatalog(List<AssetCatalog.Entry> entries)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<AssetCatalog>(CatalogAssetPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<AssetCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogAssetPath);
            }
            var so = new SerializedObject(catalog);
            var prop = so.FindProperty("_entries");
            prop.arraySize = entries.Count;
            for (var i = 0; i < entries.Count; i++)
            {
                var e = prop.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("assetId").stringValue = entries[i].assetId;
                e.FindPropertyRelative("prefab").objectReferenceValue = entries[i].prefab;
                e.FindPropertyRelative("logicalFootprint").stringValue = entries[i].logicalFootprint;
                e.FindPropertyRelative("role").stringValue = entries[i].role ?? string.Empty;
                e.FindPropertyRelative("visualYaw").intValue = entries[i].visualYaw;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
        }

        // ─── Audio catalog ────────────────────────────────────────────────────

        static void BuildAudioCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<QuietCampAudioCatalog>(AudioCatalogAssetPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<QuietCampAudioCatalog>();
                AssetDatabase.CreateAsset(catalog, AudioCatalogAssetPath);
            }

            var root = JObject.Parse(File.ReadAllText(AudioCatalogJson));
            var sounds = new List<AudioSoundDefinition>();
            foreach (var s in root["sounds"])
            {
                var assetPath = s.Value<string>("assetPath");
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(assetPath);
                if (clip == null)
                {
                    Debug.LogWarning($"[QuietCamp] Audio clip missing: {assetPath}");
                    continue;
                }
                var busName = s.Value<string>("bus");
                sounds.Add(new AudioSoundDefinition
                {
                    Key = s.Value<string>("key"),
                    Clip = clip,
                    Bus = Enum.TryParse(busName, out AudioBus bus) ? bus : AudioBus.Sfx,
                    Volume = s.Value<float?>("volume") ?? 1f,
                    Loop = s.Value<bool?>("loop") ?? false,
                    SpatialBlend = s.Value<float?>("spatialBlend") ?? 0f,
                    MinDistance = s.Value<float?>("minDistance") ?? 0f,
                    MaxDistance = s.Value<float?>("maxDistance") ?? 0f,
                    Priority = s.Value<int?>("priority") ?? 128,
                    MaxSimultaneous = s.Value<int?>("maxSimultaneous") ?? 8,
                    Channel = s.Value<string>("channel") ?? string.Empty,
                });
            }

            var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
            var busGroups = new List<AudioBusGroupBinding>();
            if (mixer != null)
            {
                foreach (AudioBus bus in Enum.GetValues(typeof(AudioBus)))
                {
                    var group = FindMixerGroup(mixer, bus.ToString());
                    if (group != null)
                        busGroups.Add(new AudioBusGroupBinding { Bus = bus, MixerGroup = group });
                }
            }

            var so = new SerializedObject(catalog);
            WriteArray(so.FindProperty("_sounds"), sounds.Count, (e, i) =>
            {
                var def = sounds[i];
                e.FindPropertyRelative("Key").stringValue = def.Key;
                e.FindPropertyRelative("Clip").objectReferenceValue = def.Clip;
                e.FindPropertyRelative("Bus").enumValueIndex = (int)def.Bus;
                e.FindPropertyRelative("Volume").floatValue = def.Volume;
                e.FindPropertyRelative("Loop").boolValue = def.Loop;
                e.FindPropertyRelative("SpatialBlend").floatValue = def.SpatialBlend;
                e.FindPropertyRelative("MinDistance").floatValue = def.MinDistance;
                e.FindPropertyRelative("MaxDistance").floatValue = def.MaxDistance;
                e.FindPropertyRelative("Channel").stringValue = def.Channel ?? string.Empty;
                e.FindPropertyRelative("Pitch").floatValue = 1f;
                e.FindPropertyRelative("Priority").intValue = def.Priority;
                e.FindPropertyRelative("MaxSimultaneous").intValue = def.MaxSimultaneous;
                e.FindPropertyRelative("PoolWarmup").intValue = 1;
            });
            WriteArray(so.FindProperty("_busGroups"), busGroups.Count, (e, i) =>
            {
                e.FindPropertyRelative("Bus").enumValueIndex = (int)busGroups[i].Bus;
                e.FindPropertyRelative("MixerGroup").objectReferenceValue = busGroups[i].MixerGroup;
            });
            so.FindProperty("_defaultPoolSize").intValue = 8;
            so.FindProperty("_persistAcrossScenes").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
        }

        static void WriteArray(SerializedProperty prop, int size, Action<SerializedProperty, int> write)
        {
            prop.arraySize = size;
            for (var i = 0; i < size; i++) write(prop.GetArrayElementAtIndex(i), i);
        }

        static AudioMixerGroup FindMixerGroup(AudioMixer mixer, string name)
        {
            foreach (var group in mixer.FindMatchingGroups(name))
                if (group.name == name) return group;
            return null;
        }
    }
}
