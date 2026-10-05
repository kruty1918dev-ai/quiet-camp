using System;
using System.Collections.Generic;
using QuietCamp.Domain;
using QuietCamp.Infrastructure;
using UnityEngine;
using UnityEngine.Rendering;

namespace QuietCamp.Presentation.World
{
    /// <summary>Rain-fed shallow water, one mesh and one throttled shared probe.
    /// Reflection updates are time-sliced; Low uses the same phase-aware sky.
    /// Water never occupies puzzle cells or participates in physics.</summary>
    public sealed class CampPuddles : MonoBehaviour
    {
        const int WaterLayer=4;
        CampAtmosphere _owner;Camera _camera;LevelData _level;Func<bool> _reduced;
        AtmosphereCatalog _catalog;Material _water,_sky,_previousSky;Mesh _mesh;
        ReflectionProbe _probe;MeshRenderer _renderer;int _render=-1,_tier=-1,_captureTier=-1;
        float _time,_clock,_nextCapture,_capturedCloud=-1;string _phase;int _layout;bool _skyReleased;
        readonly List<Vector3> _centres=new List<Vector3>();
        readonly List<float> _radii=new List<float>();
        readonly Plane[] _planes=new Plane[6];
        Matrix4x4 _lastView,_lastWorld;
        Color _horizon,_sunColor;float _cloudNow;bool _skyReady;
        public float Amount { get; private set; }
        public int CaptureCount { get; private set; }
        public bool HasWorldReflection => _water!=null&&_water.GetFloat("_HasReflection")>.5f;
        public Color SkyColor { get; private set; }
        public int VisiblePuddleCount { get; private set; }
        public IReadOnlyList<Vector3> Centres => _centres;
        public void Configure(CampAtmosphere owner,Camera camera,LevelData level,Func<bool> reduced)
        {
            _owner=owner;_camera=camera;_level=level;_reduced=reduced;_catalog=AtmosphereCatalog.Load();
            var shader=Resources.Load<Shader>("QuietCamp/CampPuddle");if(shader==null||!shader.isSupported)return;
            _water=new Material(shader){name="Rain puddles (owned)"};
            gameObject.layer=WaterLayer;_mesh=new Mesh{name="Rain-fed forest hollows"};Build();
            gameObject.AddComponent<MeshFilter>().sharedMesh=_mesh;_renderer=gameObject.AddComponent<MeshRenderer>();
            _renderer.sharedMaterial=_water;_renderer.shadowCastingMode=ShadowCastingMode.Off;_renderer.receiveShadows=false;
            shader=Resources.Load<Shader>("QuietCamp/CozySky");
            if(shader!=null){_sky=new Material(shader){name="Phase-aware reflected sky"};_previousSky=RenderSettings.skybox;}
            Advance(0);
        }
        void Build()
        {
            var rng=new System.Random(unchecked(_level.decorSeed*1277+731));
            var vertices=new List<Vector3>();var uv=new List<Vector2>();var formation=new List<Vector2>();var indices=new List<int>();
            for(int i=0;i<28;i++)
            {
                // Shallow hollows across the landscape, rather than a ring
                // around the board. They sit just above the actual meadow.
                var p=new Vector3((i%7+.2f+(float)rng.NextDouble()*.6f)/7*(_level.width+18)-_level.width*.5f-9,
                    MeadowSurface.GroundY+.003f,(i/7+.2f+(float)rng.NextDouble()*.6f)/4*(_level.height+18)-_level.height*.5f-9);
                float radius=.38f+(float)rng.NextDouble()*.62f;
                if(!CozyUnderstory.CanPlant(_level,p,radius*1.15f))continue;
                _centres.Add(p);_radii.Add(radius*1.2f);
                var hollow=new Vector2(.015f+(float)rng.NextDouble()*.12f,(float)rng.NextDouble()*6.28f);
                int start=vertices.Count;vertices.Add(p);uv.Add(Vector2.zero);formation.Add(hollow);
                float stretch=.62f+(float)rng.NextDouble()*.48f;
                for(int j=0;j<=18;j++)
                {
                    float a=j*Mathf.PI*2/18;float wobble=1+.10f*Mathf.Sin(a*3+i)+.04f*Mathf.Sin(a*5-i);
                    var radial=new Vector2(Mathf.Cos(a),Mathf.Sin(a));
                    vertices.Add(p+new Vector3(radial.x,0,radial.y*stretch)*radius*wobble);uv.Add(radial);formation.Add(hollow);
                    if(j>0){indices.Add(start);indices.Add(start+j+1);indices.Add(start+j);}
                }
            }
            _mesh.SetVertices(vertices);_mesh.SetUVs(0,uv);_mesh.SetUVs(1,formation);_mesh.SetTriangles(indices,0);_mesh.RecalculateBounds();_mesh.UploadMeshData(true);
        }
        void PrepareProbe()
        {
            if(_probe!=null)return;
            var go=new GameObject("SharedPuddleReflection");go.transform.SetParent(transform,false);go.transform.localPosition=new Vector3(0,.08f,0);
            _probe=go.AddComponent<ReflectionProbe>();_probe.mode=ReflectionProbeMode.Realtime;_probe.refreshMode=ReflectionProbeRefreshMode.ViaScripting;
            _probe.timeSlicingMode=ReflectionProbeTimeSlicingMode.IndividualFaces;_probe.hdr=true;_probe.boxProjection=true;
            _probe.size=new Vector3(_level.width+24,12,_level.height+24);_probe.center=Vector3.up*4;
            _probe.nearClipPlane=.04f;_probe.farClipPlane=24;_probe.clearFlags=ReflectionProbeClearFlags.Skybox;
            // Capture the opaque camp, not UI, water, rain billboards or fog volumes.
            _probe.cullingMask=_camera.cullingMask&((1<<BoardRenderer.BoardSurfaceLayer)|(1<<BoardRenderer.TentSelectableLayer)
                |(1<<BoardRenderer.GameplayObstacleLayer)|(1<<BoardRenderer.DecorLayer));
            _probe.shadowDistance=12;_probe.intensity=1;
        }
        public void Advance(float seconds)
        {
            if(_water==null||_owner==null)return;
            _clock+=Mathf.Max(0,seconds);if(!(_reduced?.Invoke()??false))_time+=Mathf.Max(0,seconds);
            float rain=_owner.Weather?.RainAmount??0;
            // Rain intensity feeds the reservoir; dry weather slowly drains it.
            // Small hollows form first, without a threshold switching all water on.
            Amount=Mathf.Clamp01(Amount+Mathf.Max(0,seconds)*(rain*.115f-(1-rain)*.014f));
            var profile=_catalog.Get(_owner.PhaseId);float cloud=_owner.Weather?.Cloudiness??0;
            UpdateSky(profile,cloud,Mathf.Max(0,seconds));
            if(_sky!=null)
            {
                _sky.SetColor("_Zenith",SkyColor);ApplySkyParameters(_sky);
                if(RenderSettings.skybox!=_sky)RenderSettings.skybox=_sky;
            }
            _water.SetColor("_Sky",SkyColor);ApplySkyParameters(_water);_water.SetFloat("_Fill",Amount);
            UpdateVisibility();
            _water.SetFloat("_RippleTime",_time);_water.SetFloat("_Rain",rain);_renderer.enabled=Amount>.01f&&VisiblePuddleCount>0;
            int tier=_owner.QualityTier;
            if(_tier!=tier){_tier=tier;_nextCapture=0;}
            int layout=0;var session=CampSceneHost.Current?.Session;
            if(session!=null)foreach(var p in session.State.Placements)layout=unchecked(layout*31+p.x*13+p.z*7+p.rotation+p.guestId.GetHashCode());
            bool dirty=_phase!=profile.Id||_layout!=layout||Mathf.Abs(_capturedCloud-cloud)>.2f;
            // Never show a sunny capture in overcast/night water while waiting
            // for the throttled next capture. The matching sky is always ready.
            bool skyChanged=_phase!=profile.Id||Mathf.Abs(_capturedCloud-cloud)>.2f;
            if(skyChanged)_water.SetFloat("_HasReflection",0);
            if(_render>=0&&_probe!=null&&_probe.IsFinishedRendering(_render))
            {
                _render=-1;
                if(_probe.texture!=null&&!skyChanged&&_captureTier==tier)
                {
                    _water.SetTexture("_EnvCube",_probe.texture);_water.SetVector("_CubeDecode",_probe.textureHDRDecodeValues);
                    _water.SetVector("_ProbePosition",_probe.transform.position);_water.SetVector("_ProbeMin",_probe.bounds.min);_water.SetVector("_ProbeMax",_probe.bounds.max);
                    _water.SetFloat("_HasReflection",1);
                }
            }
            if(tier==0){ReleaseProbe();return;}
            if(_renderer.enabled&&Amount>.035f&&_render<0&&_clock>=_nextCapture&&(dirty||!HasWorldReflection||_captureTier!=tier))
            {
                if(!QualitySettings.realtimeReflectionProbes)return;
                PrepareProbe();int resolution=tier==1?32:64;
                if(_probe.resolution!=resolution)_water.SetFloat("_HasReflection",0);
                _probe.resolution=resolution;
                _probe.shadowDistance=tier==1?0:12;
                _render=_probe.RenderProbe();CaptureCount++;_phase=profile.Id;_layout=layout;_capturedCloud=cloud;_captureTier=tier;_nextCapture=_clock+15;
            }
        }
        void UpdateSky(AtmosphereCatalog.Profile profile,float cloud,float seconds)
        {
            bool night=profile.Id=="night";
            var palette=SeasonPalette.For(_level);var ambient=palette.Ambient(profile.Ambient);var sunlight=profile.Sun*palette.SunTint;
            var clear=night?new Color(.055f,.11f,.19f):profile.Id=="evening"?new Color(.30f,.35f,.49f)
                :profile.Id=="morning"?new Color(.43f,.61f,.69f):new Color(.39f,.60f,.77f);
            var sky=Color.Lerp(clear,ambient*(night?.26f:.70f),cloud);
            var horizon=Color.Lerp(Color.Lerp(ambient,sunlight,.18f)*(night?.36f:.88f),ambient*(night?.35f:.83f),cloud);
            var sun=sunlight*profile.SunIntensity;
            float ease=_skyReady?1-Mathf.Exp(-seconds/1.3f):1;
            SkyColor=Color.Lerp(SkyColor,sky,ease);_horizon=Color.Lerp(_horizon,horizon,ease);
            _cloudNow=Mathf.Lerp(_cloudNow,cloud,ease);_sunColor=Color.Lerp(_sunColor,sun,ease);_skyReady=true;
        }
        void ApplySkyParameters(Material material)
        {
            material.SetColor("_Horizon",_horizon);material.SetFloat("_Cloud",_cloudNow);material.SetColor("_SunColor",_sunColor);
            material.SetVector("_SunDirection",RenderSettings.sun!=null?-RenderSettings.sun.transform.forward:Vector3.up);
        }
        void UpdateVisibility()
        {
            var view=_camera.projectionMatrix*_camera.worldToCameraMatrix;var world=transform.localToWorldMatrix;
            if(view==_lastView&&world==_lastWorld)return;_lastView=view;_lastWorld=world;
            GeometryUtility.CalculateFrustumPlanes(_camera,_planes);VisiblePuddleCount=0;
            float scale=Mathf.Max(Mathf.Abs(transform.lossyScale.x),Mathf.Abs(transform.lossyScale.z));
            for(int i=0;i<_centres.Count;i++)
                if(GeometryUtility.TestPlanesAABB(_planes,new Bounds(transform.TransformPoint(_centres[i]),new Vector3(_radii[i]*2*scale,.06f,_radii[i]*2*scale))))VisiblePuddleCount++;
        }
        void ReleaseProbe()
        {
            _water.SetFloat("_HasReflection",0);_water.SetTexture("_EnvCube",null);
            _render=-1;_captureTier=-1;
            if(_probe!=null)Destroy(_probe.gameObject);_probe=null;
        }
        public void ReleaseSky(bool restore)
        {
            if(_skyReleased)return;_skyReleased=true;
            if(restore&&RenderSettings.skybox==_sky)RenderSettings.skybox=_previousSky;
        }
        void OnDestroy()
        {
            if(!_skyReleased)ReleaseSky(true);
            if(_probe!=null)Destroy(_probe.gameObject);
            if(_water!=null)Destroy(_water);if(_sky!=null)Destroy(_sky);if(_mesh!=null)Destroy(_mesh);
        }
    }
}
