using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using QuietCamp.Infrastructure;
using QuietCamp.Presentation;
using QuietCamp.Presentation.World;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace QuietCamp.Tests
{
    public class ForestCoveragePlayModeTests
    {
        [SetUp] public void GuardPerformanceStorage()
        {
#if UNITY_EDITOR
            if (UnityEditor.SessionState.GetBool("QcPerf.Active", false) && !UnityEngine.Application.productName.StartsWith("QuietCampPerfQA"))
                Assert.Ignore("Performance driver requires isolated QA saves.");
#endif
        }
        static IEnumerator Frames(int count) { while (count-- > 0) yield return null; }
        static void Size(int width, int height) => typeof(ScreenshotPlayModeTest).GetMethod("SetGameViewSize", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { width, height });
        static IEnumerator Shot(string name) => (IEnumerator)typeof(ScreenshotPlayModeTest).GetMethod("Shot", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { Path.Combine(Directory.GetCurrentDirectory(), "Screenshots/ForestCoverage"), name });
        static bool InView(Camera camera, Vector3 p)
        {
            var uv = camera.WorldToViewportPoint(p);
            return uv.z > 0 && uv.x > 0 && uv.x < 1 && uv.y > 0 && uv.y < 1;
        }
        [UnityTest] public IEnumerator DetailedPlantsContinueThroughVisibleForestAndReuseTilesWhenCameraMoves()
        {
            if (UnityEngine.Application.isBatchMode) Assert.Ignore("Requires rendered Game View");
            Size(720, 1600); yield return SceneManager.LoadSceneAsync("Boot");
            yield return PrivacyBootTestSupport.EnterGame();
            var deadline = Time.realtimeSinceStartup + 25;
            while (Object.FindAnyObjectByType<QuietCampBootstrap>()?.StartupReady != true && Time.realtimeSinceStartup < deadline) yield return null;
            var services = QuietCampBootstrap.ServicesRef; Assert.NotNull(services);
            services.Tutorial.Skip(); services.Tutorial.MarkMenuIntroSeen();
            services.Progression.MarkCompleted("QC001"); services.Progression.MarkCompleted("QC002");
            var adaptive = Object.FindAnyObjectByType<AdaptiveCampQuality>(); if (adaptive != null) adaptive.enabled = false;
            services.PendingLevelId = "QC003"; services.ReducedMotion = true; services.Settings.textScale = 1;
            services.Save.Session = new QuietCamp.Application.SessionSaveData();
            yield return SceneManager.LoadSceneAsync("Camp");
            deadline = Time.realtimeSinceStartup + 20;
            while (CampSceneHost.Current?.UiReady != true && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(CampSceneHost.Current?.UiReady == true, "Camp world and HUD did not finish initialization.");
            var host = CampSceneHost.Current; var level = host.Session.Level;
            var sourceBefore = JsonUtility.ToJson(level); var camera = Camera.main;
            host.Session.DebugApplyWitness(); host.Session.Select(null); yield return Frames(10);
            var layoutBefore = JsonUtility.ToJson(services.Save.Session);
            var floor = Object.FindAnyObjectByType<VisibleForestFloor>(); Assert.NotNull(floor);
            foreach (var size in new[] { new Vector2Int(720, 1600), new Vector2Int(1280, 800), new Vector2Int(2560, 1080) })
            {
                Size(size.x, size.y); yield return Frames(20);
                int previous = 0;
                foreach (int quality in new[] { 1, 2, 3 })
                {
                    services.Settings.quality = quality; yield return Frames(20); floor.RefreshNow();
                    var rich = floor.RichPlantRoots.ToArray();
                    if(previous>0)Assert.AreEqual(previous,rich.Length,"Quality changed the forest silhouette");else Assert.Greater(rich.Length,0); previous = rich.Length;
                    var visible = rich.Where(p => InView(camera, p)).ToArray();
                    AssertGroundCoverage(camera, floor);
                    float Edge(Vector3 p) => Mathf.Max(Mathf.Abs(p.x) - level.width * .5f, Mathf.Abs(p.z) - level.height * .5f);
                    // Landscape framing shows much less ground beyond six metres.
                    // Require real far plants there; enforce density per visible tile below.
                    Assert.Greater(visible.Count(p => Edge(p) > 6), 3, "Only the apron has detailed plants");
                    foreach (var key in floor.VisibleTiles)
                    {
                        var centre = new Vector3((key.x + .5f) * VisibleForestFloor.TileSize, 0, (key.y + .5f) * VisibleForestFloor.TileSize);
                        if (!InView(camera, centre) || Edge(centre) < 4) continue;
                        Assert.Greater(rich.Count(p => Mathf.FloorToInt(p.x / VisibleForestFloor.TileSize) == key.x && Mathf.FloorToInt(p.z / VisibleForestFloor.TileSize) == key.y), 3, "Bare visible forest tile " + key);
                    }
                    foreach (var p in rich)
                    {
                        Assert.Greater(Edge(p), .18f);
                        Assert.IsFalse(CampTrail.IsCorridor(level, p, .28f));
                    }
                    var filters = floor.GetComponentsInChildren<MeshFilter>();
                    Assert.AreEqual(1, filters.Select(f => f.GetComponent<MeshRenderer>().sharedMaterial).Distinct().Count());
                    Assert.LessOrEqual(floor.VisibleTileCount + floor.PooledTileCount, VisibleForestFloor.MaximumTiles);
                    Assert.LessOrEqual(floor.VisibleTriangleCount, floor.VisibleTileCount * 6000);
                    Assert.AreEqual(floor.VisibleTileCount, filters.Length);
                    Assert.IsEmpty(floor.GetComponentsInChildren<Collider>(true));
                    foreach (var filter in filters)
                    {
                        var renderer = filter.GetComponent<MeshRenderer>();
                        Assert.AreEqual(ShadowCastingMode.On, renderer.shadowCastingMode); Assert.IsTrue(renderer.receiveShadows);
                        Assert.AreEqual("QuietCamp/FoliageLit", renderer.sharedMaterial.shader.name);
                        Assert.AreEqual(filter.sharedMesh.vertexCount, filter.sharedMesh.normals.Length);
                        var stream = new List<Vector4>(); filter.sharedMesh.GetUVs(1, stream);
                        Assert.AreEqual(filter.sharedMesh.vertexCount,stream.Count);
                        foreach (var plant in stream.Distinct())
                        {
                            var p = filter.transform.TransformPoint(new Vector3(plant.x,plant.z,plant.y));
                            if(Mathf.Abs(p.x)>=level.width*.5f||Mathf.Abs(p.z)>=level.height*.5f)continue;
                            // The seasonal composer intentionally adds short
                            // grass and occasional flowers to playable cells.
                            // Tall shrubs, blocked cells and access lanes stay clear.
                            Assert.LessOrEqual(plant.w,.1301f,"Tall foliage obscures the puzzle");
                            Assert.IsFalse(CampTrail.IsCorridor(level,p,.12f),"Grass hides the access lane");
                            var cell=BoardMath.CellOf(level,p);
                            Assert.IsFalse(level.blocked.Any(b=>b[0]==cell.X&&b[1]==cell.Z),"Grass grows through a gameplay obstacle");
                        }
                    }
                    yield return Shot(size.x + "x" + size.y + "_tier" + quality);
                    Assert.AreEqual(size.x, Screen.width); Assert.AreEqual(size.y, Screen.height);
                    Debug.Log($"[ForestCoverageQA] {Screen.width}x{Screen.height} quality={quality} tiles={floor.VisibleTileCount} rich={rich.Length} onscreen={visible.Length} triangles={floor.VisibleTriangleCount}");
                    AssertLandscapeBalance(camera, floor, level);
                    // Full far tiles must have greenery in every depth band.
                    // Counting plants per tile alone missed repeated empty strips.
                    var bands = new int[4];
                    foreach (var p in rich)
                    {
                        var key = new Vector2Int(Mathf.FloorToInt(p.x / floor.ActiveTileSize), Mathf.FloorToInt(p.z / floor.ActiveTileSize));
                        var centre = new Vector3((key.x + .5f) * floor.ActiveTileSize, 0, (key.y + .5f) * floor.ActiveTileSize);
                        // Use complete far tiles intersecting the view: a narrow
                        // phone may only show a tile's edge, never its centre.
                        if (Edge(centre) < floor.ActiveTileSize * .5f + 2.75f) continue;
                        int band = Mathf.Clamp(Mathf.FloorToInt(Mathf.Repeat(p.z, floor.ActiveTileSize) / floor.ActiveTileSize * 4), 0, 3);
                        bands[band]++;
                    }
                    Assert.Greater(bands.Sum(), 30);
                    foreach (int band in bands) Assert.Greater(band, bands.Sum() * .10f, "Repeated bare depth band");
                    Debug.Log($"[ForestCoverageQA] depth bands={string.Join(",", bands)}");
                }
            }
            var originalRoots = new HashSet<Vector3>(floor.RichPlantRoots);
            var meshes = floor.GetComponentsInChildren<MeshFilter>(true).Select(f => f.sharedMesh).ToArray();
            var pose = camera.transform.position;
            camera.transform.position += new Vector3(18, 0, 18); floor.RefreshNow();
            Assert.IsFalse(originalRoots.SetEquals(floor.RichPlantRoots));
            Assert.IsTrue(floor.GetComponentsInChildren<MeshFilter>().Any(f => meshes.Contains(f.sharedMesh)), "No pool reuse after camera movement");
            Assert.LessOrEqual(floor.VisibleTileCount + floor.PooledTileCount, VisibleForestFloor.MaximumTiles);
            camera.transform.position = pose; floor.RefreshNow();
            Assert.IsTrue(originalRoots.SetEquals(floor.RichPlantRoots), "Returning regenerated a different forest");
            // Previously the 96-tile cap silently truncated rows in a large view.
            float originalSize = camera.orthographicSize;
            camera.orthographicSize = originalSize * 4; floor.RefreshNow(); yield return Frames(2);
            Assert.Greater(floor.ActiveTileSize, VisibleForestFloor.TileSize);
            AssertGroundCoverage(camera, floor);
            Assert.LessOrEqual(floor.VisibleTileCount + floor.PooledTileCount, VisibleForestFloor.MaximumTiles);
            Assert.LessOrEqual(floor.VisibleTriangleCount, floor.VisibleTileCount * 8500);
            Debug.Log($"[ForestCoverageQA] expanded view tiles={floor.VisibleTileCount} span={floor.ActiveTileSize} triangles={floor.VisibleTriangleCount}");
            yield return Shot("expanded_view");
            camera.orthographicSize = originalSize; floor.RefreshNow();
            Assert.IsTrue(originalRoots.SetEquals(floor.RichPlantRoots));
            // Top rays now look above the horizon. The lower visible forest
            // must update immediately, including after a quick camera move.
            var originalRotation = camera.transform.rotation;
            camera.orthographic = false; camera.fieldOfView = 60;
            camera.transform.SetPositionAndRotation(new Vector3(0, 8, -15), Quaternion.Euler(12, 0, 0));
            floor.RefreshNow(); yield return Frames(2); AssertGroundCoverage(camera, floor);
            Assert.Greater(floor.VisibleRichPlantCount, 50);
            camera.transform.position += new Vector3(12, 0, 0); yield return Frames(2);
            AssertGroundCoverage(camera, floor);
            Debug.Log($"[ForestCoverageQA] horizon view tiles={floor.VisibleTileCount} rich={floor.VisibleRichPlantCount} span={floor.ActiveTileSize}");
            yield return Shot("horizon_view");
            camera.orthographic = true; camera.transform.SetPositionAndRotation(pose, originalRotation);
            floor.RefreshNow(); Assert.IsTrue(originalRoots.SetEquals(floor.RichPlantRoots));
            Assert.AreEqual(sourceBefore, JsonUtility.ToJson(level));
            Assert.AreEqual(layoutBefore, JsonUtility.ToJson(services.Save.Session));
            var owned = floor.GetComponentsInChildren<MeshFilter>(true).Select(f => f.sharedMesh).ToArray();
            var material = floor.GetComponentInChildren<MeshRenderer>().sharedMaterial;
            yield return SceneManager.LoadSceneAsync("MainMenu"); yield return Frames(15);
            Assert.IsTrue(owned.All(m => m == null)); Assert.IsTrue(material == null);
            Assert.IsTrue(CozyVegetationLibrary.Load().plants.All(p => p.mesh != null));
        }
        static void AssertLandscapeBalance(Camera camera, VisibleForestFloor floor, QuietCamp.Domain.LevelData level)
        {
            // Count per visible square metre, not per distance band: the far
            // band has more area, and widescreen crops a different part of it.
            float Edge(Vector3 p) => Mathf.Max(Mathf.Abs(p.x) - level.width * .5f, Mathf.Abs(p.z) - level.height * .5f);
            int nearArea = 0, farArea = 0;
            foreach (var key in floor.VisibleTiles)
            for (float z = .25f; z < floor.ActiveTileSize; z += .5f)
            for (float x = .25f; x < floor.ActiveTileSize; x += .5f)
            {
                var p = floor.transform.TransformPoint(new Vector3(key.x * floor.ActiveTileSize + x, 0, key.y * floor.ActiveTileSize + z));
                float edge = Edge(p);
                if (!InView(camera, p) || CampTrail.IsCorridor(level, p, .35f)) continue;
                if (edge >= .75f && edge < 3) nearArea++;
                if (edge >= 3 && edge < 8) farArea++;
            }
            var accent = Object.FindAnyObjectByType<CozyUnderstory>();
            var accentRoots = new HashSet<Vector3>();
            foreach (var filter in accent.GetComponentsInChildren<MeshFilter>())
            {
                var stream = new List<Vector4>(); filter.sharedMesh.GetUVs(1, stream);
                foreach (var plant in stream) accentRoots.Add(accent.transform.TransformPoint(new Vector3(plant.x, plant.z, plant.y)));
            }
            var roots = floor.RichPlantRoots.Concat(accentRoots).Where(p => InView(camera, p)).ToArray();
            int near = roots.Count(p => Edge(p) >= .75f && Edge(p) < 3);
            int far = roots.Count(p => Edge(p) >= 3 && Edge(p) < 8);
            Assert.Greater(near, 0); Assert.Greater(far, 0); Assert.Greater(nearArea, 0); Assert.Greater(farArea, 0);
            float ratio = (float)near / nearArea / ((float)far / farArea);
            Assert.LessOrEqual(ratio, 2f, "Near vegetation forms a dense wreath instead of a continuous landscape");
            Assert.IsFalse(Object.FindObjectsByType<Transform>().Any(t => t.name == "PlantCluster"), "Extra near-board ring is still being spawned");
            Debug.Log($"[LandscapeBalanceQA] {Screen.width}x{Screen.height} tier={QuietCampBootstrap.ServicesRef.EffectiveQuality} near={near} far={far} densityRatio={ratio:F2}");
        }
        static void AssertGroundCoverage(Camera camera, VisibleForestFloor floor)
        {
            var keys = new HashSet<Vector2Int>(floor.VisibleTiles); int samples = 0;
            var ground = new Plane(floor.transform.up, floor.transform.position);
            var meadow = Object.FindAnyObjectByType<MeadowSurface>(); Assert.NotNull(meadow);
            var meadowBounds = meadow.GetComponent<MeshFilter>().sharedMesh.bounds;
            for (int y = 1; y < 20; y++) for (int x = 1; x < 28; x++)
            {
                var ray = camera.ViewportPointToRay(new Vector3(x / 28f, y / 20f, 0));
                if (!ground.Raycast(ray, out float distance)) continue;
                var world = ray.GetPoint(distance); var viewport = camera.WorldToViewportPoint(world);
                if (viewport.z < camera.nearClipPlane || viewport.z > camera.farClipPlane) continue;
                var p = floor.transform.InverseTransformPoint(world);
                var meadowPoint = meadow.transform.InverseTransformPoint(world);
                Assert.That(meadowPoint.x, Is.InRange(meadowBounds.min.x, meadowBounds.max.x), "Plants extend beyond the ground");
                Assert.That(meadowPoint.z, Is.InRange(meadowBounds.min.z, meadowBounds.max.z), "Plants extend beyond the ground");
                var key = new Vector2Int(Mathf.FloorToInt(p.x / floor.ActiveTileSize), Mathf.FloorToInt(p.z / floor.ActiveTileSize));
                Assert.IsTrue(keys.Contains(key), "Visible ground has no vegetation tile: " + key); samples++;
            }
            Assert.Greater(samples, 50);
        }
    }
}
