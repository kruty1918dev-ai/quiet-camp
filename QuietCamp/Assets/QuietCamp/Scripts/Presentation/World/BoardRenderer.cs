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
        readonly CampArtDefinition _art;
        readonly AssetCatalog _catalog;
        readonly Transform _baseRoot, _gridRoot, _obstacleRoot, _tentRoot, _overlayRoot;
        readonly List<GameObject> _tintObjects = new List<GameObject>();
        readonly Dictionary<string, TentPresenter> _tents = new Dictionary<string, TentPresenter>();
        float _belongingsMotionScale=1;bool _belongingsReduced;
        BoardRim _rim;
        GameObject _pathOverlay;
        GameObject _previewOverlay;
        GameObject _previewDoor;
        GameObject _campOrientation;
        readonly List<GameObject> _pathCells = new List<GameObject>();
        readonly List<GameObject> _previewCells = new List<GameObject>();
        GameObject _issueOverlay;
        GameObject _hintOverlay;
        GameObject _hintGhost;
        Material _overlayMaterial;
        Material _groundMaterial;
        CampGroundDetails _groundDetails;
        public CampGroundDetails SurfaceDetails=>_groundDetails;

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
            Transform tentRoot, Transform overlayRoot, CampArtDefinition art = null)
        {
            _level = level;
            _art = art;
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
            if(_art==null)_rim = BoardRim.Create(_level, _gridRoot);
            BuildObstacles();
            _groundDetails = _gridRoot.gameObject.AddComponent<CampGroundDetails>();
            _groundDetails.Configure(_level,_groundMaterial,_art);

        }

        // ─── Base ────────────────────────────────────────────────────────────

        void BuildBase()
        {
            if(_art!=null)return;
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
            if (_art != null)
            {
                _groundMaterial.SetColor("_GroundLow", CampArtDefinition.Color(_art.groundLow));
                _groundMaterial.SetColor("_GroundHigh", CampArtDefinition.Color(_art.groundHigh));
                _groundMaterial.SetColor("_SoilTone", CampArtDefinition.Color(_art.soilColor));
                if(_art.atmosphere!=null)
                {
                    var p=_art.atmosphere;var seasonal=SeasonPalette.For(p.season);
                    var low=Color.Lerp(CampArtDefinition.Color(_art.groundLow),seasonal.GrassDark,.32f);
                    var high=Color.Lerp(CampArtDefinition.Color(_art.groundHigh),seasonal.GrassLight,.32f);
                    if(p.snowCover>0){low=Color.Lerp(low,new Color(.67f,.73f,.76f),p.snowCover);high=Color.Lerp(high,new Color(.85f,.90f,.91f),p.snowCover);}
                    _groundMaterial.SetColor("_GroundLow",low);_groundMaterial.SetColor("_GroundHigh",high);
                }
            }
            _groundMaterial.SetFloat("_CellGrid",0);
            if(_art!=null)
            {
                DestroyOwned(_groundMaterial);
                _groundMaterial=new Material(Resources.Load<Shader>("QuietCamp/PlacementGrid")){name="Placement lines above continuous landscape"};
                _groundMaterial.SetVector("_GridOffset",new Vector4(_level.width*.5f,_level.height*.5f,0,0));
                SetPlacementGridVisible(false);
            }
            for (var x = 0; x < _level.width; x++)
            for (var z = 0; z < _level.height; z++)
            {
                var cell = new Cell(x, z);
                var tile = NewPrimitive(PrimitiveType.Cube, $"Cell_{x}_{z}", _gridRoot,
                    new Vector3(1f, 0.014f, 1f),
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
                if (blocked.Contains(cell) || rng.NextDouble() > .82f*season.GrassWeight) continue;
                var p = BoardMath.CellCenterWorld(_level, cell);
                p.x += (float)(rng.NextDouble() * 0.5 - 0.25);
                p.z += (float)(rng.NextDouble() * 0.5 - 0.25);
                p.y = 0.016f;
                var go = SpawnModel("grass", _gridRoot, p, rng.Next(360), DecorLayer);
                if (go != null) { go.name="Camp field grass"; go.transform.localScale *= .38f;Kruty1918.Atmos.FoliageSway.Shared.ApplySlots(go,new[]{Kruty1918.Atmos.FoliageSway.Species.Grass}); }
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
                    if(tree)SeasonalTreeVisual.Apply(model,_level,data.assetId,_art?.atmosphere?.season);
                    if(!tree)
                    {
                        var rs=model.GetComponentsInChildren<Renderer>();var bounds=rs[0].bounds;foreach(var r in rs)bounds.Encapsulate(r.bounds);
                        model.transform.localScale*=Mathf.Min(1,.86f/Mathf.Max(bounds.size.x,bounds.size.z));
                        SeasonalPropVisual.Apply(model,_level,data?.assetId??"stone_largeA",_art?.atmosphere?.season);
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
            if(BakedCampEnvironment.Uses(_level))return; // Already rooted and baked with the real sun.
            var sun = RenderSettings.sun;
            var dir = sun != null ? sun.transform.forward : new Vector3(.4f,-.8f,.4f).normalized;
            // The generated surround owns its trees. Keep rule masks and UI, without a second visual canopy spawner.
            foreach (var crown in _art == null ? _level.canopies ?? System.Array.Empty<ShadeCanopyData>() : System.Array.Empty<ShadeCanopyData>())
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
                foreach (var c in tree.GetComponentsInChildren<Collider>()) DestroyOwned(c);
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
        /// <summary>Tint the existing rule-zone pool; no per-frame mesh rebuild or new placements.</summary>
        public void SetShadeIntroductionStrength(float strength)
        {
            strength=Mathf.Clamp01(strength);
            if(_shadeOverlay==null)return;
            _shadeOverlay.SetActive(strength>0);
            foreach(var node in _shadeCells)
                if(node.activeSelf)Tint(node,new Color(.42f,.83f,.92f,.48f*strength));
        }
        public Vector3? ShadeIntroductionAnchor
        {
            get
            {
                if(_level.shade==null||_level.shade.Length==0)return null;
                var centre=Vector3.zero;
                foreach(var cell in _level.shade)centre+=BoardMath.CellCenterWorld(_level,new Cell(cell[0],cell[1]));
                centre/=_level.shade.Length;
                var best=Vector3.zero;float distance=float.PositiveInfinity;
                foreach(var cell in _level.shade)
                {
                    var point=BoardMath.CellCenterWorld(_level,new Cell(cell[0],cell[1]));
                    float d=(point-centre).sqrMagnitude;
                    if(d<distance){distance=d;best=point;}
                }
                return best+Vector3.up*BoardMath.OverlayY;
            }
        }
        GameObject _shadeOverlay;
        readonly List<GameObject> _shadeCells = new List<GameObject>();
        GameObject _wishOverlay;
        readonly List<GameObject> _wishCells=new List<GameObject>();
        GameObject _noiseOverlay;
        readonly List<GameObject> _noiseCells=new List<GameObject>();
        public void ShowQuietIntroduction(bool visible)
        {
            var safe=new List<Cell>();var noisy=new List<Cell>();
            var blocked=new HashSet<Cell>();
            foreach(var at in _level.blocked)blocked.Add(new Cell(at[0],at[1]));
            foreach(var at in _level.noise)blocked.Add(new Cell(at[0],at[1]));
            if(visible)
                for(int x=0;x<_level.width;x++)for(int z=0;z<_level.height;z++)
                {
                    var cell=new Cell(x,z);
                    if(!blocked.Contains(cell)&&!CampAccess.IsReserved(_level,cell))
                        (RuleEvaluator.IsNoisy(_level,cell)?noisy:safe).Add(cell);
                }
            UpdateCells(ref _shadeOverlay,_shadeCells,"QuietSafe",safe,new Color(.35f,.8f,.6f,.32f));
            UpdateCells(ref _noiseOverlay,_noiseCells,"FireNoise",noisy,new Color(.95f,.43f,.18f,.42f));
        }
        public void SetQuietIntroductionStrength(float strength)
        {
            strength=Mathf.Clamp01(strength);
            if(_shadeOverlay!=null)_shadeOverlay.SetActive(strength>0);
            if(_noiseOverlay!=null)_noiseOverlay.SetActive(strength>0);
            foreach(var node in _shadeCells)if(node.activeSelf)Tint(node,new Color(.35f,.8f,.6f,.32f*strength));
            foreach(var node in _noiseCells)if(node.activeSelf)Tint(node,new Color(.95f,.43f,.18f,.42f*strength));
        }

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
        /// <summary>Reveal the cached route from the real door towards the camp entrance.</summary>
        public void SetPassageIntroductionStrength(RouteEvidence route,float strength,float progress)
        {
            strength=Mathf.Clamp01(strength);progress=Mathf.Clamp01(progress);
            int count=route?.Cells?.Length??0;
            for(int i=0;i<_pathCells.Count&&i<count;i++)
            {
                float reveal=Mathf.Clamp01(progress*count-(count-1-i));
                var color=route.Reachable?new Color(1f,.86f,.36f,.8f):new Color(.95f,.64f,.33f,.7f);
                color.a*=strength*reveal;Tint(_pathCells[i],color);
            }
            foreach(var cell in _routeBreakCells)if(cell.activeSelf)Tint(cell,new Color(.95f,.32f,.2f,.8f*strength));
        }
        GameObject _routeBreakOverlay;
        readonly List<GameObject> _routeBreakCells = new List<GameObject>();

        public void HidePath()
        { if (_pathOverlay != null) _pathOverlay.SetActive(false); if (_routeBreakOverlay != null) _routeBreakOverlay.SetActive(false); }

        public static Color PreviewColor(RuleReport report)
            => !report.CanCommit ? new Color(1f, .28f, .24f, .6f)
                : report.Issues.Count > 0 ? new Color(.95f, .82f, .46f, .5f)
                : new Color(.55f, .85f, .64f, .5f);

        public void ShowPlacementPreview(Placement placement, RuleReport report)
        {
            var centre=BoardMath.TentCenter(_level,placement.x,placement.z);
            var color=PreviewColor(report);
            // One tent-sized outline instead of four separate cell boxes plus a door dot.
            if(_previewOverlay==null)
            {
                _previewOverlay=new GameObject("TentPlacementFootprint");
                _previewOverlay.transform.SetParent(_overlayRoot,false);
                var outline=OverlayCell("TentFootprint",new Cell(0,0),color);
                outline.transform.SetParent(_previewOverlay.transform,true);
                outline.transform.localScale=new Vector3(2,1,2);
                _previewCells.Add(outline);
            }
            _previewOverlay.SetActive(true);
            var footprint=_previewCells[0];
            footprint.transform.position=new Vector3(centre.x,BoardMath.OverlayY+.002f,centre.z);
            Tint(footprint,color);
            if(_groundMaterial.HasProperty("_PlacementFocus"))
                _groundMaterial.SetVector("_PlacementFocus",new Vector4(centre.x,centre.z,3.2f,1));
            if(_previewDoor==null)
                _previewDoor=NewPrimitive(PrimitiveType.Cube,"TentEntranceMat",_overlayRoot,
                    new Vector3(.48f,.008f,.30f),Vector3.zero,null,RuleOverlayLayer,_overlayMaterial);
            // Cardinal doorway direction is independent of the asymmetric logical exit cell.
            var direction=placement.rotation==0?Vector3.forward:placement.rotation==1?Vector3.right:
                placement.rotation==2?Vector3.back:Vector3.left;
            _previewDoor.transform.position=new Vector3(centre.x,BoardMath.OverlayY+.004f,centre.z)+direction*1.12f;
            _previewDoor.transform.rotation=Quaternion.LookRotation(direction);
            Tint(_previewDoor,color);
            _previewDoor.SetActive(true);
        }

        public void HidePlacementPreview()
        { if (_previewOverlay != null) _previewOverlay.SetActive(false); if(_previewDoor!=null)_previewDoor.SetActive(false); }
        public void SetPlacementGridVisible(bool visible)
        {
            if(_groundMaterial==null)return;
            if(_groundMaterial.HasProperty("_GridVisible"))_groundMaterial.SetFloat("_GridVisible",visible?1:0);
            if(_groundMaterial.HasProperty("_CellGrid"))_groundMaterial.SetFloat("_CellGrid",visible?1:0);
        }

        /// <summary>Stable spatial orientation, separate from candidate validity and wish zones.</summary>
        public void ShowCampOrientation(bool visible)
        {
            if(visible&&_campOrientation==null)BuildCampOrientation();
            if(_campOrientation!=null)_campOrientation.SetActive(visible);
        }

        void BuildCampOrientation()
        {
            _campOrientation=new GameObject("CampBoundaryAndExits");
            _campOrientation.transform.SetParent(_overlayRoot,false);
            float halfX=_level.width*.5f,halfZ=_level.height*.5f;
            var exits=new HashSet<Cell>(CampAccess.Points(_level));
            var gaps=new List<Vector2>[] {new List<Vector2>(),new List<Vector2>(),new List<Vector2>(),new List<Vector2>()};
            foreach(var cell in exits)
            {
                var at=BoardMath.CellCenterWorld(_level,cell);
                // Nearest actual edge, not a radial arrow towards an arbitrary board centre.
                float[] distances={Mathf.Abs(at.z+halfZ),Mathf.Abs(at.x-halfX),Mathf.Abs(at.z-halfZ),Mathf.Abs(at.x+halfX)};
                int edge=0;
                for(int i=1;i<4;i++)if(distances[i]<distances[edge])edge=i;
                float along=edge%2==0?at.x:at.z;
                if(distances[edge]<=.6f)gaps[edge].Add(new Vector2(along-.46f,along+.46f));
                var marker=new GameObject("CampExit_"+cell.X+"_"+cell.Z);
                marker.transform.SetParent(_campOrientation.transform,false);
                marker.transform.localPosition=at+Vector3.up*(BoardMath.OverlayY+.006f);
                var outward=edge==0?Vector3.back:edge==1?Vector3.right:edge==2?Vector3.forward:Vector3.left;
                marker.transform.localRotation=Quaternion.LookRotation(outward);
                var color=new Color(1,.96f,.80f,.95f);
                NewPrimitive(PrimitiveType.Cube,"ExitArrowStem",marker.transform,new Vector3(.08f,.006f,.65f),new Vector3(0,0,.10f),color,RuleOverlayLayer,_overlayMaterial);
                var left=NewPrimitive(PrimitiveType.Cube,"ExitArrowLeft",marker.transform,new Vector3(.08f,.006f,.28f),new Vector3(-.08f,0,.50f),color,RuleOverlayLayer,_overlayMaterial);
                var right=NewPrimitive(PrimitiveType.Cube,"ExitArrowRight",marker.transform,new Vector3(.08f,.006f,.28f),new Vector3(.08f,0,.50f),color,RuleOverlayLayer,_overlayMaterial);
                left.transform.localRotation=Quaternion.Euler(0,-45,0);right.transform.localRotation=Quaternion.Euler(0,45,0);
            }
            var vertices=new List<Vector3>();var triangles=new List<int>();
            void Strip(float x,float z,float width,float depth)
            {
                int n=vertices.Count;float y=BoardMath.OverlayY+.003f;
                vertices.Add(new Vector3(x-width*.5f,y,z-depth*.5f));vertices.Add(new Vector3(x-width*.5f,y,z+depth*.5f));
                vertices.Add(new Vector3(x+width*.5f,y,z+depth*.5f));vertices.Add(new Vector3(x+width*.5f,y,z-depth*.5f));
                triangles.AddRange(new[]{n,n+1,n+2,n,n+2,n+3});
            }
            for(int edge=0;edge<4;edge++)
            {
                bool horizontal=edge%2==0;float half=horizontal?halfX:halfZ;
                float fixedAt=edge==0?-halfZ:edge==1?halfX:edge==2?halfZ:-halfX;
                gaps[edge].Sort((a,b)=>a.x.CompareTo(b.x));float start=-half;
                void Segment(float end)
                {
                    end=Mathf.Clamp(end,-half,half);
                    if(end>start)
                    {
                        float mid=(start+end)*.5f;
                        if(horizontal)Strip(mid,fixedAt,end-start,.045f);else Strip(fixedAt,mid,.045f,end-start);
                    }
                }
                foreach(var gap in gaps[edge]){Segment(gap.x);start=Mathf.Max(start,Mathf.Min(half,gap.y));}
                Segment(half);
            }
            // A narrow dark edge keeps the same light border legible on snow as on grass.
            var contrast=new List<Vector3>(vertices);
            for(int i=0;i<vertices.Count;i+=4)
            {
                var centre=(vertices[i]+vertices[i+2])*.5f;
                bool horizontal=vertices[i+2].x-vertices[i].x>vertices[i+2].z-vertices[i].z;
                for(int j=0;j<4;j++)
                {
                    var point=contrast[i+j];point.y-=.001f;
                    if(horizontal)point.z+=Mathf.Sign(point.z-centre.z)*.025f;
                    else point.x+=Mathf.Sign(point.x-centre.x)*.025f;
                    contrast[i+j]=point;
                }
            }
            void BoundaryMesh(string name,List<Vector3> points,Color color,int order)
            {
                var mesh=new Mesh{name=name};
                mesh.SetVertices(points);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
                var line=new GameObject(name);line.transform.SetParent(_campOrientation.transform,false);
                line.AddComponent<MeshFilter>().sharedMesh=mesh;
                var renderer=line.AddComponent<MeshRenderer>();renderer.sharedMaterial=_overlayMaterial;renderer.sortingOrder=order;
                renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;
                line.layer=RuleOverlayLayer;line.AddComponent<OwnedCampMesh>().Mesh=mesh;Tint(line,color);
            }
            BoundaryMesh("CampBoundaryContrast",contrast,new Color(.1f,.2f,.16f,.45f),0);
            BoundaryMesh("CampBoundary",vertices,new Color(1,.96f,.80f,.72f),1);
        }

        public void ShowPlacementGuidance(QuietCamp.Application.CampSession session,string guestId,int rotation)
        {
            var guest=System.Array.Find(_level.guests,g=>g.id==guestId);
            var quietShade=new List<Cell>();
            if(guest?.shade==true||guest?.quiet==true)
                for(int x=0;x<_level.width;x++)for(int z=0;z<_level.height;z++)
                {
                    var cell=new Cell(x,z);
                    bool shaded=System.Array.Exists(_level.shade,s=>s[0]==x&&s[1]==z);
                    if((!guest.shade||shaded)&&(!guest.quiet||!RuleEvaluator.IsNoisy(_level,cell)))quietShade.Add(cell);
                }
            UpdateCells(ref _wishOverlay,_wishCells,"PlacementWish",quietShade,new Color(.49f,.73f,.67f,.13f));
        }
        public void HidePlacementGuidance()
        {
            SetPlacementGridVisible(false);
            if(_wishOverlay!=null)_wishOverlay.SetActive(false);
        }

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
            DestroyOwned(_issueOverlay);
            _issueOverlay = null;
        }

        /// <summary>Green cells the advisor suggests for the focused guest.</summary>
        public void ShowHintCells(Cell[] cells)
            => _hintOverlay = CellsOverlay("HintOverlay", cells, HintColor, _hintOverlay);

        /// <summary>Actual tent at the suggested pose; the ground outline carries the hint colour.</summary>
        public void ShowHintGhost(Placement move, string assetId)
        {
            if (_hintGhost != null) DestroyOwned(_hintGhost);
            _hintGhost = null;
            if (move == null || assetId == null) return;
            var go = SpawnModel(assetId, _overlayRoot, Vector3.zero, 0, RuleOverlayLayer);
            if (go == null) return;
            go.transform.localPosition = BoardMath.TentCenter(_level, move.x, move.z);
            go.transform.localEulerAngles = new Vector3(0f, BoardMath.TentYaw(move.rotation), 0f);
            foreach (var collider in go.GetComponentsInChildren<Collider>()) collider.enabled = false;
            if (UnityEngine.Application.isPlaying) TentCloth.Apply(go);
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
            if (_hintOverlay != null) { DestroyOwned(_hintOverlay); _hintOverlay = null; }
            if (_hintGhost != null) { DestroyOwned(_hintGhost); _hintGhost = null; }
        }

        GameObject CellsOverlay(string name, Cell[] cells, Color color, GameObject previous)
        {
            if (previous != null) DestroyOwned(previous);
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
            if(!name.StartsWith("PlacementWish"))
            {
                Strip(-.46f,0,.035f,.95f); Strip(.46f,0,.035f,.95f);
                Strip(0,-.46f,.95f,.035f); Strip(0,.46f,.95f,.035f);
            }
            else Strip(0,0,1f,1f);
            if(name.StartsWith("ShadeRule")||name.StartsWith("FriendDoors")
                ||name.StartsWith("QuietSafe")||name.StartsWith("FireNoise"))Strip(0,0,.90f,.90f);
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
            if(_art?.atmosphere!=null)
            {
                if(UnityEngine.Application.isPlaying)go.GetComponent<TentCloth>()?.ApplyClimate(_art.atmosphere.season,_art.atmosphere.wetness,_art.atmosphere.snowCover);
                else SeasonalPropVisual.Apply(go,_level,"tent",_art.atmosphere.season);
            }
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

        static void DestroyOwned(UnityEngine.Object value)
        { if (UnityEngine.Application.isPlaying) UnityEngine.Object.Destroy(value); else UnityEngine.Object.DestroyImmediate(value); }

        public void ClearAll()
        {
            if (_rim != null) DestroyOwned(_rim.gameObject);
            if (_groundDetails != null) DestroyOwned(_groundDetails);
            _rim = null;
            foreach (var t in _tents.Values) t.Dispose();
            _tents.Clear();
            HidePath();
            HideIssueCells();
            HideHint();
            if(_shadeOverlay!=null) DestroyOwned(_shadeOverlay);
            if(_noiseOverlay!=null) DestroyOwned(_noiseOverlay);
            if(_wishOverlay!=null) DestroyOwned(_wishOverlay);
            _wishCells.Clear();
            _shadeCells.Clear();_noiseCells.Clear();
            _tintObjects.Clear();
            if (_pathOverlay != null) DestroyOwned(_pathOverlay);
            if (_previewOverlay != null) DestroyOwned(_previewOverlay);
            if (_previewDoor != null) DestroyOwned(_previewDoor);
            if(_campOrientation!=null)DestroyOwned(_campOrientation);
            if (_routeBreakOverlay != null) DestroyOwned(_routeBreakOverlay);
            _pathCells.Clear(); _previewCells.Clear();
            if (_overlayMaterial != null) DestroyOwned(_overlayMaterial);
            if (_groundMaterial != null) DestroyOwned(_groundMaterial);
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
            if(UnityEngine.Application.isPlaying && (layer==GameplayObstacleLayer||layer==TentSelectableLayer||assetId.StartsWith("tree")))RainSurface.Attach(go);
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
            if (collider != null) DestroyOwned(collider);
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
            var shader = Resources.Load<Shader>("QuietCamp/RuleOverlay");
            var m = new Material(shader);
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Overlay;
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
