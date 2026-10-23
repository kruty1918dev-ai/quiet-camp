using System.Globalization;
using Newtonsoft.Json.Linq;
using QuietCamp.Domain;
using QuietCamp.Infrastructure;
using QuietCamp.Presentation.World;
using UnityEngine;
public static class PrefabGeometry {
 static float Number(string v)=>float.Parse(v,CultureInfo.InvariantCulture);
 public static JArray Build(LevelData level,CampArtDefinition art,string folder){
  var result=new JArray();var meta=JObject.Parse(File.ReadAllText(Path.Combine(folder,"prefabs.json")));var materials=JObject.Parse(File.ReadAllText(Path.Combine(folder,"materials.json")));
  var noise=level.noise.Select(p=>(p[0],p[1])).ToHashSet();
  foreach(var cell in level.blocked){
   if(noise.Contains((cell[0],cell[1])))continue;
   var obj=level.objects.FirstOrDefault(o=>o.x==cell[0]&&o.z==cell[1]);string asset=obj?.assetId??"stone_largeA";
   var position=new Vector3(cell[0]+.5f-level.width*.5f,0,cell[1]+.5f-level.height*.5f);
   var model=Read(asset,meta,materials,folder);if(model==null)throw new InvalidDataException("Unresolved actual prefab "+asset);
   float height=model.vertices.Where((_,i)=>i%3==1).Max()-model.vertices.Where((_,i)=>i%3==1).Min();float scale=(asset.StartsWith("tree")?2.4f:.6f)/height;
   for(int i=0;i<model.vertices.Length;i++)model.vertices[i]*=scale;
   if(!asset.StartsWith("tree")){
    float span=Math.Max(model.vertices.Where((_,i)=>i%3==0).Max()-model.vertices.Where((_,i)=>i%3==0).Min(),model.vertices.Where((_,i)=>i%3==2).Max()-model.vertices.Where((_,i)=>i%3==2).Min());
    float s=Math.Min(1,.86f/span);for(int i=0;i<model.vertices.Length;i++)model.vertices[i]*=s;
   }else{
    var profile=SeasonProfile.For(art.atmosphere?.season??level.environment.seasonId,level.order);
    bool bare=profile.BareTree(position,level.decorSeed,false);
    if(bare){var points=Enumerable.Range(0,model.colors.Length).Select(i=>new Vector3(model.vertices[i*3],model.vertices[i*3+1],model.vertices[i*3+2])).ToArray();var normals=Enumerable.Range(0,model.colors.Length).Select(i=>new Vector3(model.normals[i*3],model.normals[i*3+1],model.normals[i*3+2])).ToArray();var colors=model.colors.Select(CampArtDefinition.Color).ToArray();var b=SeasonalTreeGeometry.Bare(points,normals,Enumerable.Range(0,points.Length).ToArray(),colors);model.vertices=b.Vertices.SelectMany(v=>new[]{v.x,v.y,v.z}).ToArray();model.normals=b.Normals.SelectMany(v=>new[]{v.x,v.y,v.z}).ToArray();model.colors=b.Colors.Select(SeasonModelAppearance.Pack).ToArray();}
    else{float v=SeasonProfile.Variation(position,level.decorSeed);model.colors=model.colors.Select(c=>SeasonModelAppearance.Pack(profile.Palette.Plant(CampArtDefinition.Color(c),v))).ToArray();}
   }
   result.Add(new JObject{["id"]=$"gameplay/{cell[0]}/{cell[1]}",["assetId"]=asset,["source"]="BoardRenderer.BuildObstacles + real seasonal geometry/palette",["position"]=new JArray(position.x,position.y,position.z),["yaw"]=(obj?.rotation??0)*90,["model"]=JObject.FromObject(model)});
  }
  foreach(var placement in level.witness){
   var guest=level.guests.First(g=>g.id==placement.guestId);var model=Read(guest.assetId,meta,materials,folder);if(model==null)throw new InvalidDataException("Missing actual tent prefab "+guest.assetId);
   result.Add(new JObject{["id"]="tent/"+guest.id,["assetId"]=guest.assetId,["source"]="TentPresenter / saved reference solution",["state"]="solved",["position"]=new JArray(placement.x+1-level.width*.5f,.02f,placement.z+1-level.height*.5f),["yaw"]=placement.rotation*90,["model"]=JObject.FromObject(model)});
  }
  return result;
 }
 public static CampArtDefinition.Model Read(string asset,JObject meta,JObject materials,string folder){
  var record=meta[asset];if(record==null)return null;var source=Path.Combine(folder,record.Value<string>("file"));
  var points=new List<Vector3>();var positions=new List<Vector3>();var normals=new List<Vector3>();var colors=new List<int>();var color=Color.white;
  var shift=record["position"].Values<float>().ToArray();var scale=record["scale"].Values<float>().ToArray();
  foreach(var line in File.ReadLines(source)){
   var a=line.Split(' ',StringSplitOptions.RemoveEmptyEntries);if(a.Length==0)continue;
   if(a[0]=="v")points.Add(new Vector3(Number(a[1])*scale[0]+shift[0],Number(a[2])*scale[1]+shift[1],Number(a[3])*scale[2]+shift[2]));
   else if(a[0]=="usemtl"){
    var c=materials[a[1]]?.Values<float>().ToArray();if(c==null)throw new InvalidDataException("Missing Unity base material "+a[1]);color=new Color(c[0],c[1],c[2]);
   }else if(a[0]=="f"){
    var f=a.Skip(1).Select(v=>int.Parse(v.Split('/')[0])-1).ToArray();for(int i=1;i<f.Length-1;i++){
     var p=points[f[0]];var q=points[f[i]];var r=points[f[i+1]];var n=Vector3.Cross(q-p,r-p).normalized;foreach(var v in new[]{p,q,r}){positions.Add(v);normals.Add(n);colors.Add(SeasonModelAppearance.Pack(color));}
    }
   }
  }
  return new CampArtDefinition.Model{id=asset,vertices=positions.SelectMany(v=>new[]{v.x,v.y,v.z}).ToArray(),normals=normals.SelectMany(v=>new[]{v.x,v.y,v.z}).ToArray(),colors=colors.ToArray()};
 }
}
