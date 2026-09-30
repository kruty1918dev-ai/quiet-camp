using System.Collections.Generic;
using Kruty1918.Atmos;
using QuietCamp.Domain;
using QuietCamp.Infrastructure;
using UnityEngine;
namespace QuietCamp.Presentation.World
{
    /// <summary>
    /// Builds the whole playable field for one level into the scene's scaffold
    /// roots: base slab, grid lines, rock obstacles, campfire, rule overlays,
    /// entry marker and the tents of committed placements. Logical occupancy
    /// stays in RuleEvaluator — this class only draws BoardState.
    /// </summary>
    public sealed class BoardRenderer
    {
        public const int BoardSurfaceLayer = 8;
        public const int TentSelectableLayer = 9;
        public const int GameplayObstacleLayer = 10;
        public const int DecorLayer = 11;
        public const int RuleOverlayLayer = 12;

        static readonly Color GroundColor = new Color(0.34f, 0.42f, 0.30f);
        static readonly Color BaseSideColor = new Color(0.42f, 0.36f, 0.24f);
        static readonly Color GrassA = new Color(0.55f, 0.66f, 0.47f);
        static readonly Color GrassB = new Color(0.52f, 0.63f, 0.44f);
        static readonly Color ShadeTint = new Color(0.44f, 0.55f, 0.38f);
        static readonly Color PathColor = new Color(1f, 0.88f, 0.45f, 0.55f);

        readonly LevelData _level;
        readonly AssetCatalog _catalog;
        readonly Transform _baseRoot, _gridRoot, _obstacleRoot, _tentRoot, _overlayRoot;
        readonly List<GameObject> _tintObjects = new List<GameObject>();
        readonly Dictionary<string, TentPresenter> _tents = new Dictionary<string, TentPresenter>();
        GameObject _pathOverlay;
        Material _overlayMaterial;

        public IReadOnlyDictionary<string, TentPresenter> Tents => _tents;

        public BoardRenderer(
            LevelData level, AssetCatalog catalog,
            Transform baseRoot, Transform gridRoot, Transform obstacleRoot,
            Transform tentRoot, Transform overlayRoot)
        {
            _level = level;
            _catalog = catalog;
            _baseRoot = baseRoot;
            _gridRoot = gridRoot;
            _obstacleRoot = obstacleRoot;
            _tentRoot = tentRoot;
            _overlayRoot = overlayRoot;
            _overlayMaterial = MakeOverlayMaterial();
            Build();
        }

        void Build()
        {
            BuildBase();
            BuildCells();
            BuildObstacles();
            BuildTufts();
        }

        // ─── Base ────────────────────────────────────────────────────────────

        void BuildBase()
        {
            var baseGo = NewPrimitive(PrimitiveType.Cube, "BaseSlab", _baseRoot,
                new Vector3(_level.width + BoardMath.Overhang * 2f,
                    BoardMath.BaseThickness, _level.height + BoardMath.Overhang * 2f),
                new Vector3(0f, BoardMath.BaseCenterY, 0f), GroundColor, BoardSurfaceLayer);
            var side = NewPrimitive(PrimitiveType.Cube, "BaseSide", _baseRoot,
                new Vector3(_level.width + BoardMath.Overhang * 2f + 0.04f,
                    BoardMath.BaseThickness - 0.04f, _level.height + BoardMath.Overhang * 2f + 0.04f),
                new Vector3(0f, BoardMath.BaseCenterY - 0.02f, 0f), BaseSideColor, BoardSurfaceLayer);
            _tintObjects.Add(baseGo);
            _tintObjects.Add(side);
        }

        /// <summary>
        /// One raised tile per playable cell: subtle checker grass tones and a
        /// darker green for shade cells — the only zone painted on the field,
        /// like the reference render. Thin gaps between tiles read as grid
        /// lines over the darker slab.
        /// </summary>
        void BuildCells()
        {
            var shade = new HashSet<Cell>();
            foreach (var s in _level.shade) shade.Add(new Cell(s[0], s[1]));

            for (var x = 0; x < _level.width; x++)
            for (var z = 0; z < _level.height; z++)
            {
                var cell = new Cell(x, z);
                var color = shade.Contains(cell) ? ShadeTint
                    : ((x + z) & 1) == 0 ? GrassA : GrassB;
                var tile = NewPrimitive(PrimitiveType.Cube, $"Cell_{x}_{z}", _gridRoot,
                    new Vector3(0.988f, 0.014f, 0.988f),
                    BoardMath.CellCenterWorld(_level, cell) + new Vector3(0f, 0.007f, 0f),
                    color, BoardSurfaceLayer);
                _tintObjects.Add(tile);
            }
        }

        /// <summary>Deterministic grass tufts on some free cells — meadow feel.</summary>
        void BuildTufts()
        {
            if (_catalog == null) return;
            var blocked = new HashSet<Cell>();
            foreach (var b in _level.blocked) blocked.Add(new Cell(b[0], b[1]));
            foreach (var n in _level.noise) blocked.Add(new Cell(n[0], n[1]));
            var rng = new System.Random(_level.decorSeed * 7919 + 13);
            for (var x = 0; x < _level.width; x++)
            for (var z = 0; z < _level.height; z++)
            {
                var cell = new Cell(x, z);
                if (blocked.Contains(cell) || rng.NextDouble() > 0.28) continue;
                var p = BoardMath.CellCenterWorld(_level, cell);
                p.x += (float)(rng.NextDouble() * 0.5 - 0.25);
                p.z += (float)(rng.NextDouble() * 0.5 - 0.25);
                p.y = 0.016f;
                var go = SpawnModel("grass", _gridRoot, p, rng.Next(360), DecorLayer);
                if (go != null) go.transform.localScale *= 0.65f;
            }
        }

        // ─── Obstacles ───────────────────────────────────────────────────────

        void BuildObstacles()
        {
            var noise = new HashSet<Cell>();
            foreach (var n in _level.noise) noise.Add(new Cell(n[0], n[1]));
            var fire = new List<Cell>(noise);
            foreach (var b in _level.blocked)
            {
                var cell = new Cell(b[0], b[1]);
                if (noise.Contains(cell)) continue;
                SpawnModel("stone_largeA", _obstacleRoot,
                    BoardMath.CellCenterWorld(_level, cell),
                    Random.Range(0, 4) * 90, GameplayObstacleLayer);
            }
            foreach (var cell in fire)
            {
                var go = SpawnModel("campfire_stones", _obstacleRoot,
                    BoardMath.CellCenterWorld(_level, cell), 0, GameplayObstacleLayer);
                if (go != null)
                {
                    var logs = SpawnModel("log_stack", go.transform,
                        new Vector3(0f, 0.03f, 0f), 45, GameplayObstacleLayer);
                    Campfire.Create(go.transform);
                }
            }
        }

        // ─── Overlays ────────────────────────────────────────────────────────

        /// <summary>Toggles the BFS path entry->door overlay for the selected guest.</summary>
        public void ShowPath(Cell door, HashSet<Cell> occupied)
        {
            HidePath();
            var path = RuleEvaluator.Path(_level, occupied,
                new Cell(_level.entry[0], _level.entry[1]), door);
            if (path == null || path.Count == 0) return;
            _pathOverlay = new GameObject("PathOverlay");
            _pathOverlay.transform.SetParent(_overlayRoot, false);
            foreach (var cell in path)
            {
                var go = OverlayCell("path_" + cell, cell, PathColor);
                go.transform.SetParent(_pathOverlay.transform, true);
            }
        }

        public void HidePath()
        {
            if (_pathOverlay == null) return;
            Object.Destroy(_pathOverlay);
            _pathOverlay = null;
        }

        GameObject OverlayCell(string name, Cell cell, Color color)
        {
            var go = NewPrimitive(PrimitiveType.Cube, name, _overlayRoot,
                new Vector3(0.94f, 0.002f, 0.94f),
                BoardMath.CellCenterWorld(_level, cell) + new Vector3(0f, BoardMath.OverlayY, 0f),
                null, RuleOverlayLayer, _overlayMaterial);
            Tint(go, color);
            _tintObjects.Add(go);
            return go;
        }

        // ─── Tents ───────────────────────────────────────────────────────────

        public TentPresenter PlaceTent(string guestId, string assetId, Placement placement)
        {
            var go = SpawnModel(assetId, _tentRoot, Vector3.zero, 0, TentSelectableLayer);
            if (go == null)
            {
                Debug.LogError($"[QuietCamp] Missing tent prefab '{assetId}'.");
                return null;
            }
            var presenter = new TentPresenter(go, _level, guestId);
            _tents[guestId] = presenter;
            presenter.ApplyPlacement(placement, instant: true);
            return presenter;
        }

        public void RemoveTent(string guestId)
        {
            if (_tents.TryGetValue(guestId, out var presenter))
            {
                presenter.Dispose();
                _tents.Remove(guestId);
            }
        }

        /// <summary>Re-renders the committed layout snapshot (post commit/undo/redo).</summary>
        public void SyncPlacements(IReadOnlyList<Placement> placements, LevelData level,
            float durationScale = 1f)
        {
            var alive = new HashSet<string>();
            foreach (var p in placements)
            {
                alive.Add(p.guestId);
                if (!_tents.TryGetValue(p.guestId, out var presenter))
                {
                    var guest = System.Array.Find(level.guests, g => g.id == p.guestId);
                    if (guest == null) continue;
                    presenter = PlaceTent(p.guestId, guest.assetId, p);
                }
                else presenter.ApplyPlacement(p, instant: false, durationScale);
            }
            var remove = new List<string>();
            foreach (var kv in _tents)
                if (!alive.Contains(kv.Key)) remove.Add(kv.Key);
            foreach (var id in remove) RemoveTent(id);
        }

        public void ClearAll()
        {
            foreach (var t in _tents.Values) t.Dispose();
            _tents.Clear();
            HidePath();
        }

        // ─── Helpers ─────────────────────────────────────────────────────────

        GameObject SpawnModel(string assetId, Transform parent, Vector3 localPos,
            int yaw, int layer)
        {
            if (_catalog == null || !_catalog.TryGet(assetId, out var entry) || entry.prefab == null)
            {
                Debug.LogError($"[QuietCamp] AssetCatalog missing '{assetId}'.");
                return null;
            }
            var go = Object.Instantiate(entry.prefab, parent);
            go.transform.localPosition = localPos;
            go.transform.localEulerAngles = new Vector3(0f, yaw, 0f);
            SetLayerRecursively(go, layer);
            return go;
        }

        static void SetLayerRecursively(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform) SetLayerRecursively(child.gameObject, layer);
        }

        static GameObject NewPrimitive(PrimitiveType type, string name, Transform parent,
            Vector3 scale, Vector3 localPos, Color? color, int layer, Material material = null)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localScale = scale;
            go.transform.localPosition = localPos;
            var collider = go.GetComponent<Collider>();
            if (collider != null) Object.Destroy(collider);
            var renderer = go.GetComponent<Renderer>();
            if (material != null) renderer.sharedMaterial = material;
            else if (color.HasValue)
                renderer.sharedMaterial = MakeLitMaterial(color.Value);
            go.layer = layer;
            return go;
        }

        static Material _litFallback;
        static Material MakeLitMaterial(Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Simple Lit");
            var m = new Material(shader);
            m.SetColor("_BaseColor", color);
            return m;
        }

        Material MakeOverlayMaterial()
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            var m = new Material(shader);
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            return m;
        }

        static void Tint(GameObject go, Color color)
        {
            var renderer = go.GetComponent<Renderer>();
            if (renderer == null) return;
            var mat = renderer.material;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            else if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
        }
    }
}
