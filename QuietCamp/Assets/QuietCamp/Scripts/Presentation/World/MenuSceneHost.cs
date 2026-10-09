using System;
using System.Collections.Generic;
using Kruty1918.Atmos;
using Kruty1918.Audio;
using Kruty1918.UIActions.API;
using QuietCamp.Domain;
using QuietCamp.Infrastructure;
using QuietCamp.Presentation.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace QuietCamp.Presentation.World
{
    /// <summary>
    /// Main-menu scene host: the menu is a living campsite, not a poster.
    /// Builds a different real level's clearing on each application launch,
    /// using its gameplay terrain, objects, lighting, wind and particles.
    /// MenuCameraDrift adds quiet motion behind the HTML menu on CanvasRoot.
    /// </summary>
    public sealed class MenuSceneHost : MonoBehaviour
    {
        GameServices _services;
        ScreenRouter _router;
        MenuScreens _screens;
        CampAtmosphere _atmosphere;
        AtmosphereCatalog.Profile _profile;
        AtmosphereCatalog _catalog;
        Light _sun;
        Camera _camera;
        Transform _listenerProxy;
        Transform _world;
        RectTransform _menuViewport, _safeArea;
        Vector2 _lastMenuSize;
        Vector3 _firePos;
        readonly List<System.IDisposable> _leases = new List<System.IDisposable>();
        System.IDisposable _menuContext;
        AlbumDiorama _album;
        BoardRenderer _board;
        public LevelData BackgroundLevel => _services?.MenuBackdrop;

        public static MenuSceneHost Current { get; private set; }

        /// <summary>Set at the end of Start — the readiness signal the
        /// ScreenRouter waits for before revealing the menu.</summary>
        public bool IsReady { get; private set; }
        public bool UiReady => IsReady && (_atmosphere?.InitialWorldReady ?? true) && (_screens?.UiReady ?? false);
        public float VisibleDimming => _screens != null && (_screens.Current=="Main"||_screens.Current=="Settings") ? .55f : 0;

        public void Configure(GameServices services, ScreenRouter router)
        {
            _services = services;
            _router = router;
        }

        void Start() { Current = this; StartCoroutine(ComposeScene()); }
        System.Collections.IEnumerator ComposeScene()
        {
            Current = this;
            var canvasRoot = ResolveScene("CanvasRoot");
            var safeArea = (RectTransform)ResolveScene("CanvasRoot/SafeArea");
            if (canvasRoot == null || safeArea == null)
            {
                Debug.LogError("[QuietCamp] MainMenu scene lacks CanvasRoot/SafeArea.");
                yield break;
            }
            if (safeArea.GetComponent<SafeAreaFitter>() == null)
                safeArea.gameObject.AddComponent<SafeAreaFitter>();

            var camera = FindCamera();
            if (camera == null)
            {
                Debug.LogError("[QuietCamp] MainMenu scene lacks a camera.");
                yield break;
            }
            _camera=camera;
            // Navigation is available even if optional scenery fails in a player.
            using (PerformanceAudit.Measure("QC.MenuSceneHost.Start"))
                _screens = new MenuScreens(_services, safeArea);
            RegisterActions();
            _menuContext = _services.ContextStack.Push(new UiContextRegistration(
                "Menu", UiContextLayer.Global, 0, () => true,
                new UiActionId("qc.back")));
            yield return null;
            while (!_screens.PreparationReady) yield return null;
            TryBuildWorld(() => BuildWorld(camera, safeArea));
            yield return null;
            if (_world != null) TryBuildWorld(() => ConfigureWorldAtmosphere(camera));
            yield return null;
            _router?.Dive.WarmSceneUnderCover();
            var album=gameObject.AddComponent<AlbumDiorama>();
            _album=album;
            album.Configure(_services,_screens,camera,_world,_atmosphere,GetComponent<MenuCameraDrift>());
            _screens.SetDarkSky(_profile != null &&
                (_profile.Id == "evening" || _profile.Id == "night"));
            // A completed final level lands here through the leaf transition.
            if (!string.IsNullOrEmpty(_services.PendingMenuScreen))
            {
                _screens.Show(_services.PendingMenuScreen);
                _services.PendingMenuScreen = null;
            }
            IsReady = true;
        }

        // ─── Living diorama ──────────────────────────────────────────────────

        void BuildWorld(Camera camera, RectTransform safeArea)
        {
            using var audit = PerformanceAudit.Measure("QC.MenuSceneHost.BuildWorld");
            var level = _services.MenuBackdrop;
            if (level == null)
            {
                var id = MenuDiorama.SelectId(CampContent.Summaries, _services.Settings.lastMenuBackdropId, new System.Random().Next());
                level = LevelLoader.Load(id);
                _services.MenuBackdrop = level;
                _services.Settings.lastMenuBackdropId = id;
                _services.Save.Save();
            }

            var world = new GameObject("World").transform;
            _world=world;
            SceneManager.MoveGameObjectToScene(world.gameObject, gameObject.scene);
            _board = MenuDiorama.Build(world, _services.Assets, level);

            // Leave the wordmark and navigation their own edge bands. The
            // campfire belongs in the open centre, never under the CTA.
            var viewportGo = new GameObject("MenuViewport", typeof(RectTransform));
            var viewport = (RectTransform)viewportGo.transform;
            viewport.SetParent(safeArea, false);
            _menuViewport = viewport; _safeArea = safeArea;
            viewport.anchorMin = new Vector2(0f, 0.12f);
            viewport.anchorMax = new Vector2(1f, 0.72f);
            viewport.offsetMin = viewport.offsetMax = Vector2.zero;

            CameraFitter.Configure(camera, new Vector3(52f, 225f, 0f));
            var cameraListener = camera.GetComponent<AudioListener>();
            if (cameraListener != null) { cameraListener.enabled = false; Destroy(cameraListener); }
            // The listener belongs to navigation, so hiding the menu world for
            // an album diorama cannot turn off all audio.
            var listener = new GameObject("MenuListenerProxy"); listener.transform.SetParent(transform, false);
            _listenerProxy = listener.transform;
            listener.transform.SetPositionAndRotation(new Vector3(0, 2, 0), camera.transform.rotation);
            listener.AddComponent<AudioListener>();

            _catalog = AtmosphereCatalog.Load();
            _profile = _catalog.Resolve(level.id, level.lighting);
            _sun = MenuDiorama.CreateSun(world);
            MenuDiorama.ApplySun(_sun, _profile);
            _board.BuildCanopies();
        }
        void ConfigureWorldAtmosphere(Camera camera)
        {
            using var audit = PerformanceAudit.Measure("QC.MenuSceneHost.ConfigureAtmosphere");
            var level = _services.MenuBackdrop; var world = _world; var viewport = _menuViewport;
            var tier = QualityTier();
            _atmosphere = gameObject.AddComponent<CampAtmosphere>();
            _atmosphere.Configure(camera, level, viewport, _profile,
                () => _services.ReducedMotion, tier, world, ()=>_services.EffectiveQuality);

            var drift = gameObject.AddComponent<MenuCameraDrift>();
            drift.Configure(camera, level, viewport, () => _services.ReducedMotion,
                () => _router != null && (_router.IsBusy
                    || (_router.Dive != null && !_router.Dive.IsIdle)));

            // A fire exists exactly where this playable clearing has one.
            bool hasFire = level.noise != null && level.noise.Length > 0;
            if (hasFire) _firePos = BoardMath.CellCenterWorld(level, new Cell(level.noise[0][0], level.noise[0][1]));
            _atmosphere.RegisterDecor(world);
            _atmosphere.SetFire(_firePos, hasFire);
            _atmosphere.BindRainWorld(world);
            _atmosphere.Soundscape.AccentSuppressed = () => (_router?.IsBusy ?? false)
                || (_screens.Current != "Main" && _screens.Current != "Album");
        }

        void TryBuildWorld(Action build)
        {
            try { build(); }
            catch (Exception error)
            {
                Debug.LogWarning($"[QuietCamp] Menu scenery unavailable; navigation remains usable. {error.GetType().Name}: {error.Message}");
                _board?.ClearAll(); _board = null;
                if (_atmosphere != null) { _atmosphere.SetSuspended(true); Destroy(_atmosphere); _atmosphere = null; }
                var drift = GetComponent<MenuCameraDrift>(); if (drift != null) { drift.enabled = false; Destroy(drift); }
                if (_world != null) { _world.gameObject.SetActive(false); Destroy(_world.gameObject); _world = null; }
                if (_menuViewport != null) { Destroy(_menuViewport.gameObject); _menuViewport = null; }
                if (_camera != null)
                {
                    if (_listenerProxy != null) { _listenerProxy.gameObject.SetActive(false); Destroy(_listenerProxy.gameObject); _listenerProxy = null; }
                    var listener = _camera.GetComponent<AudioListener>() ?? _camera.gameObject.AddComponent<AudioListener>();
                    listener.enabled = true;
                }
            }
        }

        int QualityTier() => _services.EffectiveQuality;

        float _roadmapSoundWeight=1;
        void Update()
        {
            if (_safeArea != null && _menuViewport != null && _lastMenuSize != _safeArea.rect.size)
            {
                _lastMenuSize = _safeArea.rect.size;
                var wide = _lastMenuSize.x >= 960 && _lastMenuSize.x > _lastMenuSize.y * 1.1f;
                _menuViewport.anchorMin = wide ? new Vector2(.46f, .1f) : new Vector2(0, .12f);
                _menuViewport.anchorMax = wide ? new Vector2(.98f, .90f) : new Vector2(1, .72f);
            }
            bool roadmap=_screens?.Current=="Levels"||_screens?.Current=="BranchPreview";
            if(roadmap||_roadmapSoundWeight<1)
            {_roadmapSoundWeight=Mathf.MoveTowards(_roadmapSoundWeight,roadmap?0:1,Time.unscaledDeltaTime/.6f);_atmosphere?.Soundscape?.SetVisibility(_roadmapSoundWeight);}
            if (_services?.Audio != null)
            {
                _services.Audio.MaxActiveVoices = _services.EffectiveQuality == 0 ? 8 : _services.EffectiveQuality == 1 ? 12 : 16;
                _services.Audio.SetPlaybackScale(_services.MusicBedHandle, _screens?.Current == "Settings" ? .75f : 1);
            }
        }

        void LateUpdate()
        {
            if (_listenerProxy != null && _camera != null) _listenerProxy.rotation = _camera.transform.rotation;
        }

        // ─── Actions ─────────────────────────────────────────────────────────

        void RegisterActions()
        {
            var h = _services.Dispatch;
            var levels = LevelLoader.MvpLevelIds();
            _leases.Add(h.Register(new UiActionId("qc.continue"), () =>
            {
                var id = _services.ContinueLevel();
                if (id != null) _router.GoToCamp(id);
                return UiActionResult.Performed();
            }));
            _leases.Add(h.Register(new UiActionId("qc.play"), req =>
            {
                var id = req.Payload as string ?? req.TargetId;
                if (!string.IsNullOrEmpty(id)) _router.GoToCamp(id);
                return UiActionResult.Performed();
            }));
            _leases.Add(h.Register(new UiActionId("qc.levels"), () => { _services.LevelMapJourney="main";return Show("Levels"); }));
            _leases.Add(h.Register(new UiActionId("qc.album"), () => Show("Album")));
            _leases.Add(h.Register(new UiActionId("qc.settings"), () => Show("Settings")));
            _leases.Add(h.Register(new UiActionId("qc.economy"), () => Show("Economy")));
            _leases.Add(h.Register(new UiActionId("qc.tutorial.restart"), () =>
            {
                _services.Tutorial.LearnAgain();
                var target = _services.Tutorial.CurrentLevelId;
                if (target != null) _router.GoToCamp(target);
                return UiActionResult.Performed();
            }));
            _leases.Add(h.Register(new UiActionId("qc.back"), () =>
            {
                if (_screens.Current != "Main") _screens.NavigateBack();
                return UiActionResult.Performed();
            }));
        }

        UiActionResult Show(string screen)
        {
            if (screen == "DemoComplete") _screens.ShowDemoComplete();
            else _screens.Show(screen);
            return UiActionResult.Performed();
        }

        void OnDestroy()
        {
            _board?.ClearAll(); _board = null;
            _screens?.Dispose();
            foreach (var l in _leases) l.Dispose();
            _leases.Clear();
            _menuContext?.Dispose();
            if (Current == this) Current = null;
        }

        // ─── Scene helpers ───────────────────────────────────────────────────

        static Transform ResolveScene(string path)
        {
            var parts = path.Split('/');
            var scene = SceneManager.GetActiveScene();
            foreach (var root in scene.GetRootGameObjects())
            {
                var t = root.transform;
                var i = t.name == parts[0] ? 1 : 0;
                for (; i < parts.Length && t != null; i++) t = t.Find(parts[i]);
                if (t != null) return t;
            }
            return null;
        }

        static Camera FindCamera()
        {
            var cam = Camera.main;
            if (cam == null)
                cam = FindFirstObjectByType<Camera>();
            return cam;
        }
    }
}
