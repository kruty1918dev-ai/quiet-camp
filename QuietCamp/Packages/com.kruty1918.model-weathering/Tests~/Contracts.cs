using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using Kruty1918.ScenePlacement;
using Kruty1918.ModelWeathering;
using Kruty1918.ModelWeathering.Samples;
static class Contracts
{
    static int checks;
    static readonly JsonSerializerOptions options=new JsonSerializerOptions{IncludeFields=true,IgnoreReadOnlyProperties=true};
    static void Check(bool ok,string name){if(!ok)throw new Exception("FAIL: "+name);checks++;}
    static void Expect<T>(Action action,string name) where T:Exception{try{action();}catch(T){checks++;return;}throw new Exception("FAIL: "+name);}
    static void Main()
    {
        var source=Example.Model();var original=JsonSerializer.Serialize(source,options);var zero=Weathering.Apply("bench",source,new WeatheringProfile{intensity=0});Check(JsonSerializer.Serialize(zero.mesh,options)==original,"zero intensity exact copy");
        var worn=Weathering.Apply("bench",source,new WeatheringProfile{intensity=1,seed=7,damageProbability=0});Check(JsonSerializer.Serialize(source,options)==original,"source untouched");Check(worn.mesh.colors.Any(c=>c.r<.99f),"visible material wear");
        foreach(int t in source.parts[0].triangles)for(int k=0;k<3;k++){int v=source.indices[t*3+k];Check((source.positions[v]-worn.mesh.positions[v]).Length<.00001f,"protected supports unchanged");}
        var removed=Weathering.Apply("bench",source,new WeatheringProfile{intensity=1,damageProbability=1});Check(removed.mesh.TriangleCount==6,"only marked board removed");Check(removed.growth.Count>0&&removed.soilMask.Length==source.positions.Length,"growth and masks emitted");
        var again=Weathering.Apply("bench",source,new WeatheringProfile{intensity=1,seed=7,damageProbability=0});Check(JsonSerializer.Serialize(worn.mesh,options)==JsonSerializer.Serialize(again.mesh,options),"deterministic wear");
        var unmarked=source.Copy();unmarked.parts=Array.Empty<MeshPart>();var safe=Weathering.Apply("bench",unmarked,new WeatheringProfile{intensity=1,damageProbability=1});Check(safe.mesh.TriangleCount==source.TriangleCount&&safe.diagnostics.Any(d=>d.code=="no-structural-parts"),"unmarked structure not removed");
        Expect<OperationCanceledException>(()=>Weathering.Apply("bench",source,new WeatheringProfile(),new CancellationToken(true)),"cancellation");
        Console.WriteLine("model-weathering: "+checks+" assertions passed (Core only; no native acceptance).");
    }
}
