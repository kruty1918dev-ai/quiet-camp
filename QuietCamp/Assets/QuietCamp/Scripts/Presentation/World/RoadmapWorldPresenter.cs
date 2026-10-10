using System;
using System.Collections.Generic;
using QuietCamp.Application;
using QuietCamp.Presentation.UI;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Kruty1918.Audio;

namespace QuietCamp.Presentation.World
{
    /// <summary>A real continuous world. All geometry is baked; input changes only the camera and three chunk leases.</summary>
    public sealed class RoadmapWorldPresenter : MonoBehaviour
    {
        const int Layer=28;
        sealed class Lease
        {
            public int owners;public ResourceRequest request;public RoadmapWorldChunk asset;
            public readonly List<Action<RoadmapWorldChunk>> listeners=new List<Action<RoadmapWorldChunk>>();
        }
        static readonly Dictionary<string,Lease> Leases=new Dictionary<string,Lease>();
        readonly Dictionary<int,GameObject> _chunks=new Dictionary<int,GameObject>();
        readonly Dictionary<int,string> _leased=new Dictionary<int,string>();
        readonly List<Camera> _disabledCameras=new List<Camera>();
        readonly Dictionary<string,float> _oldFloats=new Dictionary<string,float>();
        readonly Dictionary<string,Vector4> _oldVectors=new Dictionary<string,Vector4>();
        readonly MeshRenderer[] _markers=new MeshRenderer[5];
        readonly MaterialPropertyBlock _markerBlock=new MaterialPropertyBlock();
        GameServices _services;MenuScreens _screens;RoadmapWorldAsset _asset;
        GameObject _root,_cameraRoot,_inputRoot;Camera _camera;Light _sun;
        Light _oldSun;Material _oldSky;bool _oldFog;Color _oldAmbient,_oldSkyColor,_oldEquator,_oldGround;
        AmbientMode _oldAmbientMode;SphericalHarmonicsL2 _oldProbe;
        float _route,_target,_zoom=1,_animationTime,_animationStart,_animationEnd,_clock;
        int _frontier,_generation;bool _entered,_animate,_preview;
        VolumeProfile _profile;
        AudioHandle _forestAudio,_waterAudio,_birdAudio;
        float _birdTime=8;
        int _quality=-1;
        public bool Ready=>_entered&&_asset!=null&&_chunks.Count==_leased.Count&&_chunks.Count>0;
        public string Fault {get;private set;}
        public Camera WorldCamera=>_camera;
        public Transform WorldRoot=>_root?.transform;
        public int LoadedChunks=>_chunks.Count;
        public float RouteCoordinate=>_route;
        public int Frontier=>_frontier;
        public bool IsOpen=>_entered;
        public bool ReducedMotion=>_services?.ReducedMotion??true;
        public void Configure(GameServices services,MenuScreens screens)
        {
            _services=services;_screens=screens;screens.ScreenChanged+=OnScreen;
            if(screens.Current=="Levels")Enter();
        }
        void OnScreen(string screen){if(screen=="Levels")Enter();else Leave();}
        void Enter()
        {
            if(_entered)return;
            _asset=Resources.Load<RoadmapWorldAsset>("QuietCamp/CinematicRoadmap/World");
            if(_asset==null){Fault="Native cinematic world has not been baked";Debug.LogError("[Roadmap] "+Fault);return;}
            _frontier=RoadmapPilotPolicy.Frontier(_services.Progression.IsCompleted);
            _route=_frontier;
            if(_services.LevelMapAnchor!=null&&_services.LevelMapAnchor.revision==_asset.revision)
            {
                int previous=RoadmapPilotPolicy.Index(_services.LevelMapAnchor.nodeId);
                if(previous>=0)_route=Mathf.Min(_frontier,previous+Mathf.Clamp(_services.LevelMapAnchor.offset,-.45f,.45f));
            }
            _target=_route;Build();
            if(_services.RoadmapAdvanceFrom>=0&&!ReducedMotion)
            {
                _route=Mathf.Clamp(_services.RoadmapAdvanceFrom,0,_frontier);_target=_frontier;
                _animationStart=_route;_animationEnd=_frontier;_animationTime=0;_animate=true;
            }
            _services.RoadmapAdvanceFrom=-1;
            var canvas=_screens.Overlay.transform.parent as RectTransform;
            var rect=QcUi.Stretch(canvas,"CinematicWorldInput");_inputRoot=rect.gameObject;
            rect.SetSiblingIndex(_screens.Overlay.transform.GetSiblingIndex());
            _inputRoot.AddComponent<UnityEngine.UI.Image>().color=Color.clear;
            _inputRoot.AddComponent<RoadmapWorldInput>().Configure(this);
            SetCamera(_route);UpdateWindow(false);
        }
        /// <summary>Same production scene for native Editor captures; no game save or bootstrap involved.</summary>
        public void ConfigureCapture(RoadmapWorldAsset asset,float route,int frontier,bool low)
        {
            _preview=true;_asset=asset;_frontier=Mathf.Clamp(frontier,0,4);_route=_target=route;Build();
            SetCamera(route);UpdateWindow(true,low);
        }
        void Build()
        {
            _entered=true;_generation++;Fault=null;
            _oldSun=RenderSettings.sun;_oldSky=RenderSettings.skybox;_oldFog=RenderSettings.fog;
            _oldAmbient=RenderSettings.ambientLight;_oldSkyColor=RenderSettings.ambientSkyColor;_oldEquator=RenderSettings.ambientEquatorColor;
            _oldGround=RenderSettings.ambientGroundColor;_oldAmbientMode=RenderSettings.ambientMode;_oldProbe=RenderSettings.ambientProbe;
            foreach(var name in new[]{"_AtmosWindStrength","_AtmosWindTime","_AtmosWaveLen","_AtmosWaveSpeed","_AtmosFlutterScale","_RoadmapRevealZ"})_oldFloats[name]=Shader.GetGlobalFloat(name);
            foreach(var name in new[]{"_AtmosWindXZ","_AtmosSunDirW","_AtmosSunColor","_AtmosAmbient","_RoadmapFogColor"})_oldVectors[name]=Shader.GetGlobalVector(name);
            foreach(var cam in FindObjectsByType<Camera>(FindObjectsSortMode.None))
                if(cam.enabled&&cam.gameObject.scene==gameObject.scene){_disabledCameras.Add(cam);cam.enabled=false;}
            _root=new GameObject("Cinematic Ukrainian valley");_root.transform.SetParent(transform,false);_root.transform.localScale=Vector3.one*_asset.worldScale;
            _cameraRoot=new GameObject("Roadmap perspective camera");_cameraRoot.transform.SetParent(transform,false);_camera=_cameraRoot.AddComponent<Camera>();
            _camera.cullingMask=1<<Layer;_camera.orthographic=false;_camera.fieldOfView=_asset.fieldOfView;
            _camera.nearClipPlane=.05f;_camera.farClipPlane=130;_camera.clearFlags=CameraClearFlags.SolidColor;_camera.backgroundColor=_asset.sky;
            _camera.allowHDR=true;_camera.allowMSAA=true;
            var data=_camera.GetUniversalAdditionalCameraData();data.renderPostProcessing=true;data.requiresDepthTexture=true;data.volumeLayerMask=1<<Layer;
            var sunObject=new GameObject("Valley afternoon sun");sunObject.transform.SetParent(_root.transform,false);_sun=sunObject.AddComponent<Light>();
            _sun.type=LightType.Directional;_sun.transform.rotation=Quaternion.Euler(38,-48,0);_sun.color=new Color(1,.93f,.79f);_sun.intensity=1.12f;
            _sun.shadows=LightShadows.Soft;_sun.shadowBias=.025f;_sun.shadowNormalBias=.25f;_sun.cullingMask=1<<Layer;RenderSettings.sun=_sun;
            RenderSettings.ambientMode=AmbientMode.Custom;
            var probe=new SphericalHarmonicsL2();probe.AddAmbientLight(new Color(.67f,.73f,.72f).linear*.8f);RenderSettings.ambientProbe=probe;
            RenderSettings.ambientLight=new Color(.67f,.73f,.72f);RenderSettings.fog=false;
            var volume=new GameObject("Valley gentle color grade");volume.transform.SetParent(_root.transform,false);volume.layer=Layer;
            var v=volume.AddComponent<Volume>();v.isGlobal=true;v.priority=30;_profile=ScriptableObject.CreateInstance<VolumeProfile>();v.sharedProfile=_profile;
            var grade=_profile.Add<ColorAdjustments>();grade.postExposure.Override(.15f);grade.saturation.Override(-3);
            var tone=_profile.Add<Tonemapping>();tone.mode.Override(TonemappingMode.Neutral);
            var focus=_profile.Add<DepthOfField>();focus.mode.Override(DepthOfFieldMode.Off);
            var blur=_profile.Add<MotionBlur>();blur.intensity.Override(0);
            AddRenderer(_root.transform,"Far landscape",_asset.horizon,_asset.ground,false);
            for(int i=0;i<5;i++)
            {
                var m=AddRenderer(_root.transform,"Waystone "+_asset.waypoints[i].levelId,_asset.markerMesh,_asset.marker,true);
                m.transform.localPosition=_asset.waypoints[i].position;_markers[i]=m;
            }
            ApplyAtmosphere();
            if(!_preview&&_services.Audio!=null)
            {
                _forestAudio=_services.Audio.Play("ambience.biome.forest",new AudioPlayOptions(volumeScale:.4f));
                _waterAudio=_services.Audio.Play("ambience.biome.water",new AudioPlayOptions(volumeScale:.35f,initialPlaybackScale:0));
            }
        }
        static MeshRenderer AddRenderer(Transform parent,string name,Mesh mesh,Material material,bool shadows)
        {
            var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.layer=Layer;go.transform.SetParent(parent,false);
            go.GetComponent<MeshFilter>().sharedMesh=mesh;var r=go.GetComponent<MeshRenderer>();r.sharedMaterial=material;
            r.shadowCastingMode=shadows?ShadowCastingMode.On:ShadowCastingMode.Off;r.receiveShadows=true;return r;
        }
        void UpdateWindow(bool synchronous,bool low=false)
        {
            int centre=Mathf.Clamp(Mathf.RoundToInt(_route),0,4);var wanted=new HashSet<int>();
            for(int i=Mathf.Max(0,centre-1);i<=Mathf.Min(4,centre+1);i++)wanted.Add(i);
            foreach(var pair in new List<KeyValuePair<int,string>>(_leased))if(!wanted.Contains(pair.Key))
            {if(_chunks.TryGetValue(pair.Key,out var go)){DestroyOwned(go);_chunks.Remove(pair.Key);}Release(pair.Value);_leased.Remove(pair.Key);}
            foreach(int index in wanted)if(!_leased.ContainsKey(index))
            {
                int id=index,generation=_generation;string path=_asset.chunks[id];_leased.Add(id,path);
                void Loaded(RoadmapWorldChunk chunk)
                {
                    if(!_entered||generation!=_generation||!_leased.ContainsKey(id))return;
                    if(chunk==null||chunk.sourceHash!=_asset.sourceHash){Fault="Missing/stale native chunk "+id;Debug.LogError("[Roadmap] "+Fault);return;}
                    var go=new GameObject("Valley chunk "+id);go.transform.SetParent(_root.transform,false);
                    var meshes=(low||(_services?.EffectiveQuality??1)==0)?chunk.low:chunk.balanced;
                    for(int part=0;part<meshes.Length;part++)if(meshes[part]!=null)
                    {
                        var material=part==0?_asset.ground:part==1?_asset.structure:part==4?_asset.water:_asset.foliage;
                        AddRenderer(go.transform,"Baked part "+part,meshes[part],material,part!=0&&part!=4);
                    }
                    _chunks[id]=go;
                }
                Acquire(path,Loaded,synchronous);
            }
        }
        static void Acquire(string path,Action<RoadmapWorldChunk> loaded,bool synchronous)
        {
            if(Leases.TryGetValue(path,out var existing))
            {existing.owners++;if(existing.request==null||existing.request.isDone)loaded(existing.asset);else existing.listeners.Add(loaded);return;}
            var lease=new Lease{owners=1};Leases.Add(path,lease);
            if(synchronous){lease.asset=Resources.Load<RoadmapWorldChunk>(path);loaded(lease.asset);return;}
            lease.listeners.Add(loaded);lease.request=Resources.LoadAsync<RoadmapWorldChunk>(path);
            lease.request.completed+=op=>
            {
                lease.asset=lease.request.asset as RoadmapWorldChunk;
                if(lease.owners>0){foreach(var listener in lease.listeners.ToArray())listener(lease.asset);}else{if(lease.asset!=null)Resources.UnloadAsset(lease.asset);Leases.Remove(path);}
                lease.listeners.Clear();
            };
        }
        static void Release(string path)
        {
            if(!Leases.TryGetValue(path,out var lease)||--lease.owners>0)return;
            if(lease.request!=null&&!lease.request.isDone)return;
            if(lease.asset!=null)Resources.UnloadAsset(lease.asset);Leases.Remove(path);
        }
        public void Drag(float screenDelta)
        {if(!_entered)return;_animate=false;_target=Mathf.Clamp(_target+screenDelta/Mathf.Max(200,Screen.height)*1.6f,0,_frontier);}
        public void Zoom(float factor){_animate=false;_zoom=Mathf.Clamp(_zoom*factor,.85f,1.15f);}
        public void Step(int direction){_animate=false;_target=Mathf.Clamp(Mathf.Round(_target)+direction,0,_frontier);}
        public void ActivateFocused()
        {
            if(_camera==null)return;
            int i=Mathf.Clamp(Mathf.RoundToInt(_route),0,_frontier);
            Tap(_camera.WorldToScreenPoint(_root.transform.TransformPoint(_asset.waypoints[i].position)));
        }
        public void Tap(Vector2 screen)
        {
            if(!Ready||_preview||_services==null)return;
            float dp=Screen.dpi>0?Screen.dpi/160f:2;float radius=Mathf.Max(24*dp,Screen.width/720f*42);
            int best=-1;float distance=radius;
            for(int i=0;i<=_frontier;i++)
            {
                var p=_camera.WorldToScreenPoint(_root.transform.TransformPoint(_asset.waypoints[i].position));
                if(p.z<=0)continue;float d=Vector2.Distance(screen,p);if(d<distance){distance=d;best=i;}
            }
            if(best<0)return;string id=_asset.waypoints[best].levelId;
            if(!RoadmapPilotPolicy.CanPlay(id,_services.Progression.IsCompleted,_services.CanStart))return;
            Remember();_services.PendingMenuScreen="Levels";
            _services.Actions.Execute(new Kruty1918.UIActions.API.UiActionRequest(new Kruty1918.UIActions.API.UiActionId("qc.play"),Kruty1918.UIActions.API.UiActionSource.Button,"Menu",null,id));
        }
        public void SetCamera(float route)
        {
            if(_camera==null)return;_route=Mathf.Clamp(route,0,4);int a=Mathf.FloorToInt(_route),b=Mathf.Min(4,a+1);
            float t=Mathf.SmoothStep(0,1,_route-a);var from=_asset.waypoints[a];var to=_asset.waypoints[b];
            var focus=Vector3.Lerp(from.focus,to.focus,t);var rotation=Quaternion.Euler(Mathf.Lerp(from.pitch,to.pitch,t),Mathf.LerpAngle(from.yaw,to.yaw,t),0);
            var position=focus+rotation*Vector3.back*Mathf.Lerp(from.distance,to.distance,t)/_zoom;
            _camera.transform.position=_root.transform.TransformPoint(position);_camera.transform.rotation=rotation;
            for(int i=0;i<5;i++)
            {
                var m=_markers[i];if(m==null)continue;m.enabled=i<=_frontier;
                float pulse=i==_frontier&&!ReducedMotion?1+.06f*Mathf.Sin(_clock*1.6f):1;m.transform.localScale=Vector3.one*pulse;
                _markerBlock.SetColor("_BaseColor",i==_frontier?new Color(.95f,.83f,.49f):new Color(.69f,.71f,.57f));m.SetPropertyBlock(_markerBlock);
            }
        }
        void ApplyAtmosphere()
        {
            Shader.SetGlobalVector("_AtmosWindXZ",new Vector4(.88f,.47f,0,0));Shader.SetGlobalFloat("_AtmosWindStrength",ReducedMotion?0:.18f);
            Shader.SetGlobalFloat("_AtmosWindTime",_clock);Shader.SetGlobalFloat("_AtmosWaveLen",7);Shader.SetGlobalFloat("_AtmosWaveSpeed",.65f);Shader.SetGlobalFloat("_AtmosFlutterScale",1);
            Shader.SetGlobalVector("_AtmosSunDirW",-_sun.transform.forward);Shader.SetGlobalVector("_AtmosSunColor",_sun.color);
            Shader.SetGlobalVector("_AtmosAmbient",new Color(.67f,.73f,.72f));
            Shader.SetGlobalVector("_RoadmapFogColor",_asset.sky.linear);
            Shader.SetGlobalFloat("_RoadmapRevealZ",(_frontier==4?300:_frontier*40+26)*_asset.worldScale);
        }
        void LateUpdate()
        {
            if(!_entered||_preview)return;_clock+=Time.unscaledDeltaTime;
            if(_quality!=_services.EffectiveQuality)
            {
                _quality=_services.EffectiveQuality;
                foreach(var go in _chunks.Values)DestroyOwned(go);_chunks.Clear();
                foreach(var path in _leased.Values)Release(path);_leased.Clear();_generation++;
            }
            if(_animate){_animationTime+=Time.unscaledDeltaTime;_route=Mathf.Lerp(_animationStart,_animationEnd,Mathf.SmoothStep(0,1,_animationTime/3));if(_animationTime>=3||ReducedMotion){_animate=false;_route=_target;}}
            else _route=ReducedMotion?_target:Mathf.Lerp(_route,_target,1-Mathf.Exp(-Time.unscaledDeltaTime*12));
            SetCamera(_route);UpdateWindow(false);ApplyAtmosphere();
            if(_services.Audio!=null)
            {
                _services.Audio.SetPlaybackScale(_services.AmbientWindHandle,.32f);
                _services.Audio.SetPlaybackScale(_waterAudio,Mathf.SmoothStep(0,1,Mathf.InverseLerp(2.1f,3.4f,_route)));
                _birdTime-=Time.unscaledDeltaTime;
                if(_birdTime<=0){_birdTime=18;_birdAudio.Stop();_birdAudio=_services.Audio.Play("ambience.bird",new AudioPlayOptions(volumeScale:.2f));}
            }
        }
        void Remember()
        {
            if(_services==null||_asset==null)return;int i=Mathf.Clamp(Mathf.RoundToInt(_route),0,4);
            _services.LevelMapAnchor=new QuietCamp.Domain.RoadmapAnchor{journeyId="main",nodeId=_asset.waypoints[i].levelId,revision=_asset.revision,offset=_route-i};
        }
        public void Leave()
        {
            if(!_entered)return;Remember();_entered=false;_generation++;_animate=false;
            if(_camera!=null)_camera.enabled=false;
            foreach(var path in _leased.Values)Release(path);_leased.Clear();_chunks.Clear();
            _forestAudio.Stop();_waterAudio.Stop();_birdAudio.Stop();_forestAudio=_waterAudio=_birdAudio=default;
            _services?.Audio?.SetPlaybackScale(_services.AmbientWindHandle,1);
            DestroyOwned(_inputRoot);DestroyOwned(_cameraRoot);DestroyOwned(_root);
            if(_profile!=null)foreach(var component in _profile.components)DestroyOwned(component);
            DestroyOwned(_profile);
            foreach(var cam in _disabledCameras)if(cam!=null)cam.enabled=true;_disabledCameras.Clear();
            RenderSettings.sun=_oldSun;RenderSettings.skybox=_oldSky;RenderSettings.fog=_oldFog;RenderSettings.ambientMode=_oldAmbientMode;
            RenderSettings.ambientLight=_oldAmbient;RenderSettings.ambientSkyColor=_oldSkyColor;RenderSettings.ambientEquatorColor=_oldEquator;RenderSettings.ambientGroundColor=_oldGround;RenderSettings.ambientProbe=_oldProbe;
            foreach(var p in _oldFloats)Shader.SetGlobalFloat(p.Key,p.Value);foreach(var p in _oldVectors)Shader.SetGlobalVector(p.Key,p.Value);_oldFloats.Clear();_oldVectors.Clear();
            _root=_cameraRoot=_inputRoot=null;_camera=null;_asset=null;
        }
        static void DestroyOwned(UnityEngine.Object value){if(value==null)return;if(UnityEngine.Application.isPlaying)Destroy(value);else DestroyImmediate(value);}
        void OnDestroy(){if(_screens!=null)_screens.ScreenChanged-=OnScreen;Leave();}
    }
}
