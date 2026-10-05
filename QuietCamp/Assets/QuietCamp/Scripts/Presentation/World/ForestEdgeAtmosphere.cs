using System;
using System.Collections.Generic;
using QuietCamp.Domain;
using QuietCamp.Infrastructure;
using UnityEngine;
using UnityEngine.Rendering;

namespace QuietCamp.Presentation.World
{
    /// <summary>World-space forest depth outside the readable glade. One draw
    /// per tier, no camera/physics/extra light, driven by the atmosphere owner.</summary>
    public sealed class ForestEdgeAtmosphere : MonoBehaviour
    {
        CampAtmosphere _owner;Camera _camera;LevelData _level;
        Material _volumeMaterial,_layerMaterial;Mesh _box,_layers;
        GameObject _volume,_low;Vector2 _extent;Vector2Int _screen;
        AtmosphereCatalog _catalog;AtmosphereCatalog.Profile _phase;
        Func<bool> _reduced;float _time;
        float _ortho,_yaw,_density;Color _tint;bool _ready;float _height=4.5f;
        public Color Tint => _tint;
        public float Density => _density;
        public bool UsesVolume => _volume!=null&&_volume.activeSelf;
        public const float ClearMargin=1.6f;
        public Vector2 ClearHalfSize => new Vector2(_level.width*.5f+ClearMargin,_level.height*.5f+ClearMargin);
        public void Configure(CampAtmosphere owner,Camera camera,LevelData level,Func<bool> reduced)
        {
            _owner=owner;_camera=camera;_level=level;_reduced=reduced;
            _catalog=AtmosphereCatalog.Load();
            var shader=Resources.Load<Shader>("QuietCamp/ForestVolume");
            if(shader!=null&&shader.isSupported)
            {
                _box=new Mesh{name="Forest edge bounds"};
                _box.vertices=new[]{new Vector3(-.5f,-.5f,-.5f),new Vector3(.5f,-.5f,-.5f),new Vector3(.5f,.5f,-.5f),new Vector3(-.5f,.5f,-.5f),new Vector3(-.5f,-.5f,.5f),new Vector3(.5f,-.5f,.5f),new Vector3(.5f,.5f,.5f),new Vector3(-.5f,.5f,.5f)};
                _box.triangles=new[]{0,2,1,0,3,2,4,5,6,4,6,7,0,4,7,0,7,3,1,2,6,1,6,5,0,1,5,0,5,4,3,7,6,3,6,2};
                _box.RecalculateBounds();_box.UploadMeshData(true);
                _volumeMaterial=new Material(shader){name="Forest edge air"};
                _volumeMaterial.SetFloat("_EdgeFog",1);_volumeMaterial.SetVector("_GladeHalfSize",new Vector4(level.width*.5f,level.height*.5f,ClearMargin,6));
                _volumeMaterial.SetFloat("_Seed",level.decorSeed%19);
                _volume=Surface("ForestEdgeVolume",_box,_volumeMaterial);
            }
            shader=Resources.Load<Shader>("QuietCamp/ForestRim");
            if(shader!=null&&shader.isSupported)
            {
                _layers=new Mesh{name="Layered forest rim"};
                _layerMaterial=new Material(shader){name="Low forest edge air"};
                _low=Surface("ForestEdgeLayers",_layers,_layerMaterial);
            }
            Fit();Advance(0);
        }
        GameObject Surface(string name,Mesh mesh,Material material)
        {
            var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(transform,false);
            go.GetComponent<MeshFilter>().sharedMesh=mesh;var renderer=go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            return go;
        }
        void Fit()
        {
            var half=ClearHalfSize+Vector2.one*9;
            var plane=new Plane(Vector3.up,Vector3.zero);
            // Fit actual ground visible on phone/tablet, including the upper
            // fog layer's parallax. Never move or reframe the game camera.
            for(int x=0;x<2;x++)for(int y=0;y<2;y++)
            {
                var ray=_camera.ViewportPointToRay(new Vector3(x,y,0));
                if(plane.Raycast(ray,out float distance))
                {
                    var p=ray.GetPoint(distance);half=Vector2.Max(half,new Vector2(Mathf.Abs(p.x),Mathf.Abs(p.z))+Vector2.one*5);
                }
            }
            _extent=Vector2.Min(half,Vector2.one*64);
            if(_volume!=null){_volume.transform.position=Vector3.up*_height*.5f;_volume.transform.localScale=new Vector3(_extent.x*2,_height,_extent.y*2);}
            if(_layers!=null)BuildLayers();
            _screen=new Vector2Int(Screen.width,Screen.height);_ortho=_camera.orthographicSize;_yaw=_camera.transform.eulerAngles.y;
        }
        void BuildLayers()
        {
            var vertices=new List<Vector3>(80);var colors=new List<Color>(80);var triangles=new List<int>(240);
            var inside=ClearHalfSize;
            for(int layer=0;layer<2;layer++)for(int band=0;band<3;band++)
            {
                float near=band/3f,far=(band+1)/3f;
                Vector3 Point(int corner,float fraction)
                {var half=Vector2.Lerp(inside,_extent,fraction);return new Vector3((corner==0||corner==3?-1:1)*half.x,.14f+layer*.28f,(corner<2?-1:1)*half.y);}
                for(int side=0;side<4;side++)
                {
                    int start=vertices.Count,next=(side+1)%4;
                    vertices.Add(Point(side,near));vertices.Add(Point(next,near));vertices.Add(Point(next,far));vertices.Add(Point(side,far));
                    float A(float t)=>Mathf.SmoothStep(0,1,t)*(.60f-layer*.12f);
                    colors.Add(new Color(1,1,1,A(near)));colors.Add(new Color(1,1,1,A(near)));colors.Add(new Color(1,1,1,A(far)));colors.Add(new Color(1,1,1,A(far)));
                    triangles.AddRange(new[]{start,start+1,start+2,start,start+2,start+3});
                }
            }
            _layers.Clear();_layers.SetVertices(vertices);_layers.SetColors(colors);_layers.SetTriangles(triangles,0);_layers.RecalculateBounds();
        }
        public void Advance(float seconds)
        {
            if(_owner==null||_camera==null)return;
            if(_screen!=new Vector2Int(Screen.width,Screen.height)||Mathf.Abs(_ortho-_camera.orthographicSize)>.05f||Mathf.Abs(Mathf.DeltaAngle(_yaw,_camera.transform.eulerAngles.y))>2)Fit();
            if(_phase==null||_phase.Id!=_owner.PhaseId)_phase=_catalog.Get(_owner.PhaseId);
            var phase=_phase;
            if(!(_reduced?.Invoke()??false))_time+=Mathf.Max(0,seconds);
            float cloud=_owner.Weather?.Cloudiness??0,rain=_owner.Weather?.RainAmount??0;
            var composition=EnvironmentCompositionData.For(_level);
            float humidity=Mathf.Clamp01(composition.moisture+rain*.22f);
            _height=composition.weatherId=="mist"&&humidity<.85f?2.1f:Mathf.Lerp(1.4f,5.5f,humidity);
            bool mist=phase.Mist||composition.weatherId=="mist"||humidity>.65f||rain>.1f;
            var palette=SeasonPalette.For(_level);var season=SeasonProfile.For(_level);
            // Low fog must rise above winter drifts instead of being buried below the surface.
            if(season.Winter)_height=Mathf.Max(_height,3.7f);
            var shaded=Color.Lerp(season.Winter?new Color(.27f,.31f,.40f):season.Autumn?new Color(.29f,.25f,.20f):new Color(.20f,.32f,.28f),palette.Ambient(phase.Ambient),.30f);
            var fog=palette.Fog;
            if(phase.Id=="night")fog=Color.Lerp(palette.Ambient(phase.Ambient),new Color(.24f,.32f,.34f),.35f);
            var hazy=Color.Lerp(palette.Ambient(phase.Ambient),fog,.54f);
            var target=Color.Lerp(shaded,hazy,Mathf.Clamp01((phase.Mist?1:0)+cloud*.8f+rain*.4f));
            float density=Mathf.Lerp(.065f,.48f,Mathf.SmoothStep(0,1,(humidity-.35f)/.6f));
            if(composition.weatherId=="mist")density=Mathf.Max(.30f,density);
            if(phase.Id=="night")density=Mathf.Max(.095f,density);
            float ease=_ready?1-Mathf.Exp(-Mathf.Max(0,seconds)/1.6f):1;
            _tint=Color.Lerp(_tint,target,ease);_density=Mathf.Lerp(_density,density,ease);_ready=true;
            bool volume=_volume!=null;
            if(_volume!=null)
            {
                _volume.SetActive(volume);_volumeMaterial.SetFloat("_Steps",_owner.QualityTier==0?3:_owner.QualityTier==1?4:6);
                _volume.transform.position=Vector3.up*_height*.5f;_volume.transform.localScale=new Vector3(_extent.x*2,_height,_extent.y*2);
                _volumeMaterial.SetFloat("_FogHeight",_height);
                _volumeMaterial.SetFloat("_LightTime",_time);
                _volumeMaterial.SetFloat("_Density",_density);_volumeMaterial.SetColor("_FogColor",_tint);
            }
            if(_low!=null)
            {
                _low.SetActive(!volume);var tint=_tint;tint.a=Mathf.Clamp(_density*2.2f,.08f,.22f);_layerMaterial.SetColor("_FogColor",tint);
            }
        }
        void OnDestroy()
        {
            if(_volumeMaterial!=null)Destroy(_volumeMaterial);if(_layerMaterial!=null)Destroy(_layerMaterial);
            if(_box!=null)Destroy(_box);if(_layers!=null)Destroy(_layers);
        }
    }
}
