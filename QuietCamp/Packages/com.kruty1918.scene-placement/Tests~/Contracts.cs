using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using Kruty1918.ScenePlacement;
using Kruty1918.ScenePlacement.Samples;
static class Contracts
{
    static int checks;
    static readonly JsonSerializerOptions options=new JsonSerializerOptions{IncludeFields=true,IgnoreReadOnlyProperties=true};
    static void Check(bool ok,string name){if(!ok)throw new Exception("FAIL: "+name);checks++;}
    static void Expect<T>(Action action,string name) where T:Exception{try{action();}catch(T){checks++;return;}throw new Exception("FAIL: "+name);}
    static void Main()
    {
        CompositionContracts.Run();
        ReviewContracts.Run();
        Check(Example.Run().Ok,"basic API");
        var slope=new PlaneSurface(V3.Zero,new V3(-.25f,1,0));
        var bench=Example.Shape("four-legs");bench.contacts=new[]{new V3(-.5f,0,-.25f),new V3(.5f,0,-.25f),new V3(-.5f,0,.25f),new V3(.5f,0,.25f)};
        var req=new PlacementRequest{id="bench",shape=bench,target=V3.Zero,maxSupportGap=.01f};
        Check(!PlacementSolver.Place(req,new PlacementContext(slope)).Ok,"level four-legged bench rejects unequal ground");
        req.alignToSurface=true;var aligned=PlacementSolver.Place(req,new PlacementContext(slope));Check(aligned.Ok,"aligned contacts on slope");
        foreach(var anchor in bench.contacts){var p=aligned.instances[0].pose.Point(anchor);slope.TrySample(p,-V3.Up,out var hit);Check((hit.point-p).Length<.0001f,"each leg contacts ground");}
        var context=new PlacementContext(new PlaneSurface(),1);context.Reserve("across-cell",new OrientedBox(new Box(new V3(.99f,.5f,0),new V3(.1f,.5f,.1f)),Pose.At(V3.Zero)));
        Check(!PlacementSolver.Place(new PlacementRequest{id="rotated",shape=Example.Shape(),target=new V3(1.05f,0,0),yaw=45},context).Ok,"rotated overlap across grid cells");
        var checkpoint=context.ReservationCount;var ensemble=PlacementSolver.Ensemble(new[]{new PlacementRequest{id="a",shape=Example.Shape(),target=new V3(5,0,0)},new PlacementRequest{id="b",shape=Example.Shape(),target=new V3(5,0,0)}},context);
        Check(!ensemble.Ok&&ensemble.instances.Count==0&&context.ReservationCount==checkpoint,"ensemble all-or-nothing");
        var ground=new GeometryBuilder();ground.Quad(new V3(0,0,0),new V3(0,0,2),new V3(2,.4f,2),new V3(2,.4f,0),Tint.White);
        var mesh=ground.Build();mesh.channels=new[]{new VertexChannel{name="uv1",components=1,values=Enumerable.Repeat(.25f,mesh.positions.Length).ToArray()}};
        mesh.parts=new[]{new MeshPart{id="bank",role="ground",triangles=new[]{0,1}}};var joined=new GeometryBuilder();joined.Append(mesh,Pose.At(V3.Zero));joined.Box(new V3(3,1,0),new V3(1,1,1),Tint.White);var combined=joined.Build();combined.Validate();
        Check(combined.channels[0].values[0]==.25f&&combined.parts[0].triangles.Length==2,"append preserves custom channels and parts");
        var triangleSurface=new TriangleSurface(mesh);Check(triangleSurface.TrySample(new V3(1,0,1),-V3.Up,out var sample)&&Math.Abs(sample.point.y-.2f)<.0001f,"mesh surface query");
        var token=new CancellationToken(true);Expect<OperationCanceledException>(()=>PlacementSolver.Ensemble(new[]{req},context,token),"cancellation");Check(context.ReservationCount==checkpoint,"cancel rolls back");
        Check(new OrientedBox(new Box(V3.Zero,new V3(1,1,1)),Pose.At(V3.Zero)).Intersects(new OrientedBox(new Box(V3.Zero,new V3(1,1,1)),Pose.At(new V3(1,0,0),45))),"OBB intersection");
        Expect<ArgumentException>(()=>Pose.At(V3.Zero,0,new V3(0,1,1)),"zero scale rejected");
        Console.WriteLine("scene-placement: "+checks+" assertions passed (Core only; no native acceptance).");
    }
}
