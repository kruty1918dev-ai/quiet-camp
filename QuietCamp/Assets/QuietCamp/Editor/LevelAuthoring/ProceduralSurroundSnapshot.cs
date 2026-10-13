using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using QuietCamp.Domain;
using QuietCamp.Infrastructure;
using QuietCamp.Presentation.World;
using UnityEngine;

namespace QuietCamp.Editor.LevelAuthoring
{
    // Evaluate the same generator used by gameplay, then retain its actual geometry
    // and poses. No map scenes or map behaviour are activated by this import.
    static class ProceduralSurroundSnapshot
    {
        public static CampArtDefinition Capture(LevelData level)
        {
            var art = LevelAuthoringDocument.Create(false).art;
            art.width = level.width; art.height = level.height;
            art.instances = Array.Empty<CampArtDefinition.Instance>(); art.cover = Array.Empty<CampArtDefinition.CoverPoint>();
            art.streamCentre = 100; art.meadowRelief = 0; art.blurPixels = 0;
            art.cameraEuler = new[] { CameraFitter.Euler.x, CameraFitter.Euler.y, CameraFitter.Euler.z };
            var root = new GameObject("Temporary canonical surround snapshot") { hideFlags = HideFlags.HideAndDontSave };
            var models = new List<CampArtDefinition.Model>(); var instances = new List<CampArtDefinition.Instance>();
            var ground = new List<LandscapeMesh>();
            try
            {
                DecorSpawner.Spawn(level, AssetCatalog.Load(), root.transform);
                var cameraObject = new GameObject("Snapshot camera"); cameraObject.transform.SetParent(root.transform, false);
                var camera = cameraObject.AddComponent<Camera>(); camera.enabled = false;
                CameraFitter.Configure(camera); camera.aspect = art.cameraAspect;
                var bounds = CameraFitter.ContentBounds(level);
                camera.orthographicSize = Mathf.Max(level.width, level.height) * .85f;
                camera.transform.position = bounds.center - camera.transform.forward * CameraFitter.Distance;
                art.orthographicSize = camera.orthographicSize; art.cameraTarget = new[] { bounds.center.x, bounds.center.y, bounds.center.z };
                if (!BakedCampEnvironment.Uses(level))
                {
                    var composition = new GameObject("Canonical environment"); composition.transform.SetParent(root.transform, false);
                    composition.AddComponent<EnvironmentComposer>().Configure(level, camera, root.transform, AssetCatalog.Load(), false);
                    var floor = new GameObject("Canonical forest floor"); floor.transform.SetParent(root.transform, false);
                    floor.AddComponent<VisibleForestFloor>().Configure(level, camera, false);
                }
                foreach (var filter in root.GetComponentsInChildren<MeshFilter>())
                {
                    var renderer = filter.GetComponent<MeshRenderer>(); var mesh = filter.sharedMesh;
                    if (renderer == null || mesh == null || !renderer.enabled) continue;
                    // Contact decals are an atmosphere overlay rather than editable vegetation.
                    if (filter.name == "ForestContactDepth" || filter.name.Contains("Contact")) continue;
                    bool water = renderer.sharedMaterials.Any(m => m != null && m.shader != null && m.shader.name.Contains("Stylized Water"));
                    bool forestFloor = filter.GetComponentInParent<VisibleForestFloor>() != null;
                    var vertices = mesh.vertices; var normals = mesh.normals; var colors = mesh.colors;
                    var points = new List<float>(); var directions = new List<float>(); var tint = new List<int>();
                    var matrix = root.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                    for (int slot = 0; slot < mesh.subMeshCount; slot++)
                    {
                        var material = renderer.sharedMaterials.ElementAtOrDefault(slot);
                        Color baseColor = Color.white;
                        if (material != null && material.HasProperty("_BaseColor")) baseColor = material.GetColor("_BaseColor");
                        else if (material != null && material.HasProperty("_Color")) baseColor = material.GetColor("_Color");
                        var indices = mesh.GetTriangles(slot);
                        for (int triangle = 0; triangle < indices.Length; triangle += 3)
                        {
                            var a = matrix.MultiplyPoint3x4(vertices[indices[triangle]]);
                            var b = matrix.MultiplyPoint3x4(vertices[indices[triangle + 1]]);
                            var triangleC = matrix.MultiplyPoint3x4(vertices[indices[triangle + 2]]);
                            // The forest-floor shader hides its clearing edge. Bake that
                            // exclusion into geometry so exported foliage cannot cover cells.
                            if (forestFloor
                                && Mathf.Min(a.x, b.x, triangleC.x) < level.width / 2f + .2f && Mathf.Max(a.x, b.x, triangleC.x) > -level.width / 2f - .2f
                                && Mathf.Min(a.z, b.z, triangleC.z) < level.height / 2f + .2f && Mathf.Max(a.z, b.z, triangleC.z) > -level.height / 2f - .2f) continue;
                            for (int vertex = 0; vertex < 3; vertex++)
                            {
                            int index = indices[triangle + vertex];
                            var p = matrix.MultiplyPoint3x4(vertices[index]);
                            var n = normals.Length == vertices.Length ? matrix.MultiplyVector(normals[index]).normalized : Vector3.up;
                            var c = (colors.Length == vertices.Length ? colors[index] : Color.white) * baseColor;
                            points.AddRange(new[] { p.x, p.y, p.z }); directions.AddRange(new[] { n.x, n.y, n.z });
                            tint.Add(Rgb(c));
                            }
                        }
                    }
                    if (points.Count == 0) continue;
                    if (filter.GetComponentInParent<MeadowSurface>() != null || water || filter.name.Contains("Ground")
                        || filter.name.Contains("Trail") || filter.name.Contains("Earth") || filter.name.Contains("Shore"))
                    {
                        var sourceUv = mesh.uv; var sourceFlow = new List<Vector3>(); mesh.GetUVs(2, sourceFlow);
                        var triangleOrder = Enumerable.Range(0, mesh.subMeshCount).SelectMany(slot => mesh.GetTriangles(slot)).ToArray();
                        ground.Add(new LandscapeMesh { name = water ? "Existing river" : "Existing meadow", water = water, vertices = points.ToArray(), normals = directions.ToArray(),
                            uv = triangleOrder.SelectMany(i => sourceUv.Length == vertices.Length ? new[] { sourceUv[i].x, sourceUv[i].y } : new[] { 0f, 0f }).ToArray(),
                            flow = water && sourceFlow.Count == vertices.Length ? triangleOrder.SelectMany(i => new[] { sourceFlow[i].x, sourceFlow[i].y, sourceFlow[i].z }).ToArray() : null,
                            colors = Enumerable.Range(0, triangleOrder.Length).SelectMany(i => { var c = water && colors.Length == vertices.Length ? colors[triangleOrder[i]] : CampArtDefinition.Color(tint[i]); return new[] { c.r, c.g, c.b, c.a }; }).ToArray(),
                            triangles = Enumerable.Range(0, tint.Count).ToArray() });
                        continue;
                    }
                    // A bounded renderer becomes a movable Lego piece; a generated vegetation
                    // batch stays one piece until the user replaces it with individually painted plants.
                    var center = renderer.bounds.center; center.y = 0;
                    for (int i = 0; i < points.Count; i += 3) { points[i] -= center.x; points[i + 2] -= center.z; }
                    string id = "existing_" + level.id + "_" + models.Count;
                    var model = new CampArtDefinition.Model { vertices = points.ToArray(), normals = directions.ToArray(), colors = tint.ToArray(), shading = "solid" };
                    using (var hash = SHA256.Create()) id += "_" + BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(model)))).Replace("-", "").Substring(0, 16);
                    model.id = id; models.Add(model);
                    instances.Add(new CampArtDefinition.Instance { id = filter.name + "/" + models.Count, modelId = id, x = center.x, z = center.z, scale = 1, surfaceAttached = false });
                }
                art.models = art.models.Concat(models).ToArray(); art.instances = instances.ToArray();
                if (ground.Count > 0)
                {
                    const int resolution = 33;
                    art.landscape = new FrozenLandscape { resolution = resolution, size = 64, originX = -32, originZ = -32,
                        heights = Enumerable.Repeat(MeadowSurface.GroundY, resolution * resolution).ToArray(), meshes = ground.ToArray() };
                }
                return art;
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        static int Rgb(Color c) => Mathf.RoundToInt(Mathf.Clamp01(c.r) * 255) << 16
            | Mathf.RoundToInt(Mathf.Clamp01(c.g) * 255) << 8 | Mathf.RoundToInt(Mathf.Clamp01(c.b) * 255);
    }
}
