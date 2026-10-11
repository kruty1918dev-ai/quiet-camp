using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using Kruty1918.ScenePlacement;
using Kruty1918.PowerLines;
using Kruty1918.PowerLines.Samples;
static class Contracts
{
    static int checks;
    static readonly JsonSerializerOptions options=new JsonSerializerOptions{IncludeFields=true,IgnoreReadOnlyProperties=true};
    static void Check(bool ok,string name){if(!ok)throw new Exception("FAIL: "+name);checks++;}
    static void Expect<T>(Action action,string name) where T:Exception{try{action();}catch(T){checks++;return;}throw new Exception("FAIL: "+name);}
    static void Main()
    {
        var spec=Example.Spec();var kit=Example.Kit();var result=Example.Run();Check(result.Ok&&result.supports.Count==3&&result.spans.Count==4,"connected network");
        foreach(var cable in result.spans){var a=result.supports.Single(p=>p.id==cable.from).pose.Point(kit.sockets[cable.socket]);var b=result.supports.Single(p=>p.id==cable.to).pose.Point(kit.sockets[cable.socket]);Check((cable.points[0]-a).Length<.00001f&&(cable.points.Last()-b).Length<.00001f,"socket endpoints exact");Check(cable.points[cable.points.Length/2].y<cable.points[0].y,"sag below anchors");cable.mesh.Validate();}
        spec.damage=new[]{new CableDamage{span=0,state=CableState.Severed}};var severed=PowerGenerator.Generate(spec,kit,new PlacementContext(new PlaneSurface()));Check(severed.Ok&&severed.spans.Count==6,"two severed ends per conductor");
        spec.fallenSupports=new[]{0};spec.damage=Array.Empty<CableDamage>();var context=new PlacementContext(new PlaneSurface());var invalid=PowerGenerator.Generate(spec,kit,context);Check(!invalid.Ok&&context.ReservationCount==0,"fallen pole cannot anchor intact line");
        spec.damage=new[]{new CableDamage{span=0,state=CableState.Grounded}};var grounded=PowerGenerator.Generate(spec,kit,new PlacementContext(new PlaneSurface()));Check(grounded.Ok,"fallen pole grounded span");foreach(var c in grounded.spans.Where(c=>c.state==CableState.Grounded))Check(c.points.All(p=>Math.Abs(p.y-spec.radius)<.00001f),"ground cable contacts surface");
        Expect<OperationCanceledException>(()=>PowerGenerator.Generate(Example.Spec(),kit,context,new CancellationToken(true)),"cancellation");
        Console.WriteLine("power-lines: "+checks+" assertions passed (Core only; no native acceptance).");
    }
}
