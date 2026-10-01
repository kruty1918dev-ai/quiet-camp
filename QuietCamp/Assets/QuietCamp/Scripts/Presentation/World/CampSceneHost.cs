using System;
using System.Collections.Generic;
using Kruty1918.Audio;
using Kruty1918.Atmos;
using Kruty1918.InputRouting.API;
using Kruty1918.Notifications.API;
using Kruty1918.UIActions.API;
using QuietCamp.Application;
using QuietCamp.Domain;
using QuietCamp.Infrastructure;
using QuietCamp.Presentation.UI;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
namespace QuietCamp.Presentation.World
{
    /// <summary>
    /// Camp-scene composition: loads the level, builds the board into the
    /// scene scaffold, restores the saved session, wires placement input, HUD,
    /// audio bridge, lighting preset and the Gameplay UI context.
    /// Created by QuietCampBootstrap when the Camp scene finishes loading.
    /// </summary>
    public sealed class CampSceneHost : MonoBehaviour
    {
        GameServices _services;
        ScreenRouter _router;
        CampSession _session;
        BoardRenderer _renderer;
        PlacementController _placement;
        Transform _decorRoot;
        CampHud _hud;
        HintService _hint;
        TutorialDirector _tutorial;
        LocalizedLabel _tutorialLabel;
        GameObject _tutorialBar;
        readonly List<System.IDisposable> _leases = new List<System.IDisposable>();
        IDisposable _gameplayContext;
        float _birdTimer = 30f;
        AtmosphereCatalog _atmosphereCatalog;
        AtmosphereCatalog.Profile _atmosphereProfile;
        CampAtmosphere _atmosphere;
        FireVisual[] _fireVisuals;
        AudioHandle _fireSound, _cricketSound;
        System.Random _ambienceRandom;
        bool _completed;
        float _owlTimer = 60f;
        float _rustleCooldown;
        float _gustSoundCooldown;
        float _twigCooldown;
        /// <summary>Opening calm: no owl or gust one-shots for the first seconds
        /// after entering the scene — wind/fire may already be audible.</summary>
        float _entryCalm = 12f;
        /// <summary>Shared minimum gap between accented one-shots (bird/owl/gust).</summary>
        float _accentCooldown;
        int _lastBirdAnchor = -1;
        int _lastOwlAnchor = -1;
        // Phase-weight fades: loops keep running while only their playback
        // scale moves — no restart when the day phase changes.
        float _windScale = 1f, _windTarget = 1f;
        float _fireScale, _fireTarget;
        float _cricketScale, _cricketTarget;
        readonly List<Vector3> _canopyAnchors = new List<Vector3>();
        readonly List<Vector3> _edgeAnchors = new List<Vector3>();
        Vector3 _firePos;

        public static CampSceneHost Current { get; private set; }

        /// <summary>Set at the end of Start — the explicit readiness signal
        /// the ScreenRouter waits for before revealing (level loaded, world
        /// built, HUD applied, camera fitted, phase applied).</summary>
        public bool IsReady { get; private set; }

        public CampSession Session => _session;
        public CampAtmosphere Atmosphere => _atmosphere;

        public void Configure(GameServices services, ScreenRouter router)
        {
            _services = services;
            _router = router;
        }

        void Start()
        {
            Current = this;
            var levelId = _services.PendingLevelId ?? "QC_TEST";
            LevelData level;
            try { level = LevelLoader.Load(levelId); }
            catch (System.Exception e)
            {
                Debug.LogError($"[QuietCamp] {e.Message}");
                _services.Notifications.Show("save.failed", GameplayNotificationKind.Error);
                levelId = "QC_TEST";
                level = LevelLoader.Load(levelId);
            }
            BuildSession(level);
            BuildWorld(level);
            BuildHud(level);
            RegisterActions();
            ConfigureAtmosphere(level);
            _services.PendingLevelId = null;
            IsReady = true;
        }

        /// <summary>Cancels an in-flight placement drag without committing —
        /// used when a scene transition gates input mid-gesture.</summary>
        public void CancelActiveDrag() => _placement?.Cancel();

        // ─── Build ───────────────────────────────────────────────────────────

        void BuildSession(LevelData level)
        {
            _session = new CampSession(level);
            _hint = new HintService(level);
            _tutorial = new TutorialDirector(level.tutorialKey);
            _session.Evented += OnSessionEvent;

            // Restore a matching in-progress layout; mismatched contentHash
            // keeps progress but drops the stale session.
            var saved = _services.Save.Session;
            if (saved != null && saved.levelId == level.id
                && saved.contentHash == level.contentHash
                && saved.placements != null && saved.placements.Length > 0)
            {
                _session.Restore(saved.placements, saved.selectedGuestId);
            }
        }

        void BuildWorld(LevelData level)
        {
            var world = Find("World");
            var boardRoot = world != null ? world.transform.Find("BoardRoot") : null;
            Transform Child(string n) => boardRoot != null ? boardRoot.Find(n) : null;

            _renderer = new BoardRenderer(level, _services.Assets,
                Child("Base"), Child("Grid"), Child("Obstacle"),
                Child("Tent"), Child("Overlay"));
            _renderer.SyncPlacements(_session.State.Placements, level);

            _decorRoot = Find("DecorRoot")?.transform;
            if (_decorRoot != null)
            {
                for (var i = _decorRoot.childCount - 1; i >= 0; i--)
                    Destroy(_decorRoot.GetChild(i).gameObject);
                DecorSpawner.Spawn(level, _services.Assets, _decorRoot);
                CollectAnchors(_decorRoot);
            }

            // The scene's evening fire light must sit over this level's fire
            // cell — the prefab position was authored for a fixed cell.
            var lightingRoot = Find("LightingRoot");
            var fireLightT = lightingRoot != null ? lightingRoot.transform.Find("FireLight") : null;
            if (fireLightT != null && level.noise != null && level.noise.Length > 0)
            {
                _firePos = BoardMath.CellCenterWorld(level,
                    new Cell(level.noise[0][0], level.noise[0][1]));
                fireLightT.position = _firePos + new Vector3(0f, 0.5f, 0f);
            }

            var camera = FindCamera();
            var viewportGo = Find("CanvasRoot/SafeArea/Gameplay/BoardViewport");
            var viewport = viewportGo != null ? viewportGo.transform as RectTransform : null;
            CameraFitter.Configure(camera);
            CameraFitter.Fit(camera, level, viewport);

            // Stable listener proxy above the board centre — positional
            // sources read distance against the field, not the far camera.
            var camListener = camera.GetComponent<AudioListener>();
            if (camListener != null) camListener.enabled = false;
            var proxy = new GameObject("ListenerProxy");
            proxy.transform.SetPositionAndRotation(
                new Vector3(0f, 2f * BoardMath.CellSize, 0f), camera.transform.rotation);
            proxy.AddComponent<AudioListener>();

            var inputGo = new GameObject("PlacementController");
            _placement = inputGo.AddComponent<PlacementController>();
            _placement.Configure(_session, _renderer, _services.InputPolicy, camera,
                _services.Assets, () => _services.ReducedMotion,
                () => _services.MotionScale);
            _placement.PlacementFailed += key
                => _services.Notifications.Show(_services.Localization.T(key),
                    GameplayNotificationKind.Warning, dedupKey: key + _session.SelectedGuestId);
            _placement.PlacementCommitted += PersistSession;
            _placement.Cancelled += () => PlayAudio("ui.back");
            // Lifting a committed tent is a dry woody accent at its old cell;
            // picking a fresh tent for a new preview is a soft fabric rustle.
            _placement.TentGrabbed += id =>
            {
                var p = _session.State.Find(id);
                if (p == null) return;
                _services.Audio?.PlayAt("sfx.twig",
                    BoardMath.CellCenterWorld(_session.Level, new Cell(p.x, p.z))
                    + Vector3.up * .3f, .2f);
            };
            _placement.PreviewBegan += pos
                => _services.Audio?.PlayAt("sfx.rustle", pos, .3f);
        }

        /// <summary>Decor positions feed positional ambience: birds sing from
        /// canopies, rustles and the owl come from the clearing's edge.</summary>
        void CollectAnchors(Transform decorRoot)
        {
            _canopyAnchors.Clear();
            _edgeAnchors.Clear();
            foreach (Transform child in decorRoot)
            {
                var pos = child.position;
                if (child.name.StartsWith("tree", StringComparison.OrdinalIgnoreCase))
                {
                    var bounds = new Bounds(pos, Vector3.zero);
                    foreach (var r in child.GetComponentsInChildren<Renderer>())
                        bounds.Encapsulate(r.bounds);
                    _canopyAnchors.Add(new Vector3(pos.x, bounds.max.y * .9f, pos.z));
                }
                else
                {
                    _edgeAnchors.Add(pos + Vector3.up * .4f);
                }
            }
            if (_edgeAnchors.Count == 0) _edgeAnchors.Add(new Vector3(4f, .4f, 0f));
            if (_canopyAnchors.Count == 0) _canopyAnchors.Add(new Vector3(3f, 2f, -3f));
        }

        void BuildHud(LevelData level)
        {
            var safeAreaGo = Find("CanvasRoot/SafeArea");
            var safeArea = safeAreaGo != null ? safeAreaGo.transform as RectTransform : null;
            if (safeArea == null)
            {
                Debug.LogError("[QuietCamp] Camp scene lacks CanvasRoot/SafeArea.");
                return;
            }
            if (safeArea.GetComponent<SafeAreaFitter>() == null)
                safeArea.gameObject.AddComponent<SafeAreaFitter>();
            _hud = new CampHud(_services, _session, safeArea, _router);
            _hud.AreaShown += cells => ShowAreaOverlay(cells);
            _hud.MoveShown += move => ShowMoveOverlay(move);
            if (!_tutorial.Finished && _tutorial.ActiveKey != null)
            {
                var bar = QcUi.Anchor(safeArea, "TutorialBar",
                    new Vector2(0, 1), new Vector2(1, 1),
                    new Vector2(24, -420), new Vector2(-24, -330));
                bar.gameObject.AddComponent<UnityEngine.UI.Image>().color =
                    new Color(0.14f, 0.11f, 0.09f, 0.85f);
                // Gameplay hint sits under ModalLayer — a modal (settings,
                // pause, hint panel) always renders above gameplay chrome.
                var modalLayer = safeArea.Find("ModalLayer");
                if (modalLayer != null)
                    bar.SetSiblingIndex(modalLayer.GetSiblingIndex());
                _tutorialBar = bar.gameObject;
                _tutorialLabel = QcUi.Label(bar, _tutorial.ActiveKey,
                    QcUi.TextSmall, TMPro.TextAlignmentOptions.Center, QcUi.Cream);
                _tutorial.Changed += () =>
                {
                    if (_tutorialLabel != null)
                        Destroy(_tutorialLabel.transform.parent.gameObject);
                };
            }
        }

        void RegisterActions()
        {
            var h = _services.Dispatch;
            _gameplayContext = _services.ContextStack.Push(new UiContextRegistration(
                "Gameplay", UiContextLayer.Gameplay, 0, () => true,
                new UiActionId("qc.back")));

            _leases.Add(h.Register(new UiActionId("qc.back"), () =>
            {
                if (_hud.HasModalOpen) { _hud.CloseTopModal(); return Performed(); }
                if (_placement.HasPreview) { _placement.Cancel(); return Performed(); }
                _hud.ShowPause();
                return Performed();
            }));
            _leases.Add(h.Register(new UiActionId("qc.pause"), () => { _hud.ShowPause(); return Performed(); }));
            _leases.Add(h.Register(new UiActionId("qc.resume"), () => { _hud.CloseAllModals(); return Performed(); }));
            _leases.Add(h.Register(new UiActionId("qc.rotate"), () =>
            {
                _placement.Rotate();
                _tutorial.ReportAction("rotate");
                PlayAudio("placement.rotate");
                return Performed();
            }));
            _leases.Add(h.Register(new UiActionId("qc.undo"), () =>
            {
                if (_session.Undo()) { PlayAudio("placement.undo"); PersistSession(); }
                return Performed();
            }));
            _leases.Add(h.Register(new UiActionId("qc.redo"), () =>
            {
                if (_session.Redo()) { PlayAudio("placement.undo"); PersistSession(); }
                return Performed();
            }));
            _leases.Add(h.Register(new UiActionId("qc.remove"), () =>
            {
                var id = _session.SelectedGuestId;
                var wasPlaced = id != null && _session.State.Find(id) != null;
                _placement.Remove();
                if (wasPlaced && _twigCooldown <= 0f) { _twigCooldown = 2f; PlayAudio("sfx.twig"); }
                return Performed();
            }));
            _leases.Add(h.Register(new UiActionId("qc.check"), () =>
            {
                var report = _session.Check();
                _tutorial.ReportAction("check");
                if (!report.IsSolved)
                {
                    var key = _hint.Explain(report);
                    _services.Notifications.Show(_services.Localization.T(key),
                        GameplayNotificationKind.Warning, dedupKey: key);
                    PlayAudio("rule.invalid");
                }
                return Performed();
            }));
            _leases.Add(h.Register(new UiActionId("qc.hint"), () => { _hud.ShowHint(); return Performed(); }));
            _leases.Add(h.Register(new UiActionId("qc.settings"), () => { _hud.ShowSettings(); return Performed(); }));
            _leases.Add(h.Register(new UiActionId("qc.levels"), () =>
            {
                PersistSession();
                _services.Save.Save();
                _router.GoToMenu();
                return Performed();
            }));
            _leases.Add(h.Register(new UiActionId("qc.next"), () =>
            {
                var next = _services.Progression.NextAfter(_session.Level.id, LevelLoader.MvpLevelIds());
                _services.Save.Save();
                if (next != null) _router.GoToNextCamp(next);
                else _router.GoToMenu();
                return Performed();
            }));
            _leases.Add(h.Register(new UiActionId("qc.album"), () =>
            {
                PersistSession();
                _services.Save.Save();
                _router.GoToMenu();
                return Performed();
            }));
            _leases.Add(h.Register(new UiActionId("qc.select"), req =>
            {
                var before = _session.SelectedGuestId;
                _session.Select(req.Payload as string ?? req.TargetId);
                // Only an actual selection change earns feedback.
                if (_session.SelectedGuestId != before) PlayAudio("ui.select");
                return Performed();
            }));
        }

        static UiActionResult Performed() => UiActionResult.Performed();

        // ─── Session events / audio / saves ─────────────────────────────────

        void OnSessionEvent(CampEvent e)
        {
            switch (e.Kind)
            {
                case CampEventKind.BoardCommitted:
                    if (_session.State.Contains(e.GuestId))
                        _tutorial.ReportAction("commit");
                    _renderer.SyncPlacements(_session.State.Placements, _session.Level,
                        _services.MotionScale);
                    // Commit lands where the guest sits, not flat at the listener.
                    var placed = _session.State.Find(e.GuestId);
                    if (placed != null)
                        _services.Audio?.PlayAt("placement.commit",
                            BoardMath.CellCenterWorld(_session.Level,
                                new Cell(placed.x, placed.z)) + Vector3.up * .4f);
                    break;
                case CampEventKind.LevelCompleted:
                    OnLevelCompleted();
                    break;
            }
        }

        void OnLevelCompleted()
        {
            if (_completed) return;
            _completed = true;
            // Completion ducks the forest briefly (0.75 / 100 ms / 500 ms /
            // 900 ms) instead of silencing it — ordinary taps never duck.
            _services.Audio?.DuckBus(Kruty1918.Audio.AudioBus.Ambience, .75f, .1f, .5f, .9f);
            PlayAudio("level.complete");
            StartCoroutine(ChimeAfter(.18f));
            var level = _session.Level;
            var first = _services.Progression.MarkCompleted(level.id);
            var album = _services.Save.Album;
            var entries = new List<AlbumSaveData.Entry>(album.entries ?? new AlbumSaveData.Entry[0]);
            entries.RemoveAll(x => x.levelId == level.id);
            entries.Add(new AlbumSaveData.Entry
            {
                levelId = level.id,
                placements = _session.State.Snapshot(),
                order = entries.Count,
                cosmeticId = _services.Progression.CosmeticFlags > 0 ? "fabric.b" : "fabric.a",
            });
            album.entries = entries.ToArray();
            _services.Save.Session = new SessionSaveData(); // done — clear in-progress
            if (!_services.Save.Save())
                _services.Notifications.Show(_services.Localization.T("save.failed"),
                    GameplayNotificationKind.Error);
            // Completion must not turn a night level back into an evening level.
            _services.Motion.Cancel(this);
            var mvp = LevelLoader.MvpLevelIds();
            if (mvp.Count > 0 && level.id == mvp[mvp.Count - 1])
            {
                // DemoComplete follows the completion panel via qc.next.
            }
        }

        /// <summary>The completion chime is a soft afterglow, not a second
        /// fanfare — it follows the main cue instead of stacking onto it.</summary>
        System.Collections.IEnumerator ChimeAfter(float delay)
        {
            yield return new WaitForSeconds(delay);
            _services.Audio?.Play("sfx.chime", new AudioPlayOptions(volumeScale: .3f));
        }

        void PersistSession()
        {
            _services.Save.Session = new SessionSaveData
            {
                levelId = _session.Level.id,
                contentHash = _session.Level.contentHash,
                ruleVersion = _session.Level.ruleVersion,
                placements = _session.State.Snapshot(),
                selectedGuestId = _session.SelectedGuestId,
            };
            _services.Save.Save();
        }

        void PlayAudio(string key) => _services.Audio?.Play(key);

        // ─── Lighting ────────────────────────────────────────────────────────

        void ConfigureAtmosphere(LevelData level)
        {
            _atmosphereCatalog = AtmosphereCatalog.Load();
            _ambienceRandom = new System.Random(level.decorSeed);
            _fireVisuals = FindObjectsByType<FireVisual>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            _atmosphere = gameObject.AddComponent<CampAtmosphere>();
            var viewport = Find("CanvasRoot/SafeArea/Gameplay/BoardViewport")?.transform as RectTransform;
            var profile = _atmosphereCatalog.Resolve(level.id, level.lighting);
            var tier = QualityTier();
            _atmosphere.Configure(FindCamera(), level, viewport, profile,
                () => _services.ReducedMotion, tier, _decorRoot);
            // No near-leaf flyby while dragging a tent, behind a modal or
            // during a scene transition (atmosphere spec §4.2/§9).
            if (_atmosphere.Particles != null)
                _atmosphere.Particles.NearLeafSuppressed = () =>
                    (_placement != null && _placement.HasPreview)
                    || (_hud != null && _hud.HasModalOpen)
                    || (_router != null && _router.IsBusy);
            _atmosphere.GustStarted += OnWindGust;
            // Voice budget by quality tier (Low/Balanced/High → 8/12/16);
            // decorative one-shots are skipped first when the pool is full.
            if (_services.Audio != null)
                _services.Audio.MaxActiveVoices = tier == 0 ? 8 : tier == 1 ? 12 : 16;
            SetAtmospherePhase(profile.Id);
        }

        /// <summary>0–2 = Low/Balanced/High. settings.quality 0 = auto from
        /// the active QualitySettings level; 1–3 is a manual override.</summary>
        int QualityTier()
        {
            int q = _services.Settings.quality;
            if (q >= 1 && q <= 3) return q - 1;
            return Mathf.Clamp(QualitySettings.GetQualityLevel(), 0, 2);
        }

        /// <summary>One real gust = one quiet gust one-shot plus, at most,
        /// one rustle at the nearest decorative anchor (cooldown-gated).</summary>
        void OnWindGust()
        {
            if (_services.Audio == null || _gustSoundCooldown > 0f
                || _entryCalm > 0f || _accentCooldown > 0f || _completed
                || (_router != null && _router.IsBusy)
                || (_hud != null && _hud.HasModalOpen)) return;
            _gustSoundCooldown = 18f; // spec: at least 18 s between gust one-shots
            _accentCooldown = 3f;
            var dir = _atmosphere.Wind.DirectionXZ;
            var dir3 = new Vector3(dir.x, 0f, dir.y);
            float meadow = Mathf.Max(_session.Level.width, _session.Level.height) * .5f
                + DecorSpawner.Apron;
            _services.Audio.Play("ambience.gust", new AudioPlayOptions(
                position: -dir3 * meadow + Vector3.up * 1.2f,
                pitchOffset: Jitter(.015f)));
            if (_rustleCooldown <= 0f)
            {
                _rustleCooldown = 10f;
                _services.Audio.Play("sfx.rustle", new AudioPlayOptions(
                    position: NearestAnchor(-dir3), volumeScale: .7f,
                    pitchOffset: Jitter(.02f)));
            }
        }

        Vector3 NearestAnchor(Vector3 toward)
        {
            var best = _edgeAnchors[0];
            var bestDot = float.MinValue;
            foreach (var a in _edgeAnchors)
            {
                var d = Vector3.Dot((a - Vector3.zero).normalized, toward.normalized);
                if (d > bestDot) { bestDot = d; best = a; }
            }
            return best;
        }

        /// <summary>One presentation authority for light, scenery, fire and phase-dependent audio.</summary>
        public void SetAtmospherePhase(string phase)
        {
            var profile = _atmosphereCatalog.Get(phase);
            if (_atmosphereProfile == profile) return;
            _atmosphereProfile = profile;
            _atmosphere.Apply(profile);
            var lighting = Find("LightingRoot");
            var dir = lighting != null ? lighting.transform.Find("DirectionalLight") : null;
            var fire = lighting != null ? lighting.transform.Find("FireLight") : null;
            if (dir != null)
            {
                var light = dir.GetComponent<Light>();
                if (light != null)
                {
                    dir.localEulerAngles = new Vector3(profile.Elevation, -35f, 0f);
                    light.intensity = profile.SunIntensity;
                    light.color = profile.Sun;
                }
            }
            bool hasFire = _fireVisuals.Length > 0 && profile.Fire;
            if (fire != null) fire.gameObject.SetActive(hasFire);
            foreach (var fv in _fireVisuals) if (fv != null) fv.SetBurning(hasFire);
            // Phase weights fade on running loops — a loop that survives the
            // phase change is never restarted, only re-weighted.
            if (hasFire && _services.Audio != null)
            {
                if (!_fireSound.IsValid)
                {
                    _fireSound = _services.Audio.PlayAt("ambience.fire",
                        _firePos + Vector3.up * .4f);
                    _fireScale = 0f;
                }
                _fireTarget = 1f;
            }
            else _fireTarget = 0f;
            if (profile.Crickets > 0f && _services.Audio != null)
            {
                if (!_cricketSound.IsValid)
                {
                    _cricketSound = _services.Audio.Play("ambience.crickets");
                    _cricketScale = 0f;
                }
                _cricketTarget = profile.Crickets;
            }
            else _cricketTarget = 0f;
            _windTarget = profile.WindAudio;
            _birdTimer = NextBirdDelay();
            _owlTimer = NextOwlDelay();
            _atmosphere.SetFire(_firePos, hasFire);
        }

        float NextBirdDelay() => _atmosphereProfile == null || _atmosphereProfile.BirdMax == 0
            ? float.PositiveInfinity : Mathf.Lerp(_atmosphereProfile.BirdMin,
                _atmosphereProfile.BirdMax, (float)_ambienceRandom.NextDouble());

        float NextOwlDelay() => _atmosphereProfile == null || _atmosphereProfile.OwlMax == 0
            ? float.PositiveInfinity : Mathf.Lerp(_atmosphereProfile.OwlMin,
                _atmosphereProfile.OwlMax, (float)_ambienceRandom.NextDouble());

        // ─── Frame loop ──────────────────────────────────────────────────────

        void Update()
        {
            _hud?.PumpHint();
            // Gameplay hints never render over an open modal — restore the
            // bar only once every modal has fully closed.
            if (_tutorialBar != null)
            {
                var modalOpen = _hud != null && _hud.HasModalOpen;
                if (_tutorialBar.activeSelf == modalOpen)
                    _tutorialBar.SetActive(!modalOpen);
            }
            _rustleCooldown -= Time.deltaTime;
            _gustSoundCooldown -= Time.deltaTime;
            _twigCooldown -= Time.deltaTime;
            _accentCooldown -= Time.deltaTime;
            _entryCalm -= Time.deltaTime;
            TickAmbienceFades();
            if (_atmosphereProfile == null) return;
            // Decorative accents rest during completion, scene transitions and
            // open modals (pause/settings/hint) — nothing queues up behind them.
            bool quiet = _completed || (_router != null && _router.IsBusy)
                || (_hud != null && _hud.HasModalOpen);
            _birdTimer -= Time.deltaTime;
            if (_birdTimer <= 0f)
            {
                _birdTimer = NextBirdDelay();
                if (!quiet && _accentCooldown <= 0f && _services.Audio != null)
                {
                    _accentCooldown = 3f;
                    var i = NextAnchor(_canopyAnchors, ref _lastBirdAnchor);
                    // Evening birds sing softer than morning/noon.
                    var scale = _atmosphereProfile.Id == "evening" ? .65f : 1f;
                    _services.Audio.Play("ambience.bird", new AudioPlayOptions(
                        position: _canopyAnchors[i], volumeScale: scale,
                        pitchOffset: Jitter(.025f)));
                }
            }
            _owlTimer -= Time.deltaTime;
            if (_owlTimer <= 0f)
            {
                _owlTimer = NextOwlDelay();
                if (!quiet && _entryCalm <= 0f && _accentCooldown <= 0f
                    && _services.Audio != null)
                {
                    _accentCooldown = 3f;
                    var i = NextAnchor(_edgeAnchors, ref _lastOwlAnchor);
                    _services.Audio.Play("ambience.owl", new AudioPlayOptions(
                        position: _edgeAnchors[i], pitchOffset: Jitter(.01f)));
                }
            }
        }

        /// <summary>Moves loop weights toward their phase targets — wind bed
        /// (3 s), fire (1.2 s) and crickets (4 s) crossfade without restarts.</summary>
        void TickAmbienceFades()
        {
            if (_services.Audio == null) return;
            if (_windScale != _windTarget)
            {
                _windScale = MoveToward(_windScale, _windTarget, 3f);
                _services.Audio.SetPlaybackScale(_services.AmbientWindHandle, _windScale);
            }
            if (_fireScale != _fireTarget || (_fireTarget == 0f && _fireSound.IsValid))
            {
                _fireScale = MoveToward(_fireScale, _fireTarget, 1.2f);
                if (_fireTarget == 0f && _fireScale <= 0f)
                {
                    _fireSound.Stop();
                    _fireSound = default;
                }
                else _services.Audio.SetPlaybackScale(_fireSound, _fireScale);
            }
            if (_cricketScale != _cricketTarget
                || (_cricketTarget == 0f && _cricketSound.IsValid))
            {
                _cricketScale = MoveToward(_cricketScale, _cricketTarget, 4f);
                if (_cricketTarget == 0f && _cricketScale <= 0f)
                {
                    _cricketSound.Stop();
                    _cricketSound = default;
                }
                else _services.Audio.SetPlaybackScale(_cricketSound, _cricketScale);
            }
        }

        static float MoveToward(float current, float target, float seconds)
            => Mathf.MoveTowards(current, target, Time.deltaTime / seconds);

        /// <summary>Picks an anchor different from the last used index —
        /// consecutive bird/owl calls never come from the same spot twice.</summary>
        int NextAnchor(List<Vector3> anchors, ref int last)
        {
            if (anchors.Count <= 1) return last = 0;
            int i;
            do { i = _ambienceRandom.Next(anchors.Count); } while (i == last);
            return last = i;
        }

        float Jitter(float range)
            => (float)(_ambienceRandom.NextDouble() * 2.0 - 1.0) * range;

        void ShowAreaOverlay(Cell[] cells) { /* area overlay uses existing path/chip visuals */ }
        void ShowMoveOverlay(Placement move) { /* ghost flash handled by HUD text */ }

        void OnDestroy()
        {
            if (_atmosphere != null) _atmosphere.GustStarted -= OnWindGust;
            _fireSound.Stop();
            _cricketSound.Stop();
            if (_session != null) _session.Evented -= OnSessionEvent;
            _session?.Dispose();
            _hud?.Dispose();
            _renderer?.ClearAll();
            foreach (var l in _leases) l.Dispose();
            _leases.Clear();
            _gameplayContext?.Dispose();
            if (Current == this) Current = null;
        }

        // ─── Scene helpers ───────────────────────────────────────────────────

        static GameObject Find(string path)
        {
            var scene = SceneManager.GetActiveScene();
            foreach (var root in scene.GetRootGameObjects())
            {
                var t = Resolve(root.transform, path);
                if (t != null) return t.gameObject;
            }
            return null;
        }

        static Transform Resolve(Transform root, string path)
        {
            var parts = path.Split('/');
            var t = root;
            var i = root.name == parts[0] ? 1 : 0;
            for (; i < parts.Length; i++)
            {
                t = t.Find(parts[i]);
                if (t == null) return null;
            }
            return t;
        }

        static Camera FindCamera()
        {
            var go = Find("MainCamera");
            var camera = go != null ? go.GetComponent<Camera>() : null;
            return camera != null ? camera : Camera.main;
        }
    }
}
