using System;
using System.Linq;
using QuietCamp.Infrastructure;
using UnityEngine;

namespace QuietCamp.Presentation.World
{
    public static class SeasonModelAppearance
    {
        public static CampArtDefinition.Model Resolve(CampArtDefinition.Model source,SeasonAtmospherePreset preset)
        {
            if(preset==null||string.IsNullOrEmpty(source.seasonalRole))return source;
            var m=new CampArtDefinition.Model{id=source.id,sourceModelId=source.sourceModelId,authoringRecipe=source.authoringRecipe,shading=source.shading,vertices=source.vertices,normals=source.normals,uv=source.uv,tangents=source.tangents,colors=source.colors.ToArray(),seasonalRole=source.seasonalRole,seasonalLeafTriangles=source.seasonalLeafTriangles,weatherExposure=source.weatherExposure,seasonalGrowth=source.seasonalGrowth,mossCoverage=source.mossCoverage};
            if(source.seasonalGrowth!=null)
            {
                int season=preset.season=="spring"?0:preset.season=="summer"?1:preset.season=="autumn"?2:3;
                float growth=source.seasonalGrowth[season];
                m.vertices=source.vertices.ToArray();float bottom=float.MaxValue;
                for(int j=1;j<m.vertices.Length;j+=3)bottom=Mathf.Min(bottom,m.vertices[j]);
                for(int j=0;j<m.vertices.Length;j+=3){m.vertices[j]*=growth;m.vertices[j+1]=bottom+(m.vertices[j+1]-bottom)*growth;m.vertices[j+2]*=growth;}
            }
            var leaf=new System.Collections.Generic.HashSet<int>(source.seasonalLeafTriangles??Array.Empty<int>());
            for(int i=0;i<m.colors.Length;i++)
            {
                var color=CampArtDefinition.Color(source.colors[i]);bool foliage=leaf.Contains(i/3)||source.seasonalRole=="conifer"&&color.g>color.r*1.04f||source.seasonalRole=="groundcover";
                if(foliage)
                {
                    var tint=preset.season=="autumn"?new Color(.70f,.46f,.14f):preset.season=="spring"?new Color(.38f,.53f,.22f):new Color(.32f,.43f,.18f);
                    if(preset.season!="winter"&&source.seasonalRole!="conifer")color=Color.Lerp(color,tint,preset.season=="autumn"?.68f:.27f);
                }
                if(source.mossCoverage!=null)
                {
                    var moss=preset.season=="winter"?new Color(.31f,.36f,.26f):preset.season=="autumn"?new Color(.30f,.35f,.15f):new Color(.27f,.38f,.16f);
                    color=Color.Lerp(color,moss,source.mossCoverage[i]*(.75f+preset.wetness*.25f));
                }
                // Snow is applied in world space by the weather material, after scene shelter
                // is known. Local normals alone paint interiors and rotated objects incorrectly.
                m.colors[i]=Pack(color);
            }
            if(preset.season=="autumn"&&source.seasonalRole=="deciduous"&&preset.leafLoss>0)
            {
                var groups=LeafPieces(source,leaf);
                int loss=groups.Length<2?0:Mathf.Min(groups.Length-1,Mathf.Max(1,Mathf.FloorToInt(groups.Length*preset.leafLoss)));
                if(groups.Length==1)
                {
                    m.vertices=m.vertices.ToArray();var points=groups[0].SelectMany(t=>new[]{t*9,t*9+3,t*9+6}).ToArray();
                    float x=(points.Min(i=>m.vertices[i])+points.Max(i=>m.vertices[i]))*.5f,z=(points.Min(i=>m.vertices[i+2])+points.Max(i=>m.vertices[i+2]))*.5f,y=points.Min(i=>m.vertices[i+1]);
                    float size=Mathf.Sqrt(1-preset.leafLoss*.9f);
                    foreach(int i in points){m.vertices[i]=x+(m.vertices[i]-x)*size;m.vertices[i+1]=y+(m.vertices[i+1]-y)*size;m.vertices[i+2]=z+(m.vertices[i+2]-z)*size;}
                }
                var missing=new System.Collections.Generic.HashSet<int>(groups.Take(loss).SelectMany(g=>g));
                Filter(m,Enumerable.Range(0,source.colors.Length/3).Where(t=>!missing.Contains(t)).ToArray(),leaf);
            }
            if(preset.season=="winter" && (source.seasonalRole=="deciduous"||source.seasonalRole=="flower"||source.seasonalRole=="groundcover"))
                Filter(m,Enumerable.Range(0,source.colors.Length/3).Where(t=>source.seasonalRole=="deciduous"?!leaf.Contains(t):false).ToArray(),leaf);
            return m;
        }
        static void Filter(CampArtDefinition.Model m,int[] triangles,System.Collections.Generic.HashSet<int> leaves)
        {
            // Validated model channels have a fixed number of values per triangle.
            // Copy each channel once without allocating enumerators for every face.
            m.vertices=SelectTriangles(m.vertices,triangles,9);
            m.normals=SelectTriangles(m.normals,triangles,9);
            m.colors=SelectTriangles(m.colors,triangles,3);
            m.mossCoverage=SelectTriangles(m.mossCoverage,triangles,3);
            m.weatherExposure=SelectTriangles(m.weatherExposure,triangles,3);
            m.uv=SelectTriangles(m.uv,triangles,6);
            m.tangents=SelectTriangles(m.tangents,triangles,12);
            int count=0;
            foreach(int triangle in triangles)if(leaves.Contains(triangle))count++;
            var mappedLeaves=new int[count];int next=0;
            for(int i=0;i<triangles.Length;i++)if(leaves.Contains(triangles[i]))mappedLeaves[next++]=i;
            m.seasonalLeafTriangles=mappedLeaves;
        }
        static T[] SelectTriangles<T>(T[] source,int[] triangles,int width)
        {
            if(source==null)return null;
            var selected=new T[triangles.Length*width];
            for(int i=0;i<triangles.Length;i++)Array.Copy(source,triangles[i]*width,selected,i*width,width);
            return selected;
        }
        static int[][] LeafPieces(CampArtDefinition.Model m,System.Collections.Generic.HashSet<int> leaves)
        {
            var parent=Enumerable.Range(0,m.colors.Length/3).ToArray();var vertices=new System.Collections.Generic.Dictionary<Vector3Int,int>();
            int Root(int i){while(parent[i]!=i){parent[i]=parent[parent[i]];i=parent[i];}return i;}
            foreach(int t in leaves)for(int k=0;k<3;k++)
            {
                int v=t*9+k*3;var key=new Vector3Int(Mathf.RoundToInt(m.vertices[v]*1000),Mathf.RoundToInt(m.vertices[v+1]*1000),Mathf.RoundToInt(m.vertices[v+2]*1000));
                if(vertices.TryGetValue(key,out var previous))parent[Root(t)]=Root(previous);else vertices[key]=t;
            }
            // Remove entire closed leaf clumps, never random faces with open holes.
            return leaves.GroupBy(Root).Select(g=>g.ToArray()).OrderBy(g=>g.Min(t=>m.vertices[t*9])*.37f+g.Max(t=>m.vertices[t*9+2])*.61f).ThenBy(g=>g.Min()).ToArray();
        }
        public static int Pack(Color c)=>Mathf.RoundToInt(Mathf.Clamp01(c.r)*255)<<16|Mathf.RoundToInt(Mathf.Clamp01(c.g)*255)<<8|Mathf.RoundToInt(Mathf.Clamp01(c.b)*255);
    }
}
