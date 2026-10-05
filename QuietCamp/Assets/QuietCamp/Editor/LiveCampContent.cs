using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json;
using QuietCamp.Domain;
using QuietCamp.Infrastructure;
using UnityEditor;
using UnityEngine;

namespace QuietCamp.Editor
{
    public static class LiveCampContent
    {
        const string Legacy = "Assets/QuietCamp/Resources/QuietCamp/LegacyLevels";

        // Run BEFORE changing campaign recipes. Never overwrite an existing archive.
        public static void ArchiveLegacy()
        {
            Directory.CreateDirectory(Legacy);
            foreach (var id in LevelLoader.MvpLevelIds())
            {
                var file = Path.Combine(Legacy, ArchiveName(id) + ".json");
                if (File.Exists(file)) continue;
                var level = LevelLoader.Load(id);
                File.WriteAllText(file, JsonConvert.SerializeObject(level, Formatting.Indented));
                Debug.Log("[LiveCamp] Archived " + id);
            }
            var test = Path.Combine(Legacy, "QC_TEST.json");
            if (!File.Exists(test)) File.Copy("Assets/QuietCamp/Resources/QuietCamp/Levels/QC_TEST.json", test);
            AssetDatabase.Refresh();
        }

        public static string ArchiveName(string id) => id.Replace(':', '_');

        public static void PrepareRainMeshes()
        {
            foreach(var name in new[]{"tent_smallOpen","tent_detailedOpen","stone_largeA","log","log_stack",
                "stump_round","tree_default","tree_pineRoundA","campfire_stones"})
            {
                var path="Assets/ThirdParty/KenneyNature/Models/"+name+".obj";
                var importer=AssetImporter.GetAtPath(path) as ModelImporter;
                if(importer==null)throw new InvalidOperationException("Missing rain mesh "+path);
                if(!importer.isReadable){importer.isReadable=true;importer.SaveAndReimport();}
                Debug.Log("[LiveCamp] Readable rain mesh "+name);
            }
            AssetDatabase.SaveAssets();
        }

        // Preserve the exact current campaign before moving its access points.
        // Existing solutions remain valid; old album snapshots keep their content.
        public static void UpgradeAccess()
        {
            const string revisions = "Assets/QuietCamp/Resources/QuietCamp/ContentRevisions";
            Directory.CreateDirectory(revisions);
            var changes = new List<(string file, LevelData level)>();
            var ids = LevelLoader.MvpLevelIds();
            for (int i = 0; i < ids.Count; i++)
            {
                var level = LevelLoader.Load(ids[i]);
                var archive = Path.Combine(revisions, level.contentHash + ".json");
                if (!File.Exists(archive)) File.WriteAllText(archive, JsonConvert.SerializeObject(level, Formatting.Indented));
                var oldAccess=JsonConvert.SerializeObject(new { level.entry, level.accessPoints });
                GeneratedCampSource.ConfigureAccess(level, i + 1, true);
                var errors = LevelContentValidator.Validate(level);
                if (errors.Count > 0) throw new InvalidOperationException(ids[i] + ": " + string.Join(",", errors));
                if(oldAccess!=JsonConvert.SerializeObject(new { level.entry, level.accessPoints }))
                {
                    level.contentHash = "";
                    using (var sha = System.Security.Cryptography.SHA256.Create())
                        level.contentHash = BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(
                            JsonConvert.SerializeObject(level)))).Replace("-", "").ToLowerInvariant();
                }
                var file = i < 10 ? "Assets/QuietCamp/Resources/QuietCamp/Levels/" + ids[i] + ".json"
                    : "Assets/QuietCamp/Resources/QuietCamp/GeneratedLevels/" + ArchiveName(ids[i]) + ".json";
                changes.Add((file, level));
            }
            // Validate the whole upgrade before writing any playable content.
            foreach (var change in changes)
            {
                File.WriteAllText(change.file, JsonConvert.SerializeObject(change.level, Formatting.Indented));
                Debug.Log("[LiveCamp] Upgraded access " + change.level.id);
            }
            AssetDatabase.Refresh();
        }

        public static void BuildLive()
        {
            if (!File.Exists(Path.Combine(Legacy, "gen_qc_camp_20.json")))
                throw new InvalidOperationException("Archive all old levels before authoring live content.");
            const string revisions="Assets/QuietCamp/Resources/QuietCamp/ContentRevisions";
            Directory.CreateDirectory(revisions);
            var summaries = new List<LevelSummary>();
            var staged=new List<(string path,string content)>();
            var ids = LevelLoader.MvpLevelIds();
            for (int i = 0; i < ids.Count; i++)
            {
                var id = ids[i];
                var current=LevelLoader.Load(id);
                var archive=Path.Combine(revisions,current.contentHash+".json");
                var level=CampCampaignAuthoring.Create(id,i+1,CampCampaignAuthoring.SeedForNumber(i+1));
                level.contentHash=CampContent.CalculateHash(level);
                if(current.contentHash!=level.contentHash&&!File.Exists(archive))staged.Add((archive,JsonConvert.SerializeObject(current,Formatting.Indented)));
                var errors = LevelContentValidator.Validate(level);
                if (errors.Count > 0) throw new InvalidOperationException(id + ": " + string.Join(",",errors));
                var solver=new CampSolver(level,Array.Empty<Placement>(),20);
                var status=solver.Step();while(status==CampSolver.Status.Searching)status=solver.Step(.05);
                if(status!=CampSolver.Status.Solved)throw new InvalidOperationException(id+": independent solver "+status);
                string file=i<10?"Assets/QuietCamp/Resources/QuietCamp/Levels/"+id+".json"
                    :"Assets/QuietCamp/Resources/QuietCamp/GeneratedLevels/"+ArchiveName(id)+".json";
                staged.Add((file,JsonConvert.SerializeObject(level,Formatting.Indented)));
                summaries.Add(new LevelSummary { id=id, number=i+1, width=level.width, height=level.height,
                    environmentPreset=level.environmentPreset, shade=level.guests.Any(g=>g.shade),
                    quiet=level.guests.Any(g=>g.quiet), friends=level.friends.Length>0, fire=level.noise.Length>0,
                    entry=level.entry, mapObjects=level.objects, lighting=level.lighting,
                    decorSeed=level.decorSeed, accessPoints=level.accessPoints, canopies=level.canopies,
                    exteriorWalkable=level.exteriorWalkable,environment=level.environment });
                Debug.Log("[LiveCamp] Authored and solved " + id + " " + level.width + "x" + level.height);
            }
            staged.Add(("Assets/QuietCamp/Resources/QuietCamp/level_summaries.json",JsonConvert.SerializeObject(summaries,Formatting.Indented)));
            var campaignPath="Assets/QuietCamp/Resources/QuietCamp/campaign.json";
            var campaign=Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(campaignPath));
            campaign["contentVersion"]=CampCampaignAuthoring.Version;
            campaign["levels"]=new Newtonsoft.Json.Linq.JArray(staged.Where(x=>x.path.Contains("/Levels/")||x.path.Contains("/GeneratedLevels/"))
                .Select(x=>Newtonsoft.Json.Linq.JObject.Parse(x.content)).Select(x=>new Newtonsoft.Json.Linq.JObject{["id"]=x["id"],["contentHash"]=x["contentHash"]}));
            staged.Add((campaignPath,campaign.ToString(Newtonsoft.Json.Formatting.Indented)));
            PublishStaged(staged);
            AssetDatabase.Refresh();
        }
        static void PublishStaged(List<(string path,string content)> staged)
        {
            // Validate every level before a single campaign write. Restore all
            // previous bytes if the filesystem rejects a staged publication.
            var originals=staged.ToDictionary(x=>x.path,x=>File.Exists(x.path)?File.ReadAllBytes(x.path):null);
            try
            {
                foreach(var item in staged){Directory.CreateDirectory(Path.GetDirectoryName(item.path));File.WriteAllText(item.path+".tmp",item.content);}
                foreach(var item in staged){if(File.Exists(item.path))File.Delete(item.path);File.Move(item.path+".tmp",item.path);}
            }
            catch
            {
                foreach(var item in originals){if(item.Value==null){if(File.Exists(item.Key))File.Delete(item.Key);}else File.WriteAllBytes(item.Key,item.Value);}
                throw;
            }
            finally{foreach(var item in staged)if(File.Exists(item.path+".tmp"))File.Delete(item.path+".tmp");}
        }
    }
}
