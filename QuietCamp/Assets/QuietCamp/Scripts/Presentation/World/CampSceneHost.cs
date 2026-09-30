using System;
using System.Collections.Generic;
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
        CampHud _hud;
        HintService _hint;
        TutorialDirector _tutorial;
        LocalizedLabel _tutorialLabel;
        readonly List<System.IDisposable> _leases = new List<System.IDisposable>();
        IDisposable _gameplayContext;
        float _birdTimer = 30f;
        bool _completed;

        public static CampSceneHost Current { get; private set; }

        public CampSession Session => _session;

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
            ApplyLighting(level.lighting == "evening");
            _services.PendingLevelId = null;
        }

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

            var decorRoot = world != null ? world.transform.Find("DecorRoot") : null;
            if (decorRoot != null)
            {
                for (var i = decorRoot.childCount - 1; i >= 0; i--)
                    Destroy(decorRoot.GetChild(i).gameObject);
                DecorSpawner.Spawn(level, _services.Assets, decorRoot);
            }

            var camera = FindCamera();
            var viewportGo = Find("CanvasRoot/SafeArea/Gameplay/BoardViewport");
            var viewport = viewportGo != null ? viewportGo.transform as RectTransform : null;
            CameraFitter.Configure(camera);
            CameraFitter.Fit(camera, level, viewport);

            var inputGo = new GameObject("PlacementController");
            _placement = inputGo.AddComponent<PlacementController>();
            _placement.Configure(_session, _renderer, _services.InputPolicy, camera,
                _services.Assets, () => _services.ReducedMotion);
            _placement.PlacementFailed += key
                => _services.Notifications.Show(_services.Localization.T(key),
                    GameplayNotificationKind.Warning, dedupKey: key + _session.SelectedGuestId);
            _placement.PlacementCommitted += () =>
            {
                PlayAudio("placement.commit");
                PersistSession();
            };
            _placement.Cancelled += () => PlayAudio("ui.back");
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
                if (_hud.HasModalOpen) { _hud.CloseAllModals(); return Performed(); }
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
                _placement.Remove();
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
            _leases.Add(h.Register(new UiActionId("qc.settings"), () => { _hud.ShowPause(); return Performed(); }));
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
                if (next != null) _router.GoToCamp(next);
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
                _session.Select(req.Payload as string ?? req.TargetId);
                PlayAudio("ui.select");
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
                    _renderer.SyncPlacements(_session.State.Placements, _session.Level);
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
            PlayAudio("level.complete");
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
            ApplyLighting(evening: true);
            _services.Motion.Cancel(this);
            var mvp = LevelLoader.MvpLevelIds();
            if (mvp.Count > 0 && level.id == mvp[mvp.Count - 1])
            {
                // DemoComplete follows the completion panel via qc.next.
            }
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

        void ApplyLighting(bool evening)
        {
            var lighting = Find("LightingRoot");
            var dir = lighting != null ? lighting.transform.Find("DirectionalLight") : null;
            var fire = lighting != null ? lighting.transform.Find("FireLight") : null;
            if (dir != null)
            {
                var light = dir.GetComponent<Light>();
                if (light != null)
                {
                    if (evening)
                    {
                        dir.localEulerAngles = new Vector3(18f, -35f, 0f);
                        light.intensity = 0.40f;
                        light.color = new Color(1f, 0.824f, 0.608f);
                    }
                    else
                    {
                        dir.localEulerAngles = new Vector3(50f, -35f, 0f);
                        light.intensity = 1.05f;
                        light.color = new Color(1f, 0.941f, 0.839f);
                    }
                }
            }
            RenderSettings.ambientLight = evening
                ? new Color(0.451f, 0.561f, 0.608f)
                : new Color(0.725f, 0.788f, 0.796f);
            if (fire != null) fire.gameObject.SetActive(evening);
        }

        // ─── Frame loop ──────────────────────────────────────────────────────

        void Update()
        {
            _hud?.PumpHint();
            _birdTimer -= Time.deltaTime;
            if (_birdTimer <= 0f)
            {
                _birdTimer = 25f + UnityEngine.Random.value * 20f;
                PlayAudio("ambience.bird");
            }
        }

        void ShowAreaOverlay(Cell[] cells) { /* area overlay uses existing path/chip visuals */ }
        void ShowMoveOverlay(Placement move) { /* ghost flash handled by HUD text */ }

        void OnDestroy()
        {
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
