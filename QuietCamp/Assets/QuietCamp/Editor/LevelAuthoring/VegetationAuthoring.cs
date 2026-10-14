using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using QuietCamp.Infrastructure;
using UnityEngine;

namespace QuietCamp.Editor.LevelAuthoring
{
    [Serializable] public sealed class VegetationPreset
    {
        public string id,label,description;
        public int revision=1,attempts=1800;
        public float radius=2,spacing=.24f,height=.23f;
        public string[] models;
        public bool shoreline;
    }
    public static class VegetationAuthoring
    {
        sealed class Catalog{public VegetationPreset[] presets;}
        public static VegetationPreset[] Presets=>JsonConvert.DeserializeObject<Catalog>(Resources.Load<TextAsset>("QuietCamp/vegetation_presets").text).presets;
        public static int Apply(LevelAuthoringDocument d,VegetationPreset preset,int seed)
        {
            if(preset.id=="forest_recovery"){var settings=ForestRegrowthAuthoring.Defaults();settings.seed=seed;return ForestRegrowthAuthoring.Apply(d,settings);}
            AuthoringVegetationSnapshot.ExpandCover(d);
            var palette=LevelAuthoringPalette.Entries.ToDictionary(e=>e.id);
            var models=d.art.models.ToDictionary(m=>m.id);var originals=d.art.instances.Where(i=>!(i.id??"").StartsWith("vegetation/",StringComparison.Ordinal)).ToList();
            var anchors=originals.Where(i=>i.modelId!=null&&models.ContainsKey(i.modelId)&&new[]{"Дерева","Кущі","Квіти","Каміння","Паркани"}.Contains(palette.TryGetValue(models[i.modelId].sourceModelId??i.modelId,out var e)?e.category:""))
                .Select(i=>new Vector2(i.x,i.z)).ToArray();
            var selected=preset.models.Where(palette.ContainsKey).Select(id=>WorldStudyFactory.MetricModel(id)).ToArray();
            var solids=originals.Where(i=>i.modelId!=null&&models.ContainsKey(i.modelId)).Select(i=>new{instance=i,bounds=AuthoringPropCatalog.ModelBounds(models[i.modelId])})
                .Where(p=>models[p.instance.modelId].shading!="foliage"&&p.bounds.size.y>2).ToArray();
            if(selected.Length==0)throw new InvalidOperationException("Немає моделей для пресету зелені.");
            var random=new System.Random(seed);var occupied=new HashSet<Vector2Int>();
            Vector2Int Cell(float x,float z)=>new Vector2Int(Mathf.FloorToInt(x/preset.spacing),Mathf.FloorToInt(z/preset.spacing));
            foreach(var i in originals)occupied.Add(Cell(i.x,i.z));
            int added=0;float extent=Mathf.Min(18,d.art.surroundExtent*.6f);
            for(int attempt=0;attempt<preset.attempts;attempt++)
            {
                float x,z;
                if(anchors.Length>0 && attempt%3!=0){var a=anchors[random.Next(anchors.Length)];double angle=random.NextDouble()*Math.PI*2,radius=random.NextDouble()*preset.radius;x=a.x+(float)(Math.Cos(angle)*radius);z=a.y+(float)(Math.Sin(angle)*radius);}
                else {x=(float)(random.NextDouble()*2-1)*extent;z=(float)(random.NextDouble()*2-1)*extent;}
                if(Mathf.Abs(x)<d.level.width*.5f+.65f&&Mathf.Abs(z)<d.level.height*.5f+.65f)continue;
                float y=d.art.landscape?.SampleHeight(x,z)??d.art.GroundHeight(x,z);if(y<-.10f)continue;
                if(preset.shoreline)
                {
                    bool near=d.art.landscape!=null?d.art.landscape.SampleHeight(x+.8f,z)<-.15f||d.art.landscape.SampleHeight(x-.8f,z)<-.15f:Mathf.Abs(x-d.art.streamCentre)<d.art.streamWidth*.5f+2.3f;
                    if(!near)continue;
                }
                var c=Cell(x,z);bool close=false;for(int dx=-1;dx<=1;dx++)for(int dz=-1;dz<=1;dz++)if(occupied.Contains(c+new Vector2Int(dx,dz)))close=true;if(close)continue;
                // Avoid planting inside substantial solid objects. Use actual frozen bounds.
                bool solid=solids.Any(p=>{var point=Quaternion.Inverse(Quaternion.Euler(p.instance.rotationEuler==null?new Vector3(0,p.instance.yaw,0):CampArtDefinition.Vector(p.instance.rotationEuler)))*new Vector3(x-p.instance.x,0,z-p.instance.z);return Mathf.Abs(point.x-p.bounds.center.x*p.instance.scale)<p.bounds.extents.x*p.instance.scale+.3f&&Mathf.Abs(point.z-p.bounds.center.z*p.instance.scale)<p.bounds.extents.z*p.instance.scale+.3f;});
                if(solid)continue;
                var source=selected[random.Next(selected.Length)];var b=AuthoringPropCatalog.ModelBounds(source);float scale=preset.height/Mathf.Max(.01f,b.size.y)*Mathf.Lerp(.7f,1.25f,(float)random.NextDouble());
                if(source.id=="ua_sunflower_patch")scale=Mathf.Lerp(.85f,1.05f,(float)random.NextDouble());
                models[source.id]=source;occupied.Add(c);originals.Add(new CampArtDefinition.Instance{id="vegetation/"+preset.id+"/"+added++,modelId=source.id,x=x,z=z,y=y-b.min.y*scale,scale=scale,yaw=(float)random.NextDouble()*360,surfaceAttached=true,surfaceOffset=-b.min.y*scale});
            }
            d.art.models=models.Values.ToArray();d.art.instances=originals.ToArray();d.vegetationPreset=preset.id;d.vegetationSeed=seed;
            if(d.art.atmosphere!=null)SeasonAtmosphereAuthoring.Apply(d,d.art.atmosphere);
            return added;
        }
    }
}
