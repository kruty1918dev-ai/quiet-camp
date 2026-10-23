using System.IO.Compression;
using System.Security.Cryptography;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using QuietCamp.Domain;
using QuietCamp.Infrastructure;
using QuietCamp.Presentation.World;
if(args.Length>0&&args[0]=="--mobile-performance"){MobilePerformanceChecks.Run(args[1]);return;}
if(args.Length>0&&args[0]=="--mobile-check-all"){MobilePerformanceChecks.CheckAll(args[1]);return;}
if(args.Length>0&&args[0]=="--composition-check"){CompositionChecks.Run(args[1],args[2]);return;}
if(args.Length>0&&args[0]=="--well-check"){WellChecks.Run(args[1]);return;}
if(args.Length<2)throw new ArgumentException("resources-directory output-directory [comma-separated campaign orders]");
string resources=Path.GetFullPath(args[0]),output=Path.GetFullPath(args[1]);Directory.CreateDirectory(output);
var campaign=JObject.Parse(File.ReadAllText(Path.Combine(resources,"QuietCamp/campaign.json")));
var ids=campaign["mvpLevelIds"].Values<string>().Concat(campaign["generatedLevelIds"].Values<string>()).ToArray();
bool repairs=args.Contains("--composition-candidates");
var repairKinds=(args.FirstOrDefault(a=>a.StartsWith("--repair-kinds="))?.Split('=')[1]??"well.opening,bridge.crossing,shade.support").Split(',').ToHashSet();
if(repairKinds.Any(k=>!new[]{"well.opening","bridge.crossing","shade.support"}.Contains(k)))throw new ArgumentException("Unsupported managed WorldOps candidate operation.");
var selected=args.Length>2&&!args[2].StartsWith("--")?args[2].Split(',').Select(int.Parse).ToHashSet():Enumerable.Range(1,ids.Length).ToHashSet();
string Hash(string p)=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(p))).ToLowerInvariant();
foreach(var (id,index) in ids.Select((id,index)=>(id,index))){
 if(!selected.Contains(index+1))continue;
 string levelPath=Path.Combine(resources,"QuietCamp",id.StartsWith("gen:")?"GeneratedLevels/"+id.Replace(':','_')+".json":"Levels/"+id+".json");
 var level=JsonConvert.DeserializeObject<LevelData>(File.ReadAllText(levelPath));
 string revision=Path.Combine(resources,"QuietCamp/ArtRevisions",level.contentHash+".json");
 string artPath=File.Exists(revision)?revision:Path.Combine(resources,"QuietCamp/ArtCompositions",id.Replace(':','_')+".json");
 var raw=JObject.Parse(File.ReadAllText(artPath));if(raw.Value<bool?>("absent")==true||raw["profile"]!=null)throw new InvalidDataException("Missing baked art for "+id);
 var art=raw.ToObject<CampArtDefinition>();string validation=null;try{art.Validate(level);}catch(InvalidDataException e){validation=e.Message;}
 if(art.landscape==null)throw new InvalidDataException("No baked landscape for "+id);
 var originalCount=art.models.Sum(m=>m.colors.Length/3);
 string repairFailure=null;
 if(repairs){if(repairKinds.Contains("well.opening"))QuietCamp.Editor.LevelAuthoring.WorldOps.WorldOpsCompositionRepairs.OpenWells(art);if(repairKinds.Contains("bridge.crossing"))QuietCamp.Editor.LevelAuthoring.WorldOps.WorldOpsCompositionRepairs.BridgeCrossings(art);try{if(repairKinds.Contains("shade.support"))QuietCamp.Editor.LevelAuthoring.WorldOps.WorldOpsCompositionRepairs.ShadeSupport(art,level);}catch(InvalidOperationException e){repairFailure=e.Message;}art.Validate(level);raw=JObject.FromObject(art);}
 raw["models"]=JArray.FromObject(art.models.Select(m=>SeasonModelAppearance.Resolve(m,art.atmosphere)));
 var document=new JObject{["schemaVersion"]=1,["order"]=index+1,["id"]=id,["level"]=JObject.Parse(File.ReadAllText(levelPath)),["art"]=raw,
 ["source"]=new JObject{["levelPath"]=Path.GetRelativePath(resources,levelPath),["artPath"]=Path.GetRelativePath(resources,artPath),["levelSha256"]=Hash(levelPath),["artSha256"]=Hash(artPath),["contentHash"]=level.contentHash,["originalModelTriangles"]=originalCount},
 ["gameplayModels"]=PrefabGeometry.Build(level,art,Path.Combine(Path.GetDirectoryName(output),"prefab-sources")),
 ["sourceValidationFailure"]=validation,["candidateRepairFailure"]=repairFailure,["compositionCandidates"]=repairs,["requestedRepairKinds"]=JArray.FromObject(repairs?repairKinds.ToArray():Array.Empty<string>()),
 ["scope"]="Read-only exported authored geometry, real SeasonModelAppearance. Blender shading/camera viewport approximation is not Unity Game View. Runtime procedural river, meadow and weather are separately disclosed."};
 string file=Path.Combine(output,$"{index+1:000}.json.gz");using(var zip=new GZipStream(File.Create(file),CompressionLevel.Fastest))using(var writer=new StreamWriter(zip))using(var json=new JsonTextWriter(writer)){document.WriteTo(json);}
 Console.WriteLine($"EXPORTED {index+1:000} {id} {art.instances.Length} authored instances; {Path.GetRelativePath(resources,artPath)}");
}
