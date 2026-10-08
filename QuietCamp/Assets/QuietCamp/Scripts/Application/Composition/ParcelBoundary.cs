using System;
using System.Collections.Generic;
using System.Linq;

namespace QuietCamp.Composition
{
    public static class ParcelBoundary
    {
        // Declared solid intervals plus named gates must cover all four edges.
        // Damaged boards remain an art detail; an unowned geographic hole is rejected.
        public static bool Closed(EnsembleTemplate template,IEnumerable<EnsembleRole> roles,
            IReadOnlyDictionary<string,VisualAssetDefinition> assets,string fenceVariant)
        {
            for(int side=0;side<4;side++)
            {
                bool horizontal=side<2;float edge=(side%2==0?-1:1)*(horizontal?template.depth:template.width)*.5f;
                float half=(horizontal?template.width:template.depth)*.5f;
                var intervals=new List<(float a,float b)>();
                foreach(var role in roles)
                {
                    string id=role.asset=="$fence"?"ua_"+fenceVariant+"_fence":role.asset;
                    if(!assets.TryGetValue(id,out var asset))continue;
                    if(asset.placementClass!="boundary"&&role.id!="gate"&&role.id!="entrance"&&!role.entrance)continue;
                    if(Math.Abs((horizontal?role.z:role.x)-edge)>.15f)continue;
                    float yaw=role.yaw*(float)Math.PI/180,scale=(role.height>0?role.height:asset.height)/asset.height;
                    float extent=(float)(horizontal?Math.Abs(Math.Cos(yaw))*asset.width+Math.Abs(Math.Sin(yaw))*asset.depth
                        :Math.Abs(Math.Sin(yaw))*asset.width+Math.Abs(Math.Cos(yaw))*asset.depth)*scale*.5f;
                    float centre=horizontal?role.x:role.z;intervals.Add((centre-extent,centre+extent));
                }
                float covered=-half;
                foreach(var interval in intervals.OrderBy(i=>i.a))
                {if(interval.a>covered+.035f)return false;covered=Math.Max(covered,interval.b);}
                if(covered<half-.035f)return false;
            }return true;
        }
    }
}
