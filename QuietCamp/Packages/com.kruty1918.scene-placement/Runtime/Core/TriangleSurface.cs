using System;
using System.Collections.Generic;

namespace Kruty1918.ScenePlacement
{
    /// <summary>A copied static mesh, indexed in XZ. Highest Y-up surface is sampled; no scene/global physics.</summary>
    public sealed class TriangleSurface : ISurfaceQuery
    {
        readonly MeshData mesh;
        readonly Dictionary<(int,int),List<int>> cells=new Dictionary<(int,int),List<int>>();
        readonly float cellSize;
        readonly string tag;
        public TriangleSurface(MeshData source,float cellSize=2,string tag="ground",int maximumTriangles=100000)
        {
            source.Validate();if(cellSize<=0||!Numbers.Finite(cellSize)||source.TriangleCount>maximumTriangles)throw new ArgumentException("Bounded mesh and positive cell size required");
            mesh=source.Copy();this.cellSize=cellSize;this.tag=tag;
            long memberships=0;
            for(int t=0;t<mesh.TriangleCount;t++)
            {
                var a=mesh.positions[mesh.indices[t*3]];var b=mesh.positions[mesh.indices[t*3+1]];var c=mesh.positions[mesh.indices[t*3+2]];
                int x0=Cell(Math.Min(a.x,Math.Min(b.x,c.x))),x1=Cell(Math.Max(a.x,Math.Max(b.x,c.x)));
                int z0=Cell(Math.Min(a.z,Math.Min(b.z,c.z))),z1=Cell(Math.Max(a.z,Math.Max(b.z,c.z)));
                memberships+=(long)(x1-x0+1)*(z1-z0+1);if(memberships>1000000)throw new ArgumentException("Surface spatial-index limit; increase cellSize");
                for(int z=z0;z<=z1;z++)for(int x=x0;x<=x1;x++){if(!cells.TryGetValue((x,z),out var list))cells.Add((x,z),list=new List<int>());list.Add(t);}
            }
        }
        int Cell(float f)=>(int)Math.Floor(f/cellSize);
        public bool TrySample(V3 target,V3 direction,out SurfaceSample sample)
        {
            sample=default;if(!target.Finite||Math.Abs(direction.y)<.999f||Math.Abs(direction.x)>.001f||Math.Abs(direction.z)>.001f||!cells.TryGetValue((Cell(target.x),Cell(target.z)),out var list))return false;
            bool found=false;float top=float.NegativeInfinity;
            foreach(int t in list)
            {
                var a=mesh.positions[mesh.indices[t*3]];var b=mesh.positions[mesh.indices[t*3+1]];var c=mesh.positions[mesh.indices[t*3+2]];
                var ab=b-a;var ac=c-a;float determinant=ab.x*ac.z-ab.z*ac.x;if(Math.Abs(determinant)<1e-9f)continue;
                var p=target-a;float u=(p.x*ac.z-p.z*ac.x)/determinant,v=(ab.x*p.z-ab.z*p.x)/determinant;
                if(u<-.00001f||v<-.00001f||u+v>1.00001f)continue;
                float y=a.y+u*ab.y+v*ac.y;if(y<=top)continue;top=y;var n=V3.Cross(ab,ac).Normalized;if(n.y<0)n=-n;
                sample=new SurfaceSample{point=new V3(target.x,y,target.z),normal=n,tag=tag,light=1};found=true;
            }
            return found;
        }
    }

    [Serializable] public sealed class SurfaceRecipe
    {
        public float groundHeight;
        public float[] heights=Array.Empty<float>();public int width,depth;public float spacing=1;public V3 origin;
        public MeshData mesh;
        public ISurfaceQuery Create()=>mesh!=null?new TriangleSurface(mesh):heights.Length>0?new HeightFieldSurface(heights,width,depth,spacing,origin):new PlaneSurface(groundHeight);
    }
}
