using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using Kruty1918.ScenePlacement;
using Kruty1918.FenceAuthoring;
using Kruty1918.FenceAuthoring.Samples;
static class Contracts
{
    static int checks;
    static readonly JsonSerializerOptions options=new JsonSerializerOptions{IncludeFields=true,IgnoreReadOnlyProperties=true};
    static void Check(bool ok,string name){if(!ok)throw new Exception("FAIL: "+name);checks++;}
    static void Expect<T>(Action action,string name) where T:Exception{try{action();}catch(T){checks++;return;}throw new Exception("FAIL: "+name);}
    static void Main()
    {
        var open=Example.Run();Check(open.Ok,"open fence API");Check(open.connections.Count(c=>c.gate)==1,"gate has a span");
        foreach(var c in open.connections){Check(open.instances.Any(p=>p.id==c.a)&&open.instances.Any(p=>p.id==c.b),"both connection supports exist");}
        var square=new FenceSpec{id="closed",closed=true,jitter=0,lean=0,contour=new[]{V3.Zero,new V3(6,0,0),new V3(6,0,6),new V3(0,0,6)}};
        var closed=FenceGenerator.Generate(square,Example.Kit(),new PlacementContext(new PlaneSurface()));Check(closed.Ok&&closed.connections.Count==16,"closed perimeter complete");
        Check(closed.connections.Last().b==closed.connections.First().a,"last connection closes loop");
        var slope=FenceGenerator.Generate(new FenceSpec{id="slope",jitter=0,lean=0,contour=new[]{V3.Zero,new V3(6,0,0)}},Example.Kit(),new PlacementContext(new PlaneSurface(V3.Zero,new V3(-.05f,1,0))));
        Check(slope.Ok&&slope.connections.Last().to.y>slope.connections.First().from.y,"sections follow slope");
        var context=new PlacementContext(new PlaneSurface());context.Reserve("building",new OrientedBox(new Box(new V3(3,.5f,0),new V3(.5f,1,1)),Pose.At(V3.Zero)));int checkpoint=context.ReservationCount;
        var fail=FenceGenerator.Generate(new FenceSpec{id="blocked",jitter=0,lean=0,contour=new[]{V3.Zero,new V3(6,0,0)}},Example.Kit(),context);
        Check(!fail.Ok&&fail.instances.Count==0&&context.ReservationCount==checkpoint,"blocked fence rolled back");
        Expect<OperationCanceledException>(()=>FenceGenerator.Generate(square,Example.Kit(),context,new CancellationToken(true)),"cancellation");
        Console.WriteLine("fence-authoring: "+checks+" assertions passed (Core only; no native acceptance).");
    }
}
