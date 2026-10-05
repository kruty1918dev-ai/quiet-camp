using System;
using System.Collections.Generic;
using QuietCamp.Domain;
using UnityEngine;
using UnityEngine.Rendering;

namespace QuietCamp.Presentation.World
{
    /// <summary>One bounded mesh of old bootprints, and persistent fire-thaw on the clearing material.
    /// No walking rules, colliders, saves, per-print objects or extra rendering cameras.</summary>
    public sealed class CampGroundDetails : MonoBehaviour
    {
        public const int MaximumPrints = 256;
        LevelData _level;
        Material _ground, _printMaterial;
        Mesh _mesh;
        GameObject _printRoot;
        CampAtmosphere _atmosphere;
        readonly List<Vector3> _centres = new List<Vector3>();
        float _melt = 1, _refresh;
        public int PrintCount => _centres.Count;
        public IReadOnlyList<Vector3> PrintCentres => _centres;
        public float MeltAmount => _melt;

        public void Configure(LevelData level, Material ground)
        {
            _level = level; _ground = ground;
            _ground.SetFloat("_SnowSeason", SeasonProfile.For(level).Winter ? 1 : 0);
            var go = new GameObject("Camp bootprints", typeof(MeshFilter), typeof(MeshRenderer));
            _printRoot = go;
            go.transform.SetParent(transform, false); go.layer = BoardRenderer.DecorLayer;
            _mesh = new Mesh { name = "Bounded camp surface traces" };
            go.GetComponent<MeshFilter>().sharedMesh = _mesh;
            _printMaterial = new Material(Resources.Load<Shader>("QuietCamp/FoliageLit")) { name = "Lit camp bootprints" };
            _printMaterial.SetColor("_BaseColor", Color.white); _printMaterial.SetFloat("_VertexTint", 1);
            _printMaterial.SetFloat("_SwayAmp", 0); _printMaterial.SetFloat("_FlutterAmp", 0);
            _printMaterial.SetFloat("_InnerShade", 0);
            var renderer = go.GetComponent<MeshRenderer>(); renderer.sharedMaterial = _printMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = true;
            PublishHeat();
        }

        public static float HeatAt(LevelData level, Vector3 point)
        {
            float heat = 0;
            foreach (var fire in level.noise ?? Array.Empty<int[]>())
            {
                if (fire?.Length != 2) continue;
                var centre = BoardMath.CellCenterWorld(level, new Cell(fire[0],fire[1]));
                float distance = new Vector2(point.x-centre.x,point.z-centre.z).magnitude;
                heat = Mathf.Max(heat, 1-Mathf.SmoothStep(0,1,Mathf.Clamp01((distance-.35f)/1.15f)));
            }
            return heat;
        }

        public void AdvanceHeat(float seconds, float fireStrength, float snowfall)
        {
            // Wet ash stays exposed after quenching; falling snow slowly covers a cold pit again.
            float target = fireStrength > .15f ? 1 : 0;
            _melt = Mathf.MoveTowards(_melt, target, Mathf.Max(0,seconds)
                * (target > _melt ? 1/12f : Mathf.Clamp01(snowfall)/180f));
            PublishHeat();
        }
        void LateUpdate()
        {
            if (_level == null || Time.time < _refresh) return;
            _refresh = Time.time+.5f;
            if (_atmosphere == null)
                foreach (var candidate in FindObjectsByType<CampAtmosphere>(FindObjectsSortMode.None))
                    if (candidate.gameObject.scene == gameObject.scene && candidate.isActiveAndEnabled) { _atmosphere=candidate; break; }
            AdvanceHeat(.5f, _atmosphere?.RainShelter?.FireStrength ?? 1, _atmosphere?.Weather?.Snowfall ?? 0);
        }
        void PublishHeat()
        {
            if (_ground == null) return;
            _ground.SetFloat("_HeatMelt", _melt);
            for (int i=0;i<4;i++)
            {
                Vector4 site = Vector4.zero;
                if (i<(_level.noise?.Length??0) && _level.noise[i]?.Length == 2)
                {
                    var point = transform.TransformPoint(BoardMath.CellCenterWorld(_level,new Cell(_level.noise[i][0],_level.noise[i][1])));
                    site = new Vector4(point.x,point.z,1.50f,1);
                }
                _ground.SetVector("_HeatSite"+i,site);
            }
        }

        public void Rebuild(IReadOnlyList<Placement> placements)
        {
            var vertices = new List<Vector3>(); var normals = new List<Vector3>();
            var colors = new List<Color>(); var anchors = new List<Vector4>(); var indices = new List<int>();
            var occupied = new HashSet<Cell>(); var visited = new HashSet<Cell>(); _centres.Clear();
            foreach (var p in _level.blocked ?? Array.Empty<int[]>()) if(p?.Length==2) occupied.Add(new Cell(p[0],p[1]));
            foreach (var p in _level.noise ?? Array.Empty<int[]>()) if(p?.Length==2) occupied.Add(new Cell(p[0],p[1]));
            foreach (var p in placements) foreach(var cell in RuleEvaluator.Footprint(p)) occupied.Add(cell);
            var color = new CampBiomeProfile(_level).TraceColor;
            var season = SeasonProfile.For(_level); var palette = season.Palette;
            var shore = EnvironmentCompositionData.For(_level).shore;

            void Print(Vector3 at, Vector3 forward, int parity)
            {
                if(_centres.Count>=MaximumPrints || ShorelineGeometry.Contains(shore,at,.30f)) return;
                var cell = new Cell(Mathf.FloorToInt(at.x+_level.width*.5f),Mathf.FloorToInt(at.z+_level.height*.5f));
                if(occupied.Contains(cell))return;
                var side=Vector3.Cross(Vector3.up,forward).normalized;
                at+=side*(parity%2==0?-.11f:.11f);
                at.y=Mathf.Max(at.y,season.Winter?.018f+season.SnowDepth(_level,at):0)+.012f;
                var traceColor=season.Winter?Color.Lerp(color,palette.Soil*.62f,HeatAt(_level,at)*_melt):color;
                _centres.Add(at);
                // An irregular sole and separate heel read as a boot rather than a painted dot.
                void Sole(float start,float end,float width)
                {
                    int first=vertices.Count; var points=new[]{new Vector2(-width*.75f,start),new Vector2(-width,end-.025f),
                        new Vector2(-width*.50f,end),new Vector2(width*.60f,end),new Vector2(width,end-.035f),new Vector2(width*.72f,start)};
                    foreach(var p in points)
                    {
                        vertices.Add(at+side*p.x+forward*p.y);normals.Add(Vector3.up);
                        colors.Add(traceColor);
                        anchors.Add(new Vector4(0,0,0,-1));
                    }
                    for(int i=1;i<points.Length-1;i++){indices.Add(first);indices.Add(first+i);indices.Add(first+i+1);}
                }
                Sole(-.015f,.125f,.054f); Sole(-.105f,-.035f,.044f);
            }
            foreach(var access in CampAccess.Points(_level))
                for(int step=1;step<19;step++)
                {
                    var at=CampTrail.Centre(_level,access,step*.43f);
                    var next=CampTrail.Centre(_level,access,step*.43f+.1f);next.y=at.y;
                    Print(at,(next-at).normalized,step);
                }
            var report=RuleEvaluator.Evaluate(_level,placements,false);
            foreach(var route in report.Routes)
            {
                if(!route.Reachable)continue;
                for(int i=0;i<route.Cells.Length;i++)
                {
                    var cell=route.Cells[i];if(!visited.Add(cell)||occupied.Contains(cell))continue;
                    var at=BoardMath.CellCenterWorld(_level,cell);at.y=.024f;
                    var direction=route.Cells.Length>1?BoardMath.CellCenterWorld(_level,route.Cells[i==0?1:i-1])-at:Vector3.forward;
                    direction.y=0;direction.Normalize();
                    Print(at-direction*.19f,direction,i*2);Print(at+direction*.19f,direction,i*2+1);
                }
            }
            _mesh.Clear();_mesh.SetVertices(vertices);_mesh.SetNormals(normals);_mesh.SetColors(colors);
            _mesh.SetUVs(1,anchors);_mesh.SetTriangles(indices,0);_mesh.RecalculateBounds();
        }
        void OnDestroy(){if(_printRoot!=null)Destroy(_printRoot);if(_mesh!=null)Destroy(_mesh);if(_printMaterial!=null)Destroy(_printMaterial);}
    }
}
