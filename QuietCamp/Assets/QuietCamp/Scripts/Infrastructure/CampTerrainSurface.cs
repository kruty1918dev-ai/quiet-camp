using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace QuietCamp.Infrastructure
{
    /// <summary>Queries the triangles actually displayed, including the distant margins.
    /// The authored height field and source geometry remain untouched.</summary>
    public sealed class CampTerrainSurface
    {
        const int Side=32;
        readonly List<(Vector3 a,Vector3 b,Vector3 c)>[] _cells=new List<(Vector3,Vector3,Vector3)>[Side*Side];
        readonly FrozenLandscape _landscape;
        public Rect Bounds {get;}
        int X(float x)=>Mathf.Clamp((int)((x-Bounds.xMin)/Bounds.width*Side),0,Side-1);
        int Z(float z)=>Mathf.Clamp((int)((z-Bounds.yMin)/Bounds.height*Side),0,Side-1);
        public CampTerrainSurface(FrozenLandscape landscape)
        {
            _landscape=landscape;
            var meshes=landscape.meshes.Where(m=>!m.water&&!m.decoration).ToArray();
            float x0=landscape.originX,z0=landscape.originZ,x1=x0+landscape.size,z1=z0+landscape.size;
            foreach(var mesh in meshes)for(int i=0;i<mesh.vertices.Length;i+=3)
            {x0=Math.Min(x0,mesh.vertices[i]);x1=Math.Max(x1,mesh.vertices[i]);z0=Math.Min(z0,mesh.vertices[i+2]);z1=Math.Max(z1,mesh.vertices[i+2]);}
            Bounds=Rect.MinMaxRect(x0,z0,x1,z1);
            foreach(var mesh in meshes)for(int i=0;i<mesh.triangles.Length;i+=3)
            {
                Vector3 Point(int k){int at=mesh.triangles[k]*3;return new Vector3(mesh.vertices[at],mesh.vertices[at+1],mesh.vertices[at+2]);}
                var a=Point(i);var b=Point(i+1);var c=Point(i+2);
                for(int z=Z(Math.Min(a.z,Math.Min(b.z,c.z)));z<=Z(Math.Max(a.z,Math.Max(b.z,c.z)));z++)
                for(int x=X(Math.Min(a.x,Math.Min(b.x,c.x)));x<=X(Math.Max(a.x,Math.Max(b.x,c.x)));x++)
                {int cell=z*Side+x;if(_cells[cell]==null)_cells[cell]=new List<(Vector3,Vector3,Vector3)>();_cells[cell].Add((a,b,c));}
            }
        }
        public static bool Height(Vector3 a,Vector3 b,Vector3 c,float x,float z,out float y)
        {
            y=0;float determinant=(b.z-c.z)*(a.x-c.x)+(c.x-b.x)*(a.z-c.z);
            if(Math.Abs(determinant)<1e-10f)return false;
            float u=((b.z-c.z)*(x-c.x)+(c.x-b.x)*(z-c.z))/determinant;
            float v=((c.z-a.z)*(x-c.x)+(a.x-c.x)*(z-c.z))/determinant;
            if(u<-.00001f||v<-.00001f||u+v>1.00001f)return false;
            y=u*a.y+v*b.y+(1-u-v)*c.y;return true;
        }
        public float Sample(float x,float z)
        {
            var cell=_cells[Z(z)*Side+X(x)];
            if(cell!=null)foreach(var t in cell)if(Height(t.a,t.b,t.c,x,z,out float y))return y;
            return _landscape.SampleHeight(x,z);
        }
        public Vector3 Position(CampArtDefinition art,CampArtDefinition.Instance item)
        {
            var p=new Vector3(item.x,item.y,item.z);
            if(item.surfaceAttached==true&&!item.id.StartsWith("river-bank/",StringComparison.Ordinal))
                p.y+=Sample(p.x,p.z)-art.landscape.SampleHeight(p.x,p.z);
            return p;
        }
    }
}
