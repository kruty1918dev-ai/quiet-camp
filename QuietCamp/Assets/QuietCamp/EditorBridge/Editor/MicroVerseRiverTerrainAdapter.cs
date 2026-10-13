#if UNITY_EDITOR && QUIETCAMP_RIVER_TERRAIN
using System;
using System.Collections.Generic;
using System.Linq;
using JBooth.MicroVerseCore;
using Newtonsoft.Json;
using QuietCamp.Editor.LevelAuthoring;
using QuietCamp.Infrastructure;
using sc.modeling.river.runtime;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Splines;
using SplinePath = JBooth.MicroVerseCore.SplinePath;

namespace QuietCamp.EditorBridge
{
    [InitializeOnLoad] static class MicroVerseRiverTerrainConnection
    {
        static MicroVerseRiverTerrainConnection() => RiverTerrainIntegration.Register(workspace => new MicroVerseRiverTerrainAdapter(workspace));
    }
    // Only this boundary knows vendor components. Export contains their evaluated geometry.
    sealed class MicroVerseRiverTerrainAdapter : IRiverTerrainAuthoringAdapter
    {
        readonly LevelAuthoringWorkspace _workspace;
        readonly GameObject _root, _preview;
        readonly MicroVerse _microverse;
        readonly Terrain _terrain;
        readonly TerrainData _terrainData;
        readonly SplineContainer _spline;
        readonly RiverModeler _river;
        readonly SplinePath _path, _bank;
        readonly HeightStamp _base, _flat;
        readonly List<GameObject> _stamps = new List<GameObject>();
        readonly List<UnityEngine.Object> _owned = new List<UnityEngine.Object>();
        readonly List<Mesh> _previewMeshes = new List<Mesh>();
        readonly Texture2D _hill;
        readonly Material _groundMaterial, _waterMaterial;
        Texture2D _baseTexture;
        LevelAuthoringDocument _document;
        FrozenLandscape _baked;
        string _key;
        bool _waiting, _finished, _disposed, _evaluate;
        int _polls;
        public override string ToString() => $"waiting={_waiting}, evaluate={_evaluate}, finished={_finished}, disposed={_disposed}, polls={_polls}, baked={_baked != null}, enabled={_microverse.enabled}, modifying={_microverse.IsModifyingTerrain}";
        public GameObject Surface { get; }
        public bool IsUpdating => _waiting || _microverse.IsModifyingTerrain || _baked == null;

        public MicroVerseRiverTerrainAdapter(LevelAuthoringWorkspace workspace)
        {
            _workspace = workspace;
            if (UnityEngine.Object.FindObjectsByType<MicroVerse>(FindObjectsInactive.Include, FindObjectsSortMode.None).Any())
                throw new InvalidOperationException("Закрийте іншу сцену MicroVerse перед відкриттям цієї майстерні.");
            _root = Child("Річка й земля — робочі інструменти", workspace.Root.transform); _root.SetActive(false);
            _microverse = _root.AddComponent<MicroVerse>();
            _terrainData = new TerrainData { name = "Authoring terrain — derived from JSON" }; _owned.Add(_terrainData);
            var layer = new TerrainLayer { name = "Authoring ground", diffuseTexture = Texture2D.whiteTexture }; _owned.Add(layer);
            _terrainData.terrainLayers = new[] { layer };
            var terrainObject = Terrain.CreateTerrainGameObject(_terrainData); terrainObject.name = "Земля MicroVerse";
            terrainObject.transform.SetParent(_root.transform, false); _terrain = terrainObject.GetComponent<Terrain>();
            _terrain.drawHeightmap = false; _terrain.drawTreesAndFoliage = false;
            terrainObject.GetComponent<TerrainCollider>().enabled = false;
            _microverse.explicitTerrains = new[] { _terrain };
            _base = Child("Початкова земля", _root.transform).AddComponent<HeightStamp>(); _base.mode = HeightStamp.CombineMode.Override;
            _base.falloff.filterType = FalloffFilter.FilterType.Global;
            _bank = Child("М’який прибережний вал", _root.transform).AddComponent<SplinePath>();
            _bank.modifySplatMap = false; _bank.clearDetails = _bank.clearTrees = _bank.clearObjects = false; _bank.sdfRes = SplinePath.SDFRes.k256;
            var riverRoot = Child("Русло — спільна крива", _root.transform);
            _spline = riverRoot.AddComponent<SplineContainer>();
            _bank.spline = _spline;
            _path = riverRoot.AddComponent<SplinePath>(); _path.spline = _spline;
            _path.modifySplatMap = false; _path.clearDetails = _path.clearTrees = _path.clearObjects = false; _path.sdfRes = SplinePath.SDFRes.k256;
            // River Modeler's Reset resolves its spline before our assignments.
            // Keep both on the same object, including while the workspace is inactive.
            var riverObject = riverRoot;
            riverObject.AddComponent<MeshFilter>(); riverObject.AddComponent<MeshRenderer>();
            _waterMaterial = new Material(Resources.Load<Material>(workspace.Document.art.waterMaterial)) { name = "Authoring Stylized Water 3" }; _owned.Add(_waterMaterial);
            if (_waterMaterial.HasProperty("_VertexColorTransparency")) _waterMaterial.SetFloat("_VertexColorTransparency", 1);
            riverObject.GetComponent<MeshRenderer>().sharedMaterial = _waterMaterial;
            _microverse.enabled = false;
            _root.SetActive(true);
            _river = riverObject.AddComponent<RiverModeler>(); _river.splineContainer = _spline;
            _root.SetActive(false);
            _microverse.enabled = true;
            _river.rebuildTriggers = 0;
            _river.microVerseSettings = new RiverModeler.MicroVerseRiverSettings();
            _flat = Child("Рівне ігрове поле", _root.transform).AddComponent<HeightStamp>();
            _flat.mode = HeightStamp.CombineMode.Override; _flat.stamp = Texture2D.blackTexture;
            _flat.falloff.filterType = FalloffFilter.FilterType.Box;
            _hill = new Texture2D(65, 65, TextureFormat.RFloat, false, true) { name = "Soft landscape stamp", wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color[65 * 65];
            for (int z = 0; z < 65; z++) for (int x = 0; x < 65; x++)
            { float u = x / 32f - 1, v = z / 32f - 1; float h = Mathf.Pow(Mathf.Max(0, 1 - u * u - v * v), 2); pixels[z * 65 + x] = new Color(h, 0, 0); }
            _hill.SetPixels(pixels); _hill.Apply(); _owned.Add(_hill);
            _groundMaterial = new Material(Resources.Load<Shader>("QuietCamp/CampArtLit")) { name = "Authoring ground" }; _owned.Add(_groundMaterial);
            _groundMaterial.SetFloat("_ArtGround", 1);
            _preview = Child("Оточення — точний перегляд експорту", workspace.Root.transform);
            Surface = Child("Поверхня для пензля", _preview.transform); Surface.AddComponent<MeshCollider>();
            MicroVerse.OnFinishedUpdating.AddListener(Finished);
            EditorApplication.update += Poll;
        }
        static GameObject Child(string name, Transform parent)
        { var go = new GameObject(name); go.transform.SetParent(parent, false); return go; }
        public void Apply(LevelAuthoringDocument document)
        {
            document.terrain.Validate();
            var data = document.terrain;
            var curve = new Spline(data.points.Select(p => new float3(p.x, -.02f, p.z)), TangentMode.AutoSmooth);
            for (int i = 0; i <= 256; i++)
            {
                var point = SplineUtility.EvaluatePosition(curve, i / 256f);
                if (Mathf.Abs(point.x) < document.level.width / 2f + data.width / 2 + .2f
                    && Mathf.Abs(point.z) < document.level.height / 2f + data.width / 2 + .2f)
                    throw new InvalidOperationException("Русло перетинає поле. Пересуньте точки річки перед застосуванням.");
            }
            _document = document;
            string key = document.level.width + "/" + document.level.height + "/" + JsonConvert.SerializeObject(new
                { data.width, data.depth, data.bankWidth, data.bankSoftness, data.points, data.stamps });
            if (key == _key) return;
            _key = key; _root.SetActive(false);
            if (_baseTexture == null)
            {
                _terrainData.heightmapResolution = data.resolution; _terrainData.size = new Vector3(data.size, 16, data.size);
                _terrain.transform.localPosition = new Vector3(-data.size / 2, -8, -data.size / 2);
                _baseTexture = new Texture2D(data.resolution, data.resolution, TextureFormat.RFloat, false, true) { wrapMode = TextureWrapMode.Clamp };
                var colors = data.baseHeights.Select(h => new Color((h + 8) / 16, 0, 0)).ToArray(); _baseTexture.SetPixels(colors); _baseTexture.Apply(); _owned.Add(_baseTexture);
                _base.stamp = _baseTexture; _base.transform.localPosition = new Vector3(0, -8, 0); _base.transform.localScale = new Vector3(data.size, 16, data.size);
            }
            foreach (var stamp in _stamps) UnityEngine.Object.DestroyImmediate(stamp); _stamps.Clear();
            foreach (var descriptor in data.stamps)
            {
                var node = Child(descriptor.height >= 0 ? "Пагорб" : "Низина", _root.transform); _stamps.Add(node);
                var stamp = node.AddComponent<HeightStamp>(); stamp.stamp = _hill;
                stamp.mode = descriptor.height >= 0 ? HeightStamp.CombineMode.Add : HeightStamp.CombineMode.Subtract;
                stamp.falloff.filterType = FalloffFilter.FilterType.Box;
                // Add/Subtract stamp values are measured from the terrain's origin.
                node.transform.localPosition = new Vector3(descriptor.x, -8, descriptor.z);
                node.transform.localScale = new Vector3(descriptor.radius * 2, Mathf.Abs(descriptor.height), descriptor.radius * 2);
                node.transform.SetSiblingIndex(_base.transform.GetSiblingIndex() + 1);
            }
            _spline.Spline = curve;
            _river.settings.shape.width = data.width;
            _river.settings.uv.flowVectors = true;
            _river.microVerseSettings.bankWidth = data.bankWidth; _river.microVerseSettings.bankHeight = .08f;
            _river.microVerseSettings.bankFalloff = data.bankSoftness; _river.microVerseSettings.bedDepth = data.depth;
            _river.microVerseSettings.bedSmoothness = Mathf.Min(data.width * .3f, .6f);
            _flat.transform.localPosition = new Vector3(0, -.045f, 0); _flat.transform.localScale = new Vector3(document.level.width + .8f, .01f, document.level.height + .8f);
            _flat.transform.SetAsLastSibling();
            _groundMaterial.SetColor("_ArtAmbient", CampArtDefinition.Color(document.art.ambientColor) * .55f);
            _waiting = true; _finished = false; _evaluate = true; _microverse.enabled = true; _root.SetActive(true);
            _river.Rebuild(); _river.UpdateMicroVerseSpline(); _microverse.Invalidate();
            // This Core version has a smooth trench rather than a cross-section curve.
            // Two ready SplinePath stamps share the same spline: a soft bank, then its bed.
            _bank.width = data.width * .5f + data.bankWidth; _bank.smoothness = data.bankSoftness; _bank.trench = -.08f;
            _path.splineWidths.Clear();
            _path.width = data.width * .32f; _path.smoothness = Mathf.Max(.1f, data.width * .18f); _path.trench = data.depth;
            _path.OnMoved(); _bank.OnMoved(); _microverse.Invalidate();
        }
        void Finished() { if (!_disposed && _waiting && !_microverse.IsModifyingTerrain) _finished = true; }
        void Poll()
        {
            _polls++;
            if (!_disposed && _waiting && _evaluate)
            {
                _evaluate = false;
                // Evaluate once after the debounced gesture. This package supports
                // synchronous evaluation; completion uses its actual job state.
                _microverse.Modify(true, true, false);
                _microverse.LateUpdate();
            }
            // ExecuteAlways LateUpdate completes MicroVerse's asynchronous jobs.
            // EditMode does not schedule it continuously when the scene is idle.
            if (!_disposed && _waiting)
            {
                // Drive the vendor's normal job drain as well as its terrain work.
                // TestRunner may suspend regular MonoBehaviour editor updates.
                _microverse.SendMessage("OnEditorUpdate", SendMessageOptions.RequireReceiver);
                _microverse.LateUpdate();
                EditorApplication.QueuePlayerLoopUpdate();
            }
            if (_disposed || !_waiting || !_finished || _microverse.IsModifyingTerrain) return;
            _microverse.SaveBackToTerrain();
            _baked = BakeTerrain(); BuildPreview(); _waiting = false;
            _microverse.enabled = false;
            _workspace.ReprojectPlants(SampleHeight);
            _document.art.landscape = _baked;
            SceneView.RepaintAll();
        }
        public FrozenLandscape Bake()
        { if (IsUpdating) throw new InvalidOperationException("Земля ще оновлюється."); return _baked; }
        public float SampleHeight(float x, float z) => _baked != null ? _baked.SampleHeight(x, z) : _document.art.GroundHeight(x, z);
        public void SelectRiver() { Selection.activeGameObject = _spline.gameObject; _workspace.Frame(); }
        FrozenLandscape BakeTerrain()
        {
            using var timing = new Unity.Profiling.ProfilerMarker("QuietCamp.Authoring.Landscape.Bake").Auto();
            int n = _terrainData.heightmapResolution; float size = _document.terrain.size;
            var source = _terrainData.GetHeights(0, 0, n, n);
            var data = new FrozenLandscape { resolution = n, originX = -size / 2, originZ = -size / 2, size = size, heights = new float[n * n] };
            for (int z = 0; z < n; z++) for (int x = 0; x < n; x++) data.heights[z * n + x] = source[z, x] * 16 - 8;
            var meshes = new List<LandscapeMesh>(); float step = size / (n - 1);
            for (int start = 0; start < n - 1; start += 32)
            {
                int rows = Math.Min(32, n - 1 - start), count = (rows + 1) * n;
                var mesh = new LandscapeMesh { name = "Земля " + start, vertices = new float[count * 3], normals = new float[count * 3], uv = new float[count * 2], colors = new float[count * 4], triangles = new int[rows * (n - 1) * 6] };
                for (int z = 0; z <= rows; z++) for (int x = 0; x < n; x++)
                {
                    int i = z * n + x, worldZ = z + start; float h = data.heights[worldZ * n + x];
                    mesh.vertices[i * 3] = x * step - size / 2; mesh.vertices[i * 3 + 1] = h; mesh.vertices[i * 3 + 2] = worldZ * step - size / 2;
                    float left = data.heights[worldZ * n + Mathf.Max(0, x - 1)], right = data.heights[worldZ * n + Mathf.Min(n - 1, x + 1)];
                    float down = data.heights[Mathf.Max(0, worldZ - 1) * n + x], up = data.heights[Mathf.Min(n - 1, worldZ + 1) * n + x];
                    var normal = new Vector3(left - right, step * 2, down - up).normalized;
                    mesh.normals[i * 3] = normal.x; mesh.normals[i * 3 + 1] = normal.y; mesh.normals[i * 3 + 2] = normal.z;
                    mesh.uv[i * 2] = x / (float)(n - 1); mesh.uv[i * 2 + 1] = worldZ / (float)(n - 1);
                    var c = Color.Lerp(CampArtDefinition.Color(_document.art.groundLow), CampArtDefinition.Color(_document.art.groundHigh), Mathf.Clamp01((h + .05f) * .5f));
                    if (h < -.06f) c = Color.Lerp(c, CampArtDefinition.Color(_document.art.soilColor), .6f);
                    mesh.colors[i * 4] = c.r; mesh.colors[i * 4 + 1] = c.g; mesh.colors[i * 4 + 2] = c.b; mesh.colors[i * 4 + 3] = 1;
                }
                int index = 0;
                for (int z = 0; z < rows; z++) for (int x = 0; x < n - 1; x++)
                { int a = z * n + x; foreach (int value in new[] { a, a + n, a + 1, a + 1, a + n, a + n + 1 }) mesh.triangles[index++] = value; }
                meshes.Add(mesh);
            }
            var river = _river.meshFilter.sharedMesh;
            if (river == null || river.vertexCount == 0) throw new InvalidOperationException("River Modeler не створив воду.");
            var matrix = _workspace.Root.transform.worldToLocalMatrix * _river.meshFilter.transform.localToWorldMatrix;
            var vertices = river.vertices.Select(matrix.MultiplyPoint3x4).ToArray(); var normals = river.normals.Select(v => matrix.MultiplyVector(v).normalized).ToArray();
            var uv = river.uv; var colors = river.colors;
            var flow = new List<Vector3>(); river.GetUVs(2, flow);
            var tangents = river.tangents.Select(t => { var v = matrix.MultiplyVector(new Vector3(t.x, t.y, t.z)).normalized; return new Vector4(v.x, v.y, v.z, t.w); }).ToArray();
            meshes.Add(new LandscapeMesh { name = "Річка", water = true,
                vertices = vertices.SelectMany(v => new[] { v.x, v.y, v.z }).ToArray(), normals = normals.SelectMany(v => new[] { v.x, v.y, v.z }).ToArray(),
                uv = uv.Length == vertices.Length ? uv.SelectMany(v => new[] { v.x, v.y }).ToArray() : new float[vertices.Length * 2],
                lightmapUv = river.uv2.SelectMany(v => new[] { v.x, v.y }).ToArray(),
                flow = flow.SelectMany(v => new[] { v.x, v.y, v.z }).ToArray(), tangents = tangents.SelectMany(v => new[] { v.x, v.y, v.z, v.w }).ToArray(),
                colors = (colors.Length == vertices.Length ? colors : Enumerable.Repeat(Color.white, vertices.Length).ToArray()).SelectMany(c => new[] { c.r, c.g, c.b, c.a }).ToArray(), triangles = river.triangles });
            data.meshes = meshes.ToArray(); data.Validate(); return data;
        }
        void BuildPreview()
        {
            foreach (var mesh in _previewMeshes) UnityEngine.Object.DestroyImmediate(mesh); _previewMeshes.Clear();
            for (int i = _preview.transform.childCount - 1; i >= 0; i--)
                if (_preview.transform.GetChild(i).gameObject != Surface) UnityEngine.Object.DestroyImmediate(_preview.transform.GetChild(i).gameObject);
            var filters = new List<MeshFilter>();
            foreach (var data in _baked.meshes.Where(m => !m.water))
            {
                var node = Child(data.name, _preview.transform); var mesh = data.CreateMesh(); _previewMeshes.Add(mesh);
                var filter = node.AddComponent<MeshFilter>(); filter.sharedMesh = mesh; filters.Add(filter);
                var renderer = node.AddComponent<MeshRenderer>(); renderer.sharedMaterial = _groundMaterial;
            }
            var paint = new Mesh { name = "Paintable dry ground", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            paint.CombineMeshes(filters.Select(f => new CombineInstance { mesh = f.sharedMesh, transform = Matrix4x4.identity }).ToArray());
            var vertices = paint.vertices; var triangles = paint.triangles; var allowed = new List<int>();
            float halfX = _document.level.width / 2f + .2f, halfZ = _document.level.height / 2f + .2f;
            for (int i = 0; i < triangles.Length; i += 3)
            {
                var a = vertices[triangles[i]]; var b = vertices[triangles[i + 1]]; var c = vertices[triangles[i + 2]];
                if (Mathf.Min(a.y, b.y, c.y) < -.075f) continue;
                if (Mathf.Min(a.x, b.x, c.x) < halfX && Mathf.Max(a.x, b.x, c.x) > -halfX && Mathf.Min(a.z, b.z, c.z) < halfZ && Mathf.Max(a.z, b.z, c.z) > -halfZ) continue;
                allowed.Add(triangles[i]); allowed.Add(triangles[i + 1]); allowed.Add(triangles[i + 2]);
            }
            paint.triangles = allowed.ToArray(); Surface.GetComponent<MeshCollider>().sharedMesh = paint; _previewMeshes.Add(paint);
        }
        public void Dispose()
        {
            if (_disposed) return; _disposed = true;
            MicroVerse.OnFinishedUpdating.RemoveListener(Finished); EditorApplication.update -= Poll;
            if (_root != null) _root.SetActive(false);
            if (_root != null) UnityEngine.Object.DestroyImmediate(_root);
            if (_preview != null) UnityEngine.Object.DestroyImmediate(_preview);
            foreach (var mesh in _previewMeshes) if (mesh != null) UnityEngine.Object.DestroyImmediate(mesh);
            foreach (var value in _owned) if (value != null) UnityEngine.Object.DestroyImmediate(value);
        }
    }
}
#endif
