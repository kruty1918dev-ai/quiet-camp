using System.Collections.Generic;
using UnityEngine;

namespace QuietCamp.Presentation.World
{
    /// <summary>Managed seasonal geometry, shared with the roadmap; source meshes remain untouched.</summary>
    public static class SeasonalTreeGeometry
    {
        public sealed class Geometry
        {
            public Vector3[] Vertices,Normals;public Color[] Colors;public int[] Indices;public Bounds Bounds;
            public float Height=>Mathf.Max(.01f,Bounds.size.y);
        }
        public static Geometry Read(GameObject root)
        {
            var vertices=new List<Vector3>();var normals=new List<Vector3>();var colors=new List<Color>();var indices=new List<int>();
            foreach(var filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh=filter.sharedMesh;var renderer=filter.GetComponent<Renderer>();if(mesh==null||!mesh.isReadable||renderer==null)continue;
                var matrix=root.transform.worldToLocalMatrix*filter.transform.localToWorldMatrix;
                var normalMatrix=matrix.inverse.transpose;
                var source=mesh.vertices;var ns=mesh.normals;var materials=renderer.sharedMaterials;
                for(int slot=0;slot<mesh.subMeshCount;slot++)
                {
                    var material=slot<materials.Length?materials[slot]:null;
                    var color=material!=null&&material.HasProperty("_BaseColor")?material.GetColor("_BaseColor"):Color.white;
                    foreach(int index in mesh.GetTriangles(slot))
                    {indices.Add(vertices.Count);vertices.Add(matrix.MultiplyPoint3x4(source[index]));normals.Add(normalMatrix.MultiplyVector(index<ns.Length?ns[index]:Vector3.up).normalized);colors.Add(color);}
                }
            }
            return Finish(vertices,normals,colors,indices);
        }
        public static Geometry Bare(Vector3[] source,Vector3[] sourceNormals,int[] sourceIndices,Color[] sourceColors)
        {
            if(source.Length==0)return Finish(new List<Vector3>(),new List<Vector3>(),new List<Color>(),new List<int>());
            var bounds=new Bounds(source[0],Vector3.zero);foreach(var p in source)bounds.Encapsulate(p);
            float h=Mathf.Max(.01f,bounds.size.y);var centre=bounds.center;float bottom=bounds.min.y;
            var vertices=new List<Vector3>();var normals=new List<Vector3>();var colors=new List<Color>();var indices=new List<int>();
            float woodTop=bottom;Vector3 stem=centre;
            for(int i=0;i<sourceIndices.Length;i+=3)
            {
                int a=sourceIndices[i];var color=sourceColors[a];if(color.g>color.r*1.03f)continue;
                for(int j=0;j<3;j++)
                {
                    int index=sourceIndices[i+j];var p=source[index];if(p.y>woodTop){woodTop=p.y;stem=p;}
                    indices.Add(vertices.Count);vertices.Add(p);normals.Add(sourceNormals[index]);colors.Add(sourceColors[index]);
                }
            }
            void Triangle(Vector3 a,Vector3 b,Vector3 c,Color color)
            {
                int start=vertices.Count;var n=Vector3.Cross(b-a,c-a).normalized;
                vertices.Add(a);vertices.Add(b);vertices.Add(c);
                for(int i=0;i<3;i++){normals.Add(n);colors.Add(color);indices.Add(start+i);}
            }
            var bark=new Color(.37f,.28f,.23f);
            void Rod(Vector3 a,Vector3 b,float radius)
            {
                var axis=(b-a).normalized;var side=Vector3.Cross(axis,Vector3.up);
                if(side.sqrMagnitude<.001f)side=Vector3.right;else side.Normalize();
                var up=Vector3.Cross(side,axis).normalized;
                Vector3 Ring(Vector3 p,int i,float r){float angle=i*Mathf.PI*.5f;return p+(side*Mathf.Cos(angle)+up*Mathf.Sin(angle))*r;}
                for(int i=0;i<4;i++)
                {
                    var p=Ring(a,i,radius);var q=Ring(a,i+1,radius);var r=Ring(b,i,radius*.48f);var s=Ring(b,i+1,radius*.48f);
                    Triangle(p,r,s,bark);Triangle(p,s,q,bark);Triangle(a,q,p,bark);Triangle(b,r,s,bark);
                }
            }
            // Keep the original trunk. Nine boughs with one fork each give a
            // recognizable deciduous silhouette at a bounded mobile cost.
            var tip=new Vector3(centre.x,bounds.max.y,centre.z);
            stem=new Vector3(centre.x,Mathf.Max(bottom+h*.27f,woodTop-h*.06f),centre.z);
            Rod(stem,tip,h*.022f);
            for(int tier=0;tier<3;tier++)for(int branch=0;branch<3;branch++)
            {
                float angle=branch*Mathf.PI*2/3+tier*.77f;
                var direction=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));
                var at=new Vector3(centre.x,bottom+h*(.38f+tier*.16f),centre.z);
                float radius=.82f-tier*.13f;
                var end=at+Vector3.Scale(direction,new Vector3(bounds.extents.x,0,bounds.extents.z))*radius+Vector3.up*h*.17f;
                Rod(at,end,h*(.018f-tier*.003f));
                var fork=Vector3.Lerp(at,end,.57f);var tangent=new Vector3(-direction.z,0,direction.x);
                var terminal=end+tangent*h*.065f+Vector3.up*h*.07f;
                terminal.x=Mathf.Clamp(terminal.x,bounds.min.x+.015f,bounds.max.x-.015f);
                terminal.z=Mathf.Clamp(terminal.z,bounds.min.z+.015f,bounds.max.z-.015f);
                Rod(fork,terminal,h*.008f);
            }
            return Finish(vertices,normals,colors,indices);
        }
        static Geometry Finish(List<Vector3> vertices,List<Vector3> normals,List<Color> colors,List<int> indices)
        {
            var bounds=new Bounds();if(vertices.Count>0){bounds=new Bounds(vertices[0],Vector3.zero);foreach(var p in vertices)bounds.Encapsulate(p);}
            return new Geometry{Vertices=vertices.ToArray(),Normals=normals.ToArray(),Colors=colors.ToArray(),Indices=indices.ToArray(),Bounds=bounds};
        }
    }
}
