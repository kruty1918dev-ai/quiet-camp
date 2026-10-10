using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using QuietCamp.Application;
using QuietCamp.Composition;

static class CinematicPilotContracts
{
    public static void Run(Dictionary<string,SceneCompositionDocument> docs,Dictionary<string,VisualAssetDefinition> assets,Dictionary<string,EnsembleTemplate> templates)
    {
        void Require(bool ok,string reason){if(!ok)throw new Exception(reason);}
        Require(docs.Count==1,"Pilot must be one continuous world");
        var doc=docs.Values.Single();
        Require(doc.nodes.Select(n=>n.id).SequenceEqual(RoadmapPilotPolicy.LevelIds),"Pilot IDs/order changed");
        var result=SceneComposer.Compose(doc,assets,templates);
        Require(result.Valid,"Pilot semantic composition rejected");
        Require(result.parcels.Count==doc.ensembles.Length,"Partial ensemble publication");
        Require(JsonConvert.SerializeObject(result)==JsonConvert.SerializeObject(SceneComposer.Compose(doc,assets,templates)),"Non-deterministic pilot");
        Require(!result.instances.Select(i=>i.asset).Concat(doc.landmarks.Select(l=>l.asset)).Any(id=>id.IndexOf("tent",StringComparison.OrdinalIgnoreCase)>=0),"Tent in roadmap dependency");
        Require(result.spans.Any(s=>s.id=="reclaimed-yard/approach-gate"&&s.height==0),"Yard has no owned approach");
        // Intentional growth is allowed only for a canopy attached to its structural owner.
        var altered=JsonConvert.DeserializeObject<Dictionary<string,EnsembleTemplate>>(JsonConvert.SerializeObject(templates));
        var growth=altered["pilot.yard"].roles.Single(r=>r.id=="roof-tree");growth.growsThrough=null;
        Require(SceneComposer.Compose(doc,assets,altered).diagnostics.Any(d=>d.code=="role-overlap"),"Ordinary collisions were suppressed");
        growth.growsThrough="well";
        Require(SceneComposer.Compose(doc,assets,altered).diagnostics.Any(d=>d.code=="invalid-growth-owner"),"Invalid growth owner admitted");
        for(int completedCount=0;completedCount<=12;completedCount++)
        {
            var completed=new HashSet<string>(Enumerable.Range(1,completedCount).Select(i=>"QC"+i.ToString("000")));
            Require(RoadmapPilotPolicy.Frontier(completed.Contains)==Math.Min(completedCount,4),"Incorrect frontier");
            Require(!RoadmapPilotPolicy.CanPlay("QC006",completed.Contains,id=>true),"Launch outside pilot");
            Require(completed.Count==completedCount,"Older progress was trimmed");
        }
        var legacy=new ProgressionService();legacy.Restore(new[]{"QC012"},"QC012",3);
        Require(RoadmapPilotPolicy.LevelIds.All(id=>RoadmapPilotPolicy.Completed(legacy,id)),"Older progress with gaps not presented as finished");
        Require(legacy.CompletedCount==1&&legacy.CosmeticFlags==3,"Legacy data rewritten");
        legacy.Restore(new[]{"gen:qc_camp:12"},"gen:qc_camp:12",0);
        Require(RoadmapPilotPolicy.LevelIds.All(id=>RoadmapPilotPolicy.Completed(legacy,id)),"Older generated main progress not recognized");
        legacy.Restore(new[]{"QC_LH008"},"QC_LH008",0);
        Require(!RoadmapPilotPolicy.LegacyBeyondPilot(legacy),"Side journey mistaken for older main progress");
        Console.WriteLine("PASS cinematic pilot: five IDs, complete ensembles, deterministic composition, owned approaches, explicit growth, no tents, progress/access boundaries");
    }
}
