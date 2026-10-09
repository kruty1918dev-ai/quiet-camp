using System;
using System.Collections;
using System.Collections.Generic;
using Kruty1918.Audio;
using Kruty1918.InputRouting.API;
using Kruty1918.InputRouting.Runtime;
using Kruty1918.Localization;
using Kruty1918.Notifications.API;
using Kruty1918.Notifications.Runtime;
using Kruty1918.UIActions.API;
using Kruty1918.UIActions.Runtime;
using Kruty1918.UiFoundation;
using QuietCamp.Application;
using QuietCamp.Infrastructure;
using QuietCamp.Presentation.UI;
using QuietCamp.Presentation.World;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
namespace QuietCamp.Presentation
{
    /// <summary>
    /// Persistent application root created once by the Boot scene. Composes the
    /// whole service graph (save, localization, audio, input policy, contexts,
    /// action router, hotkeys, escape, motion, transitions, notifications,
    /// progression), owns the per-frame tick loop and spawns scene hosts.
    /// </summary>
    public sealed class QuietCampBootstrap : MonoBehaviour
    {
        static QuietCampBootstrap _instance;
        public static GameServices ServicesRef => _instance?._services;
#if UNITY_EDITOR
        // Tests opt into isolated RAM-only saves before Boot. Player startup always uses the normal adapter.
        public static Func<SaveAdapter> EditorSaveFactory;
        public static bool EditorDisableAudio;
#endif
        static SaveAdapter CreateStartupSave()
        {
#if UNITY_EDITOR
            if(EditorSaveFactory!=null)return EditorSaveFactory();
#endif
            return new SaveAdapter();
        }

        [SerializeField] string _firstSceneName = "MainMenu";

        GameServices _services;
        ScreenRouter _router;
        UiHotkeyService _hotkeys;
        UiMotionService _motion;
        AudioService _audio;
        bool _paused, _focused = true;
        SaveAdapter _startupSave;
        ILocalizationService _startupLocale;
        BootPrivacyPanel _privacyPanel;
        bool _exitRequested;

        void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);
            QualitySettings.vSyncCount = 0;
            UnityEngine.Application.targetFrameRate = 60;
            _bootView = BootCampView.Create(transform, RetryBoot);
        }

        void Compose()
        {
            using var audit = PerformanceAudit.Measure("QC.QuietCampBootstrap.Compose");
            // Persistence first — everything else reads restored settings.
            _bootView.Stage("boot.save", .08f);
            var save = _startupSave;
            ScreenOrientationPolicy.Apply(save.Settings.orientation);

            var localization = _startupLocale;
            LocalizedLabel.Localization = localization;
            LocalizedLabel.TextScale = save.Settings.textScale;

            _bootView.Localize(localization.T);
            _bootView.Stage("boot.sound", .25f);

#if UNITY_EDITOR
            var audioCatalog = EditorDisableAudio ? null : QuietCampAudioCatalog.Load();
#else
            var audioCatalog = QuietCampAudioCatalog.Load();
#endif
            if (audioCatalog == null
#if UNITY_EDITOR
                && !EditorDisableAudio
#endif
                )
                Debug.LogWarning("[QuietCamp] AudioCatalog missing — run Tools/Quiet Camp/Setup Project.");
            _audio = audioCatalog != null
                ? new AudioService(audioCatalog, audioCatalog) : null;
            _audio?.Initialize();
            ApplyAudioSettings(save.Settings);

            _bootView.Stage("boot.forest", .45f);
            var assets = AssetCatalog.Load();
            if (assets == null)
                Debug.LogWarning("[QuietCamp] AssetCatalog missing — run Tools/Quiet Camp/Setup Project.");

            _bootView.Stage("boot.controls", .65f);
            var inputPolicy = new GameplayInputPolicy();
            var contexts = new UiContextStack();
            var journal = new UiActionJournal();
            var dispatch = new QcActionHandler();
            RegisterAllActionIds(dispatch);
            var router = new UiActionRouter(
                new List<IUiActionHandler> { dispatch }, journal);
            _hotkeys = new UiHotkeyService(router, contexts, inputPolicy, DefaultHotkeys());
            var escape = new UiEscapeRouter(contexts, router, journal);
            _motion = new UiMotionService(new SettingsMotionSource(save.Settings));
            _motion.ReducedMotion = save.Settings.reducedMotion;
            var transitions = new SceneTransitionService();

            var toastLayer = BuildToastLayer();
            var presenter = new ToastPresenter(toastLayer, localization.T, () => _services);
            var notifications = new GameplayNotificationService(
                new GameplayNotificationSettings(), presenter);

            var progression = new ProgressionService();
            progression.Restore(save.Progress.completedIds,
                save.Progress.lastLevelId, save.Progress.cosmeticFlags);

            _services = new GameServices(
                save, localization, audioCatalog, _audio, assets,
                inputPolicy, contexts, router, dispatch, _hotkeys, journal,
                escape, _motion, notifications, transitions, progression);
            gameObject.AddComponent<AdaptiveCampQuality>().Configure(_services);
            _services.Haptics.Suspended = _paused || !_focused;
            _router = new ScreenRouter(_services);
            EscapeRouterRef = escape;

            save.SaveFailed += _ =>
                notifications.Show(localization.T("save.failed"),
                    GameplayNotificationKind.Error);

            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        void ApplyAudioSettings(SettingsSaveData s)
        {
            if (_audio == null) return;
            _audio.SetBusVolume(AudioBus.Master, s.master);
            _audio.SetBusVolume(AudioBus.Music, s.music);
            _audio.SetBusVolume(AudioBus.Ambience, s.ambience);
            _audio.SetBusVolume(AudioBus.Ui, s.effects);
            _audio.SetBusVolume(AudioBus.Sfx, s.effects);
        }

        BootCampView _bootView;
        Coroutine _startup;
        public bool StartupReady { get; private set; }

        void RetryBoot()
        {
            if (_startup != null) return;
            _bootView.Retry();
            BeginStartup();
        }
        void BeginStartup()
        {
            var running = StartCoroutine(InitializeCamp());
            // A coroutine can fail synchronously before its first yield.
            _startup = _bootView != null && !_bootView.Failed && !StartupReady ? running : null;
        }

        IEnumerator Start()
        {
            // Paint the game's own native boot view before resource work.
            yield return null;
            BeginStartup();
        }

        IEnumerator InitializeCamp()
        {
            Exception failure = null;
            // Prepare only local persistence and localization before presenting the document.
            // Optional SDK factories and scene hosts are composed after acknowledgement.
            if (_startupSave == null)
            {
                try
                {
                    _startupSave = _services?.Save ?? CreateStartupSave();
                    if (_services == null) _startupSave.Load(out _);
                    _startupLocale = _services?.Localization ?? QuietCampLocalization.Create(_startupSave.MemoryOnly?System.IO.Path.Combine(System.IO.Path.GetTempPath(),"quietcamp-roadmap-qa-locale.txt"):null);
                    if (_startupSave.HasSave && !string.IsNullOrEmpty(_startupSave.Settings.language))
                        _startupLocale.TrySetLanguage(_startupSave.Settings.language);
                    ScreenOrientationPolicy.Apply(_startupSave.Settings.orientation);
                    _bootView.Localize(_startupLocale.T);
                }
                catch (Exception error) { failure = error; }
            }
            if (failure == null)
            {
                try { _privacyPanel = BootPrivacyPanel.Create(_bootView.PolicyParent, _startupSave, _startupLocale); }
                catch (Exception error) { failure = error; }
            }
            if (failure != null)
            {
                if (BootPrivacyPanel.Current != null) Destroy(BootPrivacyPanel.Current.gameObject);
                Debug.LogException(failure); _bootView.ShowPolicy(false); _bootView.Fail(); _startup = null; yield break;
            }
            _bootView.ShowPolicy(true);
            while (!_privacyPanel.Accepted && !_privacyPanel.Declined && !_exitRequested)
            {
                if (_privacyPanel.MountError != null)
                {
                    Debug.LogError("[QuietCamp] Boot policy UI failed: " + _privacyPanel.MountError);
                    Destroy(_privacyPanel.gameObject); _privacyPanel = null;
                    _bootView.ShowPolicy(false); _bootView.Fail(); _startup = null; yield break;
                }
                yield return null;
            }
            if (_privacyPanel.Declined || _exitRequested)
            {
                _exitRequested = true; _startup = null;
                // Editor stays on Boot; the real player exits. Refusal never writes a receipt.
                UnityEngine.Application.Quit(); yield break;
            }
            Destroy(_privacyPanel.gameObject); _privacyPanel = null; _bootView.ShowPolicy(false);
            if (_services == null)
            {
                try { Compose(); }
                catch (Exception error) { failure = error; }
                if (failure != null)
                {
                    _audio?.Dispose(); _audio = null;
                    foreach (Transform child in transform)
                        if (child != _bootView.transform) Destroy(child.gameObject);
                    Debug.LogException(failure);
                    _bootView.Fail(); _startup = null; yield break;
                }
            }
            _ = _services.Analytics.RestoreConsent(); // Optional SDK/network cannot hold startup.
            UnityEngine.Application.targetFrameRate = 60;
            if (!_services.AmbientWindHandle.IsValid)
                _services.AmbientWindHandle = _audio?.Play("ambience.wind", new AudioPlayOptions(initialPlaybackScale: 0)) ?? default;
            if (!_services.MusicBedHandle.IsValid)
                _services.MusicBedHandle = _audio?.Play("music.clearing", new AudioPlayOptions(initialPlaybackScale: 0)) ?? default;
            _bootView.Stage("boot.menu", .75f);
            var foliage = FoliageDiveTransition.Ensure(_services);
            System.Threading.Tasks.Task covering = null;
            try { covering = foliage.CoverAsync(); }
            catch (Exception error) { failure = error; }
            if (covering != null) while (!covering.IsCompleted) yield return null;
            if (covering?.IsFaulted == true) failure = covering.Exception;
            if (failure != null)
            { Debug.LogException(failure); foliage.Recover(); _bootView.Fail(); _startup = null; yield break; }
            // Hide native Boot only after the foliage has painted a fully covered frame.
            _bootView.Fade(0);
            var introduction = _services.Tutorial.NeedsIntroduction;
            var firstScene = introduction ? "Camp" : _firstSceneName;
            if (introduction) _services.PendingLevelId = _services.Tutorial.CurrentLevelId;
            if (SceneManager.GetActiveScene().name != "Camp" && (SceneManager.GetActiveScene().name != firstScene || MenuSceneHost.Current == null || !MenuSceneHost.Current.UiReady))
            {
                AsyncOperation load = null;
                try { load = SceneManager.LoadSceneAsync(firstScene); }
                catch (Exception error) { failure = error; }
                if (failure != null || load == null)
                {
                    if (failure != null) Debug.LogException(failure);
                    foliage.Recover(); _bootView.Fade(1); _bootView.Fail(); _startup = null; yield break;
                }
                while (!load.isDone)
                {
                    _bootView.Stage("boot.menu", .75f + .15f * Mathf.Clamp01(load.progress / .9f));
                    yield return null;
                }
            }
            _bootView.Stage("boot.ready", .94f);
            float deadline = Time.realtimeSinceStartup + 15;
            while (!StartupUiReady() && Time.realtimeSinceStartup < deadline)
                yield return null;
            if (!StartupUiReady())
            { foliage.Recover(); _bootView.Fade(1); _bootView.Fail(); _startup = null; yield break; }
            _bootView.Stage("boot.ready", 1);
            foliage.BeginReveal();
            var revealing = foliage.RevealAsync();
            float elapsed = 0, duration = _services.ReducedMotion ? foliage.Config.ReducedOut : foliage.Config.RevealDuration;
            while (!revealing.IsCompleted)
            {
                elapsed += Time.unscaledDeltaTime;
                float fraction = Mathf.Clamp01(elapsed / duration);
                _audio?.SetPlaybackScale(_services.AmbientWindHandle, fraction);
                _audio?.SetPlaybackScale(_services.MusicBedHandle, fraction);
                yield return null;
            }
            if (revealing.IsFaulted)
            { Debug.LogException(revealing.Exception); foliage.Recover(); _bootView.Fade(1); _bootView.Fail(); _startup = null; yield break; }
            _audio?.SetPlaybackScale(_services.AmbientWindHandle, 1);
            _audio?.SetPlaybackScale(_services.MusicBedHandle, 1);
            Destroy(_bootView.gameObject); _bootView = null;
            StartupReady = true; _startup = null;
        }

        static bool StartupUiReady() => SceneManager.GetActiveScene().name == "Camp"
            ? CampSceneHost.Current != null && CampSceneHost.Current.UiReady
            : MenuSceneHost.Current != null && MenuSceneHost.Current.UiReady;

        // ─── Persistent toast overlay ────────────────────────────────────────

        RectTransform BuildToastLayer()
        {
            var canvasGo = new GameObject("OverlayCanvas", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 80;
            var scaler = canvasGo.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            var layer = QcUi.Stretch(canvasGo.transform as RectTransform, "ToastLayer");
            return layer;
        }

        // ─── Action ids & hotkeys ────────────────────────────────────────────

        static void RegisterAllActionIds(QcActionHandler dispatch)
        {
            // All ids must exist in ActionIds at router-construction time;
            // scene hosts later attach the actual implementations.
            foreach (var id in new[]
            {
                "qc.back", "qc.pause", "qc.resume", "qc.rotate", "qc.undo",
                "qc.redo", "qc.remove", "qc.check", "qc.hint", "qc.settings",
                "qc.levels", "qc.next", "qc.album", "qc.select", "qc.play",
                "qc.continue", "qc.autoplace", "qc.transition.back",
                "qc.menu", "qc.economy", "qc.tutorial.restart",
            })
                dispatch.Register(new UiActionId(id), _ =>
                    UiActionResult.Rejected(UiActionReason.ActionUnavailable));
        }

        static List<UiHotkeyBinding> DefaultHotkeys()
        {
            var gameplay = new[] { "Gameplay" };
            return new List<UiHotkeyBinding>
            {
                new UiHotkeyBinding(new UiActionId("qc.rotate"), Key.R, allowedContexts: gameplay),
                new UiHotkeyBinding(new UiActionId("qc.undo"), Key.Z, ctrl: true, allowedContexts: gameplay),
                new UiHotkeyBinding(new UiActionId("qc.redo"), Key.Y, ctrl: true, allowedContexts: gameplay),
                new UiHotkeyBinding(new UiActionId("qc.check"), Key.Enter, Key.NumpadEnter, allowedContexts: gameplay),
                new UiHotkeyBinding(new UiActionId("qc.hint"), Key.H, allowedContexts: gameplay),
                new UiHotkeyBinding(new UiActionId("qc.remove"), Key.Delete, Key.Backspace, allowedContexts: gameplay),
            };
        }

        UiEscapeRouter EscapeRouterRef;

        // ─── Frame loop ──────────────────────────────────────────────────────

        void Update()
        {
            if (_exitRequested) return;
            _hotkeys?.Tick();
            _motion?.Tick();
            _audio?.Tick();
            if (_focused && !_paused)
                _services?.Analytics.Tick(Time.unscaledDeltaTime, CampSceneHost.Current?.GameplayActive ?? false, _services.EffectiveQuality);
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                // Back on a required disclosure is a refusal, never implicit acknowledgement.
                if (_privacyPanel != null && !_privacyPanel.Accepted)
                { _exitRequested = true; UnityEngine.Application.Quit(); }
                else EscapeRouterRef?.TryHandleEscape();
            }
        }

        // ─── Scene hosting ───────────────────────────────────────────────────

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == "Boot" && _services != null && _bootView == null && _startup == null)
            {
                StartupReady = false;
                _bootView = BootCampView.Create(transform, RetryBoot);
                _bootView.Localize(_services.Localization.T);
                RetryBoot();
                return;
            }
            if (_services == null || (scene.name != "Camp" && scene.name != "MainMenu")) return;
            var go = new GameObject(scene.name + "Host");
            SceneManager.MoveGameObjectToScene(go, scene);
            if (scene.name == "Camp")
                go.AddComponent<CampSceneHost>().Configure(_services, _router);
            else if (scene.name == "MainMenu")
                go.AddComponent<MenuSceneHost>().Configure(_services, _router);
        }

        void OnApplicationPause(bool paused)
        {
            _paused = paused;
            if (!paused) RestoreScreenOrientation();
            if (_services != null) _services.Haptics.Suspended = _paused || !_focused;
            if (paused)
            {
                _services?.Analytics.EndLevel(ComfortOutcome.Backgrounded, keepAttempt: true);
                _services?.Save.Save();
            }
            AudioListener.pause = _paused || !_focused;
        }

        void OnApplicationFocus(bool focused)
        {
            _focused = focused;
            if (focused) RestoreScreenOrientation();
            AudioListener.pause = _paused || !_focused;
            if (_services != null) _services.Haptics.Suspended = _paused || !_focused;
        }

        void RestoreScreenOrientation()
        {
            // Returning from an OS dialog or external activity must keep the player's
            // fixed display choice, even when that activity enabled autorotation.
            if (_services != null) ScreenOrientationPolicy.Apply(_services.Save.Settings.orientation);
        }

        void OnApplicationQuit() => _services?.Save.Save();

        void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            _services?.Dispose();
            if (_instance == this) { AudioListener.pause = false; _instance = null; }
        }

        /// <summary>Settings-backed reduced-motion source for UiMotionService.</summary>
        sealed class SettingsMotionSource : IUiReducedMotionSource
        {
            readonly SettingsSaveData _settings;
            public SettingsMotionSource(SettingsSaveData settings) => _settings = settings;
            public bool ReduceMotion => _settings.reducedMotion;
            public void SetReduceMotion(bool value) => _settings.reducedMotion = value;
        }
    }
}
