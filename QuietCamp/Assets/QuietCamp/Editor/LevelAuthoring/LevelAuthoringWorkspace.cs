using System;
using System.Collections.Generic;
using System.Linq;
using QuietCamp.Domain;
using QuietCamp.Infrastructure;
using QuietCamp.Presentation.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace QuietCamp.Editor.LevelAuthoring
{
    public sealed class LevelAuthoringWorkspace : IDisposable
    {
        public GameObject Root { get; private set; }
        public Transform Scenery { get; private set; }
        public GameObject Surface { get; private set; }
        public Camera Camera { get; private set; }
        public LevelAuthoringDocument Document { get; private set; }
        public Scene Scene => Root.scene;
        BoardRenderer _board;
        Transform _boardRoot;
        readonly List<Mesh> _batches = new List<Mesh>();
        GameObject _batchRoot;
        Mesh _paintMesh;
        string _terrainKey;
        string _scenePath;
        public int SceneryCount => Scenery.GetComponentsInChildren<LevelAuthoringAsset>().Length;

        public LevelAuthoringWorkspace(LevelAuthoringDocument document)
        {
            Document = document;
            // Open a saved empty template, so an existing unsaved Untitled scene is preserved.
            const string cache = "Assets/QuietCamp/Editor/LevelAuthoring/Workspaces";
            System.IO.Directory.CreateDirectory(cache);
            _scenePath = cache + "/" + Guid.NewGuid().ToString("N") + ".unity";
            System.IO.File.Copy("Assets/QuietCamp/Editor/LevelAuthoring/WorkspaceTemplate.unity", _scenePath);
            AssetDatabase.ImportAsset(_scenePath, ImportAssetOptions.ForceSynchronousImport);
            var scene = EditorSceneManager.OpenScene(_scenePath, OpenSceneMode.Additive);
            Root = new GameObject("QuietCamp — майстерня рівня"); SceneManager.MoveGameObjectToScene(Root, scene);
            Scenery = Child("Декор — редаговані об’єкти", Root.transform);
            _boardRoot = Child("Ігрове поле", Root.transform);
            var cameraRoot = Child("Камера майстерні", Root.transform).gameObject;
            Camera = cameraRoot.AddComponent<Camera>(); Camera.enabled = false;
            Camera.clearFlags = CameraClearFlags.SolidColor; Camera.backgroundColor = new Color(.3f, .4f, .25f);
            var background = LevelAuthoringDocument.Copy(document.art);
            background.instances = Array.Empty<CampArtDefinition.Instance>(); background.cover = Array.Empty<CampArtDefinition.CoverPoint>();
            var view = cameraRoot.AddComponent<CampArtView>(); view.Configure(background, document.level, Root.transform, AssetCatalog.Load());
            Camera.aspect = document.art.cameraAspect;
            view.FitCamera(new Rect(0, 0, 1, 1));
            var light = Child("Сонце майстерні", Root.transform).gameObject.AddComponent<Light>(); light.type = LightType.Directional;
            view.ApplyLighting(light);
            UpdateSurface(); _terrainKey = TerrainKey(document);
            // Only this selected paint surface has a collider. The puzzle and water are not brush targets.
            foreach (var marker in document.art.instances) Spawn(marker, false);
            MaterializeCover(document.art.cover);
            document.art.cover = Array.Empty<CampArtDefinition.CoverPoint>();
            RefreshBoard(); RefreshPlantBatches();
            EditorSceneManager.SetActiveScene(scene);
            Frame();
        }
        static Transform Child(string name, Transform parent)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false); return go.transform;
        }
        public GameObject Spawn(CampArtDefinition.Instance item, bool undo = true)
        {
            var entry = LevelAuthoringPalette.Get(item.modelId ?? item.assetId);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(entry.prefab, Scene); go.transform.SetParent(Scenery, false);
            go.transform.localPosition = new Vector3(item.x, item.y, item.z);
            go.transform.localRotation = item.rotationEuler != null ? Quaternion.Euler(CampArtDefinition.Vector(item.rotationEuler)) : Quaternion.Euler(0, item.yaw, 0);
            var scale = item.scaleXYZ != null ? CampArtDefinition.Vector(item.scaleXYZ) : Vector3.one * item.scale;
            go.transform.localScale = entry.model != null ? scale : Vector3.Scale(go.transform.localScale, scale);
            if (undo) Undo.RegisterCreatedObjectUndo(go, "Додати декор"); return go;
        }
        void MaterializeCover(CampArtDefinition.CoverPoint[] cover)
        {
            var plants = LevelAuthoringPalette.Entries.Where(e => e.smallPlant && e.id != "cover_blades").ToArray();
            foreach (var point in cover ?? Array.Empty<CampArtDefinition.CoverPoint>())
            {
                var options = plants.Where(e => point.kind == 0 ? e.category == "Кущі" : point.kind == 1 ? e.category == "Трава" : e.category == "Квіти").ToArray();
                var entry = point.kind == 3 ? LevelAuthoringPalette.Get("cover_blades") : options.Length == 0 ? LevelAuthoringPalette.Get("cover_blades") : options[Math.Min(options.Length - 1, (int)(point.variation * options.Length))];
                var bounds = entry.prefab.GetComponent<MeshFilter>().sharedMesh.bounds;
                float scale = Mathf.Min(point.height / Mathf.Max(.001f, bounds.size.y), point.maximumSpan / Mathf.Max(.001f, Mathf.Max(bounds.size.x, bounds.size.z)));
                var go = Spawn(new CampArtDefinition.Instance { modelId = entry.id, x = point.x, y = point.y, z = point.z, yaw = point.yaw, scale = 1 }, false);
                go.transform.localScale = Vector3.one * scale;
            }
        }
        public void ApplyDocument(LevelAuthoringDocument document)
        {
            Document = document; RefreshBoard();
            if (_terrainKey != TerrainKey(document))
            {
                PlacementToolIntegration.Adapter?.Stop();
                var old = Camera.GetComponent<CampArtView>(); old.Release(); UnityEngine.Object.DestroyImmediate(old);
                var background = LevelAuthoringDocument.Copy(document.art);
                background.instances = Array.Empty<CampArtDefinition.Instance>(); background.cover = Array.Empty<CampArtDefinition.CoverPoint>();
                var view = Camera.gameObject.AddComponent<CampArtView>(); view.Configure(background, document.level, Root.transform, AssetCatalog.Load());
                UpdateSurface(); _terrainKey = TerrainKey(document);
            }
            Camera.transform.rotation = Quaternion.Euler(CampArtDefinition.Vector(document.art.cameraEuler));
            float distance = 18; Camera.transform.position = CampArtDefinition.Vector(document.art.cameraTarget) - Camera.transform.forward * distance;
            Camera.orthographicSize = document.art.orthographicSize;
            var sun = Root.GetComponentInChildren<Light>(); sun.transform.rotation = Quaternion.Euler(CampArtDefinition.Vector(document.art.sunEuler)); sun.intensity = document.art.sunIntensity;
        }
        static string TerrainKey(LevelAuthoringDocument d) => string.Join("/", d.level.width, d.level.height, d.art.streamCentre, d.art.streamWidth, d.art.streamSlope, d.art.streamBend, d.art.meadowRelief, d.art.waterDepth);
        void UpdateSurface()
        {
            if (_paintMesh != null) UnityEngine.Object.DestroyImmediate(_paintMesh);
            Surface = Root.GetComponentsInChildren<MeshFilter>().First(f => f.name == "MeadowGround").gameObject;
            var source = Surface.GetComponent<MeshFilter>().sharedMesh;
            _paintMesh = UnityEngine.Object.Instantiate(source); _paintMesh.name = "Paint surface outside puzzle";
            var points = source.vertices; var triangles = source.triangles; var filtered = new List<int>();
            float halfX = Document.level.width * .5f + .2f, halfZ = Document.level.height * .5f + .2f;
            for (int i = 0; i < triangles.Length; i += 3)
            {
                var a = points[triangles[i]]; var b = points[triangles[i+1]]; var c = points[triangles[i+2]];
                if (Mathf.Min(a.x,b.x,c.x) < halfX && Mathf.Max(a.x,b.x,c.x) > -halfX && Mathf.Min(a.z,b.z,c.z) < halfZ && Mathf.Max(a.z,b.z,c.z) > -halfZ) continue;
                filtered.Add(triangles[i]); filtered.Add(triangles[i+1]); filtered.Add(triangles[i+2]);
            }
            _paintMesh.triangles = filtered.ToArray();
            Surface.AddComponent<MeshCollider>().sharedMesh = _paintMesh;
        }
        public LevelAuthoringDocument Capture()
        {
            var copy = LevelAuthoringDocument.Copy(Document);
            var markers = Scenery.GetComponentsInChildren<LevelAuthoringAsset>();
            copy.art.instances = markers.Select((m, i) =>
            {
                var p = Root.transform.InverseTransformPoint(m.transform.position);
                var rotation = Quaternion.Inverse(Root.transform.rotation) * m.transform.rotation;
                var scale = m.transform.localScale;
                // PWB subparents are disabled by the adapter; support user-created groups too.
                for (var parent = m.transform.parent; parent != Scenery && parent != null; parent = parent.parent) scale = Vector3.Scale(scale, parent.localScale);
                if (!m.packedModel) scale = new Vector3(scale.x / m.baseScale.x, scale.y / m.baseScale.y, scale.z / m.baseScale.z);
                var euler = rotation.eulerAngles;
                return new CampArtDefinition.Instance { id = "authored/" + i, modelId = m.packedModel ? m.stableId : null, assetId = m.packedModel ? null : m.stableId,
                    x = p.x, y = p.y, z = p.z, yaw = euler.y, scale = scale.x, rotationEuler = new[] { euler.x, euler.y, euler.z }, scaleXYZ = new[] { scale.x, scale.y, scale.z } };
            }).ToArray();
            copy.art.models = markers.Where(m => m.packedModel).Select(m => m.stableId).Distinct().Select(id => LevelAuthoringPalette.Get(id).model).ToArray();
            copy.art.cover = Array.Empty<CampArtDefinition.CoverPoint>(); copy.art.levelId = copy.level.id;
            return copy;
        }
        public void RefreshBoard()
        {
            _board?.ClearAll();
            for (int i = _boardRoot.childCount - 1; i >= 0; i--) UnityEngine.Object.DestroyImmediate(_boardRoot.GetChild(i).gameObject);
            Transform Part(string name) => Child(name, _boardRoot);
            _board = new BoardRenderer(Document.level, AssetCatalog.Load(), Part("Base"), Part("Grid"), Part("Obstacle"), Part("Tent"), Part("Overlay"), Document.art);
            _board.SyncPlacements(Document.level.witness ?? Array.Empty<Placement>(), Document.level, 1, true);
            // Logical picking uses the board plane, not scenery Physics.
            foreach (var collider in _boardRoot.GetComponentsInChildren<Collider>()) UnityEngine.Object.DestroyImmediate(collider);
        }
        public void RefreshPlantBatches()
        {
            if (_batchRoot != null) UnityEngine.Object.DestroyImmediate(_batchRoot);
            foreach (var mesh in _batches) UnityEngine.Object.DestroyImmediate(mesh); _batches.Clear();
            _batchRoot = Child("Пакети дрібної зелені — preview", Root.transform).gameObject; _batchRoot.hideFlags = HideFlags.DontSave;
            var plants = Scenery.GetComponentsInChildren<LevelAuthoringAsset>().Where(m => m.smallPlant).ToArray();
            foreach (var group in plants.GroupBy(p => p.GetComponent<MeshRenderer>().sharedMaterial))
            {
                var parts = group.ToArray();
                for (int start = 0; start < parts.Length; start += 500)
                {
                    var chunk = parts.Skip(start).Take(500).ToArray();
                    var mesh = new Mesh { name = "Authoring foliage batch", indexFormat = IndexFormat.UInt32 };
                    mesh.CombineMeshes(chunk.Select(m => new CombineInstance { mesh = m.GetComponent<MeshFilter>().sharedMesh, transform = Root.transform.worldToLocalMatrix * m.transform.localToWorldMatrix }).ToArray());
                    _batches.Add(mesh);
                    var node = Child("Зелень", _batchRoot.transform).gameObject; node.AddComponent<MeshFilter>().sharedMesh = mesh;
                    node.AddComponent<MeshRenderer>().sharedMaterial = group.Key;
                    foreach (var plant in chunk) plant.GetComponent<MeshRenderer>().enabled = false;
                }
            }
        }
        public void Frame()
        {
            var view = SceneView.lastActiveSceneView ?? EditorWindow.GetWindow<SceneView>();
            view.LookAt(CampArtDefinition.Vector(Document.art.cameraTarget), Quaternion.Euler(CampArtDefinition.Vector(Document.art.cameraEuler)), Document.art.orthographicSize * 1.2f, true);
            view.sceneLighting = true; view.Repaint();
        }
        public void Dispose()
        {
            _board?.ClearAll(); _board = null;
            if (_paintMesh != null) UnityEngine.Object.DestroyImmediate(_paintMesh);
            foreach (var mesh in _batches) if (mesh != null) UnityEngine.Object.DestroyImmediate(mesh); _batches.Clear();
            if (Root != null) { var scene = Root.scene; UnityEngine.Object.DestroyImmediate(Root); if (scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true); }
            if (!string.IsNullOrEmpty(_scenePath)) AssetDatabase.DeleteAsset(_scenePath);
        }
    }
}
