using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace QuietCamp.Infrastructure
{
    /// <summary>Deterministic bank groups from the level's existing plant models; no gameplay objects.</summary>
    public static class RiverBankEcology
    {
        public static CampArtDefinition.Instance[] Generate(CampArtDefinition art,FrozenLandscape land,RiverEnvironmentProfile profile)
        {
            if(land==null||profile.maxBankPlants==0)return Array.Empty<CampArtDefinition.Instance>();
            var grasses=art.models.Where(m=>m.seasonalRole=="groundcover"&&m.vertices.Length>0).ToArray();
            var compact=grasses.Where(m=>!m.id.StartsWith("cover_",StringComparison.Ordinal)
                &&!(m.sourceModelId??m.id).Contains("leafsLarge")).ToArray();
            if(compact.Length>0)grasses=compact;
            var reeds=art.models.Where(m=>(m.sourceModelId??m.id).IndexOf("reed",StringComparison.OrdinalIgnoreCase)>=0).ToArray();
            if(grasses.Length==0&&reeds.Length==0)return Array.Empty<CampArtDefinition.Instance>();
            var field=new RiverSurfaceField(land);var result=new List<CampArtDefinition.Instance>();
            var random=new System.Random(art.seed^31871);
            // Spend the finite budget around the clearing, not at the first far
            // upstream stations encountered in mesh order.
            foreach(var station in field.reaches.SelectMany(r=>r.Skip(1)).OrderBy(s=>s.centre.sqrMagnitude))
            {
                if(Mathf.Abs(station.centre.x)>28||Mathf.Abs(station.centre.z)>28)continue;
                for(int side=-1;side<=1;side+=2)
                {
                    float patch=RiverLandscapeGeometry.Patch(station.distance*.22f,side*11,art.seed);
                    if(patch<.43f)continue;
                    int group=2+(int)(patch*4);
                    for(int j=0;j<group&&result.Count<profile.maxBankPlants;j++)
                    {
                        float outward=station.halfWidth+.07f+(float)random.NextDouble()*.9f;
                        float along=((float)random.NextDouble()-.5f)*1.7f;
                        float x=station.centre.x+station.side.x*outward*side-station.side.y*along;
                        float z=station.centre.z+station.side.y*outward*side+station.side.x*along;
                        if(Mathf.Abs(x)<art.width*.5f+1||Mathf.Abs(z)<art.height*.5f+1)
                            if(Mathf.Abs(x)<art.width*.5f+1&&Mathf.Abs(z)<art.height*.5f+1)continue;
                        float h=land.SampleHeight(x,z);
                        if(!field.TrySample(x,z,out var sample)||sample.signedDistance<.015f||sample.signedDistance>1.5f||h<sample.centre.y+.005f)continue;
                        if(art.instances.Any(p=>Mathf.Abs(p.x-x)<.22f&&Mathf.Abs(p.z-z)<.22f))continue;
                        var choices=outward<station.halfWidth+.45f&&reeds.Length>0?reeds:grasses.Length>0?grasses:reeds;
                        var model=choices[random.Next(choices.Length)];float height=0,min=float.MaxValue,max=float.MinValue;
                        for(int k=1;k<model.vertices.Length;k+=3){min=Mathf.Min(min,model.vertices[k]);max=Mathf.Max(max,model.vertices[k]);}height=max-min;
                        float scale=profile.bankPlantHeight*(.65f+(float)random.NextDouble()*.45f)/Mathf.Max(.1f,height);
                        result.Add(new CampArtDefinition.Instance{id="river-bank/"+result.Count,modelId=model.id,x=x,z=z,y=h-min*scale,
                            yaw=(float)random.NextDouble()*360,scale=scale,surfaceAttached=true,surfaceOffset=-min*scale});
                    }
                }
            }
            return result.ToArray();
        }
    }
}
