using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using Kruty1918.ScenePlacement;
using Kruty1918.RockPlacement;
using Kruty1918.RockPlacement.Samples;
static class Contracts
{
    static int checks;
    static readonly JsonSerializerOptions options=new JsonSerializerOptions{IncludeFields=true,IgnoreReadOnlyProperties=true};
    static void Check(bool ok,string name){if(!ok)throw new Exception("FAIL: "+name);checks++;}
    static void Expect<T>(Action action,string name) where T:Exception{try{action();}catch(T){checks++;return;}throw new Exception("FAIL: "+name);}
    static void Main()
    {
        CompositionContracts.Run();
        var spec=Example.Spec();var result=Example.Run();Check(result.Ok&&result.instances.Count>0,"rock group API");var hero=result.instances.Single(p=>p.id.EndsWith("/0",StringComparison.Ordinal));Check(hero.pose.position.y<0,"hero embedded in ground");
        var repeat=RockGenerator.Generate(spec,new PlacementContext(new PlaneSurface()));Check(JsonSerializer.Serialize(result.instances,options)==JsonSerializer.Serialize(repeat.instances,options),"deterministic geology");
        foreach(var a in result.instances)foreach(var b in result.instances.Where(p=>StringComparer.Ordinal.Compare(a.id,p.id)<0))foreach(var av in a.volumes)foreach(var bv in b.volumes)Check(!av.Intersects(bv),"stones do not overlap");
        var tilted=RockGenerator.Generate(spec,new PlacementContext(new PlaneSurface(V3.Zero,new V3(-.15f,1,0))));Check(tilted.Ok&&tilted.instances[0].pose.up.x<0,"slope orientation");
        var context=new PlacementContext(new PlaneSurface());context.Reserve("path",new OrientedBox(new Box(new V3(0,.1f,0),new V3(1,1,1)),Pose.At(V3.Zero)));var blocked=RockGenerator.Generate(spec,context);Check(!blocked.Ok,"reserved path protects hero footprint");
        Expect<OperationCanceledException>(()=>RockGenerator.Generate(spec,new PlacementContext(new PlaneSurface()),new CancellationToken(true)),"cancellation");
        Console.WriteLine("rock-placement: "+checks+" assertions passed (Core only; no native acceptance).");
    }
}
