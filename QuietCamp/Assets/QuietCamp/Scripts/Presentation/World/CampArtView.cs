using System;
using System.Collections.Generic;
using System.Linq;
using QuietCamp.Domain;
using QuietCamp.Infrastructure;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace QuietCamp.Presentation.World
{
    /// <summary>Builds an authored camp surround and its camera from one resolved JSON snapshot.</summary>
    public sealed class CampArtView : MonoBehaviour
    {
        readonly List<Mesh> _meshes = new List<Mesh>();
        readonly List<Material> _materials = new List<Material>();
        GameObject _root;
        Camera _camera;
        CampArtEdgeBlur _blur;
        VolumeProfile _volumeProfile;
        LevelData _level;
        public CampArtDefinition Definition { get; private set; }
        public int TriangleCount { get; private set; }

        public void Configure(CampArtDefinition definition, LevelData level, Transform parent, AssetCatalog catalog)
        {
            if (Definition != null) throw new InvalidOperationException("Camp art is already configured.");
            Definition = definition; _level = level; _camera = GetComponent<Camera>();
            _root = new GameObject("AuthoredCampSurround"); _root.transform.SetParent(parent, false);
            var shader = Resources.Load<Shader>("QuietCamp/CampArtLit");
            if (shader == null) throw new InvalidOperationException("Camp art lighting shader missing.");
            var material = new Material(shader) { name = "Camp art vertex palette" };
            material.SetColor("_ArtAmbient", CampArtDefinition.Color(definition.ambientColor) * .38f);
            _materials.Add(material);
            var catalogMeshes = new Dictionary<string, Mesh>(StringComparer.Ordinal);
            foreach (var source in definition.models)
            {
                var mesh = new Mesh { name = source.id, indexFormat = IndexFormat.UInt32 };
                int count = source.colors.Length; var positions = new Vector3[count]; var normals = new Vector3[count]; var colors = new Color[count];
                for (int i = 0; i < count; i++)
                {
                    int n = i * 3;
                    positions[i] = new Vector3(source.vertices[n], source.vertices[n + 1], source.vertices[n + 2]);
                    normals[i] = new Vector3(source.normals[n], source.normals[n + 1], source.normals[n + 2]);
                    colors[i] = Linear(CampArtDefinition.Color(source.colors[i]));
                }
                mesh.vertices = positions; mesh.normals = normals; mesh.colors = colors;
                mesh.triangles = Enumerable.Range(0, count).ToArray(); mesh.RecalculateBounds();
                _meshes.Add(mesh); catalogMeshes.Add(source.id, mesh);
            }
            var batches = new List<CombineInstance>();
            foreach (var item in definition.instances)
            {
                var position = new Vector3(item.x, item.y, item.z);
                var rotation = Quaternion.Euler(0, item.yaw, 0);
                if (!string.IsNullOrEmpty(item.assetId))
                {
                    var prefab = catalog?.Prefab(item.assetId);
                    if (prefab == null) throw new InvalidOperationException("Camp art asset missing: " + item.assetId);
                    var instance = Instantiate(prefab, _root.transform);
                    instance.name = "Scenery_" + item.assetId; instance.transform.localPosition = position;
                    instance.transform.localRotation = rotation; instance.transform.localScale *= item.scale;
                    foreach (var collider in instance.GetComponentsInChildren<Collider>()) DestroyOwned(collider);
                    foreach (var child in instance.GetComponentsInChildren<Transform>()) child.gameObject.layer = BoardRenderer.DecorLayer;
                }
                else batches.Add(new CombineInstance { mesh = catalogMeshes[item.modelId], transform = Matrix4x4.TRS(position, rotation, Vector3.one * item.scale) });
            }
            var combined = new Mesh { name = "Authored camp trees, reeds and bridge", indexFormat = IndexFormat.UInt32 };
            combined.CombineMeshes(batches.ToArray(), true, true); _meshes.Add(combined);
            AddMesh("CampModels", combined, material);
            BuildGround(material);
            BuildGroundcover(material);
            BuildStream(material);
            ConfigureVolume();
            _blur = new CampArtEdgeBlur(_camera, definition.blurPixels);
            _camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
        }
        public void FitCamera(Rect gameplay)
        {
            var data = Definition;
            _camera.transform.rotation = Quaternion.Euler(CampArtDefinition.Vector(data.cameraEuler));
            _camera.orthographic = true;
            float areaAspect = _camera.aspect * gameplay.width / Mathf.Max(.01f, gameplay.height);
            float size = data.orthographicSize * Mathf.Max(1, data.cameraAspect / areaAspect) / Mathf.Max(.01f, gameplay.height);
            _camera.orthographicSize = size;
            var target = CampArtDefinition.Vector(data.cameraTarget);
            var centre = gameplay.center;
            var offset = _camera.transform.right * ((.5f - centre.x) * 2 * size * _camera.aspect)
                + _camera.transform.up * ((.5f - centre.y) * 2 * size);
            _camera.transform.position = target + offset - _camera.transform.forward * 30;
            _camera.nearClipPlane = .1f; _camera.farClipPlane = 90;
            _blur?.ProtectBoard(_level);
        }
        public void ApplyLighting(Light sun, float weatherMultiplier = 1)
        {
            if (sun == null) return;
            sun.transform.rotation = Quaternion.Euler(CampArtDefinition.Vector(Definition.sunEuler));
            sun.color = CampArtDefinition.Color(Definition.sunColor);
            sun.intensity = Definition.sunIntensity * Mathf.Max(.75f, weatherMultiplier);
            sun.shadows = LightShadows.Soft; sun.shadowStrength = .56f; sun.shadowBias = .025f; sun.shadowNormalBias = .12f;
            sun.GetUniversalAdditionalLightData().softShadowQuality = SoftShadowQuality.High;
        }
        void ConfigureVolume()
        {
            _volumeProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            var colors = _volumeProfile.Add<ColorAdjustments>(true);
            colors.postExposure.Override(Definition.exposure); colors.saturation.Override(Definition.saturation); colors.contrast.Override(Definition.contrast);
            var tonemap = _volumeProfile.Add<Tonemapping>(true); tonemap.mode.Override(TonemappingMode.Neutral);
            var vignette = _volumeProfile.Add<Vignette>(true); vignette.intensity.Override(.08f); vignette.smoothness.Override(.65f);
            var volume = _root.AddComponent<Volume>(); volume.isGlobal = true; volume.priority = 20; volume.sharedProfile = _volumeProfile;
        }
        void BuildGround(Material material)
        {
            var positions = new List<Vector3>(); var colors = new List<Color>(); var rng = new System.Random(Definition.seed);
            for (int x = -32; x < 32; x++) for (int z = -32; z < 32; z++)
            {
                float s = .55f; float wx = x * s, wz = z * s;
                var color = Color.Lerp(CampArtDefinition.Color(Definition.groundLow), CampArtDefinition.Color(Definition.groundHigh), (float)rng.NextDouble() * .04f + .35f);
                Quad(positions, colors, new Vector3(wx, -.045f, wz), new Vector3(wx, -.045f, wz + s), new Vector3(wx + s, -.045f, wz + s), new Vector3(wx + s, -.045f, wz), color);
            }
            var groundMaterial = new Material(material); groundMaterial.SetFloat("_ArtGround", 1); _materials.Add(groundMaterial);
            AddMesh("MeadowGround", MakeMesh("Meadow triangles", positions, colors), groundMaterial, false);
        }
        void BuildGroundcover(Material material)
        {
            var vertices = new List<Vector3>(); var colors = new List<Color>(); var rng = new System.Random(Definition.seed + 31);
            var library = CozyVegetationLibrary.Load();
            if (library != null && library.plants.Length > 0)
            {
                BuildLibraryGroundcover(vertices, colors, library, rng);
                var foliage = new Material(material); foliage.SetFloat("_ArtGrass", 1); _materials.Add(foliage);
                AddMesh("GroupedProjectVegetation", MakeMesh("Project groundcover", vertices, colors), foliage, false);
                return;
            }
            for (int i = 0; i < 14500; i++)
            {
                float x = (float)(rng.NextDouble() * 32 - 16), z = (float)(rng.NextDouble() * 32 - 16);
                if (Mathf.Abs(x) < _level.width * .5f + .32f && Mathf.Abs(z) < _level.height * .5f + .32f) continue;
                var uv = ViewGround(x, z); if (Mathf.Abs(uv.x - StreamU(uv.y)) < Definition.streamWidth * .65f) continue;
                float grouping = .5f + .5f * Mathf.Sin(x * 1.7f + Mathf.Cos(z * .85f));
                if (rng.NextDouble() > .38f + .5f * grouping) continue;
                float height = .08f + (float)rng.NextDouble() * .24f, width = height * .24f;
                var color = Color.Lerp(CampArtDefinition.Color(0x647E3F), CampArtDefinition.Color(0x96AB54), (float)rng.NextDouble());
                for (int blade = 0; blade < 3; blade++)
                {
                    float yaw = (float)rng.NextDouble() * Mathf.PI * 2;
                    var side = new Vector3(Mathf.Cos(yaw), 0, Mathf.Sin(yaw)) * width;
                    var root = new Vector3(x, -.02f, z);
                    Triangle(vertices, colors, root - side, root + Vector3.up * height + side * .2f, root + side, color);
                }
                if (i % 9 == 0)
                {
                    var centre = new Vector3(x, height * .75f, z);
                    float radius = .075f;
                    for (int petal = 0; petal < 5; petal++)
                    {
                        float a = petal * Mathf.PI * .4f, b = a + .72f;
                        Triangle(vertices, colors, centre, centre + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * radius,
                            centre + new Vector3(Mathf.Cos(b), .025f, Mathf.Sin(b)) * radius, new Color(.95f, .92f, .72f));
                    }
                }
            }
            var grassMaterial = new Material(material); grassMaterial.SetFloat("_ArtGrass", 1); _materials.Add(grassMaterial);
            AddMesh("GroupedGrassAndDaisies", MakeMesh("Camp groundcover", vertices, colors), grassMaterial, false);
        }
        void BuildLibraryGroundcover(List<Vector3> vertices, List<Color> colors, CozyVegetationLibrary library, System.Random rng)
        {
            // Use the game's real readable CC0 meshes, with their baked source colours.
            var shapes = library.plants.Where(p => p.mesh != null && p.mesh.isReadable && p.colors != null && p.colors.Length > 0)
                .Select(p =>
                {
                    var source = p.mesh.vertices; var points = new List<Vector3>(); var palette = new List<Color>();
                    for (int slot = 0; slot < p.mesh.subMeshCount; slot++)
                    foreach (int index in p.mesh.GetTriangles(slot))
                    { points.Add(source[index]); palette.Add(p.colors[Mathf.Min(slot, p.colors.Length - 1)]); }
                    return new { plant = p, points, palette };
                }).ToArray();
            if (shapes.Length == 0) throw new InvalidOperationException("Camp vegetation has no readable source meshes.");
            for (int i = 0; i < 7800; i++)
            {
                float x = (float)(rng.NextDouble() * 32 - 16), z = (float)(rng.NextDouble() * 32 - 16);
                if (Mathf.Abs(x) < _level.width * .5f + .38f && Mathf.Abs(z) < _level.height * .5f + .38f) continue;
                var uv = ViewGround(x, z);
                if (Mathf.Abs(uv.x - StreamU(uv.y)) < Definition.streamWidth * .65f) continue;
                float grouping = .5f + .5f * Mathf.Sin(x * 1.45f + Mathf.Cos(z * .8f));
                if (rng.NextDouble() > .36f + .48f * grouping) continue;
                var shape = shapes[rng.Next(shapes.Length)];
                float height = shape.plant.kind == CozyVegetationLibrary.PlantKind.Shrub
                    ? .16f + (float)rng.NextDouble() * .30f : .10f + (float)rng.NextDouble() * .23f;
                float size = CozyVegetationLibrary.GroundcoverScale(shape.plant.kind, shape.plant.mesh.bounds, height);
                var rotation = Quaternion.Euler(0, (float)rng.NextDouble() * 360, 0); var origin = new Vector3(x, -.02f, z);
                for (int index = 0; index < shape.points.Count; index++)
                {
                    vertices.Add(origin + rotation * shape.points[index] * size);
                    colors.Add(Linear(shape.palette[index]));
                }
            }
        }
        float StreamU(float v) => Definition.streamCentre + Mathf.Sin(v * .38f) * .32f + Mathf.Sin(v * .83f) * .11f;
        static Vector2 ViewGround(float x, float z) => new Vector2((-x + z) * .7071068f, (-x - z) * .7071068f);
        static Vector3 GroundPoint(float u, float v, float y) => new Vector3((-u - v) * .7071068f, y, (u - v) * .7071068f);
        void BuildStream(Material baseMaterial)
        {
            var vertices = new List<Vector3>(); var colors = new List<Color>();
            for (int i = 0; i < 100; i++)
            {
                float a = -24 + i * .48f, b = a + .48f, width = Definition.streamWidth * .5f;
                for (int band = 0; band < 5; band++)
                {
                    float low = -width + band * width * .4f, high = low + width * .4f;
                    var color = Color.Lerp(CampArtDefinition.Color(0x397D86), CampArtDefinition.Color(0x6EB4AF), .18f + band * .12f + (i % 4) * .04f);
                    Quad(vertices, colors, GroundPoint(StreamU(a) + low, a, -.018f), GroundPoint(StreamU(b) + low, b, -.018f),
                        GroundPoint(StreamU(b) + high, b, -.018f), GroundPoint(StreamU(a) + high, a, -.018f), color);
                }
            }
            var water = new Material(baseMaterial) { name = "Faceted stream water" }; water.SetFloat("_ArtWater", 1); _materials.Add(water);
            AddMesh("ExteriorStream", MakeMesh("Stream ribbon", vertices, colors), water, false);
        }
        void AddMesh(string name, Mesh mesh, Material material, bool casts = true)
        {
            var go = new GameObject(name); go.layer = BoardRenderer.DecorLayer; go.transform.SetParent(_root.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
            renderer.shadowCastingMode = casts ? ShadowCastingMode.On : ShadowCastingMode.Off; renderer.receiveShadows = true;
            TriangleCount += mesh.triangles.Length / 3;
        }
        Mesh MakeMesh(string name, List<Vector3> positions, List<Color> colors)
        {
            var mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(positions); mesh.SetColors(colors); mesh.SetTriangles(Enumerable.Range(0, positions.Count).ToArray(), 0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); _meshes.Add(mesh); return mesh;
        }
        static Color Linear(Color value) => QualitySettings.activeColorSpace == ColorSpace.Linear ? value.linear : value;
        static void Triangle(List<Vector3> vertices, List<Color> colors, Vector3 a, Vector3 b, Vector3 c, Color color)
        { vertices.Add(a); vertices.Add(b); vertices.Add(c); var linear = Linear(color); colors.Add(linear); colors.Add(linear); colors.Add(linear); }
        static void Quad(List<Vector3> vertices, List<Color> colors, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color color)
        { Triangle(vertices, colors, a, b, c, color); Triangle(vertices, colors, a, c, d, color); }
        public void Release()
        {
            _blur?.Dispose(); _blur = null;
            if (_root != null) DestroyOwned(_root); _root = null;
            foreach (var mesh in _meshes) if (mesh != null) DestroyOwned(mesh); _meshes.Clear();
            foreach (var material in _materials) if (material != null) DestroyOwned(material); _materials.Clear();
            if (_volumeProfile != null) DestroyOwned(_volumeProfile); _volumeProfile = null;
        }
        void OnDestroy() => Release();
        static void DestroyOwned(UnityEngine.Object value)
        { if (UnityEngine.Application.isPlaying) Destroy(value); else DestroyImmediate(value); }
    }
}
