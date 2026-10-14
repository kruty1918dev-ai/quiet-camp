using System.Collections.Generic;
using System.Linq;
using QuietCamp.Infrastructure;
using UnityEngine;
using UnityEngine.Rendering;

namespace QuietCamp.Presentation.World
{
    public static class SeasonSurfaceAccents
    {
        public static Mesh Build(CampArtDefinition art,SeasonAtmospherePreset preset)
        {
            if(preset.season!="spring"&&preset.season!="autumn")return null;
            var models=art.models.ToDictionary(m=>m.id);
            var anchors=art.instances.Where(i=>i.modelId!=null&&models.TryGetValue(i.modelId,out var m)&&(m.seasonalRole=="deciduous"||m.seasonalRole=="flower")).ToArray();
            if(anchors.Length==0)return null;
            var random=new System.Random(art.seed^preset.seed^710);var vertices=new List<Vector3>();var colors=new List<Color>();var response=new List<Vector2>();
            void Triangle(Vector3 a,Vector3 b,Vector3 c,Color color)
            {vertices.AddRange(new[]{a,b,c});var tint=QualitySettings.activeColorSpace==ColorSpace.Linear?color.linear:color;colors.AddRange(new[]{tint,tint,tint});response.AddRange(new[]{Vector2.zero,new Vector2(.15f,.25f),Vector2.zero});}
            int count=preset.season=="spring"?(preset.id=="spring_sunny"?260:140):300;
            for(int i=0;i<count;i++)
            {
                var anchor=anchors[preset.season=="spring"?(i/12)%anchors.Length:random.Next(anchors.Length)];float angle=(float)random.NextDouble()*Mathf.PI*2,radius=.5f+(float)random.NextDouble()*(preset.season=="spring"?1.1f:2.6f);
                float x=anchor.x+Mathf.Cos(angle)*radius,z=anchor.z+Mathf.Sin(angle)*radius;

                float y=art.GroundHeight(x,z);if(y<-.055f)continue;
                var root=new Vector3(x,y+.035f,z);
                if(preset.season=="spring")
                {
                    root.y+=.13f;float size=.10f+(float)random.NextDouble()*.055f;
                    var tint=i%3==0?new Color(.88f,.7f,.72f):new Color(.94f,.91f,.76f);
                    for(int petal=0;petal<5;petal++)
                    {float a=petal*Mathf.PI*.4f;var tip=root+new Vector3(Mathf.Cos(a),.12f,Mathf.Sin(a))*size;var side=new Vector3(-Mathf.Sin(a),0,Mathf.Cos(a))*size*.45f;Triangle(root,tip-side,tip+side,tint);}
                }
                else
                {
                    float size=.07f+(float)random.NextDouble()*.065f;var tip=new Vector3(Mathf.Cos(angle),.05f,Mathf.Sin(angle))*size;
                    var side=new Vector3(-Mathf.Sin(angle),0,Mathf.Cos(angle))*size*.45f;
                    Triangle(root-tip,root+side,root+tip,i%2==0?new Color(.66f,.38f,.1f):new Color(.76f,.55f,.18f));
                    Triangle(root-tip,root+tip,root-side,i%2==0?new Color(.66f,.38f,.1f):new Color(.76f,.55f,.18f));
                }
            }
            var mesh=new Mesh{name="Seasonal bloom and fallen leaves",indexFormat=IndexFormat.UInt32};mesh.SetVertices(vertices);mesh.SetColors(colors);mesh.SetUVs(1,response);mesh.SetTriangles(Enumerable.Range(0,vertices.Count).ToArray(),0);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
    }
}
