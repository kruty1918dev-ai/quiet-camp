using System.Collections.Generic;
using Kruty1918.Audio;
using Kruty1918.InputRouting.API;
using Kruty1918.InputRouting.Runtime;
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

        [SerializeField] string _firstSceneName = "MainMenu";

        GameServices _services;
        ScreenRouter _router;
        UiHotkeyService _hotkeys;
        UiMotionService _motion;
        AudioService _audio;

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
            UnityEngine.Application.targetFrameRate = QualitySettings.GetQualityLevel() >= 2 ? 60 : 30;
            Compose();
        }

        void Compose()
        {
            // Persistence first — everything else reads restored settings.
            var save = new SaveAdapter();
            save.Load(out _);

            var localization = QuietCampLocalization.Create();
            if (!string.IsNullOrEmpty(save.Settings.language))
                localization.TrySetLanguage(save.Settings.language);
            LocalizedLabel.Localization = localization;
            LocalizedLabel.TextScale = save.Settings.textScale;

            var audioCatalog = QuietCampAudioCatalog.Load();
            if (audioCatalog == null)
                Debug.LogWarning("[QuietCamp] AudioCatalog missing — run Tools/Quiet Camp/Setup Project.");
            _audio = audioCatalog != null
                ? new AudioService(audioCatalog, audioCatalog) : null;
            _audio?.Initialize();
            ApplyAudioSettings(save.Settings);

            var assets = AssetCatalog.Load();
            if (assets == null)
                Debug.LogWarning("[QuietCamp] AssetCatalog missing — run Tools/Quiet Camp/Setup Project.");

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
            var presenter = new ToastPresenter(toastLayer, localization.T);
            var notifications = new GameplayNotificationService(
                new GameplayNotificationSettings(), presenter);

            var progression = new ProgressionService();
            progression.Restore(save.Progress.completedIds,
                save.Progress.lastLevelId, save.Progress.cosmeticFlags);

            _services = new GameServices(
                save, localization, audioCatalog, _audio, assets,
                inputPolicy, contexts, router, dispatch, _hotkeys, journal,
                escape, _motion, notifications, transitions, progression);
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
                "qc.continue",
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
            _hotkeys?.Tick();
            _motion?.Tick();
            _audio?.Tick();
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
                EscapeRouterRef?.TryHandleEscape();
        }

        // ─── Scene hosting ───────────────────────────────────────────────────

        void Start()
        {
            // Smooth 60 fps target where the device allows it; calm/balanced
            // pacing stays controlled by Settings.calmMode.
            UnityEngine.Application.targetFrameRate = 60;
            if (SceneManager.GetActiveScene().name != _firstSceneName)
                SceneManager.LoadScene(_firstSceneName);
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            var go = new GameObject(scene.name + "Host");
            SceneManager.MoveGameObjectToScene(go, scene);
            if (scene.name == "Camp")
                go.AddComponent<CampSceneHost>().Configure(_services, _router);
            else if (scene.name == "MainMenu")
                go.AddComponent<MenuSceneHost>().Configure(_services, _router);
        }

        void OnApplicationPause(bool paused)
        {
            if (paused) _services?.Save.Save();
        }

        void OnApplicationQuit() => _services?.Save.Save();

        void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            _services?.Dispose();
            if (_instance == this) _instance = null;
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
