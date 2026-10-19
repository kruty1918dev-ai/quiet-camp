using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Kruty1918.Audio;
using Newtonsoft.Json.Linq;
using QuietCamp.Application;
using QuietCamp.Editor.LevelAuthoring;
using QuietCamp.Editor.LevelAuthoring.WorldOps;
using QuietCamp.Infrastructure;
using QuietCamp.Presentation;
using QuietCamp.Presentation.World;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Encoder;
using UnityEditor.Recorder.Input;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;

namespace QuietCamp.Marketing
{
    [Serializable] public sealed class TeaserJob
    {
        public string status, phase, error, sourceHash, output;
        public bool record;
        public int shot, onlyShot = -1, frames;
        public double deadline;
        public List<TeaserReceipt> receipts = new();
    }
    [Serializable] public sealed class TeaserReceipt
    {
        public string shotId, levelId, sourceHash, video;
        public int frames, placements, sampleRate;
        public bool memoryOnly, completed, recorded;
        public int renderedFrames;
        public float audioPeak;
        public List<string> actions = new();
    }

    /// <summary>WorldOps capture extension. Campaign documents are read, never rewritten.</summary>
    [InitializeOnLoad] public static class TeaserPipeline
    {
        const string JobKey = "QuietCamp.Marketing.Job";
        static TeaserJob _job;
        static EditorWindow _gameView;
        static string Journal => Path.GetFullPath("UserSettings/Teaser/job.json");
        public static bool Busy => Job != null && new[]{"launch","waiting","recording","stopping"}.Contains(Job.status);
        public static TeaserJob Job => _job ??= File.Exists(Journal) ? WorldOpsStorage.Read<TeaserJob>(Journal) : null;
        static TeaserPipeline()
        {
            WorldOpsBridge.TeaserCommand = Command;
            WorldOpsBridge.TeaserBusy = () => Busy;
            EditorApplication.update += Tick;
            AssemblyReloadEvents.beforeAssemblyReload += () => { if (Job?.status == "recording") Fail("Assembly reload interrupted the capture; re-record this shot."); };
        }
        public static object Command(JObject request)
        {
            string action = (string)request["action"] ?? "status";
            if (action == "status") return new { job=Job,playing=EditorApplication.isPlaying,view=PlayModeWindow.GetViewType().ToString(),frame=Time.frameCount,scale=Time.timeScale,
                screen=new[]{Screen.width,Screen.height},camera=Camera.main==null?null:new{position=Vector(Camera.main.transform.position),euler=Vector(Camera.main.transform.eulerAngles)},
                cameras=UnityEngine.Object.FindObjectsByType<CinemachineCamera>().Select(c=>new{c.name,position=Vector(c.transform.position),euler=Vector(c.transform.eulerAngles),live=CinemachineCore.IsLive(c)}).ToArray() };
            if (action == "cancel") { Fail("Cancelled by user."); return Job; }
            if (action == "create") { TeaserAssets.Create(); return Validate(); }
            if (action == "validate") return Validate();
            if (action == "reframe")
            {
                if(Busy||EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop the capture before changing cameras.");
                var config=TeaserAssets.Load();int index=(int)request["shot"];
                if(index<0||index>=config.shots.Length)throw new ArgumentOutOfRangeException("shot");
                var shot=config.shots[index];
                if(request["target"]!=null){var target=request["target"].ToObject<float[]>();if(target.Length!=3||target.Any(v=>float.IsNaN(v)||float.IsInfinity(v)||Math.Abs(v)>30))throw new ArgumentException("Invalid target.");shot.target=new Vector3(target[0],target[1],target[2]);}
                if(request["ortho"]!=null)shot.ortho=Mathf.Clamp((float)request["ortho"],3,20);
                TeaserAssets.Reframe(config,index);return Validate();
            }
            if (action == "record" || action == "preview") { Start(action == "record", (int?)request["shot"] ?? -1); return Job; }
            throw new ArgumentException("Unknown teaser action.");
        }
        public static object Validate()
        {
            var config = TeaserAssets.Load() ?? throw new InvalidOperationException("Create the teaser assets first.");
            if (config.shots == null || config.shots.Length != 7 || config.shots.Sum(s=>s.frames) != 1260) throw new InvalidDataException("Seven shots must total exactly 1260 frames.");
            if (config.width != 1920 || config.height != 1080 || config.timeline == null || Math.Abs(config.timeline.duration - 42) > .001 || config.cameraRig == null || config.music == null)
                throw new InvalidDataException("Missing assets or incorrect export/Timeline duration.");
            if (EditorBuildSettings.scenes.Any(s=>s.enabled && s.path==TeaserAssets.ScenePath)) throw new InvalidOperationException("Marketing scene must remain outside the player build.");
            foreach (var shot in config.shots)
            {
                if (shot.frames <= 0 || shot.motion == null || shot.scene!="Camp" || shot.transition != "Cut" || shot.transitionSeconds != 0) throw new InvalidDataException("Unsupported shot timing, scene or transition: " + shot.id);
                var doc = LevelAuthoringCampaign.Open(shot.levelId,preview:false);
                var errors = doc.Validate(true); if (errors.Count > 0) throw new InvalidDataException(string.Join("; ",errors));
                if (doc.level.witness == null || doc.level.witness.Length != doc.level.guests.Length) throw new InvalidDataException("Missing legal replay: " + shot.id);
            }
            return new { status="valid",frames=1260,seconds=42,width=1920,height=1080,sourceHash=SourceHash() };
        }
        public static void Start(bool record,int onlyShot=-1)
        {
            if (Busy || WorldOpsService.Busy || EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Wait for the existing Editor operation to finish.");
            if(SessionState.GetBool("QuietCamp.NativeTestsRunning",false)||File.Exists(Path.GetFullPath("Temp/ai/qc007-art-request.json")))throw new InvalidOperationException("Another native verification job is active.");
            Validate();
            if (onlyShot < -1 || onlyShot > 6) throw new ArgumentOutOfRangeException(nameof(onlyShot));
            var config = TeaserAssets.Load();
            _job = new TeaserJob { status="launch", record=record, shot=Math.Max(0,onlyShot), onlyShot=onlyShot, sourceHash=SourceHash(), output=Path.GetFullPath(config.exportFolder) };
            Directory.CreateDirectory(_job.output); Write();
            SessionState.SetBool(JobKey,true);
        }
        static void Write() => WorldOpsStorage.Json(Journal,_job);
        static float[] Vector(Vector3 v)=>new[]{v.x,v.y,v.z};
        public static void Progress(string phase,int frame){Job.phase=phase;Job.frames=frame;Job.deadline=EditorApplication.timeSinceStartup+180;Write();}
        public static string SourceHash()
        {
            var files = Directory.GetFiles("Assets/Marketing/Teaser","*",SearchOption.AllDirectories)
                .Concat(Directory.GetFiles("Assets/QuietCamp/Scripts","*.cs",SearchOption.AllDirectories))
                .Concat(Directory.GetFiles("Assets/QuietCamp/Resources/QuietCamp","*",SearchOption.TopDirectoryOnly))
                .Concat(new[]{"Packages/manifest.json","Packages/packages-lock.json"})
                .Where(p=>File.Exists(p)&&Path.GetExtension(p)!=".md").OrderBy(p=>p,StringComparer.Ordinal);
            var text = string.Join("\n", files.Select(p=>p+":"+WorldOpsStorage.FileHash(p))) + "\n" + UnityEngine.Application.unityVersion;
            var config = TeaserAssets.Load();
            if (config != null) foreach (var shot in config.shots)
            {
                string key=shot.levelId.Replace(':','_');
                text += "\n"+AssetDatabase.GetAssetDependencyHash("Assets/QuietCamp/Resources/QuietCamp/ArtCompositions/"+key+".json");
                string level="Assets/QuietCamp/Resources/QuietCamp/"+(shot.levelId.StartsWith("gen:")?"GeneratedLevels/":"Levels/")+key+".json";
                text += "\n"+AssetDatabase.GetAssetDependencyHash(level);
            }
            using var hash=SHA256.Create();return BitConverter.ToString(hash.ComputeHash(System.Text.Encoding.UTF8.GetBytes(text))).Replace("-","").ToLowerInvariant();
        }
        static void Tick()
        {
            if (!SessionState.GetBool(JobKey,false) || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            try
            {
                var job=Job; if(job==null)return;
                if(job.status=="recording")
                {
                    if(_gameView==null)_gameView=EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView"));
                    _gameView.Focus();_gameView.Repaint();EditorApplication.QueuePlayerLoopUpdate();
                    AudioListener.pause=false;
                }
                if(job.status=="recording"&&EditorApplication.timeSinceStartup>job.deadline)throw new TimeoutException("Recorder made no progress for 180 seconds; check Game View focus.");
                if(job.status=="launch")
                {
                    if(EditorApplication.isPlayingOrWillChangePlaymode)return;
                    var config=TeaserAssets.Load();var doc=LevelAuthoringCampaign.Open(config.shots[job.shot].levelId,preview:false);
                    job.status="waiting";job.deadline=EditorApplication.timeSinceStartup+240;Write();
                    LevelAuthoringPlayDriver.Start(doc,true,controlledCapture:true);return;
                }
                if(job.status=="waiting")
                {
                    if(EditorApplication.timeSinceStartup>job.deadline)throw new TimeoutException("Camp scene did not become ready.");
                    if(CampSceneHost.Current?.UiReady!=true)return;
                    if(QuietCampBootstrap.ServicesRef?.Save.MemoryOnly!=true)throw new InvalidOperationException("Capture cannot use persistent progress.");
                    job.status="recording";job.deadline=EditorApplication.timeSinceStartup+180;Write();
                    new TeaserReplay().Begin(TeaserAssets.Load(),job);return;
                }
                if(job.status=="stopping"&&EditorApplication.isPlaying)
                {
                    // Let the restored Game View finish its layout/repaint. Exiting
                    // synchronously inside a player's coroutine left Unity 6.6's
                    // GameView.OnPlayModeStateChanged with a null render target.
                    if(EditorApplication.timeSinceStartup>=job.deadline)EditorApplication.ExitPlaymode();
                    return;
                }
                if(job.status=="stopping"&&!EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    if(job.onlyShot>=0||job.shot==6){job.status="captured";SessionState.SetBool(JobKey,false);}
                    else{job.shot++;job.status="launch";}Write();
                }
            }
            catch(Exception e){Fail(e.ToString());}
        }
        public static void Captured(TeaserReceipt receipt)
        {
            if(SourceHash()!=Job.sourceHash){Fail("Sources changed during capture. This take cannot certify the current revision.");return;}
            Job.receipts.Add(receipt);Job.status="stopping";Job.deadline=EditorApplication.timeSinceStartup+.3;Write();
        }
        public static void Fail(string error)
        {
            if(Job==null)return;Job.status="error";Job.error=error;Write();SessionState.SetBool(JobKey,false);
            Debug.LogError("[Teaser] "+error);EditorApplication.delayCall+=()=>{if(EditorApplication.isPlaying)EditorApplication.ExitPlaymode();};
        }
        [MenuItem("Tools/Marketing/Teaser/Create Assets")] static void Create()=>TeaserAssets.Create();
        [MenuItem("Tools/Marketing/Teaser/Open Teaser")] static void Open(){TeaserAssets.Create();if(!EditorApplication.isPlaying&&EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())EditorSceneManager.OpenScene(TeaserAssets.ScenePath);}
        [MenuItem("Tools/Marketing/Teaser/Validate Teaser")] static void ValidateMenu()=>Debug.Log(Validate());
        [MenuItem("Tools/Marketing/Teaser/Preview Teaser")] static void Preview()=>Start(false);
        [MenuItem("Tools/Marketing/Teaser/Record Teaser")] static void Record()=>Start(true);
        [MenuItem("Tools/Marketing/Teaser/Open Export Folder")] static void Folder()=>EditorUtility.RevealInFinder(Path.GetFullPath(TeaserAssets.Load().exportFolder));
    }

}
