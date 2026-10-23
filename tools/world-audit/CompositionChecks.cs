using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using QuietCamp.Domain;
using QuietCamp.Infrastructure;
using QuietCamp.Editor.LevelAuthoring.WorldOps;
using UnityEngine;
static class CompositionChecks
{
 static void Require(bool pass,string message){if(!pass)throw new InvalidDataException(message);}
 static JObject Read(string p){using var zip=new GZipStream(File.OpenRead(p),CompressionMode.Decompress);using var r=new StreamReader(zip);return JObject.Parse(r.ReadToEnd());}
 static bool Ray(Vector3 p,Vector3 direction,CampArtDefinition.Model model)
 {
  for(int i=0;i<model.vertices.Length;i+=9)
  {
   Vector3 Point(int k)=>new Vector3(model.vertices[k],model.vertices[k+1],model.vertices[k+2]);var a=Point(i);var e1=Point(i+3)-a;var e2=Point(i+6)-a;var h=Vector3.Cross(direction,e2);float det=Vector3.Dot(e1,h);if(Mathf.Abs(det)<1e-8f)continue;
   float inv=1/det;var s=p-a;float u=inv*Vector3.Dot(s,h);if(u<0||u>1)continue;var q=Vector3.Cross(s,e1);float v=inv*Vector3.Dot(direction,q);if(v<0||u+v>1)continue;if(inv*Vector3.Dot(e2,q)>.0001f)return true;
  }return false;
 }
 public static void Run(string baseline,string candidates)
 {
  int roofs=0,exceptions=0,bridges=0,wells=0,levels=0;
  foreach(var path in Directory.GetFiles(candidates,"*.json.gz").OrderBy(p=>p))
  {
   var d=Read(path);var before=Read(Path.Combine(baseline,Path.GetFileName(path)));var level=d["level"].ToObject<LevelData>();var art=d["art"].ToObject<CampArtDefinition>();
   Require(JToken.DeepEquals(d["level"],before["level"]),"Puzzle or reference layout changed");Require(JToken.DeepEquals(d["source"],before["source"]),"Source provenance changed");
   // Bundle meshes are after the real seasonal resolver; winter intentionally
   // emits empty deciduous/ground-cover meshes. Validate the displayed subset.
   var displayed=JsonConvert.DeserializeObject<CampArtDefinition>(JsonConvert.SerializeObject(art));displayed.models=displayed.models.Where(m=>m.vertices.Length>0).ToArray();var present=displayed.models.Select(m=>m.id).ToHashSet();displayed.instances=displayed.instances.Where(p=>p.modelId==null||present.Contains(p.modelId)).ToArray();displayed.Validate(level);art.landscape.Validate();
   var shade=art.models.FirstOrDefault(m=>m.id=="worldops/shade-support");
   if(shade!=null)
   {
    Require(shade.colors.Length/3<512,"Support geometry exceeds the bounded triangle budget");var direction=-(Quaternion.Euler(CampArtDefinition.Vector(art.atmosphere?.sunEuler??art.sunEuler))*new Vector3(0,0,1)).normalized;
    foreach(var cell in level.shade)foreach(float dx in new[]{.18f,.5f,.82f})foreach(float dz in new[]{.18f,.5f,.82f})
    {float x=cell[0]+dx-level.width*.5f,z=cell[1]+dz-level.height*.5f;Require(Ray(new Vector3(x,Mathf.Max(.03f,art.landscape.SampleHeight(x,z)+.03f),z),direction,shade),"Physical support misses shade cell "+level.order+" / "+cell[0]+","+cell[1]);}
    Require(art.sunEuler.SequenceEqual(before["art"]["sunEuler"].ToObject<float[]>())&&JsonConvert.SerializeObject(art.atmosphere)==JsonConvert.SerializeObject(before["art"]["atmosphere"].ToObject<SeasonAtmospherePreset>()),"Source light or weather changed");
    roofs++;
   }
   else if(level.shade.Length>0&&(d["requestedRepairKinds"]==null||d["requestedRepairKinds"].Values<string>().Contains("shade.support"))){Require(!string.IsNullOrEmpty(d.Value<string>("candidateRepairFailure")),"A rejected placement lost its exception");exceptions++;}
   var original=before["art"].ToObject<CampArtDefinition>();var originalModels=original.models.ToDictionary(m=>m.id);
   var originalPrimary=original.instances.FirstOrDefault(p=>p.id==original.primaryLandmarkInstanceId&&p.modelId!=null&&originalModels.TryGetValue(p.modelId,out var m)&&WorldOpsCompositionRepairs.IsBridge(m.sourceModelId??m.id));
   if(originalPrimary!=null)
   {
    Require(art.instances.Any(p=>p.id==originalPrimary.id),"Unique campaign bridge was removed");
    foreach(var duplicate in original.instances.Where(p=>p.id!=originalPrimary.id&&p.modelId!=null&&originalModels.TryGetValue(p.modelId,out var m)&&(m.sourceModelId??m.id)=="ua_valley_footbridge"&&Mathf.Abs(p.x-originalPrimary.x)<.3f&&Mathf.Abs(p.z-originalPrimary.z)<.3f))
     Require(!art.instances.Any(p=>p.id==duplicate.id||p.id.StartsWith("overgrowth/"+duplicate.id+"/",StringComparison.Ordinal)),"Old crossing or its unsupported details still intersect the reserved bridge");
   }
   var models=art.models.ToDictionary(m=>m.id);var water=new RiverSurfaceField(art.landscape);
   foreach(var pose in art.instances.Where(p=>p.modelId!=null&&models.TryGetValue(p.modelId,out var m)&&WorldOpsCompositionRepairs.IsBridge(m.sourceModelId??m.id)))
   {
    Require(water.TrySample(pose.x,pose.z,out var flow),"Bridge river lost");var axis=Quaternion.Euler(0,pose.yaw,0)*new Vector3(0,0,1);Require(Mathf.Abs(axis.x*flow.direction.x+axis.z*flow.direction.y)<.02f,"Bridge is still parallel to river");
    var model=models[pose.modelId];float lo=float.MaxValue,hi=float.MinValue;for(int i=2;i<model.vertices.Length;i+=3){lo=Mathf.Min(lo,model.vertices[i]);hi=Mathf.Max(hi,model.vertices[i]);}
    foreach(float z in new[]{lo,hi}){var point=new Vector3(pose.x,pose.y,pose.z)+axis*z*(pose.scaleXYZ?[2]??pose.scale);Require(!water.TrySample(point.x,point.z,out var end)||!end.Wet,"Bridge end remains in water");}
    Require(art.instances.Any(p=>p.id=="worldops/bridge-approach/"+pose.id),"Bridge has no physical landings");bridges++;
   }
   wells+=WorldWellShafts.Find(art).Length;
   var copy=JsonConvert.DeserializeObject<CampArtDefinition>(JsonConvert.SerializeObject(art));string snapshot=JsonConvert.SerializeObject(copy);
   WorldOpsCompositionRepairs.OpenWells(copy);WorldOpsCompositionRepairs.BridgeCrossings(copy);
   if(shade!=null)WorldOpsCompositionRepairs.ShadeSupport(copy,level);
   if(JsonConvert.SerializeObject(copy)!=snapshot)
   {
    var old=JObject.Parse(snapshot).Descendants().OfType<JValue>().ToArray();var next=JObject.Parse(JsonConvert.SerializeObject(copy)).Descendants().OfType<JValue>().ToArray();
    Console.WriteLine("Idempotence scalar counts "+old.Length+" -> "+next.Length);
    File.WriteAllText(Path.Combine(Path.GetTempPath(),"qc-composition-before.json"),snapshot);File.WriteAllText(Path.Combine(Path.GetTempPath(),"qc-composition-after.json"),JsonConvert.SerializeObject(copy));
    foreach(var pair in old.Zip(next,(a,b)=>(a,b)).Where(p=>p.a.Path!=p.b.Path||!JToken.DeepEquals(p.a,p.b)).Take(6))Console.WriteLine(pair.a.Path+" : "+pair.a+" -> "+pair.b.Path+" : "+pair.b);
    throw new InvalidDataException("WorldOps repairs are not idempotent for "+level.order);
   }levels++;
   Console.WriteLine($"PASS {level.order:000}: source/puzzle immutable, art channels valid, bridge/well/shade facts checked; exception={d.Value<string>("candidateRepairFailure")??"none"}");
  }
  Require((levels==41||levels==10)&&bridges==5&&wells==6,"Expected 41 composition or 10 contact levels, five bridges and six wells; got "+levels+","+bridges+","+wells);
  Console.WriteLine($"COMPOSITION PASS: {levels} levels, {bridges} transverse bridges with dry ends, {wells} shafts, {roofs} physical shade-support candidates, {exceptions} explicit shade exceptions. No Unity launched; no native artistic acceptance.");
 }
}
