using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Kruty1918.UIActions.API;
using Newtonsoft.Json;
using QuietCamp.Infrastructure;
using QuietCamp.Presentation;
using QuietCamp.Presentation.World;
using UnityEditor;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Encoder;
using UnityEditor.Recorder.Input;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace QuietCamp.Editor.LevelAuthoring
{
    /// <summary>Editor-only replay of real placement input, recorded from Game View and its audio mix.</summary>
    public sealed class CampaignPromoRecorder
    {
        const int Frames = 132;
        static CampaignPromoRecorder _active;
        RecorderController _controller;
        RecorderControllerSettings _settings;
        MovieRecorderSettings _movie;
        Mouse _mouse;
        InputDevice[] _hardware;
        EditorPointerInputScope _inputScope;
        string _output;
        readonly List<object> _actions = new();
        bool _finished;
        int _firstFrame;
        bool _gridInStill;
        int _peakRainRunoff;
        float _ambientPeakRms,_windMinimum=float.PositiveInfinity,_windMaximum;
        readonly float[] _audioSamples=new float[512];
        bool _stillsOnly, _screenChanged;
        uint _oldWidth, _oldHeight;
        int _oldCaptureRate;
        object _cameraBefore, _cameraAfter;
        AdaptiveCampQuality _adaptiveQuality;
        bool _adaptiveWasEnabled;
        int _oldEffectiveQuality,_oldRenderQuality;
        int _width=1440,_height=1080,_quality=1;
        WorldOps.WorldCriteria _worldCriteria;

        public static object Begin(string output, bool stillsOnly = false,int width=1440,int height=1080,WorldOps.WorldCriteria criteria=null)
        {
            if (_active != null) throw new InvalidOperationException("A recording is already active.");
            if (!EditorApplication.isPlaying || !EditorAuthoringPreview.CampaignPreview || QuietCampBootstrap.ServicesRef?.Save.MemoryOnly != true)
                throw new InvalidOperationException("Promo capture requires the memory-only campaign preview.");
            var host = CampSceneHost.Current;
            if (host?.UiReady != true || host.Session.State.Placements.Count != 0 || host.Session.IsCompleted)
                throw new InvalidOperationException("Start with an empty, ready campaign level.");
            if (string.IsNullOrEmpty(output) || !Path.IsPathRooted(output)) throw new ArgumentException("An absolute capture directory is required.");
            Directory.CreateDirectory(output);
            var runner = new CampaignPromoRecorder(); _active = runner;
            runner._output = output;
            runner._stillsOnly = stillsOnly;
            runner._width=width;runner._height=height;runner._worldCriteria=criteria;runner._quality=criteria?.quality??1;
            EditorApplication.playModeStateChanged += runner.PlayModeChanged;
            AssemblyReloadEvents.beforeAssemblyReload += runner.Interrupted;
            host.StartCoroutine(runner.Guarded(runner.Record()));
            return new { status = "promo-recording", id = host.Session.Level.id, output };
        }

        IEnumerator Guarded(IEnumerator work)
        {
            while (true)
            {
                object next;
                try { if (!work.MoveNext()) break; next = work.Current; }
                catch (Exception error) { Finish(error.ToString()); yield break; }
                yield return next;
            }
            Finish(null);
        }

        IEnumerator Record()
        {
            var host = CampSceneHost.Current;
            var services = QuietCampBootstrap.ServicesRef;
            _adaptiveQuality=UnityEngine.Object.FindAnyObjectByType<AdaptiveCampQuality>();
            _adaptiveWasEnabled=_adaptiveQuality!=null&&_adaptiveQuality.enabled;
            if(_adaptiveQuality!=null)_adaptiveQuality.enabled=false;
            _oldEffectiveQuality=services.EffectiveQuality;_oldRenderQuality=QualitySettings.GetQualityLevel();
            services.EffectiveQuality=_quality;QualitySettings.SetQualityLevel(Mathf.Min(_quality,QualitySettings.names.Length-1),true);
            services.Localization.TrySetLanguage("uk");
            var bootstrap = UnityEngine.Object.FindAnyObjectByType<QuietCampBootstrap>();
            bootstrap.SendMessage("OnApplicationPause", false, SendMessageOptions.RequireReceiver);
            bootstrap.SendMessage("OnApplicationFocus", true, SendMessageOptions.RequireReceiver);
            var game = EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView"));
            game.Focus();
            PlayModeWindow.GetRenderingResolution(out _oldWidth, out _oldHeight);
            _oldCaptureRate = Time.captureFramerate; _screenChanged = true;
            PlayModeWindow.SetCustomRenderingResolution((uint)_width, (uint)_height, "QuietCamp composition review");
            if (_stillsOnly)
            {
                Time.captureFramerate = 30;
            }
            else
            {
            _movie = ScriptableObject.CreateInstance<MovieRecorderSettings>();
            _movie.name = "QuietCamp Gameplay"; _movie.Enabled = true;
            _movie.EncoderSettings = new CoreEncoderSettings
            {
                EncodingQuality = CoreEncoderSettings.VideoEncodingQuality.High,
                Codec = CoreEncoderSettings.OutputCodec.MP4
            };
            _movie.CaptureAudio = true;
            _movie.AudioInputSettings.PreserveAudio = true;
            _movie.ImageInputSettings = new GameViewInputSettings { OutputWidth = _width, OutputHeight = _height };
            _movie.OutputFile = Path.Combine(_output, "gameplay");
            _settings = ScriptableObject.CreateInstance<RecorderControllerSettings>();
            _settings.AddRecorderSettings(_movie); _settings.SetRecordModeToManual();
            _settings.FrameRate = 30; _settings.FrameRatePlayback = FrameRatePlayback.Constant;
            _settings.CapFrameRate = false; _settings.ExitPlayMode = false;
            _controller = new RecorderController(_settings); _controller.PrepareRecording();
            }
            // Recorder changes the Game View size. Let layout and input projection settle first.
            // Device Simulator can own the visible play view. Its rendering
            // does not resume Game View's WaitForEndOfFrame coroutine; still
            // inspection uses normal player frames and ScreenCapture instead.
            for (int i = 0; i < 20; i++) yield return _stillsOnly ? null : new WaitForEndOfFrame();
            if(_worldCriteria!=null)
            {
                EditorAuthoringPreview.CaptureClockRunning=true;
                for(int i=0;i<60;i++)yield return _stillsOnly?null:new WaitForEndOfFrame();
            }
            // The temporary replay must keep receiving its virtual pointer while
            // Windows focuses the user's IDE. Never alter the project's input asset.
            _inputScope=new EditorPointerInputScope();
            _hardware = InputSystem.devices.Where(d => (d is Mouse || d is Keyboard || d is Touchscreen) && d.enabled).ToArray();
            foreach (var device in _hardware) InputSystem.DisableDevice(device);
            _mouse = InputSystem.AddDevice<Mouse>("QuietCampPromoReplay"); _mouse.MakeCurrent();
            if (!_stillsOnly && !_controller.StartRecording()) throw new InvalidOperationException("Unity Recorder did not start.");
            if (Screen.width != _width || Screen.height != _height) throw new InvalidOperationException("Game View did not reach the required review resolution.");
            _cameraBefore=OpeningCampaignGameplayProbe.CameraProof(host.Session.Level,
                UnityEngine.Object.FindAnyObjectByType<CampArtView>()?.Definition);
            _firstFrame = Time.frameCount;
            var input = UnityEngine.Object.FindAnyObjectByType<PlacementController>();
            var witness = host.Session.Level.witness;
            var atmosphere=UnityEngine.Object.FindAnyObjectByType<CampArtView>()?.SeasonAtmosphere;
            if (witness == null || witness.Length != host.Session.Level.guests.Length) throw new InvalidOperationException("Missing complete witness.");
            int selected = -1;
            Vector2 aim = Vector2.zero;
            for (int frame = 0; frame < Frames; frame++)
            {
                if(_worldCriteria!=null&&frame==0)WorldFrame("empty");
                if(_worldCriteria!=null&&frame==110)WorldFrame("placed");
                if(_worldCriteria!=null&&frame==115)
                {
                    // Same placed tents, a later wind phase, before completion props.
                    for(int tick=0;tick<24;tick++)yield return _stillsOnly?null:new WaitForEndOfFrame();
                    WorldFrame("breeze");
                }
                _peakRainRunoff=Math.Max(_peakRainRunoff,atmosphere?.RainContacts?.RunoffCount??0);
                if(atmosphere!=null){_windMinimum=Mathf.Min(_windMinimum,atmosphere.WindStrength);_windMaximum=Mathf.Max(_windMaximum,atmosphere.WindStrength);}
                if(frame<6||(frame>104&&frame<116))
                {
                    AudioListener.GetOutputData(_audioSamples,0);float sum=0;foreach(var sample in _audioSamples)sum+=sample*sample;
                    _ambientPeakRms=Mathf.Max(_ambientPeakRms,Mathf.Sqrt(sum/_audioSamples.Length));
                }
                int step = (frame - 6) * witness.Length / 96;
                if (frame >= 6 && frame < 102 && step != selected)
                {
                    selected = step;
                    var p = witness[selected];
                    var world = BoardMath.CellCenterWorld(host.Session.Level, new QuietCamp.Domain.Cell(p.x, p.z));
                    aim = Camera.main.WorldToScreenPoint(world);
                    services.Actions.Execute(new UiActionRequest(new UiActionId("qc.select"), UiActionSource.Programmatic, "Gameplay", p.guestId, p.guestId));
                    if (!input.BeginCardDrag(p.guestId, 0, aim)) throw new InvalidOperationException("The ordinary placement input refused the replay drag.");
                    for (int turn = 0; turn < p.rotation; turn++) input.Rotate();
                    _actions.Add(new { frame, kind = "begin-card-drag", p.guestId, p.x, p.z, p.rotation });
                }
                if (selected >= 0 && frame < 104)
                {
                    int start = 6 + Mathf.CeilToInt(selected * 96f / witness.Length);
                    int end = 6 + Mathf.CeilToInt((selected + 1) * 96f / witness.Length) - 3;
                    bool held = frame < end;
                    float approach = Mathf.Clamp01((frame - start) / 7f);
                    var pointer = aim + Vector2.right * Mathf.Lerp(-14, 0, approach);
                    InputSystem.QueueStateEvent(_mouse, new MouseState { position = pointer, buttons = (ushort)(held ? 1 : 0) });
                    if (selected == witness.Length - 1 && frame == end - 2)
                    {
                        _gridInStill = input.HasPreview;
                        if (!_gridInStill) throw new InvalidOperationException("The gameplay still must show an active placement grid.");
                        ScreenCapture.CaptureScreenshot(Path.Combine(_output, "gameplay.png"));
                        if(_worldCriteria!=null)WorldOps.WorldOpsVisibility.Capture(Path.Combine(_output,"placement"),_worldCriteria);
                        _actions.Add(new { frame, kind = "gameplay-still-with-placement-grid" });
                    }
                }
                if (frame == 116)
                {
                    foreach (var expected in witness)
                    {
                        var placed = host.Session.State.Find(expected.guestId);
                        if (placed == null || placed.x != expected.x || placed.z != expected.z || placed.rotation != expected.rotation)
                            throw new InvalidOperationException("Pointer replay did not reach the intended placement: " + expected.guestId+
                                "; expected="+JsonConvert.SerializeObject(expected)+"; actual="+JsonConvert.SerializeObject(placed));
                    }
                    services.Actions.Execute(new UiActionRequest(new UiActionId("qc.check"), UiActionSource.Programmatic, "Gameplay"));
                    if (!host.Session.IsCompleted) throw new InvalidOperationException("The replay did not solve the level through Check.");
                    _actions.Add(new { frame, kind = "canonical-check", solved = true });
                }
                if (frame == 120 && (_stillsOnly || _worldCriteria!=null))
                {
                    if(_worldCriteria!=null)
                    {
                        // Inspect the actual completed state, after care props appear and
                        // entrance panels close. Four frames after Check was too early.
                        var tents=UnityEngine.Object.FindObjectsByType<TentStoryVisual>(FindObjectsSortMode.None);
                        float deadline=Time.realtimeSinceStartup+6;
                        while(tents.Any(t=>!t.Cared||t.Closure<.999f)&&Time.realtimeSinceStartup<deadline)yield return null;
                        if(tents.Any(t=>!t.Cared||t.Closure<.999f))throw new InvalidOperationException("Completed tent presentation did not settle.");
                        WorldFrame("complete");
                    }
                    ScreenCapture.CaptureScreenshot(Path.Combine(_output, "complete.png"));
                    _cameraAfter = OpeningCampaignGameplayProbe.CameraProof(host.Session.Level,
                        UnityEngine.Object.FindAnyObjectByType<CampArtView>()?.Definition);
                    _actions.Add(new { frame, kind = "completed-gameplay-still" });
                }
                if(frame==124&&_worldCriteria?.inspectMapCoverage==true)
                    yield return InspectMapCoverage();
                yield return _stillsOnly ? null : new WaitForEndOfFrame();
            }
        }

        void Finish(string error)
        {
            if (_finished) return; _finished = true;
            if(_worldCriteria!=null)EditorAuthoringPreview.CaptureClockRunning=false;
            try { _controller?.StopRecording(); }
            catch (Exception e) { error = (error ?? "") + "\n" + e; }
            // Recorder's end-of-frame hook can outlive StopRecording by a frame.
            // The movie is closed; prevent its queued SkipFrame from rendering stopped audio.
            if (_movie != null) _movie.AudioInputSettings.PreserveAudio = false;
            if (_mouse != null && _mouse.added) InputSystem.RemoveDevice(_mouse);
            _inputScope?.Dispose();_inputScope=null;
            if (_hardware != null) foreach (var device in _hardware) if (device.added) InputSystem.EnableDevice(device);
            var host = CampSceneHost.Current;
            var art = UnityEngine.Object.FindAnyObjectByType<CampArtView>();
            var receipt = new
            {
                status = error == null ? "captured" : "error", error,
                kind = _stillsOnly ? "native-game-view-real-placement-stills" : "native-game-view-recorded-game-audio-and-real-placement-input", playerBuild = false,
                id = host?.Session.Level.id, width = _width, height = _height, fps = 30, plannedFrames = Frames,
                elapsedGameFrames = Time.frameCount - _firstFrame, stillHasPlacementGrid = _gridInStill,
                completed = host?.Session.IsCompleted, memoryOnly = QuietCampBootstrap.ServicesRef?.Save.MemoryOnly,
                playingAtmosphereVoices = art?.SeasonAtmosphere?.PlayingVoices,
                rainContacts=art?.SeasonAtmosphere?.RainContactCount,groundRainContacts=art?.SeasonAtmosphere?.GroundRainContacts,
                fabricRainContacts=art?.SeasonAtmosphere?.FabricRainContacts,activeRainContacts=art?.SeasonAtmosphere?.RainContacts?.ActiveCount,
                rainRunoff=art?.SeasonAtmosphere?.RainContacts?.RunoffCount,
                peakRainRunoff=_peakRainRunoff,
                ambientPeakRms=_ambientPeakRms,windMinimum=_windMinimum,windMaximum=_windMaximum,
                sharedWindStrength=host?.Atmosphere?.Wind.Strength,reactiveVoices=host?.Atmosphere?.Soundscape?.OwnedVoiceCount,
                canvasRainPlaying=host?.Atmosphere?.Soundscape?.CanvasRainVoice.IsValid==true&&host.Atmosphere.Soundscape.CanvasRainVoice.Source.isPlaying,
                voiceAnchorDistance=art?.SeasonAtmosphere?.MaximumVoiceAnchorDistance,
                audioAnchor=art?.SeasonAtmosphere?.AudioAnchor?.name,
                foliageWaveLength=Shader.GetGlobalFloat("_AtmosWaveLen"),foliageWaveSpeed=Shader.GetGlobalFloat("_AtmosWaveSpeed"),foliageFlutter=Shader.GetGlobalFloat("_AtmosFlutterScale"),
                riverParticles = art?.SeasonAtmosphere?.RiverParticles, riverRipples = art?.SeasonAtmosphere?.RiverRipples,
                floatingLeaves = art?.SeasonAtmosphere?.FloatingLeaves, riverReflectionRendered=art?.RiverReflections?.HasRenderedReflection,
                riverEnvironmentReflected=art?.RiverReflections?.HasEnvironmentReflection,
                renderQuality=QualitySettings.GetQualityLevel(),effectiveQuality=QuietCampBootstrap.ServicesRef?.EffectiveQuality,
                surfaceSteps = host?.SurfaceDetails?.Surface?.Steps, actions = _actions,
                audioSource = _stillsOnly ? "Still inspection; no audio recording claimed." : "Unity Recorder captures the running Unity audio mix. No external soundtrack.",
                movie = _stillsOnly ? null : "gameplay.mp4", image = "gameplay.png", cameraBefore = _cameraBefore, cameraAfter = _cameraAfter
            };
            File.WriteAllText(Path.Combine(_output, "capture.json"), JsonConvert.SerializeObject(receipt, Formatting.Indented));
            if(QuietCampBootstrap.ServicesRef!=null)QuietCampBootstrap.ServicesRef.EffectiveQuality=_oldEffectiveQuality;
            QualitySettings.SetQualityLevel(_oldRenderQuality,true);
            if(_adaptiveQuality!=null)_adaptiveQuality.enabled=_adaptiveWasEnabled;
            if (_screenChanged)
            {
                Time.captureFramerate = _oldCaptureRate;
                PlayModeWindow.SetCustomRenderingResolution(_oldWidth, _oldHeight, "QuietCamp restored Game View");
            }
            if (_movie != null) UnityEngine.Object.Destroy(_movie); if (_settings != null) UnityEngine.Object.Destroy(_settings);
            EditorApplication.playModeStateChanged -= PlayModeChanged;
            AssemblyReloadEvents.beforeAssemblyReload -= Interrupted;
            _active = null;
        }

        void WorldFrame(string state)
        {
            ScreenCapture.CaptureScreenshot(Path.Combine(_output,state+".png"));
            WorldOps.WorldOpsVisibility.Capture(Path.Combine(_output,state),_worldCriteria);
        }

        IEnumerator InspectMapCoverage()
        {
            var cover=UnityEngine.Object.FindAnyObjectByType<CampGroundDetails>();var camera=Camera.main;
            if(cover==null||camera==null)throw new InvalidOperationException("Map cover inspection needs the active world.");
            var position=camera.transform.position;var rotation=camera.transform.rotation;float size=camera.orthographicSize;
            var motion=camera.GetComponent<CameraAtmosphereMotion>();bool moving=motion!=null&&motion.enabled;
            if(moving)motion.enabled=false;
            try
            {
                var bounds=cover.VegetationBounds;
                if(bounds.width<=0)throw new InvalidOperationException("No seasonal ground cover to inspect.");
                int index=0;
                foreach(var offset in new[]{new Vector2(-.28f,-.28f),new Vector2(-.28f,.28f),new Vector2(.28f,-.28f),new Vector2(.28f,.28f)})
                {
                    var point=bounds.center+Vector2.Scale(bounds.size,offset);
                    var art=UnityEngine.Object.FindAnyObjectByType<CampArtView>()?.Definition;
                    var target=new Vector3(point.x,art?.GroundHeight(point.x,point.y)??0,point.y);
                    camera.transform.SetPositionAndRotation(target-rotation*Vector3.forward*25,rotation);camera.orthographicSize=6;
                    yield return null;yield return null;
                    // Deliberately displaced camera: diagnostic evidence, not a player view.
                    string name="coverage-"+index++;
                    ScreenCapture.CaptureScreenshot(Path.Combine(_output,name+".png"));
                    File.WriteAllText(Path.Combine(_output,name+".json"),JsonConvert.SerializeObject(new{kind="environment-coverage-diagnostic",target=new[]{target.x,target.y,target.z},bounds=new[]{bounds.xMin,bounds.yMin,bounds.xMax,bounds.yMax},patches=cover.VegetationPatchCount},Formatting.Indented));
                    yield return null;yield return null;
                }
            }
            finally
            {camera.transform.SetPositionAndRotation(position,rotation);camera.orthographicSize=size;if(moving)motion.enabled=true;}
        }

        void Interrupted() { if (!_finished) Finish("Capture interrupted before completion."); }
        void PlayModeChanged(PlayModeStateChange state) { if (state == PlayModeStateChange.ExitingPlayMode) Interrupted(); }
    }
}
