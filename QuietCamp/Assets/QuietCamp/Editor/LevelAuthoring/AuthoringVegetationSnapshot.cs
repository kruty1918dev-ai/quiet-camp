using System;
using System.Collections.Generic;
using System.Linq;
using QuietCamp.Infrastructure;
using UnityEngine;

namespace QuietCamp.Editor.LevelAuthoring
{
    public static class AuthoringVegetationSnapshot
    {
        // Freeze procedural cover into the same separate editable objects used by
        // the workspace. Palette revisions cannot alter this snapshot afterwards.
        public static void ExpandCover(LevelAuthoringDocument document)
        {
            if(document.art.cover==null||document.art.cover.Length==0)return;
            var plants=LevelAuthoringPalette.Entries.Where(e=>e.smallPlant&&e.id!="cover_blades").ToArray();
            var categories=new[]{"Кущі","Трава","Квіти"};
            var options=categories.Select(c=>plants.Where(e=>e.category==c).ToArray()).ToArray();
            var models=document.art.models.ToDictionary(m=>m.id);var instances=new List<CampArtDefinition.Instance>(document.art.instances);
            int index=0;
            foreach(var point in document.art.cover)
            {
                var choices=options[Mathf.Clamp(point.kind,0,2)];
                var entry=point.kind==3||choices.Length==0?LevelAuthoringPalette.Get("cover_blades"):choices[Mathf.Clamp((int)(point.variation*choices.Length),0,choices.Length-1)];
                if(entry.model==null)throw new InvalidOperationException("Cover requires packed geometry: "+entry.id);
                if(!models.TryGetValue(entry.id,out var model))models[entry.id]=model=entry.model;
                var bounds=AuthoringPropCatalog.ModelBounds(model);
                float scale=Mathf.Min(point.height/Mathf.Max(.001f,bounds.size.y),point.maximumSpan/Mathf.Max(.001f,Mathf.Max(bounds.size.x,bounds.size.z)));
                instances.Add(new CampArtDefinition.Instance{id="cover/"+(index++),modelId=model.id,x=point.x,y=point.y,z=point.z,yaw=point.yaw,scale=scale,surfaceAttached=true,surfaceOffset=point.y-document.art.GroundHeight(point.x,point.z)});
            }
            document.art.models=models.Values.ToArray();document.art.instances=instances.ToArray();document.art.cover=Array.Empty<CampArtDefinition.CoverPoint>();
        }
    }
}
