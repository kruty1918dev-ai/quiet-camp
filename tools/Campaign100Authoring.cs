using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using QuietCamp.Domain;
using QuietCamp.Infrastructure;

static class Campaign100Authoring
{
    public static void Publish(string root)
    {
        string design=Path.Combine(root,"Design/Campaign100/2026-10-11");
        string assets=Path.Combine(root,"QuietCamp/Assets/QuietCamp");
        string content=Path.Combine(assets,"Resources/QuietCamp");
        var levels=JsonConvert.DeserializeObject<LevelData[]>(File.ReadAllText(Path.Combine(design,"Authoring/prepared-levels.json")));
        if(levels.Length!=100||levels.Select(l=>l.id).Distinct().Count()!=100)throw new Exception("Exactly 100 unique levels required");
        foreach(var level in levels)
        {
            var issues=LevelContentValidator.Validate(level);
            if(issues.Count>0)throw new Exception(level.id+": "+string.Join(",",issues));
            if(!RuleEvaluator.Evaluate(level,level.witness).IsSolved)throw new Exception("Invalid witness "+level.id);
            var solver=new CampSolver(level,Array.Empty<Placement>(),20);
            var status=solver.Step();while(status==CampSolver.Status.Searching)status=solver.Step(.05);
            if(status!=CampSolver.Status.Solved||!RuleEvaluator.Evaluate(level,solver.Solution).IsSolved)throw new Exception("Independent solver "+level.id);
            level.contentHash=CampContent.CalculateHash(level);
        }
        // Complete validation precedes publication. Previous exact sources remain in design and revisions.
        Directory.CreateDirectory(Path.Combine(content,"ContentRevisions"));
        foreach(var path in Directory.GetFiles(Path.Combine(design,"Baseline/Levels"),"*.json"))
        {
            var old=JsonConvert.DeserializeObject<LevelData>(File.ReadAllText(path));
            string revision=Path.Combine(content,"ContentRevisions",old.contentHash+".json");
            if(!File.Exists(revision))File.Copy(path,revision);
        }
        File.Copy(Path.Combine(design,"Authoring/curated-gameplay-models.json"),Path.Combine(content,"campaign100_models.json"),true);
        var ids=new HashSet<string>(levels.Select(l=>l.id));
        string archive=Path.Combine(assets,"Editor/ContentArchive/DeferredLevels");Directory.CreateDirectory(archive);
        foreach(var folder in new[]{"Levels","GeneratedLevels"})
        foreach(var path in Directory.GetFiles(Path.Combine(content,folder),"*.json"))
        {
            var old=JsonConvert.DeserializeObject<LevelData>(File.ReadAllText(path));
            if(old.id=="QC_TEST"||ids.Contains(old.id))continue;
            string destination=Path.Combine(archive,Path.GetFileName(path));
            File.Move(path,destination);
            if(File.Exists(path+".meta"))File.Move(path+".meta",destination+".meta");
        }
        foreach(var level in levels)
        {
            string folder=level.id.StartsWith("gen:")?"GeneratedLevels":"Levels";
            File.WriteAllText(Path.Combine(content,folder,level.id.Replace(':','_')+".json"),JsonConvert.SerializeObject(level,Formatting.Indented)+"\n");
        }
        var campaign=JObject.Parse(File.ReadAllText(Path.Combine(design,"Baseline/campaign.json")));
        campaign["contentRevision"]="cozy-campaign-100-v1";campaign["bundledLevelLimit"]=100;
        campaign["mvpLevelIds"]=JArray.FromObject(levels.Take(10).Select(l=>l.id));
        campaign["generatedLevelIds"]=JArray.FromObject(levels.Skip(10).Select(l=>l.id));
        campaign["campaignLevelIds"]=JArray.FromObject(levels.Select(l=>l.id));
        campaign["levels"]=JArray.FromObject(levels.Select(l=>new{id=l.id,contentHash=l.contentHash}));
        string[] districts={"glades","forest","farms","stations","sunfield","stillwater","villages","highlands","passes","haven"};
        campaign["districts"]=JArray.FromObject(districts.Select((id,i)=>new{id,act=i<2?1:2,from=i*10+1,to=(i+1)*10}));
        File.WriteAllText(Path.Combine(content,"campaign.json"),campaign.ToString()+"\n");
        var summaries=levels.Select(l=>new LevelSummary{id=l.id,number=l.order,width=l.width,height=l.height,
            environmentPreset=l.environmentPreset,lighting=l.lighting,contentHash=l.contentHash,ruleVersion=l.ruleVersion,decorSeed=l.decorSeed,
            shade=l.guests.Any(g=>g.shade),quiet=l.guests.Any(g=>g.quiet),friends=l.friends.Length>0,fire=l.noise.Length>0,
            entry=l.entry,mapObjects=l.objects,accessPoints=l.accessPoints,canopies=l.canopies,exteriorWalkable=l.exteriorWalkable,noise=l.noise,environment=l.environment}).ToArray();
        File.WriteAllText(Path.Combine(content,"level_summaries.json"),JsonConvert.SerializeObject(summaries,Formatting.Indented)+"\n");
        var monetization=JObject.Parse(File.ReadAllText(Path.Combine(design,"Baseline/monetization.json")));
        foreach(var journey in monetization["journeys"].OfType<JObject>())journey["published"]=false;
        File.WriteAllText(Path.Combine(content,"monetization.json"),monetization.ToString()+"\n");
        File.WriteAllText(Path.Combine(content,"bonus_camps.json"),"[]\n");
        File.WriteAllText(Path.Combine(design,"publication-receipt.json"),JsonConvert.SerializeObject(new{
            publishedUtc=DateTime.UtcNow.ToString("o"),count=100,uniqueIds=100,validator=true,witness=true,independentSolver=true,
            oldSnapshotsPreserved=true,oldOptionalJourneysDeferred=true,playerBuild=false,nativeAcceptance=false,
            content=levels.Select(l=>new{id=l.id,number=l.order,hash=l.contentHash})},Formatting.Indented)+"\n");
        Console.WriteLine("PASS published exactly 100 validated/witness/independently solved levels; native art acceptance remains pending.");
    }
}
