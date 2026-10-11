using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using Kruty1918.ScenePlacement;
using Kruty1918.RoadAuthoring;
using Kruty1918.RoadAuthoring.Samples;
static class Contracts
{
    static int checks;
    static readonly JsonSerializerOptions options=new JsonSerializerOptions{IncludeFields=true,IgnoreReadOnlyProperties=true};
    static void Check(bool ok,string name){if(!ok)throw new Exception("FAIL: "+name);checks++;}
    static void Expect<T>(Action action,string name) where T:Exception{try{action();}catch(T){checks++;return;}throw new Exception("FAIL: "+name);}
    static void Main()
    {
        var spec=Example.Spec();var result=Example.Run();Check(result.Ok&&result.mesh.TriangleCount>0,"road API");Check(result.corridor.Count==result.route.points.Length-1,"complete reserved route");
        foreach(var p in result.mesh.positions)Check(Math.Abs(p.y-spec.heightOffset)<.00001f,"road hugs plane");
        Check(Math.Abs(result.widths[0]-.8f)<.00001f&&result.widths.Last()>.99f,"smooth width transition");Check(result.mesh.colors.Select(c=>c.r).Distinct().Count()>=1,"material attributes");
        var repeat=RoadGenerator.Generate(spec,new PlaneSurface());Check(JsonSerializer.Serialize(repeat.mesh,options)==JsonSerializer.Serialize(result.mesh,options),"deterministic edges");
        var next=new RoutePath(new[]{result.route.points.Last(),new V3(3,0,9)});
        Check(RoadGenerator.ValidateConnections(new[]{("main",result.route,"next"),("next",next,(string)null)}).Count==0,"connected endpoints");
        Check(RoadGenerator.ValidateConnections(new[]{("main",result.route,"missing")}).Count==1,"missing continuation diagnosed");
        var cross=new RoadSpec{id="cross",smooth=false,points=new[]{new V3(-2,0,-2),new V3(2,0,2),new V3(-2,0,2),new V3(2,0,-2)},bands=new[]{new SurfaceBand()}};
        Check(!RoadGenerator.Generate(cross,new PlaneSurface()).Ok,"self-intersection rejected");
        var steep=RoadGenerator.Generate(spec,new PlaneSurface(V3.Zero,new V3(0,1,-2)));Check(!steep.Ok&&steep.terrainAdjustments.Count>0,"terrain requests remain explicit");
        Expect<OperationCanceledException>(()=>RoadGenerator.Generate(spec,new PlaneSurface(),new CancellationToken(true)),"cancellation");
        Console.WriteLine("road-authoring: "+checks+" assertions passed (Core only; no native acceptance).");
    }
}
