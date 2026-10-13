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
            var m=new CampArtDefinition.Model{id=source.id,sourceModelId=source.sourceModelId,authoringRecipe=source.authoringRecipe,shading=source.shading,vertices=source.vertices,normals=source.normals,uv=source.uv,tangents=source.tangents,colors=source.colors.ToArray(),seasonalRole=source.seasonalRole,seasonalLeafTriangles=source.seasonalLeafTriangles};
            var leaf=new System.Collections.Generic.HashSet<int>(source.seasonalLeafTriangles??Array.Empty<int>());
            for(int i=0;i<m.colors.Length;i++)
            {
                var color=CampArtDefinition.Color(source.colors[i]);bool foliage=leaf.Contains(i/3)||source.seasonalRole=="conifer"&&color.g>color.r*1.04f||source.seasonalRole=="groundcover";
                if(foliage)
                {
                    var tint=preset.season=="autumn"?new Color(.70f,.46f,.14f):preset.season=="spring"?new Color(.38f,.53f,.22f):new Color(.32f,.43f,.18f);
                    if(preset.season!="winter"&&source.seasonalRole!="conifer")color=Color.Lerp(color,tint,preset.season=="autumn"?.68f:.27f);
                }
                if(preset.snowCover>0 && source.normals[i*3+1]>.18f)
                    color=Color.Lerp(color,new Color(.79f,.84f,.85f),Mathf.SmoothStep(0,1,source.normals[i*3+1]) * preset.snowCover);
                m.colors[i]=Pack(color);
            }
            if(preset.season=="winter" && (source.seasonalRole=="deciduous"||source.seasonalRole=="flower"||source.seasonalRole=="groundcover"))
            {
                var triangles=Enumerable.Range(0,source.colors.Length/3).Where(t=>source.seasonalRole=="deciduous"?!leaf.Contains(t):false).ToArray();
                m.vertices=triangles.SelectMany(t=>source.vertices.Skip(t*9).Take(9)).ToArray();
                m.normals=triangles.SelectMany(t=>source.normals.Skip(t*9).Take(9)).ToArray();
                m.colors=triangles.SelectMany(t=>m.colors.Skip(t*3).Take(3)).ToArray();
                m.uv=source.uv==null?null:triangles.SelectMany(t=>source.uv.Skip(t*6).Take(6)).ToArray();
                m.tangents=source.tangents==null?null:triangles.SelectMany(t=>source.tangents.Skip(t*12).Take(12)).ToArray();
            }
            return m;
        }
        public static int Pack(Color c)=>Mathf.RoundToInt(Mathf.Clamp01(c.r)*255)<<16|Mathf.RoundToInt(Mathf.Clamp01(c.g)*255)<<8|Mathf.RoundToInt(Mathf.Clamp01(c.b)*255);
    }
}
