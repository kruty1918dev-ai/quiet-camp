using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Kruty1918.ScenePlacement;

namespace Kruty1918.VegetationPlacement
{
    public interface IEnvironmentalFields {float Sample(string field,V3 point,float fallback);}
    public sealed class ConstantFields : IEnvironmentalFields
    {public float moisture=.5f,light=.7f,density=1;public float Sample(string field,V3 point,float fallback)=>field=="moisture"?moisture:field=="light"?light:field=="density"?density:fallback;}
    [Serializable] public sealed class Species
    {
        public string id;public AssetShape shape;public int tier;public float weight=1,minimumScale=.8f,maximumScale=1.2f;
        public float minimumYaw,maximumYaw=360;
        public float minimumMoisture,maximumMoisture=1,minimumLight,maximumLight=1,maximumSlope=40,spacing=.5f;
    }
    public sealed class VegetationSpec
    {
        public string id;public int seed;public Box zone;public Box? outputBounds;public float cellSize=1,density=.8f,patchScale=.12f,patchContrast=.5f;
        public Species[] species=Array.Empty<Species>();public int maximumCandidates=100000;public string[] forbiddenSurfaceTags=new[]{"water","building","deck"};
    }
    public sealed class VegetationResult
    {public int evaluated,acceptedTotal;public readonly List<PlacedInstance> instances=new List<PlacedInstance>();public readonly List<Diagnostic> diagnostics=new List<Diagnostic>();public bool Ok=>!diagnostics.Any(d=>d.error);}
    public static class VegetationGenerator
    {
        public static VegetationResult Generate(VegetationSpec spec,PlacementContext context,IEnvironmentalFields fields=null,CancellationToken cancellation=default)
        {
            if(spec==null||context==null||string.IsNullOrWhiteSpace(spec.id)||!spec.zone.Valid||!Numbers.Finite(spec.cellSize)||spec.cellSize<=0||spec.species==null||spec.species.Length==0||!Numbers.Finite(spec.density)||spec.density<0||spec.density>1||!Numbers.Finite(spec.patchScale)||spec.patchScale<=0||!Numbers.Finite(spec.patchContrast)||spec.patchContrast<0||spec.patchContrast>1||spec.maximumCandidates<1||spec.maximumCandidates>1000000||spec.species.Any(s=>s==null||string.IsNullOrWhiteSpace(s.id)||s.shape==null||!Numbers.Finite(s.weight)||s.weight<0||!Numbers.Finite(s.minimumScale)||s.minimumScale<=0||!Numbers.Finite(s.maximumScale)||s.maximumScale<s.minimumScale||!Numbers.Finite(s.spacing)||s.spacing<0||s.minimumMoisture>s.maximumMoisture||s.minimumLight>s.maximumLight)||spec.species.Select(s=>s.id).Distinct(StringComparer.Ordinal).Count()!=spec.species.Length)throw new ArgumentException("Invalid vegetation profile");
            fields=fields??new ConstantFields();var result=new VegetationResult();var candidates=new List<(PlacementRequest request,Species species)>();
            int x0=(int)Math.Floor((spec.zone.center.x-spec.zone.half.x)/spec.cellSize),x1=(int)Math.Ceiling((spec.zone.center.x+spec.zone.half.x)/spec.cellSize),z0=(int)Math.Floor((spec.zone.center.z-spec.zone.half.z)/spec.cellSize),z1=(int)Math.Ceiling((spec.zone.center.z+spec.zone.half.z)/spec.cellSize);
            if((long)(x1-x0+1)*(z1-z0+1)>spec.maximumCandidates){result.diagnostics.Add(new Diagnostic("vegetation-candidate-limit",spec.id,"Split source zones or increase sampling cell size"));return result;}
            for(int z=z0;z<=z1;z++)for(int x=x0;x<=x1;x++)
            {
                cancellation.ThrowIfCancellationRequested();result.evaluated++;string id=spec.id+"/"+x+"/"+z;
                var p=new V3((x+.15f+Numbers.Unit(id,spec.seed)*.7f)*spec.cellSize,0,(z+.15f+Numbers.Unit(id,spec.seed,1)*.7f)*spec.cellSize);
                if(Math.Abs(p.x-spec.zone.center.x)>spec.zone.half.x||Math.Abs(p.z-spec.zone.center.z)>spec.zone.half.z)continue;
                if(!context.surface.TrySample(p,-V3.Up,out var surface)||spec.forbiddenSurfaceTags.Contains(surface.tag))continue;
                p=surface.point;float density=fields.Sample("density",p,1);float patch=.5f+.25f*(float)Math.Sin(p.x*spec.patchScale+Numbers.Unit(spec.id,spec.seed)*6.28)+.25f*(float)Math.Cos(p.z*spec.patchScale*1.37);
                if(Numbers.Unit(id,spec.seed,2)>spec.density*density*((1-spec.patchContrast)+patch*spec.patchContrast))continue;
                float moisture=fields.Sample("moisture",p,surface.moisture),light=fields.Sample("light",p,surface.light);
                var compatible=spec.species.Where(s=>s.weight>0&&moisture>=s.minimumMoisture&&moisture<=s.maximumMoisture&&light>=s.minimumLight&&light<=s.maximumLight).OrderBy(s=>s.id,StringComparer.Ordinal).ToArray();if(compatible.Length==0)continue;
                float draw=Numbers.Unit(id,spec.seed,3)*compatible.Sum(s=>s.weight);var chosen=compatible.Last();foreach(var s in compatible){draw-=s.weight;if(draw<=0){chosen=s;break;}}
                float scale=chosen.minimumScale+(chosen.maximumScale-chosen.minimumScale)*Numbers.Unit(id,spec.seed,4);
                if(!Numbers.Finite(chosen.minimumYaw)||!Numbers.Finite(chosen.maximumYaw)||chosen.maximumYaw<chosen.minimumYaw)throw new ArgumentException("Finite ordered species yaw range required.");
                candidates.Add((new PlacementRequest{id=id,owner=spec.id,shape=chosen.shape,target=p,scale=scale,yaw=chosen.minimumYaw+Numbers.Unit(id,spec.seed,5)*(chosen.maximumYaw-chosen.minimumYaw),settlement=.005f*scale,maxSlope=chosen.maximumSlope,maxSupportGap=.08f},chosen));
            }
            var admitted=new List<(PlacedInstance instance,Species species)>();
            var tierCells=new Dictionary<(int,int,int),List<(PlacedInstance instance,Species species)>>();
            var spacingByTier=spec.species.GroupBy(s=>s.tier).ToDictionary(g=>g.Key,g=>Math.Max(.001f,g.Max(s=>s.spacing*s.maximumScale)));
            // Compile the complete source zone, then partition output. Camera/chunk
            // windows never change decisions or consume a different random stream.
            int checkpoint=context.ReservationCount;var rejected=new SortedDictionary<string,int>(StringComparer.Ordinal);
            try{foreach(var entry in candidates.OrderBy(c=>c.species.tier).ThenBy(c=>Numbers.Unit(c.request.id,spec.seed,10)).ThenBy(c=>c.request.id,StringComparer.Ordinal))
            {
                cancellation.ThrowIfCancellationRequested();var request=entry.request;
                float spacingCell=spacingByTier[entry.species.tier];int cx=(int)Math.Floor(request.target.x/spacingCell),cz=(int)Math.Floor(request.target.z/spacingCell);bool crowded=false;
                for(int z=cz-1;z<=cz+1&&!crowded;z++)for(int x=cx-1;x<=cx+1&&!crowded;x++)if(tierCells.TryGetValue((entry.species.tier,x,z),out var neighbors))crowded=neighbors.Any(a=>new V3(a.instance.pose.position.x-request.target.x,0,a.instance.pose.position.z-request.target.z).Length<Math.Max(a.species.spacing*a.instance.pose.scale.x,entry.species.spacing*request.scale));
                if(crowded)continue;
                var next=PlacementSolver.Place(request,context,cancellation);if(!next.Ok){foreach(var diagnostic in next.diagnostics){rejected.TryGetValue(diagnostic.code,out int count);rejected[diagnostic.code]=count+1;}continue;}
                var instance=next.instances[0];admitted.Add((instance,entry.species));result.acceptedTotal++;
                if(!tierCells.TryGetValue((entry.species.tier,cx,cz),out var cell))tierCells.Add((entry.species.tier,cx,cz),cell=new List<(PlacedInstance,Species)>());cell.Add((instance,entry.species));
                if(!spec.outputBounds.HasValue||Math.Abs(instance.pose.position.x-spec.outputBounds.Value.center.x)<=spec.outputBounds.Value.half.x&&Math.Abs(instance.pose.position.z-spec.outputBounds.Value.center.z)<=spec.outputBounds.Value.half.z)result.instances.Add(instance);
            }
            foreach(var pair in rejected)result.diagnostics.Add(new Diagnostic(pair.Key,spec.id,pair.Value+" optional vegetation candidates rejected",false));
            return result;
            }catch{context.Rollback(checkpoint);throw;}
        }
    }
}
