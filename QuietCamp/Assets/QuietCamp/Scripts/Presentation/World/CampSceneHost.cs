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
        bool _checkingCamp;
        public bool IsFinishingCamp => _checkingCamp || (_hud?.IsFinishing ?? false);
        public bool IsIntroducingShade => _hud?.IsShadeIntroduction ?? false;
        public bool IsIntroducingPlacement => _hud?.IsPlacementIntroduction ?? false;
        public bool IsIntroducingPassage => _hud?.IsPassageIntroduction ?? false;
        public bool IsIntroducingQuiet => _hud?.IsQuietIntroduction ?? false;
        public bool IntroductionWaiting { get; private set; }
        public bool CanContinueIntroduction { get; private set; }
        public int? PlacementDemonstrationStep => IsIntroducingPlacement ? _hud.PlacementDemonstrationStep : (int?)null;
        float _twigCooldown;
        float _musicScale = 1;
        Vector3 _firePos;

        public static CampSceneHost Current { get; private set; }
        public CampGroundDetails SurfaceDetails=>_renderer?.SurfaceDetails;

        /// <summary>Set at the end of Start — the explicit readiness signal
        /// the ScreenRouter waits for before revealing (level loaded, world
        /// built, HUD applied, camera fitted, phase applied).</summary>
        public bool IsReady { get; private set; }

        public CampSession Session => _session;
        public CampAtmosphere Atmosphere => _atmosphere;
        public bool GameplayActive => IsReady && !_completed && !IsFinishingCamp && !IsIntroducingShade && !IsIntroducingPlacement && !IsIntroducingPassage && !IsIntroducingQuiet && !(_hud?.HasModalOpen ?? false) && !(_router?.IsBusy ?? false) && !_services.MonetizationBusy;
        public bool UiReady => IsReady && (_atmosphere?.InitialWorldReady ?? true) && (_hud?.UiReady ?? false);

        public void Configure(GameServices services, ScreenRouter router)
        {
            _services = services;
            _router = router;
        }

        void Start() { Current = this; StartCoroutine(ComposeScene()); }
        System.Collections.IEnumerator ComposeScene()
        {
            LevelData level = null;
            using (PerformanceAudit.Measure("QC.CampSceneHost.Start"))
            {
                var levelId = _services.PendingLevelId ?? "QC_TEST";
                if (!_services.CanStart(levelId))
                    _services.Notifications.Show(_services.Localization.T("journey.access.denied"), GameplayNotificationKind.Info);
                else
                {
                    try { level = CampContent.SessionLevel(_services.Save.Session, levelId); }
                    catch (Exception error)
                    { Debug.LogError($"[QuietCamp] {error.Message}"); _services.Notifications.Show("save.failed", GameplayNotificationKind.Error); }
                }
            }
            if (level == null) { IsReady = true; StartCoroutine(ReturnToMenu()); yield break; }
            BuildSession(level); yield return null;
            BuildWorld(level); yield return null;
            BuildHud(level); yield return null;
            Canvas.ForceUpdateCanvases();
            var htmlViewport = Find("CanvasRoot/SafeArea/Gameplay/BoardViewport");
            CameraFitter.Fit(FindCamera(), level, htmlViewport != null ? htmlViewport.transform as RectTransform : null);
            RegisterActions(); yield return null;
            ConfigureAtmosphere(level); yield return null;
            _renderer.BuildCanopies();
            _atmosphere.RegisterDecor(Find("World")?.transform);
            _atmosphere.BindRainWorld(Find("World")?.transform,_renderer);
            _router?.Dive.WarmSceneUnderCover();
            _services.PendingLevelId = null;
            Placement demo=null;
            if(_tutorial.NeedsPlacementDemonstration(_session))
            {
                var guest=_session.Level.guests[0];float distance=float.PositiveInfinity;
                foreach(var site in PlacementGuidance.Sites(_session,guest.id,0))
                {
                    if(site.Status!=PlacementGuidance.Fit.Ready)continue;
                    var centre=BoardMath.TentCenter(level,site.X,site.Z);
                    if(centre.sqrMagnitude>=distance)continue;
                    distance=centre.sqrMagnitude;demo=new Placement{guestId=guest.id,x=site.X,z=site.Z,rotation=0};
                }
            }
            if(demo!=null)
            {
                var target=BoardMath.TentCenter(level,demo.x,demo.z);
                _hud.BeginPlacementIntroduction(target+new Vector3(1.15f,0,-.95f),target);
            }
            else if(_tutorial.NeedsShadeDemonstration(_session))_hud.BeginShadeIntroduction();
            else TryIntroduceQuiet();
            IsReady = true;
            if (_tutorial.Guiding(_session.Level.id)) _tutorial.BeginIntroduction();
            if(IsIntroducingShade)StartCoroutine(IntroduceShade());
            if(IsIntroducingPlacement)StartCoroutine(IntroducePlacement(demo));
        }

        System.Collections.IEnumerator IntroducePlacement(Placement example)
        {
            while(!UiReady||(_router?.IsBusy??false))yield return null;
            try
            {
                _renderer.ShowHintCells(RuleEvaluator.Footprint(example));
                _hud.SetPlacementDemonstration(0);
                float elapsed=0;
                while(elapsed<5.5f)
                {
                    elapsed+=Time.unscaledDeltaTime;
                    _hud.SetPlacementDemonstration(elapsed/5.5f);
                    _hud.SetIntroductionOpacity(Mathf.Clamp01(elapsed/.3f));
                    yield return null;
                }
                yield return WaitForIntroductionTap();
                elapsed=0;
                while(elapsed<.3f)
                {
                    elapsed+=Time.unscaledDeltaTime;
                    _hud.SetIntroductionOpacity(1-Mathf.Clamp01(elapsed/.3f));
                    yield return null;
                }
                _tutorial.MarkPlacementDemonstrationSeen();
            }
            finally
            {
                _renderer?.HideHint();
                _hud?.EndIntroduction();
                _hud?.SetTutorial(_tutorial.Cue(_session));
            }
        }

        System.Collections.IEnumerator IntroduceShade()
        {
            // Do not spend the lesson underneath the scene-transition cover or during cold art loading.
            while(!UiReady||(_router?.IsBusy??false))yield return null;
            try
            {
                _renderer.ShowShade(true);_renderer.SetShadeIntroductionStrength(0);
                yield return AnimateShadeIntroduction(_services.ReducedMotion ? .08f : .65f,0,1,0,0);
                _hud.ShowShadeExplanation();
                yield return AnimateShadeIntroduction(_services.ReducedMotion ? .08f : .25f,1,1,0,1);
                yield return WaitForIntroductionTap();
                yield return AnimateShadeIntroduction(_services.ReducedMotion ? .08f : .35f,1,0,1,0);
                _tutorial.ExplainSign("shade");
            }
            finally
            {
                _renderer?.ShowShade(false);
                _hud?.EndIntroduction();
                _hud?.SetTutorial(_tutorial.Cue(_session));
            }
            if(IsReady)TryIntroduceQuiet();
        }

        void TryIntroduceQuiet()
        {
            if(!_tutorial.NeedsQuietDemonstration(_session)||_hud==null||IsIntroducingQuiet)return;
            var fire=_session.Level.noise[0];
            _hud.BeginQuietIntroduction(BoardMath.CellCenterWorld(_session.Level,new Cell(fire[0],fire[1])));
            StartCoroutine(IntroduceQuiet());
        }
        System.Collections.IEnumerator IntroduceQuiet()
        {
            while(!UiReady||(_router?.IsBusy??false))yield return null;
            // Rain can quench the pit during cold scene loading. Do not teach
            // an absent restriction or mark the burning-fire lesson as seen.
            if(_session.Level.CampfireNoiseSuppressed)
            {
                _hud.EndIntroduction();_hud.SetTutorial(_tutorial.Cue(_session));yield break;
            }
            try
            {
                _renderer.ShowQuietIntroduction(true);
                _hud.ShowShadeExplanation();
                float elapsed=0,duration=_services.ReducedMotion ? .08f : .65f;
                while(elapsed<duration)
                {
                    elapsed+=Time.unscaledDeltaTime;float strength=Mathf.Clamp01(elapsed/duration);
                    _renderer.SetQuietIntroductionStrength(strength);_hud.SetIntroductionOpacity(strength);
                    yield return null;
                }
                yield return WaitForIntroductionTap();
                elapsed=0;duration=_services.ReducedMotion ? .08f : .35f;
                while(elapsed<duration)
                {
                    elapsed+=Time.unscaledDeltaTime;float strength=1-Mathf.Clamp01(elapsed/duration);
                    _renderer.SetQuietIntroductionStrength(strength);_hud.SetIntroductionOpacity(strength);
                    yield return null;
                }
                _tutorial.ExplainSign("quiet");
            }
            finally
            {
                _renderer?.ShowQuietIntroduction(false);_hud?.EndIntroduction();
                _hud?.SetTutorial(_tutorial.Cue(_session));
            }
        }

        void TryIntroducePassage()
        {
            if(!_tutorial.NeedsPassageDemonstration(_session)||IsIntroducingPassage||IsFinishingCamp)return;
            var route=TutorialDirector.PassageIntroductionRoute(_session);
            if(route==null)return;
            _hud.BeginPassageIntroduction(BoardMath.CellCenterWorld(_session.Level,route.Goal),route.Reachable);
            StartCoroutine(IntroducePassage(route));
        }
        System.Collections.IEnumerator IntroducePassage(RouteEvidence route)
        {
            // Let the real placement release its capture and finish removing its preview first.
            yield return null;
            while(!UiReady||(_router?.IsBusy??false))yield return null;
            try
            {
                _renderer.ShowRoute(route);_renderer.ShowHintCells(new[]{route.Start,route.Goal});
                float reveal=_services.ReducedMotion ? .08f : .7f;
                float enter=_services.ReducedMotion ? .08f : .25f;
                float exit=_services.ReducedMotion ? .08f : .35f;
                float total=reveal+enter,elapsed=0;bool speaking=false;
                while(elapsed<total)
                {
                    elapsed+=Time.unscaledDeltaTime;
                    if(elapsed>=reveal&&!speaking){speaking=true;_hud.ShowShadeExplanation();}
                    _renderer.SetPassageIntroductionStrength(route,1,_services.ReducedMotion?1:Mathf.Clamp01(elapsed/reveal));
                    _hud.SetIntroductionOpacity(Mathf.Clamp01((elapsed-reveal)/enter));
                    yield return null;
                }
                yield return WaitForIntroductionTap();
                elapsed=0;
                while(elapsed<exit)
                {
                    elapsed+=Time.unscaledDeltaTime;
                    float alpha=1-Mathf.Clamp01(elapsed/exit);
                    _renderer.SetPassageIntroductionStrength(route,alpha,1);_hud.SetIntroductionOpacity(alpha);
                    yield return null;
                }
                _tutorial.MarkPassageDemonstrationSeen();
            }
            finally
            {
                _renderer?.HidePath();_renderer?.HideHint();
                _hud?.EndIntroduction();_hud?.SetTutorial(_tutorial.Cue(_session));
            }
        }

        System.Collections.IEnumerator WaitForIntroductionTap()
        {
            var gate=new TutorialTapGate(Time.unscaledTimeAsDouble,12*PlacementTargetProjector.ScreenDpScale());
            IntroductionWaiting=true;CanContinueIntroduction=false;
            _hud.ShowIntroductionContinue(false);
            try
            {
                bool accepted=false;
                while(!accepted)
                {
                    if(!CanContinueIntroduction&&gate.Ready(Time.unscaledTimeAsDouble))
                    {CanContinueIntroduction=true;_hud.ShowIntroductionContinue(true);}
                    var screen=UnityEngine.InputSystem.Touchscreen.current;
                    var touch=screen?.primaryTouch;
                    bool touchActive=touch!=null&&(touch.press.isPressed||touch.press.wasReleasedThisFrame);
                    int fingers=0;
                    if(screen!=null)foreach(var finger in screen.touches)if(finger.press.isPressed)fingers++;
                    if(fingers>1)gate.Cancel();
                    else
                    {
                        var mouse=UnityEngine.InputSystem.Mouse.current;
                        bool mouseActive=!touchActive&&mouse!=null&&mouse.enabled;
                        if(touchActive||mouseActive)
                        {
                            var position=touchActive?touch.position.ReadValue():mouse.position.ReadValue();
                            int id=touchActive?touch.touchId.ReadValue():-1;
                            bool began=touchActive?touch.press.wasPressedThisFrame:mouse.leftButton.wasPressedThisFrame;
                            bool ended=touchActive?touch.press.wasReleasedThisFrame:mouse.leftButton.wasReleasedThisFrame;
                            bool canceled=touchActive&&touch.phase.ReadValue()==UnityEngine.InputSystem.TouchPhase.Canceled;
                            if(canceled||!new Rect(0,0,Screen.width,Screen.height).Contains(position))gate.Cancel();
                            else
                            {
                                if(began)gate.Press(Time.unscaledTimeAsDouble,id,position.x,position.y);
                                gate.Move(id,position.x,position.y);
                                if(ended)accepted=gate.Release(Time.unscaledTimeAsDouble,id,position.x,position.y);
                            }
                        }
                    }
                    yield return null;
                }
                // Consume the release while world controls are still blocked.
                yield return null;
            }
            finally{IntroductionWaiting=false;CanContinueIntroduction=false;_hud?.HideIntroductionContinue();}
        }

        System.Collections.IEnumerator AnimateShadeIntroduction(float duration,float shadeFrom,float shadeTo,float guideFrom,float guideTo)
        {
            float time=0;
            while(time<duration)
            {
                time+=Mathf.Min(.05f,Time.unscaledDeltaTime);
                float t=Mathf.SmoothStep(0,1,Mathf.Clamp01(time/duration));
                _renderer.SetShadeIntroductionStrength(Mathf.Lerp(shadeFrom,shadeTo,t));
                _hud.SetIntroductionOpacity(Mathf.Lerp(guideFrom,guideTo,t));
                yield return null;
            }
        }

        /// <summary>Cancels an in-flight placement drag without committing —
        /// used when a scene transition gates input mid-gesture.</summary>
        public void CancelActiveDrag() => _placement?.Cancel();

        // ─── Build ───────────────────────────────────────────────────────────

        System.Collections.IEnumerator ReturnToMenu()
        {
            while (_router.IsBusy) yield return null;
            _services.PendingLevelId = null; _services.PendingMenuScreen = null;
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
            var art = CampArtDefinition.Load(level);
            using var audit = PerformanceAudit.Measure("QC.CampSceneHost.BuildWorld");
            var world = Find("World");
            var boardRoot = world != null ? world.transform.Find("BoardRoot") : null;
            Transform Child(string n) => boardRoot != null ? boardRoot.Find(n) : null;

            _renderer = new BoardRenderer(level, _services.Assets,
                Child("Base"), Child("Grid"), Child("Obstacle"),
                Child("Tent"), Child("Overlay"), art);
            _renderer.SyncPlacements(_session.State.Placements, level, _services.MotionScale, _services.ReducedMotion);

            _decorRoot = Find("DecorRoot")?.transform;
            if (_decorRoot != null)
            {
                for (var i = _decorRoot.childCount - 1; i >= 0; i--)
                    Destroy(_decorRoot.GetChild(i).gameObject);
                if (art == null) DecorSpawner.Spawn(level, _services.Assets, _decorRoot);
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
            if (art != null) camera.gameObject.AddComponent<CampArtView>().Configure(art, level, _decorRoot, _services.Assets);
            // Art construction also supports its own Editor camera. The game
            // uses the board-centred camera contract from 2026-10-10.
            CameraFitter.Configure(camera);
            CameraFitter.Fit(camera, level, viewport);
            _renderer.BindViewport(camera.GetComponent<Kruty1918.GameplayViewport.GameplayViewport>());
            _renderer.SurfaceDetails.Bind(_decorRoot,paused:()=>!GameplayActive,reduced:()=>_services.ReducedMotion);

            // Stable listener proxy above the board centre — positional
            // sources read distance against the field, not the far camera.
            var camListener = camera.GetComponent<AudioListener>();
            if (camListener != null) { camListener.enabled = false; Destroy(camListener); }
            var proxy = new GameObject("ListenerProxy");
            proxy.transform.SetParent(transform,false);
            proxy.transform.SetPositionAndRotation(
                new Vector3(0f, 2f * BoardMath.CellSize, 0f), camera.transform.rotation);
            proxy.AddComponent<AudioListener>();

            var inputGo = new GameObject("PlacementController");
            inputGo.transform.SetParent(transform,false);
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
                    _hud.ShowGuideHint(key);
            };
            _placement.PlacementCommitted += PersistSession;
            _placement.PlacementCommitted += TryIntroducePassage;
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
            _hud.ShadeIntroductionAnchor = _renderer.ShadeIntroductionAnchor;
            _placement.PreviewChanged += _hud.SetPlacementPreview;
            _placement.TentInspected += _hud.InspectTent;
            if (_session.SelectedGuestId == null && level.guests.Length > 0)
                _session.Select(level.guests[0].id);
            _hud.GuestSelected += _ => _tutorial.ReportAction("select", _session);
            _hud.SetTutorial(_tutorial.Cue(_session));
            // Act framing: the first unplayed glade of a new district opens
            // with the guide narrating what this region is — once ever.
            var district = LevelLoader.DistrictFor(level.id);
            if (!LevelLoader.Campaign100Enabled() && district != null && !_services.Progression.IsCompleted(level.id)
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
                if (sign != null && sign!="shade" && sign!="quiet")
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
                if (IsIntroducingQuiet || IsIntroducingPassage || IsIntroducingPlacement || IsIntroducingShade || IsFinishingCamp || _session.IsCompleted || _services.MonetizationBusy || !_tutorial.CanCompleteLevel(_session.Level.id)) return Performed();
                _placement.Cancel();
                var learning=_tutorial.Guiding(_session.Level.id);
                if (!learning&&!_services.Economy.CanCheck) { _hud.ShowEconomy(); return Performed(); }
                _checkingCamp=true;
                _hud.BeginFinishing();
                var report = RuleEvaluator.Evaluate(_session.Level, _session.State.Placements, true);
                try
                {
                    var outcome = _services.Attempts.Check(_session, _atmosphere?.PhaseId, learning);
                    _services.Analytics.RuleReport(report);
                    if(outcome!=CampAttemptResult.Completed)StartCoroutine(ResumeAfterCheck(outcome,report,learning));
                }
                catch(Exception exception)
                {
                    Debug.LogException(exception);
                    if(!_session.IsCompleted)StartCoroutine(ResumeAfterCheck(CampAttemptResult.SaveFailed,report,learning));
                }
                return Performed();
            }));
            // Concrete hint: advisor picks guest + pose → green cells + ghost
            // tent on the board, plus a short toast line.
            _leases.Add(h.Register(new UiActionId("qc.hint"), () =>
            {
                if (_session.IsCompleted || _services.MonetizationBusy) return Performed();
                if(_services.Economy.RecoveryHintPending(_session.Level.id)){ShowRecoveryHint();return Performed();}
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
                _services.PendingMenuScreen=null;
                _router.GoToMenu();
                return Performed();
            }
            _leases.Add(h.Register(new UiActionId("qc.menu"), () =>
            {
                _services.PendingMenuScreen = null;
                return LeaveCamp();
            }));
            _leases.Add(h.Register(new UiActionId("qc.next"), () =>
            {
                if (!_session.IsCompleted || _advancing || _router.IsBusy || _services.MonetizationBusy) return Performed();
                _advancing = true;
                _services.Save.Save();
                _services.PendingMenuScreen=null;
                var next = _services.NextLevel(_session.Level.id);
                if (next != null) _router.GoToNextCamp(next);
                else _router.GoToMenu();
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
                    break;
                case CampEventKind.SelectionChanged:
                    if(!IsIntroducingShade&&!IsIntroducingQuiet&&!_placement.HasPreview)_renderer.ShowShade(false);
                    if (e.GuestId != null && _renderer.Tents.TryGetValue(e.GuestId, out var selected))
                        selected.Pulse(_services.ReducedMotion, _services.MotionScale);
                    break;
                case CampEventKind.LevelCompleted:
                    OnLevelCompleted();
                    break;
            }
        }

        System.Collections.IEnumerator ResumeAfterCheck(CampAttemptResult outcome,RuleReport report,bool learning)
        {
            while(!_hud.FinishExitComplete)yield return null;
            _hud.ResumeAfterFinishing();
            while(_hud.IsFinishing)yield return null;
            _checkingCamp=false;
            if(outcome==CampAttemptResult.SaveFailed)
            {
                _services.Notifications.Show(_services.Localization.T("save.failed"),GameplayNotificationKind.Error);
                yield break;
            }
            if(outcome==CampAttemptResult.FailedAttempt)
            {
                _services.Analytics.Action(ComfortAction.Check);
                _advisor=new HintAdvisor(_session.Level);
                if(report.Issues.Count>0)_services.Notifications.Show(_services.Localization.T("rule."+report.Issues[0].Code),GameplayNotificationKind.Warning,dedupKey:"attempt.failed");
                PlayAudio("rule.invalid"); _services.PlayHaptic(HapticCue.Warning);
                if(_services.Economy.RecoveryHintPending(_session.Level.id))ShowRecoveryHint();
                else if(!learning&&!_services.Economy.CanCheck)_hud.ShowEconomy();
            }
            else if(outcome==CampAttemptResult.NoLives)_hud.ShowEconomy();
        }

        void OnLevelCompleted()
        {
            if (_completed) return;
            _completed = true;
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
            StartCoroutine(AdvanceAfterCompletion());
        }

        /// <summary>Advance after the settled closing animation. Scene teardown cancels the pending transition.</summary>
        System.Collections.IEnumerator AdvanceAfterCompletion()
        {
#if UNITY_EDITOR
            // A capture has a deliberately frozen simulation schedule and advances explicitly.
            if(EditorAuthoringPreview.ControlledCapture)yield break;
#endif
            yield return new WaitForSecondsRealtime(1.6f);
            while(_session!=null&&_session.IsCompleted&&(_router.IsBusy||_services.MonetizationBusy))yield return null;
            if (_session != null && _session.IsCompleted && !_router.IsBusy)
                _services.Actions.Execute(new UiActionRequest(
                    new UiActionId("qc.next"), UiActionSource.Programmatic, "Gameplay"));
        }

        void ShowRecoveryHint()
        {
            var s=_advisor.Suggest(_session.State.Placements,_session.SelectedGuestId);
            if(s?.Move==null)return;
            if(_services.Economy.UseRecoveryHint(_session.Level.id)!=EconomyResult.Applied)
            {_services.Notifications.Show(_services.Localization.T("save.failed"),GameplayNotificationKind.Error);return;}
            _session.Select(s.GuestId);
            _renderer.ShowHintCells(s.Cells);
            var guest=Array.Find(_session.Level.guests,g=>g.id==s.GuestId);
            _renderer.ShowHintGhost(s.Move,guest?.assetId);
            PlayAudio("sfx.hint");
            _hud.ShowGuideHint("guide.recovery",6);
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
            if(_atmosphere.SeasonalWeather!=null)_atmosphere.SeasonalWeather.AudioAnchor=transform.Find("ListenerProxy");
            // No near-leaf flyby while dragging a tent, behind a modal or
            // during a scene transition (atmosphere spec §4.2/§9).
            if (_atmosphere.Particles != null)
                _atmosphere.Particles.NearLeafSuppressed = () =>
                    (_placement != null && _placement.HasPreview)
                    || (_hud != null && _hud.HasModalOpen)
                    || (_router != null && _router.IsBusy);
            _renderer.SurfaceDetails.Bind(_decorRoot,_atmosphere,()=>!GameplayActive,()=>_services.ReducedMotion);
            if(_atmosphere.Soundscape!=null)_atmosphere.Soundscape.AccentSuppressed = () => !GameplayActive;
            // Voice budget by quality tier (Low/Balanced/High → 8/12/16);
            // decorative one-shots are skipped first when the pool is full.
            if (_services.Audio != null)
                _services.Audio.MaxActiveVoices = tier == 0 ? 8 : tier == 1 ? 12 : 16;
            SetAtmospherePhase(_atmosphere.PhaseId??profile.Id);
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
                    var authoredElevation=_session?.Level?.environment?.sunElevation??-1;
                    dir.localEulerAngles = new Vector3(authoredElevation>=0?authoredElevation:profile.Elevation, 65f, 0f);
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
            if(IsReady&&_session!=null&&_atmosphere!=null&&_fireVisuals!=null)
            {
                bool burning=_fireVisuals.Length>0&&!(_atmosphere.RainShelter?.Extinguished??false);
                if(_session.SetCampfireBurning(burning))
                {
                    _advisor=new HintAdvisor(_session.Level);
                    if(IsIntroducingQuiet)_renderer.ShowQuietIntroduction(true);
                }
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

        GameObject Find(string path)
        {
            var scene = gameObject.scene;
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

        Camera FindCamera()
        {
            var go = Find("MainCamera");
            var camera = go != null ? go.GetComponent<Camera>() : null;
            if(camera!=null)return camera;
            foreach(var root in gameObject.scene.GetRootGameObjects())
            {camera=root.GetComponentInChildren<Camera>();if(camera!=null)return camera;}
            return null;
        }
    }
}
