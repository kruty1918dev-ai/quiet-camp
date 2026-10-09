using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using QuietCamp.Application;
using QuietCamp.Domain;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace QuietCamp.Presentation.UI.Prepared
{
    /// <summary>One orthographic world camera, one shared material and bounded, reusable chunk meshes.
    /// Input/progression stay on the map controller. No prefab, solver or texture loading while scrolling.</summary>
    public sealed partial class RoadmapWorldRenderer : MonoBehaviour
    {
        const int Layer=28;
        const float Pitch=55;
        sealed class Slot
        {
            public int Key=int.MinValue;public bool Wanted,Ready;
            public Mesh Mesh,BakedMesh;public MeshRenderer Renderer;public MeshFilter Filter;
            public RoadmapReveal Reveal;
            public bool StoryCompleted,KeepStoryMesh;
            public readonly MaterialPropertyBlock Block=new MaterialPropertyBlock();
        }
        sealed class CameraLease {public Camera Camera;public int Mask,Owners;}
        static readonly Dictionary<Camera,CameraLease> CameraLeases=new Dictionary<Camera,CameraLease>();
        static RoadmapWorldRenderer _owner;
        Camera _cameraLease;
        RoadmapGraphic _map;RawImage _image;Camera _camera;
        Mesh _placeholder;
        Light _sun,_previousSun;bool _sunOverride;RenderTexture _texture;Material _material;Transform _world;
        readonly Slot[] _glades=new Slot[16],_chunks=new Slot[3],_branches=new Slot[8];
        readonly RoadmapSlotPlan[] _plans={new RoadmapSlotPlan(3),new RoadmapSlotPlan(16),new RoadmapSlotPlan(8)};
        readonly List<Vector3> _vertices=new List<Vector3>(),_normals=new List<Vector3>();
        readonly List<Color> _colors=new List<Color>();
        readonly List<Vector2> _wind=new List<Vector2>();
        readonly List<Vector2> _sourceUv=new List<Vector2>();
        readonly List<int> _triangles=new List<int>();
        readonly Vector3[] _corners=new Vector3[4];
        static readonly Vector2[] ConductorAnchors={new Vector2(.229351f,.582817f),new Vector2(.201305f,.757323f),new Vector2(.160795f,.931829f)};
        Rect _cameraView;
        Coroutine _builder;int _width,_tier=-1,_frontier=-1,_revealRevision=-1;float _scale,_localFog,_terrainHorizon=float.PositiveInfinity;bool _farLandmark;
        public bool Ready {get;private set;}
        public Camera WorldCamera=>_camera;
        public Transform WorldRoot=>_world;
        public Rect CameraView=>_cameraView;
        public int LiveMeshes {get;private set;}
        public int MaterialCount=>_material!=null?1:0;
        public int TextureCount=>_texture!=null?1:0;
        public int Rebuilds {get;private set;}
        public int Vertices {get;private set;}
        Slot[][] _pools;
        public void Configure(RoadmapGraphic map,RectTransform viewport)
        {
            _map=map;
            if(_owner!=null&&_owner!=this)_owner.enabled=false;_owner=this;
            var shader=Resources.Load<Shader>("QuietCamp/RoadmapWorld");
            if(shader==null||!shader.isSupported)throw new InvalidOperationException("Roadmap world shader is unavailable");
            _material=new Material(shader){name="Roadmap world shared lighting"};
            var rect=QcUi.Stretch(viewport,"RoadmapWorldView");rect.SetAsFirstSibling();_image=rect.gameObject.AddComponent<RawImage>();_image.raycastTarget=false;
            _world=new GameObject("Roadmap streamed world").transform;
            _camera=new GameObject("Roadmap single world camera").AddComponent<Camera>();_camera.transform.SetParent(_world,false);
            _camera.orthographic=true;_camera.transform.rotation=Quaternion.Euler(Pitch,0,0);_camera.cullingMask=1<<Layer;
            _camera.nearClipPlane=.1f;_camera.farClipPlane=180;_camera.clearFlags=CameraClearFlags.SolidColor;
            _camera.backgroundColor=new Color(.69f,.75f,.64f);_camera.allowHDR=false;_camera.allowMSAA=false;
            var additional=_camera.GetUniversalAdditionalCameraData();additional.renderPostProcessing=false;additional.requiresColorTexture=false;additional.requiresDepthTexture=false;
            _sun=new GameObject("Roadmap warm key light").AddComponent<Light>();_sun.transform.SetParent(_world,false);
            _sun.type=LightType.Directional;_sun.intensity=2.5f;_sun.color=new Color(1,.90f,.73f);_sun.shadows=LightShadows.Soft;_sun.shadowBias=.035f;_sun.shadowNormalBias=.3f;_sun.cullingMask=1<<Layer;
            RenderPipelineManager.beginCameraRendering+=BeforeCamera;RenderPipelineManager.endCameraRendering+=AfterCamera;
            if(!UsesBaked){_vertices.Capacity=_normals.Capacity=_colors.Capacity=_wind.Capacity=_sourceUv.Capacity=_triangles.Capacity=40000;BuildPlaceholder();}
            CreateSlots(_chunks,"terrain");CreateSlots(_glades,"camp");CreateSlots(_branches,"branch");_pools=new[]{_chunks,_glades,_branches};ExcludeUnderlyingCamera();
        }
        void CreateSlots(Slot[] pool,string name)
        {
            for(int i=0;i<pool.Length;i++)
            {
                var obj=new GameObject("Roadmap pooled "+name+i,typeof(MeshFilter),typeof(MeshRenderer));obj.layer=Layer;obj.transform.SetParent(_world,false);
                var slot=new Slot{Mesh=new Mesh{name="Roadmap cached "+name+i,indexFormat=IndexFormat.UInt32},Renderer=obj.GetComponent<MeshRenderer>(),Filter=obj.GetComponent<MeshFilter>()};
                slot.Mesh.MarkDynamic();slot.Filter.sharedMesh=slot.Mesh;slot.Renderer.sharedMaterial=_material;
                slot.Renderer.shadowCastingMode=ShadowCastingMode.On;slot.Renderer.receiveShadows=true;obj.SetActive(false);pool[i]=slot;
            }
        }
        void ExcludeUnderlyingCamera()
        {
            if(!ReferenceEquals(_cameraLease,null))return;var camera=Camera.main;if(camera==null||camera==_camera)return;
            if(!CameraLeases.TryGetValue(camera,out var lease)){lease=new CameraLease{Camera=camera,Mask=camera.cullingMask};CameraLeases.Add(camera,lease);}
            lease.Owners++;_cameraLease=camera;camera.cullingMask&=~(1<<Layer);
        }
        void ReleaseUnderlyingCamera()
        {
            if(ReferenceEquals(_cameraLease,null))return;
            if(CameraLeases.TryGetValue(_cameraLease,out var lease)&&--lease.Owners==0){if(lease.Camera!=null)lease.Camera.cullingMask=lease.Mask;CameraLeases.Remove(_cameraLease);}
            _cameraLease=null;
        }
        float SinPitch=>Mathf.Sin(Pitch*Mathf.Deg2Rad);
        Vector3 At(int index)=>At(_map.Data.Nodes[index].x,_map.Data.Y(index));
        Vector3 At(float x,float distance)=>new Vector3((x-.5f)*(_map.HasCulturalLandscape?RoadmapRuralLayout.PathWidth:_map.rectTransform.rect.width/_scale),0,-distance/((UsesBaked?28:_scale)*SinPitch));
        float Ground(float x,float z)
        {
#if UNITY_EDITOR
            if(_offlineDocs!=null){if(ZoneAt(x,z,"water")!=null)return -.12f;return Application.RoadmapCompositionAdapter.TerrainHeight(_map.Data,_map.Environment,x,-z)+QuietCamp.Composition.SurfaceRecipes.Height(_offlineDocs.SelectMany(d=>d.surfaces),x,-z);}
#endif
            float distance=-z*_scale*SinPitch;int index=_map.Data.NodeAt(distance);
            var a=At(index);var b=At(Mathf.Min(index+1,_map.Data.Nodes.Length-1));
            float proximity=Mathf.Min(new Vector2(x-a.x,z-a.z).magnitude,new Vector2(x-b.x,z-b.z).magnitude);
            float clear=Mathf.SmoothStep(0,1,Mathf.InverseLerp(5,9,proximity));
            var environment=_map.Environment.Sample(distance);
            return (environment.Relief*Mathf.Sin(x*.32f)*Mathf.Cos(z*.16f)+environment.Snow*.25f*(1+Mathf.Sin(x*.2f+z*.13f)))*clear;
        }
        public Vector2 ScreenTarget(int index)
        {
            var p=_camera.WorldToViewportPoint(At(index)+Vector3.up*.1f);var visible=_cameraView;
            return new Vector2(_map.rectTransform.rect.xMin+p.x*_map.rectTransform.rect.width,visible.yMin+p.y*visible.height);
        }
        void LateUpdate()
        {
            if(_map==null||_map.VisibleArea.height<=0)return;
            _material.SetVector("_WorldSun",new Vector3(-.4f,.7f,-.5f));_material.SetColor("_WorldSunColor",new Color(1,.94f,.79f));_material.SetColor("_WorldAmbient",new Color(.55f,.63f,.48f));
            _scale=_map.ProjectionScale;
            _image.rectTransform.GetWorldCorners(_corners);
            var a=_map.rectTransform.InverseTransformPoint(_corners[0]);var b=_map.rectTransform.InverseTransformPoint(_corners[2]);
            _cameraView=Rect.MinMaxRect(a.x,a.y,b.x,b.y);
            Resize();
            float distance=_map.rectTransform.rect.yMax-_cameraView.center.y;
            var target=At(.5f,distance);if(UsesBaked)target.x+=_map.ZoomOffsetX;_camera.transform.position=target-_camera.transform.forward*30;
            _camera.orthographicSize=_cameraView.height/(2*_scale);_camera.aspect=_cameraView.width/_cameraView.height;
            var visual=_map.VisualAt(distance);
            _camera.backgroundColor=visual.Palette.GrassLight;
            // One world sun. Temperature/ambient interpolate continuously; no nearest-node lighting switch.
            var sun=-(Quaternion.Euler(Mathf.Lerp(50,22,visual.Environment.Night),65,0)*Vector3.forward);
            _sun.transform.rotation=Quaternion.LookRotation(-sun);_sun.color=visual.Sun;_sun.intensity=Mathf.Lerp(.85f,.22f,visual.Environment.Night);
            _material.SetVector("_WorldSun",sun);_material.SetColor("_WorldSunColor",visual.Sun);
            _material.SetColor("_WorldAmbient",visual.Ambient);
            _material.SetFloat("_WorldMotion",_map.Reduced?0:1);_material.SetFloat("_WorldTime",Time.unscaledTime);
            if(_revealRevision!=_map.RevealState.Revision)
            {
                _revealRevision=_map.RevealState.Revision;_frontier=_map.Frontier;
                if(_builder!=null&&!UsesBaked){StopCoroutine(_builder);_builder=null;}
                foreach(var slot in _glades)if(slot.Key>=0&&slot.Key<_map.Data.Nodes.Length&&(slot.Reveal!=_map.Reveal(slot.Key)||slot.StoryCompleted!=(_map.Scenes[slot.Key]?.StoryCompleted??false)))slot.Ready=false;
                foreach(var slot in _branches)slot.Ready=false;
                // Neighbor terrain was sampled under the former reveal cap. Rebuild only
                // affected leases, retaining their old mesh until the new one is complete.
                float horizon=Mathf.Min(_terrainHorizon,_map.KnownDistance);
                foreach(var slot in _chunks)if(slot.Key>=0&&_map.Data.Chunks[slot.Key].Bottom+600>=horizon)slot.Ready=false;
                _terrainHorizon=_map.KnownDistance;
            }
            float nearSpan=_map.KnownDistance-_map.Data.Y(_map.Frontier);
            _material.SetVector("_RevealFront",new Vector4(_map.RevealDistance+120,_map.RevealDistance+nearSpan+500,(UsesBaked?28:_scale)*SinPitch,0));
            _material.SetColor("_RevealFog",Color.Lerp(_map.RevealFog,visual.Palette.Fog,.65f).linear);
            if(UsesBaked)TrimBaked();Sync();Ready=true;LiveMeshes=Vertices=0;
            foreach(var pool in _pools??Array.Empty<Slot[]>())foreach(var slot in pool)if(slot.Wanted){Ready&=slot.Ready;LiveMeshes++;Vertices+=slot.Ready?(slot.BakedMesh??slot.Mesh).vertexCount:(_placeholder!=null?_placeholder.vertexCount:0);}
            if(_builder==null&&!Ready&&(!UsesBaked||CanLoadBaked))_builder=StartCoroutine(UsesBaked?LoadBaked():BuildPending());
        }
        void Resize()
        {
            int width=Mathf.RoundToInt(_map.rectTransform.rect.width);
            bool rebuild=_tier!=_map.QualityTier||width!=_width&&!_map.HasCulturalLandscape;
            _width=width;_tier=_map.QualityTier;
            if(rebuild){if(_builder!=null&&!UsesBaked){StopCoroutine(_builder);_builder=null;}foreach(var pool in _pools??Array.Empty<Slot[]>())foreach(var slot in pool)slot.Ready=false;}
            var rect=_image.rectTransform.rect;var canvas=_image.canvas;
            float ratio=Mathf.Max(.25f,canvas!=null?canvas.scaleFactor:1);
            int w=Mathf.Clamp(Mathf.RoundToInt(rect.width*ratio),64,2048),h=Mathf.Clamp(Mathf.RoundToInt(rect.height*ratio),64,2048);
            float cap=_tier==2?1800:_tier==1?1600:1280;
            float down=Mathf.Min(1,cap/Mathf.Max(w,h));w=Mathf.Max(64,Mathf.RoundToInt(w*down));h=Mathf.Max(64,Mathf.RoundToInt(h*down));
            const int maxPixels=2*1024*1024;
            if(UsesBaked&&(long)w*h>maxPixels){float factor=Mathf.Sqrt(maxPixels/(float)((long)w*h));w=Mathf.Max(64,Mathf.FloorToInt(w*factor));h=Mathf.Max(64,Mathf.FloorToInt(h*factor));}
            if(_texture!=null&&_texture.width==w&&_texture.height==h)return;
            _camera.targetTexture=null;_image.texture=null;if(_texture!=null){_texture.Release();DisposeObject(_texture);}
            _texture=new RenderTexture(w,h,24,RenderTextureFormat.ARGB32){name="Roadmap single view",antiAliasing=1};_texture.Create();_camera.targetTexture=_texture;_image.texture=_texture;
        }
        void Sync()
        {
            foreach(var pool in _pools??Array.Empty<Slot[]>())foreach(var slot in pool)slot.Wanted=false;
            foreach(var plan in _plans)plan.Begin();
            int region=_map.Data.RegionAt(_map.rectTransform.rect.yMax-_map.VisibleArea.center.y);
            for(int c=_map.FirstActiveChunk;c<=_map.LastActiveChunk;c++)if(c>=0)_plans[0].Request(c);
            int nearest=_map.Data.NearestNode(_map.rectTransform.rect.yMax-_map.VisibleArea.center.y);
            for(int i=Mathf.Max(0,nearest-12);i<=Mathf.Min(_map.Data.Nodes.Length-1,nearest+12);i++)
                if(_map.Reveal(i)!=RoadmapReveal.Hidden&&_map.InView(_map.Centre(i).y,500))_plans[1].Request(i);
            for(int r=Mathf.Max(0,region-1);r<=Mathf.Min(region+1,_map.Data.Definition.regions.Length-1);r++)
            {
                var data=_map.Data.Definition.regions[r];for(int b=0;b<data.branches.Length;b++)
                {
                    var branch=data.branches[b];int anchor=_map.Data.NodeIndex(branch.anchorNodeId);
                    if(anchor>=0&&_map.BranchVisible(branch)&&_map.InView(_map.Position(branch.x,_map.Data.RegionStarts[r]+branch.y).y,300*_map.Zoom))_plans[2].Request(r*4+b);
                }
            }
            // Resolve the whole window first. A new leading identity must not evict a
            // cached node/terrain/branch that will be requested later in the same frame.
            for(int p=0;p<_plans.Length;p++)
            {
                var plan=_plans[p];plan.Resolve();
                for(int request=0;request<plan.Count;request++)
                {
                    var slot=_pools[p][plan.SlotAt(request)];int key=plan.KeyAt(request);
                    if(slot.Key!=key){slot.Key=key;slot.BakedMesh=null;slot.Mesh.Clear();slot.KeepStoryMesh=false;slot.Ready=false;slot.Renderer.gameObject.SetActive(false);}
                    slot.Wanted=true;
                }
            }
            foreach(var pool in _pools??Array.Empty<Slot[]>())foreach(var slot in pool)
            {
                bool camp=pool==_glades;bool placeholder=!UsesBaked&&camp&&slot.Wanted&&!slot.Ready&&!slot.KeepStoryMesh;
                if(slot.Wanted)
                {
                    float sceneFog=0;
                    if(camp){slot.Reveal=_map.Reveal(slot.Key);sceneFog=slot.Reveal==RoadmapReveal.Revealed?1-_map.RevealOpacity(slot.Key):Mathf.Max(.50f,1-_map.RevealOpacity(Mathf.Max(0,slot.Key-2)));}
                    else if(pool==_branches){var branch=_map.Data.Definition.regions[slot.Key/4].branches[slot.Key%4];sceneFog=1-_map.RevealOpacity(_map.Data.NodeIndex(branch.anchorNodeId));}
                    slot.Block.SetFloat("_SceneFog",sceneFog);slot.Renderer.SetPropertyBlock(slot.Block);
                }
                slot.Filter.sharedMesh=UsesBaked?slot.BakedMesh:placeholder?_placeholder:slot.Mesh;
                slot.Renderer.transform.position=UsesBaked?BakedPosition(pool,slot.Key):placeholder?At(slot.Key):Vector3.zero;
                if(placeholder)slot.Renderer.shadowCastingMode=ShadowCastingMode.Off;
                slot.Renderer.gameObject.SetActive(slot.Wanted&&(slot.Ready||placeholder||!UsesBaked&&slot.Mesh.vertexCount>0));
            }
        }
        public void InvalidateStory(int index)
        {
            foreach(var slot in _glades)if(slot.Key==index)
            {slot.Ready=false;slot.KeepStoryMesh=_map.Reveal(index)==RoadmapReveal.Revealed;}
        }
        void Clear(){_localFog=0;_farLandmark=false;_vertices.Clear();_normals.Clear();_colors.Clear();_wind.Clear();_sourceUv.Clear();_triangles.Clear();}
        void Vertex(Vector3 position,Vector3 normal,Color color,Vector2 wind,bool linear=false,Vector2? uv=null)
        {color.a=_farLandmark?1.1f:1-_localFog;_triangles.Add(_vertices.Count);_vertices.Add(position);_normals.Add(normal);_colors.Add(linear?color:color.linear);_wind.Add(wind);_sourceUv.Add(uv??new Vector2(position.x*.1f,position.z*.1f));}
        void Triangle(Vector3 a,Vector3 b,Vector3 c,Color tint)
        {var normal=Vector3.Cross(b-a,c-a).normalized;Vertex(a,normal,tint,new Vector2(-1,0));Vertex(b,normal,tint,new Vector2(-1,0));Vertex(c,normal,tint,new Vector2(-1,0));}
        void BuildPlaceholder()
        {
            Clear();var library=RoadmapModelLibrary.Load();
            for(int i=0;i<3;i++){float angle=i*2.094f;Append(library.Get("tree_pineRoundA"),new Vector3(Mathf.Cos(angle)*2.7f,0,Mathf.Sin(angle)*2.7f),2.1f,0);}
            var neutral=new Color(.50f,.56f,.48f).linear;for(int i=0;i<_colors.Count;i++)_colors[i]=Color.Lerp(neutral,_colors[i],.12f);
            _placeholder=new Mesh{name="Roadmap shared pending silhouette"};_placeholder.SetVertices(_vertices);_placeholder.SetNormals(_normals);_placeholder.SetColors(_colors);_placeholder.SetUVs(1,_wind);_placeholder.SetTriangles(_triangles,0);_placeholder.RecalculateBounds();Clear();
        }
        IEnumerator BuildPending()
        {
            ProceduralBuilds++;
            yield return null;
            while(true)
            {
                Slot next=null;int kind=-1;
                for(int k=0;k<3&&next==null;k++)
                {
                    // Prioritize the visible camp, then the current terrain, then neighbor/detail work.
                    var pool=k==0?_glades:k==1?_chunks:_branches;float nearest=float.PositiveInfinity;
                    foreach(var slot in pool)if(slot.Wanted&&!slot.Ready)
                    {
                        float y=k==0?_map.Data.Y(slot.Key):k==1?(_map.Data.Chunks[slot.Key].Top+_map.Data.Chunks[slot.Key].Bottom)*.5f:float.MaxValue;
                        float distance=k==2?0:Mathf.Abs(y-(_map.rectTransform.rect.yMax-_cameraView.center.y));
                        if(distance<nearest){nearest=distance;next=slot;kind=k==0?1:k==1?0:2;}
                    }
                }
                if(next==null)break;int key=next.Key;Clear();
                if(kind==0)yield return Terrain(key);
                else if(kind==1)
                {
                    var scene=_map.Scenes[key];
                    if(scene==null)
                    {
                        if(_map.Reveal(key)==RoadmapReveal.Revealed){yield return null;continue;}
                        var silhouette=At(key);var models=RoadmapModelLibrary.Load();
                        for(int i=0;i<4;i++){float a=i*1.57f;Append(models.Get("tree_pineRoundA"),silhouette+new Vector3(Mathf.Cos(a)*3,0,Mathf.Sin(a)*3),2.3f,0);yield return null;}
                        for(int i=0;i<_colors.Count;i++)_colors[i]=Color.Lerp(new Color(.57f,.65f,.53f).linear,_colors[i],.12f);
                    }
                    else
                    {
                    var origin=At(key);origin.y=Ground(origin.x,origin.z);
                    long started=System.Diagnostics.Stopwatch.GetTimestamp();
                    foreach(var prop in scene.Props)
                    {
                        if(next.Key!=key||!next.Wanted)break;
                        Append(prop.Geometry??RoadmapModelLibrary.Load().Get(prop.Asset),origin+prop.Position,prop.Height,prop.Yaw,scene,prop);
                        if((System.Diagnostics.Stopwatch.GetTimestamp()-started)/(double)System.Diagnostics.Stopwatch.Frequency>=.00125){yield return null;started=System.Diagnostics.Stopwatch.GetTimestamp();}
                    }
                    // The shared terrain already flattens the clearing around each camp.
                    // A separate rectangular board would reveal the puzzle footprint as a hard patch.
                    }
                }
                else yield return Branch(key);
                if(next.Key==key&&next.Wanted)
                {
                    using(var work=_map.Work.Measure(RoadmapWorkMetrics.Glade))
                    {next.Mesh.Clear();next.Mesh.SetVertices(_vertices);next.Mesh.SetNormals(_normals);next.Mesh.SetColors(_colors);next.Mesh.SetUVs(1,_wind);next.Mesh.SetTriangles(_triangles,0);next.Mesh.RecalculateBounds();}
                    // Wind stays inside this conservative bound, including shadows.
                    var bounds=next.Mesh.bounds;bounds.Expand(.5f);next.Mesh.bounds=bounds;
                    next.Renderer.shadowCastingMode=kind==1&&_map.Reveal(key)!=RoadmapReveal.Revealed?ShadowCastingMode.Off:ShadowCastingMode.On;
                    next.KeepStoryMesh=false;
                    if(kind==1)next.StoryCompleted=_map.Scenes[key]?.StoryCompleted??false;
                    next.Filter.sharedMesh=next.Mesh;next.Renderer.transform.position=Vector3.zero;next.Ready=true;next.Renderer.gameObject.SetActive(true);Rebuilds++;
                }
                yield return null;
            }
            _builder=null;
        }
        void Append(RoadmapModelLibrary.Model model,Vector3 origin,float height,float yaw,RoadmapSceneGenerator.Scene scene=null,RoadmapSceneGenerator.Prop prop=null,Color? vertexTint=null)
        {
            if(model==null)
            {
#if UNITY_EDITOR
                if(_offline)throw new InvalidOperationException("Offline model reference missing: "+prop?.Asset);
#endif
                return;
            }using var work=_map.Work.Measure(RoadmapWorkMetrics.Glade);var rotation=Quaternion.Euler(0,yaw,0);
            for(int triangle=0;triangle<model.Positions.Length;triangle+=3)
            {
                var color=model.Colors[triangle/3];
                if(prop?.Asset=="ua_orchard_tree"&&RoadmapModelLibrary.IsOrchardFruit(color)&&prop.HasVisual&&(prop.Visual.Environment.Snow>.05f||prop.Visual.Environment.Temperature<.4f))continue;
                if(prop?.HasVisual==true)
                {
                    if(prop.Sway)color=prop.Visual.Plant(color,World.SeasonProfile.Variation(prop.Position,0),prop.Asset.Contains("pine"));
                    if(prop.Asset=="ua_wheat_patch")
                    {
                        Color Grain(RoadmapEnvironmentProfile p)=>p.Season=="spring"?new Color(.40f,.56f,.20f):p.Season=="autumn"?new Color(.59f,.49f,.28f):new Color(.80f,.64f,.29f);
                        color=Color.Lerp(Grain(prop.Visual.Environment.From),Grain(prop.Visual.Environment.To),prop.Visual.Environment.Blend);
                    }
                    color=Color.Lerp(color,new Color(.88f,.92f,.98f),prop.Visual.Environment.Snow*Mathf.SmoothStep(0,1,Mathf.Clamp01((model.Normals[triangle].y-.35f)/.4f)));
                }
                else if(scene!=null&&prop!=null)color=scene.SurfaceColor(color,prop,model.Normals[triangle]);
                if(prop?.StorySilhouette==true)color=Color.Lerp(scene.Palette.Fog,scene.Palette.GrassDark,.35f);
                if(vertexTint.HasValue)color*=vertexTint.Value;
                color=color.linear;
                for(int vertex=triangle;vertex<triangle+3;vertex++)
                {
                    var local=prop!=null?prop.LeafVertex(model.Positions[vertex],model.Colors[triangle/3]):model.Positions[vertex];var normal=rotation*model.Normals[vertex];
                    float bend=Mathf.Clamp01(local.y-.15f);
                    if(prop?.Asset=="ua_orchard_tree"&&model.Colors[triangle/3].g<model.Colors[triangle/3].r*1.03f)bend*=.08f;
                    var wind=prop?.Sway==true?new Vector2(bend*height*(prop.HasVisual?prop.Visual.Environment.Wind:1),origin.x+origin.z):Vector2.zero;
                    Vertex(origin+rotation*local*height,normal,color,wind,true,model.UVs!=null?model.UVs[vertex]:(Vector2?)null);
                }
            }
        }
        IEnumerator Terrain(int chunkIndex)
        {
            var data=_map.Data;var chunk=data.Chunks[chunkIndex];var region=data.Definition.regions[chunk.Region];float top=chunk.Top,bottom=chunk.Bottom;
            float half=_map.HasCulturalLandscape?region.culturalLandscape.width*.5f:_map.rectTransform.rect.width/_scale*.5f+12,zTop=-top/(_scale*SinPitch),zBottom=-bottom/(_scale*SinPitch);
            int first=chunk.FirstNode;var scene=_map.Scenes[first];
            var palette=_map.VisualAt(top).Palette;
            var ground=palette.GrassLight;
            long slice=System.Diagnostics.Stopwatch.GetTimestamp();
            for(float z=zBottom;z<zTop;z+=3)
            {
                for(float x=-half;x<half;x+=3)
                {
                    float right=Mathf.Min(half,x+3),back=Mathf.Min(zTop,z+3);var tint=ground;
                    var a=new Vector3(x,Ground(x,z),z);var b=new Vector3(x,Ground(x,back),back);var c=new Vector3(right,Ground(right,back),back);var d=new Vector3(right,Ground(right,z),z);
                    TerrainTriangle(a,b,c,tint);TerrainTriangle(a,c,d,tint);
                }
                if((System.Diagnostics.Stopwatch.GetTimestamp()-slice)/(double)System.Diagnostics.Stopwatch.Frequency>=.00125){yield return null;slice=System.Diagnostics.Stopwatch.GetTimestamp();}
            }
            int frontierRegion=data.RegionForNode(_map.Frontier);
            bool landmarkRegion=chunk.Region==frontierRegion+1;
#if UNITY_EDITOR
            if(_offline)landmarkRegion=true;
#endif
            if(landmarkRegion&&chunk.FirstNode==region.firstLevel-1&&region.revealRules?.farLandmarkAssetId!=null)
            {
                var hint=At(region.revealRules.farLandmarkX,data.RegionStarts[chunk.Region]+100);hint.y=Ground(hint.x,hint.z);
                _farLandmark=true;
                Append(RoadmapModelLibrary.Load().Get(region.revealRules.farLandmarkAssetId),hint,Mathf.Clamp(region.revealRules.farLandmarkHeight,1,15),0);
                _farLandmark=false;yield return null;
            }
            #if UNITY_EDITOR
            if(_offlineDocs!=null){yield return SemanticTerrain(chunkIndex);yield return SemanticPaths(chunkIndex);yield break;}
            else
#endif
            if(region.culturalLandscape!=null)yield return RuralTerrain(chunkIndex);
            uint seed=2166136261;foreach(char ch in region.id)seed=(seed^ch)*16777619;
            if(region.culturalLandscape!=null)seed=unchecked((uint)region.culturalLandscape.seed);
            // Global candidate cells prevent a new random forest pattern at every chunk boundary.
            int firstCandidate=Mathf.FloorToInt(top/24),lastCandidate=Mathf.CeilToInt(bottom/24);
            for(int t=firstCandidate;t<lastCandidate;t++)
#if UNITY_EDITOR
                if(_offlineDocs==null)
#endif
            {
                float distance=(t+RoadmapRuralLayout.Unit((int)seed,t,1))*24;if(distance<top||distance>=bottom)continue;
                float x=(RoadmapRuralLayout.Unit((int)seed,t,2)*2-1)*half,z=-distance/((UsesBaked?28:_scale)*SinPitch);
                bool clear=ClearRoute(x,z,1.2f)||RuralReserved(region.culturalLandscape,x,distance)||_map.HasCulturalLandscape&&!RoadmapRuralLayout.ClearOfWater(_map.Environment,x,distance,.6f);
                if(!clear)
                {
                    var visual=_map.VisualAt(-z*_scale*SinPitch);var profile=visual.Environment;
                    // Forest belts frame open agricultural plots, rather than making a wreath of trees.
                    float forest=Mathf.Abs(x)>15?Mathf.Min(.95f,profile.Trees+.22f):profile.Trees;
                    if(RoadmapRuralLayout.Unit((int)seed,t,3)<forest)
                    {
                        bool pine=RoadmapRuralLayout.Unit((int)seed,t,4)<profile.Pines;string asset=pine?"tree_pineRoundA":"tree_default";
                        var tree=!pine&&RoadmapRuralLayout.Unit((int)seed,t,5)<profile.Bare?RoadmapModelLibrary.Load().BareTree(asset):RoadmapModelLibrary.Load().Get(asset);
                        var prop=new RoadmapSceneGenerator.Prop{Asset=asset,Sway=true,Position=new Vector3(x,0,z),HasVisual=true,Visual=visual};
                        Append(tree,new Vector3(x,Ground(x,z),z),2.2f+RoadmapRuralLayout.Unit((int)seed,t,6),RoadmapRuralLayout.Unit((int)seed,t,7)*360,null,prop);
                    }
                    if(RoadmapRuralLayout.Unit((int)seed,t,8)<profile.Grass*.65f)
                    {
                        string asset=t%5==0&&RoadmapRuralLayout.Unit((int)seed,t,9)<profile.Flowers?"flower_yellowA":"grass_leafsLarge";
                        Append(RoadmapModelLibrary.Load().Get(asset),new Vector3(x+.8f,Ground(x,z),z+.5f),.45f,0,null,new RoadmapSceneGenerator.Prop{Asset=asset,Sway=true,HasVisual=true,Visual=visual});
                    }
                }
                if((System.Diagnostics.Stopwatch.GetTimestamp()-slice)/(double)System.Diagnostics.Stopwatch.Frequency>=.00125){yield return null;slice=System.Diagnostics.Stopwatch.GetTimestamp();}
            }
            for(int i=first;i<Mathf.Min(data.Nodes.Length-1,chunk.LastNode+1);i++)
            {
                var a=At(i);var b=At(i+1);for(int p=0;p<24;p++)
                {
                    float t=p/24f,u=(p+1)/24f;var start=Vector3.Lerp(a,b,t);var end=Vector3.Lerp(a,b,u);start.x=Mathf.Lerp(a.x,b.x,Mathf.SmoothStep(0,1,t));end.x=Mathf.Lerp(a.x,b.x,Mathf.SmoothStep(0,1,u));
                    var pathPalette=_map.VisualAt(-start.z*_scale*SinPitch).Palette;var color=Color.Lerp(pathPalette.GrassLight,pathPalette.Soil,.65f);
#if UNITY_EDITOR
                    if(_offlineDocs!=null)OwnedTrail(start,end,.45f,color,chunk.Top,chunk.Bottom);else
#endif
                    Trail(start,end,.45f,color);
                }
                yield return null;
            }
        }
        Color GroundColor(Color color,Vector3 p)
        {
#if UNITY_EDITOR
            if(_offlineDocs!=null)return CompositionGround(p,color);
#endif
            var environment=_map.Environment.Sample(-p.z*_scale*SinPitch);
            float half=_map.HasCulturalLandscape?RoadmapRuralLayout.PathWidth*.5f:_map.rectTransform.rect.width/_scale*.5f;
            float bank=Mathf.Abs(p.x-(half*.68f+Mathf.Sin(p.z*.065f)*1.4f));
            float water=environment.Water*Mathf.SmoothStep(0,1,Mathf.InverseLerp(3.0f,.8f,bank));
            // A lightweight peripheral watercourse within the same terrain mesh/material.
            // Gameplay water and authored shores are untouched.
            color=Color.Lerp(color,new Color(.27f,.49f,.53f),water);
            return color*(.98f+.035f*Mathf.Sin(p.x*.36f+p.z*.22f)+.025f*Mathf.Sin(p.x*.71f-p.z*.38f));
        }
        RoadmapCulturalLandscapeData CultureAt(float distance)=>_map.Data.Definition.regions[_map.Data.RegionAt(distance)].culturalLandscape;
        bool ClearRoute(float x,float z,float radius)
        {
            if(_map.HasCulturalLandscape)return !RoadmapRuralLayout.ClearOfPath(_map.Data,x,-z*_scale*SinPitch,radius);
            float distance=-z*_scale*SinPitch;int index=_map.Data.NodeAt(distance);
            for(int n=Mathf.Max(0,index-2);n<=Mathf.Min(_map.Data.Nodes.Length-1,index+2);n++)
            {
                var a=At(n);if(new Vector2(x-a.x,z-a.z).sqrMagnitude<(5.9f+radius)*(5.9f+radius))return true;
                if(n+1>=_map.Data.Nodes.Length)continue;var b=At(n+1);
                float t=Mathf.InverseLerp(a.z,b.z,z);float pathX=Mathf.Lerp(a.x,b.x,Mathf.SmoothStep(0,1,t));
                if(z<=a.z&&z>=b.z&&Mathf.Abs(x-pathX)<radius+.9f)return true;
            }
            int region=_map.Data.RegionAt(distance);
            for(int r=Mathf.Max(0,region-1);r<=Mathf.Min(_map.Data.Definition.regions.Length-1,region+1);r++)
                foreach(var branch in _map.Data.Definition.regions[r].branches)
                {
                    var b=At(branch.x,_map.Data.RegionStarts[r]+branch.y);var a=At(_map.Data.NodeIndex(branch.anchorNodeId));
                    if(new Vector2(x-b.x,z-b.z).sqrMagnitude<(3+radius)*(3+radius))return true;
                    var delta=b-a;float t=Mathf.Clamp01(Vector3.Dot(new Vector3(x,0,z)-a,delta)/Mathf.Max(.001f,delta.sqrMagnitude));
                    if(new Vector2(x-a.x-delta.x*t,z-a.z-delta.z*t).sqrMagnitude<(radius+.6f)*(radius+.6f))return true;
                }
            return false;
        }
        bool RuralReserved(RoadmapCulturalLandscapeData profile,float x,float distance)
        {
            if(profile==null)return false;int strip=Mathf.FloorToInt(distance/RoadmapRuralLayout.StripLength);
            for(int s=Mathf.Max(0,strip-1);s<=strip+1;s++)
            {
                var rural=CultureAt((s+.5f)*RoadmapRuralLayout.StripLength);
                for(int part=0;part<RoadmapRuralLayout.Parts;part++)
                    if(RoadmapRuralLayout.TrySample(rural,s,part,out var p))
                    {
                        float dz=(distance-p.Distance)/(_scale*SinPitch);
                        float padding=RoadmapRuralLayout.Radius(p.Asset)+.35f;
                        if(Mathf.Abs(x-p.X)<padding&&Mathf.Abs(dz)<padding&&RoadmapRuralLayout.Admitted(_map.Data,_map.Environment,p))return true;
                    }
            }
            return false;
        }
        IEnumerator RuralTerrain(int chunkIndex)
        {
            var chunk=_map.Data.Chunks[chunkIndex];var library=RoadmapModelLibrary.Load();
            int first=Mathf.Max(0,Mathf.FloorToInt(chunk.Top/RoadmapRuralLayout.StripLength)-3),last=Mathf.CeilToInt(chunk.Bottom/RoadmapRuralLayout.StripLength);
            long slice=System.Diagnostics.Stopwatch.GetTimestamp();
            for(int strip=first;strip<=last;strip++)
            {
                var profile=CultureAt((strip+.5f)*RoadmapRuralLayout.StripLength);
                for(int part=0;part<RoadmapRuralLayout.Parts;part++)
                {
                    if(!RoadmapRuralLayout.TrySample(profile,strip,part,out var p)||p.Distance<chunk.Top||p.Distance>=chunk.Bottom)continue;
                    float z=-p.Distance/(_scale*SinPitch);bool crop=p.Asset=="ua_wheat_patch"||p.Asset=="ua_sunflower_patch";
                    if(!RoadmapRuralLayout.Admitted(_map.Data,_map.Environment,p))continue;
                    var visual=_map.VisualAt(p.Distance);
                    var geometry=p.Asset=="ua_orchard_tree"&&RoadmapRuralLayout.Unit(profile.seed,strip,30)<visual.Environment.Bare?library.BareTree(p.Asset):library.Get(p.Asset);
                    if(crop&&_tier<2)geometry=library.Get(p.Asset+"_lod")??geometry;
                    var prop=new RoadmapSceneGenerator.Prop{Asset=p.Asset,Sway=p.Sway,HasVisual=true,Visual=visual,Position=new Vector3(p.X,0,z)};
                    if(crop)
                    {
                        // A broad cultivated bed reads as a field. Small clumps carry the detail;
                        // we do not instantiate thousands of individual stalks or particle systems.
                        var tint=Color.Lerp(visual.Palette.GrassLight,visual.Palette.Soil,.55f);
                        if(p.Asset=="ua_wheat_patch")tint=Color.Lerp(tint,new Color(.67f,.58f,.28f),Mathf.Clamp01(visual.Environment.Temperature)*.35f);
                        for(int row=0;row<3;row++)
                        {
                            float shift=(row-1)*1.05f;var at=new Vector3(p.X+shift,Ground(p.X+shift,z),z);
                            Append(geometry,at,p.Height,p.Yaw,null,prop);
                        }
                        FieldBed(p.X,z,1.65f,1.12f,tint);
                    }
                    else
                    {
                        float ground=Ground(p.X,z);
                        if(p.Asset.Contains("fence")||p.Asset=="ua_gate")ground-=.035f;
                        Append(geometry,new Vector3(p.X,ground,z),p.Height,p.Yaw,null,prop);
                    }
                    if((System.Diagnostics.Stopwatch.GetTimestamp()-slice)/(double)System.Diagnostics.Stopwatch.Frequency>=.00125)
                    {yield return null;slice=System.Diagnostics.Stopwatch.GetTimestamp();}
                }
                if(RoadmapRuralLayout.TrySample(profile,strip,13,out var poleA)&&RoadmapRuralLayout.TrySample(profile,strip,14,out var poleB)&&poleB.Distance<_map.Data.Height)
                {
                    float az=-poleA.Distance/(_scale*SinPitch),bz=-poleB.Distance/(_scale*SinPitch);
                    if(RoadmapRuralLayout.Admitted(_map.Data,_map.Environment,poleA)&&RoadmapRuralLayout.Admitted(_map.Data,_map.Environment,poleB))for(int wire=-1;wire<=1;wire+=2)
                    {
                        float offset=wire*.134f*poleA.Height;
                        var a=new Vector3(poleA.X+offset,Ground(poleA.X,az)+.903f*poleA.Height,az);
                        var b=new Vector3(poleB.X+offset,Ground(poleB.X,bz)+.903f*poleB.Height,bz);
                        Cable(a,b,.24f,chunk.Top,chunk.Bottom);
                    }
                }
                // Identical high-voltage supports form a single corridor. Distribution poles
                // belong to farmsteads; never connect them to the transmission tower circuit.
                if(RoadmapRuralLayout.TrySample(profile,strip,12,out var tower))
                {
                    float start=tower.Distance,end=start+RoadmapRuralLayout.StripLength*3;
                    var next=CultureAt(end);float x=tower.X;
                    float za=-start/(_scale*SinPitch),zb=-end/(_scale*SinPitch);
                    if(RoadmapRuralLayout.TrySample(next,strip+3,12,out var nextTower)&&RoadmapRuralLayout.Admitted(_map.Data,_map.Environment,tower)&&RoadmapRuralLayout.Admitted(_map.Data,_map.Environment,nextTower))
                    {
                        // Anchor geometry is documented in the kit manifest (lower insulator end).
                        foreach(var phase in ConductorAnchors)for(int circuit=-1;circuit<=1;circuit+=2)
                        {
                            var a=new Vector3(x+circuit*phase.x*5.8f,Ground(x,za)+phase.y*5.8f,za);
                            var b=new Vector3(x+circuit*phase.x*5.8f,Ground(x,zb)+phase.y*5.8f,zb);
                            Cable(a,b,.75f,chunk.Top,chunk.Bottom);
                        }
                    }
                }
                yield return null;slice=System.Diagnostics.Stopwatch.GetTimestamp();
            }
        }
        void FieldBed(float x,float z,float halfWidth,float halfLength,Color color)
        {
            Vector3 AtBed(float px,float pz)=>new Vector3(px,Ground(px,pz)+.025f,pz);
            var a=AtBed(x-halfWidth,z-halfLength);var b=AtBed(x-halfWidth,z+halfLength);
            var c=AtBed(x+halfWidth,z+halfLength);var d=AtBed(x+halfWidth,z-halfLength);
            Triangle(a,b,c,color);Triangle(a,c,d,color);
        }
        void Cable(Vector3 a,Vector3 b,float sag,float top,float bottom)
        {
            const int steps=12;var tint=new Color(.24f,.29f,.28f);
            Vector3 Point(float t)=>Vector3.Lerp(a,b,t)-Vector3.up*(4*t*(1-t)*sag);
            for(int i=0;i<steps;i++)
            {
                var p=Point(i/(float)steps);var q=Point((i+1)/(float)steps);float distance=-(p.z+q.z)*.5f*_scale*SinPitch;
                if(distance<top||distance>=bottom)continue;
                // Two orthogonal strips are readable from every permitted camera angle.
                var side=Vector3.right*.018f;Triangle(p-side,q+side,q-side,tint);Triangle(p-side,p+side,q+side,tint);
                side=Vector3.up*.018f;Triangle(p-side,q+side,q-side,tint);Triangle(p-side,p+side,q+side,tint);
            }
        }
        void TerrainTriangle(Vector3 a,Vector3 b,Vector3 c,Color color)
        {var normal=Vector3.Cross(b-a,c-a).normalized;Vertex(a,normal,GroundColor(_map.VisualAt(-a.z*_scale*SinPitch).Palette.GrassLight,a),new Vector2(-1,0));Vertex(b,normal,GroundColor(_map.VisualAt(-b.z*_scale*SinPitch).Palette.GrassLight,b),new Vector2(-1,0));Vertex(c,normal,GroundColor(_map.VisualAt(-c.z*_scale*SinPitch).Palette.GrassLight,c),new Vector2(-1,0));}
        void Trail(Vector3 a,Vector3 b,float width,Color color)
        {
            var delta=b-a;var side=Vector3.Cross(Vector3.up,delta).normalized*width;
            Vector3 Surface(Vector3 p){p.y=Ground(p.x,p.z)+.07f;return p;}
            Vector3 al=Surface(a-side),ar=Surface(a+side),bl=Surface(b-side),br=Surface(b+side);
            Triangle(al,bl,br,color);Triangle(al,br,ar,color);
            var outer=side*1.65f;var edge=Color.Lerp(color,_map.VisualAt(-a.z*_scale*SinPitch).Palette.GrassLight,.80f);
            Triangle(Surface(a-outer),Surface(b-outer),bl,edge);Triangle(Surface(a-outer),bl,al,edge);
            Triangle(ar,br,Surface(b+outer),edge);Triangle(ar,Surface(b+outer),Surface(a+outer),edge);
        }
        RoadmapSceneGenerator.Scene _branchScene;
        IEnumerator Branch(int key)
        {
            int r=key/4,branchIndex=key%4;var data=_map.Data;var branch=data.Definition.regions[r].branches[branchIndex];
            var origin=At(branch.x,data.RegionStarts[r]+branch.y);origin.y=Ground(origin.x,origin.z);
            var anchor=At(data.NodeIndex(branch.anchorNodeId));
            for(int p=0;p<18;p++){float t=p/18f,u=(p+1)/18f;var a=Vector3.Lerp(anchor,origin,t);var b=Vector3.Lerp(anchor,origin,u);a.x=Mathf.Lerp(anchor.x,origin.x,Mathf.SmoothStep(0,1,t));b.x=Mathf.Lerp(anchor.x,origin.x,Mathf.SmoothStep(0,1,u));Trail(a,b,.3f,new Color(.57f,.49f,.33f));}
            var model=RoadmapModelLibrary.Load();
            var scene=RoadmapBranchArt.Create(branch,_branchScene);_branchScene=scene;
            foreach(var prop in scene.Props)
            {
                var at=origin+prop.Position;at.y=Ground(at.x,at.z);
                Append(model.Get(prop.Asset),at,prop.Height,prop.Yaw,scene,prop,scene.Night?new Color(.72f,.80f,.92f):Color.white);yield return null;
            }
            var previous=origin;
            for(int i=0;i<branch.nodes.Length;i++)
            {
                var reveal=_map.RevealState.BranchNode(branch,i);
#if UNITY_EDITOR
                if(_offline)reveal=i>=QuietCamp.Application.RoadmapBranchPolicy.TeaserCount(branch)?RoadmapReveal.Hidden:i==0&&_offlineBranch>0?RoadmapReveal.Revealed:RoadmapReveal.Silhouette;
#endif
                if(reveal==RoadmapReveal.Hidden)continue;
                var node=branch.nodes[i];var at=origin+new Vector3(node.x,0,node.y);at.y=Ground(at.x,at.z);
                _localFog=reveal==RoadmapReveal.Silhouette?.82f:0;
                Trail(previous,at,.25f,new Color(.57f,.49f,.33f));
                if(reveal==RoadmapReveal.Revealed)
                    foreach(var prop in node.world.props){if(prop.assetId.StartsWith("tent"))continue;Append(model.Get(prop.assetId),at+new Vector3(prop.x,0,prop.z),prop.height,prop.yaw);yield return null;}
                else
                {
                    // Unknown node contains generic silhouettes only, never its authored props/details.
                    Append(model.Get("tree_pineRoundA"),at,2.7f,0);
                    yield return null;
                }
                previous=at;
            }
            _localFog=0;
        }
        void BeforeCamera(ScriptableRenderContext context,Camera camera)
        {if(camera!=_camera||!isActiveAndEnabled)return;_previousSun=RenderSettings.sun;RenderSettings.sun=_sun;_sunOverride=true;}
        void AfterCamera(ScriptableRenderContext context,Camera camera)
        {if(camera==_camera&&_sunOverride){RenderSettings.sun=_previousSun;_sunOverride=false;}}
        void OnDisable()
        {
            if(_sunOverride){RenderSettings.sun=_previousSun;_sunOverride=false;}
            if(_builder!=null){StopCoroutine(_builder);_builder=null;}CancelBakedRequest();ReleaseBaked();
            if(_world!=null)_world.gameObject.SetActive(false);if(_camera!=null)_camera.enabled=false;if(_sun!=null)_sun.enabled=false;if(_image!=null)_image.enabled=false;
            ReleaseUnderlyingCamera();
        }
        void OnEnable(){if(_owner!=null&&_owner!=this)_owner.enabled=false;_owner=this;if(_world!=null)_world.gameObject.SetActive(true);if(_camera!=null){_camera.enabled=true;_sun.enabled=true;_image.enabled=true;ExcludeUnderlyingCamera();}}
        static void DisposeObject(UnityEngine.Object obj){if(obj==null)return;if(UnityEngine.Application.isPlaying)Destroy(obj);else DestroyImmediate(obj);}
        void OnDestroy()
        {
            if(_owner==this)_owner=null;ReleaseUnderlyingCamera();ReleaseBaked();
            RenderPipelineManager.beginCameraRendering-=BeforeCamera;RenderPipelineManager.endCameraRendering-=AfterCamera;
            if(_image!=null)DisposeObject(_image.gameObject);
            if(_world!=null)DisposeObject(_world.gameObject);
            if(_texture!=null){_texture.Release();DisposeObject(_texture);}
            if(_material!=null)DisposeObject(_material);if(_placeholder!=null)DisposeObject(_placeholder);
            foreach(var pool in _pools??Array.Empty<Slot[]>())foreach(var slot in pool)if(slot!=null)DisposeObject(slot.Mesh);
        }
    }
}
