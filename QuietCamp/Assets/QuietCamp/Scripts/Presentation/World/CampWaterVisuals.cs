using System.Collections.Generic;
using QuietCamp.Domain;
using UnityEngine;
using UnityEngine.Rendering;

namespace QuietCamp.Presentation.World
{
    /// <summary>Authored shoreline using Stylized Water 3; a small sky cube replaces reflection cameras.</summary>
    public sealed class CampWaterVisuals : MonoBehaviour
    {
        Material _water, _bank;
        readonly List<Mesh> _meshes = new List<Mesh>();
        Cubemap _sky;
        ReflectionProbe _probe;
        CampAtmosphere _atmosphere;
        readonly Color[] _pixels = new Color[16*16];
        float _nextSky;
        Color _lastSky, _lastHorizon;
        public int ChunkCount { get; private set; }
        public bool HasReflection => _sky != null;
        public static CampWaterVisuals Attach(LevelData level, Transform parent, Camera camera)
        {
            if (level?.environment?.shore == null || parent == null) return null;
            var source = Resources.Load<Material>("QuietCamp/Water/CampLakeMobile");
            if (source == null || source.shader == null || !source.shader.isSupported)
            { Debug.LogWarning("[QuietCamp] Shore water material unavailable; the playable clearing remains ready."); return null; }
            var go = new GameObject("Camp shore water"); go.transform.SetParent(parent,false);
            var water = go.AddComponent<CampWaterVisuals>(); water.Build(level,source);
            return water;
        }
        void Build(LevelData level, Material source)
        {
            _water = new Material(source) { name = "Quiet water (owned)" };
            _water.SetFloat("_SunReflectionSize",.72f);
            var foam=_water.GetColor("_IntersectionColor");foam.a=.22f;_water.SetColor("_IntersectionColor",foam);
            _bank = new Material(Shader.Find("Universal Render Pipeline/Simple Lit")) { name = "Soft worn shore" };
            var palette=SeasonPalette.For(level);
            _bank.SetColor("_BaseColor",SeasonProfile.For(level).Winter?palette.GrassLight*.9f:palette.Soil*1.08f);
            var shore = level.environment.shore;
            const int segments = 8, across = 4;
            float length = 48f;
            for (int chunk=0;chunk<6;chunk++)
            {
                var vertices = new List<Vector3>(); var uvs = new List<Vector2>(); var colors = new List<Color>(); var indices = new List<int>();
                for(int z=0;z<=segments;z++)
                {
                    float distance = -length*.5f + (chunk+z/(float)segments)*(length/6);
                    for(int x=0;x<=across;x++)
                    {
                        float fraction=x/(float)across;
                        var point=ShorelineGeometry.Point(shore,distance,fraction,.07f);
                        vertices.Add(point);
                        uvs.Add(new Vector2(Vector3.Dot(point,ShorelineGeometry.Side(shore)),distance));
                        float shallow=Mathf.Pow(Mathf.Abs(fraction-.5f)*2,2);
                        colors.Add(new Color(Mathf.Pow(shallow,4)*.65f,1-shallow*.87f,0,0));
                        if(z==segments||x==across)continue;
                        int a=z*(across+1)+x;
                        indices.AddRange(new[]{a,a+across+1,a+1,a+1,a+across+1,a+across+2});
                    }
                }
                // Ensure the grid winding faces up for every shoreline orientation.
                if(Vector3.Cross(vertices[indices[1]]-vertices[indices[0]],vertices[indices[2]]-vertices[indices[0]]).y<0)
                    for(int i=0;i<indices.Count;i+=3){int t=indices[i+1];indices[i+1]=indices[i+2];indices[i+2]=t;}
                var mesh = new Mesh { name = "Shore water chunk" }; mesh.SetVertices(vertices);mesh.SetUVs(0,uvs);mesh.SetColors(colors);mesh.SetTriangles(indices,0);mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();
                var bounds=mesh.bounds;bounds.Expand(.3f);mesh.bounds=bounds;_meshes.Add(mesh);
                var go = new GameObject("Shore water " + chunk);go.transform.SetParent(transform,false);go.layer=4;
                go.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=_water;
                renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=true;renderer.reflectionProbeUsage=ReflectionProbeUsage.Simple;
                ChunkCount++;
                BuildBanks(vertices,segments,across,chunk);
            }
            _sky = new Cubemap(16,TextureFormat.RGBA32,true) { name = "Weather sky reflection",filterMode=FilterMode.Trilinear,wrapMode=TextureWrapMode.Clamp };
            _probe = gameObject.AddComponent<ReflectionProbe>();_probe.mode=ReflectionProbeMode.Custom;_probe.customBakedTexture=_sky;
            _probe.size=new Vector3(120,20,120);_probe.center=Vector3.up*3;_probe.boxProjection=false;_probe.intensity=1;_probe.importance=10;
            UpdateSky();
        }
        void BuildBanks(List<Vector3> water,int segments,int across,int chunk)
        {
            var vertices = new List<Vector3>();var indices=new List<int>();
            for(int edge=0;edge<2;edge++)
            for(int z=0;z<=segments;z++)
            {
                var point=water[z*(across+1)+(edge==0?0:across)];
                var other=water[z*(across+1)+(edge==0?1:across-1)];var outward=(point-other).normalized;
                point.y=.075f;
                vertices.Add(point);vertices.Add(point+outward*(.16f+.055f*Mathf.Sin(z*1.7f+chunk)));
                if(z==0)continue;int a=vertices.Count-4;indices.AddRange(new[]{a,a+2,a+1,a+1,a+2,a+3});
            }
            for(int i=0;i<indices.Count;i+=3)
                if(Vector3.Cross(vertices[indices[i+1]]-vertices[indices[i]],vertices[indices[i+2]]-vertices[indices[i]]).y<0)
                {int t=indices[i+1];indices[i+1]=indices[i+2];indices[i+2]=t;}
            var mesh=new Mesh{name="Irregular shallow shore"};mesh.SetVertices(vertices);mesh.SetTriangles(indices,0);mesh.RecalculateNormals();mesh.RecalculateBounds();_meshes.Add(mesh);
            var go=new GameObject("Shore bank "+chunk);go.layer=BoardRenderer.DecorLayer;go.transform.SetParent(transform,false);
            go.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=_bank;renderer.shadowCastingMode=ShadowCastingMode.Off;
        }
        void LateUpdate()
        {
            if(_water==null)return;
            if(_atmosphere==null)
            {
                var composition=GetComponentInParent<EnvironmentComposer>();
                foreach(var owner in FindObjectsByType<CampAtmosphere>(FindObjectsSortMode.None))
                    if(owner.Environment==composition){_atmosphere=owner;break;}
            }
            bool reduced=QuietCampBootstrap.ServicesRef?.ReducedMotion??false;
            float wind=_atmosphere?.Wind.Strength??.2f,rain=_atmosphere?.Weather?.RainAmount??0;
            _water.SetFloat("_Speed",reduced?.02f:.25f+wind*.15f);
            _water.SetFloat("_WaveHeight",reduced?.006f:.023f+wind*.026f);
            _water.SetFloat("_NormalStrength",.05f+rain*.055f);
            _water.SetFloat("_SunReflectionStrength",Mathf.Lerp(.65f,.12f,_atmosphere?.Weather?.Cloudiness??0));
            if(Time.unscaledTime>=_nextSky){_nextSky=Time.unscaledTime+2;UpdateSky();}
        }
        void UpdateSky()
        {
            if(_sky==null)return;
            var sky=RenderSettings.skybox;
            var zenith=sky!=null&&sky.HasProperty("_Zenith")?sky.GetColor("_Zenith"):RenderSettings.ambientSkyColor;
            var horizon=sky!=null&&sky.HasProperty("_Horizon")?sky.GetColor("_Horizon"):RenderSettings.ambientEquatorColor;
            var difference=_lastSky-zenith;
            var horizonDifference=_lastHorizon-horizon;
            if(Mathf.Max(Mathf.Abs(difference.r),Mathf.Abs(difference.g),Mathf.Abs(difference.b),
                Mathf.Abs(horizonDifference.r),Mathf.Abs(horizonDifference.g),Mathf.Abs(horizonDifference.b))<.015f && _lastSky.a>0)return;
            _lastSky=zenith; _lastHorizon=horizon;
            for(int face=0;face<6;face++)
            {
                for(int y=0;y<16;y++)for(int x=0;x<16;x++)
                {
                    float t=face==(int)CubemapFace.PositiveY?1:face==(int)CubemapFace.NegativeY?0:Mathf.Clamp01(y/15f);
                    _pixels[y*16+x]=Color.Lerp(horizon,zenith,t);
                }
                _sky.SetPixels(_pixels,(CubemapFace)face);
            }
            _sky.Apply(true,false);
        }
        void OnDestroy()
        {
            foreach(var mesh in _meshes)if(mesh!=null)Destroy(mesh);
            if(_water!=null)Destroy(_water);if(_bank!=null)Destroy(_bank);if(_sky!=null)Destroy(_sky);
        }
    }
}
