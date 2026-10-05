using System.Collections.Generic;
using Kruty1918.Atmos;
using QuietCamp.Domain;
using QuietCamp.Infrastructure;
using UnityEngine;
using DG.Tweening;
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
        static readonly Color PathColor = new Color(1f, 0.88f, 0.45f, 0.55f);

        readonly LevelData _level;
        readonly AssetCatalog _catalog;
        readonly Transform _baseRoot, _gridRoot, _obstacleRoot, _tentRoot, _overlayRoot;
        readonly List<GameObject> _tintObjects = new List<GameObject>();
        readonly Dictionary<string, TentPresenter> _tents = new Dictionary<string, TentPresenter>();
        float _belongingsMotionScale=1;bool _belongingsReduced;
        BoardRim _rim;
        GameObject _pathOverlay;
        GameObject _previewOverlay;
        GameObject _previewDoor;
        readonly List<GameObject> _pathCells = new List<GameObject>();
        readonly List<GameObject> _previewCells = new List<GameObject>();
        GameObject _issueOverlay;
        GameObject _hintOverlay;
        GameObject _hintGhost;
        Material _overlayMaterial;
        Material _groundMaterial;
        CampGroundDetails _groundDetails;

        Kruty1918.GameplayViewport.GameplayViewport _viewport;
        public IReadOnlyDictionary<string, TentPresenter> Tents => _tents;
        public void BindViewport(Kruty1918.GameplayViewport.GameplayViewport viewport)
        {
            _viewport = viewport;
            foreach (var tent in _tents.Values) tent.BindViewport(viewport);
        }

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
            // The scene Base is below the meadow; keep the visible rim with the surface grid.
            _rim = BoardRim.Create(_level, _gridRoot);
            BuildObstacles();
            BuildTufts();

            // A worn route through the forest explains access without glyphs.
            CampTrail.BuildAll(_level,_gridRoot);
            _groundDetails = _gridRoot.gameObject.AddComponent<CampGroundDetails>();
            _groundDetails.Configure(_level,_groundMaterial);
        }

        // ─── Base ────────────────────────────────────────────────────────────

        void BuildBase()
        {
            var baseGo = NewPrimitive(PrimitiveType.Cube, "BaseSlab", _baseRoot,
                new Vector3(_level.width + BoardMath.Overhang * 2f,
                    BoardMath.BaseThickness, _level.height + BoardMath.Overhang * 2f),
                new Vector3(0f, BoardMath.BaseCenterY, 0f), Color.Lerp(SeasonPalette.For(_level).GrassDark,SeasonPalette.For(_level).GrassLight,.5f)*.84f, BoardSurfaceLayer);
            var side = NewPrimitive(PrimitiveType.Cube, "BaseSide", _baseRoot,
                new Vector3(_level.width + BoardMath.Overhang * 2f + 0.04f,
                    BoardMath.BaseThickness - 0.04f, _level.height + BoardMath.Overhang * 2f + 0.04f),
                new Vector3(0f, BoardMath.BaseCenterY - 0.02f, 0f), BaseSideColor, BoardSurfaceLayer);
            _tintObjects.Add(baseGo);
            _tintObjects.Add(side);
        }

        /// <summary>
        /// One raised tile per playable cell with subtle seasonal grass tones.
        /// Tree shadows explain shade; rule zones appear
        /// only on request. Thin gaps between tiles read as grid
        /// lines over the darker slab.
        /// </summary>
        void BuildCells()
        {
            var palette=SeasonPalette.For(_level);
            _groundMaterial=new Material(Resources.Load<Shader>("QuietCamp/Meadow")){name="Continuous clearing ground"};
            _groundMaterial.SetVector("_ClearingHalfSize",new Vector4(_level.width*.5f,_level.height*.5f,0,0));
            _groundMaterial.SetColor("_GroundLow",Color.Lerp(palette.GrassDark,palette.GrassLight,.43f));
            _groundMaterial.SetColor("_GroundHigh",Color.Lerp(palette.GrassDark,palette.GrassLight,.83f));
            _groundMaterial.SetColor("_SoilTone",palette.Soil);
            _groundMaterial.SetFloat("_CellGrid",1);
            for (var x = 0; x < _level.width; x++)
            for (var z = 0; z < _level.height; z++)
            {
                var cell = new Cell(x, z);
                var tile = NewPrimitive(PrimitiveType.Cube, $"Cell_{x}_{z}", _gridRoot,
                    new Vector3(0.992f, 0.014f, 0.992f),
                    BoardMath.CellCenterWorld(_level, cell) + new Vector3(0f, 0.007f, 0f),
                    null, BoardSurfaceLayer,_groundMaterial);
                _tintObjects.Add(tile);
            }
        }

        /// <summary>Deterministic grass tufts on some free cells — meadow feel.</summary>
        void BuildTufts()
        {
            if (_catalog == null) return;
            var season=SeasonProfile.For(_level);if(season.Winter)return;
            var blocked = new HashSet<Cell>();
            foreach (var b in _level.blocked) blocked.Add(new Cell(b[0], b[1]));
            foreach (var n in _level.noise) blocked.Add(new Cell(n[0], n[1]));
            var rng = new System.Random(_level.decorSeed * 7919 + 13);
            for (var x = 0; x < _level.width; x++)
            for (var z = 0; z < _level.height; z++)
            {
                var cell = new Cell(x, z);
                if (blocked.Contains(cell) || rng.NextDouble() > 0.28*season.GrassWeight) continue;
                var p = BoardMath.CellCenterWorld(_level, cell);
                p.x += (float)(rng.NextDouble() * 0.5 - 0.25);
                p.z += (float)(rng.NextDouble() * 0.5 - 0.25);
                p.y = 0.016f;
                var go = SpawnModel("grass", _gridRoot, p, rng.Next(360), DecorLayer);
                if (go != null) { go.transform.localScale *= 0.65f;Kruty1918.Atmos.FoliageSway.Shared.ApplySlots(go,new[]{Kruty1918.Atmos.FoliageSway.Species.Grass}); }
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
                var data = System.Array.Find(_level.objects ?? System.Array.Empty<EnvironmentObjectData>(), o => o.x == cell.X && o.z == cell.Z);
                var model = SpawnModel(data?.assetId ?? "stone_largeA", _obstacleRoot,
                    BoardMath.CellCenterWorld(_level, cell), (data?.rotation ?? 0)*90, GameplayObstacleLayer);
                if (model != null)
                {
                    bool tree=data?.assetId?.StartsWith("tree")==true;
                    if(tree)Kruty1918.Atmos.FoliageSway.Shared.ApplySlots(model,new[]{Kruty1918.Atmos.FoliageSway.Species.Trunk,
                        data.assetId=="tree_default"?Kruty1918.Atmos.FoliageSway.Species.Canopy:Kruty1918.Atmos.FoliageSway.Species.Conifer});
                    DecorSpawner.ScaleToHeight(model,tree?2.4f:.6f);
                    if(tree)SeasonalTreeVisual.Apply(model,_level,data.assetId);
                    if(!tree)
                    {
                        var rs=model.GetComponentsInChildren<Renderer>();var bounds=rs[0].bounds;foreach(var r in rs)bounds.Encapsulate(r.bounds);
                        model.transform.localScale*=Mathf.Min(1,.86f/Mathf.Max(bounds.size.x,bounds.size.z));
                        SeasonalPropVisual.Apply(model,_level,data?.assetId??"stone_largeA");
                    }
                }
            }
            foreach (var cell in fire)
                CampfireSite.Create(_catalog,_obstacleRoot,BoardMath.CellCenterWorld(_level,cell),GameplayObstacleLayer);
        }

        // Each crown is centred so its shadow projects onto the authored ellipse.
        // Sun direction stays fixed throughout solving, including wind gusts.
        public void BuildCanopies()
        {
            var sun = RenderSettings.sun;
            var dir = sun != null ? sun.transform.forward : new Vector3(.4f,-.8f,.4f).normalized;
            foreach (var crown in _level.canopies ?? System.Array.Empty<ShadeCanopyData>())
            {
                float crownHeight = 2.1f;
                // Keep the trunk outside the board while projecting into the clearing.
                crownHeight=Mathf.Max(crownHeight,(crown.x+.4f)*Mathf.Max(.2f,-dir.y)/Mathf.Max(.12f,dir.x));
                var centre = new Vector3(crown.x - _level.width*.5f, 0, crown.z - _level.height*.5f);
                var projection = new Vector3(dir.x,0,dir.z) * (crownHeight/Mathf.Max(.2f,-dir.y));
                var tree = SpawnModel("tree_default", _obstacleRoot, centre-projection, 0, DecorLayer);
                if (tree == null) continue;
                tree.name = "ShadeCanopy";
                Kruty1918.Atmos.FoliageSway.Shared.ApplySlots(tree,new[]{Kruty1918.Atmos.FoliageSway.Species.Trunk,Kruty1918.Atmos.FoliageSway.Species.Canopy});
                DecorSpawner.ScaleToHeight(tree, crownHeight/ .7f);
                // Ellipse bounds describe the crown's ground projection.
                var rs = tree.GetComponentsInChildren<Renderer>();
                var bounds = rs[0].bounds; foreach (var r in rs) bounds.Encapsulate(r.bounds);
                tree.transform.localScale = Vector3.Scale(tree.transform.localScale,
                    new Vector3(crown.radiusX*2/Mathf.Max(.1f,bounds.size.x),1,crown.radiusZ*2/Mathf.Max(.1f,bounds.size.z)));
                SeasonalTreeVisual.Apply(tree,_level,"tree_default");
                foreach (var c in tree.GetComponentsInChildren<Collider>()) Object.Destroy(c);
            }
            // Restored camps can be drawn before lighting creates the canopies.
            // Re-evaluate packing against these final renderer bounds too.
            if(_tents.Count>0)
            {
                var placements=new List<Placement>();foreach(var tent in _tents.Values)if(tent.Placement!=null)placements.Add(tent.Placement);
                RefreshBelongings(placements,_belongingsMotionScale,_belongingsReduced);
            }
        }

        public void ShowShade(bool visible)
        {
            var cells = new List<Cell>();
            if (visible) foreach (var s in _level.shade) cells.Add(new Cell(s[0],s[1]));
            UpdateCells(ref _shadeOverlay, _shadeCells, "ShadeRule", cells, new Color(.65f,.8f,.95f,.23f));
        }
        GameObject _shadeOverlay;
        readonly List<GameObject> _shadeCells = new List<GameObject>();

        // ─── Overlays ────────────────────────────────────────────────────────

        /// <summary>Toggles the BFS path entry->door overlay for the selected guest.</summary>
        public void ShowPath(Cell door, HashSet<Cell> occupied)
        {
            var path = RuleEvaluator.Path(_level, occupied,
                new Cell(_level.entry[0], _level.entry[1]), door);
            UpdateCells(ref _pathOverlay, _pathCells, "PathOverlay", path, PathColor);
        }

        public void ShowRoute(RouteEvidence route)
        {
            UpdateCells(ref _pathOverlay, _pathCells, "PathOverlay", route?.Cells,
                route != null && route.Reachable ? PathColor : new Color(.95f,.64f,.33f,.62f));
            UpdateCells(ref _routeBreakOverlay, _routeBreakCells, "RouteBreak", route?.BlockingCells,
                new Color(.86f,.36f,.20f,.78f));
        }
        GameObject _routeBreakOverlay;
        readonly List<GameObject> _routeBreakCells = new List<GameObject>();

        public void HidePath()
        { if (_pathOverlay != null) _pathOverlay.SetActive(false); if (_routeBreakOverlay != null) _routeBreakOverlay.SetActive(false); }

        public static Color PreviewColor(RuleReport report)
            => !report.CanCommit ? new Color(1f, .62f, .3f, .55f)
                : report.Issues.Count > 0 ? new Color(.95f, .82f, .46f, .5f)
                : new Color(.55f, .85f, .64f, .5f);

        public void ShowPlacementPreview(Placement placement, RuleReport report)
        {
            var cells = new List<Cell>(RuleEvaluator.Footprint(placement));
            var door = RuleEvaluator.Door(placement);
            if (CampWalkability.Contains(_level, door)) cells.Add(door);
            UpdateCells(ref _previewOverlay, _previewCells, "PlacementPreview", cells, PreviewColor(report));
            if(_previewDoor==null)
                _previewDoor=NewPrimitive(PrimitiveType.Cylinder,"PreviewDoorDot",_overlayRoot,
                    new Vector3(.17f,.018f,.17f),Vector3.zero,null,RuleOverlayLayer,_overlayMaterial);
            _previewDoor.transform.position=BoardMath.CellCenterWorld(_level,door)+Vector3.up*.047f;
            Tint(_previewDoor,new Color(1f,.84f,.26f,.95f));
            _previewDoor.SetActive(CampWalkability.Contains(_level,door));
        }

        public void HidePlacementPreview()
        { if (_previewOverlay != null) _previewOverlay.SetActive(false); if(_previewDoor!=null)_previewDoor.SetActive(false); }

        // Dragging retargets existing cells; no destroy/instantiate or material
        // cloning on every grid boundary. Path finding uses the candidate's occupancy.
        void UpdateCells(ref GameObject root, List<GameObject> pool, string name,
            IReadOnlyList<Cell> cells, Color color)
        {
            if (root == null)
            {
                root = new GameObject(name);
                root.transform.SetParent(_overlayRoot, false);
            }
            root.SetActive(cells != null && cells.Count > 0);
            var count = 0;
            if (cells != null)
                foreach (var cell in cells)
                {
                    if (!CampWalkability.Contains(_level, cell)) continue;
                    if (count == pool.Count)
                    {
                        var node = OverlayCell(name + "_" + count, cell, color);
                        node.transform.SetParent(root.transform, true);
                        pool.Add(node);
                    }
                    var go = pool[count++];
                    go.transform.position = BoardMath.CellCenterWorld(_level, cell) + Vector3.up * (BoardMath.OverlayY + (name == "PlacementPreview" ? .001f : 0f));
                    Tint(go, color);
                    go.SetActive(true);
                }
            for (var i = count; i < pool.Count; i++) pool[i].SetActive(false);
        }

        // ─── Issue / hint visuals ────────────────────────────────────────────

        static readonly Color IssueColor = new Color(1f, 0.55f, 0.32f, 0.5f);
        static readonly Color HintColor = new Color(0.42f, 0.85f, 0.5f, 0.55f);

        /// <summary>Amber cells for the rule a failed check is complaining about.</summary>
        public void ShowIssueCells(Cell[] cells)
            => _issueOverlay = CellsOverlay("IssueOverlay", cells, IssueColor, _issueOverlay);

        public void HideIssueCells()
        {
            if (_issueOverlay == null) return;
            Object.Destroy(_issueOverlay);
            _issueOverlay = null;
        }

        /// <summary>Green cells the advisor suggests for the focused guest.</summary>
        public void ShowHintCells(Cell[] cells)
            => _hintOverlay = CellsOverlay("HintOverlay", cells, HintColor, _hintOverlay);

        /// <summary>Actual tent at the suggested pose; the ground outline carries the hint colour.</summary>
        public void ShowHintGhost(Placement move, string assetId)
        {
            if (_hintGhost != null) Object.Destroy(_hintGhost);
            _hintGhost = null;
            if (move == null || assetId == null) return;
            var go = SpawnModel(assetId, _overlayRoot, Vector3.zero, 0, RuleOverlayLayer);
            if (go == null) return;
            go.transform.localPosition = BoardMath.TentCenter(_level, move.x, move.z);
            go.transform.localEulerAngles = new Vector3(0f, BoardMath.TentYaw(move.rotation), 0f);
            foreach (var collider in go.GetComponentsInChildren<Collider>()) collider.enabled = false;
            TentCloth.Apply(go);
            _hintGhost = go;
            if (_animateOverlays)
            {
                go.transform.localScale *= .92f;
                go.transform.DOScale(go.transform.localScale / .92f, CampMotion.Enter * _overlayPace)
                    .SetEase(CampMotion.Settle).SetUpdate(true).SetLink(go);
            }
        }

        public void HideHint()
        {
            if (_hintOverlay != null) { Object.Destroy(_hintOverlay); _hintOverlay = null; }
            if (_hintGhost != null) { Object.Destroy(_hintGhost); _hintGhost = null; }
        }

        GameObject CellsOverlay(string name, Cell[] cells, Color color, GameObject previous)
        {
            if (previous != null) Object.Destroy(previous);
            if (cells == null || cells.Length == 0) return null;
            var root = new GameObject(name);
            root.transform.SetParent(_overlayRoot, false);
            foreach (var cell in cells)
            {
                var go = OverlayCell(name + "_" + cell, cell, color);
                go.transform.SetParent(root.transform, true);
            }
            if (_animateOverlays)
            {
                root.transform.localScale = Vector3.one * .96f;
                root.transform.DOScale(1f, CampMotion.Enter * _overlayPace)
                    .SetEase(CampMotion.Settle).SetUpdate(true).SetLink(root);
            }
            return root;
        }

        bool _animateOverlays = true;
        float _overlayPace = 1f;
        public void ConfigureMotion(bool reducedMotion, float pace)
        {
            _animateOverlays = !reducedMotion;
            _overlayPace = pace;
        }

        GameObject OverlayCell(string name, Cell cell, Color color)
        {
            // A thin ground frame keeps grass visible and gives a second cue besides colour.
            var go = new GameObject(name); go.layer = RuleOverlayLayer;
            go.transform.SetParent(_overlayRoot, false);
            go.transform.localPosition = BoardMath.CellCenterWorld(_level, cell) + Vector3.up * BoardMath.OverlayY;
            var mesh = new Mesh { name = "Placement ground outline" };
            var vertices = new List<Vector3>(); var triangles = new List<int>();
            void Strip(float x, float z, float sx, float sz)
            {
                int i = vertices.Count;
                vertices.Add(new Vector3(x-sx*.5f,0,z-sz*.5f)); vertices.Add(new Vector3(x-sx*.5f,0,z+sz*.5f));
                vertices.Add(new Vector3(x+sx*.5f,0,z+sz*.5f)); vertices.Add(new Vector3(x+sx*.5f,0,z-sz*.5f));
                triangles.AddRange(new[]{i,i+1,i+2,i,i+2,i+3});
            }
            Strip(-.46f,0,.035f,.95f); Strip(.46f,0,.035f,.95f);
            Strip(0,-.46f,.95f,.035f); Strip(0,.46f,.95f,.035f);
            if (name.StartsWith("RouteBreak"))
            { Strip(0,0,.07f,.60f); Strip(0,0,.60f,.07f); }
            mesh.SetVertices(vertices); mesh.SetTriangles(triangles,0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>(); renderer.sharedMaterial = _overlayMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; renderer.receiveShadows = false;
            go.AddComponent<OwnedCampMesh>().Mesh = mesh;
            Tint(go, color);
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
            presenter.BindViewport(_viewport);
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
            float durationScale = 1f, bool reducedMotion = false)
        {
            _belongingsMotionScale=durationScale;_belongingsReduced=reducedMotion;
            var alive = new HashSet<string>();
            foreach (var p in placements)
            {
                alive.Add(p.guestId);
                if (!_tents.TryGetValue(p.guestId, out var presenter))
                {
                    var guest = System.Array.Find(level.guests, g => g.id == p.guestId);
                    if (guest == null) continue;
                    presenter = PlaceTent(p.guestId, guest.assetId, p);
                    presenter?.Appear(reducedMotion, durationScale);
                }
                else presenter.ApplyPlacement(p, instant: reducedMotion, durationScale);
            }
            var remove = new List<string>();
            foreach (var kv in _tents)
                if (!alive.Contains(kv.Key)) remove.Add(kv.Key);
            foreach (var id in remove)
            {
                _tents[id].Disappear(reducedMotion, durationScale);
                _tents.Remove(id);
            }
            RefreshBelongings(placements,durationScale,reducedMotion);
            _groundDetails?.Rebuild(placements);
        }

        void RefreshBelongings(IReadOnlyList<Placement> placements,float durationScale,bool reduced)
        {
            var reserved=TentBelongingsLayout.Reservations(_level,placements);
            // Actual obstacle bounds supplement occupied cells: large boulders,
            // trunks and firewood can protrude past their logical cell.
            foreach(var renderer in _obstacleRoot.GetComponentsInChildren<Renderer>())
            {
                var b=renderer.bounds;var min=new Vector2(float.MaxValue,float.MaxValue);var max=-min;
                for(int i=0;i<8;i++)
                {
                    var p=_tentRoot.InverseTransformPoint(b.center+Vector3.Scale(b.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1)));
                    min=Vector2.Min(min,new Vector2(p.x,p.z));max=Vector2.Max(max,new Vector2(p.x,p.z));
                }
                reserved.Add(Rect.MinMaxRect(min.x-.07f,min.y-.07f,max.x+.07f,max.y+.07f));
            }
            var sorted=new List<Placement>(placements);sorted.Sort((a,b)=>System.StringComparer.Ordinal.Compare(a.guestId,b.guestId));
            foreach(var p in sorted)if(_tents.TryGetValue(p.guestId,out var tent))
            {
                var story=tent.Root.GetComponent<TentStoryVisual>();if(story==null)continue;
                reserved.Add(story.LivingSpace.Footprint(BoardMath.TentCenter(_level,p.x,p.z),p.rotation));
            }
            foreach(var p in sorted)if(_tents.TryGetValue(p.guestId,out var tent))
            {
                var story=tent.Root.GetComponent<TentStoryVisual>();if(story==null)continue;
                var body=story.LivingSpace.Footprint(BoardMath.TentCenter(_level,p.x,p.z),p.rotation);
                var outdoor=TentBelongingsLayout.Find(_level,p,body,reserved);
                story.Schedule(p,outdoor,_tentRoot,reduced?0:(Mathf.Max(CampMotion.Enter,CampMotion.Change)+.12f)*Mathf.Max(.01f,durationScale),reduced);
            }
        }

        public void ClearAll()
        {
            if (_rim != null) Object.Destroy(_rim.gameObject);
            if (_groundDetails != null) Object.Destroy(_groundDetails);
            _rim = null;
            foreach (var t in _tents.Values) t.Dispose();
            _tents.Clear();
            HidePath();
            HideIssueCells();
            HideHint();
            if(_shadeOverlay!=null) Object.Destroy(_shadeOverlay);
            _tintObjects.Clear();
            if (_pathOverlay != null) Object.Destroy(_pathOverlay);
            if (_previewOverlay != null) Object.Destroy(_previewOverlay);
            if (_previewDoor != null) Object.Destroy(_previewDoor);
            if (_routeBreakOverlay != null) Object.Destroy(_routeBreakOverlay);
            _pathCells.Clear(); _previewCells.Clear();
            if (_overlayMaterial != null) Object.Destroy(_overlayMaterial);
            if (_groundMaterial != null) Object.Destroy(_groundMaterial);
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
            if(layer==GameplayObstacleLayer||layer==TentSelectableLayer||assetId.StartsWith("tree"))RainSurface.Attach(go);
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
            if (color.HasValue) Tint(go, color.Value);
            if (layer == RuleOverlayLayer || layer == BoardSurfaceLayer)
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            go.layer = layer;
            return go;
        }

        static Material _litFallback;
        static Material MakeLitMaterial(Color color)
        {
            if (_litFallback == null)
            {
                _litFallback = new Material(Shader.Find("Universal Render Pipeline/Simple Lit")) { name = "Shared meadow surface" };
                _litFallback.SetColor("_BaseColor", Color.white);
            }
            return _litFallback;
        }

        Material MakeOverlayMaterial()
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            var m = new Material(shader);
            m.SetFloat("_Surface", 1f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.SetFloat("_Blend", 0f);
            m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            return m;
        }

        static readonly MaterialPropertyBlock OverlayTint = new MaterialPropertyBlock();
        static void Tint(GameObject go, Color color)
        {
            var renderer = go.GetComponent<Renderer>();
            if (renderer == null) return;
            OverlayTint.Clear();
            OverlayTint.SetColor("_BaseColor", color);
            renderer.SetPropertyBlock(OverlayTint);
        }
    }
}
