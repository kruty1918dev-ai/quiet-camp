using QuietCamp.Infrastructure;
using System.Collections.Generic;
using QuietCamp.Domain;
using UnityEngine;
using UnityEngine.Rendering;

namespace QuietCamp.Presentation.World
{
    /// <summary>Autumn leaves detach from visible deciduous crowns, share wind, then settle on surfaces.
    /// One dynamic opaque mesh, a fixed pool and no per-leaf behaviours or colliders.</summary>
    public sealed class SeasonalLeafFall : MonoBehaviour
    {
        public const int MaximumLeaves=28;
        sealed class Leaf
        {
            public bool active,landed;public float age,life,size,phase,yaw;
            public Vector3 position,velocity;public Color color;public RainSurface.Contact contact;
        }
        readonly Leaf[] _leaves=new Leaf[MaximumLeaves];
        readonly List<Vector3> _sources=new List<Vector3>();
        readonly List<SeasonalTreeVisual> _authoredTrees=new List<SeasonalTreeVisual>();Transform _world;
        readonly List<Vector3> _vertices=new List<Vector3>(),_normals=new List<Vector3>();
        readonly List<Color> _colors=new List<Color>();readonly List<Vector4> _anchors=new List<Vector4>();
        readonly List<int> _indices=new List<int>();
        CampAtmosphere _host;EnvironmentComposer _forest;Camera _camera;System.Random _random;
        Mesh _mesh;Material _material;MeshRenderer _renderer;float _timer,_progress;int _revision=-1,_budget=14;bool _reduced;
        Matrix4x4 _sourceTransform;Vector3 _cameraPosition;Quaternion _cameraRotation;
        public int ActiveLeafCount {get;private set;}
        public IReadOnlyList<Vector3> Sources=>_sources;
        public void Configure(CampAtmosphere host,EnvironmentComposer forest,LevelData level,Camera camera)
        {
            var season=SeasonProfile.For(level);if(!season.Autumn)return;
            var shader=Resources.Load<Shader>("QuietCamp/FoliageLit");if(shader==null)return;
            _host=host;_forest=forest;_camera=camera;_progress=season.Progress;
            _random=new System.Random(unchecked(level.decorSeed*5197+733));
            for(int i=0;i<_leaves.Length;i++)_leaves[i]=new Leaf();
            _mesh=new Mesh{name="Pooled autumn leaf fall"};_mesh.MarkDynamic();
            gameObject.AddComponent<MeshFilter>().sharedMesh=_mesh;
            _material=new Material(shader){name="Lit folded autumn leaves"};
            _material.SetColor("_BaseColor",Color.white);_material.SetFloat("_VertexTint",1);_material.SetFloat("_ClusterWind",1);
            _material.SetFloat("_Cull",0);_material.SetFloat("_SwayAmp",0);_material.SetFloat("_FlutterAmp",0);_material.SetFloat("_InnerShade",.06f);
            _renderer=gameObject.AddComponent<MeshRenderer>();_renderer.sharedMaterial=_material;
            _renderer.shadowCastingMode=ShadowCastingMode.On;_renderer.receiveShadows=true;gameObject.layer=BoardRenderer.DecorLayer;
            _timer=.7f;_host.GustStarted+=Gust;RefreshSources();
        }
        float Next()=>(float)_random.NextDouble();
        void RefreshSources()
        {
            if(_forest==null)return;
            bool changed=_world!=_host.Weather?.World;
            if(changed)
            {
                _world=_host.Weather?.World;_authoredTrees.Clear();
                if(_world!=null)_authoredTrees.AddRange(_world.GetComponentsInChildren<SeasonalTreeVisual>());
            }
            if(_forest.CanopyRevision==_revision&&_sourceTransform==_forest.transform.localToWorldMatrix
                &&!changed&&(_camera.transform.position-_cameraPosition).sqrMagnitude<.64f&&Quaternion.Angle(_camera.transform.rotation,_cameraRotation)<2)return;
            _revision=_forest.CanopyRevision;_sources.Clear();
            _sourceTransform=_forest.transform.localToWorldMatrix;_cameraPosition=_camera.transform.position;_cameraRotation=_camera.transform.rotation;
            void Add(Vector3 source)
            {
                var screen=_camera.WorldToViewportPoint(source);
                if(screen.z>0&&screen.x>-.12f&&screen.x<1.12f&&screen.y>-.1f&&screen.y<1.2f)_sources.Add(source);
            }
            foreach(var source in _forest.LeafSources)Add(source);
            foreach(var tree in _authoredTrees)if(tree!=null&&tree.HasLeaves&&tree.isActiveAndEnabled)Add(tree.LeafSource);
        }
        void Spawn(bool gust)
        {
            if(_sources.Count==0||ActiveLeafCount>=_budget||_reduced)return;
            Leaf leaf=null;for(int i=0;i<_budget;i++)if(!_leaves[i].active){leaf=_leaves[i];break;}if(leaf==null)return;
            var source=_sources[_random.Next(_sources.Count)];
            leaf.active=true;leaf.landed=false;leaf.age=0;leaf.life=8+Next()*5;leaf.size=.07f+Next()*.07f;
            leaf.phase=Next()*Mathf.PI*2;leaf.yaw=Next()*360;
            leaf.position=source+new Vector3(Next()*.4f-.2f,Next()*.2f,Next()*.4f-.2f);
            var wind=_host.SampleWind(leaf.position,Mathf.Max(0,leaf.position.y));
            leaf.velocity=new Vector3(wind.DirectionXZ.x,-.35f,wind.DirectionXZ.y)*(gust?.65f:.15f);
            leaf.color=Color.Lerp(new Color(.47f,.27f,.13f),new Color(.89f,.61f,.24f),Next());ActiveLeafCount++;
        }
        void Gust()
        {
            if(_mesh==null||_reduced)return;
            RefreshSources();for(int i=0;i<(_budget<=6?2:5);i++)Spawn(true);
        }
        public void Advance(float seconds,int tier,bool reduced)
        {
            using var audit = PerformanceAudit.Measure("QC.SeasonalLeafFall.Advance");
            if(_mesh==null)return;_reduced=reduced;_budget=tier<=0?6:tier==1?14:MaximumLeaves;
            float dt=Mathf.Min(.1f,Mathf.Max(0,seconds));RefreshSources();
            if(reduced)
            {
                foreach(var leaf in _leaves)leaf.active=false;ActiveLeafCount=0;_timer=1;_mesh.Clear();_renderer.enabled=false;return;
            }
            _timer-=dt;
            if(_timer<=0){Spawn(false);_timer=Mathf.Lerp(2.1f,.8f,_progress)+Next()*.9f;}
            ActiveLeafCount=0;
            for(int i=0;i<_leaves.Length;i++)
            {
                var leaf=_leaves[i];if(!leaf.active)continue;
                if(i>=_budget){leaf.active=false;continue;}
                leaf.age+=dt;if(leaf.age>=leaf.life){leaf.active=false;continue;}
                if(!leaf.landed)
                {
                    var wind=_host.SampleWind(leaf.position,Mathf.Max(0,leaf.position.y));
                    var target=new Vector3(wind.DirectionXZ.x*wind.Strength*.8f,-.38f-.12f*Mathf.Sin(leaf.phase+leaf.age*2),wind.DirectionXZ.y*wind.Strength*.8f);
                    leaf.velocity=Vector3.Lerp(leaf.velocity,target,1-Mathf.Exp(-dt*2.5f));var previous=leaf.position;leaf.position+=leaf.velocity*dt;
                    // Query a short swept segment, once a leaf reaches a surface; no independent collision system.
                    float ground=_host.Weather?.World!=null?_host.Weather.World.position.y:0;
                    if(RainSurface.TryCastSegment(previous,leaf.position,ground,_host.Weather?.World,out var contact))
                    {leaf.contact=contact;leaf.position=contact.point+contact.normal*.006f;leaf.landed=true;leaf.life=Mathf.Min(leaf.life,leaf.age+2.2f);}
                }
                else if(leaf.contact.Resolve(out var point,out var normal)){leaf.position=point+normal*.006f;leaf.contact.normal=normal;}
                else {leaf.active=false;continue;}
                ActiveLeafCount++;
            }
            Draw();
        }
        void Draw()
        {
            _vertices.Clear();_normals.Clear();_colors.Clear();_indices.Clear();_anchors.Clear();
            void Triangle(Vector3 a,Vector3 b,Vector3 c,Color color)
            {
                a=transform.InverseTransformPoint(a);b=transform.InverseTransformPoint(b);c=transform.InverseTransformPoint(c);
                var n=Vector3.Cross(b-a,c-a).normalized;int at=_vertices.Count;
                _vertices.Add(a);_vertices.Add(b);_vertices.Add(c);
                for(int i=0;i<3;i++){_normals.Add(n);_colors.Add(color);_indices.Add(at+i);_anchors.Add(new Vector4(0,0,0,-1));}
            }
            foreach(var leaf in _leaves)
            {
                if(!leaf.active)continue;
                float scale=Mathf.SmoothStep(0,1,Mathf.Min(leaf.age/.5f,(leaf.life-leaf.age)/.9f));
                Quaternion pose;
                if(leaf.landed)pose=Quaternion.FromToRotation(Vector3.up,leaf.contact.normal)*Quaternion.Euler(0,leaf.yaw,0);
                else pose=Quaternion.Euler(Mathf.Sin(leaf.age*3+leaf.phase)*38,leaf.yaw+leaf.age*43,Mathf.Sin(leaf.age*2+leaf.phase)*32);
                var axis=pose*(Vector3.forward*leaf.size*scale);var side=pose*(Vector3.right*leaf.size*.58f*scale);
                var fold=leaf.position+pose*(Vector3.up*leaf.size*.12f*scale);
                Triangle(leaf.position-axis,fold,leaf.position+side,leaf.color);Triangle(leaf.position+side,fold,leaf.position+axis,leaf.color);
                Triangle(leaf.position+axis,fold,leaf.position-side,leaf.color*.85f);Triangle(leaf.position-side,fold,leaf.position-axis,leaf.color*.85f);
            }
            _mesh.Clear();_mesh.SetVertices(_vertices);_mesh.SetNormals(_normals);_mesh.SetColors(_colors);_mesh.SetUVs(1,_anchors);_mesh.SetTriangles(_indices,0);_mesh.RecalculateBounds();
            _renderer.enabled=ActiveLeafCount>0;
        }
        void OnDestroy(){if(_host!=null)_host.GustStarted-=Gust;if(_mesh!=null)Destroy(_mesh);if(_material!=null)Destroy(_material);}
    }
}
