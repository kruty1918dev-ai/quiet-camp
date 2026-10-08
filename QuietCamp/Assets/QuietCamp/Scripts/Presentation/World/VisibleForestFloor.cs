using QuietCamp.Infrastructure;
using System.Collections.Generic;
using QuietCamp.Domain;
using UnityEngine;
using UnityEngine.Rendering;

namespace QuietCamp.Presentation.World
{
    /// <summary>Seeded, pooled groundcover tiles cover the camera's actual forest view.
    /// Offscreen tiles are released; no terrain-wide objects, colliders or per-plant Update.</summary>
    [DefaultExecutionOrder(200)]
    public sealed class VisibleForestFloor : MonoBehaviour
    {
        public const float TileSize = 6f;
        public const int MaximumTiles = 96;
        sealed class Tile
        {
            public GameObject root;
            public Mesh mesh;
            public readonly List<Vector3> richRoots = new List<Vector3>();
            public int triangles;
        }
        sealed class PlantShape
        {
            public Vector3[] vertices, normals;
            public int[] triangles;
            public Color[] colors;
            public CozyVegetationLibrary.PlantKind kind;
            public Bounds bounds;
            public float radius;
            public int triangleCount;
            public PlantShape(CozyVegetationLibrary.Plant source)
            {
                var sourceVertices = source.mesh.vertices; var sourceNormals = source.mesh.normals;
                kind = source.kind;
                bounds=source.mesh.bounds;var extents = bounds.extents;
                radius = new Vector2(extents.x, extents.z).magnitude;
                var combinedVertices = new List<Vector3>(); var combinedNormals = new List<Vector3>();
                var combinedColors = new List<Color>(); var combinedTriangles = new List<int>();
                // Resolve material slots once. Moving the camera then copies compact
                // arrays instead of allocating a vertex-remapping dictionary per plant.
                for (int slot = 0; slot < source.mesh.subMeshCount; slot++)
                {
                    var remap = new Dictionary<int, int>();
                    foreach (int index in source.mesh.GetTriangles(slot))
                    {
                        if (!remap.TryGetValue(index, out int mapped))
                        {
                            mapped = combinedVertices.Count; remap.Add(index, mapped);
                            combinedVertices.Add(sourceVertices[index]); combinedNormals.Add(sourceNormals[index]);
                            combinedColors.Add(source.colors[slot]);
                        }
                        combinedTriangles.Add(mapped);
                    }
                }
                vertices = combinedVertices.ToArray(); normals = combinedNormals.ToArray();
                colors = combinedColors.ToArray(); triangles = combinedTriangles.ToArray();
                triangleCount = triangles.Length / 3;
            }
        }
        readonly Dictionary<Vector2Int, Tile> _tiles = new Dictionary<Vector2Int, Tile>();
        readonly Stack<Tile> _pool = new Stack<Tile>();
        readonly HashSet<Vector2Int> _wanted = new HashSet<Vector2Int>();
        readonly List<Vector2Int> _remove = new List<Vector2Int>();
        static CozyVegetationLibrary _cachedLibrary;
        static PlantShape[] _cachedPlants;
        readonly Queue<Vector2Int> _pending = new Queue<Vector2Int>();
        readonly List<Vector3> _buildVertices = new List<Vector3>(20000), _buildNormals = new List<Vector3>(20000);
        readonly List<Color> _buildColors = new List<Color>(20000);
        readonly List<Vector4> _buildRoots = new List<Vector4>(20000);
        readonly List<int> _buildIndices = new List<int>(20000);
        bool _deferred;
        public bool InitialReady => _pending.Count == 0;
        readonly Plane[] _planes = new Plane[6];
        readonly Vector3[] _corners = new Vector3[8];
        Material _material;
        PlantShape[] _plants;
        LevelData _level;
        Camera _camera;
        Matrix4x4 _lastView, _lastGround;
        int _quality = -1;
        float _tileSize = TileSize;
        float _nextRefresh;
        Rect _coverage;
        public int CoverageRefreshCount {get;private set;}
        public float ActiveTileSize => _tileSize;
        public int VisibleTileCount => _tiles.Count;
        public int PooledTileCount => _pool.Count;
        public IEnumerable<Vector2Int> VisibleTiles => _tiles.Keys;
        public int VisibleRichPlantCount
        {
            get { int count = 0; foreach (var tile in _tiles.Values) count += tile.richRoots.Count; return count; }
        }
        public int VisibleTriangleCount
        {
            get { int count = 0; foreach (var tile in _tiles.Values) count += tile.triangles; return count; }
        }
        public IEnumerable<Vector3> RichPlantRoots
        {
            get { foreach (var tile in _tiles.Values) foreach (var root in tile.richRoots) yield return transform.TransformPoint(root); }
        }

        public void Configure(LevelData level, Camera camera, bool deferred = false)
        {
            using var audit = PerformanceAudit.Measure("QC.VisibleForestFloor.Configure");
            if (!ReferenceEquals(_level, level)) {ReleaseTiles();_quality = -1;}
            _level = level; _camera = camera; _deferred = deferred;
            var library = CozyVegetationLibrary.Load();
            if (_plants == null && library != null && library.plants.Length >= 6)
            {
                if (_cachedLibrary != library || _cachedPlants == null)
                {
                    _cachedPlants = new PlantShape[library.plants.Length];
                    for (int i = 0; i < _cachedPlants.Length; i++) _cachedPlants[i] = new PlantShape(library.plants[i]);
                    _cachedLibrary = library;
                }
                _plants = _cachedPlants;
            }
            if (_material == null)
            {
                _material = new Material(Shader.Find("QuietCamp/FoliageLit")) { name = "Visible forest floor (owned)" };
                _material.SetColor("_BaseColor", Color.white);
                _material.SetFloat("_VertexTint", 1); _material.SetFloat("_ClusterWind", 1); _material.SetFloat("_Cull", 0);
                _material.SetFloat("_SwayAmp", .32f); _material.SetFloat("_SwayFreq", .46f);
                _material.SetFloat("_FlutterAmp", .006f); _material.SetFloat("_FlutterFreq", 1.3f);
                _material.SetFloat("_RootLock", .15f); _material.SetFloat("_InnerShade", .22f);
            }
            _material.SetFloat("_SnowCover",SeasonProfile.For(level).Palette.SnowCoverage);
            _material.SetFloat("_Dew",SeasonProfile.For(level).Id=="spring"?.035f:0);
            RefreshNow();
        }
        void LateUpdate()
        {
            using var audit = PerformanceAudit.Measure("QC.VisibleForestFloor.LateUpdate");
            if (_camera == null || _level == null) return;
            if (_pending.Count > 0)
            {
                // One initial tile per frame under the transition cover. This
                // bounds each upload; route readiness waits for the complete floor.
                AddTile(_pending.Dequeue());
                if (_pending.Count == 0) _deferred = false;
                return;
            }
            // Budget micro-motion checks, but a resize, zoom, diorama turn or
            // large camera move cannot leave newly visible ground bare for .2s.
            bool urgent=_camera.projectionMatrix!=_checkedProjection||transform.localToWorldMatrix!=_lastGround
                ||(_camera.transform.position-_checkedPosition).sqrMagnitude>.64f
                ||Quaternion.Angle(_camera.transform.rotation,_checkedRotation)>2;
            if(!urgent&&Time.unscaledTime<_nextRefresh)return;_nextRefresh=Time.unscaledTime+.2f;
            RememberCamera();
            var view = _camera.projectionMatrix * _camera.worldToCameraMatrix;
            if (view != _lastView || transform.localToWorldMatrix != _lastGround
                || _quality != (QuietCampBootstrap.ServicesRef?.EffectiveQuality ?? 1))
            {
                if(!ForestGroundView.TryBounds(_camera,transform,_corners,out var footprint,SeasonProfile.For(_level).Winter?2.3f:ForestGroundView.MaximumHeight))return;
                // Motion inside the padded footprint does not rebuild/cull tile sets.
                if(_quality != (QuietCampBootstrap.ServicesRef?.EffectiveQuality ?? 1)||footprint.xMin<_coverage.xMin||footprint.xMax>_coverage.xMax||footprint.yMin<_coverage.yMin||footprint.yMax>_coverage.yMax)RefreshNow();
                _lastView=view;_lastGround=transform.localToWorldMatrix;
                _quality=QuietCampBootstrap.ServicesRef?.EffectiveQuality??1;
            }
        }
        public void RefreshNow()
        {
            if (_camera == null || _level == null) return;
            int quality = QuietCampBootstrap.ServicesRef?.EffectiveQuality ?? 1;
            _quality = quality;
            _lastView = _camera.projectionMatrix * _camera.worldToCameraMatrix;
            _lastGround = transform.localToWorldMatrix;
            RememberCamera();
            GeometryUtility.CalculateFrustumPlanes(_camera, _planes);
            if (!ForestGroundView.TryBounds(_camera, transform, _corners, out var footprint,SeasonProfile.For(_level).Winter?2.3f:ForestGroundView.MaximumHeight))
            {
                _wanted.Clear(); ReleaseTiles(); return;
            }
            CoverageRefreshCount++;
            _coverage=Rect.MinMaxRect(footprint.xMin-1.2f,footprint.yMin-1.2f,footprint.xMax+1.2f,footprint.yMax+1.2f);
            footprint=Rect.MinMaxRect(footprint.xMin-2,footprint.yMin-2,footprint.xMax+2,footprint.yMax+2);
            // A large view simplifies the entire forest uniformly. Never stop
            // halfway through a row and leave the far side of the screen bare.
            float size = TileSize;
            while (!CollectTiles(footprint, size)) size *= 2;
            if (!Mathf.Approximately(size, _tileSize)) { ReleaseTiles(); _tileSize = size; }
            _remove.Clear();
            foreach (var pair in _tiles) if (!_wanted.Contains(pair.Key)) _remove.Add(pair.Key);
            foreach (var key in _remove) { var tile = _tiles[key]; tile.root.SetActive(false); _pool.Push(tile); _tiles.Remove(key); }
            _pending.Clear();
            foreach (var key in _wanted)
            {
                if (_tiles.ContainsKey(key)) continue;
                if (_deferred && _tiles.Count == 0) _pending.Enqueue(key);
                else AddTile(key);
            }
        }
        void AddTile(Vector2Int key)
        {
            if (_tiles.ContainsKey(key)) return;
            var tile = _pool.Count > 0 ? _pool.Pop() : CreateTile();
            Build(tile, key); tile.root.SetActive(true); _tiles.Add(key, tile);
        }
        Matrix4x4 _checkedProjection;
        Vector3 _checkedPosition;
        Quaternion _checkedRotation;
        void RememberCamera()
        {
            _checkedProjection=_camera.projectionMatrix;
            _checkedPosition=_camera.transform.position;
            _checkedRotation=_camera.transform.rotation;
        }
        bool CollectTiles(Rect footprint, float size)
        {
            _wanted.Clear();
            int x0 = Mathf.FloorToInt(footprint.xMin / size) - 1, x1 = Mathf.FloorToInt(footprint.xMax / size) + 1;
            int z0 = Mathf.FloorToInt(footprint.yMin / size) - 1, z1 = Mathf.FloorToInt(footprint.yMax / size) + 1;
            // Bound CPU work too when the camera zooms far out.
            if ((long)(x1 - x0 + 1) * (z1 - z0 + 1) > MaximumTiles * 4) return false;
            for (int z = z0; z <= z1; z++)
            for (int x = x0; x <= x1; x++)
            {
                var key = new Vector2Int(x, z);
                if (!GeometryUtility.TestPlanesAABB(_planes, ForestGroundView.TileBounds(transform, key, size,SeasonProfile.For(_level).Winter?2.3f:ForestGroundView.MaximumHeight))) continue;
                _wanted.Add(key);
                if (_wanted.Count > MaximumTiles) return false;
            }
            return true;
        }
        void ReleaseTiles()
        {
            _pending.Clear();
            foreach (var tile in _tiles.Values) { tile.root.SetActive(false); _pool.Push(tile); }
            _tiles.Clear();
        }
        Tile CreateTile()
        {
            var root = new GameObject("Forest floor tile", typeof(MeshFilter), typeof(MeshRenderer));
            root.transform.SetParent(transform, false); root.layer = BoardRenderer.DecorLayer;
            var mesh = new Mesh { name = "Pooled forest groundcover" };
            root.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = root.GetComponent<MeshRenderer>(); renderer.sharedMaterial = _material;
            renderer.shadowCastingMode = ShadowCastingMode.On; renderer.receiveShadows = true;
            return new Tile { root = root, mesh = mesh };
        }
        void Build(Tile tile, Vector2Int key)
        {
            using var audit = PerformanceAudit.Measure("QC.VisibleForestFloor.Build");
            tile.root.name = $"Forest floor {key.x},{key.y}";
            var origin = new Vector3(key.x * _tileSize, 0, key.y * _tileSize); tile.root.transform.localPosition = origin;
            var rng = new System.Random(unchecked(_level.decorSeed * 491 + key.x * 73856093 ^ key.y * 19349663));
            var vertices = _buildVertices; var normals = _buildNormals; var colors = _buildColors;
            var roots = _buildRoots; var indices = _buildIndices;
            vertices.Clear(); normals.Clear(); colors.Clear(); roots.Clear(); indices.Clear();
            tile.richRoots.Clear();
            float Next() => (float)rng.NextDouble();
            void Triangle(Vector3 a, Vector3 b, Vector3 c, Color color, Vector4 plant)
            {
                int start = vertices.Count; vertices.Add(a); vertices.Add(b); vertices.Add(c);
                var normal = Vector3.Cross(b - a, c - a).normalized;
                for (int i = 0; i < 3; i++) { normals.Add(normal); colors.Add(color); roots.Add(plant); indices.Add(start + i); }
            }
            var season=SeasonProfile.For(_level);var palette=season.Palette;
            tile.root.GetComponent<MeshRenderer>().shadowCastingMode=season.Winter?ShadowCastingMode.Off:ShadowCastingMode.On;
            int pockets = 54;
            for (int pocket = 0; pocket < pockets&&!season.Winter; pocket++)
            {
                var centre = PocketCentre(pocket, pockets, Next(), Next()); centre.y = .008f;
                float density = PatchDensity(origin + centre);
                for (int shoot = 0; shoot < 3; shoot++)
                {
                    if(Next()>season.GrassWeight)continue;
                    if (shoot > 0 && Next() > density) continue;
                    var root = centre + new Vector3(Next() * .5f - .25f, 0, Next() * .5f - .25f);
                    var world = origin + root;
                    bool inside=Mathf.Abs(world.x)<_level.width*.5f&&Mathf.Abs(world.z)<_level.height*.5f;
                    if (CampTrail.IsCorridor(_level, world, .12f)||Blocked(_level,world)) continue;
                    float height = inside?.055f+Next()*.065f:.12f + Next() * .16f;
                    var groundSpecies=Species(_level,pocket);
                    if(!inside&&groundSpecies=="wheat")height=.46f+Next()*.28f;
                    var plant = new Vector4(root.x, root.z, root.y, height);
                    var green = Color.Lerp(palette.GrassDark,palette.GrassLight,Next());
                    float yaw = Next() * Mathf.PI * 2;
                    for (int blade = 0; blade < 3; blade++)
                    {
                        var direction = new Vector3(Mathf.Cos(yaw + blade * 2.1f), 0, Mathf.Sin(yaw + blade * 2.1f));
                        var side = Vector3.Cross(Vector3.up, direction) * .025f;
                        var middle = root + direction * height * .28f + Vector3.up * height * .5f;
                        var tip = root + direction * height * .48f + Vector3.up * height;
                        Triangle(root - side, middle - side, middle + side, green * .85f, plant);
                        Triangle(root - side, middle + side, root + side, green * .85f, plant);
                        Triangle(middle - side, tip, middle + side, green, plant);
                    }
                    if (pocket % (season.Id=="spring"?4:6) == 0 && shoot == 0 && palette.FlowerWeight>.1f)
                    {
                        var tip = root + Vector3.up * height;
                        var species=Species(_level,pocket);
                        var flower=species=="poppy"?new Color(.83f,.23f,.16f):species=="daisy"?new Color(.97f,.94f,.81f):species=="wheat"?new Color(.76f,.64f,.34f):new Color(.90f,.75f,.40f);
                        int petalCount=species=="daisy"?8:species=="poppy"?5:4;
                        for (int petal = 0; petal < petalCount; petal++)
                        {
                            float angle = petal * Mathf.PI * 2/petalCount;
                            var direction = new Vector3(Mathf.Cos(angle),species=="wheat"?.9f:.15f,Mathf.Sin(angle)) * (inside?.04f:.07f);
                            var side = Vector3.Cross(Vector3.up, direction) * .45f;
                            Triangle(tip, tip + direction - side, tip + direction + side, flower, plant);
                        }
                        if(species=="daisy")Triangle(tip+Vector3.up*.014f,tip+new Vector3(-.025f,.014f,.025f),tip+new Vector3(.025f,.014f,.025f),new Color(.98f,.74f,.24f),plant);
                    }
                }
            }
            // One distribution supplies the clearing edge and distant forest.
            // World-space patches continue across tiles; neither distance from
            // the board nor the camera adds a dense decorative ring.
            if (_plants != null&&!season.Winter)
            {
                var richRandom = new System.Random(unchecked(_level.decorSeed * 1973 + key.x * 83492791 ^ key.y * 297121507));
                float RichNext() => (float)richRandom.NextDouble();
                int clusters = 9;
                int triangleLimit = season.Autumn?5560:6000; // reserve the remaining budget for litter
                for (int cluster = 0; cluster < clusters; cluster++)
                {
                    // Stratified pockets prevent broad bare patches beyond
                    // the apron while retaining irregular natural clumps.
                    var centre = PocketCentre(cluster, clusters, RichNext(), RichNext()); centre.y = .012f;
                    float density = PatchDensity(origin + centre);
                    for (int shoot = 0; shoot < 4; shoot++)
                    {
                        var root = centre + new Vector3(RichNext() * 1.4f - .7f, 0, RichNext() * 1.4f - .7f);
                        var world = origin + root;
                        int id = shoot == 0 ? richRandom.Next(2) : shoot < 3 ? 2 + richRandom.Next(2) : 4 + richRandom.Next(2);
                        var shape = _plants[id];
                        float height = shape.kind == CozyVegetationLibrary.PlantKind.Shrub ? .48f + RichNext() * .34f
                            : shape.kind == CozyVegetationLibrary.PlantKind.Leaf ? .26f + RichNext() * .23f : .27f + RichNext() * .18f;
                        height=CozyVegetationLibrary.GroundcoverScale(shape.kind,shape.bounds,height);
                        float radius = shape.radius * height + height * .36f;
                        float edgeDistance = Mathf.Max(Mathf.Abs(world.x) - _level.width * .5f, Mathf.Abs(world.z) - _level.height * .5f);
                        float weight = Mathf.SmoothStep(0, 1, (edgeDistance - .35f) / .9f);
                        if (shoot > 0) weight *= density;
                        weight*=season.ShrubWeight;
                        if(shape.kind==CozyVegetationLibrary.PlantKind.Flower)weight*=palette.FlowerWeight;
                        float chance = RichNext();
                        var rotation = Quaternion.Euler(0, RichNext() * 360, 0);
                        float tint = RichNext();
                        if (chance >= weight || !CozyUnderstory.CanPlant(_level, world, radius)
                            || CampTrail.IsCorridor(_level, world, .28f) || indices.Count / 3 + shape.triangleCount > triangleLimit) continue;
                        var plant = new Vector4(root.x, root.z, root.y, height);
                        int start = vertices.Count;
                        for (int index = 0; index < shape.vertices.Length; index++)
                        {
                            var color = shape.colors[index];
                            if (color.g > color.r) color = Color.Lerp(palette.GrassDark,palette.GrassLight,tint);
                            vertices.Add(root + rotation * shape.vertices[index] * height);
                            normals.Add(rotation * shape.normals[index]); colors.Add(color); roots.Add(plant);
                        }
                        foreach (int index in shape.triangles) indices.Add(start + index);
                        tile.richRoots.Add(world);
                    }
                }
            }
            if(season.Autumn)
            {
                // A folded diamond catches real light; roots w=-1 keep fallen leaves still.
                int count=Mathf.RoundToInt(110*season.LeafLitter);
                for(int leaf=0;leaf<count;leaf++)
                {
                    var root=new Vector3(Next()*_tileSize,0,Next()*_tileSize);var world=origin+root;
                    if(Blocked(_level,world))continue;
                    bool inside=Mathf.Abs(world.x)<_level.width*.5f&&Mathf.Abs(world.z)<_level.height*.5f;
                    if(CampTrail.IsCorridor(_level,world,.28f)&&Next()<.7f)continue;
                    root.y=inside?.020f:.012f;float angle=Next()*Mathf.PI*2,size=.055f+Next()*.055f;
                    var axis=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*size;
                    var side=new Vector3(-axis.z,0,axis.x)*.55f;var fold=root+Vector3.up*.016f;
                    var color=Color.Lerp(new Color(.44f,.27f,.14f),new Color(.86f,.57f,.22f),Next());
                    var anchor=new Vector4(root.x,root.z,root.y,-1);
                    Triangle(root-axis,fold,root+side,color,anchor);Triangle(root+side,fold,root+axis,color,anchor);
                    Triangle(root+axis,fold,root-side,color*.88f,anchor);Triangle(root-side,fold,root-axis,color*.88f,anchor);
                }
            }
            if(season.Winter)
            {
                // Coherent world-space drifts bury roots, never the compacted puzzle or its paths.
                const int steps=10;var shore=EnvironmentCompositionData.For(_level).shore;
                var snowPoints=new Vector3[(steps+1)*(steps+1)];
                var snowNormals=new Vector3[snowPoints.Length];var snowColors=new Color[snowPoints.Length];
                Vector3 Snow(int x,int z)
                {
                    var p=origin+new Vector3(x*_tileSize/steps,0,z*_tileSize/steps);
                    p.y=.018f+season.SnowDepth(_level,p);return p-origin;
                }
                for(int z=0;z<=steps;z++)for(int x=0;x<=steps;x++)
                {
                    int at=z*(steps+1)+x;var p=Snow(x,z);snowPoints[at]=p;var world=p+origin;
                    // World-space central differences give identical normals at adjoining tile edges.
                    // The triangulation stays cheap without painting each triangle as a separate facet.
                    const float e=.12f;
                    float dx=season.SnowDepth(_level,world+Vector3.right*e)-season.SnowDepth(_level,world-Vector3.right*e);
                    float dz=season.SnowDepth(_level,world+Vector3.forward*e)-season.SnowDepth(_level,world-Vector3.forward*e);
                    snowNormals[at]=new Vector3(-dx,2*e,-dz).normalized;
                    snowColors[at]=Color.Lerp(new Color(.78f,.83f,.91f),new Color(.96f,.97f,1),Mathf.Clamp01(p.y*.6f+.35f));
                    snowColors[at]=Color.Lerp(snowColors[at],palette.Soil*.72f,CampGroundDetails.HeatAt(_level,world));
                }
                bool Clear(Vector3 p)
                {
                    var w=p+origin;
                    return !(Mathf.Abs(w.x)<_level.width*.5f+.2f&&Mathf.Abs(w.z)<_level.height*.5f+.2f)&&!ShorelineGeometry.Contains(shore,w,.18f);
                }
                for(int z=0;z<steps;z++)for(int x=0;x<steps;x++)
                {
                    int a=z*(steps+1)+x,b=a+1,c=(z+1)*(steps+1)+x,d=c+1;
                    if(Clear(snowPoints[a])&&Clear(snowPoints[c])&&Clear(snowPoints[b]))SnowTriangle(a,c,b);
                    if(Clear(snowPoints[b])&&Clear(snowPoints[c])&&Clear(snowPoints[d]))SnowTriangle(b,c,d);
                }
                void SnowTriangle(int a,int b,int c)
                {
                    int start=vertices.Count;
                    foreach(int at in new[]{a,b,c})
                    {vertices.Add(snowPoints[at]);normals.Add(snowNormals[at]);colors.Add(snowColors[at]);roots.Add(new Vector4(0,0,0,-1));indices.Add(start++);}
                }
            }
            tile.mesh.Clear();
            tile.mesh.indexFormat = vertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            tile.mesh.SetVertices(vertices); tile.mesh.SetNormals(normals); tile.mesh.SetColors(colors); tile.mesh.SetUVs(1, roots); tile.mesh.SetTriangles(indices, 0);
            tile.triangles = indices.Count / 3; tile.mesh.RecalculateBounds();
            var bounds = tile.mesh.bounds; bounds.Expand(.7f); tile.mesh.bounds = bounds;
        }
        float PatchDensity(Vector3 position)
        {
            // Low-frequency clumps unrelated to the clearing shape. Keep a
            // sparse base everywhere so airy glades still read as living ground.
            int seed = _level.decorSeed & 1023;
            float patch = Mathf.PerlinNoise(position.x * .13f + seed * .37f,
                position.z * .13f + seed * .61f);
            return Mathf.Lerp(.4f, 1f, Mathf.SmoothStep(0, 1, (patch - .25f) * 2));
        }
        static bool Blocked(LevelData level,Vector3 world)
        {
            var cell=BoardMath.CellOf(level,world);
            foreach(var blocked in level.blocked??System.Array.Empty<int[]>())
                if(blocked?.Length==2&&blocked[0]==cell.X&&blocked[1]==cell.Z)return true;
            var shore=EnvironmentCompositionData.For(level).shore;
            // Use the actual curved band and keep both banks alive.
            return ShorelineGeometry.Contains(shore,world,.8f);
        }
        static string Species(LevelData level,int index)
        {
            var species=EnvironmentCompositionData.For(level).meadowSpecies;
            if(species==null||species.Length==0)return "grass";
            return species[Mathf.Abs(index)%species.Length];
        }
        Vector3 PocketCentre(int index, int count, float jitterX, float jitterZ)
        {
            // Every row spans the full tile even when count is not a square.
            // An incomplete ceil(sqrt(count)) grid left a repeated bare strip.
            int rows = Mathf.FloorToInt(Mathf.Sqrt(count)), smallRow = count / rows, extras = count % rows;
            int row = 0, columns = smallRow + (extras > 0 ? 1 : 0);
            while (index >= columns)
            {
                index -= columns; row++; columns = smallRow + (row < extras ? 1 : 0);
            }
            return new Vector3((index + .2f + jitterX * .6f) * _tileSize / columns, 0,
                (row + .2f + jitterZ * .6f) * _tileSize / rows);
        }
        void OnDestroy()
        {
            foreach (var tile in _tiles.Values) if (tile.mesh != null) Destroy(tile.mesh);
            foreach (var tile in _pool) if (tile.mesh != null) Destroy(tile.mesh);
            if (_material != null) Destroy(_material);
        }
    }
}
