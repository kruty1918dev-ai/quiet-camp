using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using QuietCamp.Infrastructure;
using QuietCamp.Presentation;
using QuietCamp.Presentation.World;
using UnityEditor;
using UnityEditor.Recorder;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace QuietCamp.Editor.LevelAuthoring.WorldOps
{
    public static class WorldOpsNativeRunner
    {
        static EditorWindow _gameView;
        static string RoundFolder(WorldRun run,WorldLevelRun item)=>Path.Combine(WorldOpsService.LevelFolder(run,item.levelId),"round-"+item.round,"attempt-"+item.attempts);
        public static void Begin(WorldRun run,WorldLevelRun item)
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Another Play Mode session is active.");
            WorldOpsService.VerifyFrames(item.beforeFrames);
            item.attempts++;run.nativeLevel=item.levelId;run.nativeProfile=run.recipe.criteria.profiles[0];run.nativePhase=item.changes?.changed==true&&item.beforeFrames.Count==0?"before":"after";run.nativeState="launch";run.status="capturing";
            run.nativeOriginalQuality=QualitySettings.GetQualityLevel();
            PlayModeWindow.GetRenderingResolution(out run.nativeOriginalWidth,out run.nativeOriginalHeight);
            run.nativeOriginalView=(int)PlayModeWindow.GetViewType();
            PlayModeWindow.SetViewType(PlayModeWindow.PlayModeViewTypes.GameView);
            EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView")).Focus();
        }
        public static bool Tick(WorldRun run)
        {
            if(_gameView==null)_gameView=EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView"));
            _gameView.Focus();_gameView.Repaint();
            if(PlayModeWindow.GetViewType()!=PlayModeWindow.PlayModeViewTypes.GameView)PlayModeWindow.SetViewType(PlayModeWindow.PlayModeViewTypes.GameView);
            EditorApplication.QueuePlayerLoopUpdate();
            var item=run.levels.Single(x=>x.levelId==run.nativeLevel);
            if(run.nativeState=="launch")
            {
                if(EditorApplication.timeSinceStartup<run.nativeRetryAt)return false;
                if(EditorApplication.isPlayingOrWillChangePlaymode&&!EditorApplication.isPlaying)return false;
                if(EditorApplication.isPlaying&&(!EditorAuthoringPreview.ControlledCapture||QuietCampBootstrap.ServicesRef?.Save.MemoryOnly!=true))throw new InvalidOperationException("Another Play Mode session is active.");
                string folder=Path.Combine(RoundFolder(run,item),run.nativePhase,run.nativeProfile);Directory.CreateDirectory(folder);
                var document=WorldOpsStorage.Read<LevelAuthoringDocument>(run.nativePhase=="before"?item.beforePath:item.stagePath);
                string art=Path.Combine(folder,"art");Directory.CreateDirectory(art);WorldOpsStorage.Json(Path.Combine(art,item.levelId.Replace(':','_')+".json"),document.art);
                run.nativeFolder=folder;
                if(!EditorApplication.isPlaying)LevelAuthoringPlayDriver.Start(document,true,art,false,true);
                else
                {
                    try{EditorAuthoringPreview.SwitchCampaignCapture(document.level,document.art,art,WorldOpsStorage.Write);}
                    catch(IOException) when(++run.nativeIoRetries<=3)
                    {run.nativeRetryAt=EditorApplication.timeSinceStartup+.5;return true;}
                    var services=QuietCampBootstrap.ServicesRef;services.Save.Session=new QuietCamp.Application.SessionSaveData();services.PendingLevelId=item.levelId;
                    SceneManager.LoadSceneAsync("Camp");
                }
                // Advance only after capture inputs were committed. A failed replacement
                // must remain resumable in launch, not wait for a scene that never started.
                run.nativeIoRetries=0;run.nativeRetryAt=0;
                run.nativeState="starting";run.nativeDeadline=EditorApplication.timeSinceStartup+240;
                return true;
            }
            if(EditorApplication.timeSinceStartup>run.nativeDeadline&&run.nativeState!="launch"&&run.nativeState!="settling"&&run.nativeState!="menu-settling"&&run.nativeState!="failed-stop")
            {
                item.status="exception";item.error="Native Game View did not complete before its deadline.";run.nativeState="failed-stop";
            }
            if(run.nativeState=="starting")
            {
                if(!EditorApplication.isPlaying||CampSceneHost.Current?.UiReady!=true||CampSceneHost.Current.Session.Level.id!=item.levelId||CampSceneHost.Current.Session.IsCompleted||CampSceneHost.Current.Session.State.Placements.Count!=0||QuietCampBootstrap.ServicesRef?.Save.MemoryOnly!=true)return false;
                run.nativeState="settling";run.nativeDeadline=EditorApplication.timeSinceStartup+3;return true;
            }
            if(run.nativeState=="settling")
            {
                if(EditorApplication.timeSinceStartup<run.nativeDeadline)return false;
                // Preview-only runs inspect native stills without claiming movie/audio
                // validation. Recorder's end-of-frame wait stalls in Device Simulator.
                bool motion=!run.recipe.previewOnly&&run.recipe.operations.Any(o=>o.kind=="atmosphere.preset");
                CampaignPromoRecorder.Begin(run.nativeFolder,!motion,run.nativeProfile=="portrait"?750:1440,run.nativeProfile=="portrait"?1334:1080,run.recipe.criteria);
                run.nativeState="recording";run.nativeDeadline=EditorApplication.timeSinceStartup+240;return true;
            }
            if(run.nativeState=="recording")
            {
                string receipt=Path.Combine(run.nativeFolder,"capture.json");if(!File.Exists(receipt))return false;
                var capture=JObject.Parse(File.ReadAllText(receipt));
                if((string)capture["status"]!="captured"||capture["completed"]?.Value<bool>()!=true||capture["memoryOnly"]?.Value<bool>()!=true)
                {item.status="exception";item.error="Native placement replay failed: "+(string)capture["error"];run.nativeState="failed-stop";return true;}
                foreach(var state in new[]{"empty","placement","placed","breeze","complete"})
                {
                    string name=state=="placement"?"gameplay":state;
                    if(!File.Exists(Path.Combine(run.nativeFolder,name+".png"))||!File.Exists(Path.Combine(run.nativeFolder,state+".diagnostics.json")))return false;
                }
                if(run.recipe.criteria.inspectMapCoverage&&Enumerable.Range(0,4).Any(i=>!File.Exists(Path.Combine(run.nativeFolder,"coverage-"+i+".png"))))return false;
                if(item.changes?.changed==true||run.recipe.criteria.includeMenu)
                {
                    QuietCampBootstrap.ServicesRef.SelectMenuCapture();
                    PlayModeWindow.SetCustomRenderingResolution(run.nativeProfile=="portrait"?750u:1440u,run.nativeProfile=="portrait"?1334u:1080u,"QuietCamp WorldOps menu");
                    SceneManager.LoadSceneAsync("MainMenu");run.nativeState="menu-starting";run.nativeDeadline=EditorApplication.timeSinceStartup+90;return true;
                }
                return Advance(run);
            }
            if(run.nativeState=="menu-starting")
            {
                if(MenuSceneHost.Current?.UiReady!=true||MenuSceneHost.Current.BackgroundLevel?.id!=item.levelId)return false;
                var drift=UnityEngine.Object.FindAnyObjectByType<MenuCameraDrift>();if(drift!=null)drift.enabled=false;
                var adaptive=UnityEngine.Object.FindAnyObjectByType<AdaptiveCampQuality>();if(adaptive!=null)adaptive.enabled=false;
                QuietCampBootstrap.ServicesRef.EffectiveQuality=run.recipe.criteria.quality;
                QualitySettings.SetQualityLevel(Math.Min(run.recipe.criteria.quality,QualitySettings.names.Length-1),true);
                EditorAuthoringPreview.CaptureClockRunning=true;
                run.nativeState="menu-settling";run.nativeDeadline=EditorApplication.timeSinceStartup+3;return true;
            }
            if(run.nativeState=="menu-settling")
            {
                if(EditorApplication.timeSinceStartup<run.nativeDeadline)return false;
                ScreenCapture.CaptureScreenshot(Path.Combine(run.nativeFolder,"menu.png"));
                run.nativeState="menu-writing";run.nativeDeadline=EditorApplication.timeSinceStartup+30;return true;
            }
            if(run.nativeState=="menu-writing")
            {
                if(!File.Exists(Path.Combine(run.nativeFolder,"menu.png")))return false;
                if(QuietCampBootstrap.ServicesRef?.Save.MemoryOnly!=true)throw new InvalidOperationException("The menu capture left memory-only progress.");
                var census=WorldOpsGeometryCensus.Capture(Camera.main);
                WorldOpsStorage.Json(Path.Combine(run.nativeFolder,"menu.geometry-census.json"),census);
                WorldOpsStorage.Json(Path.Combine(run.nativeFolder,"menu-budget.json"),WorldOpsGeometryCensus.CheckBudget(census,run.recipe.criteria.maximumSceneTriangles,item.levelId));
                WorldOpsStorage.Json(Path.Combine(run.nativeFolder,"menu-capture.json"),new{kind="native-main-menu-with-ui",levelId=item.levelId,profile=run.nativeProfile,phase=run.nativePhase,seed=run.recipe.seed,width=Screen.width,height=Screen.height,quality=QualitySettings.GetQualityLevel(),memoryOnly=true});
                return Advance(run);
            }
            if(run.nativeState=="failed-stop")
            {
                if(EditorApplication.isPlaying){EditorApplication.ExitPlaymode();return false;}
                if(EditorApplication.isPlayingOrWillChangePlaymode)return false;
                RestoreCaptureSettings(run);run.nativeState=null;run.nativeLevel=null;run.status="running";return true;
            }
            if(run.nativeState!="stopping"||EditorApplication.isPlayingOrWillChangePlaymode)return false;
            RestoreCaptureSettings(run);
            var profiles=run.recipe.criteria.profiles;
            var frames=new List<WorldFrame>(item.beforeFrames);var findings=new List<ValidationFinding>();
            foreach(var phase in item.changes?.changed==true&&item.beforeFrames.Count==0?new[]{"before","after"}:new[]{"after"})foreach(var profile in profiles)
            {
                string folder=Path.Combine(RoundFolder(run,item),phase,profile);
                var evidence=Directory.GetFiles(folder,"*",SearchOption.AllDirectories).ToDictionary(f=>f,WorldOpsStorage.FileHash);
                foreach(var state in new[]{"empty","placement","placed","breeze","complete"})
                {
                    string file=Path.Combine(folder,(state=="placement"?"gameplay":state)+".png");
                    var diagnostics=JObject.Parse(File.ReadAllText(Path.Combine(folder,state+".diagnostics.json")));
                    var defects=diagnostics["findings"].ToObject<List<ValidationFinding>>();
                    foreach(var defect in defects){defect.profile=profile;defect.evidence=file;if(phase=="after")findings.Add(defect);}
                    var png=File.ReadAllBytes(file);int width=(png[16]<<24)|(png[17]<<16)|(png[18]<<8)|png[19],height=(png[20]<<24)|(png[21]<<16)|(png[22]<<8)|png[23];
                    if(width!=(profile=="portrait"?750:1440)||height!=(profile=="portrait"?1334:1080))throw new InvalidDataException("Game View dimensions do not match the capture profile.");
                    frames.Add(new WorldFrame{path=file,sha256=WorldOpsStorage.FileHash(file),profile=profile,state=state,phase=phase,findings=defects,evidence=evidence});
                }
                if(run.recipe.criteria.inspectMapCoverage)foreach(int i in Enumerable.Range(0,4))
                {
                    string file=Path.Combine(folder,"coverage-"+i+".png");
                    var png=File.ReadAllBytes(file);int width=(png[16]<<24)|(png[17]<<16)|(png[18]<<8)|png[19],height=(png[20]<<24)|(png[21]<<16)|(png[22]<<8)|png[23];
                    if(width!=(profile=="portrait"?750:1440)||height!=(profile=="portrait"?1334:1080))throw new InvalidDataException("Coverage image dimensions do not match the capture profile.");
                    frames.Add(new WorldFrame{path=file,sha256=WorldOpsStorage.FileHash(file),profile=profile,state="environment-coverage-"+i,phase=phase,evidence=evidence});
                }
                if(item.changes?.changed==true||run.recipe.criteria.includeMenu)
                {
                    string file=Path.Combine(folder,"menu.png");var png=File.ReadAllBytes(file);
                    int width=(png[16]<<24)|(png[17]<<16)|(png[18]<<8)|png[19],height=(png[20]<<24)|(png[21]<<16)|(png[22]<<8)|png[23];
                    if(width!=(profile=="portrait"?750:1440)||height!=(profile=="portrait"?1334:1080))throw new InvalidDataException("Main menu dimensions do not match the capture profile.");
                    var defects=WorldOpsStorage.Read<List<ValidationFinding>>(Path.Combine(folder,"menu-budget.json"));
                    foreach(var defect in defects){defect.profile=profile;defect.evidence=file;if(phase=="after")findings.Add(defect);}
                    frames.Add(new WorldFrame{path=file,sha256=WorldOpsStorage.FileHash(file),profile=profile,state="menu",phase=phase,evidence=evidence,findings=defects});
                }
            }
            WorldOpsService.NativeFinished(item,frames,findings);run.status="running";return true;
        }
        static bool Advance(WorldRun run)
        {
            int next=Array.IndexOf(run.recipe.criteria.profiles,run.nativeProfile)+1;
            if(next<run.recipe.criteria.profiles.Length){run.nativeProfile=run.recipe.criteria.profiles[next];run.nativeState="launch";return true;}
            if(run.nativePhase=="before"){run.nativePhase="after";run.nativeProfile=run.recipe.criteria.profiles[0];run.nativeState="launch";return true;}
            run.nativeState="stopping";EditorApplication.ExitPlaymode();return true;
        }
        public static void RestoreCaptureSettings(WorldRun run)
        {
            if(run.nativeOriginalQuality>=0){QualitySettings.SetQualityLevel(run.nativeOriginalQuality,true);run.nativeOriginalQuality=-1;}
            if(run.nativeOriginalWidth>0&&run.nativeOriginalHeight>0)
            {PlayModeWindow.SetCustomRenderingResolution(run.nativeOriginalWidth,run.nativeOriginalHeight,"QuietCamp restored Game View");run.nativeOriginalWidth=run.nativeOriginalHeight=0;}
            if(run.nativeOriginalView>=0){PlayModeWindow.SetViewType((PlayModeWindow.PlayModeViewTypes)run.nativeOriginalView);run.nativeOriginalView=-1;}
        }
    }
}
