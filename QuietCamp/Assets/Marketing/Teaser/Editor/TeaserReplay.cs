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
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;
using UnityEngine.InputSystem;
using static UnityEngine.Object;

namespace QuietCamp.Marketing
{
    public sealed class TeaserReplay
    {
        RecorderController _recorder;
        RecorderRenderBridge _renderBridge;
        TeaserBlackout _blackout;
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
        readonly Dictionary<InputDevice,bool> _inputStates=new();
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
            var receipt=new TeaserReceipt{shotId=shot.id,levelId=shot.levelId,frames=shot.frames,sourceHash=job.sourceHash,memoryOnly=services.Save.MemoryOnly,sampleRate=AudioSettings.outputSampleRate,recorded=job.record};
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
            foreach(var device in InputSystem.devices)_inputStates[device]=device.enabled;
            _oldView=PlayModeWindow.GetViewType();
            if(_oldView!=PlayModeWindow.PlayModeViewTypes.GameView)PlayModeWindow.SetViewType(PlayModeWindow.PlayModeViewTypes.GameView);
            var game=EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView"));game.Focus();
            PlayModeWindow.SetCustomRenderingResolution(1920,1080,"QuietCamp teaser Full HD");
            InputSystem.onDeviceChange+=DeviceChanged;
            foreach(var device in InputSystem.devices.ToArray())BlockDevice(device);
            foreach(var input in FindObjectsByType<PlacementController>(FindObjectsSortMode.None))input.enabled=false;
            foreach(var motion in FindObjectsByType<CameraAtmosphereMotion>(FindObjectsSortMode.None))motion.enabled=false;
            services.Audio.SetBusVolume(AudioBus.Master,1);services.Audio.SetBusVolume(AudioBus.Music,0);services.Audio.SetBusVolume(AudioBus.Ambience,config.ambienceGain);services.Audio.SetBusVolume(AudioBus.Sfx,config.effectsGain);services.Audio.SetBusVolume(AudioBus.Ui,0);
            _rig=Instantiate(config.cameraRig);var director=_rig.GetComponent<PlayableDirector>();
            var camera=Camera.main;camera.orthographic=true;camera.rect=new Rect(0,0,1,1);
            _blackout=new TeaserBlackout(camera);
            var brain=camera.GetComponent<CinemachineBrain>()??camera.gameObject.AddComponent<CinemachineBrain>();
            brain.UpdateMethod=CinemachineBrain.UpdateMethods.ManualUpdate;
            foreach(var track in config.timeline.GetOutputTracks())if(track is CinemachineTrack)director.SetGenericBinding(track,brain);
            var atmosphere=FindAnyObjectByType<SeasonAtmosphereView>();
            foreach(var track in config.timeline.GetOutputTracks())if(track is ActivationTrack&&atmosphere!=null)director.SetGenericBinding(track,atmosphere.gameObject);
            // Offline capture samples the authored Timeline score on an ordinary
            // in-engine AudioSource. This avoids DSP seeks on every manual Timeline
            // evaluation. The music is still recorded by Recorder with game audio.
            var audioTrack=config.timeline.GetOutputTracks().OfType<AudioTrack>().Single();
            var scoreClip=audioTrack.GetClips().Single();
            var score=(AudioPlayableAsset)scoreClip.asset;
            director.ClearGenericBinding(audioTrack);
            var music=_rig.GetComponent<AudioSource>();music.clip=score.clip;music.loop=score.loop;music.spatialBlend=0;music.volume=config.musicGain;
            music.Play();
            var volume=_rig.AddComponent<Volume>();volume.isGlobal=true;volume.priority=100;volume.sharedProfile=shot.look!=null?shot.look:config.profile;
            foreach(var light in FindObjectsByType<Light>(FindObjectsSortMode.None))if(light.type==LightType.Directional)light.intensity*=shot.sunMultiplier;
            services.Audio.SetBusVolume(AudioBus.Ambience,config.ambienceGain*shot.ambienceMultiplier);
            double start=config.shots.Take(job.shot).Sum(s=>s.frames)/30.0;
            director.timeUpdateMode=DirectorUpdateMode.Manual;director.Play();director.time=start;director.Evaluate();brain.ManualUpdate();
            EditorAuthoringPreview.CaptureClockRunning=true;
            Time.captureFramerate=30;
            int initial=shot.initialTentCount==-2?host.Session.Level.witness.Length:shot.initialTentCount<0?(shot.startWithTents?host.Session.Level.witness.Length:0):shot.initialTentCount;
            for(int i=0;i<initial;i++)Commit(host,i==shot.alternateGuest?Alternative(host,host.Session.Level.witness[i]):host.Session.Level.witness[i],receipt);
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
            Time.timeScale=1;
            music.Stop();music.timeSamples=Mathf.RoundToInt((float)((start-scoreClip.start+scoreClip.clipIn)%score.clip.length)*score.clip.frequency);music.Play();
            director.timeUpdateMode=DirectorUpdateMode.Manual;
            brain.UpdateMethod=CinemachineBrain.UpdateMethods.ManualUpdate;
            var signals=config.timeline.GetOutputTracks().OfType<SignalTrack>().SelectMany(t=>t.GetMarkers().OfType<SignalEmitter>()).Where(s=>s.time>=start&&s.time<start+shot.frames/30.0).OrderBy(s=>s.time).ToArray();
            int nextSignal=0;int mask=camera.cullingMask;var color=camera.backgroundColor;var flags=camera.clearFlags;
            for(int frame=0;frame<shot.frames;frame++)
            {
                if(job.status=="error")yield break;
                if(frame%15==0)TeaserPipeline.Progress("recording",frame);
                HideUi();
                double time=start+frame/30.0;director.time=time;director.Evaluate();brain.ManualUpdate();
                music.volume=config.musicGain*shot.musicMultiplier*(config.revision=="v01"?Mathf.Lerp(.25f,1,Mathf.Clamp01((float)time/35)):1);
                while(nextSignal<signals.Length&&time+1e-5>=signals[nextSignal].time)
                {
                    var signal=signals[nextSignal++].asset.name;
                    if(signal=="look:"+shot.id){receipt.actions.Add("frame"+frame+": shot lighting/profile applied");}
                    else if(signal.StartsWith("place-alternate-")&&int.TryParse(signal.Substring(16),out int alternate))Commit(host,Alternative(host,host.Session.Level.witness[alternate]),receipt);
                    else if(signal.StartsWith("place-")&&int.TryParse(signal.Substring(6),out int index)&&index>=0&&index<host.Session.Level.witness.Length)Commit(host,host.Session.Level.witness[index],receipt);
                    else if(TeaserGameplayEvidence.Execute(host,signal)){receipt.actions.Add("frame"+frame+": native "+signal);}
                    else if(signal=="complete-camp")
                    {
                        if(!host.Session.Check().IsSolved)throw new InvalidOperationException("Canonical camp completion rejected replay.");
                        receipt.actions.Add("frame"+frame+": canonical Check solved");
                    }
                    else throw new InvalidOperationException("Unknown replay signal: "+signal);
                }
                if(time>=config.fadeAt)
                {
                    float fade=Mathf.Clamp01((float)(time-config.fadeAt)/Mathf.Max(.01f,config.fadeSeconds));AudioListener.volume=1-fade;
                    if((config.revision=="v02"||config.revision=="v03")&&volume.profile.TryGet<ColorAdjustments>(out var grade))grade.postExposure.value=-4*fade*fade;
                }
                if(config.revision=="v03"?Mathf.RoundToInt((float)time*30)>=Mathf.RoundToInt(config.blackAt*30):time>=config.blackAt)
                {
                    _blackout.Active=true;
                    camera.cullingMask=0;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;
                    AudioListener.volume=Mathf.Clamp01(1-(float)(time-config.fadeAt)/Mathf.Max(.01f,config.fadeSeconds));
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
            receipt.renderedFrames=_renderBridge?.Frames??0;
            if(job.record&&receipt.renderedFrames!=shot.frames)throw new InvalidOperationException("Recorder render count mismatch.");
            camera.cullingMask=mask;camera.clearFlags=flags;camera.backgroundColor=color;
            _renderBridge?.Dispose();_renderBridge=null;
            _blackout?.Dispose();_blackout=null;
            _recorder?.StopRecording();if(_movie!=null)_movie.AudioInputSettings.PreserveAudio=false;
            yield return null;
            receipt.video=Directory.GetFiles(folder,"*.mp4").OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();
            if(job.record&&(receipt.video==null||new FileInfo(receipt.video).Length<10000))throw new IOException("Recorder did not produce an MP4.");
            WorldOpsStorage.Json(Path.Combine(folder,"capture.json"),receipt);
            Cleanup();TeaserPipeline.Captured(receipt);
        }
        static QuietCamp.Domain.Placement Alternative(CampSceneHost host,QuietCamp.Domain.Placement p)
        {
            for(int r=1;r<4;r++)
            {
                var candidate=p.Copy();candidate.rotation=(p.rotation+r)%4;
                if(host.Session.Preview(PlacementCommand.Place(candidate.guestId,candidate.x,candidate.z,candidate.rotation)).CanCommit)return candidate;
            }
            throw new InvalidOperationException("No legal alternate rotation for the authored replay.");
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
        void DeviceChanged(InputDevice device,InputDeviceChange change)
        {if(change==InputDeviceChange.Added||change==InputDeviceChange.Enabled)BlockDevice(device);}
        void BlockDevice(InputDevice device)
        {
            if(!_inputStates.ContainsKey(device))_inputStates[device]=device.enabled;
            if(device.enabled)InputSystem.DisableDevice(device);
        }
        void Cleanup()
        {
            EditorApplication.playModeStateChanged-=PlayChanged;
            InputSystem.onDeviceChange-=DeviceChanged;
            _renderBridge?.Dispose();_renderBridge=null;
            _blackout?.Dispose();_blackout=null;
            try{_recorder?.StopRecording();}catch(Exception e){Debug.LogWarning(e.Message);}
            if(_movie!=null)_movie.AudioInputSettings.PreserveAudio=false;
            EditorAuthoringPreview.CaptureClockRunning=false;AudioListener.volume=1;
            if(_restore){PlayModeWindow.SetCustomRenderingResolution(_oldWidth,_oldHeight,"Restored Game View");if(PlayModeWindow.GetViewType()!=_oldView)PlayModeWindow.SetViewType(_oldView);Time.captureFramerate=_oldRate;Time.timeScale=_oldTimeScale;AudioListener.pause=_oldAudioPause;EditorUtility.audioMasterMute=_oldEditorMute;UnityEngine.Application.runInBackground=_oldBackground;QualitySettings.SetQualityLevel(_oldQuality,true);if(QuietCampBootstrap.ServicesRef!=null)QuietCampBootstrap.ServicesRef.EffectiveQuality=_oldEffective;_restore=false;}
            foreach(var pair in _inputStates)if(pair.Key.added&&pair.Key.enabled!=pair.Value){if(pair.Value)InputSystem.EnableDevice(pair.Key);else InputSystem.DisableDevice(pair.Key);}
            _inputStates.Clear();
            if(_movie!=null)DestroyImmediate(_movie);if(_settings!=null)DestroyImmediate(_settings);
        }
    }
}
