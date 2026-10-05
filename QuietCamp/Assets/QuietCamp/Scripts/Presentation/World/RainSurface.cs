using System.Collections.Generic;
using UnityEngine;

namespace QuietCamp.Presentation.World
{
    /// <summary>Read-only mesh queries for cosmetic rain. No physics colliders,
    /// gameplay rays or per-frame mesh copies. Shared geometry is reference counted.</summary>
    public sealed class RainSurface : MonoBehaviour
    {
        sealed class Geometry
        {
            public Vector3[] vertices;
            public int[] triangles, slots, neighbours;
            public Bounds bounds;
            public int owners;
        }
        public struct Contact
        {
            public RainSurface surface;
            internal int geometryVersion;
            public int triangle;
            public Vector2 barycentric;
            public Vector3 point, normal;
            public float distance;
            public bool IsFabric => surface != null && geometryVersion==surface._geometryVersion && surface.IsFabric(triangle);
            public bool Resolve(out Vector3 position, out Vector3 facing)
            {
                position = point; facing = normal;
                return triangle < 0 || (surface != null && surface.Resolve(this, out position, out facing));
            }
            public Vector3 Project(Vector3 offset, Vector3 centre)
                => surface != null ? surface.Project(this, centre + offset) : centre + offset;
            /// <summary>Advance downhill over connected fabric triangles. At an
            /// open hem the contact stops; water never floats past the roof.</summary>
            public bool Slide(float distance)
                => surface != null && surface.Slide(ref this, distance);
        }
        static readonly List<RainSurface> Surfaces = new List<RainSurface>();
        static readonly Dictionary<Mesh, Geometry> Cache = new Dictionary<Mesh, Geometry>();
        public static int CachedMeshCount => Cache.Count;
        Mesh _mesh; Geometry _geometry; Renderer _renderer; bool[] _cloth; Bounds[] _clothBounds;
        Material[] _materials;TentCloth _clothOwner;int _clothVersion=-1;
        Vector4[] _storageCentres;float[] _storagePressure;
        Vector3[] _deformed;
        int[] _vertexStamps;
        int _poseVersion=1, _cachedWindVersion=-1,_geometryVersion;
        Matrix4x4 _localToWorld, _worldToLocal;
        static int _windVersion;
        static Vector2 _wind; static float _strength, _time;
        public static void CaptureWind()
        {
            var direction = Shader.GetGlobalVector("_AtmosWindXZ");
            var wind=new Vector2(direction.x,direction.y);var strength=Shader.GetGlobalFloat("_AtmosWindStrength");var time=Shader.GetGlobalFloat("_AtmosWindTime");
            if(wind!=_wind||strength!=_strength||time!=_time)_windVersion++;
            _wind=wind;_strength=strength;_time=time;
        }
        public static void Attach(GameObject model)
        {
            foreach (var filter in model.GetComponentsInChildren<MeshFilter>(true))
                if (filter.GetComponent<RainSurface>() == null && filter.sharedMesh != null)
                    filter.gameObject.AddComponent<RainSurface>();
        }
        void Awake() => RefreshGeometry();
        public void RefreshGeometry()
        {
            _geometryVersion=unchecked(_geometryVersion+1);
            if(_geometry!=null && --_geometry.owners==0)Cache.Remove(_mesh);
            Surfaces.Remove(this);_geometry=null;_cloth=null;_clothBounds=null;_deformed=null;_vertexStamps=null;
            _materials=null;_clothOwner=null;_clothVersion=-1;_storageCentres=null;_storagePressure=null;
            _mesh = GetComponent<MeshFilter>()?.sharedMesh; _renderer = GetComponent<Renderer>();
            if (_mesh == null || !_mesh.isReadable || _renderer == null) return;
            if (!Cache.TryGetValue(_mesh, out _geometry))
            {
                _geometry = new Geometry { vertices = _mesh.vertices, triangles = _mesh.triangles, bounds = _mesh.bounds };
                _geometry.slots = new int[_geometry.triangles.Length / 3];
                _geometry.neighbours=new int[_geometry.triangles.Length];
                for(int i=0;i<_geometry.neighbours.Length;i++)_geometry.neighbours[i]=-1;
                var edges=new Dictionary<long,(int triangle,int side)>();
                // Authoring often duplicates vertices along hard normal seams.
                // Weld only query topology, leaving render geometry untouched.
                var welded=new int[_geometry.vertices.Length];var positions=new Dictionary<Vector3,int>();
                for(int i=0;i<welded.Length;i++)
                {
                    if(!positions.TryGetValue(_geometry.vertices[i],out int index))
                    {index=positions.Count;positions.Add(_geometry.vertices[i],index);}
                    welded[i]=index;
                }
                for(int t=0;t<_geometry.triangles.Length;t+=3)for(int side=0;side<3;side++)
                {
                    int a=welded[_geometry.triangles[t+(side+1)%3]],b=welded[_geometry.triangles[t+(side+2)%3]];
                    long key=((long)Mathf.Min(a,b)<<32)|(uint)Mathf.Max(a,b);
                    if(edges.TryGetValue(key,out var other))
                    {
                        _geometry.neighbours[t+side]=other.triangle;
                        _geometry.neighbours[other.triangle*3+other.side]=t/3;
                    }
                    else edges.Add(key,(t/3,side));
                }
                for (int s = 0; s < _mesh.subMeshCount; s++)
                {
                    var part = _mesh.GetSubMesh(s);
                    for (int t = part.indexStart / 3; t < (part.indexStart + part.indexCount) / 3; t++) _geometry.slots[t] = s;
                }
                Cache.Add(_mesh, _geometry);
            }
            _geometry.owners++;
            if(isActiveAndEnabled)Surfaces.Add(this);
        }
        void OnEnable() { if (_geometry != null && !Surfaces.Contains(this)) Surfaces.Add(this); }
        void OnDisable() => Surfaces.Remove(this);
        void OnDestroy()
        {
            Surfaces.Remove(this);
            if (_geometry != null && --_geometry.owners == 0) Cache.Remove(_mesh);
        }
        void CaptureMaterials()
        {
            if (_cloth != null) return;
            var materials = _renderer.sharedMaterials;_materials=materials;_clothOwner=GetComponentInParent<TentCloth>();
            _storageCentres=new Vector4[materials.Length];_storagePressure=new float[materials.Length];
            _cloth = new bool[materials.Length]; _clothBounds=new Bounds[materials.Length];
            for (int i = 0; i < materials.Length; i++)
            {
                _cloth[i] = materials[i] != null && materials[i].shader.name == "QuietCamp/TentCloth";
                if(_cloth[i]){Vector3 min=materials[i].GetVector("_MeshMin");Vector3 size=materials[i].GetVector("_MeshSize");_clothBounds[i]=new Bounds(min+size*.5f,size);}
            }
        }
        void PreparePose()
        {
            var matrix=transform.localToWorldMatrix;
            int version=_clothOwner!=null?_clothOwner.DeformationVersion:0;
            if(matrix==_localToWorld && _cachedWindVersion==_windVersion&&_clothVersion==version)return;
            if(_clothVersion!=version&&_materials!=null)
                for(int i=0;i<_materials.Length;i++)if(_cloth[i]&&_materials[i]!=null)
                { _storageCentres[i]=_materials[i].GetVector("_StorageBulge");_storagePressure[i]=_materials[i].GetFloat("_StorageAmount"); }
            _clothVersion=version;
            _localToWorld=matrix;_worldToLocal=transform.worldToLocalMatrix;_cachedWindVersion=_windVersion;
            if(_poseVersion==int.MaxValue){if(_vertexStamps!=null)System.Array.Clear(_vertexStamps,0,_vertexStamps.Length);_poseVersion=1;}else _poseVersion++;
        }
        Vector3 Vertex(int index, int slot)
        {
            var p = _geometry.vertices[index];
            if (slot < _cloth.Length && _cloth[slot])
            {
                if(_deformed==null)
                {
                    int count=_geometry.vertices.Length*_cloth.Length;
                    _deformed=new Vector3[count];_vertexStamps=new int[count];
                }
                int key=slot*_geometry.vertices.Length+index;
                if(_vertexStamps[key]!=_poseVersion)
                {
                    _deformed[key]=TentCloth.DeformVertex(p,_clothBounds[slot],_localToWorld,_worldToLocal,_wind,_strength,_time,_storageCentres[slot],_storagePressure[slot]);
                    _vertexStamps[key]=_poseVersion;
                }
                p=_deformed[key];
            }
            return p;
        }
        bool Resolve(Contact contact, out Vector3 point, out Vector3 normal)
        {
            point = contact.point; normal = contact.normal;
            if (_geometry==null || contact.geometryVersion!=_geometryVersion || !isActiveAndEnabled || !_renderer.enabled || contact.triangle<0 || contact.triangle>=_geometry.slots.Length) return false;
            CaptureMaterials();PreparePose(); int t = contact.triangle * 3, slot = _geometry.slots[contact.triangle];
            var a = _localToWorld.MultiplyPoint3x4(Vertex(_geometry.triangles[t], slot));
            var b = _localToWorld.MultiplyPoint3x4(Vertex(_geometry.triangles[t + 1], slot));
            var c = _localToWorld.MultiplyPoint3x4(Vertex(_geometry.triangles[t + 2], slot));
            point = a * (1 - contact.barycentric.x - contact.barycentric.y) + b * contact.barycentric.x + c * contact.barycentric.y;
            normal = Vector3.Cross(b - a, c - a).normalized;
            if (normal.y < 0) normal = -normal;
            return true;
        }
        bool IsFabric(int triangle)
        {
            if(_geometry==null || triangle<0 || triangle>=_geometry.slots.Length)return false;
            CaptureMaterials();int slot=_geometry.slots[triangle];
            return slot<_cloth.Length && _cloth[slot];
        }
        static Vector3 Barycentric(Vector3 point,Vector3 a,Vector3 b,Vector3 c)
        {
            var e1=b-a;var e2=c-a;var p=point-a;
            float d00=Vector3.Dot(e1,e1),d01=Vector3.Dot(e1,e2),d11=Vector3.Dot(e2,e2);
            float denominator=d00*d11-d01*d01;
            if(Mathf.Abs(denominator)<1e-8f)return new Vector3(1,0,0);
            float u=(d11*Vector3.Dot(p,e1)-d01*Vector3.Dot(p,e2))/denominator;
            float v=(d00*Vector3.Dot(p,e2)-d01*Vector3.Dot(p,e1))/denominator;
            return new Vector3(1-u-v,u,v);
        }
        bool Slide(ref Contact contact,float distance)
        {
            if(!IsFabric(contact.triangle)||!Resolve(contact,out var start,out var normal))return false;
            var downhill=Vector3.ProjectOnPlane(Vector3.down,normal);
            if(downhill.sqrMagnitude<.0001f)return false;
            var target=start+downhill.normalized*Mathf.Max(0,distance);
            int triangle=contact.triangle;
            for(int step=0;step<12;step++)
            {
                WorldTriangle(triangle,out var a,out var b,out var c);
                var weights=Barycentric(target,a,b,c);
                bool inside=weights.x>=-.00001f&&weights.y>=-.00001f&&weights.z>=-.00001f;
                if(inside)
                {
                    weights=new Vector3(Mathf.Max(0,weights.x),Mathf.Max(0,weights.y),Mathf.Max(0,weights.z));
                    weights/=weights.x+weights.y+weights.z;
                    contact.triangle=triangle;contact.barycentric=new Vector2(weights.y,weights.z);
                    Resolve(contact,out contact.point,out contact.normal);return true;
                }
                var from=Barycentric(start,a,b,c);float fraction=1;int side=-1;
                for(int axis=0;axis<3;axis++)if(weights[axis]<0)
                {
                    float crossing=Mathf.Clamp01(from[axis]/Mathf.Max(.000001f,from[axis]-weights[axis]));
                    if(crossing<=fraction){fraction=crossing;side=axis;}
                }
                if(side<0)return false;
                var edge=Vector3.Lerp(from,weights,fraction);
                edge=new Vector3(Mathf.Max(0,edge.x),Mathf.Max(0,edge.y),Mathf.Max(0,edge.z));
                edge/=Mathf.Max(.000001f,edge.x+edge.y+edge.z);
                start=a*edge.x+b*edge.y+c*edge.z;
                contact.triangle=triangle;contact.barycentric=new Vector2(edge.y,edge.z);
                Resolve(contact,out contact.point,out contact.normal);
                int next=_geometry.neighbours[triangle*3+side];
                if(next<0||!IsFabric(next))return false;
                WorldTriangle(next,out var na,out var nb,out var nc);
                if(Vector3.Dot(Vector3.Cross(b-a,c-a).normalized,Vector3.Cross(nb-na,nc-na).normalized)<.5f)return false;
                triangle=next;
            }
            return false;
        }
        Vector3 Project(Contact contact, Vector3 point)
        {
            if(_geometry==null||contact.geometryVersion!=_geometryVersion||contact.triangle<0||contact.triangle>=_geometry.slots.Length)return point;
            PreparePose();
            int triangle=contact.triangle;
            for(int step=0;step<3;step++)
            {
                WorldTriangle(triangle,out var a,out var b,out var c);
                var e1=b-a;var e2=c-a;var p=point-a;
                float d00=Vector3.Dot(e1,e1),d01=Vector3.Dot(e1,e2),d11=Vector3.Dot(e2,e2);
                float denominator=d00*d11-d01*d01;if(Mathf.Abs(denominator)<.00000001f)return a;
                float u=(d11*Vector3.Dot(p,e1)-d01*Vector3.Dot(p,e2))/denominator;
                float v=(d00*Vector3.Dot(p,e2)-d01*Vector3.Dot(p,e1))/denominator,w=1-u-v;
                if(u>=0&&v>=0&&w>=0)return a*w+b*u+c*v;
                int side=w<u&&w<v?0:u<v?1:2;
                int next=_geometry.neighbours[triangle*3+side];
                if(next>=0&&step<2)
                {
                    WorldTriangle(next,out var na,out var nb,out var nc);
                    if(Vector3.Dot(Vector3.Cross(e1,e2).normalized,Vector3.Cross(nb-na,nc-na).normalized)>.985f)
                    {triangle=next;continue;}
                }
                w=Mathf.Max(0,w);u=Mathf.Max(0,u);v=Mathf.Max(0,v);
                return (a*w+b*u+c*v)/Mathf.Max(.0001f,w+u+v);
            }
            return point;
        }
        void WorldTriangle(int triangle,out Vector3 a,out Vector3 b,out Vector3 c)
        {
            int t=triangle*3,slot=_geometry.slots[triangle];
            a=_localToWorld.MultiplyPoint3x4(Vertex(_geometry.triangles[t],slot));
            b=_localToWorld.MultiplyPoint3x4(Vertex(_geometry.triangles[t+1],slot));
            c=_localToWorld.MultiplyPoint3x4(Vertex(_geometry.triangles[t+2],slot));
        }
        public static Contact Cast(Ray ray, float groundY, Transform world = null, float maximumDistance = float.PositiveInfinity)
        {
            float distance = ray.direction.y<-.000001f ? (groundY - ray.origin.y) / ray.direction.y : float.PositiveInfinity;
            if(distance<0)distance=float.PositiveInfinity;
            var hit = new Contact { triangle = -1, distance = distance, point = ray.GetPoint(distance), normal = Vector3.up };
            foreach (var surface in Surfaces)
            {
                if (surface == null || !surface.isActiveAndEnabled || !surface._renderer.enabled
                    || (world != null && !surface.transform.IsChildOf(world))) continue;
                var bounds = surface._renderer.bounds; bounds.Expand(.2f);
                if (!bounds.IntersectRay(ray, out float entry) || entry > Mathf.Min(hit.distance,maximumDistance)) continue;
                surface.CaptureMaterials();surface.PreparePose();
                var origin = surface._worldToLocal.MultiplyPoint3x4(ray.origin);
                var direction = surface._worldToLocal.MultiplyVector(ray.direction);
                var geometry = surface._geometry;
                for (int t = 0; t < geometry.triangles.Length; t += 3)
                {
                    int slot = geometry.slots[t / 3];
                    var a = surface.Vertex(geometry.triangles[t], slot);
                    var b = surface.Vertex(geometry.triangles[t + 1], slot);
                    var c = surface.Vertex(geometry.triangles[t + 2], slot);
                    var e1 = b - a; var e2 = c - a; var p = Vector3.Cross(direction, e2);
                    float det = Vector3.Dot(e1, p); if (Mathf.Abs(det) < .000001f) continue;
                    float inverse = 1 / det; var offset = origin - a;
                    float u = Vector3.Dot(offset, p) * inverse; if (u < 0 || u > 1) continue;
                    var q = Vector3.Cross(offset, e1); float v = Vector3.Dot(direction, q) * inverse;
                    if (v < 0 || u + v > 1) continue;
                    float d = Vector3.Dot(e2, q) * inverse; if (d < 0 || d >= hit.distance || d>maximumDistance) continue;
                    hit = new Contact { surface = surface, geometryVersion=surface._geometryVersion, triangle = t / 3, barycentric = new Vector2(u, v), distance = d };
                    surface.Resolve(hit, out hit.point, out hit.normal);
                }
            }
            return hit;
        }
        public static bool TryCastSegment(Vector3 previous,Vector3 current,float groundY,Transform world,out Contact contact)
        {
            var movement=current-previous;float distance=movement.magnitude;contact=default;
            if(distance<.000001f)return false;
            contact=Cast(new Ray(previous,movement/distance),groundY,world,distance+.00001f);
            return contact.distance>=0 && contact.distance<=distance+.00001f;
        }
    }
}
