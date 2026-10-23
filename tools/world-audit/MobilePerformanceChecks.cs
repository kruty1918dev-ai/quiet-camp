using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using QuietCamp.Application;
using QuietCamp.Infrastructure;

// Managed source/DTO probe. No Unity engine, rendering, native mesh or phone measurements.
static class MobilePerformanceChecks
{
    sealed class Reference { public string profile; public int seed; }
    static CampArtDefinition LegacyRead(string json, out string profile)
    {
        profile=null;
        if(JObject.Parse(json).Value<bool?>("absent")==true)return null;
        var reference=JsonConvert.DeserializeObject<Reference>(json);profile=reference.profile;
        return JsonConvert.DeserializeObject<CampArtDefinition>(json);
    }
    static string Digest(CampArtDefinition value) => Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(value))));
    static void Require(bool value,string message) { if(!value)throw new InvalidOperationException(message); }
    static (double ms,long bytes) Measure(Action action,int iterations)
    {
        action(); var elapsed=new List<double>();var allocated=new List<long>();
        for(int sample=0;sample<5;sample++) {
            GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();
            long before=GC.GetAllocatedBytesForCurrentThread();var clock=Stopwatch.StartNew();
            for(int i=0;i<iterations;i++)action();
            clock.Stop();allocated.Add((GC.GetAllocatedBytesForCurrentThread()-before)/iterations);elapsed.Add(clock.Elapsed.TotalMilliseconds/iterations);
        }
        elapsed.Sort();allocated.Sort();return(elapsed[2],allocated[2]);
    }
    public static void Run(string resources)
    {
        foreach(string json in new[]{"{\"absent\":true}","{\"absent\":false}","{\"absent\":null}","{\"profile\":\"forest\",\"seed\":17}","{\"profile\":\"forest\"}"}) {
            var previous=LegacyRead(json,out var previousProfile);var current=CampArtDefinition.ReadDocument(json,out var profile);
            Require((previous==null)==(current==null)&&previousProfile==profile,"Document marker/profile behavior changed");
            if(current!=null)Require(Digest(previous)==Digest(current),"Document serialization changed");
        }
        bool rejected=false;try{CampArtDefinition.ReadDocument("null",out _);}catch(InvalidDataException){rejected=true;}
        Require(rejected,"Null document must fail validation");
        foreach(string id in new[]{"QC001","QC007","QC010"}) {
            string json=File.ReadAllText(Path.Combine(resources,"QuietCamp","ArtCompositions",id+".json"));
            var previous=LegacyRead(json,out var previousProfile);var current=CampArtDefinition.ReadDocument(json,out var profile);
            Require(profile==previousProfile&&Digest(previous)==Digest(current),id+" decoded geometry/serialization changed");
            var old=Measure(()=>GC.KeepAlive(LegacyRead(json,out _)),2);
            var fast=Measure(()=>GC.KeepAlive(CampArtDefinition.ReadDocument(json,out _)),2);
            var moving=current.instances.Where(i=>!string.IsNullOrEmpty(i.motion)&&!string.IsNullOrEmpty(i.modelId)).Select(i=>i.modelId).ToHashSet();
            var temporary=current.models.Where(m=>!moving.Contains(m.id)).ToArray();
            long streamBytes=temporary.Sum(m=>(long)(m.vertices.Length+m.normals.Length+(m.uv?.Length??0)+(m.tangents?.Length??0))*4+(long)m.colors.Length*20);
            Console.WriteLine(JsonConvert.SerializeObject(new {scope="managed JSON decode only; not Unity load time or device FPS",id,oldMs=old.ms,newMs=fast.ms,oldBytes=old.bytes,newBytes=fast.bytes,
                identicalDecodedData=true,eligiblePrototypeModels=temporary.Length,basePrototypeStreamBytes=streamBytes}));
        }
        var now=new DateTime(2026,10,23,0,0,0,DateTimeKind.Utc);var data=new EconomySaveData {initialized=true,pro=true,proVerificationRequired=true,proValidUntilUtc=now.AddHours(1).ToString("O")};
        var economy=new CampEconomy(data,new EconomyRules(),()=>true,()=>now);int accepted=0;
        bool LegacyLease()=>data.pro&&(!data.proVerificationRequired||(DateTime.TryParse(data.proValidUntilUtc,CultureInfo.InvariantCulture,DateTimeStyles.RoundtripKind,out var until)&&until.ToUniversalTime()>now));
        var beforeLease=Measure(()=>{for(int i=0;i<10000;i++)if(LegacyLease())accepted++;},10);
        var afterLease=Measure(()=>{for(int i=0;i<10000;i++)if(economy.IsPro)accepted++;},10);
        Require(accepted>0,"Lease benchmark must exercise real queries");
        Console.WriteLine(JsonConvert.SerializeObject(new {scope="managed paid lease queries, 10000 per sample; not device FPS",oldMs=beforeLease.ms,newMs=afterLease.ms}));
        Console.WriteLine("PASS mobile source contracts: absent/profile markers, unchanged decoded geometry/serialization and readonly lease queries.");
    }
    public static void CheckAll(string resources)
    {
        var paths=Directory.GetFiles(Path.Combine(resources,"QuietCamp","ArtCompositions"),"*.json").OrderBy(p=>p).ToArray();
        Require(paths.Length>0,"No current compositions found");int checkedCount=0;
        foreach(string path in paths) {
            string json=File.ReadAllText(path);
            var previous=LegacyRead(json,out var previousProfile);var current=CampArtDefinition.ReadDocument(json,out var profile);
            Require((previous==null)==(current==null)&&previousProfile==profile&&Digest(previous)==Digest(current),Path.GetFileName(path)+" changed decoded data");
            checkedCount++;
            if(checkedCount%10==0)GC.Collect();
            if(checkedCount%25==0)Console.WriteLine("Checked "+checkedCount+"/"+paths.Length+" current documents");
        }
        Console.WriteLine("PASS "+checkedCount+" current composition documents: identical decoded data and serialization; no render or authoring mutation.");
    }
}
