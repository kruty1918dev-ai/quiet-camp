using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using QuietCamp.Domain;
using UnityEditor;

namespace QuietCamp.Editor.LevelAuthoring.WorldOps
{
    /// <summary>Repair an obsolete reference solution without changing a puzzle's rules or scenery.</summary>
    public static class WorldOpsWitnessRepair
    {
        public static object Run(string repositoryProject,string[] levelIds)
        {
            if(WorldOpsService.Busy||EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop native work first.");
            if(string.IsNullOrWhiteSpace(repositoryProject)||levelIds==null||levelIds.Length==0||levelIds.Any(string.IsNullOrWhiteSpace))throw new ArgumentException("An explicit repository and campaign levels are required.");
            var writes=new Dictionary<string,string>();var sources=new Dictionary<string,string>();var changed=new List<string>();
            foreach(var project in new[]{Path.GetFullPath(repositoryProject),WorldOpsService.EditorProject}.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                string root=Path.Combine(project,"Assets/QuietCamp/Resources/QuietCamp");
                var repaired=new Dictionary<string,LevelAuthoringDocument>();
                foreach(string levelId in levelIds.Distinct())
                {
                if(!LevelAuthoringCampaign.ReadCards(root).Any(c=>c.id==levelId))throw new ArgumentException("Not a campaign level.");
                var doc=LevelAuthoringCampaign.Open(levelId,root,Path.Combine(project,"Temp/WorldOpsWitnessRepair"),false);
                if(RuleEvaluator.Evaluate(doc.level,doc.level.witness).IsSolved&&LevelContentValidator.Validate(doc.level).Count==0)continue;
                // Preserve the already legal non-quiet placements when possible.
                var pinned=(doc.level.witness??Array.Empty<Placement>()).Where(p=>doc.level.guests.Any(g=>g.id==p.guestId&&!g.quiet)).ToArray();
                var solver=new CampSolver(doc.level,pinned,3);
                while(solver.CurrentStatus==CampSolver.Status.Searching)solver.Step(.01);
                if(solver.CurrentStatus!=CampSolver.Status.Solved)
                {
                    solver=new CampSolver(doc.level,Array.Empty<Placement>(),5);
                    while(solver.CurrentStatus==CampSolver.Status.Searching)solver.Step(.01);
                }
                if(solver.CurrentStatus!=CampSolver.Status.Solved)throw new InvalidDataException("Reference repair: "+solver.CurrentStatus);
                doc.level.witness=solver.Solution;
                if(LevelContentValidator.Validate(doc.level).Count>0||!RuleEvaluator.Evaluate(doc.level,doc.level.witness).IsSolved)throw new InvalidDataException("Repaired witness failed canonical validation.");
                repaired.Add(levelId,doc);
                }
                // Validate the complete prospective campaign before any write.
                var invalid=new List<string>();
                foreach(var card in LevelAuthoringCampaign.ReadCards(root))
                {
                    string path=Path.Combine(root,card.id.StartsWith("gen:")?"GeneratedLevels":"Levels",card.id.Replace(':','_')+".json");
                    sources[path]=WorldOpsStorage.FileHash(path);
                    var level=repaired.TryGetValue(card.id,out var replacement)?replacement.level:WorldOpsStorage.Read<LevelData>(path);
                    if(LevelContentValidator.Validate(level).Count>0||!RuleEvaluator.Evaluate(level,level.witness).IsSolved)invalid.Add(card.id);
                }
                if(invalid.Count>0)throw new InvalidDataException("Campaign levels need explicit repair: "+string.Join(", ",invalid));
                foreach(var pair in repaired)
                {
                    foreach(var write in LevelAuthoringCampaign.PrepareApply(pair.Value,root,false,includeArt:false))writes.Add(write.Key,write.Value);
                    changed.Add(project+":"+pair.Key);
                }
            }
            if(sources.Any(p=>WorldOpsStorage.FileHash(p.Key)!=p.Value))throw new InvalidOperationException("Campaign changed during preparation.");
            var originals=writes.Keys.ToDictionary(p=>p,p=>File.Exists(p)?File.ReadAllBytes(p):null);
            try
            {
                foreach(var pair in writes){Directory.CreateDirectory(Path.GetDirectoryName(pair.Key));File.WriteAllText(pair.Key+".pending",pair.Value);}
                foreach(var path in writes.Keys)AuthoringFileTransaction.CommitPrepared(path);
            }
            catch
            {
                foreach(var pair in originals){if(pair.Value==null){if(File.Exists(pair.Key))File.Delete(pair.Key);}else File.WriteAllBytes(pair.Key,pair.Value);}
                throw;
            }
            string report=Path.Combine(WorldOpsService.Home,"witness-repair-"+DateTime.UtcNow.ToString("yyyyMMddTHHmmssfff")+".json");
            WorldOpsStorage.Json(report,new{levelIds,changed,rulesChanged=false,artChanged=false,sources,files=writes.Keys.ToArray()});
            AssetDatabase.Refresh();return new{levelIds,changed,report};
        }
    }
}
