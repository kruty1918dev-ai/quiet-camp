using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using QuietCamp.Application;
using QuietCamp.Domain;

static class UkrainianRoadmapContracts
{
    public static void Run(RoadmapDefinition map)
    {
        void Require(bool ok,string message){if(!ok)throw new Exception("Ukrainian roadmap: "+message);}
        const string folder="QuietCamp/Assets/QuietCamp/Authoring/Roadmap/Models/UkrainianRural/";
        var manifest=JObject.Parse(File.ReadAllText(folder+"manifest.json"));
        var models=JArray.Parse(File.ReadAllText("QuietCamp/Assets/QuietCamp/Resources/QuietCamp/roadmap_culture_models.json"));
        var ids=new HashSet<string>(StringComparer.Ordinal);int total=0;
        foreach(var item in models)
        {
            string id=(string)item["id"];Require(ids.Add(id),"Duplicate model "+id);
            var data=item["data"].ToObject<float[]>();var colors=item["colors"].ToObject<int[]>();
            Require(data.Length>0&&data.Length%18==0&&colors.Length==data.Length/18,"Invalid streams "+id);
            Require(data.All(float.IsFinite)&&colors.All(c=>c>=0&&c<=0xffffff),"Invalid values "+id);
            int count=data.Length/18;total+=count;Require(count<=1100,"Triangle budget "+id);
            float minY=float.MaxValue,maxY=float.MinValue;
            for(int v=0;v<data.Length;v+=6)
            {
                minY=Math.Min(minY,data[v+1]);maxY=Math.Max(maxY,data[v+1]);
                float normal=data[v+3]*data[v+3]+data[v+4]*data[v+4]+data[v+5]*data[v+5];
                Require(Math.Abs(normal-1)<.002,"Invalid normal "+id);
            }
            Require(Math.Abs(minY)<.00001&&Math.Abs(maxY-1)<.00001,"Normalized height/pivot "+id);
            foreach(var tri in Enumerable.Range(0,count))
            {
                int a=tri*18;double x=data[a+6]-data[a],y=data[a+7]-data[a+1],z=data[a+8]-data[a+2];
                double u=data[a+12]-data[a],v=data[a+13]-data[a+1],w=data[a+14]-data[a+2];
                double nx=y*w-z*v,ny=z*u-x*w,nz=x*v-y*u;
                Require(nx*nx+ny*ny+nz*nz>1e-18,"Degenerate triangle "+id);
            }
            var entry=manifest["models"].FirstOrDefault(e=>(string)e["id"]==id);Require(entry!=null,"Missing metadata "+id);
            Require((bool)entry["roadmapOnly"]&&!(bool)entry["externalGeometry"],"Wrong provenance/scope "+id);
            Require((int)entry["triangles"]==count&&File.Exists(folder+(string)entry["source"]),"Missing editable geometry "+id);
        }
        Require(total==(int)manifest["totalTriangles"]&&ids.Count==14,"Kit totals differ");
        Require(RoadmapRuralLayout.ModelIds.All(ids.Contains)&&ids.Contains("ua_wheat_patch_lod")&&ids.Contains("ua_sunflower_patch_lod"),"Missing kit asset");
        Require(map.regions.Length==5&&map.regions.Sum(r=>r.nodePositions.Length)==30,"Main coverage changed");
        Require(map.regions.All(r=>r.culturalLandscape?.profile=="ukrainian-rural"),"Region missed cultural profile");
        Require(RoadmapValidator.Validate(map,modelExists:ids.Union(new[]{"story_trail_shelter"}).Contains).Any(e=>e.StartsWith("missing-model:")),"Missing base-model guard did not run");
        var profile=new RoadmapCulturalLandscapeData{fields=1,orchards=1,settlements=1};
        var used=new HashSet<string>();
        for(int strip=0;strip<32;strip++)for(int part=0;part<RoadmapRuralLayout.Parts;part++)
        {
            if(!RoadmapRuralLayout.TrySample(profile,strip,part,out var a))continue;
            Require(RoadmapRuralLayout.TrySample(profile,strip,part,out var b)&&a.Asset==b.Asset&&a.X==b.X&&a.Distance==b.Distance,"Nondeterministic placement");
            Require(float.IsFinite(a.X)&&float.IsFinite(a.Distance)&&a.Height>0&&Math.Abs(a.X)<profile.width/2-3,"Bounds "+a.Asset);
            used.Add(a.Asset);Require(a.Sway==(a.Asset=="ua_wheat_patch"||a.Asset=="ua_sunflower_patch"||a.Asset=="ua_orchard_tree"),"Rigid infrastructure sway");
        }
        Require(RoadmapRuralLayout.ModelIds.All(used.Contains),"Unused model family");
        // A half-open chunk partition must produce exactly one owner for every placement.
        for(int strip=0;strip<64;strip++)for(int part=0;part<RoadmapRuralLayout.Parts;part++)
        {
            if(!RoadmapRuralLayout.TrySample(profile,strip,part,out var p))continue;
            int owners=0;for(int chunk=0;chunk<128;chunk++){float top=chunk*420,bottom=top+420;if(p.Distance>=top&&p.Distance<bottom)owners++;}
            Require(owners==1,"Duplicate/lost seam prop");
        }
        long allocated=GC.GetAllocatedBytesForCurrentThread();float checksum=0;
        for(int i=0;i<10000;i++)if(RoadmapRuralLayout.TrySample(profile,i/RoadmapRuralLayout.Parts,i%RoadmapRuralLayout.Parts,out var p))checksum+=p.X;
        Require(checksum!=0&&GC.GetAllocatedBytesForCurrentThread()==allocated,"Sampler allocated");
        var catalog=new RoadmapCatalog(map);var climate=new RoadmapEnvironmentSampler(catalog);var placed=new List<RoadmapRuralLayout.Placement>();
        int stripCount=(int)Math.Ceiling(catalog.Height/RoadmapRuralLayout.StripLength);
        for(int strip=0;strip<stripCount;strip++)
        {
            var rural=map.regions[catalog.RegionAt((strip+.5f)*RoadmapRuralLayout.StripLength)].culturalLandscape;
            for(int part=0;part<RoadmapRuralLayout.Parts;part++)
            {
                if(!RoadmapRuralLayout.TrySample(rural,strip,part,out var p)||p.Distance>=catalog.Height)continue;
                if(!RoadmapRuralLayout.Admitted(catalog,climate,p))continue;
                placed.Add(p);int owner=0;foreach(var chunk in catalog.Chunks)if(p.Distance>=chunk.Top&&p.Distance<chunk.Bottom)owner++;
                Require(owner==1,"Actual map duplicated/lost rural prop");
            }
        }
        for(int n=0;n<catalog.Nodes.Length;n++)
        {
            Require(placed.Any(p=>Math.Abs(p.Distance-catalog.Y(n))<650),"Node lacks nearby cultural environment "+n);
            Require(!RoadmapRuralLayout.ClearOfPath(catalog,(catalog.Nodes[n].x-.5f)*RoadmapRuralLayout.PathWidth,catalog.Y(n),.55f),"Camp exclusion failed");
        }
        for(int i=0;i<1000;i++)RoadmapRuralLayout.ClearOfPath(catalog,12.6f,i*3,.55f);
        allocated=GC.GetAllocatedBytesForCurrentThread();int free=0;
        for(int i=0;i<10000;i++)if(RoadmapRuralLayout.ClearOfPath(catalog,12.6f,i*3,.55f))free++;
        Require(free>0&&GC.GetAllocatedBytesForCurrentThread()==allocated,"Native builder route policy allocated");
        var benchmark=JsonConvert.DeserializeObject<RoadmapDefinition>(File.ReadAllText("QuietCamp/Assets/QuietCamp/Tests/Fixtures/Roadmap/benchmark-world360.json"));
        foreach(var region in benchmark.regions)region.culturalLandscape=new RoadmapCulturalLandscapeData();
        Require(RoadmapValidator.Validate(benchmark).Count==0,"360 cultural authoring rejected");
        var large=new RoadmapCatalog(benchmark);var window=new RoadmapWindow();int maxParts=0;
        for(int n=0;n<large.Nodes.Length;n++)
        {
            window.Move(large.ChunkForNode(n),large.Chunks.Length);Require(window.Count<=3,"Cultural chunk window grew");
            int activeParts=0;
            for(int chunk=window.First;chunk<=window.Last;chunk++)
            {
                var lease=large.Chunks[chunk];int begin=Math.Max(0,(int)Math.Floor(lease.Top/RoadmapRuralLayout.StripLength)-1);
                int end=(int)Math.Ceiling(lease.Bottom/RoadmapRuralLayout.StripLength);
                for(int strip=begin;strip<=end;strip++)for(int part=0;part<RoadmapRuralLayout.Parts;part++)
                {
                    var rural=benchmark.regions[large.RegionAt((strip+.5f)*RoadmapRuralLayout.StripLength)].culturalLandscape;
                    if(RoadmapRuralLayout.TrySample(rural,strip,part,out var p)&&p.Distance>=lease.Top&&p.Distance<lease.Bottom)activeParts++;
                }
            }
            maxParts=Math.Max(maxParts,activeParts);
        }
        Require(maxParts<220,"Cultural live data budget grew with 360 nodes");
        foreach(var bad in new Action<RoadmapDefinition>[] {
            m=>m.regions[0].culturalLandscape.width=float.NaN,
            m=>m.regions[0].culturalLandscape.profile="unknown",
            m=>m.regions[0].culturalLandscape.fields=2,
            m=>m.regions[1].culturalLandscape.width=88,
            m=>m.regions[1].culturalLandscape=null})
        {
            var copy=JsonConvert.DeserializeObject<RoadmapDefinition>(JsonConvert.SerializeObject(map));bad(copy);
            Require(RoadmapValidator.Validate(copy).Any(e=>e.StartsWith("invalid-cultural-profile:")||e.StartsWith("inconsistent-cultural-world:")),"Invalid profile accepted");
        }
        var old=new RoadmapRegionData();Require(JObject.FromObject(old)["culturalLandscape"]==null,"Legacy serialization changed");
        Console.WriteLine("PASS Ukrainian roadmap: 12 original models + 2 LODs, "+total+" source triangles; 30 nodes/5 profiles; finite geometry, deterministic ownership, rigid masks, 10,000 zero-allocation samples + route checks, negative validation; 360-node cultural planner <=3 chunks / "+maxParts+" sampled props");
    }
}
