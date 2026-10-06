using Kruty1918.InputRouting.API;
using QuietCamp.Application;
using QuietCamp.Domain;
using UnityEngine;
using UnityEngine.InputSystem;
namespace QuietCamp.Presentation.World
{
    /// <summary>
    /// Touch/mouse placement input for the camp board. Pointer rays hit the
    /// mathematical plane Y=0 for placement and TentSelectable colliders for
    /// picking. One pointer owns a drag; UI overlays block world input through
    /// the shared input policy. Commits go through CampSession only.
    /// </summary>
    public sealed class PlacementController : MonoBehaviour
    {
        enum Phase { Idle, Pressed, Preview, Dragging }

        CampSession _session;
        BoardRenderer _renderer;
        IGameplayInputPolicy _policy;
        Camera _camera;
        LevelData _level;
        Infrastructure.AssetCatalog _assets;
        System.Func<bool> _reducedMotion;
        System.Func<float> _motionScale;
        Plane _plane = new Plane(Vector3.up, Vector3.zero);

        Phase _phase = Phase.Idle;
        int _pointerId = -1;
        Vector2 _pressScreen;
        bool _captured;
        Vector3 _lastDragWorld;
        bool _hasDragWorld;
        public event System.Action<Vector3,float> DragMotion;

        Placement _preview;          // candidate pose being previewed
        Cell _grabOffset;            // cell delta grabbed inside the footprint
        Placement _movedOriginal;    // original placement while moving
        GameObject _ghost;
        float _invalidToastCooldown;
        bool _cardDrag;
        TentDragCard _dragCard;
        int _rotation;
        PlacementTargetProjector _projector;
        PlacementTarget _target;
        Vector2 _grabWorld;
        float _pressTime;
        bool _targetValid;
        TentDragVisual _dragVisual;
        public bool IsPlacementActive => _captured || _phase != Phase.Idle;
        public float TouchLiftPixels => _projector?.LiftPixels ?? 0f;
        public Vector2 AimScreen => _target.AimScreen;

        public event System.Action<RuleReport> PreviewChanged;
        public event System.Action PlacementMoved;

        public event System.Action<string> PlacementFailed;
        public event System.Action PlacementCommitted;
        public event System.Action Cancelled;
        /// <summary>Raised when a committed tent is picked up to move it.</summary>
        public event System.Action<string> TentGrabbed;
        /// <summary>Raised when pressing the board starts a fresh preview for
        /// the selected guest — payload is the world point under the pointer.</summary>
        public event System.Action<Vector3> PreviewBegan;

        /// <summary>True while a ghost preview is on screen (press/drag/cancel pending).</summary>
        public bool HasPreview => _preview != null;

        public void Configure(CampSession session, BoardRenderer renderer,
            IGameplayInputPolicy policy, Camera camera,
            Infrastructure.AssetCatalog assets, System.Func<bool> reducedMotion,
            System.Func<float> motionScale = null)
        {
            _session = session;
            _renderer = renderer;
            _policy = policy;
            _camera = camera;
            _assets = assets;
            _reducedMotion = reducedMotion;
            _motionScale = motionScale;
            _level = session.Level;
            _projector = new PlacementTargetProjector(camera, _level);
            session.Evented += OnSessionEvent;
        }

        void OnDestroy()
        {
            if (_session != null) _session.Evented -= OnSessionEvent;
            EndCapture();
            if (_ghost != null) Destroy(_ghost);
        }

        void OnSessionEvent(CampEvent e)
        {
            if (e.Kind == CampEventKind.SelectionChanged)
            {
                if (_phase != Phase.Idle) { EndPreview(); ReleasePointer(); }
                _rotation = _session.State.Find(e.GuestId)?.rotation ?? 0;
            }
            else if (e.Kind == CampEventKind.BoardCommitted && _phase != Phase.Idle)
            {
                EndPreview();
                ReleasePointer();
            }
        }

        void Update()
        {
            if (_captured && !_projector.LayoutUnchanged) { Cancel(); return; }
            var sample = ReadPointer();
            if (!sample.Has)
            {
                if (_captured) Cancel();
                return;
            }
            if (sample.Canceled) { if (sample.Id == _pointerId) Cancel(); return; }

            if (sample.Began) OnPress(sample);
            if (_pointerId != -1 && sample.Id != _pointerId) return;

            // A global input block (scene transition) cancels an in-flight
            // drag without committing — the ghost vanishes, no placement.
            if (_captured && _policy != null
                && !_policy.CanProcess(GameplayInputKind.Placement, sample.Position, _pointerId))
            {
                Cancel();
                return;
            }

            if (sample.Active && _phase == Phase.Pressed)
            {
                var threshold = BoardMath.DragThresholdDp * _projector.DensityScale;
                if ((sample.Position - _pressScreen).sqrMagnitude >= threshold * threshold
                    || (sample.Id > 0 && Time.unscaledTime - _pressTime >= .2f))
                {
                    BeginPreview(_movedOriginal.x, _movedOriginal.z, _movedOriginal.rotation);
                    _phase = Phase.Dragging;
                    if (_renderer.Tents.TryGetValue(_movedOriginal.guestId, out var tent))
                        tent.SetLifted(true, ReducedMotion(), MotionScale());
                    _cardDrag = true;
                    StartAiming(sample.Id > 0);
                    LiftCard(_movedOriginal.guestId,sample.Position);
                    TentGrabbed?.Invoke(_movedOriginal.guestId);
                }
            }

            if (sample.Active && _phase == Phase.Preview)
            {
                var threshold = BoardMath.DragThresholdDp * _projector.DensityScale;
                if ((sample.Position - _pressScreen).sqrMagnitude >= threshold * threshold)
                { _phase = Phase.Dragging; StartAiming(sample.Id > 0); }
                UpdateTarget(sample.Position);
            }
            else if (sample.Active && _phase == Phase.Dragging)
            {
                UpdateTarget(sample.Position);
            }

            if (_cardDrag && _phase == Phase.Dragging) UpdateHeldVisual(sample.Position);
            if (sample.Active && _phase == Phase.Dragging && _targetValid)
            {
                var point = _target.GroundPoint;
                if (_hasDragWorld)
                {
                    var velocity = (point - _lastDragWorld) / Mathf.Max(.001f, Time.unscaledDeltaTime);
                    _dragVisual?.Follow(velocity, ReducedMotion());
                    DragMotion?.Invoke(point + Vector3.up * .45f,
                        Vector3.Distance(point,_lastDragWorld)/Mathf.Max(.001f,Time.unscaledDeltaTime));
                }
                _lastDragWorld=point;_hasDragWorld=true;
            }
            if (sample.Ended) OnRelease(sample);
        }

        public bool BeginCardDrag(string guestId, int pointerId, Vector2 screen)
        {
            if (_pointerId != -1 || _session.IsCompleted || System.Array.Find(_level.guests,g=>g.id==guestId)==null) return false;
            if (_policy != null && !_policy.TryBeginUiPointerCapture(GameplayInputKind.Placement,pointerId)) return false;
            _session.Select(guestId);
            _pointerId = pointerId; _captured = true; _pressScreen = screen;
            _pressTime = Time.unscaledTime;
            _movedOriginal = _session.State.Find(guestId)?.Copy();
            _grabOffset = new Cell(0,0); _cardDrag = true;
            _grabWorld = pointerId > 0 ? Vector2.one : Vector2.one * .5f;
            BeginPreview(-2,-2,_movedOriginal?.rotation ?? _rotation);
            _phase = Phase.Dragging;
            StartAiming(pointerId > 0);
            LiftCard(guestId,screen);
            UpdateTarget(screen);
            UpdateHeldVisual(screen);
            // The first guest can already be selected when the scene opens.
            // Card dragging is still an explicit selection for the guide.
            PreviewBegan?.Invoke(_targetValid ? _target.GroundPoint : Hit(screen));
            return true;
        }
        void LiftCard(string guestId, Vector2 screen)
        {
            if (_dragCard == null) _dragCard = gameObject.AddComponent<TentDragCard>();
            _dragCard.Show(_assets,System.Array.Find(_level.guests,g=>g.id==guestId)?.assetId,_camera,screen,_reducedMotion);
            if(_renderer.Tents.TryGetValue(guestId,out var original)) original.SetHeldCard(true);
        }

        // ─── Pointer phases ─────────────────────────────────────────────────

        void OnPress(PointerSample sample)
        {
            if (_pointerId != -1 || _session.IsCompleted) return;
            if (!ScreenToCell(sample.Position, out var cell)) return;
            var picked = PickTent(sample.Position);
            if (picked == null && !RuleEvaluator.Inside(_level, cell)) return;
            if (_policy != null && !_policy.CanProcess(GameplayInputKind.Placement, sample.Position, sample.Id)) return;

            // Selection is a tap; only a deliberate drag lifts a committed tent.
            if (picked != null) _session.Select(picked);
            else
            {
                var selected = _session.SelectedGuestId;
                // Tap-to-place: a tap on an empty cell picks up the next
                // unplaced tent — no card drag needed. Once every tent is
                // placed, the tap just releases the selection like before.
                if (selected == null || _session.State.Contains(selected))
                {
                    selected = NextUnplacedGuest();
                    if (selected == null)
                    {
                        if (_session.SelectedGuestId != null) _session.Select(null);
                        return;
                    }
                    _session.Select(selected);
                }
            }

            if (_policy != null && !_policy.TryBeginPointerCapture(GameplayInputKind.Placement, sample.Position, sample.Id)) return;
            _pointerId = sample.Id;
            _captured = true;
            _pressScreen = sample.Position;
            _pressTime = Time.unscaledTime;
            if (picked != null)
            {
                _movedOriginal = _session.State.Find(picked);
                if (_movedOriginal == null) { ReleasePointer(); return; }
                _grabOffset = new Cell(Mathf.Clamp(cell.X - _movedOriginal.x, 0, 1),
                    Mathf.Clamp(cell.Z - _movedOriginal.z, 0, 1));
                var point = Hit(sample.Position);
                _grabWorld = sample.Id > 0 ? new Vector2(point.x + _level.width * .5f - _movedOriginal.x,
                    point.z + _level.height * .5f - _movedOriginal.z)
                    : new Vector2(_grabOffset.X + .5f, _grabOffset.Z + .5f);
                _projector.Begin(false, _grabWorld, BoardMath.TentCenter(_level, _movedOriginal.x, _movedOriginal.z));
                _phase = Phase.Pressed;
                return;
            }
            _grabOffset = new Cell(0, 0);
            BeginPreview(Mathf.Min(cell.X, _level.width - 2), Mathf.Min(cell.Z, _level.height - 2), _rotation);
            // Preserve the edge anchor while this tap becomes a drag.
            _grabOffset = new Cell(cell.X - _preview.x, cell.Z - _preview.z);
            _grabWorld = new Vector2(_grabOffset.X + .5f, _grabOffset.Z + .5f);
            _projector.Begin(false, _grabWorld, BoardMath.TentCenter(_level, _preview.x, _preview.z));
            UpdateTarget(sample.Position);
            PreviewBegan?.Invoke(Hit(sample.Position));
        }

        void OnRelease(PointerSample sample)
        {
            if (sample.Id != _pointerId) return;
            // A lift-free tap never writes a duplicate undo step.
            if (_phase == Phase.Preview || _phase == Phase.Dragging)
            {
                UpdateTarget(sample.Position);
                if (_targetValid) Commit(); else Cancel();
            }
            else EndPreview();
            ReleasePointer();
        }

        void ReleasePointer()
        {
            EndCapture();
            _pointerId = -1;
        }

        void EndCapture()
        {
            if (_captured && _policy != null)
                _policy.EndPointerCapture(GameplayInputKind.Placement, _pointerId);
            _captured = false;
        }

        // ─── Preview / commit ────────────────────────────────────────────────

        void BeginPreview(int x, int z, int rotation)
        {
            _phase = Phase.Preview;
            _preview = new Placement
            {
                guestId = _session.SelectedGuestId, x = x, z = z, rotation = rotation,
            };
            ShowGhost(_session.SelectedGuestId);
            RefreshPreview();
        }

        void UpdateAnchor(Cell anchor)
        {
            if (_preview == null || (_preview.x == anchor.X && _preview.z == anchor.Z)) return;
            _preview.x = anchor.X;
            _preview.z = anchor.Z;
            RefreshPreview();
        }

        void StartAiming(bool touch)
        {
            var p = _movedOriginal ?? _preview;
            _projector.Begin(touch, _grabWorld, BoardMath.TentCenter(_level, p.x, p.z));
        }

        void UpdateTarget(Vector2 screen)
        {
            bool wasValid = _targetValid;
            _targetValid = _projector.TryProject(screen, _phase == Phase.Dragging, _preview, out _target);
            if (_targetValid)
            {
                if (!wasValid && _preview != null && _preview.x == _target.Anchor.X && _preview.z == _target.Anchor.Z) RefreshPreview();
                else UpdateAnchor(_target.Anchor);
                if (!_cardDrag && _ghost != null) _ghost.SetActive(true);
            }
            else { _renderer.HidePath(); _renderer.HidePlacementPreview(); if (_ghost != null) _ghost.SetActive(false); }
        }

        void UpdateHeldVisual(Vector2 screen)
        {
            bool onBoard = _targetValid && _preview != null && _preview.x < _level.width
                && _preview.z < _level.height && _preview.x + 1 >= 0 && _preview.z + 1 >= 0;
            _dragCard?.Follow(_target.AimScreen);
            _dragCard?.SetVisible(!onBoard);
            if (_ghost != null) _ghost.SetActive(onBoard);
        }

        void RefreshPreview()
        {
            var command = new PlacementCommand { GuestId = _preview.guestId, After = _preview };
            var report = _session.Preview(command);
            PoseGhost(_preview, report);
            _renderer.ShowPlacementPreview(_preview, report);
            PreviewChanged?.Invoke(report);
            // Path overlay tracks the preview door.
            var route = report.Routes.Find(r => r.GuestId == _preview.guestId);
            if (report.CanCommit && route != null)
                _renderer.ShowRoute(route);
            else _renderer.HidePath();
        }

        void Commit()
        {
            if (_preview == null) { EndPreview(); return; }
            var candidate = _preview.Copy();
            var original = _movedOriginal;
            var command = new PlacementCommand { GuestId = candidate.guestId, Before = original, After = candidate };
            var changed = original == null || original.x != candidate.x || original.z != candidate.z
                || original.rotation != candidate.rotation;
            if (!changed) { EndPreview(); return; }
            if (_session.TryCommit(command, out var report))
            {
                EndPreview();
                PlacementCommitted?.Invoke();
                if (original != null && (original.x != candidate.x || original.z != candidate.z))
                    PlacementMoved?.Invoke();
            }
            else
            {
                EndPreview();
                ReportFailure(report);
            }
        }

        void ReportFailure(RuleReport report)
        {
            if (Time.unscaledTime < _invalidToastCooldown) return;
            _invalidToastCooldown = Time.unscaledTime + .4f;
            var issue = report.Issues.Find(i => i.Hard) ?? report.Issues.Find(i => i.GuestId == _session.SelectedGuestId);
            PlacementFailed?.Invoke("rule." + (issue?.Code ?? "bounds"));
        }

        /// <summary>External actions: rotate preview or selected tent, remove, cancel.</summary>
        public bool Rotate()
        {
            if (_session.IsCompleted) return false;
            if (_preview != null)
            {
                _preview.rotation = (_preview.rotation + 1) % 4;
                _rotation = _preview.rotation;
                RefreshPreview();
                return true;
            }
            var id = _session.SelectedGuestId;
            var placement = id != null ? _session.State.Find(id) : null;
            if (id == null) return false;
            if (placement == null) { _rotation = (_rotation + 1) % 4; return true; }
            var command = new PlacementCommand
            {
                GuestId = id,
                Before = placement,
                After = new Placement
                { guestId = id, x = placement.x, z = placement.z, rotation = (placement.rotation + 1) % 4 },
            };
            if (!_session.TryCommit(command, out var report)) { ReportFailure(report); return false; }
            _rotation = command.After.rotation;
            PlacementCommitted?.Invoke();
            return true;
        }

        public void Remove()
        {
            var id = _session.SelectedGuestId;
            if (id == null || _session.State.Find(id) == null) return;
            EndPreview(); ReleasePointer();
            var command = PlacementCommand.Remove(id);
            if (_session.TryCommit(command, out _)) PlacementCommitted?.Invoke();
        }

        public void Cancel()
        {
            var active = _phase != Phase.Idle || _captured;
            EndPreview();
            ReleasePointer();
            _session?.Select(null);
            if (active) Cancelled?.Invoke();
        }

        void EndPreview()
        {
            _hasDragWorld = false;
            _targetValid = false;
            _cardDrag = false; _dragCard?.Hide();
            _phase = Phase.Idle;
            _preview = null;
            _movedOriginal = null;
            _renderer?.HidePath();
            _renderer?.HidePlacementPreview();
            PreviewChanged?.Invoke(null);
            if (_ghost != null) _ghost.SetActive(false);
            if (_renderer != null)
                foreach (var p in _renderer.Tents.Values) { p.SetHeldCard(false);p.SetLifted(false, ReducedMotion(), MotionScale()); }
        }

        // ─── Ghost visual ────────────────────────────────────────────────────

        string _ghostAssetId;

        void ShowGhost(string guestId)
        {
            var guest = System.Array.Find(_level.guests, g => g.id == guestId);
            if (guest == null) return;
            if (_ghost == null || _ghostAssetId != guest.assetId)
            {
                if (_ghost != null) Destroy(_ghost);
                if (_assets == null || !_assets.TryGet(guest.assetId, out var model)
                    || model.prefab == null) return;
                _ghostAssetId = guest.assetId;
                _ghost = Instantiate(model.prefab);
                _ghost.name = "Ghost_" + guestId;
                foreach (var c in _ghost.GetComponentsInChildren<Collider>())
                    c.enabled = false;
                // The footprint carries rule colours. The preview is the
                // actual tent, with its own fabric and rigid support colours.
                TentCloth.Apply(_ghost);
                _dragVisual = _ghost.AddComponent<TentDragVisual>();
            }
            _ghost.SetActive(true);
        }

        void PoseGhost(Placement p, RuleReport report)
        {
            if (_ghost == null) return;
            var position = BoardMath.TentCenter(_level, p.x, p.z);
            var rotation = new Vector3(0f, BoardMath.TentYaw(p.rotation), 0f);
            _ghost.transform.localPosition = position;
            _ghost.transform.localEulerAngles = rotation;
        }

        // ─── Ray helpers ─────────────────────────────────────────────────────

        string PickTent(Vector2 screen)
        {
            var ray = _camera.ScreenPointToRay(screen);
            if (!Physics.Raycast(ray, out var hit, 100f,
                    1 << BoardRenderer.TentSelectableLayer)) return null;
            var t = hit.collider.transform;
            while (t != null)
            {
                foreach (var kv in _renderer.Tents)
                    if (kv.Value.Root == t.gameObject) return kv.Key;
                t = t.parent;
            }
            return null;
        }

        /// <summary>Next guest without a placement, in roster (card) order.</summary>
        string NextUnplacedGuest()
        {
            foreach (var guest in _level.guests)
                if (guest != null && !_session.State.Contains(guest.id)) return guest.id;
            return null;
        }

        bool ScreenToCell(Vector2 screen, out Cell cell)
        {
            cell = default;
            var ray = _camera.ScreenPointToRay(screen);
            if (!_plane.Raycast(ray, out var distance)) return false;
            cell = BoardMath.CellOf(_level, ray.GetPoint(distance));
            return true;
        }

        Vector3 Hit(Vector2 screen)
        {
            var ray = _camera.ScreenPointToRay(screen);
            return _plane.Raycast(ray, out var distance) ? ray.GetPoint(distance) : Vector3.zero;
        }

        bool ReducedMotion() => _reducedMotion?.Invoke() == true;
        float MotionScale() => _motionScale?.Invoke() ?? 1f;

        void OnDisable()
        {
            if (_phase != Phase.Idle || _captured) Cancel();
        }

        void OnApplicationFocus(bool focused)
        {
            if (!focused && (_phase != Phase.Idle || _captured)) Cancel();
        }

        void OnApplicationPause(bool paused)
        {
            if (paused && (_phase != Phase.Idle || _captured)) Cancel();
        }

        // ─── Unified pointer ─────────────────────────────────────────────────

        struct PointerSample
        {
            public bool Has, Began, Active, Ended, Moved, Canceled;
            public int Id;
            public Vector2 Position;
        }

        PointerSample ReadPointer()
        {
            var touch = Touchscreen.current;
            if (touch != null && _pointerId != 0)
            {
                var primary = touch.primaryTouch;
                if (_pointerId > 0)
                {
                    primary = null;
                    foreach (var candidate in touch.touches)
                        if (candidate.touchId.ReadValue() == _pointerId) { primary = candidate; break; }
                    if (primary == null) return default;
                }
                var phase = primary.phase.ReadValue();
                if (phase == UnityEngine.InputSystem.TouchPhase.Began
                    || phase == UnityEngine.InputSystem.TouchPhase.Moved
                    || phase == UnityEngine.InputSystem.TouchPhase.Stationary
                    || phase == UnityEngine.InputSystem.TouchPhase.Ended
                    || phase == UnityEngine.InputSystem.TouchPhase.Canceled)
                {
                    return new PointerSample
                    {
                        Has = true,
                        Canceled = phase == UnityEngine.InputSystem.TouchPhase.Canceled,
                        Id = primary.touchId.ReadValue(),
                        Position = primary.position.ReadValue(),
                        Began = primary.press.wasPressedThisFrame,
                        Ended = primary.press.wasReleasedThisFrame,
                        Active = primary.press.isPressed,
                        Moved = phase == UnityEngine.InputSystem.TouchPhase.Moved,
                    };
                }
            }
            if (_pointerId > 0) return default;
            var mouse = Mouse.current;
            if (mouse == null) return default;
            return new PointerSample
            {
                Has = true,
                Id = 0,
                Position = mouse.position.ReadValue(),
                Began = mouse.leftButton.wasPressedThisFrame,
                Ended = mouse.leftButton.wasReleasedThisFrame,
                Active = mouse.leftButton.isPressed,
                Moved = mouse.delta.ReadValue().sqrMagnitude > 0.01f,
            };
        }
    }
}
