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
            material.SetColor("_ArtAmbient", CampArtDefinition.Color(definition.ambientColor) * .48f);
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
            // Keep the entire composition inside the existing 32 m shadow distance.
            _camera.transform.position = target + offset - _camera.transform.forward * 18;
            _camera.nearClipPlane = .1f; _camera.farClipPlane = 90;
            _blur?.ProtectBoard(_level);
        }
        public void ApplyLighting(Light sun, float weatherMultiplier = 1)
        {
            if (sun == null) return;
            sun.transform.rotation = Quaternion.Euler(CampArtDefinition.Vector(Definition.sunEuler));
            sun.color = CampArtDefinition.Color(Definition.sunColor);
            sun.intensity = Definition.sunIntensity * Mathf.Max(.75f, weatherMultiplier);
            sun.shadows = LightShadows.Soft; sun.shadowStrength = .82f; sun.shadowBias = .018f; sun.shadowNormalBias = .06f;
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
            Vector3 At(float x, float z) => new Vector3(x, GroundHeight(x, z), z);
            float step = Mathf.Max(.275f, Definition.surroundExtent / 64);
            for (int x = -64; x < 64; x++) for (int z = -64; z < 64; z++)
            {
                float s = step; float wx = x * s, wz = z * s;
                var uv = ViewGround(wx, wz);
                float bank = Mathf.Abs(uv.x - StreamU(uv.y));
                var color = Color.Lerp(CampArtDefinition.Color(Definition.groundLow), CampArtDefinition.Color(Definition.groundHigh), .4f);
                if (bank < Definition.streamWidth * .5f + .22f) color = Color.Lerp(color, CampArtDefinition.Color(Definition.soilColor), .6f);
                if (uv.x > 3.4f && uv.y < -3.6f && Mathf.Abs(uv.x - (4.1f - .12f * uv.y)) < .34f)
                    color = Color.Lerp(color, CampArtDefinition.Color(Definition.soilColor), .75f);
                Quad(positions, colors, At(wx, wz), At(wx, wz + s), At(wx + s, wz + s), At(wx + s, wz), color);
            }
            var groundMaterial = new Material(material); groundMaterial.SetFloat("_ArtGround", 1); _materials.Add(groundMaterial);
            AddMesh("MeadowGround", MakeMesh("Meadow triangles", positions, colors), groundMaterial, false);
        }
        float GroundHeight(float x, float z)
        {
            var uv = ViewGround(x, z); float bank = Mathf.Abs(uv.x - StreamU(uv.y));
            float channel = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(Definition.streamWidth * .30f, Definition.streamWidth * .59f, bank));
            float margin = Mathf.Max(Mathf.Abs(x) - _level.width * .5f, Mathf.Abs(z) - _level.height * .5f);
            float relief = Mathf.SmoothStep(0, 1, margin * .5f) * Definition.meadowRelief * (.5f + .5f * Mathf.Sin(x * .55f) * Mathf.Cos(z * .68f));
            return -.045f + relief - channel * Definition.waterDepth;
        }
        void BuildGroundcover(Material material)
        {
            var library = CozyVegetationLibrary.Load();
            if (library == null) throw new InvalidOperationException("Project vegetation library unavailable.");
            var shapes = library.plants.Where(p => p.mesh != null && p.mesh.isReadable && p.colors != null && p.colors.Length > 0)
                .Select(p =>
                {
                    var source = p.mesh.vertices; var points = new List<Vector3>(); var palette = new List<Color>();
                    for (int slot = 0; slot < p.mesh.subMeshCount; slot++)
                    foreach (int index in p.mesh.GetTriangles(slot))
                    { points.Add(source[index]); palette.Add(p.colors[Mathf.Min(slot, p.colors.Length - 1)]); }
                    return new { plant = p, points, palette };
                }).ToArray();
            var vertices = new List<Vector3>(); var colors = new List<Color>();
            foreach (var item in Definition.cover)
            {
                var root = new Vector3(item.x, item.y, item.z);
                var rotation = Quaternion.Euler(0, item.yaw, 0);
                if (item.kind == 3)
                {
                    var tint = Color.Lerp(CampArtDefinition.Color(0x647F3C), CampArtDefinition.Color(0xA7B66B), item.variation);
                    for (int blade = 0; blade < 3; blade++)
                    {
                        var side = rotation * (Quaternion.Euler(0, blade * 60, 0) * Vector3.right) * item.height * .12f;
                        Triangle(vertices, colors, root - side, root + Vector3.up * item.height + side * .25f, root + side, tint);
                    }
                    continue;
                }
                var compatible = shapes.Where(p => (int)p.plant.kind == item.kind).ToArray();
                if (compatible.Length == 0) throw new InvalidOperationException("Groundcover kind has no source mesh: " + item.kind);
                var shape = compatible[Mathf.Min(compatible.Length - 1, (int)(item.variation * compatible.Length))];
                float span = Mathf.Max(shape.plant.mesh.bounds.size.x, shape.plant.mesh.bounds.size.z);
                float size = Mathf.Min(item.height / Mathf.Max(.001f, shape.plant.mesh.bounds.size.y), item.maximumSpan / Mathf.Max(.001f, span));
                for (int j = 0; j < shape.points.Count; j++)
                {
                    vertices.Add(root + rotation * shape.points[j] * size);
                    var tint = shape.palette[j];
                    if (item.kind != 2) tint = Color.Lerp(tint, CampArtDefinition.Color(0x779B45), .55f);
                    colors.Add(Linear(tint));
                }
            }
            var foliage = new Material(material); foliage.SetFloat("_ArtGrass", 1); _materials.Add(foliage);
            AddMesh("GeneratedGroundcover", MakeMesh("Ecological groundcover", vertices, colors), foliage);
        }
        float StreamU(float v) => Definition.streamCentre + Definition.streamSlope * v + Mathf.Sin(v * .38f) * Definition.streamBend + Mathf.Sin(v * .83f) * Definition.streamBend * .34f;
        Vector2 ViewGround(float x, float z)
        {
            float yaw = Definition.frameYaw * Mathf.Deg2Rad;
            return new Vector2(x * Mathf.Cos(yaw) - z * Mathf.Sin(yaw), x * Mathf.Sin(yaw) + z * Mathf.Cos(yaw));
        }
        Vector3 GroundPoint(float u, float v, float y)
        {
            float yaw = Definition.frameYaw * Mathf.Deg2Rad;
            return new Vector3(u * Mathf.Cos(yaw) + v * Mathf.Sin(yaw), y, -u * Mathf.Sin(yaw) + v * Mathf.Cos(yaw));
        }
        void BuildStream(Material baseMaterial)
        {
            var vertices = new List<Vector3>(); var colors = new List<Color>();
            for (int i = 0; i < 100; i++)
            {
                float length = Definition.surroundExtent * 1.5f;
                float a = -length + i * (length * .02f), b = a + length * .02f, width = Definition.streamWidth * .5f + .22f;
                for (int band = 0; band < 5; band++)
                {
                    float low = -width + band * width * .4f, high = low + width * .4f;
                    Quad(vertices, colors, GroundPoint(StreamU(a) + low, a, -.018f), GroundPoint(StreamU(b) + low, b, -.018f),
                        GroundPoint(StreamU(b) + high, b, -.018f), GroundPoint(StreamU(a) + high, a, -.018f), Color.clear);
                }
            }
            var source = Resources.Load<Material>(Definition.waterMaterial);
            if (source == null || !source.shader.isSupported) throw new InvalidOperationException("Stylized Water 3 camp material unavailable.");
            var water = new Material(source) { name = "QC007 Stylized Water 3 stream" }; _materials.Add(water);
            var mesh = MakeMesh("Stream ribbon", vertices, colors);
            mesh.SetUVs(0, vertices.Select(p => { var uv = ViewGround(p.x, p.z); return new Vector2((uv.x - StreamU(uv.y)) * .4f, uv.y * .4f); }).ToList());
            mesh.RecalculateTangents();
            AddMesh("ExteriorStream", mesh, water, false);
            var cameraData = _camera.GetUniversalAdditionalCameraData();
            cameraData.requiresDepthTexture = true; cameraData.requiresColorTexture = true;
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
