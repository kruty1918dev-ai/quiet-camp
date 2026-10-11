using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Kruty1918.ScenePlacement;

namespace Kruty1918.RockPlacement
{
    [Serializable] public sealed class RockGroup
    {public string id;public V3 center,axis=new V3(1,0,0);public float radius=2,minimumScale=.3f,maximumScale=1,buriedFraction=.15f;public int count=6;}
    [Serializable] public sealed class RockSpec
    {public string id;public int seed;public RockGroup[] groups=Array.Empty<RockGroup>();public AssetShape[] rocks=Array.Empty<AssetShape>();public float maximumSlope=55;}
    public sealed class RockResult
    {public readonly List<PlacedInstance> instances=new List<PlacedInstance>();public readonly List<Diagnostic> diagnostics=new List<Diagnostic>();public bool Ok=>!diagnostics.Any(d=>d.error);}
    public static class RockGenerator
    {
        public static RockResult Generate(RockSpec spec,PlacementContext context,CancellationToken cancellation=default)
        {
            var result=new RockResult();if(spec==null||string.IsNullOrWhiteSpace(spec.id)||context==null||spec.rocks==null||spec.rocks.Length==0||spec.groups==null||spec.groups.Any(g=>g==null||string.IsNullOrWhiteSpace(g.id)||!g.center.Finite||!g.axis.Finite||g.count<1||g.count>4096||!Numbers.Finite(g.radius)||g.radius<0||!Numbers.Finite(g.minimumScale)||g.minimumScale<=0||!Numbers.Finite(g.maximumScale)||g.maximumScale<g.minimumScale||!Numbers.Finite(g.buriedFraction)||g.buriedFraction<0||g.buriedFraction>.8f)||spec.groups.Select(g=>g.id).Distinct(StringComparer.Ordinal).Count()!=spec.groups.Length)throw new ArgumentException("Invalid rock groups");
            int checkpoint=context.ReservationCount;
            try{
            foreach(var group in spec.groups.OrderBy(g=>g.id,StringComparer.Ordinal))
            for(int i=0;i<group.count;i++)
            {
                cancellation.ThrowIfCancellationRequested();string id=spec.id+"/"+group.id+"/"+i;float angle=Numbers.Unit(id,spec.seed)*6.2831853f,radius=i==0?0:(float)Math.Sqrt(Numbers.Unit(id,spec.seed,1))*group.radius;
                var axis=new V3(group.axis.x,0,group.axis.z).Normalized;if(axis.LengthSquared<.1f)axis=new V3(1,0,0);var side=V3.Cross(V3.Up,axis);var target=group.center+axis*((float)Math.Cos(angle)*radius)+side*((float)Math.Sin(angle)*radius*.55f);
                var shape=spec.rocks[Math.Min(spec.rocks.Length-1,(int)(Numbers.Unit(id,spec.seed,2)*spec.rocks.Length))];float fraction=Numbers.Unit(id,spec.seed,3);float scale=group.minimumScale+(group.maximumScale-group.minimumScale)*(i==0?1:fraction*.7f);
                float height=shape.volumes.Max(v=>v.center.y+v.half.y)-shape.volumes.Min(v=>v.center.y-v.half.y);
                var next=PlacementSolver.Place(new PlacementRequest{id=id,owner=group.id,shape=shape,target=target,scale=scale,yaw=(float)(Math.Atan2(axis.x,axis.z)/Numbers.Radians)+(Numbers.Unit(id,spec.seed,4)*2-1)*25,alignToSurface=true,settlement=height*scale*group.buriedFraction,maxSlope=spec.maximumSlope,maxSupportGap=.2f,searchRadius=i==0?0:group.radius*.25f,candidates=16},context,cancellation);
                result.instances.AddRange(next.instances);foreach(var d in next.diagnostics){d.error=i==0;result.diagnostics.Add(d);}
                if(!next.Ok&&i==0){context.Rollback(checkpoint);result.instances.Clear();return result;}
            }
            return result;
            }catch{context.Rollback(checkpoint);throw;}
        }
    }
}
