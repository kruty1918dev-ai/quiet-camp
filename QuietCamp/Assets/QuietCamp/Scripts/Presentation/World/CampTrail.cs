using QuietCamp.Domain;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace QuietCamp.Presentation.World
{
    /// <summary>Physical continuation of a reserved walking cell into the
    /// forest. The same route keeps decorative trunks and plants out of the
    /// passage. Presentation only: no colliders or changes to walking rules.</summary>
    public sealed class CampTrail : MonoBehaviour
    {
        public const float Length = 9f;
        const int Steps = 24;
        Mesh _mesh,_pebbles; Material _material,_pebbleMaterial;
        public Cell AccessCell { get; private set; }
        public Vector3 OuterEnd { get; private set; }
        public static Vector3 Outward(LevelData level,Cell cell)
        {
            if(cell.Z==0)return Vector3.back;
            if(cell.X==level.width-1)return Vector3.right;
            if(cell.Z==level.height-1)return Vector3.forward;
            if(cell.X==0)return Vector3.left;
            // Old snapshots can have an interior starting point.
            int nearest=Mathf.Min(cell.X,level.width-1-cell.X,cell.Z,level.height-1-cell.Z);
            if(nearest==cell.Z)return Vector3.back;
            if(nearest==level.width-1-cell.X)return Vector3.right;
            return nearest==level.height-1-cell.Z?Vector3.forward:Vector3.left;
        }
        public static Vector3 Centre(LevelData level,Cell cell,float distance)
        {
            var forward=Outward(level,cell);var side=Vector3.Cross(Vector3.up,forward);
            float authoredEnd=AuthoredEnd(level,cell);
            float bendStart=Mathf.Max(.6f,authoredEnd);
            float fade=Mathf.SmoothStep(0,1,Mathf.Clamp01((distance-bendStart)/1.4f));
            float phase=(level.decorSeed%97)*.09f;
            float bend=(Mathf.Sin(Mathf.Max(0,distance-bendStart)*.48f)*.55f+Mathf.Sin(distance*.95f+phase)*.12f)*fade;
            float sign=((level.decorSeed^cell.X^cell.Z)&1)==0?1:-1;
            var point=BoardMath.CellCenterWorld(level,cell)+forward*distance+side*bend*sign;
            point.y=authoredEnd>0
                ? Mathf.Lerp(.024f,.003f,Mathf.SmoothStep(0,1,Mathf.Clamp01((distance-authoredEnd)/.45f)))
                : Mathf.Lerp(.022f,.003f,Mathf.SmoothStep(0,1,Mathf.Clamp01((distance-.35f)/.4f)));
            return point;
        }
        // Continue the scenic exit beyond a straight authored run, rather than
        // laying another transparent ribbon over the same walking cells.
        static float AuthoredEnd(LevelData level,Cell access)
        {
            if(level.ruleVersion!=2||(level.exteriorWalkable?.Length??0)==0||!CampAccess.IsReserved(level,access))return 0;
            var outward=Outward(level,access);int dx=Mathf.RoundToInt(outward.x),dz=Mathf.RoundToInt(outward.z),count=0;
            for(int step=1;step<=level.exteriorWalkable.Length;step++)
            {
                var next=new Cell(access.X+dx*step,access.Z+dz*step);
                if(RuleEvaluator.Inside(level,next)||!CampWalkability.Contains(level,next))break;
                count=step;
            }
            return count>0?count+.5f:0;
        }
        public static bool IsCorridor(LevelData level,Vector3 position,float radius)
            => CorridorDistance(level,position)<radius;

        /// <summary>Signed clearance from the same visual walking network. Used for soft banks;
        /// this is presentation geometry, never a replacement for puzzle walkability.</summary>
        public static float CorridorDistance(LevelData level,Vector3 position)
        {
            float nearest=float.PositiveInfinity;
            foreach(var authored in level.ruleVersion==2?level.exteriorWalkable??System.Array.Empty<int[]>():System.Array.Empty<int[]>())
            {
                if(authored?.Length!=2)continue;
                var at=BoardMath.CellCenterWorld(level,new Cell(authored[0],authored[1]));
                nearest=Mathf.Min(nearest,Mathf.Max(Mathf.Abs(position.x-at.x),Mathf.Abs(position.z-at.z))-.52f);
            }
            foreach(var cell in CampAccess.Points(level))
            {
                var previous=Centre(level,cell,0);
                for(int i=1;i<=12;i++)
                {
                    var next=Centre(level,cell,Length*i/12f);
                    var delta=next-previous;delta.y=0;
                    var offset=position-previous;offset.y=0;
                    float t=Mathf.Clamp01(Vector3.Dot(offset,delta)/Mathf.Max(.001f,delta.sqrMagnitude));
                    nearest=Mathf.Min(nearest,(offset-delta*t).magnitude-.48f);
                    previous=next;
                }
            }
            return nearest;
        }
        public static void BuildAll(LevelData level,Transform parent)
        {
            if(level.ruleVersion==2&&(level.exteriorWalkable?.Length??0)>0)BuildAuthored(level,parent);
            foreach(var cell in CampAccess.Points(level))
            {
                var root=new GameObject("ForestTrail_"+cell.X+"_"+cell.Z);root.transform.SetParent(parent,false);
                root.layer=BoardRenderer.DecorLayer;root.AddComponent<CampTrail>().Build(level,cell);
            }
        }
        static void BuildAuthored(LevelData level,Transform parent)
        {
            var cells=new HashSet<Cell>();
            foreach(var p in level.exteriorWalkable)if(p?.Length==2)cells.Add(new Cell(p[0],p[1]));
            var vertices=new List<Vector3>();var uv=new List<Vector2>();var joins=new List<Vector4>();var triangles=new List<int>();
            var continuations=new Dictionary<Cell,Vector4>();
            foreach(var access in CampAccess.Points(level))
            {
                float end=AuthoredEnd(level,access);if(end<=0)continue;
                var direction=Outward(level,access);int count=Mathf.RoundToInt(end-.5f);
                var last=new Cell(access.X+Mathf.RoundToInt(direction.x)*count,access.Z+Mathf.RoundToInt(direction.z)*count);
                continuations.TryGetValue(last,out var mask);
                if(direction.z>0)mask.x=1;if(direction.x>0)mask.y=1;if(direction.z<0)mask.z=1;if(direction.x<0)mask.w=1;
                continuations[last]=mask;
            }
            // One quad owns each cell. The shader unions its arms before blending,
            // so bends, branches and dead ends never accumulate soil opacity.
            var delta=new[]{new Cell(0,1),new Cell(1,0),new Cell(0,-1),new Cell(-1,0)};
            foreach(var cell in cells)
            {
                var at=BoardMath.CellCenterWorld(level,cell)+Vector3.up*.024f;
                continuations.TryGetValue(cell,out var mask);
                for(int direction=0;direction<delta.Length;direction++)
                {
                    var d=delta[direction];
                    var next=new Cell(cell.X+d.X,cell.Z+d.Z);
                    if(CampWalkability.IsWalkable(level,next))mask[direction]=1;
                }
                int i=vertices.Count;
                vertices.Add(at+new Vector3(-.5f,0,-.5f));vertices.Add(at+new Vector3(.5f,0,-.5f));
                vertices.Add(at+new Vector3(-.5f,0,.5f));vertices.Add(at+new Vector3(.5f,0,.5f));
                uv.AddRange(new[]{new Vector2(-.5f,-.5f),new Vector2(.5f,-.5f),new Vector2(-.5f,.5f),new Vector2(.5f,.5f)});
                for(int corner=0;corner<4;corner++)joins.Add(mask);
                triangles.AddRange(new[]{i,i+2,i+1,i+1,i+2,i+3});
            }
            var mesh=new Mesh{name="Authored exterior walking trail"};mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetUVs(1,joins);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
            var go=new GameObject("AuthoredExteriorTrail");go.transform.SetParent(parent,false);go.layer=BoardRenderer.DecorLayer;
            go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<OwnedCampMesh>().Mesh=mesh;
            var material=new Material(Resources.Load<Shader>("QuietCamp/GroundTrail")){name="Authored trail soil"};material.SetFloat("_Length",12);material.SetFloat("_AuthoredNetwork",1);
            ApplySeason(material,level);
            go.AddComponent<OwnedCampMaterial>().Material=material;var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.receiveShadows=true;renderer.shadowCastingMode=ShadowCastingMode.Off;
            RainSurface.Attach(go);
        }
        void Build(LevelData level,Cell cell)
        {
            AccessCell=cell;OuterEnd=Centre(level,cell,Length);
            var vertices=new List<Vector3>();var uv=new List<Vector2>();var indices=new List<int>();
            float authoredEnd=AuthoredEnd(level,cell);
            void Segment(float start,float end)
            {
                int steps=Mathf.Max(1,Mathf.CeilToInt(Steps*(end-start)/(Length+.35f))),first=vertices.Count;
                for(int i=0;i<=steps;i++)
                {
                    float distance=Mathf.Lerp(start,end,i/(float)steps);
                    var point=Centre(level,cell,distance);
                    var tangent=Centre(level,cell,distance+.02f)-Centre(level,cell,distance-.02f);tangent.y=0;
                    var side=Vector3.Cross(Vector3.up,tangent.normalized);
                    float width=Mathf.Lerp(.35f,.26f,Mathf.Clamp01(distance/Length));
                    float joinFade=authoredEnd>0?Mathf.Clamp01((distance-authoredEnd)/.8f):1;
                    width*=1+joinFade*(.16f*Mathf.Sin(distance*2.34f+cell.X+cell.Z)+.06f*Mathf.Sin(distance*5.5f+(level.decorSeed%89)*.071f));
                    if(authoredEnd>0)width=Mathf.Lerp(.31f,width,joinFade);
                    vertices.Add(point-side*width);vertices.Add(point+side*width);
                    uv.Add(new Vector2(-1,distance));uv.Add(new Vector2(1,distance));
                    if(i==steps)continue;
                    int a=first+i*2;indices.AddRange(new[]{a,a+2,a+1,a+1,a+2,a+3});
                }
            }
            if(authoredEnd>0){Segment(-.35f,.5f);Segment(authoredEnd,Length);}
            else Segment(-.35f,Length);
            _mesh=new Mesh{name="Worn forest footpath"};_mesh.SetVertices(vertices);_mesh.SetUVs(0,uv);_mesh.SetTriangles(indices,0);
            _mesh.RecalculateNormals();_mesh.RecalculateBounds();gameObject.AddComponent<MeshFilter>().sharedMesh=_mesh;
            _material=new Material(Resources.Load<Shader>("QuietCamp/GroundTrail"));_material.SetFloat("_Length",Length);
            ApplySeason(_material,level);
            var renderer=gameObject.AddComponent<MeshRenderer>();renderer.sharedMaterial=_material;
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=true;
            BuildPebbles(level,cell);
            RainSurface.Attach(gameObject);
        }
        static void ApplySeason(Material material,LevelData level)
        {
            var season=SeasonProfile.For(level);var palette=season.Palette;
            material.SetColor("_SoilDark",season.Winter?palette.GrassDark*.82f:palette.Soil*.86f);
            material.SetColor("_SoilLight",season.Winter?palette.GrassLight*.88f:palette.Soil*1.06f);
            material.SetColor("_MossDark",palette.GrassDark);material.SetColor("_MossLight",palette.GrassLight);
        }
        void BuildPebbles(LevelData level,Cell cell)
        {
            var vertices=new Vector3[5*12];var indices=new int[vertices.Length];
            for(int stone=0;stone<5;stone++)
            {
                float distance=1.1f+stone*.75f;var centre=Centre(level,cell,distance);
                var side=Vector3.Cross(Vector3.up,Outward(level,cell));centre+=side*((stone%2==0?-1:1)*.37f);
                float size=.025f+.012f*(stone%3);var a=centre+Vector3.left*size;var b=centre+Vector3.forward*size;
                var c=centre+Vector3.right*size;var d=centre+Vector3.back*size;var top=centre+Vector3.up*size*.7f;
                int v=stone*12;
                vertices[v]=a;vertices[v+1]=top;vertices[v+2]=b;
                vertices[v+3]=b;vertices[v+4]=top;vertices[v+5]=c;
                vertices[v+6]=c;vertices[v+7]=top;vertices[v+8]=d;
                vertices[v+9]=d;vertices[v+10]=top;vertices[v+11]=a;
            }
            for(int i=0;i<indices.Length;i+=3){indices[i]=i;indices[i+1]=i+2;indices[i+2]=i+1;}
            _pebbles=new Mesh{name="Trail edge pebbles"};_pebbles.vertices=vertices;_pebbles.triangles=indices;_pebbles.RecalculateNormals();_pebbles.RecalculateBounds();
            var go=new GameObject("TrailEdgePebbles");go.transform.SetParent(transform,false);go.layer=BoardRenderer.DecorLayer;
            go.AddComponent<MeshFilter>().sharedMesh=_pebbles;_pebbleMaterial=new Material(Shader.Find("Universal Render Pipeline/Simple Lit"));
            _pebbleMaterial.SetColor("_BaseColor",new Color(.42f,.40f,.32f));var renderer=go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial=_pebbleMaterial;renderer.shadowCastingMode=ShadowCastingMode.On;renderer.receiveShadows=true;
        }
        void OnDestroy(){if(_mesh!=null)Destroy(_mesh);if(_pebbles!=null)Destroy(_pebbles);if(_material!=null)Destroy(_material);if(_pebbleMaterial!=null)Destroy(_pebbleMaterial);}
    }
}
