using System.Collections.Generic;
using QuietCamp.Domain;
using QuietCamp.Infrastructure;
using UnityEngine;
using UnityEngine.Rendering;

namespace QuietCamp.Presentation.World
{
    /// <summary>Camera-visible, seeded forest clusters. A tile is a single coloured wind mesh,
    /// never a GameObject/update/collider per tree. Content describes the scene; quality describes sampling.</summary>
    public sealed class EnvironmentComposer : MonoBehaviour
    {
        const float TileSize=10;
        const int MaximumTiles=48;
        sealed class Shape
        {
            public Vector3[] vertices,normals;public int[] indices;public Color[] colors;public float height;
            public Shape(SeasonalTreeGeometry.Geometry geometry)
            {vertices=geometry.Vertices;normals=geometry.Normals;indices=geometry.Indices;colors=geometry.Colors;height=geometry.Height;}
            public Shape(GameObject prefab)
            {
                var verts=new List<Vector3>();var normalsList=new List<Vector3>();var tris=new List<int>();var colorsList=new List<Color>();
                foreach(var filter in prefab.GetComponentsInChildren<MeshFilter>(true))
                {
                    var mesh=filter.sharedMesh;var renderer=filter.GetComponent<Renderer>();
                    if(mesh==null||!mesh.isReadable||renderer==null)continue;
                    var matrix=prefab.transform.worldToLocalMatrix*filter.transform.localToWorldMatrix;
                    var source=mesh.vertices;var ns=mesh.normals;var mats=renderer.sharedMaterials;
                    for(int slot=0;slot<mesh.subMeshCount;slot++)
                    {
                        var remap=new Dictionary<int,int>();var material=slot<mats.Length?mats[slot]:null;
                        var color=material!=null&&material.HasProperty("_BaseColor")?material.GetColor("_BaseColor"):Color.white;
                        foreach(var index in mesh.GetTriangles(slot))
                        {
                            if(!remap.TryGetValue(index,out int target))
                            { target=verts.Count;remap.Add(index,target);verts.Add(matrix.MultiplyPoint3x4(source[index]));normalsList.Add(matrix.MultiplyVector(ns[index]).normalized);colorsList.Add(color); }
                            tris.Add(target);
                        }
                    }
                }
                vertices=verts.ToArray();normals=normalsList.ToArray();indices=tris.ToArray();colors=colorsList.ToArray();
                float min=0,max=.01f;foreach(var v in vertices){min=Mathf.Min(min,v.y);max=Mathf.Max(max,v.y);}height=max-min;
            }
        }
        sealed class Tile { public GameObject root;public Mesh mesh;public List<Vector3> roots=new List<Vector3>();public List<Vector3> crowns=new List<Vector3>(); }
        readonly Dictionary<Vector2Int,Tile> _tiles=new Dictionary<Vector2Int,Tile>();
        readonly Stack<Tile> _pool=new Stack<Tile>();readonly HashSet<Vector2Int> _wanted=new HashSet<Vector2Int>();
        readonly List<Vector2Int> _remove=new List<Vector2Int>();readonly List<Material> _seasonMaterials=new List<Material>();
        static readonly System.Runtime.CompilerServices.ConditionalWeakTable<GameObject,Shape> SharedShapes = new System.Runtime.CompilerServices.ConditionalWeakTable<GameObject,Shape>();
        static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Shape,Shape> SharedBareShapes = new System.Runtime.CompilerServices.ConditionalWeakTable<Shape,Shape>();
        readonly Queue<Vector2Int> _pending = new Queue<Vector2Int>();
        readonly List<Vector3> _vertices = new List<Vector3>(20000), _normals = new List<Vector3>(20000);
        readonly List<Color> _colors = new List<Color>(20000);
        readonly List<Vector4> _roots = new List<Vector4>(20000);
        readonly List<int> _indices = new List<int>(30000);
        bool _deferred;
        Transform _shelterDecor;
        public bool InitialReady => _pending.Count == 0 && Shelter != null;
        readonly Vector3[] _corners=new Vector3[8];
        Camera _camera;LevelData _level;Material _forestMaterial;Shape _broadleaf,_bare,_pine;
        Rect _lastFootprint;Vector2Int _lastScreen;float _refreshAt,_tileSize=TileSize;bool _ready;
        public LevelData Level=>_level;
        public EnvironmentCompositionData Descriptor {get;private set;}
        public Transform ShoreRoot {get;private set;}
        public WindShelterField Shelter {get;private set;}
        public int VisibleClusterCount=>_tiles.Count;
        public int TreeCount {get;private set;}
        public int CanopyRevision {get;private set;}
        public IEnumerable<Vector3> LeafSources
        {get{foreach(var tile in _tiles.Values)foreach(var crown in tile.crowns)yield return transform.TransformPoint(crown);}}
        public void Configure(LevelData level,Camera camera,Transform decorRoot,AssetCatalog assets,bool deferred=false)
        {
            using var audit = PerformanceAudit.Measure("QC.EnvironmentComposer.Configure");
            _level=level;_camera=camera;_deferred=deferred;_shelterDecor=decorRoot;Descriptor=EnvironmentCompositionData.For(level);
            if(assets!=null)
            {
                if(assets.TryGet("tree_default",out var broad)&&broad.prefab!=null)_broadleaf=SharedShapes.GetValue(broad.prefab, prefab => new Shape(prefab));
                if(assets.TryGet("tree_pineRoundA",out var pine)&&pine.prefab!=null)_pine=SharedShapes.GetValue(pine.prefab, prefab => new Shape(prefab));
                if(_broadleaf!=null&&_broadleaf.vertices.Length>0)_bare=SharedBareShapes.GetValue(_broadleaf, shape => new Shape(SeasonalTreeGeometry.Bare(shape.vertices,shape.normals,shape.indices,shape.colors)));
            }
            var shader=Resources.Load<Shader>("QuietCamp/FoliageLit");
            if(shader!=null)
            {
                _forestMaterial=new Material(shader){name="Seasonal forest clusters"};
                _forestMaterial.SetColor("_BaseColor",Color.white);
                _forestMaterial.SetFloat("_SnowCover",SeasonPalette.For(_level).SnowCoverage);_forestMaterial.SetFloat("_VertexTint",1);
                _forestMaterial.SetFloat("_ClusterWind",1);_forestMaterial.SetFloat("_TreeWind",1);_forestMaterial.SetFloat("_CrownStart",.3f);
                _forestMaterial.SetFloat("_SwayAmp",.13f);_forestMaterial.SetFloat("_SwayFreq",.18f);
                _forestMaterial.SetFloat("_FlutterAmp",.003f);_forestMaterial.SetFloat("_FlutterFreq",.8f);
            }
            TintExisting(decorRoot);
            ShoreRoot=new GameObject("ShoreEnvironment").transform;ShoreRoot.SetParent(transform,false);
            Refresh();BuildStory();BuildEarthContacts(decorRoot);
            if (_pending.Count == 0) BuildShelter(decorRoot);
            CampWaterVisuals.Attach(level,ShoreRoot,camera);
            _ready=true;
        }
        void LateUpdate()
        {
            if (!_ready || _camera == null) return;
            if (_pending.Count > 0)
            {
                long start = System.Diagnostics.Stopwatch.GetTimestamp();
                do { AddTile(_pending.Dequeue()); }
                while (_pending.Count > 0 && (System.Diagnostics.Stopwatch.GetTimestamp()-start)*1000.0/System.Diagnostics.Stopwatch.Frequency < 4);
                if (_pending.Count == 0)
                { _deferred = false; FinishTiles(); BuildShelter(_shelterDecor); }
                return;
            }
            if(Time.unscaledTime<_refreshAt)return;
            _refreshAt=Time.unscaledTime+.4f;
            if(!ForestGroundView.TryBounds(_camera,transform,_corners,out var footprint))return;
            // Subtle wind camera motion stays within the prewarmed coverage.
            var size=new Vector2Int(Screen.width,Screen.height);
            if(size!=_lastScreen||Mathf.Abs(footprint.center.x-_lastFootprint.center.x)>2||Mathf.Abs(footprint.center.y-_lastFootprint.center.y)>2||Vector2.Distance(footprint.size,_lastFootprint.size)>3)Refresh();
        }
        void Refresh()
        {
            using var audit = PerformanceAudit.Measure("QC.EnvironmentComposer.Refresh");
            if(_camera==null||_forestMaterial==null||!ForestGroundView.TryBounds(_camera,transform,_corners,out var footprint))return;
            _lastFootprint=footprint;_lastScreen=new Vector2Int(Screen.width,Screen.height);
            footprint.xMin-=5;footprint.xMax+=5;footprint.yMin-=5;footprint.yMax+=5;
            // World depth is obscured by fog before this finite scenery budget ends.
            footprint=Rect.MinMaxRect(Mathf.Max(-40,footprint.xMin),Mathf.Max(-40,footprint.yMin),Mathf.Min(40,footprint.xMax),Mathf.Min(40,footprint.yMax));
            _wanted.Clear();
            float size=TileSize;
            while((Mathf.FloorToInt(footprint.xMax/size)-Mathf.FloorToInt(footprint.xMin/size)+1)*(Mathf.FloorToInt(footprint.yMax/size)-Mathf.FloorToInt(footprint.yMin/size)+1)>MaximumTiles)size*=2;
            if(_tileSize!=size){foreach(var tile in _tiles.Values){tile.root.SetActive(false);_pool.Push(tile);}_tiles.Clear();_tileSize=size;}
            for(int z=Mathf.FloorToInt(footprint.yMin/size);z<=Mathf.FloorToInt(footprint.yMax/size);z++)
            for(int x=Mathf.FloorToInt(footprint.xMin/size);x<=Mathf.FloorToInt(footprint.xMax/size);x++)
                _wanted.Add(new Vector2Int(x,z));
            _remove.Clear();foreach(var pair in _tiles)if(!_wanted.Contains(pair.Key))_remove.Add(pair.Key);
            foreach(var key in _remove){var tile=_tiles[key];tile.root.SetActive(false);_pool.Push(tile);_tiles.Remove(key);}
            _pending.Clear();
            foreach(var key in _wanted)
            {
                if(_tiles.ContainsKey(key))continue;
                if (_deferred) _pending.Enqueue(key); else AddTile(key);
            }
            if (_pending.Count == 0) FinishTiles();
        }
        void AddTile(Vector2Int key)
        {
            var tile=_pool.Count>0?_pool.Pop():CreateTile();BuildTile(tile,key);tile.root.SetActive(true);_tiles.Add(key,tile);
        }
        void FinishTiles()
        {
            TreeCount=0;foreach(var tile in _tiles.Values)TreeCount+=tile.roots.Count;
            CanopyRevision++;
        }
        Tile CreateTile()
        {
            var root=new GameObject("Forest cluster",typeof(MeshFilter),typeof(MeshRenderer));root.transform.SetParent(transform,false);root.layer=BoardRenderer.DecorLayer;
            var mesh=new Mesh{name="Clustered seasonal trees"};root.GetComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=root.GetComponent<MeshRenderer>();renderer.sharedMaterial=_forestMaterial;renderer.shadowCastingMode=ShadowCastingMode.On;renderer.receiveShadows=true;
            return new Tile{root=root,mesh=mesh};
        }
        void BuildTile(Tile tile,Vector2Int key)
        {
            using var audit = PerformanceAudit.Measure("QC.EnvironmentComposer.BuildTile");
            var origin=new Vector3(key.x*_tileSize,0,key.y*_tileSize);tile.root.transform.localPosition=origin;tile.root.name="Forest cluster "+key;
            var rng=new System.Random(unchecked(Descriptor.clusterSeed*1297+key.x*73856093^key.y*19349663));
            var vertices=_vertices;var normals=_normals;var colors=_colors;var roots=_roots;var indices=_indices;
            vertices.Clear();normals.Clear();colors.Clear();roots.Clear();indices.Clear();tile.roots.Clear();tile.crowns.Clear();
            float Next()=>(float)rng.NextDouble();var season=SeasonProfile.For(_level);var palette=season.Palette;
            int count=Mathf.RoundToInt(Mathf.Lerp(2,12,Mathf.Clamp01(Descriptor.treeDensity))*(_tileSize/TileSize));
            var cluster=new Vector3((.18f+Next()*.64f)*_tileSize,0,(.18f+Next()*.64f)*_tileSize);
            for(int i=0;i<count;i++)
            {
                float angle=Next()*Mathf.PI*2,radius=Mathf.Sqrt(Next())*_tileSize*.37f;
                var root=cluster+new Vector3(Mathf.Cos(angle)*radius,0,Mathf.Sin(angle)*radius);var world=origin+root;
                float height=2.5f+Next()*2.5f;
                if(!CanTree(_level,world,height))continue;
                float species=Next();bool pine=Descriptor.biomeId=="pines"||species<.25f;
                bool bare=season.BareTree(world,_level.decorSeed,pine);
                var shape=pine?_pine:bare?_bare:_broadleaf;if(shape==null||shape.vertices.Length==0)continue;
                float scale=height/shape.height;var rotation=Quaternion.Euler(0,Next()*360,0);int start=vertices.Count;var plant=new Vector4(root.x,root.z,root.y,height);float variation=Next();
                for(int v=0;v<shape.vertices.Length;v++)
                {
                    vertices.Add(root+rotation*shape.vertices[v]*scale);normals.Add(rotation*shape.normals[v]);
                    float tint=Mathf.Clamp01(variation*.65f+(shape.vertices[v].y/shape.height)*.25f+shape.normals[v].x*.10f);
                    colors.Add(palette.Plant(shape.colors[v],tint,pine));roots.Add(plant);
                }
                foreach(int index in shape.indices)indices.Add(start+index);tile.roots.Add(world);
                if(!pine&&!bare)tile.crowns.Add(world+Vector3.up*height*.68f);
            }
            tile.mesh.Clear();tile.mesh.indexFormat=vertices.Count>65535?IndexFormat.UInt32:IndexFormat.UInt16;
            tile.mesh.SetVertices(vertices);tile.mesh.SetNormals(normals);tile.mesh.SetColors(colors);tile.mesh.SetUVs(1,roots);tile.mesh.SetTriangles(indices,0);tile.mesh.RecalculateBounds();var bounds=tile.mesh.bounds;bounds.Expand(2);tile.mesh.bounds=bounds;
        }
        public static bool CanTree(LevelData level,Vector3 root,float height)
        {
            if(Mathf.Abs(root.x)<level.width*.5f+3||Mathf.Abs(root.z)<level.height*.5f+3)
                if(Mathf.Abs(root.x)<level.width*.5f+3&&Mathf.Abs(root.z)<level.height*.5f+3)return false;
            if(CampTrail.IsCorridor(level,root,1.5f))return false;
            foreach(var cell in level.exteriorWalkable??System.Array.Empty<int[]>())
            {if(cell?.Length!=2)continue;var at=BoardMath.CellCenterWorld(level,new Cell(cell[0],cell[1]));if((new Vector2(at.x-root.x,at.z-root.z)).sqrMagnitude<4)return false;}
            var shore=EnvironmentCompositionData.For(level).shore;
            if(ShorelineGeometry.Contains(shore,root,.8f))return false;
            // A scenic crown cannot add an un-authored shade-rule zone on the puzzle.
            var sun=RenderSettings.sun;var direction=sun!=null?-sun.transform.forward:new Vector3(-.4f,.8f,-.3f).normalized;
            var projection=root-new Vector3(direction.x,0,direction.z)*(height/Mathf.Max(.15f,direction.y));
            // Low evening light can project beyond the opposite side of the
            // board. Test the whole swept crown, including its lateral radius,
            // rather than accepting a shadow whose endpoint happens to miss it.
            float radius=Mathf.Max(1.2f,height*.35f),enter=0,exit=1;
            return !(ClipShadowAxis(root.x,projection.x-root.x,-level.width*.5f-radius,level.width*.5f+radius,ref enter,ref exit)
                &&ClipShadowAxis(root.z,projection.z-root.z,-level.height*.5f-radius,level.height*.5f+radius,ref enter,ref exit));
        }
        static bool ClipShadowAxis(float origin,float delta,float min,float max,ref float enter,ref float exit)
        {
            if(Mathf.Abs(delta)<.0001f)return origin>=min&&origin<=max;
            float near=(min-origin)/delta,far=(max-origin)/delta;
            if(near>far){float swap=near;near=far;far=swap;}
            enter=Mathf.Max(enter,near);exit=Mathf.Min(exit,far);return enter<=exit;
        }
        void TintExisting(Transform decor)
        {
            if(decor==null)return;var palette=SeasonPalette.For(_level);var copies=new Dictionary<Material,Material>();
            foreach(var renderer in decor.GetComponentsInChildren<Renderer>(true))
            {
                if(renderer.GetComponentInParent<SeasonalTreeVisual>()!=null||renderer.GetComponentInParent<SeasonalPropVisual>()!=null)continue;
                var materials=renderer.sharedMaterials;bool changed=false;
                for(int i=0;i<materials.Length;i++)
                {
                    var source=materials[i];if(source==null||!source.HasProperty("_BaseColor"))continue;
                    var color=source.GetColor("_BaseColor");if(color.g<=color.r*1.03f)continue;
                    if(!copies.TryGetValue(source,out var copy))
                    {copy=new Material(source){name=source.name+" ("+Descriptor.seasonId+")"};copy.SetColor("_BaseColor",palette.Plant(color,.52f,Descriptor.seasonId=="winter"));if(copy.HasProperty("_SnowCover"))copy.SetFloat("_SnowCover",palette.SnowCoverage);copies.Add(source,copy);_seasonMaterials.Add(copy);}
                    materials[i]=copy;changed=true;
                }
                if(changed)renderer.sharedMaterials=materials;
            }
            var ground=decor.GetComponentInChildren<MeadowSurface>();ground?.SetPalette(palette);
        }
        void BuildShelter(Transform decor)
        {
            using var audit = PerformanceAudit.Measure("QC.EnvironmentComposer.BuildShelter");
            const int resolution=32;var values=new float[resolution*resolution];var treeRoots=new List<Vector3>();
            foreach(var tile in _tiles.Values)treeRoots.AddRange(tile.roots);
            if(decor!=null)foreach(var renderer in decor.GetComponentsInChildren<Renderer>())if(renderer.bounds.size.y>1.5f)treeRoots.Add(renderer.bounds.center);
            var origin=new Vector2(-40,-40);var size=new Vector2(80,80);
            for(int z=0;z<resolution;z++)for(int x=0;x<resolution;x++)
            {
                var p=origin+new Vector2(x/(float)(resolution-1)*size.x,z/(float)(resolution-1)*size.y);float density=0;
                foreach(var tree in treeRoots){float distance=(p-new Vector2(tree.x,tree.z)).sqrMagnitude;density+=Mathf.Exp(-distance/18)*.30f;}
                values[z*resolution+x]=Mathf.Clamp01(density);
            }
            Shelter=new WindShelterField(values,resolution,origin,size,4);
        }
        void BuildStory()
        {
            using var audit = PerformanceAudit.Measure("QC.EnvironmentComposer.BuildStory");
            if(Descriptor.storyMotifs==null||Descriptor.storyMotifs.Length==0)return;
            var mesh=CampStoryComposer.Compose(_level);if(mesh==null)return;
            // Reject the full prop footprint, not just its centre: a doorway or
            // long ruined wall must never span an authored path or shoreline.
            var at=Vector3.zero;bool found=false;
            for(int ring=0;ring<3&&!found;ring++)for(int corner=0;corner<4&&!found;corner++)
            {
                float x=(corner%2==0?-1:1)*(_level.width*.5f+5.2f+ring*3);
                float z=(corner<2?-1:1)*(_level.height*.5f+4.3f+ring*3);
                var candidate=new Vector3(x,0,z);var footprint=mesh.bounds;footprint.center+=candidate;
                if(CanScenicBounds(_level,footprint)){at=candidate;found=true;}
            }
            if(!found){Destroy(mesh);return;}
            var story=new GameObject("Quiet remnants");story.transform.SetParent(transform,false);story.transform.localPosition=at;story.layer=BoardRenderer.DecorLayer;
            story.AddComponent<MeshFilter>().sharedMesh=mesh;
            var material=new Material(Resources.Load<Shader>("QuietCamp/FoliageLit")){name="Weathered quiet remnants"};
            material.SetColor("_BaseColor",Color.white);material.SetFloat("_VertexTint",1);material.SetFloat("_SnowCover",SeasonPalette.For(_level).SnowCoverage);
            material.SetFloat("_SwayAmp",0);material.SetFloat("_FlutterAmp",0);material.SetFloat("_InnerShade",.1f);_seasonMaterials.Add(material);
            story.AddComponent<MeshRenderer>().sharedMaterial=material;var owned=story.AddComponent<OwnedEnvironmentMesh>();owned.Mesh=mesh;
            story.AddComponent<CampLandmarkVisual>().Configure(_level);
        }
        public static bool CanScenicBounds(LevelData level,Bounds footprint)
        {
            if(footprint.min.x<level.width*.5f+2.5f&&footprint.max.x>-level.width*.5f-2.5f
                &&footprint.min.z<level.height*.5f+2.5f&&footprint.max.z>-level.height*.5f-2.5f)return false;
            var shore=EnvironmentCompositionData.For(level).shore;
            if(shore!=null)
            {
                float nearBank=shore.offset-shore.width*.5f-.8f-ShorelineGeometry.MaximumBend,farBank=shore.offset+shore.width*.5f+.8f+ShorelineGeometry.MaximumBend;
                float axisMin=shore.side=="left"?-footprint.max.x:shore.side=="right"?footprint.min.x:shore.side=="back"?-footprint.max.z:footprint.min.z;
                float axisMax=shore.side=="left"?-footprint.min.x:shore.side=="right"?footprint.max.x:shore.side=="back"?-footprint.min.z:footprint.max.z;
                if(axisMax>nearBank&&axisMin<farBank)return false;
            }
            for(float z=footprint.min.z;z<=footprint.max.z+.75f;z+=.75f)
            for(float x=footprint.min.x;x<=footprint.max.x+.75f;x+=.75f)
            {
                var point=new Vector3(Mathf.Min(x,footprint.max.x),0,Mathf.Min(z,footprint.max.z));
                if(CampTrail.IsCorridor(level,point,1.4f))return false;
                foreach(var cell in level.exteriorWalkable??System.Array.Empty<int[]>())
                {
                    if(cell?.Length!=2)continue;var route=BoardMath.CellCenterWorld(level,new Cell(cell[0],cell[1]));
                    if((new Vector2(route.x-point.x,route.z-point.z)).sqrMagnitude<2.25f)return false;
                }
            }
            return true;
        }
        void BuildEarthContacts(Transform decor)
        {
            using var audit = PerformanceAudit.Measure("QC.EnvironmentComposer.BuildEarthContacts");
            if(decor==null)return;var vertices=new List<Vector3>();var uv=new List<Vector2>();var indices=new List<int>();
            foreach(var renderer in decor.GetComponentsInChildren<Renderer>())
            {
                var bounds=renderer.bounds;if(bounds.size.y<.25f||bounds.size.y>1.7f||bounds.min.y>.2f)continue;
                var centre=new Vector3(bounds.center.x,.004f,bounds.center.z);
                if(Mathf.Abs(centre.x)<_level.width*.5f+.4f&&Mathf.Abs(centre.z)<_level.height*.5f+.4f)continue;
                float radius=Mathf.Clamp(Mathf.Max(bounds.extents.x,bounds.extents.z)*1.4f,.25f,1.4f);int start=vertices.Count;
                vertices.Add(centre);uv.Add(Vector2.zero);
                for(int i=0;i<=12;i++)
                {float a=i*Mathf.PI/6;var direction=new Vector2(Mathf.Cos(a),Mathf.Sin(a));vertices.Add(centre+new Vector3(direction.x,0,direction.y)*radius);uv.Add(direction);if(i>0){indices.Add(start);indices.Add(start+i+1);indices.Add(start+i);}}
            }
            if(vertices.Count==0)return;
            var shader=Resources.Load<Shader>("QuietCamp/GroundPatch");if(shader==null)return;
            var mesh=new Mesh{name="Earth around boulders"};mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetTriangles(indices,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
            var material=new Material(shader){name="Soft worn soil"};material.SetColor("_SoilTone",SeasonPalette.For(_level).Soil);_seasonMaterials.Add(material);
            var ground=new GameObject("Settled boulder soil",typeof(MeshFilter),typeof(MeshRenderer));ground.transform.SetParent(transform,false);ground.layer=BoardRenderer.DecorLayer;
            ground.GetComponent<MeshFilter>().sharedMesh=mesh;var r=ground.GetComponent<MeshRenderer>();r.sharedMaterial=material;r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=true;
            ground.AddComponent<OwnedEnvironmentMesh>().Mesh=mesh;
        }
        internal static void AppendBox(List<Vector3> vertices,List<int> indices,Vector3 centre,Vector3 size)
        {
            var half=size*.5f;int start=vertices.Count;vertices.AddRange(new[]{centre+new Vector3(-half.x,-half.y,-half.z),centre+new Vector3(half.x,-half.y,-half.z),centre+new Vector3(half.x,half.y,-half.z),centre+new Vector3(-half.x,half.y,-half.z),centre+new Vector3(-half.x,-half.y,half.z),centre+new Vector3(half.x,-half.y,half.z),centre+new Vector3(half.x,half.y,half.z),centre+new Vector3(-half.x,half.y,half.z)});
            foreach(int i in new[]{0,2,1,0,3,2,4,5,6,4,6,7,0,4,7,0,7,3,1,2,6,1,6,5,0,1,5,0,5,4,3,7,6,3,6,2})indices.Add(start+i);
        }
        void OnDestroy()
        {
            foreach(var tile in _tiles.Values)if(tile.mesh!=null)Destroy(tile.mesh);foreach(var tile in _pool)if(tile.mesh!=null)Destroy(tile.mesh);
            foreach(var material in _seasonMaterials)if(material!=null)Destroy(material);if(_forestMaterial!=null)Destroy(_forestMaterial);
        }
    }
    public sealed class OwnedEnvironmentMesh:MonoBehaviour
    {public Mesh Mesh;void OnDestroy(){if(Mesh!=null)Destroy(Mesh);}}
}
