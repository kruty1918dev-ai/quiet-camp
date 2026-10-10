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
        readonly Dictionary<UniversalRenderPipelineAsset,float> _shadowRanges=new Dictionary<UniversalRenderPipelineAsset,float>();
        readonly Dictionary<string,Vector4> _oldVectors=new Dictionary<string,Vector4>();
        readonly MeshRenderer[] _markers=new MeshRenderer[5];
        readonly MeshRenderer[] _distant=new MeshRenderer[5];
        MaterialPropertyBlock _markerBlock;
        GameServices _services;MenuScreens _screens;RoadmapWorldAsset _asset;
        GameObject _root,_cameraRoot,_inputRoot;Camera _camera;Light _sun;
        Light _oldSun;Material _oldSky;bool _oldFog;Color _oldAmbient,_oldSkyColor,_oldEquator,_oldGround;
        AmbientMode _oldAmbientMode;SphericalHarmonicsL2 _oldProbe;
        float _route,_target,_zoom=1,_animationTime,_animationStart,_animationEnd,_clock;
        int _frontier,_generation;bool _entered,_animate,_preview,_finished;
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
        public float MinRoute=>-.25f;
        public float MaxRoute=>_frontier+.2f;
        public float ZoomFactor=>_zoom;
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
            _frontier=RoadmapPilotPolicy.Frontier(_services.PilotCompleted);_finished=RoadmapPilotPolicy.Finished(_services.PilotCompleted);
            _route=_frontier;
            if(_services.LevelMapAnchor!=null&&_services.LevelMapAnchor.revision==_asset.revision)
            {
                int previous=RoadmapPilotPolicy.Index(_services.LevelMapAnchor.nodeId);
                if(previous>=0)_route=Mathf.Clamp(previous+Mathf.Clamp(_services.LevelMapAnchor.offset,-.45f,.45f),MinRoute,MaxRoute);
            }
            _target=_route;Build();
            if(_services.RoadmapAdvanceFrom>=0)
            {
                if(ReducedMotion)_route=_target=_frontier;
                else
                {
                    _route=Mathf.Clamp(_services.RoadmapAdvanceFrom,0,_frontier);_target=_frontier;
                    _animationStart=_route;_animationEnd=_frontier;_animationTime=0;_animate=true;
                }
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
            _markerBlock=new MaterialPropertyBlock();
            _entered=true;_generation++;Fault=null;
            _oldSun=RenderSettings.sun;_oldSky=RenderSettings.skybox;_oldFog=RenderSettings.fog;
            _oldAmbient=RenderSettings.ambientLight;_oldSkyColor=RenderSettings.ambientSkyColor;_oldEquator=RenderSettings.ambientEquatorColor;
            _oldGround=RenderSettings.ambientGroundColor;_oldAmbientMode=RenderSettings.ambientMode;_oldProbe=RenderSettings.ambientProbe;
            foreach(var name in new[]{"_AtmosWindStrength","_AtmosWindTime","_AtmosWaveLen","_AtmosWaveSpeed","_AtmosFlutterScale","_RoadmapRevealZ","_RoadmapFogStart"})_oldFloats[name]=Shader.GetGlobalFloat(name);
            foreach(var name in new[]{"_AtmosWindXZ","_AtmosSunDirW","_AtmosSunColor","_AtmosAmbient","_RoadmapFogColor"})_oldVectors[name]=Shader.GetGlobalVector(name);
            foreach(var cam in FindObjectsByType<Camera>(FindObjectsSortMode.None))
                if(cam.enabled&&cam.gameObject.scene==gameObject.scene){_disabledCameras.Add(cam);cam.enabled=false;}
            _root=new GameObject("Cinematic Ukrainian valley");_root.transform.SetParent(transform,false);_root.transform.localScale=Vector3.one*_asset.worldScale;
            _cameraRoot=new GameObject("Roadmap perspective camera");_cameraRoot.transform.SetParent(transform,false);_camera=_cameraRoot.AddComponent<Camera>();
            _camera.cullingMask=1<<Layer;_camera.orthographic=false;_camera.fieldOfView=_asset.fieldOfView;
            _camera.nearClipPlane=.05f;_camera.farClipPlane=130;_camera.clearFlags=CameraClearFlags.SolidColor;_camera.backgroundColor=_asset.sky;
            _camera.allowHDR=true;_camera.allowMSAA=true;
            var data=_camera.GetUniversalAdditionalCameraData();data.renderShadows=true;data.renderPostProcessing=true;data.requiresDepthTexture=true;data.volumeLayerMask=1<<Layer;
            var sunObject=new GameObject("Valley afternoon sun");sunObject.layer=Layer;sunObject.transform.SetParent(_root.transform,false);_sun=sunObject.AddComponent<Light>();
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
            var river=_asset.riverByFrontier!=null&&_asset.riverByFrontier.Length==5?_asset.riverByFrontier[_frontier]:_asset.river;
            if(river!=null)AddRenderer(_root.transform,"Continuous river",river,_asset.water,false);
            if(_asset.distantForest!=null)for(int i=0;i<_asset.distantForest.Length;i++)
                _distant[i]=AddRenderer(_root.transform,"Distant forest "+i,_asset.distantForest[i],_asset.foliage,false);
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
            int centre=Mathf.Clamp(Mathf.FloorToInt((_route*40-6)/40)+1,0,4);var wanted=new HashSet<int>();
            for(int i=Mathf.Max(0,centre-1);i<=Mathf.Min(4,centre+1);i++)wanted.Add(i);
            foreach(var pair in new List<KeyValuePair<int,string>>(_leased))if(!wanted.Contains(pair.Key))
            {if(_chunks.TryGetValue(pair.Key,out var go)){DestroyOwned(go);_chunks.Remove(pair.Key);}Release(pair.Value);_leased.Remove(pair.Key);}
            foreach(int index in wanted)if(!_leased.ContainsKey(index))
            {
                int id=index,generation=_generation;string path=_asset.chunks[id];_leased.Add(id,path);
                void Loaded(RoadmapWorldChunk chunk)
                {
                    if(!_entered||generation!=_generation||!_leased.ContainsKey(id))return;
                    if(chunk==null||chunk.sourceHash!=_asset.sourceHash){Fault="Missing/stale native chunk "+id+" at "+path+"; expected "+_asset.sourceHash+", actual "+(chunk==null?"missing":chunk.sourceHash);Debug.LogError("[Roadmap] "+Fault);return;}
                    var go=new GameObject("Valley chunk "+id);go.transform.SetParent(_root.transform,false);
                    var meshes=(low||(_services?.EffectiveQuality??1)==0)?chunk.low:chunk.balanced;
                    for(int part=0;part<meshes.Length;part++)if(meshes[part]!=null)
                    {
                        var material=part==0?_asset.ground:part==1?_asset.structure:part==4?_asset.water:_asset.foliage;
                        AddRenderer(go.transform,"Baked part "+part,meshes[part],material,part!=0&&part!=4);
                    }
                    _chunks[id]=go;
                    if(_distant[id]!=null)_distant[id].enabled=false;
                }
                Acquire(path,Loaded,synchronous);
            }
            for(int i=0;i<5;i++)if(_distant[i]!=null)_distant[i].enabled=!_chunks.ContainsKey(i);
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
        public void BeginInteraction(){if(!_entered)return;_animate=false;_target=_route;}
        public void Drag(float screenDelta)=>Drag(new Vector2(0,screenDelta));
        public void Drag(Vector2 screenDelta)=>Drag(screenDelta,_camera!=null?_camera.pixelRect.center+screenDelta:screenDelta);
        public void Drag(Vector2 screenDelta,Vector2 screenPosition)
        {
            if(!_entered||!Finite(screenDelta.x)||!Finite(screenDelta.y)||!Finite(screenPosition.x)||!Finite(screenPosition.y))return;
            BeginInteraction();
            _route=_target=Mathf.Clamp(_route+RouteDelta(screenDelta,screenPosition-screenDelta),MinRoute,MaxRoute);
        }
        public void Scroll(float notches)
        {
            if(!_entered||!Finite(notches))return;
            if(_animate)BeginInteraction();
            // Match ScrollRect: positive wheel scroll moves content downward.
            // One notch moves about 12% of the visible viewport at any zoom/aspect.
            _target=Mathf.Clamp(_target+RouteDelta(new Vector2(0,-Mathf.Clamp(notches,-3,3)*_camera.pixelHeight*.12f),_camera.pixelRect.center),MinRoute,MaxRoute);
        }
        float RouteDelta(Vector2 delta,Vector2 contact)
        {
            // Measure the actual camera path on screen, including perspective, zoom,
            // authored yaw and camera boom, instead of using a fixed speed.
            CameraPose(_route,out _,out _,out var focus);
            float probe=_route<4.19f?_route+.01f:_route-.01f;
            CameraPose(probe,out var nextPosition,out var nextRotation,out _);
            Vector3 anchor=_root.transform.TransformPoint(focus);
            var ray=_camera.ScreenPointToRay(contact);
            // Grab the ground under the contact, so foreground and distant areas
            // track the finger equally. The authored focus plane approximates terrain.
            if(new Plane(Vector3.up,anchor).Raycast(ray,out float distance))anchor=ray.GetPoint(distance);
            Vector2 now=_camera.WorldToScreenPoint(anchor);
            Vector3 local=Quaternion.Inverse(nextRotation)*(anchor-nextPosition);
            float pixels=_camera.pixelHeight*.5f/Mathf.Tan(_camera.fieldOfView*Mathf.Deg2Rad*.5f);
            Vector2 next=_camera.pixelRect.center+new Vector2(local.x,local.y)*(pixels/Mathf.Max(.01f,local.z));
            Vector2 motion=(next-now)/(probe-_route);
            return motion.sqrMagnitude>1?Vector2.Dot(delta,motion)/motion.sqrMagnitude:0;
        }
        static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
        public void Zoom(float factor){if(!_entered||!Finite(factor)||factor<=0)return;if(_animate)BeginInteraction();_zoom=Mathf.Clamp(_zoom*factor,.85f,1.15f);}
        public void Step(int direction){_animate=false;_target=Mathf.Clamp(Mathf.Round(_target)+direction,0,_frontier);}
        public void Seek(float route){if(!Finite(route))return;_animate=false;_target=Mathf.Clamp(route,MinRoute,MaxRoute);}
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
            if(!RoadmapPilotPolicy.CanPlay(id,_services.PilotCompleted,_services.CanStart))return;
            Remember();_services.PendingMenuScreen="Levels";
            _services.Actions.Execute(new Kruty1918.UIActions.API.UiActionRequest(new Kruty1918.UIActions.API.UiActionId("qc.play"),Kruty1918.UIActions.API.UiActionSource.Button,"Menu",null,id));
        }
        public void SetCamera(float route)
        {
            if(_camera==null)return;
            // Keep a landscape viewport inside the authored valley even at the widest pinch zoom.
            // Portrait retains the authored vertical FOV; wide screens cap the horizontal field.
            float aspect=Mathf.Max(1,_camera.aspect);
            _camera.fieldOfView=2*Mathf.Atan(Mathf.Tan(_asset.fieldOfView*Mathf.Deg2Rad*.5f)/aspect)*Mathf.Rad2Deg;
            _route=Mathf.Clamp(route,-.25f,4.2f);
            CameraPose(_route,out var position,out var rotation,out var focus);
            float boomDistance=Vector3.Distance(position,_root.transform.TransformPoint(focus))/_asset.worldScale;
            // Raising the camera must not turn the whole valley into fog. Keep haze in the far field.
            Shader.SetGlobalFloat("_RoadmapFogStart",boomDistance*_asset.worldScale*.9f);
            if(GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset pipeline)
            {
                if(!_shadowRanges.ContainsKey(pipeline))_shadowRanges.Add(pipeline,pipeline.shadowDistance);
                // The game's close camp cameras use 32 m shadows. An aerial map needs the
                // same sunlight to reach its ground; retain and restore every touched tier.
                pipeline.shadowDistance=Mathf.Max(_shadowRanges[pipeline],boomDistance*_asset.worldScale+24);
            }
            _camera.transform.position=position;_camera.transform.rotation=rotation;
            for(int i=0;i<5;i++)
            {
                var m=_markers[i];if(m==null)continue;m.enabled=i<=_frontier;
                bool current=i==_frontier&&!_finished;
                float pulse=current&&!ReducedMotion?1+.06f*Mathf.Sin(_clock*1.6f):1;m.transform.localScale=Vector3.one*pulse;
                _markerBlock.SetColor("_BaseColor",current?new Color(.95f,.83f,.49f):new Color(.69f,.71f,.57f));m.SetPropertyBlock(_markerBlock);
            }
        }
        void CameraPose(float route,out Vector3 position,out Quaternion rotation,out Vector3 focus)
        {
            int a=Mathf.Clamp(Mathf.FloorToInt(route),0,3),b=a+1;
            float t=route-a;var from=_asset.waypoints[a];var to=_asset.waypoints[b];
            // Linear coordinates prevent sticky stops and surges between places.
            // Endpoint margins let even a fresh profile inspect its surroundings.
            focus=Vector3.LerpUnclamped(from.focus,to.focus,t);
            rotation=Quaternion.Euler(Mathf.Lerp(from.pitch,to.pitch,t),Mathf.LerpAngle(from.yaw,to.yaw,t),0);
            float distance=Mathf.Lerp(from.distance,to.distance,t)/_zoom;
            position=_root.transform.TransformPoint(focus+rotation*Vector3.back*distance);
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
            foreach(var range in _shadowRanges)if(range.Key!=null)range.Key.shadowDistance=range.Value;_shadowRanges.Clear();
            RenderSettings.sun=_oldSun;RenderSettings.skybox=_oldSky;RenderSettings.fog=_oldFog;RenderSettings.ambientMode=_oldAmbientMode;
            RenderSettings.ambientLight=_oldAmbient;RenderSettings.ambientSkyColor=_oldSkyColor;RenderSettings.ambientEquatorColor=_oldEquator;RenderSettings.ambientGroundColor=_oldGround;RenderSettings.ambientProbe=_oldProbe;
            foreach(var p in _oldFloats)Shader.SetGlobalFloat(p.Key,p.Value);foreach(var p in _oldVectors)Shader.SetGlobalVector(p.Key,p.Value);_oldFloats.Clear();_oldVectors.Clear();
            _root=_cameraRoot=_inputRoot=null;_camera=null;_asset=null;
        }
        static void DestroyOwned(UnityEngine.Object value){if(value==null)return;if(UnityEngine.Application.isPlaying)Destroy(value);else DestroyImmediate(value);}
        void OnDestroy(){if(_screens!=null)_screens.ScreenChanged-=OnScreen;Leave();}
    }
}
