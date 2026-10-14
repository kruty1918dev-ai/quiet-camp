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
        public IRiverTerrainAuthoringAdapter River { get; private set; }
        public SeasonAtmosphereView Mood {get;private set;}
        string _moodKey;
        readonly Dictionary<string,Mesh> _seasonMeshes=new Dictionary<string,Mesh>();
        public int BoardRebuilds { get; private set; }
        public int FullPlantRebuilds { get; private set; }
        public int PartialPlantRebuilds { get; private set; }
        BoardRenderer _board;
        Transform _boardRoot;
        readonly List<Mesh> _batches = new List<Mesh>();
        readonly Dictionary<Material,Material> _previewMaterials = new Dictionary<Material,Material>();
        readonly Dictionary<string, Mesh> _frozenMeshes = new Dictionary<string, Mesh>();
        readonly Dictionary<string, CampArtDefinition.Model> _frozenModels = new Dictionary<string, CampArtDefinition.Model>();
        readonly HashSet<LevelAuthoringAsset> _modelEdits = new HashSet<LevelAuthoringAsset>();
        GameObject _batchRoot;
        Mesh _paintMesh;
        string _boardKey;
        sealed class PlantBatch { public Mesh mesh; public LevelAuthoringAsset[] plants; }
        readonly Dictionary<LevelAuthoringAsset, PlantBatch> _plantBatches = new Dictionary<LevelAuthoringAsset, PlantBatch>();
        string _terrainKey;
        string _scenePath;
        public int SceneryCount => Scenery.GetComponentsInChildren<LevelAuthoringAsset>().Length;
        public static void CloseOrphanedScenes()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.path.Replace('\\', '/').StartsWith("Assets/QuietCamp/Editor/LevelAuthoring/Workspaces/", StringComparison.Ordinal)) continue;
                var roots = scene.GetRootGameObjects();
                if (roots.Length != 1 || roots[0].name != "QuietCamp — майстерня рівня") continue;
                if (SceneManager.sceneCount == 1) EditorSceneManager.SetActiveScene(EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive));
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        public LevelAuthoringWorkspace(LevelAuthoringDocument document)
        {
            AuthoringVegetationSnapshot.ExpandCover(document);
            Document = document;
            foreach (var model in document.art.models) _frozenModels[model.id] = model;
            LevelAuthoringPalette.EnsureDocumentModels(document.art.models);
            try
            {
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
            var background = document.art.EditingCopy();
            background.instances = Array.Empty<CampArtDefinition.Instance>(); background.cover = Array.Empty<CampArtDefinition.CoverPoint>();
            background.atmosphere=null;
            var view = cameraRoot.AddComponent<CampArtView>(); view.Configure(background, document.level, Root.transform, AssetCatalog.Load(), document.terrain != null);
            Camera.aspect = document.art.cameraAspect;
            view.FitCamera(new Rect(0, 0, 1, 1));
            var light = Child("Сонце майстерні", Root.transform).gameObject.AddComponent<Light>(); light.type = LightType.Directional;
            view.ApplyLighting(light);
            if (document.terrain == null) UpdateSurface();
            else ConnectRiver(document);
            _terrainKey = TerrainKey(document);
            // Only this selected paint surface has a collider. The puzzle and water are not brush targets.
            foreach (var marker in document.art.instances) Spawn(marker, false);
            RefreshBoard(); RefreshPlantBatches();
            ApplyMood(document);
            EditorSceneManager.SetActiveScene(scene);
            Frame();
            }
            catch { Dispose(); throw; }
        }
        static Transform Child(string name, Transform parent)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false); return go.transform;
        }
        public GameObject Spawn(CampArtDefinition.Instance item, bool undo = true)
        {
            var entry = LevelAuthoringPalette.Get(item.modelId ?? item.assetId);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(entry.prefab, Scene); go.transform.SetParent(Scenery, false);
            if (entry.model != null)
            {
                if (!_frozenModels.TryGetValue(entry.id, out var model)) _frozenModels[entry.id] = model = entry.model;
                if (!_frozenMeshes.TryGetValue(entry.id, out var mesh))
                {
                    mesh = new Mesh { name = entry.id + " — frozen draft", indexFormat = IndexFormat.UInt32 };
                    LevelAuthoringPalette.PopulateMesh(mesh, model); _frozenMeshes[entry.id] = mesh;
                }
                go.GetComponent<MeshFilter>().sharedMesh = mesh;
            }
            go.transform.localPosition = new Vector3(item.x, item.y, item.z);
            go.transform.localRotation = item.rotationEuler != null ? Quaternion.Euler(CampArtDefinition.Vector(item.rotationEuler)) : Quaternion.Euler(0, item.yaw, 0);
            var scale = item.scaleXYZ != null ? CampArtDefinition.Vector(item.scaleXYZ) : Vector3.one * item.scale;
            go.transform.localScale = entry.model != null ? scale : Vector3.Scale(go.transform.localScale, scale);
            var marker=go.GetComponent<LevelAuthoringAsset>(); marker.instanceId=item.id??("authored/"+Guid.NewGuid().ToString("N"));
            marker.surfaceAttached = item.surfaceAttached ?? true;
            marker.surfaceOffset = item.surfaceAttached.HasValue ? item.surfaceOffset : item.y - Document.art.GroundHeight(item.x, item.z);
            marker.attachmentInitialized = true; marker.attachedY = item.y;
            marker.sourcePose=item; marker.capturedPosition=go.transform.localPosition;
            marker.capturedRotation=go.transform.localRotation;
            marker.capturedScale=marker.packedModel?go.transform.localScale:scale;
            ApplyPreviewMaterial(marker);
            if (undo) Undo.RegisterCreatedObjectUndo(go, "Додати декор"); return go;
        }
        public void ApplyDocument(LevelAuthoringDocument document)
        {
            AuthoringVegetationSnapshot.ExpandCover(document);
            bool replaceScenery=document.art.instances.Length!=Document.art.instances.Length ||
                !document.art.instances.Select(i=>i.id+"/"+i.modelId).SequenceEqual(Document.art.instances.Select(i=>i.id+"/"+i.modelId));
            foreach(var model in document.art.models)_frozenModels[model.id]=model;
            RiverTerrainIntegration.Validate?.Invoke(document);
            Document = document;
            if(replaceScenery)
            {
                PlacementToolIntegration.Adapter?.Stop();
                for(int i=Scenery.childCount-1;i>=0;i--)UnityEngine.Object.DestroyImmediate(Scenery.GetChild(i).gameObject);
                foreach(var item in document.art.instances)Spawn(item,false);
                RefreshPlantBatches();
            }
            if (_boardKey != Newtonsoft.Json.JsonConvert.SerializeObject(document.level)+Newtonsoft.Json.JsonConvert.SerializeObject(document.art.atmosphere)) RefreshBoard();
            if (_terrainKey != TerrainKey(document))
            {
                PlacementToolIntegration.Adapter?.Stop();
                var old = Camera.GetComponent<CampArtView>(); old.Release(); UnityEngine.Object.DestroyImmediate(old);
                var background = document.art.EditingCopy();
                background.atmosphere=null;
                background.instances = Array.Empty<CampArtDefinition.Instance>(); background.cover = Array.Empty<CampArtDefinition.CoverPoint>();
                var view = Camera.gameObject.AddComponent<CampArtView>(); view.Configure(background, document.level, Root.transform, AssetCatalog.Load(), document.terrain != null);
                if (document.terrain == null) UpdateSurface();
                _terrainKey = TerrainKey(document);
                _moodKey=null;
            }
            if (document.terrain != null) ConnectRiver(document);
            else if (River != null) { River.Dispose(); River = null; UpdateSurface(); }
            var cameraView=Camera.GetComponent<CampArtView>();
            cameraView.Definition.cameraEuler=document.art.cameraEuler;
            cameraView.Definition.cameraTarget=document.art.cameraTarget;
            cameraView.Definition.orthographicSize=document.art.orthographicSize;
            cameraView.FitCamera(new Rect(0,0,1,1));
            cameraView.SetBlur(document.art.blurPixels);
            var sun = Root.GetComponentInChildren<Light>(); sun.transform.rotation = Quaternion.Euler(CampArtDefinition.Vector(document.art.sunEuler)); sun.intensity = document.art.sunIntensity;
            ApplyMood(document);
        }
        void ApplyMood(LevelAuthoringDocument document)
        {
            string key=Newtonsoft.Json.JsonConvert.SerializeObject(document.art.atmosphere);
            if(_moodKey==key)return;
            _moodKey=key;if(Mood!=null)UnityEngine.Object.DestroyImmediate(Mood.gameObject);Mood=null;
            foreach(var mesh in _seasonMeshes.Values)UnityEngine.Object.DestroyImmediate(mesh);_seasonMeshes.Clear();
            RefreshPlantBatches();
            var sun=Root.GetComponentInChildren<Light>();
            foreach(var material in _previewMaterials.Values)material.SetColor("_ArtAmbient",CampArtDefinition.Color(document.art.atmosphere?.ambientColor??document.art.ambientColor)*.55f);
            if(document.art.atmosphere!=null)
            {
                Mood=new GameObject("Сезонна атмосфера").AddComponent<SeasonAtmosphereView>();
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(Mood.gameObject,Scene);
                Mood.Configure(document.art.atmosphere,document.art,Camera,Root.transform);
                var p=document.art.atmosphere;sun.transform.rotation=Quaternion.Euler(CampArtDefinition.Vector(p.sunEuler));sun.color=CampArtDefinition.Color(p.sunColor);sun.intensity=p.sunIntensity;sun.shadowStrength=p.shadows;
            }
            else {sun.color=CampArtDefinition.Color(document.art.sunColor);sun.shadowStrength=.82f;}
            SceneView.RepaintAll();
        }
        public void ReplaceModel(LevelAuthoringAsset marker, AuthoredModelVariant variant)
        {
            if(marker==null || !marker.transform.IsChildOf(Scenery))throw new ArgumentException("Оберіть декор відкритого рівня.");
            if(_modelEdits.Count==0)Undo.undoRedoPerformed+=RestoreEditedMeshes;
            _modelEdits.Add(marker);
            var filter=marker.GetComponent<MeshFilter>();Undo.RecordObjects(new UnityEngine.Object[]{marker,filter},"Застосувати варіант моделі");
            marker.stableId=variant.model.id;marker.packedModel=true;
            _frozenModels[variant.model.id]=variant.model;
            var entry=LevelAuthoringPalette.Get(variant.model.id);filter.sharedMesh=entry.prefab.GetComponent<MeshFilter>().sharedMesh;
            EditorUtility.SetDirty(marker);EditorUtility.SetDirty(filter);ApplyPreviewMaterial(marker);RefreshPlantBatches(new[]{marker.transform});
        }
        void RestoreEditedMeshes()
        {
            // Unity may restore the prefab asset reference for a transient mesh.
            // The serialized stable ID is authoritative; resolve only edited instances.
            var changed=new List<Transform>();
            foreach(var marker in _modelEdits)
                if(marker!=null){ApplyPreviewMaterial(marker);changed.Add(marker.transform);}
            RefreshPlantBatches(changed);
        }
        public CampArtDefinition.Model FrozenModel(string id) => _frozenModels.TryGetValue(id,out var model)?model:LevelAuthoringPalette.Get(id).model;
        public void AdvanceBaseBreeze(float time)
        {if(Mood!=null)return;foreach(var material in _previewMaterials.Values)PlantWeatherResponse.Set(material,time,.17f,0);}
        static string TerrainKey(LevelAuthoringDocument d) => d.terrain != null ? "terrain/" + d.level.width + "/" + d.level.height : string.Join("/", d.level.width, d.level.height, d.art.streamCentre, d.art.streamWidth, d.art.streamSlope, d.art.streamBend, d.art.meadowRelief, d.art.waterDepth);
        void ConnectRiver(LevelAuthoringDocument document)
        {
            if (River == null) River = RiverTerrainIntegration.Factory?.Invoke(this) ?? throw new InvalidOperationException("Інструменти річки ще не підключені.");
            River.Apply(document); Surface = River.Surface;
        }
        void UpdateSurface()
        {
            if (_paintMesh != null) UnityEngine.Object.DestroyImmediate(_paintMesh);
            if (Surface != null && Surface.GetComponent<MeshFilter>() == null) UnityEngine.Object.DestroyImmediate(Surface);
            var names = new HashSet<string>(Document.art.landscape?.meshes.Where(m => !m.water).Select(m => m.name) ?? new[] { "MeadowGround" });
            var sources = Root.GetComponentsInChildren<MeshFilter>().Where(f => names.Contains(f.name)).ToArray();
            if (sources.Length == 0) throw new InvalidOperationException("Поверхня землі не створилася.");
            Surface = Child("Поверхня для пензля — поза полем", Root.transform).gameObject;
            _paintMesh = new Mesh { name = "Paint surface outside puzzle", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            _paintMesh.CombineMeshes(sources.Select(f => new CombineInstance { mesh = f.sharedMesh, transform = Root.transform.worldToLocalMatrix * f.transform.localToWorldMatrix }).ToArray());
            // A legacy meadow is one huge quad; removing board-crossing triangles
            // would remove its entire paint surface. Sample a separate collision grid.
            if (_paintMesh.vertexCount <= 6)
            {
                var bounds = _paintMesh.bounds; const int resolution = 129;
                var grid = new Vector3[resolution * resolution]; var indices = new List<int>();
                for (int z = 0; z < resolution; z++) for (int x = 0; x < resolution; x++)
                {
                    float px = Mathf.Lerp(bounds.min.x, bounds.max.x, x / (float)(resolution - 1)), pz = Mathf.Lerp(bounds.min.z, bounds.max.z, z / (float)(resolution - 1));
                    grid[z * resolution + x] = new Vector3(px, Document.art.GroundHeight(px, pz), pz);
                    if (x < resolution - 1 && z < resolution - 1)
                    { int a = z * resolution + x; indices.AddRange(new[] { a, a + resolution, a + 1, a + 1, a + resolution, a + resolution + 1 }); }
                }
                _paintMesh.Clear(); _paintMesh.vertices = grid; _paintMesh.triangles = indices.ToArray(); _paintMesh.RecalculateNormals();
            }
            var points = _paintMesh.vertices; var triangles = _paintMesh.triangles; var filtered = new List<int>();
            float halfX = Document.level.width * .5f + .2f, halfZ = Document.level.height * .5f + .2f;
            for (int i = 0; i < triangles.Length; i += 3)
            {
                var a = points[triangles[i]]; var b = points[triangles[i+1]]; var c = points[triangles[i+2]];
                if (Mathf.Min(a.y,b.y,c.y) < -.075f) continue; // No painting on the submerged riverbed.
                if (Mathf.Min(a.x,b.x,c.x) < halfX && Mathf.Max(a.x,b.x,c.x) > -halfX && Mathf.Min(a.z,b.z,c.z) < halfZ && Mathf.Max(a.z,b.z,c.z) > -halfZ) continue;
                filtered.Add(triangles[i]); filtered.Add(triangles[i+1]); filtered.Add(triangles[i+2]);
            }
            _paintMesh.triangles = filtered.ToArray();
            Surface.AddComponent<MeshCollider>().sharedMesh = _paintMesh;
        }
        public LevelAuthoringDocument Capture(bool bakeLandscape = true)
        {
            if (bakeLandscape && River != null && River.IsUpdating) throw new InvalidOperationException("Оновлюється річка й земля. Зачекайте завершення перед збереженням або грою.");
            var copy = LevelAuthoringDocument.Copy(Document);
            if (bakeLandscape && River != null) copy.art.landscape = River.Bake();
            var markers = Scenery.GetComponentsInChildren<LevelAuthoringAsset>();
            copy.art.instances = markers.Select((m, i) =>
            {
                var matrix=Root.transform.worldToLocalMatrix*m.transform.localToWorldMatrix;
                Vector3 a=matrix.GetColumn(0),b=matrix.GetColumn(1),c=matrix.GetColumn(2);
                if(matrix.determinant<=0||Mathf.Abs(Vector3.Dot(a.normalized,b.normalized))>.0001f||Mathf.Abs(Vector3.Dot(a.normalized,c.normalized))>.0001f||Mathf.Abs(Vector3.Dot(b.normalized,c.normalized))>.0001f)
                    throw new InvalidOperationException("Розгрупуйте об’єкти зі скошеним або дзеркальним масштабом перед збереженням.");
                Vector3 p=matrix.GetColumn(3);var rotation=matrix.rotation;var scale=matrix.lossyScale;
                if (!m.packedModel) scale = new Vector3(scale.x / m.baseScale.x, scale.y / m.baseScale.y, scale.z / m.baseScale.z);
                var euler = rotation.eulerAngles;
                if(string.IsNullOrEmpty(m.instanceId))m.instanceId="authored/"+Guid.NewGuid().ToString("N");
                var source=m.sourcePose;
                // Keep the saved pose exactly when Unity only round-tripped its quaternion.
                // Moving/scaling/rotating an object replaces this transient cache on capture.
                var q=m.capturedRotation;
                var difference=new Vector4(rotation.x-q.x,rotation.y-q.y,rotation.z-q.z,rotation.w-q.w);
                var negated=new Vector4(rotation.x+q.x,rotation.y+q.y,rotation.z+q.z,rotation.w+q.w);
                bool same=source!=null&&(p-m.capturedPosition).sqrMagnitude<1e-12f&&(scale-m.capturedScale).sqrMagnitude<1e-12f&&Mathf.Min(difference.sqrMagnitude,negated.sqrMagnitude)<1e-12f;
                var item = new CampArtDefinition.Instance { id = m.instanceId, modelId = m.packedModel ? m.stableId : null, assetId = m.packedModel ? null : m.stableId,
                    x = p.x, y = p.y, z = p.z, yaw = euler.y, scale = scale.x, rotationEuler = new[] { euler.x, euler.y, euler.z }, scaleXYZ = new[] { scale.x, scale.y, scale.z } };
                if(same) {item.x=source.x;item.y=source.y;item.z=source.z;item.yaw=source.yaw;item.scale=source.scale;item.rotationEuler=source.rotationEuler;item.scaleXYZ=source.scaleXYZ;}
                m.sourcePose=item;m.capturedPosition=p;m.capturedRotation=rotation;m.capturedScale=scale;
                if (!m.attachmentInitialized)
                {
                    m.surfaceOffset = p.y - Document.art.GroundHeight(p.x, p.z);
                    m.attachedY = p.y; m.attachmentInitialized = true;
                }
                if (Math.Abs(p.y - m.attachedY) > .001f) m.surfaceAttached = false;
                item.surfaceAttached = m.surfaceAttached; item.surfaceOffset = m.surfaceOffset;
                item.motion=source?.motion;item.motionPivot=source?.motionPivot;
                return item;
            }).ToArray();
            copy.art.models = markers.Where(m => m.packedModel).Select(m => m.stableId).Distinct().Select(id =>
                _frozenModels.TryGetValue(id, out var frozen) ? frozen : LevelAuthoringPalette.Get(id).model).ToArray();
            copy.art.cover = Array.Empty<CampArtDefinition.CoverPoint>(); copy.art.levelId = copy.level.id;
            return copy;
        }
        public void RefreshBoard()
        {
            BoardRebuilds++;
            _boardKey = Newtonsoft.Json.JsonConvert.SerializeObject(Document.level)+Newtonsoft.Json.JsonConvert.SerializeObject(Document.art.atmosphere);
            _board?.ClearAll();
            for (int i = _boardRoot.childCount - 1; i >= 0; i--) UnityEngine.Object.DestroyImmediate(_boardRoot.GetChild(i).gameObject);
            Transform Part(string name) => Child(name, _boardRoot);
            _board = new BoardRenderer(Document.level, AssetCatalog.Load(), Part("Base"), Part("Grid"), Part("Obstacle"), Part("Tent"), Part("Overlay"), Document.art);
            _board.SetPlacementGridVisible(true);
            _board.SyncPlacements(Document.level.witness ?? Array.Empty<Placement>(), Document.level, 1, true);
            // Logical picking uses the board plane, not scenery Physics.
            foreach (var collider in _boardRoot.GetComponentsInChildren<Collider>()) UnityEngine.Object.DestroyImmediate(collider);
        }
        public void RefreshPlantBatches()
        {
            using var timing = new Unity.Profiling.ProfilerMarker("QuietCamp.Authoring.Plants.Full").Auto();
            FullPlantRebuilds++;
            _plantBatches.Clear();
            if (_batchRoot != null) UnityEngine.Object.DestroyImmediate(_batchRoot);
            foreach (var mesh in _batches) UnityEngine.Object.DestroyImmediate(mesh); _batches.Clear();
            _batchRoot = Child("Пакети дрібної зелені — preview", Root.transform).gameObject; _batchRoot.hideFlags = HideFlags.DontSave;
            var all=Scenery.GetComponentsInChildren<LevelAuthoringAsset>();
            foreach(var marker in all)ApplyPreviewMaterial(marker);
            var plants = all.Where(m => m.smallPlant).ToArray();
            foreach (var group in plants.GroupBy(p => p.GetComponent<MeshRenderer>().sharedMaterial))
            {
                var parts = group.ToArray();
                for (int start = 0; start < parts.Length; start += 500)
                {
                    var chunk = parts.Skip(start).Take(500).ToArray();
                    var mesh = new Mesh { name = "Authoring foliage batch", indexFormat = IndexFormat.UInt32 };
                    mesh.CombineMeshes(chunk.Select(m => new CombineInstance { mesh = m.GetComponent<MeshFilter>().sharedMesh, transform = Root.transform.worldToLocalMatrix * m.transform.localToWorldMatrix }).ToArray());
                    _batches.Add(mesh);
                    var batch = new PlantBatch { mesh = mesh, plants = chunk };
                    foreach (var plant in chunk) _plantBatches[plant] = batch;
                    var node = Child("Зелень", _batchRoot.transform).gameObject; node.AddComponent<MeshFilter>().sharedMesh = mesh;
                    node.AddComponent<MeshRenderer>().sharedMaterial = group.Key;
                    foreach (var plant in chunk) plant.GetComponent<MeshRenderer>().enabled = false;
                }
            }
        }
        void ApplyPreviewMaterial(LevelAuthoringAsset marker)
        {
            if(!marker.packedModel)return;
            // New PWB strokes must use the same frozen model as existing instances of this ID.
            if(!_frozenMeshes.TryGetValue(marker.stableId,out var mesh))
            {
                if(!_frozenModels.TryGetValue(marker.stableId,out var model))_frozenModels[marker.stableId]=model=LevelAuthoringPalette.Get(marker.stableId).model;
                mesh=new Mesh{name=marker.stableId+" — frozen draft",indexFormat=IndexFormat.UInt32};
                LevelAuthoringPalette.PopulateMesh(mesh,model);_frozenMeshes[marker.stableId]=mesh;
            }
            marker.GetComponent<MeshFilter>().sharedMesh=mesh;
            if(Document.art.atmosphere!=null)
            {
                if(!_seasonMeshes.TryGetValue(marker.stableId,out var seasonal))
                {
                    seasonal=new Mesh{name=marker.stableId+" — season",indexFormat=IndexFormat.UInt32};
                    LevelAuthoringPalette.PopulateMesh(seasonal,SeasonModelAppearance.Resolve(FrozenModel(marker.stableId),Document.art.atmosphere));_seasonMeshes[marker.stableId]=seasonal;
                }
                marker.GetComponent<MeshFilter>().sharedMesh=seasonal;
            }
            var renderer=marker.GetComponent<MeshRenderer>();var source=renderer.sharedMaterial;
            if(_previewMaterials.Values.Contains(source)||Mood?.OwnsMaterial(source)==true){Mood?.BindRenderer(renderer);return;}
            if(!_previewMaterials.TryGetValue(source,out var preview))
            {
                preview=new Material(source){name="Authoring composition lighting"};
                preview.SetColor("_ArtAmbient",CampArtDefinition.Color(Document.art.ambientColor)*.55f);
                _previewMaterials.Add(source,preview);
            }
            renderer.sharedMaterial=preview;
            Mood?.BindRenderer(renderer);
        }
        public Bounds PropBounds(string id) => _frozenMeshes.TryGetValue(id,out var mesh) ? mesh.bounds
            : _frozenModels.TryGetValue(id,out var model) ? AuthoringPropCatalog.ModelBounds(model) : LevelAuthoringPalette.Get(id).prop.Bounds;
        public void RefreshPlantBatches(IEnumerable<Transform> changed)
        {
            using var timing = new Unity.Profiling.ProfilerMarker("QuietCamp.Authoring.Plants.Partial").Auto();
            var batches = new HashSet<PlantBatch>();
            foreach (var transform in changed)
            {
                if (transform == null) continue;
                foreach (var marker in transform.GetComponentsInChildren<LevelAuthoringAsset>())
                    if (_plantBatches.TryGetValue(marker, out var batch)) batches.Add(batch);
            }
            foreach (var batch in batches)
            {
                PartialPlantRebuilds++;
                batch.mesh.Clear();
                batch.mesh.CombineMeshes(batch.plants.Where(p => p != null).Select(m => new CombineInstance
                    { mesh = m.GetComponent<MeshFilter>().sharedMesh, transform = Root.transform.worldToLocalMatrix * m.transform.localToWorldMatrix }).ToArray());
            }
        }
        public void ReprojectPlants(Func<float, float, float> height)
        {
            var changed = new List<Transform>();
            foreach (var marker in Scenery.GetComponentsInChildren<LevelAuthoringAsset>())
            {
                var point = Root.transform.InverseTransformPoint(marker.transform.position);
                if (!marker.attachmentInitialized)
                {
                    marker.surfaceOffset = point.y - Document.art.GroundHeight(point.x, point.z);
                    marker.attachedY = point.y; marker.attachmentInitialized = true;
                }
                if (Math.Abs(point.y - marker.attachedY) > .001f) marker.surfaceAttached = false;
                if (!marker.surfaceAttached) continue;
                point.y = height(point.x, point.z) + marker.surfaceOffset;
                marker.transform.position = Root.transform.TransformPoint(point); marker.attachedY = point.y;
                changed.Add(marker.transform);
            }
            RefreshPlantBatches(changed);
            Mood?.RefreshSurfaces();
        }
        public void Frame()
        {
            var view = SceneView.lastActiveSceneView ?? EditorWindow.GetWindow<SceneView>();
            view.LookAt(CampArtDefinition.Vector(Document.art.cameraTarget), Quaternion.Euler(CampArtDefinition.Vector(Document.art.cameraEuler)), Document.art.orthographicSize * 1.2f, true);
            view.sceneLighting = true; view.Repaint();
        }
        public void Dispose()
        {
            if(Mood!=null)UnityEngine.Object.DestroyImmediate(Mood.gameObject);Mood=null;
            foreach(var mesh in _seasonMeshes.Values)if(mesh!=null)UnityEngine.Object.DestroyImmediate(mesh);_seasonMeshes.Clear();
            Undo.undoRedoPerformed-=RestoreEditedMeshes;_modelEdits.Clear();
            River?.Dispose(); River = null;
            _board?.ClearAll(); _board = null;
            if (_paintMesh != null) UnityEngine.Object.DestroyImmediate(_paintMesh);
            foreach (var mesh in _batches) if (mesh != null) UnityEngine.Object.DestroyImmediate(mesh); _batches.Clear();
            if (Root != null)
            {
                var scene = Root.scene; UnityEngine.Object.DestroyImmediate(Root);
                if (scene.IsValid() && scene.isLoaded)
                {
                    // Unity cannot close its last loaded scene. Leave a harmless
                    // empty scene before removing the derived workspace asset.
                    if (SceneManager.sceneCount == 1) EditorSceneManager.SetActiveScene(EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive));
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
            if (!string.IsNullOrEmpty(_scenePath)) AssetDatabase.DeleteAsset(_scenePath);
            foreach(var material in _previewMaterials.Values)if(material!=null)UnityEngine.Object.DestroyImmediate(material);
            _previewMaterials.Clear();
            foreach(var mesh in _frozenMeshes.Values)if(mesh!=null)UnityEngine.Object.DestroyImmediate(mesh);
            _frozenMeshes.Clear(); _frozenModels.Clear();
        }
    }
}
