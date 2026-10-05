using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using QuietCamp.Domain;
using QuietCamp.Infrastructure;
using UnityEditor;
using UnityEngine;

namespace QuietCamp.Editor
{
    public static class VisibleCampContent
    {
        /// <summary>Introduce the fire in the existing third-level obstacle.
        /// Keep IDs/footprints/witness and archive the previous album revision.</summary>
        public static void UpgradeEarlyFire()
        {
            const string path="Assets/QuietCamp/Resources/QuietCamp/Levels/QC003.json";
            var level=LevelLoader.Load("QC003");
            if(level.objects.Any(o=>o.assetId=="campfire_stones"))return;
            if(level.blocked.Length!=1 || level.guests.Any(g=>g.quiet))throw new InvalidOperationException("Unexpected tutorial content; review before upgrading.");
            var archive="Assets/QuietCamp/Resources/QuietCamp/ContentRevisions/"+level.contentHash+".json";
            if(!File.Exists(archive))File.WriteAllText(archive,JsonConvert.SerializeObject(level,Formatting.Indented));
            var cell=level.blocked[0];level.noise=new[]{new[]{cell[0],cell[1]}};
            var prop=level.objects.Single(o=>o.x==cell[0]&&o.z==cell[1]);prop.assetId="campfire_stones";
            var issues=LevelContentValidator.Validate(level);if(issues.Count>0)throw new InvalidOperationException(string.Join(",",issues));
            var solver=new CampSolver(level,Array.Empty<Placement>(),10);var status=solver.Step();
            while(status==CampSolver.Status.Searching)status=solver.Step(.05);
            if(status!=CampSolver.Status.Solved)throw new InvalidOperationException("Fire tutorial no longer solves: "+status);
            level.contentHash="";
            using(var sha=SHA256.Create())level.contentHash=BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(level)))).Replace("-","").ToLowerInvariant();
            File.WriteAllText(path,JsonConvert.SerializeObject(level,Formatting.Indented));
            const string summaryPath="Assets/QuietCamp/Resources/QuietCamp/level_summaries.json";
            var summaries=JsonConvert.DeserializeObject<LevelSummary[]>(File.ReadAllText(summaryPath));summaries.Single(s=>s.id==level.id).fire=true;
            File.WriteAllText(summaryPath,JsonConvert.SerializeObject(summaries,Formatting.Indented));
            AssetDatabase.Refresh();Debug.Log("[VisibleCamp] Archived and upgraded QC003; original witness and fresh solver both solve.");
        }
    }
}
