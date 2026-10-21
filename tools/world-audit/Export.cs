using System.IO.Compression;
using System.Security.Cryptography;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using QuietCamp.Domain;
using QuietCamp.Infrastructure;
using QuietCamp.Presentation.World;
if(args.Length<2)throw new ArgumentException("resources-directory output-directory [comma-separated campaign orders]");
string resources=Path.GetFullPath(args[0]),output=Path.GetFullPath(args[1]);Directory.CreateDirectory(output);
var campaign=JObject.Parse(File.ReadAllText(Path.Combine(resources,"QuietCamp/campaign.json")));
var ids=campaign["mvpLevelIds"].Values<string>().Concat(campaign["generatedLevelIds"].Values<string>()).ToArray();
var selected=args.Length>2?args[2].Split(',').Select(int.Parse).ToHashSet():Enumerable.Range(1,ids.Length).ToHashSet();
string Hash(string p)=>Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(p)));
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
 raw["models"]=JArray.FromObject(art.models.Select(m=>SeasonModelAppearance.Resolve(m,art.atmosphere)));
 var document=new JObject{["schemaVersion"]=1,["order"]=index+1,["id"]=id,["level"]=JObject.Parse(File.ReadAllText(levelPath)),["art"]=raw,
 ["source"]=new JObject{["levelPath"]=Path.GetRelativePath(resources,levelPath),["artPath"]=Path.GetRelativePath(resources,artPath),["levelSha256"]=Hash(levelPath),["artSha256"]=Hash(artPath),["contentHash"]=level.contentHash,["originalModelTriangles"]=originalCount},
 ["gameplayModels"]=PrefabGeometry.Build(level,art,Path.Combine(Path.GetDirectoryName(output),"prefab-sources")),
 ["sourceValidationFailure"]=validation,
 ["scope"]="Read-only exported authored geometry, real SeasonModelAppearance. Blender shading/camera viewport approximation is not Unity Game View. Runtime procedural river, meadow and weather are separately disclosed."};
 string file=Path.Combine(output,$"{index+1:000}.json.gz");using(var zip=new GZipStream(File.Create(file),CompressionLevel.Fastest))using(var writer=new StreamWriter(zip))using(var json=new JsonTextWriter(writer)){document.WriteTo(json);}
 Console.WriteLine($"EXPORTED {index+1:000} {id} {art.instances.Length} authored instances; {Path.GetRelativePath(resources,artPath)}");
}
