using System;
using System.Collections.Generic;
using System.Linq;
using QuietCamp.Infrastructure;
using UnityEngine;

namespace QuietCamp.Editor.LevelAuthoring
{
    public static class AtmosphereDemoComposition
    {
        public static void Prepare(LevelAuthoringDocument d)
        {
            WeatherModelRigAuthoring.Prepare(d);
            var models=d.art.models.ToDictionary(m=>m.id);var instances=d.art.instances.ToList();
            foreach(var item in instances.ToArray())
            {
                if(!models.TryGetValue(item.modelId,out var model))continue;
                var source=model.sourceModelId??model.id;
                if(source=="qc_rowboat")item.motion="boat";
                if(source=="ua_picket_fence_damaged"){item.x=5.5f;item.z=-5;item.y=d.art.GroundHeight(item.x,item.z);item.yaw=0;}
                if(source=="ua_sunflower_patch")instances.Remove(item);

            }
            var flower=SunflowerGeometry.Create();models[flower.id]=flower;
            instances.Add(new CampArtDefinition.Instance{id="demo/sunflowers",modelId=flower.id,x=5.5f,z=-3.8f,y=d.art.GroundHeight(5.5f,-3.8f),scale=1,yaw=225,surfaceAttached=true});
            var bridge=WorldStudyFactory.MetricModel("ua_plank_bridge");var bounds=AuthoringPropCatalog.ModelBounds(bridge);
            float z=7,u=d.art.streamCentre+Kruty1918.ScenePlacement.Composition.RiverGeometry.Centre(z,d.art.streamSlope,d.art.streamBend);
            float width=Kruty1918.ScenePlacement.Composition.RiverGeometry.Width(z,d.art.streamWidth,d.art.streamWidthVariation);
            // Put the long deck axis across the river with landings on both banks.
            float yaw=bounds.size.z>bounds.size.x?90:0;float length=Mathf.Max(bounds.size.x,bounds.size.z);float scale=(width+1.5f)/Mathf.Max(.1f,length);
            var centre=Quaternion.Euler(0,yaw,0)*bounds.center*scale;
            models[bridge.id]=bridge;instances.Add(new CampArtDefinition.Instance{id="demo/river-crossing",modelId=bridge.id,x=u-centre.x,z=z-centre.z,y=.12f-bounds.min.y*scale,scale=scale,yaw=yaw,surfaceAttached=false});
            // Keep routes and the clearing calm; grow clusters rather than uniform confetti.
            instances=instances.Where(i=>
            {
                var model=models[i.modelId];var b=AuthoringPropCatalog.ModelBounds(model);
                if(b.size.y*i.scale>.9f||i.id.StartsWith("demo/")||model.shading!="foliage")return true;
                if(Mathf.Abs(i.x)<d.level.width*.5f+1.3f&&Mathf.Abs(i.z)<d.level.height*.5f+1.3f)return false;
                float patch=.5f+.5f*Mathf.Sin(i.x*.61f)*Mathf.Cos(i.z*.47f);
                return patch>.33f;
            }).ToList();
            d.art.models=models.Values.ToArray();d.art.instances=instances.ToArray();d.art.blurPixels=2.5f;
        }
    }
}
