using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using QuietCamp.Infrastructure;
using QuietCamp.Presentation;
using QuietCamp.Presentation.World;
using UnityEditor;

namespace QuietCamp.Editor.LevelAuthoring.WorldOps
{
    public static class WorldOpsNativeRunner
    {
        public static void Begin(WorldRun run,WorldLevelRun item)
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Another Play Mode session is active.");
            run.nativeLevel=item.levelId;run.nativeProfile=run.recipe.criteria.profiles[0];run.nativePhase="before";run.nativeState="launch";run.status="capturing";
        }
        public static bool Tick(WorldRun run)
        {
            var item=run.levels.Single(x=>x.levelId==run.nativeLevel);
            if(run.nativeState=="launch")
            {
                if(EditorApplication.isPlayingOrWillChangePlaymode)return false;
                string folder=Path.Combine(run.directory,item.levelId,"round-"+item.round,run.nativePhase,run.nativeProfile);Directory.CreateDirectory(folder);
                var document=WorldOpsStorage.Read<LevelAuthoringDocument>(run.nativePhase=="before"?item.beforePath:item.stagePath);
                string art=Path.Combine(folder,"art");Directory.CreateDirectory(art);WorldOpsStorage.Json(Path.Combine(art,item.levelId.Replace(':','_')+".json"),document.art);
                run.nativeFolder=folder;run.nativeState="starting";run.nativeDeadline=EditorApplication.timeSinceStartup+240;
                LevelAuthoringPlayDriver.Start(document,true,art);return true;
            }
            if(EditorApplication.timeSinceStartup>run.nativeDeadline&&run.nativeState!="launch"&&run.nativeState!="settling"&&run.nativeState!="failed-stop")
            {
                item.status="exception";item.error="Native Game View did not complete before its deadline.";run.nativeState="failed-stop";
            }
            if(run.nativeState=="starting")
            {
                if(!EditorApplication.isPlaying||CampSceneHost.Current?.UiReady!=true||QuietCampBootstrap.ServicesRef?.Save.MemoryOnly!=true)return false;
                run.nativeState="settling";run.nativeDeadline=EditorApplication.timeSinceStartup+3;return true;
            }
            if(run.nativeState=="settling")
            {
                if(EditorApplication.timeSinceStartup<run.nativeDeadline)return false;
                bool motion=run.recipe.operations.Any(o=>o.kind=="atmosphere.preset");
                CampaignPromoRecorder.Begin(run.nativeFolder,!motion,run.nativeProfile=="portrait"?750:1440,run.nativeProfile=="portrait"?1334:1080,run.recipe.criteria);
                run.nativeState="recording";run.nativeDeadline=EditorApplication.timeSinceStartup+240;return true;
            }
            if(run.nativeState=="recording")
            {
                string receipt=Path.Combine(run.nativeFolder,"capture.json");if(!File.Exists(receipt))return false;
                var capture=JObject.Parse(File.ReadAllText(receipt));
                if((string)capture["status"]!="captured"||capture["completed"]?.Value<bool>()!=true||capture["memoryOnly"]?.Value<bool>()!=true)
                {item.status="exception";item.error="Native placement replay failed: "+(string)capture["error"];run.nativeState="failed-stop";return true;}
                foreach(var state in new[]{"empty","placement","placed"})
                {
                    string name=state=="placement"?"gameplay":state;
                    if(!File.Exists(Path.Combine(run.nativeFolder,name+".png"))||!File.Exists(Path.Combine(run.nativeFolder,state+".diagnostics.json")))return false;
                }
                run.nativeState="stopping";EditorApplication.ExitPlaymode();return true;
            }
            if(run.nativeState=="failed-stop")
            {
                if(EditorApplication.isPlaying){EditorApplication.ExitPlaymode();return false;}
                if(EditorApplication.isPlayingOrWillChangePlaymode)return false;
                run.nativeState=null;run.nativeLevel=null;run.status="running";return true;
            }
            if(run.nativeState!="stopping"||EditorApplication.isPlayingOrWillChangePlaymode)return false;
            var profiles=run.recipe.criteria.profiles;int index=Array.IndexOf(profiles,run.nativeProfile);
            if(index+1<profiles.Length){run.nativeProfile=profiles[index+1];run.nativeState="launch";return true;}
            if(run.nativePhase=="before"){run.nativePhase="after";run.nativeProfile=profiles[0];run.nativeState="launch";return true;}
            var frames=new List<WorldFrame>();var findings=new List<ValidationFinding>();
            foreach(var phase in new[]{"before","after"})foreach(var profile in profiles)
            {
                string folder=Path.Combine(run.directory,item.levelId,"round-"+item.round,phase,profile);
                foreach(var state in new[]{"empty","placement","placed"})
                {
                    string file=Path.Combine(folder,(state=="placement"?"gameplay":state)+".png");
                    var diagnostics=JObject.Parse(File.ReadAllText(Path.Combine(folder,state+".diagnostics.json")));
                    var defects=diagnostics["findings"].ToObject<List<ValidationFinding>>();
                    foreach(var defect in defects){defect.profile=profile;defect.evidence=file;if(phase=="after")findings.Add(defect);}
                    var png=File.ReadAllBytes(file);int width=(png[16]<<24)|(png[17]<<16)|(png[18]<<8)|png[19],height=(png[20]<<24)|(png[21]<<16)|(png[22]<<8)|png[23];
                    if(width!=(profile=="portrait"?750:1440)||height!=(profile=="portrait"?1334:1080))throw new InvalidDataException("Game View dimensions do not match the capture profile.");
                    frames.Add(new WorldFrame{path=file,sha256=WorldOpsStorage.FileHash(file),profile=profile,state=state,phase=phase,findings=defects});
                }
            }
            WorldOpsService.NativeFinished(item,frames,findings);run.status="running";return true;
        }
    }
}
