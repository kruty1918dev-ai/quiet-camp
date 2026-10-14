using System;
using System.Linq;
using QuietCamp.Infrastructure;

namespace QuietCamp.Editor.LevelAuthoring
{
    public static class SeasonAtmosphereAuthoring
    {
        public static void Apply(LevelAuthoringDocument document,SeasonAtmospherePreset preset)
        {
            document.art.atmosphere=SeasonAtmosphereCatalog.Copy(preset);
            WeatherModelRigAuthoring.Prepare(document);
            Annotate(document);
        }
        public static void Annotate(LevelAuthoringDocument document)
        {
            document.art.models=document.art.models.Select(model=>
            {
                var entry=LevelAuthoringPalette.Entries.FirstOrDefault(e=>e.id==(model.sourceModelId??model.id))??LevelAuthoringPalette.Entries.FirstOrDefault(e=>e.id==model.id);
                string identity=(model.sourceModelId??entry?.id??model.id).ToLowerInvariant();
                var role=entry?.category=="Дерева"?(identity.Contains("pine")||identity.Contains("conifer")||identity.Contains("spruce")?"conifer":"deciduous"):
                    entry?.category=="Кущі"?"deciduous":entry?.category=="Квіти"?"flower":entry?.smallPlant==true?"groundcover":"solid";
                var source=entry?.model;var copy=new CampArtDefinition.Model{id=model.id,sourceModelId=model.sourceModelId,authoringRecipe=model.authoringRecipe,shading=model.shading,vertices=model.vertices,normals=model.normals,colors=model.colors,uv=model.uv,tangents=model.tangents,seasonalRole=model.seasonalRole??role,weatherExposure=model.weatherExposure,seasonalGrowth=model.seasonalGrowth,mossCoverage=model.mossCoverage};
                if(copy.seasonalRole=="deciduous")
                {
                    var colors=source!=null&&source.colors.Length==model.colors.Length?source.colors:model.colors;
                    copy.seasonalLeafTriangles=Enumerable.Range(0,colors.Length/3).Where(t=>Enumerable.Range(t*3,3).Count(i=>{var c=CampArtDefinition.Color(colors[i]);return c.g>c.r*1.015f&&c.g>c.b*1.1f;})>=2).ToArray();
                    if(model.seasonalLeafTriangles!=null)copy.seasonalLeafTriangles=model.seasonalLeafTriangles;
                }
                return copy;
            }).ToArray();
        }
    }
}
