using Newtonsoft.Json.Linq;
using QuietCamp.Infrastructure;
using UnityEngine;
using System.IO.Compression;
public static class WellChecks {
 static void Check(bool yes,string message){if(!yes)throw new Exception(message);}
 static double Area(LandscapeMesh m){double sum=0;for(int i=0;i<m.triangles.Length;i+=3){int a=m.triangles[i]*3,b=m.triangles[i+1]*3,c=m.triangles[i+2]*3;sum+=Math.Abs(((double)m.vertices[b]-m.vertices[a])*(m.vertices[c+2]-m.vertices[a+2])-((double)m.vertices[c]-m.vertices[a])*(m.vertices[b+2]-m.vertices[a+2]))*.5;}return sum;}
 static bool HitsGround(LandscapeMesh m,float x,float z)
 {
  for(int i=0;i<m.triangles.Length;i+=3)
  {
   int a=m.triangles[i]*3,b=m.triangles[i+1]*3,c=m.triangles[i+2]*3;
   double cross(double ax,double az,double bx,double bz)=>ax*bz-az*bx;
   double det=cross(m.vertices[b]-m.vertices[a],m.vertices[b+2]-m.vertices[a+2],m.vertices[c]-m.vertices[a],m.vertices[c+2]-m.vertices[a+2]);
   if(Math.Abs(det)<1e-10)continue;
   double u=cross(x-m.vertices[a],z-m.vertices[a+2],m.vertices[c]-m.vertices[a],m.vertices[c+2]-m.vertices[a+2])/det;
   double v=cross(m.vertices[b]-m.vertices[a],m.vertices[b+2]-m.vertices[a+2],x-m.vertices[a],z-m.vertices[a+2])/det;
   if(u>=-1e-6&&v>=-1e-6&&u+v<=1.000001)return true;
  }return false;
 }
 public static void Run(string folder){int total=0;foreach(int order in new[]{3,9,63,78,100}){
  using var zip=new GZipStream(File.OpenRead(Path.Combine(folder,$"{order:000}.json.gz")),CompressionMode.Decompress);using var reader=new StreamReader(zip);var doc=JObject.Parse(reader.ReadToEnd());var art=doc["art"].ToObject<CampArtDefinition>();var before=Newtonsoft.Json.JsonConvert.SerializeObject(art);var holes=WorldWellShafts.Find(art);
  Check(holes.Length==(order==100?2:1),"Expected actual shaft openings "+order+" but found "+holes.Length);var resolved=WorldWellShafts.Resolve(art,art.landscape);resolved.Validate();var again=WorldWellShafts.Resolve(art,resolved);Check(Newtonsoft.Json.JsonConvert.SerializeObject(resolved)==Newtonsoft.Json.JsonConvert.SerializeObject(again),"Repeated well cut changes geometry");Check(Newtonsoft.Json.JsonConvert.SerializeObject(art)==before,"Source mutated");
  double area=0;
  foreach(var hole in holes){var centre=hole.rim.Aggregate(Vector3.zero,(a,b)=>a+b)/hole.rim.Length;Check(hole.Contains(centre.x,centre.z),"Inner rim containment");Check(art.landscape.meshes.Where(m=>!m.water&&!m.decoration).Any(m=>HitsGround(m,centre.x,centre.z)),"Reproduce existing dirt-filled shaft");Check(!resolved.meshes.Where(m=>!m.water&&!m.decoration).Any(m=>HitsGround(m,centre.x,centre.z)),"Terrain still crosses well opening");
   for(int ix=1;ix<8;ix++)for(int iz=1;iz<8;iz++)
   {
    float x=hole.rim.Min(p=>p.x)+(hole.rim.Max(p=>p.x)-hole.rim.Min(p=>p.x))*ix/8,z=hole.rim.Min(p=>p.z)+(hole.rim.Max(p=>p.z)-hole.rim.Min(p=>p.z))*iz/8;
    if(hole.Contains(x,z,.015f))Check(!resolved.meshes.Where(m=>!m.water&&!m.decoration).Any(m=>HitsGround(m,x,z)),"Terrain crosses an interior well sample");
   }
   Check(resolved.meshes.Where(m=>m.name=="Well shaft — "+hole.ownerId).All(m=>!m.water&&Enumerable.Range(0,m.colors.Length/4).All(i=>m.colors[i*4+3]==0)),"Shaft must not acquire a water or snow surface");
   double a=0;for(int i=0;i<hole.rim.Length;i++){var p=hole.rim[i];var q=hole.rim[(i+1)%hole.rim.Length];a+=(double)p.x*q.z-(double)q.x*p.z;}area+=Math.Abs(a)*.5;Console.WriteLine($"WELL {order} {hole.ownerId} rim={hole.rim.Length} centre={centre.x:F3},{centre.z:F3}; no terrain/water disk added; dark shaft depth={hole.Depth}");total++;
  }
  double oldArea=art.landscape.meshes.Where(m=>!m.water&&!m.decoration).Sum(Area),newArea=resolved.meshes.Where(m=>!m.water&&!m.decoration).Sum(Area);Check(Math.Abs(oldArea-newArea-area)<.02,"Terrain exterior area changed: "+(oldArea-newArea)+" vs opening "+area);
  Console.WriteLine($"PASS level{order}: source immutable, exact rim cut, terrain area preserved, full UV/channel validation, {holes.Length} dark shafts");
 }Check(total==6,"All real well shafts covered");Console.WriteLine("6 real well openings passed geometry checks; no Unity engine executed.");}
}
