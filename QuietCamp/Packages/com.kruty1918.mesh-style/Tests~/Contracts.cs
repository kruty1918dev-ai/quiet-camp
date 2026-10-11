using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using Kruty1918.ScenePlacement;
using Kruty1918.MeshStyle;
using Kruty1918.MeshStyle.Samples;
static class Contracts
{
    static int checks;
    static readonly JsonSerializerOptions options=new JsonSerializerOptions{IncludeFields=true,IgnoreReadOnlyProperties=true};
    static void Check(bool ok,string name){if(!ok)throw new Exception("FAIL: "+name);checks++;}
    static void Expect<T>(Action action,string name) where T:Exception{try{action();}catch(T){checks++;return;}throw new Exception("FAIL: "+name);}
    static void Main()
    {
        var source=Example.Model();var before=JsonSerializer.Serialize(source,options);var result=Example.Run();result.mesh.Validate();Check(JsonSerializer.Serialize(source,options)==before,"original unchanged");
        var profile=new StyleProfile{minimumTriangles=1,referenceTriangles=20,referenceArea=1000,maximumWorldError=.01f};
        var small=MeshHarmonizer.Harmonize(source,profile,100);var large=MeshHarmonizer.Harmonize(source,profile,1000);Check(small.targetTriangles<large.targetTriangles,"projected area controls target");
        source.parts=new[]{new MeshPart{id="legs",preserve=true,triangles=Enumerable.Range(0,source.TriangleCount).ToArray()}};
        source.channels=new[]{new VertexChannel{name="wind",components=1,values=Enumerable.Repeat(.8f,source.positions.Length).ToArray()}};
        var locked=MeshHarmonizer.Harmonize(source,new StyleProfile{minimumTriangles=1,referenceTriangles=2,maximumWorldError=100,preserveBoundaries=false},10000);
        Check(locked.mesh.TriangleCount==source.TriangleCount&&locked.diagnostics.Any(d=>d.code=="protected-detail-limit"),"protected geometry overrides target");
        Check(locked.mesh.channels[0].values.All(f=>f==.8f)&&locked.mesh.parts[0].triangles.Length==source.TriangleCount,"custom channel and protected part retained");
        var octa=new MeshData{positions=new[]{new V3(1,0,0),new V3(-1,0,0),new V3(0,1,0),new V3(0,-1,0),new V3(0,0,1),new V3(0,0,-1)},indices=new[]{0,2,4,4,2,1,1,2,5,5,2,0,4,3,0,1,3,4,5,3,1,0,3,5}};
        octa.RecalculateNormals();var simplified=MeshHarmonizer.Harmonize(octa,new StyleProfile{referenceTriangles=6,minimumTriangles=1,maximumWorldError=1,maximumFaceTurn=85,preserveBoundaries=false,normals=NormalMode.Smooth},10000);
        Check(simplified.collapsedEdges>0&&simplified.mesh.TriangleCount<8,"actual closed-mesh simplification");
        Check(simplified.maximumDeviation<=1&&simplified.mesh.normals.All(n=>n.Finite),"bounded error and normals");
        var again=MeshHarmonizer.Harmonize(octa,new StyleProfile{referenceTriangles=6,minimumTriangles=1,maximumWorldError=1,maximumFaceTurn=85,preserveBoundaries=false,normals=NormalMode.Smooth},10000);
        Check(JsonSerializer.Serialize(simplified.mesh,options)==JsonSerializer.Serialize(again.mesh,options),"deterministic result");
        Expect<OperationCanceledException>(()=>MeshHarmonizer.Harmonize(source,profile,1000,new CancellationToken(true)),"cancellation");
        Console.WriteLine("mesh-style: "+checks+" assertions passed (Core only; no native acceptance).");
    }
}
