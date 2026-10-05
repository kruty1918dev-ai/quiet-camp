using System;
using QuietCamp.Domain;
using QuietCamp.Infrastructure;
using UnityEngine;
using UnityEngine.Rendering;

namespace QuietCamp.Presentation.World
{
    /// <summary>Small shadow-aware scattering volumes running from the forest rim to the clearing.
    /// One mesh, at most six draws, no cameras, colliders, lights or physics queries.</summary>
    public sealed class CanopySunlight : MonoBehaviour
    {
        readonly GameObject[] _shafts = new GameObject[6];
        readonly Material[] _materials = new Material[6];
        readonly Vector3[] _landings = new Vector3[6];
        readonly Vector3[] _corners=new Vector3[8];
        LevelData _level;Vector2Int _screen;
        Mesh _mesh;
        CampAtmosphere _owner;
        Func<bool> _reduced;
        AtmosphereCatalog.Profile _profile;
        float _visibility, _time;
        public int ActiveShafts { get; private set; }
        public Vector3[] Landings => (Vector3[])_landings.Clone();
        public float Visibility => _visibility;

        public void Configure(CampAtmosphere owner, LevelData level, Func<bool> reduced)
        {
            _owner = owner; _reduced = reduced;_level=level;
            var shader = Resources.Load<Shader>("QuietCamp/ForestVolume");
            if (shader == null || !shader.isSupported) return;
            _mesh = new Mesh { name = "Shared sunlight bounds" };
            _mesh.vertices = new[] { new Vector3(-.5f,-.5f,-.5f),new Vector3(.5f,-.5f,-.5f),new Vector3(.5f,.5f,-.5f),new Vector3(-.5f,.5f,-.5f),new Vector3(-.5f,-.5f,.5f),new Vector3(.5f,-.5f,.5f),new Vector3(.5f,.5f,.5f),new Vector3(-.5f,.5f,.5f) };
            _mesh.triangles = new[] {0,2,1,0,3,2,4,5,6,4,6,7,0,4,7,0,7,3,1,2,6,1,6,5,0,1,5,0,5,4,3,7,6,3,6,2};
            _mesh.RecalculateBounds(); _mesh.UploadMeshData(true);
            // Distributed landings keep several canopy openings visible on both phone and tablet.
            FitLandings();
            for (int i = 0; i < _shafts.Length; i++)
            {
                var go = new GameObject("CanopySunbeam" + i); go.transform.SetParent(transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = _mesh;
                var renderer = go.AddComponent<MeshRenderer>();
                var material = new Material(shader) { name = "Warm canopy scattering " + i };
                material.SetFloat("_Shaft", 1); material.SetFloat("_Seed", i * 2.31f + (level.decorSeed % 23));
                material.SetFloat("_Density", 0);
                renderer.sharedMaterial = material; renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
                _materials[i] = material; _shafts[i] = go; go.SetActive(false);
            }
        }
        public void Apply(AtmosphereCatalog.Profile profile) => _profile = profile;
        void FitLandings()
        {
            var camera=_owner.WorldCamera;
            if(camera==null||!ForestGroundView.TryBounds(camera,transform,_corners,out var footprint))return;
            _screen=new Vector2Int(Screen.width,Screen.height);
            float minX=Mathf.Max(-28,footprint.xMin),maxX=Mathf.Min(28,footprint.xMax);
            float minZ=Mathf.Max(-28,footprint.yMin),maxZ=Mathf.Min(28,footprint.yMax);
            for(int i=0;i<_landings.Length;i++)
            {
                float jitter=(_level.decorSeed%13)*.01f;
                float x=(i%3+.4f+jitter)/3f,z=(i/3+.35f+jitter)/2f;
                _landings[i]=new Vector3(Mathf.Lerp(minX,maxX,x),.02f,Mathf.Lerp(minZ,maxZ,z));
            }
        }

        /// <summary>Weather changes density, never the sun direction or the logical shade mask.</summary>
        public void Advance(float seconds)
        {
            if (_mesh == null || _profile == null || _owner == null) return;
            int tier = _owner.QualityTier;
            if(_screen!=new Vector2Int(Screen.width,Screen.height))FitLandings();
            bool reduced = _reduced?.Invoke() ?? false;
            if (!reduced) _time += Mathf.Max(0, seconds);
            float cloud = _owner.Weather?.Cloudiness ?? 0, rain = _owner.Weather?.RainAmount ?? 0;
            float daylight = _profile.Id == "night" ? 0 : _profile.Id == "evening" ? .48f : 1;
            float target = daylight * (1 - cloud * .88f) * (1 - rain);
            _visibility = Mathf.Lerp(_visibility, target, 1 - Mathf.Exp(-Mathf.Max(0, seconds) / 1.6f));
            int budget = _shafts.Length;
            var sun = RenderSettings.sun;
            Vector3 towardSun = sun != null ? -sun.transform.forward : new Vector3(-.5f,.8f,-.3f).normalized;
            float length = Mathf.Min(13, 4.2f / Mathf.Max(.28f, towardSun.y));
            ActiveShafts = 0;
            for (int i = 0; i < _shafts.Length; i++)
            {
                bool active = i < budget && _visibility > .003f;
                _shafts[i].SetActive(active); if (!active) continue; ActiveShafts++;
                var shaft = _shafts[i].transform;
                shaft.position = _landings[i] + towardSun * length * .5f;
                shaft.rotation = Quaternion.FromToRotation(Vector3.up, towardSun);
                float width = i == 0 ? .95f : i == 1 ? .72f : .55f;
                shaft.localScale = new Vector3(width, length, width);
                var mat = _materials[i];
                mat.SetFloat("_Steps", tier == 0 ? 3 : tier == 1 ? 5 : 8);
                mat.SetFloat("_LightTime", _time);
                // A gentle changing canopy opening; reduced motion keeps a steady lit volume.
                float opening = reduced ? .90f : .87f + .13f * Mathf.Sin(_time * .32f + i * 1.7f);
                mat.SetFloat("_Density", .24f * _visibility * opening);
                var palette=SeasonPalette.For(_level);
                var scattering=palette.SnowCoverage>.5f?new Color(1.34f,1.40f,1.53f):new Color(1.55f,1.32f,.83f);
                mat.SetColor("_FogColor", Color.Lerp(scattering, _profile.Sun * palette.SunTint * 1.5f, .4f));
            }
        }
        void OnDestroy()
        {
            foreach (var shaft in _shafts) if (shaft != null) Destroy(shaft);
            foreach (var mat in _materials) if (mat != null) Destroy(mat);
            if (_mesh != null) Destroy(_mesh);
        }
    }
}
