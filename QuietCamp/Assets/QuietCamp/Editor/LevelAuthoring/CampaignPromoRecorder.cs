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
        InputSettings _previousInputSettings;
        InputSettings.BackgroundBehavior _previousBackground;
        InputSettings.EditorInputBehaviorInPlayMode _previousEditorInput;
        string _output;
        readonly List<object> _actions = new();
        bool _finished;
        int _firstFrame;
        bool _gridInStill;
        int _peakRainRunoff;
        bool _stillsOnly, _screenChanged;
        uint _oldWidth, _oldHeight;
        int _oldCaptureRate;
        object _cameraBefore, _cameraAfter;
        AdaptiveCampQuality _adaptiveQuality;
        bool _adaptiveWasEnabled;
        int _oldEffectiveQuality,_oldRenderQuality;

        public static object Begin(string output, bool stillsOnly = false)
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
            services.EffectiveQuality=1;QualitySettings.SetQualityLevel(1,true);
            services.Localization.TrySetLanguage("uk");
            var bootstrap = UnityEngine.Object.FindAnyObjectByType<QuietCampBootstrap>();
            bootstrap.SendMessage("OnApplicationPause", false, SendMessageOptions.RequireReceiver);
            bootstrap.SendMessage("OnApplicationFocus", true, SendMessageOptions.RequireReceiver);
            var game = EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView"));
            game.Focus();
            if (_stillsOnly)
            {
                PlayModeWindow.GetRenderingResolution(out _oldWidth, out _oldHeight);
                _oldCaptureRate = Time.captureFramerate; _screenChanged = true;
                PlayModeWindow.SetCustomRenderingResolution(1440, 1080, "QuietCamp composition review");
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
            _movie.ImageInputSettings = new GameViewInputSettings { OutputWidth = 1440, OutputHeight = 1080 };
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
            // The temporary replay must keep receiving its virtual pointer while
            // Windows focuses the user's IDE. Never alter the project's input asset.
            _previousInputSettings=InputSystem.settings;
            _previousBackground=_previousInputSettings.backgroundBehavior;
            _previousEditorInput=_previousInputSettings.editorInputBehaviorInPlayMode;
            _previousInputSettings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            _previousInputSettings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            _hardware = InputSystem.devices.Where(d => (d is Mouse || d is Keyboard || d is Touchscreen) && d.enabled).ToArray();
            foreach (var device in _hardware) InputSystem.DisableDevice(device);
            _mouse = InputSystem.AddDevice<Mouse>("QuietCampPromoReplay"); _mouse.MakeCurrent();
            if (!_stillsOnly && !_controller.StartRecording()) throw new InvalidOperationException("Unity Recorder did not start.");
            if (Screen.width != 1440 || Screen.height != 1080) throw new InvalidOperationException("Game View did not reach the required review resolution.");
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
                _peakRainRunoff=Math.Max(_peakRainRunoff,atmosphere?.RainContacts?.RunoffCount??0);
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
                if (_stillsOnly && frame == 120)
                {
                    ScreenCapture.CaptureScreenshot(Path.Combine(_output, "complete.png"));
                    _cameraAfter = OpeningCampaignGameplayProbe.CameraProof(host.Session.Level,
                        UnityEngine.Object.FindAnyObjectByType<CampArtView>()?.Definition);
                    _actions.Add(new { frame, kind = "completed-gameplay-still" });
                }
                yield return _stillsOnly ? null : new WaitForEndOfFrame();
            }
        }

        void Finish(string error)
        {
            if (_finished) return; _finished = true;
            try { _controller?.StopRecording(); }
            catch (Exception e) { error = (error ?? "") + "\n" + e; }
            // Recorder's end-of-frame hook can outlive StopRecording by a frame.
            // The movie is closed; prevent its queued SkipFrame from rendering stopped audio.
            if (_movie != null) _movie.AudioInputSettings.PreserveAudio = false;
            if (_mouse != null && _mouse.added) InputSystem.RemoveDevice(_mouse);
            if(_previousInputSettings!=null)
            {
                _previousInputSettings.backgroundBehavior=_previousBackground;
                _previousInputSettings.editorInputBehaviorInPlayMode=_previousEditorInput;
            }
            if (_hardware != null) foreach (var device in _hardware) if (device.added) InputSystem.EnableDevice(device);
            var host = CampSceneHost.Current;
            var art = UnityEngine.Object.FindAnyObjectByType<CampArtView>();
            var receipt = new
            {
                status = error == null ? "captured" : "error", error,
                kind = _stillsOnly ? "native-game-view-real-placement-stills" : "native-game-view-recorded-game-audio-and-real-placement-input", playerBuild = false,
                id = host?.Session.Level.id, width = 1440, height = 1080, fps = 30, plannedFrames = Frames,
                elapsedGameFrames = Time.frameCount - _firstFrame, stillHasPlacementGrid = _gridInStill,
                completed = host?.Session.IsCompleted, memoryOnly = QuietCampBootstrap.ServicesRef?.Save.MemoryOnly,
                playingAtmosphereVoices = art?.SeasonAtmosphere?.PlayingVoices,
                rainContacts=art?.SeasonAtmosphere?.RainContactCount,groundRainContacts=art?.SeasonAtmosphere?.GroundRainContacts,
                fabricRainContacts=art?.SeasonAtmosphere?.FabricRainContacts,activeRainContacts=art?.SeasonAtmosphere?.RainContacts?.ActiveCount,
                rainRunoff=art?.SeasonAtmosphere?.RainContacts?.RunoffCount,
                peakRainRunoff=_peakRainRunoff,
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

        void Interrupted() { if (!_finished) Finish("Capture interrupted before completion."); }
        void PlayModeChanged(PlayModeStateChange state) { if (state == PlayModeStateChange.ExitingPlayMode) Interrupted(); }
    }
}
