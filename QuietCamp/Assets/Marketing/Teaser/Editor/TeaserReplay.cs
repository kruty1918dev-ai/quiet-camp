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
using static UnityEngine.Object;

namespace QuietCamp.Marketing
{
    public sealed class TeaserReplay
    {
        RecorderController _recorder;
        RecorderRenderBridge _renderBridge;
        RecorderControllerSettings _settings;
        MovieRecorderSettings _movie;
        uint _oldWidth,_oldHeight;
        PlayModeWindow.PlayModeViewTypes _oldView;
        int _oldRate,_oldQuality,_oldEffective;
        bool _restore;
        bool _oldBackground;
        float _oldTimeScale;
        bool _oldAudioPause;
        bool _oldEditorMute;
        GameObject _rig;
        readonly float[] _samples=new float[512];
        public void Begin(TeaserConfig config,TeaserJob job)
        {
            EditorApplication.playModeStateChanged+=PlayChanged;
            CampSceneHost.Current.StartCoroutine(Guarded(Record(config,job)));
        }
        void PlayChanged(PlayModeStateChange state){if(state==PlayModeStateChange.ExitingPlayMode)Cleanup();}
        IEnumerator Guarded(IEnumerator task)
        {
            while(true)
            {
                object current;try{if(!task.MoveNext())break;current=task.Current;}catch(Exception e){Cleanup();TeaserPipeline.Fail(e.ToString());yield break;}
                yield return current;
            }
        }
        IEnumerator Record(TeaserConfig config,TeaserJob job)
        {
            var host=CampSceneHost.Current;var services=QuietCampBootstrap.ServicesRef;var shot=config.shots[job.shot];
            var receipt=new TeaserReceipt{shotId=shot.id,levelId=shot.levelId,frames=shot.frames,sourceHash=job.sourceHash,memoryOnly=services.Save.MemoryOnly,sampleRate=AudioSettings.outputSampleRate};
            if(receipt.sampleRate!=48000)throw new InvalidOperationException("Set Editor audio output to 48000 Hz before capture; current="+receipt.sampleRate);
            UnityEngine.Random.InitState(config.seed+job.shot);
            PlayModeWindow.GetRenderingResolution(out _oldWidth,out _oldHeight);_oldRate=Time.captureFramerate;
            _oldQuality=QualitySettings.GetQualityLevel();_oldEffective=services.EffectiveQuality;_restore=true;
            _oldBackground=UnityEngine.Application.runInBackground;UnityEngine.Application.runInBackground=true;
            _oldTimeScale=Time.timeScale;_oldAudioPause=AudioListener.pause;
            _oldEditorMute=EditorUtility.audioMasterMute;EditorUtility.audioMasterMute=false;AudioListener.volume=1;
            var bootstrap=FindAnyObjectByType<QuietCampBootstrap>();
            bootstrap.SendMessage("OnApplicationPause",false,SendMessageOptions.RequireReceiver);
            bootstrap.SendMessage("OnApplicationFocus",true,SendMessageOptions.RequireReceiver);
            Time.timeScale=1;AudioListener.pause=false;
            foreach(var adaptive in FindObjectsByType<AdaptiveCampQuality>(FindObjectsSortMode.None))adaptive.enabled=false;
            services.EffectiveQuality=2;QualitySettings.SetQualityLevel(Mathf.Min(2,QualitySettings.names.Length-1),true);
            foreach(var art in FindObjectsByType<CampArtView>(FindObjectsSortMode.None))art.SetQualityTier(2);
            _oldView=PlayModeWindow.GetViewType();
            PlayModeWindow.SetViewType(PlayModeWindow.PlayModeViewTypes.GameView);
            var game=EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView"));game.Focus();
            PlayModeWindow.SetCustomRenderingResolution(1920,1080,"QuietCamp teaser Full HD");
            foreach(var input in FindObjectsByType<PlacementController>(FindObjectsSortMode.None))input.enabled=false;
            foreach(var motion in FindObjectsByType<CameraAtmosphereMotion>(FindObjectsSortMode.None))motion.enabled=false;
            services.Audio.SetBusVolume(AudioBus.Master,1);services.Audio.SetBusVolume(AudioBus.Music,0);services.Audio.SetBusVolume(AudioBus.Ambience,config.ambienceGain);services.Audio.SetBusVolume(AudioBus.Sfx,config.effectsGain);services.Audio.SetBusVolume(AudioBus.Ui,0);
            _rig=Instantiate(config.cameraRig);var director=_rig.GetComponent<PlayableDirector>();
            var camera=Camera.main;camera.orthographic=true;camera.rect=new Rect(0,0,1,1);
            var brain=camera.GetComponent<CinemachineBrain>()??camera.gameObject.AddComponent<CinemachineBrain>();
            brain.UpdateMethod=CinemachineBrain.UpdateMethods.ManualUpdate;
            foreach(var track in config.timeline.GetOutputTracks())if(track is CinemachineTrack)director.SetGenericBinding(track,brain);
            var atmosphere=FindAnyObjectByType<SeasonAtmosphereView>();
            foreach(var track in config.timeline.GetOutputTracks())if(track is ActivationTrack&&atmosphere!=null)director.SetGenericBinding(track,atmosphere.gameObject);
            var volume=_rig.AddComponent<Volume>();volume.isGlobal=true;volume.priority=100;volume.sharedProfile=config.profile;
            double start=config.shots.Take(job.shot).Sum(s=>s.frames)/30.0;
            director.timeUpdateMode=DirectorUpdateMode.Manual;director.Play();director.time=start;director.Evaluate();brain.ManualUpdate();
            EditorAuthoringPreview.CaptureClockRunning=true;
            Time.captureFramerate=30;
            if(shot.startWithTents)foreach(var p in host.Session.Level.witness)Commit(host,p,receipt);
            for(int i=0;i<90;i++){HideUi();if(i%15==0)TeaserPipeline.Progress("prewarm",i);yield return null;}
            string folder=Path.Combine(job.output,"Shots",shot.id);Directory.CreateDirectory(folder);
            if(job.record)
            {
                _movie=ScriptableObject.CreateInstance<MovieRecorderSettings>();_movie.Enabled=true;
                _movie.EncoderSettings=new CoreEncoderSettings{EncodingQuality=CoreEncoderSettings.VideoEncodingQuality.High,Codec=CoreEncoderSettings.OutputCodec.MP4};
                _movie.CaptureAudio=true;_movie.AudioInputSettings.PreserveAudio=true;
                _movie.ImageInputSettings=new GameViewInputSettings{OutputWidth=1920,OutputHeight=1080};_movie.OutputFile=Path.Combine(folder,"gameplay");
                _settings=ScriptableObject.CreateInstance<RecorderControllerSettings>();_settings.AddRecorderSettings(_movie);
                _settings.SetRecordModeToFrameInterval(0,shot.frames-1);_settings.FrameRate=30;_settings.FrameRatePlayback=FrameRatePlayback.Constant;_settings.CapFrameRate=false;_settings.ExitPlayMode=false;
                _recorder=new RecorderController(_settings);_recorder.PrepareRecording();
                TeaserPipeline.Progress("recorder-prepare",0);
                for(int i=0;i<10;i++){HideUi();yield return null;}
                if(!_recorder.StartRecording())throw new InvalidOperationException("Unity Recorder refused capture.");
                _renderBridge=new RecorderRenderBridge(_recorder,camera);
            }
            // Repeated Evaluate seeks reschedule AudioClipPlayable into silence.
            // Seek once, then play the real Timeline on Recorder's fixed game clock.
            director.time=start;director.Evaluate();director.timeUpdateMode=DirectorUpdateMode.GameTime;
            brain.UpdateMethod=CinemachineBrain.UpdateMethods.LateUpdate;
            var signals=config.timeline.GetOutputTracks().OfType<SignalTrack>().SelectMany(t=>t.GetMarkers().OfType<SignalEmitter>()).Where(s=>s.time>=start&&s.time<start+shot.frames/30.0).OrderBy(s=>s.time).ToArray();
            int nextSignal=0;int mask=camera.cullingMask;var color=camera.backgroundColor;var flags=camera.clearFlags;
            for(int frame=0;frame<shot.frames;frame++)
            {
                if(job.status=="error")yield break;
                if(frame%15==0)TeaserPipeline.Progress("recording",frame);
                HideUi();
                double time=start+frame/30.0;
                var music=_rig.GetComponentInChildren<AudioSource>();if(music!=null)music.volume=config.musicGain*Mathf.Lerp(.25f,1,Mathf.Clamp01((float)time/35));
                while(nextSignal<signals.Length&&time+1e-5>=signals[nextSignal].time)
                {
                    var signal=signals[nextSignal++].asset.name;
                    if(signal.StartsWith("place-")&&int.TryParse(signal.Substring(6),out int index)&&index>=0&&index<host.Session.Level.witness.Length)Commit(host,host.Session.Level.witness[index],receipt);
                    else if(signal=="complete-camp")
                    {
                        if(!host.Session.Check().IsSolved)throw new InvalidOperationException("Canonical camp completion rejected replay.");
                        receipt.actions.Add("frame"+frame+": canonical Check solved");
                    }
                    else throw new InvalidOperationException("Unknown replay signal: "+signal);
                }
                if(time>=41.4)
                {
                    camera.cullingMask=0;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;
                    AudioListener.volume=Mathf.Clamp01(1-(float)(time-41.4)/.12f);
                }
                if(frame==1||frame==shot.frames/2||frame==shot.frames-24)ScreenCapture.CaptureScreenshot(Path.Combine(folder,"frame-"+frame.ToString("D4")+".png"));
                AudioListener.GetOutputData(_samples,0);foreach(float sample in _samples)receipt.audioPeak=Mathf.Max(receipt.audioPeak,Mathf.Abs(sample));
                if(job.record)
                {
                    _renderBridge.Arm();
                    while(_renderBridge.Frames<=frame){if(_renderBridge.Error!=null)throw _renderBridge.Error;yield return null;}
                }
                else yield return null;
            }
            receipt.placements=host.Session.State.Placements.Count;receipt.completed=host.Session.IsCompleted;
            camera.cullingMask=mask;camera.clearFlags=flags;camera.backgroundColor=color;
            _renderBridge?.Dispose();_renderBridge=null;
            _recorder?.StopRecording();if(_movie!=null)_movie.AudioInputSettings.PreserveAudio=false;
            yield return null;
            receipt.video=Directory.GetFiles(folder,"*.mp4").OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();
            if(job.record&&(receipt.video==null||new FileInfo(receipt.video).Length<10000))throw new IOException("Recorder did not produce an MP4.");
            WorldOpsStorage.Json(Path.Combine(folder,"capture.json"),receipt);
            Cleanup();TeaserPipeline.Captured(receipt);
        }
        static void Commit(CampSceneHost host,QuietCamp.Domain.Placement p,TeaserReceipt receipt)
        {
            if(!host.Session.TryCommit(PlacementCommand.Place(p.guestId,p.x,p.z,p.rotation),out var report))throw new InvalidOperationException("Canonical placement refused: "+p.guestId);
            receipt.actions.Add("place "+p.guestId+" @ "+p.x+","+p.z+" / "+p.rotation);
        }
        static void HideUi()
        {
            foreach(var canvas in FindObjectsByType<Canvas>(FindObjectsSortMode.None))canvas.enabled=false;
            foreach(var text in FindObjectsByType<TextMesh>(FindObjectsSortMode.None))text.GetComponent<Renderer>().enabled=false;
        }
        void Cleanup()
        {
            EditorApplication.playModeStateChanged-=PlayChanged;
            _renderBridge?.Dispose();_renderBridge=null;
            try{_recorder?.StopRecording();}catch(Exception e){Debug.LogWarning(e.Message);}
            if(_movie!=null)_movie.AudioInputSettings.PreserveAudio=false;
            EditorAuthoringPreview.CaptureClockRunning=false;AudioListener.volume=1;
            if(_restore){PlayModeWindow.SetCustomRenderingResolution(_oldWidth,_oldHeight,"Restored Game View");PlayModeWindow.SetViewType(_oldView);Time.captureFramerate=_oldRate;Time.timeScale=_oldTimeScale;AudioListener.pause=_oldAudioPause;EditorUtility.audioMasterMute=_oldEditorMute;UnityEngine.Application.runInBackground=_oldBackground;QualitySettings.SetQualityLevel(_oldQuality,true);if(QuietCampBootstrap.ServicesRef!=null)QuietCampBootstrap.ServicesRef.EffectiveQuality=_oldEffective;_restore=false;}
            if(_movie!=null)DestroyImmediate(_movie);if(_settings!=null)DestroyImmediate(_settings);
        }
    }
}
