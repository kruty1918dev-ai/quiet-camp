using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace QuietCamp.Editor
{
    /// <summary>Offline, authored-palette thumbnails. Holding a card never renders another camera.</summary>
    public static class TentThumbnailBuilder
    {
        [MenuItem("Quiet Camp/Art/Refresh tent thumbnails")]
        public static void Generate()
        {
            const string directory = "Assets/QuietCamp/Resources/QuietCamp/UI/Tents";
            Directory.CreateDirectory(directory);
            var lights = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None)
                .Where(l => l.enabled).ToArray();
            var ambientMode = RenderSettings.ambientMode;
            var ambient = RenderSettings.ambientLight;
            var reflection = RenderSettings.reflectionIntensity;
            var root = new GameObject("Offline tent photography") { hideFlags = HideFlags.HideAndDontSave };
            var target = new RenderTexture(256, 256, 24, RenderTextureFormat.ARGB32);
            var photographyMaterials = new System.Collections.Generic.List<Material>();
            var previous = RenderTexture.active;
            try
            {
                foreach (var light in lights) light.enabled = false;
                RenderSettings.ambientMode = AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(.62f, .68f, .57f);
                RenderSettings.reflectionIntensity = .3f;
                root.transform.position = new Vector3(1000, 0, 1000);
                var cameraObject = new GameObject("Offline thumbnail camera"); cameraObject.transform.SetParent(root.transform, false);
                var camera = cameraObject.AddComponent<Camera>();
                camera.enabled = false; camera.orthographic = true; camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.clear; camera.cullingMask = 1 << 31;
                camera.allowHDR = false; camera.allowMSAA = false; camera.targetTexture = target;
                camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
                var sunObject = new GameObject("Offline warm sun"); sunObject.transform.SetParent(root.transform, false);
                var sun = sunObject.AddComponent<Light>(); sun.type = LightType.Directional;
                sun.color = new Color(1, .91f, .77f); sun.intensity = 1.15f; sun.cullingMask = 1 << 31;
                sun.transform.rotation = Quaternion.Euler(45, -35, 0);
                foreach (var asset in new[] { "tent_smallOpen", "tent_detailedOpen" })
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/QuietCamp/Prefabs/Models/" + asset + ".prefab");
                    if (prefab == null) throw new InvalidOperationException("Missing authored tent: " + asset);
                    var tent = UnityEngine.Object.Instantiate(prefab, root.transform, false);
                    foreach (var child in tent.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 31;
                    var renderers = tent.GetComponentsInChildren<Renderer>();
                    // Offline colour and facet lighting is independent of a
                    // loaded gameplay scene, sky probes or the Editor's sun.
                    var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/QuietCamp/Editor/TentThumbnail.shader");
                    foreach (var renderer in renderers)
                    {
                        var materials = renderer.sharedMaterials;
                        for (int i=0;i<materials.Length;i++)
                        {
                            var original = materials[i];
                            var material = new Material(shader) { hideFlags=HideFlags.HideAndDontSave };
                            material.SetColor("_BaseColor",original.HasProperty("_BaseColor") ? original.GetColor("_BaseColor") : original.color);
                            photographyMaterials.Add(material); materials[i]=material;
                        }
                        renderer.sharedMaterials=materials;
                    }
                    var bounds = renderers[0].bounds;
                    foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
                    camera.orthographicSize = bounds.extents.magnitude * 1.08f;
                    camera.transform.position = bounds.center + new Vector3(4, 3.1f, 4);
                    camera.transform.LookAt(bounds.center);
                    camera.Render(); RenderTexture.active = target;
                    var texture = new Texture2D(256, 256, TextureFormat.RGBA32, false);
                    texture.ReadPixels(new Rect(0, 0, 256, 256), 0, 0); texture.Apply();
                    string path = directory + "/" + asset + ".png";
                    File.WriteAllBytes(path, texture.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(texture);
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                    var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                    importer.textureType = TextureImporterType.Sprite;
                    importer.spriteImportMode = SpriteImportMode.Single; importer.alphaIsTransparency = true;
                    importer.mipmapEnabled = false; importer.maxTextureSize = 256;
                    importer.textureCompression = TextureImporterCompression.Compressed;
                    importer.SaveAndReimport();
                    UnityEngine.Object.DestroyImmediate(tent);
                    Debug.Log("[Tent thumbnails] Saved " + path);
                }
            }
            finally
            {
                RenderTexture.active = previous;
                UnityEngine.Object.DestroyImmediate(root);
                target.Release(); UnityEngine.Object.DestroyImmediate(target);
                foreach(var material in photographyMaterials) UnityEngine.Object.DestroyImmediate(material);
                RenderSettings.ambientMode = ambientMode; RenderSettings.ambientLight = ambient;
                RenderSettings.reflectionIntensity = reflection;
                foreach (var light in lights) if (light != null) light.enabled = true;
            }
            AssetDatabase.SaveAssets();
        }
    }
}
