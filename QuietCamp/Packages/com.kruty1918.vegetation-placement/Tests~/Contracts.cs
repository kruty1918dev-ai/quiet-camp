using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using Kruty1918.ScenePlacement;
using Kruty1918.VegetationPlacement;
using Kruty1918.VegetationPlacement.Samples;
static class Contracts
{
    static int checks;
    static readonly JsonSerializerOptions options=new JsonSerializerOptions{IncludeFields=true,IgnoreReadOnlyProperties=true};
    static void Check(bool ok,string name){if(!ok)throw new Exception("FAIL: "+name);checks++;}
    static void Expect<T>(Action action,string name) where T:Exception{try{action();}catch(T){checks++;return;}throw new Exception("FAIL: "+name);}
    static void Main()
    {
        var spec=Example.Spec();var result=Example.Run();Check(result.Ok&&result.acceptedTotal>10,"patch placement API");
        var again=VegetationGenerator.Generate(spec,new PlacementContext(new PlaneSurface()));Check(JsonSerializer.Serialize(result.instances,options)==JsonSerializer.Serialize(again.instances,options),"deterministic species and positions");
        var a=Example.Spec();a.outputBounds=new Box(new V3(-2,0,0),new V3(2,1,4));var b=Example.Spec();b.outputBounds=new Box(new V3(2,0,0),new V3(2,1,4));
        var left=VegetationGenerator.Generate(a,new PlacementContext(new PlaneSurface()));var right=VegetationGenerator.Generate(b,new PlacementContext(new PlaneSurface()));var ids=left.instances.Concat(right.instances).Select(p=>p.id).OrderBy(x=>x).ToArray();
        Check(ids.SequenceEqual(result.instances.Select(p=>p.id).OrderBy(x=>x)),"chunk output order preserves full decisions");
        var water=VegetationGenerator.Generate(spec,new PlacementContext(new PlaneSurface(0,"water")));Check(water.instances.Count==0,"no vegetation in water");
        var context=new PlacementContext(new PlaneSurface());var building=new OrientedBox(new Box(new V3(0,1,0),new V3(1,1,1)),Pose.At(V3.Zero));context.Reserve("house",building);var around=VegetationGenerator.Generate(spec,context);
        Check(around.instances.Count<result.instances.Count&&around.instances.All(p=>p.volumes.All(v=>!v.Intersects(building))),"building blocks final plant volumes");
        spec.species[0].minimumMoisture=.8f;Check(VegetationGenerator.Generate(spec,new PlacementContext(new PlaneSurface()),new ConstantFields{moisture=.2f}).instances.Count==0,"moisture restricts species");
        Expect<OperationCanceledException>(()=>VegetationGenerator.Generate(Example.Spec(),new PlacementContext(new PlaneSurface()),null,new CancellationToken(true)),"cancellation");
        Console.WriteLine("vegetation-placement: "+checks+" assertions passed (Core only; no native acceptance).");
    }
}
