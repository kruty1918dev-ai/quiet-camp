using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using QuietCamp.Application;
using QuietCamp.Infrastructure;
using QuietCamp.Presentation;
using QuietCamp.Presentation.UI;
using QuietCamp.Presentation.World;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace QuietCamp.Tests
{
    public class CozyPolishPlayModeTests
    {
        static IEnumerator Frames(int count) { for (var i = 0; i < count; i++) yield return null; }
        static void Size(int width, int height) => typeof(ScreenshotPlayModeTest).GetMethod("SetGameViewSize", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { width, height });
        static IEnumerator Shot(string name) => (IEnumerator)typeof(ScreenshotPlayModeTest).GetMethod("Shot", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { Path.Combine(Directory.GetCurrentDirectory(), "Screenshots/CozyPolish"), name });
        static Button Button(string id) => Object.FindObjectsByType<Button>().FirstOrDefault(b => b.name == "<button #" + id + ">" && b.gameObject.activeInHierarchy);
        static void Tap(string id) { var button = Button(id); Assert.NotNull(button, id); Assert.IsTrue(button.interactable, id); button.onClick.Invoke(); }
        static IEnumerator Boot()
        {
            yield return SceneManager.LoadSceneAsync("Boot");
            yield return PrivacyBootTestSupport.EnterGame();
            var deadline = Time.realtimeSinceStartup + 20;
            while (Object.FindAnyObjectByType<QuietCampBootstrap>()?.StartupReady != true && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(Object.FindAnyObjectByType<QuietCampBootstrap>().StartupReady);
            yield return Frames(15);
        }
        [UnityTest] public IEnumerator WideAlbumKeepsTheWorldClearAndSwitchesWithoutAWhiteFlash()
        {
            if (UnityEngine.Application.isBatchMode) Assert.Ignore("Requires rendered Game View");
            Size(1280,800);yield return Boot();var services=QuietCampBootstrap.ServicesRef;
            services.ReducedMotion=true;services.Settings.textScale=1.3f;services.Localization.TrySetLanguage("de");
            services.Save.Album.entries=new[]{"QC003","QC009"}.Select(id=>
            {var level=LevelLoader.Load(id);return new AlbumSaveData.Entry{levelId=id,levelSnapshot=level,contentHash=level.contentHash,placements=level.witness};}).ToArray();
            services.AlbumIndex=0;Tap("album");yield return Frames(40);
            var overlay=Object.FindObjectsByType<HtmlSurface>().Single(s=>s.name=="MenuOverlay");
            foreach(var size in new[]{new Vector2Int(1280,800),new Vector2Int(2560,1600),new Vector2Int(2560,1080)})
            {
                Size(size.x,size.y);yield return Frames(20);yield return Shot("album_clear_"+size.x+"x"+size.y);
                var sheet=overlay.Element("sheet");var viewport=overlay.Element("album-viewport");
                var corners=new Vector3[4];viewport.GetWorldCorners(corners);
                var canvas=sheet.GetComponent<CanvasGroup>();if(canvas==null)canvas=sheet.gameObject.AddComponent<CanvasGroup>();
                float alpha=canvas.alpha,scale=Time.timeScale;
                Texture2D withUi=null,world=null;
                try
                {
                    Time.timeScale=0;yield return Shot("album_comparison_ui");
                    withUi=new Texture2D(2,2);withUi.LoadImage(File.ReadAllBytes(Path.Combine(Directory.GetCurrentDirectory(),"Screenshots/CozyPolish/album_comparison_ui.png")));
                    canvas.alpha=0;yield return Shot("album_comparison_world");
                    world=new Texture2D(2,2);world.LoadImage(File.ReadAllBytes(Path.Combine(Directory.GetCurrentDirectory(),"Screenshots/CozyPolish/album_comparison_world.png")));
                    foreach(float height in new[]{.06f,.50f,.94f})
                    {
                        int px=Mathf.RoundToInt((corners[0].x+corners[2].x)*.5f);
                        int py=Mathf.RoundToInt(Mathf.Lerp(corners[0].y,corners[2].y,height));
                        float difference=0;
                        for(int y=-8;y<=8;y++)for(int x=-8;x<=8;x++)
                        {
                            var a=withUi.GetPixel(px+x,py+y);var b=world.GetPixel(px+x,py+y);
                            difference+=Mathf.Abs(a.r-b.r)+Mathf.Abs(a.g-b.g)+Mathf.Abs(a.b-b.b);
                        }
                        Assert.Less(difference/(17*17*3),.015f,"Album washes out its viewport at "+size+" height="+height);
                    }
                }
                finally{canvas.alpha=alpha;Time.timeScale=scale;if(withUi!=null)Object.Destroy(withUi);if(world!=null)Object.Destroy(world);}
            }
            services.ReducedMotion=false;Tap("album-1");yield return Frames(2);
            var veil=GameObject.Find("AlbumSwapVeil").GetComponent<Image>();Assert.Less(veil.color.grayscale,.4f);
            var album=Object.FindAnyObjectByType<AlbumDiorama>();var deadline=Time.realtimeSinceStartup+10;
            while(album.IsSwitching&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.IsFalse(album.IsSwitching);Assert.AreEqual(1,album.DisplayedIndex);Assert.AreEqual(0,veil.color.a,.001f);
            Assert.AreEqual(1,SceneManager.GetActiveScene().GetRootGameObjects().Count(g=>g.name=="AlbumDioramaWorld"));
            yield return Shot("album_after_switch");Tap("back");yield return Frames(30);
            services.Settings.textScale=1;services.Localization.TrySetLanguage("uk");services.ReducedMotion=false;
        }
        [UnityTest] public IEnumerator VisibleGroundcoverIsPooledBoundedAndDoesNotEnterThePuzzleOrPaths()
        {
            Size(720, 1600); yield return Boot(); var services = QuietCampBootstrap.ServicesRef;
            services.PendingLevelId = "QC003"; services.ReducedMotion = false; services.Save.Session = new SessionSaveData();
            yield return SceneManager.LoadSceneAsync("Camp"); yield return Frames(45);
            var camera = Camera.main; var floor = Object.FindAnyObjectByType<VisibleForestFloor>(); Assert.NotNull(floor);
            var level = CampSceneHost.Current.Session.Level;
            var levelBefore = JsonUtility.ToJson(level);
            foreach (var size in new[] { new Vector2Int(720, 1600), new Vector2Int(1280, 800) })
            {
                Size(size.x, size.y); yield return Frames(15);
                foreach (var quality in new[] { 1, 2, 3 })
                {
                    services.Settings.quality = quality; yield return Frames(20); floor.RefreshNow();
                    Assert.Greater(floor.VisibleTileCount, 4); Assert.LessOrEqual(floor.VisibleTileCount + floor.PooledTileCount, VisibleForestFloor.MaximumTiles);
                    Assert.IsEmpty(floor.GetComponentsInChildren<Collider>(true));
                    var roots = new System.Collections.Generic.HashSet<Vector2>();
                    foreach (var filter in floor.GetComponentsInChildren<MeshFilter>())
                    {
                        var renderer = filter.GetComponent<MeshRenderer>();
                        Assert.AreEqual(UnityEngine.Rendering.ShadowCastingMode.On, renderer.shadowCastingMode); Assert.IsTrue(renderer.receiveShadows);
                        var data = new System.Collections.Generic.List<Vector4>(); filter.sharedMesh.GetUVs(1, data);
                        foreach (var plant in data)
                        {
                            var p = filter.transform.TransformPoint(new Vector3(plant.x, plant.z, plant.y));
                            Assert.IsTrue(Mathf.Abs(p.x) >= level.width * .5f + .55f || Mathf.Abs(p.z) >= level.height * .5f + .55f);
                            Assert.IsFalse(CampTrail.IsCorridor(level, p, .28f)); roots.Add(new Vector2(p.x, p.z));
                        }
                    }
                    Assert.Greater(roots.Count(p => p.magnitude > 8), 60, "Distant view has a bare ring");
                    yield return Shot("forest_" + size.x + "x" + size.y + "_tier" + quality);
                    Debug.Log($"[CozyPolishQA] actual output={Screen.width}x{Screen.height}, tier={quality}, tiles={floor.VisibleTileCount}, plants={roots.Count}");
                }
            }
            var before = floor.GetComponentsInChildren<MeshFilter>(true).Select(f => f.sharedMesh).ToArray();
            var pose = camera.transform.position; camera.transform.position += new Vector3(18, 0, 18); floor.RefreshNow();
            Assert.Greater(floor.PooledTileCount + floor.VisibleTileCount, 0);
            Assert.LessOrEqual(floor.PooledTileCount + floor.VisibleTileCount, VisibleForestFloor.MaximumTiles);
            Assert.IsTrue(floor.GetComponentsInChildren<MeshFilter>(true).Any(f => before.Contains(f.sharedMesh)), "Camera change discarded the pool");
            camera.transform.position = pose; floor.RefreshNow();
            Assert.AreEqual(levelBefore, JsonUtility.ToJson(level));
            CampSceneHost.Current.Session.DebugApplyWitness(); yield return Frames(10);
            yield return Shot("camp_tents");
        }
    }
}
