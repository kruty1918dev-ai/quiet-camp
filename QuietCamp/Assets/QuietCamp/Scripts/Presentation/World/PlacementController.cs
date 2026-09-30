using System.Collections.Generic;
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
        enum Phase { Idle, Preview, Dragging }

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

        Placement _preview;          // candidate pose being previewed
        Cell _grabOffset;            // cell delta grabbed inside the footprint
        Placement _movedOriginal;    // original placement while moving
        GameObject _ghost;
        readonly List<Renderer> _ghostRenderers = new List<Renderer>();
        Material _ghostMaterial;
        float _invalidToastCooldown;

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
            _ghostMaterial = MakeGhostMaterial();
            session.Evented += OnSessionEvent;
        }

        void OnDestroy()
        {
            if (_session != null) _session.Evented -= OnSessionEvent;
            EndCapture();
        }

        void OnSessionEvent(CampEvent e)
        {
            if (e.Kind == CampEventKind.SelectionChanged && e.GuestId == null && _phase != Phase.Idle)
                Cancel();
        }

        void Update()
        {
            var sample = ReadPointer();
            if (!sample.Has) return;

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

            if (sample.Active && _phase == Phase.Preview)
            {
                // Threshold drag: keep ghost anchored under the pointer.
                if (ScreenToCell(sample.Position, out var hit))
                    UpdateAnchor(HitToAnchor(sample, hit));
                if (sample.Moved) _phase = Phase.Dragging;
            }
            else if (sample.Active && _phase == Phase.Dragging)
            {
                if (ScreenToCell(sample.Position, out var hit))
                    UpdateAnchor(HitToAnchor(sample, hit));
            }

            if (sample.Ended) OnRelease(sample);
        }

        // ─── Pointer phases ─────────────────────────────────────────────────

        void OnPress(PointerSample sample)
        {
            if (_pointerId != -1) return; // a second touch never steals the drag
            if (!ScreenToCell(sample.Position, out _)) return;

            var picked = PickTent(sample.Position);
            var canProcess = _policy == null
                || _policy.TryBeginPointerCapture(GameplayInputKind.Placement, sample.Position, sample.Id);
            if (!canProcess) return;

            _pointerId = sample.Id;
            _captured = true;
            _pressScreen = sample.Position;

            if (picked != null)
            {
                // Begin a move: the tent lifts into a ghost, old pose stays committed.
                var placement = _session.State.Find(picked);
                if (placement != null)
                {
                    _movedOriginal = placement;
                    _session.Select(picked);
                    BeginPreview(placement.x, placement.z, placement.rotation);
                    _grabOffset = new Cell(
                        BoardMath.CellOf(_level, Hit(sample.Position)).X - placement.x,
                        BoardMath.CellOf(_level, Hit(sample.Position)).Z - placement.z);
                    _renderer.Tents.TryGetValue(picked, out var presenter);
                    presenter?.SetLifted(true, ReducedMotion(), MotionScale());
                    TentGrabbed?.Invoke(picked);
                }
                return;
            }

            if (_session.SelectedGuestId == null) { ReleasePointer(); return; }
            var hit = Hit(sample.Position);
            var cell = BoardMath.CellOf(_level, hit);
            BeginPreview(cell.X, cell.Z, _preview?.rotation ?? 0);
            _grabOffset = new Cell(0, 0);
            PreviewBegan?.Invoke(hit);
        }

        void OnRelease(PointerSample sample)
        {
            if (sample.Id != _pointerId) return;
            if (_phase == Phase.Preview || _phase == Phase.Dragging) Commit();
            ReleasePointer();
        }

        void ReleasePointer()
        {
            _pointerId = -1;
            EndCapture();
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

        Cell HitToAnchor(PointerSample sample, Cell hit)
            => new Cell(hit.X - _grabOffset.X, hit.Z - _grabOffset.Z);

        void RefreshPreview()
        {
            var command = new PlacementCommand { GuestId = _preview.guestId, After = _preview };
            var report = _session.Preview(command);
            PoseGhost(_preview, report.CanCommit);
            // Path overlay tracks the preview door.
            var occupied = OccupiedWithout(_preview.guestId);
            if (report.CanCommit && RuleEvaluator.Inside(_level, RuleEvaluator.Door(_preview)))
                _renderer.ShowPath(RuleEvaluator.Door(_preview), occupied);
            else _renderer.HidePath();
        }

        void Commit()
        {
            if (_preview == null) { Cancel(); return; }
            var command = new PlacementCommand
            {
                GuestId = _preview.guestId,
                Before = _movedOriginal,
                After = _preview.Copy(),
            };
            var moved = _movedOriginal != null;
            if (_session.TryCommit(command, out var report))
            {
                EndPreview();
                PlacementCommitted?.Invoke();
            }
            else
            {
                // Hard-invalid: keep the committed layout, flash amber, explain.
                TintGhost(new Color(1f, 0.62f, 0.15f, 0.55f));
                if (_movedOriginal != null)
                    _renderer.Tents[_preview.guestId]?.ApplyPlacement(_movedOriginal, instant: false, MotionScale());
                var issue = report.Issues.Count > 0 ? report.Issues[0].Code : "bounds";
                if (Time.unscaledTime > _invalidToastCooldown)
                {
                    _invalidToastCooldown = Time.unscaledTime + 0.4f;
                    PlacementFailed?.Invoke("rule." + issue);
                }
            }
        }

        /// <summary>External actions: rotate preview or selected tent, remove, cancel.</summary>
        public void Rotate()
        {
            if (_preview != null)
            {
                _preview.rotation = (_preview.rotation + 1) % 4;
                RefreshPreview();
                return;
            }
            var id = _session.SelectedGuestId;
            var placement = id != null ? _session.State.Find(id) : null;
            if (placement == null) return;
            var command = new PlacementCommand
            {
                GuestId = id,
                Before = placement,
                After = new Placement
                { guestId = id, x = placement.x, z = placement.z, rotation = (placement.rotation + 1) % 4 },
            };
            if (_session.TryCommit(command, out _)) PlacementCommitted?.Invoke();
        }

        public void Remove()
        {
            var id = _session.SelectedGuestId;
            if (id == null || _session.State.Find(id) == null) return;
            var command = PlacementCommand.Remove(id);
            if (_session.TryCommit(command, out _)) PlacementCommitted?.Invoke();
        }

        public void Cancel()
        {
            EndPreview();
            _session.Select(null);
            Cancelled?.Invoke();
        }

        void EndPreview()
        {
            _phase = Phase.Idle;
            _preview = null;
            _movedOriginal = null;
            _renderer.HidePath();
            if (_ghost != null) _ghost.SetActive(false);
            foreach (var p in _renderer.Tents.Values) p.SetLifted(false, ReducedMotion(), MotionScale());
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
                _ghostRenderers.Clear();
                _ghostRenderers.AddRange(_ghost.GetComponentsInChildren<Renderer>());
                foreach (var r in _ghostRenderers)
                {
                    var arr = r.sharedMaterials;
                    for (var i = 0; i < arr.Length; i++) arr[i] = _ghostMaterial;
                    r.sharedMaterials = arr;
                }
            }
            _ghost.SetActive(true);
        }

        void PoseGhost(Placement p, bool valid)
        {
            if (_ghost == null) return;
            _ghost.transform.localPosition = BoardMath.TentCenter(_level, p.x, p.z);
            _ghost.transform.localEulerAngles = new Vector3(0f, BoardMath.TentYaw(p.rotation), 0f);
            TintGhost(valid ? new Color(0.45f, 0.85f, 0.45f, 0.5f) : new Color(1f, 0.62f, 0.15f, 0.55f));
        }

        void TintGhost(Color color)
        {
            if (_ghostMaterial != null && _ghostMaterial.HasProperty("_BaseColor"))
                _ghostMaterial.SetColor("_BaseColor", color);
        }

        static Material MakeGhostMaterial()
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            m.SetFloat("_Surface", 1f);
            m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            m.SetColor("_BaseColor", new Color(0.45f, 0.85f, 0.45f, 0.5f));
            return m;
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

        HashSet<Cell> OccupiedWithout(string guestId)
        {
            var set = new HashSet<Cell>();
            foreach (var b in _level.blocked) set.Add(new Cell(b[0], b[1]));
            set.Add(new Cell(_level.entry[0], _level.entry[1]));
            foreach (var p in _session.State.Placements)
            {
                if (p.guestId == guestId) continue;
                foreach (var c in RuleEvaluator.Footprint(p)) set.Add(c);
            }
            return set;
        }

        bool ReducedMotion() => _reducedMotion?.Invoke() == true;
        float MotionScale() => _motionScale?.Invoke() ?? 1f;

        // ─── Unified pointer ─────────────────────────────────────────────────

        struct PointerSample
        {
            public bool Has, Began, Active, Ended, Moved;
            public int Id;
            public Vector2 Position;
        }

        PointerSample ReadPointer()
        {
            var touch = Touchscreen.current;
            if (touch != null)
            {
                var primary = touch.primaryTouch;
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
                        Id = primary.touchId.ReadValue(),
                        Position = primary.position.ReadValue(),
                        Began = primary.press.wasPressedThisFrame,
                        Ended = primary.press.wasReleasedThisFrame,
                        Active = primary.press.isPressed,
                        Moved = phase == UnityEngine.InputSystem.TouchPhase.Moved,
                    };
                }
            }
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
