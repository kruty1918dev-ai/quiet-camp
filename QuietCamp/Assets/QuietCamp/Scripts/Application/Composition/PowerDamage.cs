using System;
using System.Collections.Generic;
using System.Linq;

namespace QuietCamp.Composition
{
    /// <summary>Source-owned damage; fallen geometry can never be a live wire anchor.</summary>
    public static class PowerDamage
    {
        public static void Validate(InfrastructureRouteDefinition route,
            IReadOnlyDictionary<string,VisualAssetDefinition> assets,List<CompositionDiagnostic> diagnostics)
        {
            void Error(string code,string message)=>diagnostics.Add(new CompositionDiagnostic
            {code=code,entityId=route.id,message=message});
            bool network=route.kind=="power"||route.kind=="distribution";
            foreach(var variant in route.supportVariants)
                if(variant==null||variant.index<0||!network||!assets.TryGetValue(variant.asset??"",out var definition)
                    ||definition.supportKind!=route.kind||definition.conductors.Length==0||float.IsNaN(variant.height)||float.IsInfinity(variant.height)||variant.height<=0)
                    Error("invalid-support-variant","Support variants require typed standing sockets and a positive finite height");
            if(route.supportVariants.Where(v=>v!=null).GroupBy(v=>v.index).Any(g=>g.Count()>1))Error("invalid-support-variant","Duplicate support variant index");
            if(!network&&(route.damage.Length>0||route.fallenSupports.Length>0))
                Error("invalid-power-damage","Only power networks accept conductor damage.");
            if(route.damage.Any(d=>d==null||d.span<0||!new[]{"severed","hanging-from","hanging-to","removed"}.Contains(d.state)
                ||float.IsNaN(d.dropLength)||float.IsInfinity(d.dropLength)||d.dropLength<=0||d.dropLength>route.supportHeight)
                ||route.damage.Where(d=>d!=null).GroupBy(d=>d.span).Any(g=>g.Count()>1))
                Error("invalid-power-damage","Damage requires unique span indices, valid state and bounded drop length.");
            if(route.fallenSupports.Any(i=>i<0)||route.fallenSupports.Distinct().Count()!=route.fallenSupports.Length)
                Error("invalid-fallen-support","Fallen support indices must be unique and nonnegative.");
            if(route.fallenSupports.Length>0&&(!assets.TryGetValue(route.fallenAsset??"",out var fallen)
                ||fallen.supportKind!=null||fallen.conductors.Length!=0||fallen.placementClass!="solid"))
                Error("invalid-fallen-support","Fallen geometry must have no live support type or conductor sockets.");
        }
        public static void Apply(InfrastructureRouteDefinition route,CompositionResult result,IReadOnlyDictionary<string,VisualAssetDefinition> assets)
        {
            if(route.kind!="power"&&route.kind!="distribution")return;
            var supports=result.instances.Where(i=>i.owner==route.id&&i.role=="support").ToArray();
            var spans=result.spans.Where(s=>s.route==route.id&&s.height>0&&s.state!="service").ToArray();
            void Error(string code,string message)=>result.diagnostics.Add(new CompositionDiagnostic
            {code=code,entityId=route.id,message=message});
            foreach(int index in route.fallenSupports)
            {
                if(index>=supports.Length){Error("invalid-fallen-support","Fallen support index is outside generated route.");continue;}
                var support=supports[index];support.asset=route.fallenAsset;support.role="fallen-support";support.state="fallen";support.height=assets[route.fallenAsset].height;
            }
            foreach(var damage in route.damage)
            {
                if(damage.span>=spans.Length){Error("invalid-power-damage","Damage index is outside generated spans.");continue;}
                spans[damage.span].state=damage.state;spans[damage.span].dropLength=damage.dropLength;
            }
            foreach(var span in spans)
            {
                var a=result.instances.Find(i=>i.id==span.a);var b=result.instances.Find(i=>i.id==span.b);
                bool from=a.role=="support",to=b.role=="support";
                if(span.state=="intact"&&(!from||!to)||span.state=="hanging-from"&&!from||span.state=="hanging-to"&&!to)
                    Error("fallen-live-conductor","Intact and hanging wires require standing attachment supports.");
            }
        }
    }
}
