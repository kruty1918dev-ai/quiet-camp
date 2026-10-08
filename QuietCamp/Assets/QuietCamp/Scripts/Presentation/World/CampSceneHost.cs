using System;
using System.Collections.Generic;
using Kruty1918.Audio;
using Kruty1918.Haptics;
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
        Camera _worldCamera;
        Transform _decorRoot;
        CampHud _hud;
        HintAdvisor _advisor;
        TutorialDirector _tutorial;
        readonly List<System.IDisposable> _leases = new List<System.IDisposable>();
        IDisposable _gameplayContext;
        AtmosphereCatalog _atmosphereCatalog;
        AtmosphereCatalog.Profile _atmosphereProfile;
        CampAtmosphere _atmosphere;
        CampFeedbackEffects _feedback;
        FireVisual[] _fireVisuals;
        bool _completed;
        bool _advancing;
        float _twigCooldown;
        float _musicScale = 1;
        Vector3 _firePos;

        public static CampSceneHost Current { get; private set; }

        /// <summary>Set at the end of Start — the explicit readiness signal
        /// the ScreenRouter waits for before revealing (level loaded, world
        /// built, HUD applied, camera fitted, phase applied).</summary>
        public bool IsReady { get; private set; }

        public CampSession Session => _session;
        public CampAtmosphere Atmosphere => _atmosphere;
        public bool GameplayActive => IsReady && !_completed && !(_hud?.HasModalOpen ?? false) && !(_router?.IsBusy ?? false) && !_services.MonetizationBusy;
        public bool UiReady => IsReady && (_atmosphere?.InitialWorldReady ?? true) && (_hud?.UiReady ?? false);

        public void Configure(GameServices services, ScreenRouter router)
        {
            _services = services;
            _router = router;
        }

        void Start()
        {
            using var audit = PerformanceAudit.Measure("QC.CampSceneHost.Start");
            Current = this;
            var levelId = _services.PendingLevelId ?? "QC_TEST";
            if (!_services.CanStart(levelId))
            {
                _services.Notifications.Show(_services.Localization.T("journey.access.denied"), GameplayNotificationKind.Info);
                IsReady = true; StartCoroutine(ReturnToMenu()); return;
            }
            LevelData level;
            try { level = CampContent.SessionLevel(_services.Save.Session,levelId); }
            catch (System.Exception e)
            {
                Debug.LogError($"[QuietCamp] {e.Message}");
                _services.Notifications.Show("save.failed", GameplayNotificationKind.Error);
                IsReady = true; StartCoroutine(ReturnToMenu()); return;
            }
            BuildSession(level);
            BuildWorld(level);
            BuildHud(level);
            Canvas.ForceUpdateCanvases();
            var htmlViewport = Find("CanvasRoot/SafeArea/Gameplay/BoardViewport");
            CameraFitter.Fit(FindCamera(), level, htmlViewport != null ? htmlViewport.transform as RectTransform : null);
            RegisterActions();
            ConfigureAtmosphere(level);
            _renderer.BuildCanopies();
            _atmosphere.RegisterDecor(Find("World")?.transform);
            _atmosphere.BindRainWorld(Find("World")?.transform);
            _services.PendingLevelId = null;
            IsReady = true;
            if (_tutorial.Guiding(_session.Level.id)) _tutorial.BeginIntroduction();
        }

        /// <summary>Cancels an in-flight placement drag without committing —
        /// used when a scene transition gates input mid-gesture.</summary>
        public void CancelActiveDrag() => _placement?.Cancel();

        // ─── Build ───────────────────────────────────────────────────────────

        System.Collections.IEnumerator ReturnToMenu()
        {
            while (_router.IsBusy) yield return null;
            _services.PendingLevelId = null; _services.PendingMenuScreen = "Levels";
            _router.GoToMenu();
        }

        void BuildSession(LevelData level)
        {
            using var audit = PerformanceAudit.Measure("QC.CampSceneHost.BuildSession");
            _session = new CampSession(level);
            _session.CompletionPersistence = () => _services.Completion.Complete(_session, _atmosphere?.PhaseId);
            _advisor = new HintAdvisor(level);
            _tutorial = _services.Tutorial;

            // Restore a matching in-progress layout; mismatched contentHash
            // keeps progress but drops the stale session. Subscribe after the
            // restore — its emits would reach OnSessionEvent before _renderer
            // exists, and BuildWorld does its own initial sync anyway.
            var saved = _services.Save.Session;
            if (saved != null && saved.levelId == level.id
                && saved.contentHash == level.contentHash
                && saved.placements != null && saved.placements.Length > 0)
            {
                _session.Restore(saved.placements, saved.selectedGuestId);
            }
            _session.Evented += OnSessionEvent;
            _session.ActionRecorded += _services.Analytics.Action;
            _session.ActionRecorded += OnTutorialAction;
        }

        void BuildWorld(LevelData level)
        {
            using var audit = PerformanceAudit.Measure("QC.CampSceneHost.BuildWorld");
            var world = Find("World");
            var boardRoot = world != null ? world.transform.Find("BoardRoot") : null;
            Transform Child(string n) => boardRoot != null ? boardRoot.Find(n) : null;

            _renderer = new BoardRenderer(level, _services.Assets,
                Child("Base"), Child("Grid"), Child("Obstacle"),
                Child("Tent"), Child("Overlay"));
            _renderer.SyncPlacements(_session.State.Placements, level, _services.MotionScale, _services.ReducedMotion);

            _decorRoot = Find("DecorRoot")?.transform;
            if (_decorRoot != null)
            {
                for (var i = _decorRoot.childCount - 1; i >= 0; i--)
                    Destroy(_decorRoot.GetChild(i).gameObject);
                DecorSpawner.Spawn(level, _services.Assets, _decorRoot);
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
            _worldCamera = camera;
            _feedback = gameObject.AddComponent<CampFeedbackEffects>();
            _feedback.Configure(QualityTier(), () => _services.ReducedMotion, level.decorSeed);
            _feedback.SetEnvironment(level);
            _feedback.FollowQuality(() => _services.EffectiveQuality);
            _feedback.FollowWind(() => _atmosphere != null ? _atmosphere.Wind.DirectionXZ * _atmosphere.Wind.Strength : Vector2.zero);
            var viewportGo = Find("CanvasRoot/SafeArea/Gameplay/BoardViewport");
            var viewport = viewportGo != null ? viewportGo.transform as RectTransform : null;
            // Menu and gameplay share the same view onto the clearing.
            CameraFitter.Configure(camera);
            CameraFitter.Fit(camera, level, viewport);
            _renderer.BindViewport(camera.GetComponent<Kruty1918.GameplayViewport.GameplayViewport>());

            // Stable listener proxy above the board centre — positional
            // sources read distance against the field, not the far camera.
            var camListener = camera.GetComponent<AudioListener>();
            if (camListener != null) { camListener.enabled = false; Destroy(camListener); }
            var proxy = new GameObject("ListenerProxy");
            proxy.transform.SetPositionAndRotation(
                new Vector3(0f, 2f * BoardMath.CellSize, 0f), camera.transform.rotation);
            proxy.AddComponent<AudioListener>();

            var inputGo = new GameObject("PlacementController");
            _placement = inputGo.AddComponent<PlacementController>();
            _placement.Configure(_session, _renderer, _services.InputPolicy, camera,
                _services.Assets, () => _services.ReducedMotion,
                () => _services.MotionScale);
            _placement.PlacementFailed += key =>
            {
                _services.Analytics.Action(ComfortAction.InvalidDrop);
                _services.Notifications.Show(_services.Localization.T(key),
                    GameplayNotificationKind.Warning, dedupKey: key + _session.SelectedGuestId);
                PlayAudio("rule.invalid");
                _services.PlayHaptic(HapticCue.Warning);
                if (_tutorial.Guiding(_session.Level.id))
                    _hud.ShowGuideHint(GuideMistakeKey(key));
            };
            _placement.PlacementCommitted += PersistSession;
            _placement.DragMotion += (position,speed) => _atmosphere?.Soundscape?.TentDragged(position,speed);
            _placement.Cancelled += () => { PlayAudio("ui.back"); _services.Analytics.Action(ComfortAction.CancelDrag); };
            // Lifting a committed tent is a dry woody accent at its old cell;
            // picking a fresh tent for a new preview is a soft fabric rustle.
            _placement.TentGrabbed += id =>
            {
                var p = _session.State.Find(id);
                if (p == null) return;
                _feedback?.Lift(BoardMath.TentCenter(_session.Level, p.x, p.z));
                _services.Audio?.PlayAt("sfx.tent.lift",
                    BoardMath.CellCenterWorld(_session.Level, new Cell(p.x, p.z))
                    + Vector3.up * .3f, .2f);
                _services.PlayHaptic(HapticCue.Light);
            };
            _placement.PreviewBegan += pos =>
            {
                _tutorial.ReportAction("select", _session);
                _services.Audio?.PlayAt("sfx.tent.lift", pos, .5f);
                _services.PlayHaptic(HapticCue.Light);
            };
        }

        void BuildHud(LevelData level)
        {
            using var audit = PerformanceAudit.Measure("QC.CampSceneHost.BuildHud");
            var safeAreaGo = Find("CanvasRoot/SafeArea");
            var safeArea = safeAreaGo != null ? safeAreaGo.transform as RectTransform : null;
            if (safeArea == null)
            {
                Debug.LogError("[QuietCamp] Camp scene lacks CanvasRoot/SafeArea.");
                return;
            }
            if (safeArea.GetComponent<SafeAreaFitter>() == null)
                safeArea.gameObject.AddComponent<SafeAreaFitter>();
            _hud = new CampHud(_services, _session, safeArea);
            _hud.BindPlacement(_placement);
            _hud.AreaShown += cells => _renderer.ShowIssueCells(cells);
            // World-anchored chips: tent top → HUD css px.
            _hud.GuestAnchor = GuestWorldAnchor;
            _hud.ProjectToHud = ProjectToHud;
            _placement.PreviewChanged += _hud.SetPlacementPreview;
            if (_session.SelectedGuestId == null && level.guests.Length > 0)
                _session.Select(level.guests[0].id);
            _hud.GuestSelected += _ => _tutorial.ReportAction("select", _session);
            _hud.SetTutorial(_tutorial.Cue(level.id));
            // Act framing: the first unplayed glade of a new district opens
            // with the guide narrating what this region is — once ever.
            var district = LevelLoader.DistrictFor(level.id);
            if (district != null && !_services.Progression.IsCompleted(level.id)
                && !_tutorial.Guiding(level.id) && !_tutorial.DistrictSeen(district.id))
            {
                _tutorial.MarkDistrictSeen(district.id);
                _hud.ShowGuideHint(district.IntroKey, 9f);
            }
            else
            {
                // The ongoing guide: a glade that introduces a sign the player was
                // never taught gets a one-time explanation — even after updates.
                var sign = _tutorial.PendingSign(level);
                if (sign != null)
                {
                    _tutorial.ExplainSign(sign);
                    _hud.ShowGuideHint("guide.sign." + sign, 7f);
                }
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
                if (_hud.HasModalOpen) { _hud.NavigateBack(); return Performed(); }
                if (_placement.HasPreview) { _placement.Cancel(); return Performed(); }
                _hud.ShowPause();
                return Performed();
            }));
            _leases.Add(h.Register(new UiActionId("qc.pause"), () => { _hud.ShowPause(); return Performed(); }));
            _leases.Add(h.Register(new UiActionId("qc.resume"), () =>
            {
                PlayAudio("sfx.resume"); _hud.CloseAllModals(); NudgeCamera(); return Performed();
            }));
            _leases.Add(h.Register(new UiActionId("qc.rotate"), () =>
            {
                var placed = _session.State.Contains(_session.SelectedGuestId);
                if (_placement.Rotate())
                {
                    if (_placement.HasPreview) _services.Analytics.Action(ComfortAction.Rotate);
                    PlayAudio("placement.rotate");
                    _services.PlayHaptic(HapticCue.Selection);
                }
                return Performed();
            }));
            _leases.Add(h.Register(new UiActionId("qc.undo"), () =>
            {
                if (_session.Undo()) { PlayAudio("placement.undo"); _services.PlayHaptic(HapticCue.Light); PersistSession(); NudgeCamera(); }
                return Performed();
            }));
            _leases.Add(h.Register(new UiActionId("qc.redo"), () =>
            {
                if (_session.Redo()) { PlayAudio("placement.undo"); _services.PlayHaptic(HapticCue.Light); PersistSession(); NudgeCamera(); }
                return Performed();
            }));
            _leases.Add(h.Register(new UiActionId("qc.remove"), () =>
            {
                var id = _session.SelectedGuestId;
                var wasPlaced = id != null && _session.State.Find(id) != null;
                _placement.Remove();
                if (wasPlaced && !_session.State.Contains(id)) _services.PlayHaptic(HapticCue.Light);
                if (wasPlaced && _twigCooldown <= 0f) { _twigCooldown = .25f; PlayAudio("sfx.tent.remove"); }
                return Performed();
            }));
            _leases.Add(h.Register(new UiActionId("qc.check"), () =>
            {
                if (_session.IsCompleted || _services.MonetizationBusy || !_tutorial.CanCompleteLevel(_session.Level.id)) return Performed();
                _placement.Cancel();
                if (_tutorial.Guiding(_session.Level.id))
                {
                    // Learning checks are free: no life spent, no layout wipe.
                    _session.Check(() => _services.Completion.Complete(_session, _atmosphere?.PhaseId));
                    if (!_session.IsCompleted) { GuideFirstIssue("guide.mistake.check"); PlayAudio("rule.invalid"); }
                    return Performed();
                }
                if (!_services.Economy.CanCheck) { _hud.ShowEconomy(); return Performed(); }
                var report = RuleEvaluator.Evaluate(_session.Level, _session.State.Placements, true);
                var outcome = _services.Attempts.Check(_session, _atmosphere?.PhaseId); // done — clear in-progress
                if (outcome == CampAttemptResult.SaveFailed)
                {
                    _services.Notifications.Show(_services.Localization.T("save.failed"), GameplayNotificationKind.Error);
                    return Performed();
                }
                _services.Analytics.RuleReport(report);
                if (outcome == CampAttemptResult.FailedAttempt)
                {
                    _services.Analytics.Action(ComfortAction.Check);
                    _advisor = new HintAdvisor(_session.Level);
                    _services.Notifications.Show(_services.Localization.T("economy.attempt.failed"), GameplayNotificationKind.Warning, dedupKey: "attempt.failed");
                    PlayAudio("rule.invalid"); _services.PlayHaptic(HapticCue.Warning);
                    if (!_services.Economy.CanCheck) _hud.ShowEconomy();
                }
                return Performed();
            }));
            // Concrete hint: advisor picks guest + pose → green cells + ghost
            // tent on the board, plus a short toast line.
            _leases.Add(h.Register(new UiActionId("qc.hint"), () =>
            {
                if (_session.IsCompleted || _services.MonetizationBusy) return Performed();
                if (!_services.Economy.CanHint) { _hud.ShowEconomy(); return Performed(); }
                var s = _advisor.Suggest(_session.State.Placements, _session.SelectedGuestId);
                if (s == null) return Performed();
                if (_services.Economy.UseHint() != EconomyResult.Applied)
                { _services.Notifications.Show(_services.Localization.T("save.failed"), GameplayNotificationKind.Error); return Performed(); }
                _services.Analytics.Action(ComfortAction.Hint);
                PlayAudio("sfx.hint");
                if (s.GuestId != null) _session.Select(s.GuestId);
                if (s.Cells != null) _renderer.ShowHintCells(s.Cells);
                if (s.Move != null)
                {
                    var guest = System.Array.Find(_session.Level.guests, g => g.id == s.GuestId);
                    _renderer.ShowHintGhost(s.Move, guest?.assetId);
                }
                if (s.TextKey != null)
                    _services.Notifications?.Show(_services.Localization.T(s.TextKey),
                        GameplayNotificationKind.Info, dedupKey: "hint");
                return Performed();
            }));
            _leases.Add(h.Register(new UiActionId("qc.settings"), () => { _hud.ShowSettings(); return Performed(); }));
            _leases.Add(h.Register(new UiActionId("qc.economy"), () => { _hud.ShowEconomy(); return Performed(); }));
            _leases.Add(h.Register(new UiActionId("qc.tutorial.restart"), () =>
            {
                PersistSession(); _tutorial.LearnAgain();
                var target = _tutorial.CurrentLevelId;
                if (target != null) _router.GoToNextCamp(target);
                return Performed();
            }));
            UiActionResult LeaveCamp()
            {
                PersistSession();
                _services.Save.Save();
                _router.GoToMenu();
                return Performed();
            }
            _leases.Add(h.Register(new UiActionId("qc.levels"), LeaveCamp));
            _leases.Add(h.Register(new UiActionId("qc.menu"), () =>
            {
                _services.PendingMenuScreen = null;
                return LeaveCamp();
            }));
            _leases.Add(h.Register(new UiActionId("qc.next"), () =>
            {
                if (!_session.IsCompleted || _advancing) return Performed();
                _advancing = true;
                var next = _services.JourneyAccess.NextAfter(_session.Level.id);
                _services.Save.Save();
                if (next != null) _router.GoToNextCamp(next);
                else
                {
                    // Last glade done — the menu opens on the demo-complete card.
                    _services.PendingMenuScreen = BonusCampCatalog.ForLevel(_session.Level.id)!=null?"Levels":"DemoComplete";
                    _router.GoToMenu();
                }
                return Performed();
            }));
            _leases.Add(h.Register(new UiActionId("qc.album"), () =>
            {
                PersistSession();
                _services.Save.Save();
                _services.PendingMenuScreen = "Album";
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
                    _renderer.HideHint();
                    _renderer.HideIssueCells();
                    _renderer.SyncPlacements(_session.State.Placements, _session.Level,
                        _services.MotionScale, _services.ReducedMotion);
                    // Commit lands where the guest sits, not flat at the listener.
                    var placed = _session.State.Find(e.GuestId);
                    if (placed != null)
                    {
                        _services.Audio?.PlayAt("placement.commit",
                            BoardMath.CellCenterWorld(_session.Level,
                                new Cell(placed.x, placed.z)) + Vector3.up * .4f);
                        _feedback?.Placement(BoardMath.TentCenter(_session.Level, placed.x, placed.z));
                        _atmosphere?.Soundscape?.GroundContact(BoardMath.TentCenter(_session.Level, placed.x, placed.z));
                        _services.Audio?.PlayAt("sfx.tent.settle",
                            BoardMath.TentCenter(_session.Level, placed.x, placed.z), .65f);
                        _services.PlayHaptic(HapticCue.Confirm);
                    }
                    if (_tutorial.Guiding(_session.Level.id)) GuideFirstIssue(null);
                    break;
                case CampEventKind.SelectionChanged:
                    _renderer.ShowShade(Array.Find(_session.Level.guests, g => g.id == e.GuestId)?.shade == true);
                    if (e.GuestId != null && _renderer.Tents.TryGetValue(e.GuestId, out var selected))
                        selected.Pulse(_services.ReducedMotion, _services.MotionScale);
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
            var guided = _tutorial.Guiding(_session.Level.id);
            _tutorial.ReportAction("complete", _session);
            TentStoryVisual.CloseAll(Find("World")?.transform, _services.ReducedMotion);
            _services.Analytics.EndLevel(ComfortOutcome.Completed);
            // Completion ducks the forest briefly (0.75 / 100 ms / 500 ms /
            // 900 ms) instead of silencing it — ordinary taps never duck.
            _services.Audio?.DuckBus(Kruty1918.Audio.AudioBus.Ambience, .75f, .1f, .5f, .9f);
            PlayAudio("level.complete");
            _services.PlayHaptic(HapticCue.Success);
            _feedback?.Complete(Vector3.zero);
            GetComponent<PhasePostFx>()?.Celebrate();
            StartCoroutine(ChimeAfter(.18f));
            var level = _session.Level;
            _services.Rewards.EarnFromPlay();
            var memory = gameObject.GetComponent<CampMemoryPresenter>() ?? gameObject.AddComponent<CampMemoryPresenter>();
            _hud.BeginStory(memory.Skip);
            memory.Play(Find("World")?.transform, () => _services.ReducedMotion, _hud.EndStory);
            // Completion must not turn a night level back into an evening level.
            _services.Motion.Cancel(this);
            var mvp = LevelLoader.MvpLevelIds();
            if (mvp.Count > 0 && level.id == mvp[mvp.Count - 1])
            {
                // DemoComplete follows the completion panel via qc.next.
            }
            if (guided && _tutorial.Finished) StartCoroutine(TutorialAutoMenu());
        }

        /// <summary>After the farewell card the tutorial hands the player back
        /// to the menu on its own — no hidden tap required. Scene teardown or
        /// any earlier navigation simply cancels this coroutine.</summary>
        System.Collections.IEnumerator TutorialAutoMenu()
        {
            yield return new WaitForSecondsRealtime(8f);
            if (_session != null && _session.IsCompleted && !_router.IsBusy)
                _services.Actions.Execute(new UiActionRequest(
                    new UiActionId("qc.menu"), UiActionSource.Programmatic, "Gameplay"));
        }

        static string GuideMistakeKey(string rawKey)
        {
            var code = rawKey != null && rawKey.StartsWith("rule.") ? rawKey.Substring(5) : rawKey;
            return code == "bounds" || code == "overlap" || code == "path" || code == "shade"
                || code == "quiet" || code == "friends" ? "guide.mistake." + code : "guide.mistake.generic";
        }

        /// <summary>The mentor reacts to real violations only — unmet wishes
        /// (path/shade/quiet/friends), never unplaced guests, which are steps
        /// the player simply has not taken yet.</summary>
        void GuideFirstIssue(string fallback)
        {
            var report = RuleEvaluator.Evaluate(_session.Level, _session.State.Placements, false);
            var issue = report.Issues.Find(i => i.Code == "path" || i.Code == "shade"
                || i.Code == "quiet" || i.Code == "friends");
            _hud.ShowGuideHint(issue != null ? GuideMistakeKey("rule." + issue.Code) : fallback);
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
            if (_completed) return;
            _services.Save.Session = new SessionSaveData
            {
                levelId = _session.Level.id,
                contentHash = _session.Level.contentHash,
                ruleVersion = _session.Level.ruleVersion,
                levelSnapshot = CampContent.Snapshot(_session.Level),
                placements = _session.State.Snapshot(),
                selectedGuestId = _session.SelectedGuestId,
            };
            _services.Save.Save();
        }

        void PlayAudio(string key) => _services.Audio?.Play(key);

        void NudgeCamera()
        {
            var camera = _worldCamera != null ? _worldCamera : Camera.main;
            if (camera != null) _atmosphere?.CameraMotion?.Impulse(.008f, camera.transform.up);
        }

        void OnTutorialAction(ComfortAction action)
        {
            var id = action == ComfortAction.Place ? "commit" : action == ComfortAction.Move ? "move"
                : action == ComfortAction.Rotate ? "rotate" : action == ComfortAction.Undo ? "undo" : null;
            if (id != null && _tutorial.ReportAction(id, _session))
                _services.Analytics.Action(ComfortAction.TutorialStep);
        }

        // ─── Lighting ────────────────────────────────────────────────────────

        void ConfigureAtmosphere(LevelData level)
        {
            using var audit = PerformanceAudit.Measure("QC.CampSceneHost.ConfigureAtmosphere");
            _atmosphereCatalog = AtmosphereCatalog.Load();
            _fireVisuals = FindObjectsByType<FireVisual>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            _atmosphere = gameObject.AddComponent<CampAtmosphere>();
            var viewport = Find("CanvasRoot/SafeArea/Gameplay/BoardViewport")?.transform as RectTransform;
            var profile = _atmosphereCatalog.Resolve(level.id, level.lighting);
            var tier = QualityTier();
            _atmosphere.Configure(FindCamera(), level, viewport, profile,
                () => _services.ReducedMotion, tier, _decorRoot, ()=>_services.EffectiveQuality);
            // No near-leaf flyby while dragging a tent, behind a modal or
            // during a scene transition (atmosphere spec §4.2/§9).
            if (_atmosphere.Particles != null)
                _atmosphere.Particles.NearLeafSuppressed = () =>
                    (_placement != null && _placement.HasPreview)
                    || (_hud != null && _hud.HasModalOpen)
                    || (_router != null && _router.IsBusy);
            _atmosphere.Soundscape.AccentSuppressed = () => !GameplayActive;
            // Voice budget by quality tier (Low/Balanced/High → 8/12/16);
            // decorative one-shots are skipped first when the pool is full.
            if (_services.Audio != null)
                _services.Audio.MaxActiveVoices = tier == 0 ? 8 : tier == 1 ? 12 : 16;
            SetAtmospherePhase(profile.Id);
        }

        /// <summary>0–2 = Low/Balanced/High. settings.quality 0 = auto from
        /// the active QualitySettings level; 1–3 is a manual override.</summary>
        int QualityTier() => _services.EffectiveQuality;

        /// <summary>One presentation authority for light, scenery, fire and phase-dependent audio.</summary>
        public void SetAtmospherePhase(string phase)
        {
            var profile = _atmosphereCatalog.Get(phase);
            if (_atmosphereProfile == profile) return;
            _atmosphereProfile = profile;
            var lighting = Find("LightingRoot");
            var dir = lighting != null ? lighting.transform.Find("DirectionalLight") : null;
            var fire = lighting != null ? lighting.transform.Find("FireLight") : null;
            if (dir != null)
            {
                var light = dir.GetComponent<Light>();
                if (light != null)
                {
                    dir.localEulerAngles = new Vector3(profile.Elevation, 65f, 0f);
                    RenderSettings.sun=light;
                    light.intensity = profile.SunIntensity;
                    light.color = profile.Sun;
                }
            }
            // Capture the new sun color/direction after the light is updated.
            _atmosphere.Apply(profile);
            bool hasFire = _fireVisuals.Length > 0 && !(_atmosphere.RainShelter?.Extinguished??false);
            // Each pit owns one adaptive local light. The old scene rig light
            // would duplicate it and does not decide whether a daytime fire burns.
            if (fire != null) fire.gameObject.SetActive(false);
            foreach (var fv in _fireVisuals) if (fv != null) fv.SetBurning(hasFire);
            _atmosphere.SetFire(_firePos, hasFire);
        }

        // ─── Frame loop ──────────────────────────────────────────────────────

        void LateUpdate() => _hud?.TickAnchors();

        void Update()
        {
            if (_session != null && !_completed && _services.Analytics.Collecting && !_services.Analytics.HasActiveAttempt)
            {
                var summary = CampContent.Summary(_session.Level.id);
                if (summary != null) _services.Analytics.BeginLevel(summary.number, _session.Level.width, _session.Level.height,
                    _session.Level.lighting == "night" ? 3 : _session.Level.lighting == "evening" ? 2 : _session.Level.lighting == "morning" ? 0 : 1);
            }
            _renderer?.ConfigureMotion(_services?.ReducedMotion ?? true, _services?.MotionScale ?? 1f);
            _hud?.Tick();
            if (_services?.Audio != null) _services.Audio.MaxActiveVoices = _services.EffectiveQuality == 0 ? 8 : _services.EffectiveQuality == 1 ? 12 : 16;
            _twigCooldown -= Time.deltaTime;
            if (_services?.Audio != null)
            {
                _musicScale = Mathf.MoveTowards(_musicScale, (_hud?.HasModalOpen ?? false) ? .65f : 1f, Time.unscaledDeltaTime * 1.5f);
                _services.Audio.SetPlaybackScale(_services.MusicBedHandle, _musicScale);
            }
        }

        // ─── Board overlays + HUD anchors ────────────────────────────────────

        /// <summary>World anchor above the guest's tent (chip baseline).</summary>
        Vector3? GuestWorldAnchor(string guestId)
        {
            if (guestId == null || !_renderer.Tents.TryGetValue(guestId, out var tent)
                || tent?.Root == null) return null;
            return tent.Root.transform.position;
        }

        /// <summary>World point → CSS px inside the HUD surface (top-left origin).</summary>
        Vector2? ProjectToHud(Vector3 world)
        {
            if (_worldCamera == null) _worldCamera = FindCamera();
            var camera = _worldCamera;
            var rect = _hud?.SurfaceRect;
            if (camera == null || rect == null) return null;
            var screen = camera.WorldToScreenPoint(world);
            if (screen.z < 0f) return null;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, screen, null, out var local))
                return null;
            return new Vector2(rect.rect.width * 0.5f + local.x, rect.rect.height * 0.5f - local.y);
        }

        void OnDestroy()
        {
            _services?.Audio?.SetPlaybackScale(_services.MusicBedHandle, 1);
            if (!_completed) _services?.Analytics?.EndLevel(ComfortOutcome.Left);
            if (_session != null)
            {
                _session.Evented -= OnSessionEvent;
                _session.ActionRecorded -= OnTutorialAction;
            }
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
