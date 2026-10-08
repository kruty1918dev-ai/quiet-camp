#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Reflection;
using System.Linq;
using QuietCamp.Infrastructure;
using QuietCamp.Presentation.UI;
using QuietCamp.Presentation.World;
using UnityEngine.SceneManagement;
using NUnit.Framework;
using QuietCamp.Presentation;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace QuietCamp.Tests
{
    public class OptimizationPlayModeTests
    {
        [SetUp] public void RequireIsolatedStorage()
        {
            if (!UnityEngine.Application.productName.StartsWith("QuietCampPerfQA"))
                Assert.Ignore("Optimization checks require isolated QA saves.");
        }
        [UnityTest] public IEnumerator CachedMapRetainsGeometryDuringScrollAndRefreshesWeather()
        {
            yield return SceneManager.LoadSceneAsync("Boot"); yield return PrivacyBootTestSupport.EnterGame();
            var services = QuietCampBootstrap.ServicesRef;
            services.Tutorial.Skip(); services.Tutorial.MarkMenuIntroSeen();
            services.Progression.Restore(LevelLoader.MvpLevelIds().ToArray(), "QC007", services.Progression.CosmeticFlags);
            services.ReducedMotion = true;
            if (MenuSceneHost.Current == null) yield return SceneManager.LoadSceneAsync("MainMenu");
            float menuDeadline = Time.realtimeSinceStartup+30;
            while (MenuSceneHost.Current?.UiReady != true && Time.realtimeSinceStartup < menuDeadline) yield return null;
            Assert.IsTrue(MenuSceneHost.Current?.UiReady == true);
            var host = MenuSceneHost.Current;
            var screens = (MenuScreens)typeof(MenuSceneHost).GetField("_screens", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(host);
            typeof(ScreenshotPlayModeTest).GetMethod("SetGameViewSize", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] { 720, 1600 });
            screens.Show("Main"); for (int i=0;i<20;i++) yield return null;
            yield return CaptureUi("main-menu");
            screens.Show("Settings"); for (int i=0;i<20;i++) yield return null;
            yield return CaptureUi("settings");
            screens.Show("Levels");
            for (int i=0;i<30;i++) yield return null;
            var map = Object.FindAnyObjectByType<RoadmapGraphic>(); Assert.NotNull(map);
            Assert.AreEqual(CampContent.Summaries.Count, map.Scenes.Count);
            map.SetWeatherMoment(25); yield return null; yield return null;
            var scroll = map.GetComponentInParent<ScrollRect>(); Assert.NotNull(scroll);
            var glade = map.GladePool.First(g => g.gameObject.activeSelf);
            int index = glade.SceneIndex;
            var vertices = glade.canvasRenderer.GetMesh().vertices;
            scroll.verticalNormalizedPosition -= .0001f; scroll.velocity = Vector2.zero;
            yield return null; yield return null;
            Assert.AreEqual(index, glade.SceneIndex, "Visible slot ownership changed on a tiny scroll.");
            CollectionAssert.AreEqual(vertices, glade.canvasRenderer.GetMesh().vertices);
            int revision = map.WeatherRevision;
            map.SetWeatherMoment(80); yield return null; yield return null;
            Assert.Greater(map.WeatherRevision, revision);
            foreach (var size in new[] { new Vector2Int(720,1600), new Vector2Int(1280,800), new Vector2Int(3440,1440) })
            {
                typeof(ScreenshotPlayModeTest).GetMethod("SetGameViewSize", BindingFlags.Static | BindingFlags.NonPublic)
                    .Invoke(null, new object[] { size.x, size.y });
                for (int i=0;i<12;i++) yield return null;
                foreach (float position in new[] { 1f, .5f, 0f })
                {
                    scroll.verticalNormalizedPosition = position; scroll.velocity = Vector2.zero;
                    for (int i=0;i<6;i++) yield return null;
                    if (size.x == 720 && position == .5f) yield return CaptureUi("roadmap");
                    Assert.AreSame(scroll, map.GetComponentInParent<ScrollRect>(), "Virtualization replaced the active scroll viewport.");
                    Assert.That(scroll.verticalNormalizedPosition, Is.EqualTo(position).Within(.003f), "Reconciliation lost the scroll position.");
                    int buttons = screens.Overlay.GetComponentsInChildren<Button>().Count(button => button.name.StartsWith("<button #level-"));
                    Assert.That(buttons, Is.InRange(1, 26), "Only the visible map targets and overscan should be mounted.");
                    var ids = LevelLoader.MvpLevelIds();
                    float top = (1-position)*(scroll.content.rect.height-scroll.viewport.rect.height);
                    for (int i=0;i<ids.Count;i++)
                    {
                        float y = RoadmapLayout.MainY(i)+100;
                        if (y > top && y < top+scroll.viewport.rect.height)
                            Assert.NotNull(screens.Overlay.Element("level-"+i), "A visible level target is missing: "+i);
                    }
                    Assert.Greater(map.VisibleVertices, 1000);
                    Assert.LessOrEqual(map.GladePool.Count, 15);
                    Assert.AreEqual(0, map.TruncatedModels);
                    foreach (var active in map.GladePool.Where(g => g.gameObject.activeSelf))
                    {
                        var mesh = active.canvasRenderer.GetMesh(); Assert.NotNull(mesh);
                        Assert.That(mesh.vertexCount, Is.InRange(1, 64999));
                        Assert.IsTrue(active.canvasRenderer.GetMaterial().shader.isSupported);
                        foreach (var vertex in mesh.vertices) Assert.IsTrue(float.IsFinite(vertex.x) && float.IsFinite(vertex.y));
                    }
                }
            }
            // Use the actual reconciled native button at the far end of the
            // campaign; an old callback must not launch an earlier pooled node.
            int last = LevelLoader.MvpLevelIds().Count-1;
            var target = screens.Overlay.Element("level-"+last); Assert.NotNull(target);
            target.GetComponent<Button>().onClick.Invoke();
            float deadline = Time.realtimeSinceStartup+30;
            while ((CampSceneHost.Current == null || !CampSceneHost.Current.UiReady) && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.NotNull(CampSceneHost.Current);
            Assert.AreEqual(LevelLoader.MvpLevelIds()[last], CampSceneHost.Current.Session.Level.id);
            while (!FoliageDiveTransition.Ensure(services).IsIdle && Time.realtimeSinceStartup < deadline) yield return null;
            typeof(ScreenshotPlayModeTest).GetMethod("SetGameViewSize", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] { 720, 1600 });
            for (int i=0;i<20;i++) yield return null;
            yield return CaptureUi("camp");
        }
        static IEnumerator CaptureUi(string name)
        {
            yield return new WaitForEndOfFrame();
            var texture = ScreenCapture.CaptureScreenshotAsTexture();
            try
            {
                var directory = Path.Combine(Directory.GetCurrentDirectory(), "Screenshots/Optimization2026-10-09"); Directory.CreateDirectory(directory);
                File.WriteAllBytes(Path.Combine(directory, name+".png"), texture.EncodeToPNG());
            }
            finally { Object.Destroy(texture); }
        }
        [UnityTest] public IEnumerator GpuLeavesCoverViewportKeepMovingAndReuseGeometry()
        {
            typeof(ScreenshotPlayModeTest).GetMethod("SetGameViewSize", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] { 720, 1600 });
            var root = new GameObject("Leaf shader QA", typeof(RectTransform), typeof(Canvas));
            var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 30000;
            RectTransform Stretch(string name)
            {
                var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(root.transform, false);
                rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; return rect;
            }
            Stretch("Pinholes probe").gameObject.AddComponent<Image>().color = Color.magenta;
            var graphic = Stretch("Leaves").gameObject.AddComponent<LeafCurtainGraphic>(); graphic.raycastTarget = false;
            var directory = Path.Combine(Directory.GetCurrentDirectory(), "Screenshots/Optimization2026-10-09"); Directory.CreateDirectory(directory);
            try
            {
                string prior = null;
                foreach (int tier in new[] { 0, 1, 2 })
                foreach (float clock in new[] { 0f, .85f, 2.7f, 7.3f, 15f })
                {
                    graphic.ConfigureQuality(tier); graphic.SetFrame(1, new Color(.35f,.45f,.38f), clock);
                    Canvas.ForceUpdateCanvases(); yield return null; yield return new WaitForEndOfFrame();
                    Assert.AreEqual("QuietCamp/UI/LeafFlight", graphic.canvasRenderer.GetMaterial().shader.name);
                    var texture = ScreenCapture.CaptureScreenshotAsTexture();
                    try
                    {
                        Assert.AreEqual(720, texture.width); Assert.AreEqual(1600, texture.height);
                        for (int y = 0; y <= 16; y++) for (int x = 0; x <= 16; x++)
                        {
                            var color = texture.GetPixel(Mathf.Clamp(x*(texture.width-1)/16,0,texture.width-1), Mathf.Clamp(y*(texture.height-1)/16,0,texture.height-1));
                            Assert.IsFalse(color.r > .95f && color.b > .95f && color.g < .03f, $"Shader pinhole: tier={tier}, clock={clock}, sample={x},{y}");
                            Assert.IsFalse(color.maxColorComponent < .02f, "Shader produced an empty black frame.");
                        }
                        var bytes = texture.EncodeToPNG();
                        var hash = System.Convert.ToBase64String(System.Security.Cryptography.SHA256.Create().ComputeHash(bytes));
                        if (prior != null) Assert.AreNotEqual(prior, hash, "The covered leaves stopped swaying."); prior = hash;
                        if (clock == 7.3f) File.WriteAllBytes(Path.Combine(directory, "leaves-tier"+tier+"-covered.png"), bytes);
                    }
                    finally { Object.Destroy(texture); }
                }
                var before = graphic.canvasRenderer.GetMesh().vertices;
                graphic.SetFrame(.5f, Color.green, 16); yield return null;
                CollectionAssert.AreEqual(before, graphic.canvasRenderer.GetMesh().vertices, "Flight must reuse the immutable mesh.");
                foreach (float phase in new[] { .5f, 1.5f })
                {
                    graphic.SetFrame(phase, new Color(.35f,.45f,.38f), 16); yield return null; yield return new WaitForEndOfFrame();
                    var texture = ScreenCapture.CaptureScreenshotAsTexture();
                    File.WriteAllBytes(Path.Combine(directory, "leaves-phase"+phase.ToString("0.0",System.Globalization.CultureInfo.InvariantCulture)+".png"),texture.EncodeToPNG());
                    Object.Destroy(texture);
                }
                foreach(float endpoint in new[]{0f,2f})
                {
                    graphic.SetFrame(endpoint, Color.green, 17); Canvas.ForceUpdateCanvases();
                    Assert.AreEqual(0, graphic.canvasRenderer.GetMesh().vertexCount);
                }
            }
            finally { Object.Destroy(root); }
        }
    }
}
#endif
