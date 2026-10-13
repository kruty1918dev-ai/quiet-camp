using System;
using System.Collections.Generic;
using System.Linq;
using QuietCamp.Infrastructure;
using UnityEngine;
using UnityEngine.ProBuilder;

namespace QuietCamp.Editor.LevelAuthoring
{
    // Open surface clipping preserves a hollow body. ProBuilder supplies the structural rim.
    public static class AircraftBreakAuthoring
    {
        struct Vertex { public Vector3 p,n;public Color c; }
        public static CampArtDefinition.Model Repair(CampArtDefinition.Model source,bool fore)
        {
            var bounds=AuthoringPropCatalog.ModelBounds(source);float cut=fore?bounds.min.z+2f:bounds.max.z-2f;
            var output=new List<Vertex>();var edge=new List<Vector3>();
            float Distance(Vertex v)=>(v.p.z-cut)*(fore?1:-1);
            for(int k=0;k<source.colors.Length;k+=3)
            {
                var polygon=new List<Vertex>();
                for(int j=0;j<3;j++){int i=k+j;polygon.Add(new Vertex{p=new Vector3(source.vertices[i*3],source.vertices[i*3+1],source.vertices[i*3+2]),n=new Vector3(source.normals[i*3],source.normals[i*3+1],source.normals[i*3+2]),c=CampArtDefinition.Color(source.colors[i])});}
                var clipped=new List<Vertex>();
                for(int j=0;j<polygon.Count;j++)
                {
                    var a=polygon[j];var b=polygon[(j+1)%polygon.Count];float da=Distance(a),db=Distance(b);
                    if(da>=0)clipped.Add(a);
                    if((da>=0)!=(db>=0))
                    {
                        float t=da/(da-db);var v=new Vertex{p=Vector3.Lerp(a.p,b.p,t),n=Vector3.Lerp(a.n,b.n,t).normalized,c=Color.Lerp(a.c,b.c,t)};clipped.Add(v);
                        if(Mathf.Abs(v.p.x)<2.7f)edge.Add(v.p);
                    }
                }
                for(int j=1;j+1<clipped.Count;j++){output.Add(clipped[0]);output.Add(clipped[j]);output.Add(clipped[j+1]);}
            }
            if(edge.Count>=8)
            {
                float low=edge.Min(v=>v.y),high=edge.Max(v=>v.y),left=edge.Min(v=>v.x),right=edge.Max(v=>v.x);
                float cx=(left+right)*.5f,cy=(low+high)*.5f,rx=Mathf.Min(2.6f,(right-left)*.5f),ry=Mathf.Min(2.4f,(high-low)*.5f);
                var vertices=new List<Vector3>();var faces=new List<Face>();
                for(int j=0;j<12;j++)
                {
                    float angle=j*Mathf.PI/6,next=(j+1)*Mathf.PI/6;int index=vertices.Count;
                    Vector3 P(float a,float inset,float depth)=>new Vector3(cx+Mathf.Cos(a)*(rx-inset),cy+Mathf.Sin(a)*(ry-inset),cut+depth*(fore?1:-1));
                    vertices.AddRange(new[]{P(angle,0,0),P(next,0,0),P(next,.12f,0),P(angle,.12f,0),P(angle,.12f,.38f),P(next,.12f,.38f)});
                    faces.Add(new Face(new[]{index,index+1,index+2,index,index+2,index+3}));
                    faces.Add(new Face(new[]{index+3,index+2,index+5,index+3,index+5,index+4}));
                }
                var pb=ProBuilderMesh.Create(vertices,faces);pb.gameObject.hideFlags=HideFlags.HideAndDontSave;
                try
                {
                    var tint=new Color(.31f,.34f,.29f);pb.colors=Enumerable.Repeat(QualitySettings.activeColorSpace==ColorSpace.Linear?tint.linear:tint,vertices.Count).ToArray();pb.ToMesh();pb.Refresh();
                    var rim=ModelAuthoringWorkshop.ReadMesh(pb.GetComponent<MeshFilter>().sharedMesh,source.sourceModelId??source.id);
                    for(int j=0;j<rim.colors.Length;j++)output.Add(new Vertex{p=new Vector3(rim.vertices[j*3],rim.vertices[j*3+1],rim.vertices[j*3+2]),n=new Vector3(rim.normals[j*3],rim.normals[j*3+1],rim.normals[j*3+2]),c=CampArtDefinition.Color(rim.colors[j])});
                }
                finally{UnityEngine.Object.DestroyImmediate(pb.gameObject);}
            }
            int Pack(Color c)=>Mathf.RoundToInt(c.r*255)<<16|Mathf.RoundToInt(c.g*255)<<8|Mathf.RoundToInt(c.b*255);
            var model=new CampArtDefinition.Model{shading=source.shading,sourceModelId=source.sourceModelId??source.id,vertices=output.SelectMany(v=>new[]{v.p.x,v.p.y,v.p.z}).ToArray(),normals=output.SelectMany(v=>new[]{v.n.x,v.n.y,v.n.z}).ToArray(),colors=output.Select(v=>Pack(v.c)).ToArray()};
            return ModelAuthoringWorkshop.Register(model,new ModelAuthoringRecipe{sourceId=source.id,operation="external",backend="surface-trim-probuilder-rim",label=fore?"Передня секція — відкритий каркас":"Хвостова секція — відкритий каркас",category="Великі об’єкти"}).model;
        }
    }
}
