using UnityEngine;
using UnityEngine.Rendering;

namespace QuietCamp.Presentation.World
{
    /// <summary>One bounded draw for rain contacts and fabric runoff. Each mark
    /// starts at a visible drop's hit; saved barycentric samples follow the roof
    /// as it moves in the wind. No decals, colliders or per-frame allocations.</summary>
    public sealed class RainImpacts : MonoBehaviour
    {
        struct Mark
        {
            public RainSurface.Contact contact, head;
            public float age, lifetime, size, sampleClock;
            public int samples;
            public bool alive, fabric, flowing;
        }
        const int Maximum = 24, Segments = 12, TrailSamples = 12;
        const int RingVertices = Segments * 2, VerticesPerMark = RingVertices + TrailSamples * 2;
        readonly Mark[] _marks = new Mark[Maximum];
        readonly RainSurface.Contact[] _trail = new RainSurface.Contact[Maximum * TrailSamples];
        readonly Vector3[] _vertices = new Vector3[Maximum * VerticesPerMark];
        readonly Color[] _colors = new Color[Maximum * VerticesPerMark];
        readonly bool[] _visible = new bool[Maximum];
        Mesh _mesh; Material _material; MeshRenderer _renderer; int _cursor;
        public int Budget { get; private set; }
        public int ActiveCount { get { int n = 0; foreach (var m in _marks) if (m.alive) n++; return n; } }
        public int RunoffCount { get { int n = 0; foreach (var m in _marks) if (m.alive && m.fabric) n++; return n; } }
        public void Configure()
        {
            _mesh = new Mesh { name = "Rain contacts and roof runoff (owned)" }; _mesh.MarkDynamic();
            var indices = new int[Maximum * (Segments + TrailSamples - 1) * 6];int t=0;
            for (int mark = 0; mark < Maximum; mark++)
            {
                int first=mark*VerticesPerMark;
                for(int s=0;s<Segments;s++) Quad(indices,ref t,first+s*2,first+(s+1)%Segments*2);
                for(int s=0;s<TrailSamples-1;s++) Quad(indices,ref t,first+RingVertices+s*2,first+RingVertices+(s+1)*2);
            }
            _mesh.vertices = _vertices; _mesh.colors = _colors; _mesh.triangles = indices;
            gameObject.AddComponent<MeshFilter>().sharedMesh = _mesh;
            _renderer = gameObject.AddComponent<MeshRenderer>();_renderer.enabled=false;
            _material = CozyParticleMaterial.Create(Texture2D.whiteTexture, 0);
            _renderer.sharedMaterial = _material; _renderer.shadowCastingMode = ShadowCastingMode.Off; _renderer.receiveShadows = false;
        }
        static void Quad(int[] indices,ref int t,int a,int b)
        {
            indices[t++]=a;indices[t++]=b;indices[t++]=a+1;
            indices[t++]=a+1;indices[t++]=b;indices[t++]=b+1;
        }
        public void SetQuality(int tier, bool reduced)
        {
            Budget = reduced ? 0 : tier == 0 ? 3 : tier == 1 ? 12 : 24;
            if (reduced) Clear();
            for (int i = Budget; i < Maximum; i++) _marks[i].alive = false;
        }
        public void Emit(RainSurface.Contact contact, float size)
        {
            if (Budget == 0 || !contact.Resolve(out _,out _)) return;
            int i=-1;float oldest=-1;
            for(int n=0;n<Budget;n++)
            {
                int candidate=(_cursor+n)%Budget;var previous=_marks[candidate];
                if(!previous.alive){i=candidate;break;}
                // A brief ground splash must not repeatedly erase a longer
                // thread on the tent before it has time to run downhill.
                float score=(previous.fabric?0:2)+previous.age/previous.lifetime;
                if(score>oldest){oldest=score;i=candidate;}
            }
            _cursor=(i+1)%Budget;bool fabric=contact.IsFabric;
            _marks[i] = new Mark { contact = contact, head=contact, alive = true, fabric=fabric, flowing=fabric,
                lifetime = fabric?1.8f:.55f, size = size, samples=1 };
            _trail[i*TrailSamples]=contact;
            // An overwritten ground mark must not leave the previous roof trail.
            for(int v=i*VerticesPerMark;v<(i+1)*VerticesPerMark;v++)_colors[v]=Color.clear;
        }
        public void Clear() { for (int i = 0; i < Maximum; i++) _marks[i].alive = false; }
        public void Advance(float seconds)
        {
            bool dirty=false,any=false;float dt=Mathf.Max(0,seconds);
            for (int i = 0; i < Maximum; i++)
            {
                var mark = _marks[i];Vector3 centre=default,normal=default;
                if(mark.alive)
                {
                    mark.age+=dt;
                    mark.alive=mark.age<mark.lifetime&&mark.contact.Resolve(out centre,out normal);
                }
                if(!mark.alive)
                {
                    if(_visible[i])
                    {
                        for(int v=i*VerticesPerMark;v<(i+1)*VerticesPerMark;v++)_colors[v]=Color.clear;
                        _visible[i]=false;dirty=true;
                    }
                    _marks[i]=mark;continue;
                }
                _visible[i]=true;dirty=true;any=true;
                float progress = Mathf.Clamp01(mark.age / (mark.fabric?.18f:.55f));
                float radius = mark.size * Mathf.Lerp(.15f, mark.fabric?.35f:1, progress);
                var tangent = Vector3.Cross(normal, Mathf.Abs(normal.y) > .9f ? Vector3.forward : Vector3.up).normalized;
                var bitangent = Vector3.Cross(normal, tangent);
                float alpha = Mathf.Sin(progress * Mathf.PI) * (mark.fabric?.20f:.28f);
                for (int s = 0; s < Segments; s++)
                {
                    float angle = s * Mathf.PI * 2 / Segments;
                    var direction = tangent * Mathf.Cos(angle) + bitangent * Mathf.Sin(angle);
                    for (int edge = 0; edge < 2; edge++)
                    {
                        int v = i * VerticesPerMark + s * 2 + edge;
                        var offset = direction * Mathf.Max(0,radius + (edge == 0 ? -.006f : .006f));
                        var point = mark.contact.Project(offset, centre);
                        _vertices[v] = transform.InverseTransformPoint(point + normal * .009f);
                        _colors[v] = new Color(.82f, .94f, .95f, alpha);
                    }
                }
                if(mark.fabric)
                {
                    // Short fixed substeps keep movement on a changing roof and
                    // let a slow frame cross several small refined triangles.
                    float remaining=Mathf.Min(dt,.25f);
                    while(remaining>0 && mark.flowing)
                    {
                        float step=Mathf.Min(remaining,.025f);remaining-=step;
                        mark.flowing=mark.head.Slide(step*(.24f+Mathf.Min(mark.age,.8f)*.25f));
                        mark.sampleClock+=step;
                        if(mark.sampleClock>=.04f || !mark.flowing)
                        {
                            mark.sampleClock=0;
                            int first=i*TrailSamples;
                            if(mark.samples==TrailSamples)
                                for(int s=0;s<TrailSamples-1;s++)_trail[first+s]=_trail[first+s+1];
                            else mark.samples++;
                            _trail[first+mark.samples-1]=mark.head;
                        }
                    }
                    DrawRunoff(i,mark);
                }
                _marks[i] = mark;
            }
            if(dirty){_mesh.vertices = _vertices; _mesh.colors = _colors; _mesh.RecalculateBounds();}
            _renderer.enabled=any;
        }
        void DrawRunoff(int index,Mark mark)
        {
            int start=index*VerticesPerMark+RingVertices;
            float fade=Mathf.SmoothStep(0,1,Mathf.Clamp01(mark.age*10)) * Mathf.Clamp01((mark.lifetime-mark.age)/.55f);
            for(int s=0;s<TrailSamples;s++)
            {
                int v=start+s*2;
                if(s>=mark.samples || !_trail[index*TrailSamples+s].Resolve(out var point,out var normal))
                {_colors[v]=_colors[v+1]=Color.clear;continue;}
                var contact=_trail[index*TrailSamples+s];
                var down=Vector3.ProjectOnPlane(Vector3.down,normal).normalized;
                var across=Vector3.Cross(normal,down).normalized;
                float along=s/(float)Mathf.Max(1,mark.samples-1);
                // A dark wet thread with a small silver head, tapered ends.
                float taper=Mathf.Sin(along*Mathf.PI);bool head=s==mark.samples-2;
                float width=head?.011f:.006f*taper;
                Color color=head?new Color(.76f,.89f,.92f,fade*.55f):new Color(.16f,.20f,.19f,fade*.40f*taper);
                _vertices[v]=transform.InverseTransformPoint(contact.Project(-across*width,point)+normal*.006f);
                _vertices[v+1]=transform.InverseTransformPoint(contact.Project(across*width,point)+normal*.006f);
                _colors[v]=_colors[v+1]=color;
            }
        }
        void OnDestroy() { if (_mesh != null) Destroy(_mesh); if (_material != null) Destroy(_material); }
    }
}
