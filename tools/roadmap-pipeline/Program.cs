using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using QuietCamp.Domain;
using QuietCamp.Application;

static class Program
{
    static readonly string Content="QuietCamp/Assets/QuietCamp/Resources/QuietCamp";
    static readonly string Fixtures="QuietCamp/Assets/QuietCamp/Tests/Fixtures/Roadmap";
    static void Require(bool ok,string message){if(!ok)throw new Exception(message);}
    static T Read<T>(string name)=>JsonConvert.DeserializeObject<T>(File.ReadAllText(name));
    static T Copy<T>(T item)=>JsonConvert.DeserializeObject<T>(JsonConvert.SerializeObject(item));
    static void Write(string name,object value){Directory.CreateDirectory(Path.GetDirectoryName(name));File.WriteAllText(name,JsonConvert.SerializeObject(value,Formatting.Indented)+"\n");}
    static void Main(string[] args)
    {
        if(args.Length>0&&args[0]=="--composition"){CompositionCommands.Run(args.Skip(1).ToArray());return;}
        if(args.Any(a=>a.StartsWith("--export"))&&File.Exists(Content+"/roadmap_regions.json")&&!string.IsNullOrEmpty(Read<RoadmapDefinition>(Content+"/roadmap_regions.json").compositionManifest))throw new InvalidOperationException("Native composition is published; use Composition Bake Main to update presentation. Legacy export cannot overwrite it.");
        VerifySlotRetention();
        if(args.Length==1&&args[0]=="--mh17-design"){Mh17DesignContracts.Run();return;}
        if(args.Length==1&&args[0]=="--coast-design"){CoastDesignContracts.Run();return;}
        if(args.Length==1&&args[0]=="--water-memory-design"){WaterMemoryDesignContracts.Run();return;}
        if(args.Length==1&&args[0]=="--story-fixture"){StoryAuthoring.Fixture();return;}
        if(args.Length>0&&args[0]=="--story-brief")
        {if(args.Length!=3)throw new ArgumentException("--story-brief <input.json> <output.md>");StoryAuthoring.Brief(args[1],args[2]);return;}
        var summaries=Read<LevelSummary[]>(Content+"/level_summaries.json");
        foreach(var s in summaries)
        {
            string path=Content+(s.id.StartsWith("gen:")?"/GeneratedLevels/"+s.id.Replace(':','_'):"/Levels/"+s.id)+".json";
            var level=Read<LevelData>(path);s.noise=level.noise;s.ruleVersion=level.ruleVersion;s.contentHash=level.contentHash;
        }
        var bonuses=Read<BonusCampDefinition[]>(Content+"/bonus_camps.json");
        var definition=RoadmapCompiler.BuildWorld(summaries,bonuses);
        string authoring="QuietCamp/Assets/QuietCamp/Authoring/Roadmap/main.json";
        if(File.Exists(authoring))definition=RoadmapCompiler.BakeAuthored(Read<RoadmapDefinition>(authoring),summaries);
        var models=Read<Dictionary<string,object>[]>(Content+"/roadmap_models.json").Select(m=>(string)m["id"]).ToHashSet();
        var localized=new[]{"uk","en","de"}.Select(lang=>Read<Dictionary<string,Dictionary<string,string>>>("QuietCamp/Assets/QuietCamp/Resources/QuietCampLocales/"+lang+".json")["entries"]).ToArray();
        foreach(var model in Read<Dictionary<string,object>[]>(Content+"/roadmap_story_models.json"))models.Add((string)model["id"]);
        foreach(var model in Read<Dictionary<string,object>[]>(Content+"/roadmap_culture_models.json"))models.Add((string)model["id"]);
        UkrainianRoadmapContracts.Run(definition);
        StoryContracts.Run(models,summaries);
        bool Preview(string id)=>id=="QC_LH008"||summaries.Any(s=>s.id==id)||bonuses.Any(b=>"bonus:"+b.id==id);
        bool Locale(string key)=>localized.All(l=>l.ContainsKey(key));
        var issues=RoadmapValidator.Validate(definition,Preview,Locale,models.Contains,k=>k=="lighthouse");Require(issues.Count==0,string.Join("; ",issues));
        foreach(var region in definition.regions)foreach(var node in region.nodePositions)foreach(var p in node.world.props)Require(models.Contains(p.assetId),"Missing model "+p.assetId);
        foreach(var region in definition.regions)foreach(var node in region.nodePositions)
        {
            var level=summaries.First(s=>s.id==node.levelId);var shore=level.environment?.shore;if(shore==null)continue;
            foreach(var prop in node.world.props.Where(p=>p.sway||p.assetId.StartsWith("tent")))
            {
                double sx=shore.side=="left"?-1:shore.side=="right"?1:0,sz=shore.side=="back"?-1:shore.side=="front"?1:0;
                double along=prop.x*sz-prop.z*sx,bend=Math.Sin(along*.27+shore.seed*.01)*.48+Math.Sin(along*.63)*.12;
                double distance=Math.Abs(prop.x*sx+prop.z*sz-shore.offset-bend)-Math.Max(1.2,shore.width)*.5;
                Require(distance>=.48,"Preview vegetation/tent intersects water: "+node.id+"/"+prop.assetId);
            }
        }
        var slice=RoadmapCompiler.BakeAuthored(Read<RoadmapDefinition>("QuietCamp/Assets/QuietCamp/Authoring/Roadmap/foundation-slice.json"),summaries.Take(7).ToArray());
        var sliceIssues=RoadmapValidator.Validate(slice,Preview,Locale,models.Contains,k=>k=="lighthouse");
        Require(sliceIssues.Count==0,"Foundation authoring: "+string.Join("; ",sliceIssues));
        var sliceCatalog=new RoadmapCatalog(slice);Require(sliceCatalog.Chunks.Length==3,"Authored chunks were flattened incorrectly");
        Require(sliceCatalog.ChunkForNode(2)==0&&sliceCatalog.ChunkForNode(3)==1&&sliceCatalog.ChunkForNode(5)==2,"Incorrect chunk membership");
        Require(sliceCatalog.ChunkAt(sliceCatalog.Chunks[1].Top)==1,"Chunk boundary lookup is unstable");
        var sliceProgress=new ProgressionService();sliceProgress.MarkCompleted(sliceCatalog.Nodes[0].levelId);sliceProgress.MarkCompleted(sliceCatalog.Nodes[1].levelId);
        Require(sliceCatalog.State(0,sliceProgress)==RoadmapNodeState.Completed&&sliceCatalog.State(2,sliceProgress)==RoadmapNodeState.Current&&sliceCatalog.State(3,sliceProgress)==RoadmapNodeState.Next&&sliceCatalog.State(4,sliceProgress)==RoadmapNodeState.Locked&&sliceCatalog.State(5,sliceProgress)==RoadmapNodeState.Unknown,"Foundation states disagree with progression");
        var invalidSlice=Copy(slice);invalidSlice.regions[0].chunks[1].firstOrder=3;
        Require(RoadmapValidator.Validate(invalidSlice).Any(e=>e.StartsWith("invalid-chunk-coverage")),"Overlapping chunks were accepted");
        Console.WriteLine("PASS 7-level foundation: references, locales, explicit chunks, five progression states, overlap rejection");
        Console.WriteLine("PASS main: "+summaries.Length+" summaries; "+definition.regions.Length+" regions; references/locales valid");
        foreach(var fixtureName in new[]{"transition-summer-autumn","transition-winter-thaw","branch-presentation"})
        {
            var fixture=Read<RoadmapDefinition>(Fixtures+"/"+fixtureName+".json");
            var errors=RoadmapValidator.Validate(fixture,localized:Locale,modelExists:models.Contains);
            Require(errors.Count==0,fixtureName+": "+string.Join("; ",errors));
            var climate=new RoadmapEnvironmentSampler(new RoadmapCatalog(fixture));
            for(int sample=0;sample<1000;sample++)climate.Sample(sample*7.3f);
            long before=GC.GetAllocatedBytesForCurrentThread();float checksum=0;
            for(int sample=0;sample<10000;sample++){var environment=climate.Sample(sample*7.3f);checksum+=environment.Snow+environment.Trees;}
            Require(GC.GetAllocatedBytesForCurrentThread()==before,"Climate sampling allocated");
            Require(checksum>0,"Empty climate sample");
            Console.WriteLine("PASS "+fixtureName+": references/locales, phases/branch offers, 10,000 zero-allocation climate samples");
        }
        int cases=0;
        void Bad(string code,Action<RoadmapDefinition> mutate)
        {var data=Copy(definition);mutate(data);var found=RoadmapValidator.Validate(data,Preview,Locale);Require(found.Any(i=>i.StartsWith(code)),"Missing diagnostic: "+code);cases++;}
        Bad("duplicate-id",d=>d.regions[1].id=d.regions[0].id);
        Bad("node-overlap",d=>{d.regions[0].nodePositions[1].y=d.regions[0].nodePositions[0].y;d.regions[0].nodePositions[1].x=d.regions[0].nodePositions[0].x;});
        Bad("unreachable-branch",d=>d.regions.First(r=>r.branches.Length>0).branches[0].anchorNodeId="missing");
        Bad("impossible-unlock",d=>d.regions[0].nodePositions[0].requires=new[]{d.regions[0].nodePositions[1].levelId});
        Bad("broken-progress-order",d=>d.regions[0].nodePositions[0].order=99);
        Bad("missing-preview",d=>d.regions[0].previewId="unknown");
        Bad("missing-localization",d=>d.regions[0].titleKey="unknown");
        Bad("invalid-season-transition",d=>d.regions[0].transition.toSeason="invalid");
        Bad("missing-region",d=>d.regions.First(r=>r.branches.Length>0).branches[0].targetRegionId="missing");
        Bad("world-budget",d=>d.regions[0].nodePositions[0].world.props=new RoadmapPropData[81]);
        Bad("invalid-prop",d=>d.regions[0].nodePositions[0].world.props[0].height=float.NaN);
        Bad("unverified-history",d=>d.regions[0].historical.contextId="real-event");
        Bad("story-budget",d=>d.regions[0].storyProps=new RoadmapStoryPropData[13]);
        Bad("branch-budget",d=>d.regions[0].branches=new RoadmapBranchData[5]);
        var authored=Copy(definition);authored.regions[0].nodePositions[0].x=.56f;authored.regions[0].historical.cultureKey="map.region.spring";
        var rebaked=RoadmapCompiler.BakeAuthored(authored,summaries);
        Require(rebaked.regions[0].nodePositions[0].x==.56f&&rebaked.regions[0].historical.cultureKey=="map.region.spring","Rebake overwrote author data");
        var main=new RoadmapCatalog(definition);var progress=new ProgressionService();
        foreach(int completed in new[]{0,1,10})
        {
            var p=new ProgressionService();for(int i=0;i<completed;i++)p.MarkCompleted(main.Nodes[i].levelId);
            var reveal=new RoadmapRevealState(main,p);
            Require(reveal.Frontier==completed&&reveal.LastKnown==completed+2,"Progress reveal incorrect");
            Require(reveal.Main(completed+3)==RoadmapReveal.Hidden,"Unknown main node leaked");
            Require(reveal.MaxScrollDistance(1100)+462<=main.Y(reveal.LastKnown)+180+.01,"Scroll cap leaks unknown world");
            int rev=p.Revision;if(completed>0){p.MarkCompleted(main.Nodes[0].levelId);Require(p.Revision==rev&&!reveal.Refresh(),"Replay changed reveal");}
            var restored=new ProgressionService();restored.Restore(p.CompletedIds,p.LastLevelId,0);
            Require(new RoadmapRevealState(main,restored).LastKnown==reveal.LastKnown,"Restart changed reveal");
        }
        Console.WriteLine("PASS reveal: fresh / 1 / 10 completed, scroll cap, replay and restart");
        var states=Enumerable.Range(0,main.Nodes.Length).Select(i=>main.Reveal(i,0)).ToArray();
        Require(RoadmapValidator.ValidateReveal(main,0,0,states).Count==0,"Initial reveal invalid");
        states[0]=RoadmapReveal.Hidden;Require(RoadmapValidator.ValidateReveal(main,0,0,states).Contains("hidden-current-level"),"Current level can disappear");cases++;
        states[0]=RoadmapReveal.Revealed;states[10]=RoadmapReveal.Silhouette;Require(RoadmapValidator.ValidateReveal(main,0,0,states).Any(e=>e.StartsWith("revealed-future")),"Future reveal not bounded");cases++;
        progress.MarkCompleted(summaries[10].id);Require(main.Frontier(progress)>=11,"Existing non-contiguous progress lost");
        var synthetic=Enumerable.Range(0,360).Select(i=>{var s=Copy(summaries[i%summaries.Length]);s.id="benchmark:"+i;s.number=i+1;return s;}).ToArray();
        var branches=Enumerable.Range(1,36).Select(i=>new BonusCampDefinition{id="benchmark:branch:"+i,afterLevel=i*10,titleKey="map.bonus.dew.title"}).ToArray();
        var benchmark=RoadmapCompiler.Build(synthetic,branches,"benchmark-360");var catalog=new RoadmapCatalog(benchmark);
        var window=new RoadmapWindow();var random=new Random(1918);
        for(int i=0;i<10000;i++){window.Move(random.Next(benchmark.regions.Length),benchmark.regions.Length);Require(window.Count<=3,"Window >3");}
        var world360=RoadmapCompiler.BuildWorld(synthetic,branches,"benchmark-world360");var worldCatalog=new RoadmapCatalog(world360);
        Require(RoadmapValidator.Validate(world360).Count==0,"World360 invalid");
        var worldWindow=new RoadmapWindow();
        for(int i=0;i<360;i++)
        {
            worldWindow.Move(worldCatalog.ChunkForNode(i),worldCatalog.Chunks.Length);int live=0;
            for(int c=worldWindow.First;c<=worldWindow.Last;c++)live+=worldCatalog.Chunks[c].LastNode-worldCatalog.Chunks[c].FirstNode+1;
            Require(live<=30&&worldWindow.Count<=3,"World360 retains too much active content");
        }
        Console.WriteLine("PASS world360 static pipeline: "+worldCatalog.Chunks.Length+" chunks, maximum three resident chunks / thirty scene leases");
        // Measure pure planner only; not a Unity frame-time substitute.
        window.Move(0,benchmark.regions.Length);var times=new double[10000];var stopwatch=new Stopwatch();long allocated=GC.GetAllocatedBytesForCurrentThread();
        for(int i=0;i<times.Length;i++){stopwatch.Restart();window.Move(catalog.RegionAt((i%360)*420),benchmark.regions.Length);stopwatch.Stop();times[i]=stopwatch.Elapsed.TotalMilliseconds;}
        long delta=GC.GetAllocatedBytesForCurrentThread()-allocated;Array.Sort(times);
        Console.WriteLine("PASS "+cases+" negative validation cases; 10,000 planner jumps; max active regions=3");
        Require(delta==0,"Warm planner allocated");
        Console.WriteLine("Pure planner CPU only: p95="+times[9500].ToString("F6")+"ms; worst="+times[^1].ToString("F6")+"ms; warm loop allocation delta="+delta+" bytes");
        if(args.Contains("--export-main"))
        {
            Require(definition.presentation=="world3d"&&definition.regions.Sum(r=>r.nodePositions.Length)==summaries.Length,"Incomplete world rollout");
            Write(Content+"/roadmap_regions.json",definition);
            Console.WriteLine("Exported main presentation only. Puzzles, journeys, historical fixtures and saves unchanged.");
        }
        if(args.Contains("--export"))
        {
            if(!File.Exists(authoring))
            {
                var source=Copy(definition);
                // First draft branch uses existing unpublished staging journey, never unlocks it.
                var intro=source.regions[0];var anchor=intro.nodePositions[^1];
                intro.branches=intro.branches.Concat(new[]{new RoadmapBranchData{id="branch:story:lighthouse",anchorNodeId=anchor.id,journeyId="lighthouse",titleKey="journey.lighthouse.title",previewId="QC_LH008",x=.28f,y=anchor.y+420,published=false,requires=new[]{anchor.levelId}}}).ToArray();intro.height+=500;
                foreach(var region in source.regions)foreach(var node in region.nodePositions)node.world=null;
                Write(authoring,source);definition=RoadmapCompiler.BakeAuthored(Read<RoadmapDefinition>(authoring),summaries);
            }
            var extraSummaries=new List<LevelSummary>();
            var configuration=Read<Dictionary<string,Newtonsoft.Json.Linq.JToken>>(Content+"/monetization.json");
            foreach(var journey in configuration["journeys"])
            {
                var ids=journey["levelIds"].ToObject<string[]>();if(ids.Length==0)continue;
                var local=ids.Select((id,index)=>
                {
                    var level=Read<LevelData>(Content+"/Levels/"+id+".json");return new LevelSummary{id=id,number=index+1,width=level.width,height=level.height,decorSeed=level.decorSeed,lighting=level.lighting,environmentPreset=level.environmentPreset,
                        contentHash=level.contentHash,ruleVersion=level.ruleVersion,noise=level.noise,entry=level.entry,accessPoints=level.accessPoints,mapObjects=level.objects,canopies=level.canopies,exteriorWalkable=level.exteriorWalkable,environment=level.environment,
                        shade=level.guests.Any(g=>g.shade),quiet=level.guests.Any(g=>g.quiet),friends=level.friends.Length>0,fire=level.objects.Any(o=>o.assetId.Contains("campfire"))};
                }).ToArray();
                string id=(string)journey["id"],file="QuietCamp/Assets/QuietCamp/Authoring/Roadmap/"+id+".json";
                var map=RoadmapCompiler.Build(local,Array.Empty<BonusCampDefinition>(),"regions-1",id);
                if(!File.Exists(file)){var source=Copy(map);foreach(var region in source.regions)foreach(var node in region.nodePositions)node.world=null;Write(file,source);}
                map=RoadmapCompiler.BakeAuthored(Read<RoadmapDefinition>(file),local);
                Require(RoadmapValidator.Validate(map,k=>local.Any(l=>l.id==k),Locale,models.Contains).Count==0,"Journey validation");
                Write(Content+"/Roadmaps/"+id+".json",map);extraSummaries.AddRange(local);
            }
            Write(Content+"/journey_summaries.json",extraSummaries);
            Write(Content+"/level_summaries.json",summaries);Write(Content+"/roadmap_regions.json",definition);
            foreach(var item in Read<Newtonsoft.Json.Linq.JObject[]>(Fixtures+"/historical-authoring.json"))
            {
                var name=(string)item["name"];var summary=item["summary"].ToObject<LevelSummary>();
                var map=RoadmapCompiler.BakeAuthored(item["map"].ToObject<RoadmapDefinition>(),new[]{summary});
                Require(RoadmapValidator.Validate(map,k=>k==summary.id,Locale,models.Contains).Count==0,"Historical QA validation: "+name);
                foreach(var prop in map.regions[0].storyProps)Require(models.Contains(prop.assetId),"Missing story preview: "+prop.assetId);
                Write(Fixtures+"/"+name+".json",map);Write(Fixtures+"/"+name+"-summaries.json",new[]{summary});
            }
            var storySummaries=synthetic.Take(180).Select(s=>{var copy=Copy(s);copy.id="benchmark-story:"+copy.number;return copy;}).ToArray();
            var story=RoadmapCompiler.Build(storySummaries,Array.Empty<BonusCampDefinition>(),"benchmark-story-180","benchmark-story");
            Require(RoadmapValidator.Validate(story).Count==0,"Long story route validation");
            Write(Fixtures+"/benchmark-story-180.json",story);Write(Fixtures+"/benchmark-story-summaries.json",storySummaries);
            Write(Fixtures+"/benchmark-world360.json",world360);Write(Fixtures+"/foundation-slice.json",slice);Write(Fixtures+"/foundation-slice-summaries.json",summaries.Take(7).ToArray());Write(Content+"/Roadmaps/foundation_slice.json",slice);
            Write(Fixtures+"/benchmark-360.json",benchmark);Write(Fixtures+"/benchmark-summaries.json",synthetic);
            Console.WriteLine("Exported static main regions and isolated benchmark fixtures. No level/save/player changes.");
        }
    }
    static void VerifySlotRetention()
    {
        var plan=new RoadmapSlotPlan(3);
        void Frame(params int[] keys){plan.Begin();foreach(int key in keys)plan.Request(key);plan.Resolve();}
        Frame(10,11,12);int eleven=plan.SlotAt(1),twelve=plan.SlotAt(2);
        Frame(9,10,11);Require(plan.SlotAt(2)==eleven,"Incoming leading key evicted a later cache hit");
        Frame(12,11,10);Require(plan.SlotAt(1)==eleven,"Reverse traversal evicted a retained key");
        Frame(10,10,11);Require(plan.Count==2,"Duplicate request consumes budget");
        bool overflow=false;try{Frame(1,2,3,4);}catch(InvalidOperationException){overflow=true;}Require(overflow,"Slot overflow accepted");
        Frame(10,11,12);long before=GC.GetAllocatedBytesForCurrentThread();
        for(int i=0;i<10000;i++){plan.Begin();plan.Request(i+2);plan.Request(i+1);plan.Request(i);plan.Resolve();}
        Require(GC.GetAllocatedBytesForCurrentThread()==before,"Slot planning allocated during scroll");
        Console.WriteLine("PASS slot cache: leading miss, reversal, duplicate, overflow, 10,000 zero-allocation window changes");
    }
}
