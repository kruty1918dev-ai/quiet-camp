using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using QuietCamp.Application;
using QuietCamp.Infrastructure;
using QuietCamp.Presentation;
using QuietCamp.Presentation.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace QuietCamp.Tests
{
    public sealed class ProceduralRoadmapPlayModeTests
    {
#if UNITY_EDITOR
        bool _async;
        [SetUp] public void FinishedShaders() { _async=UnityEditor.ShaderUtil.allowAsyncCompilation;UnityEditor.ShaderUtil.allowAsyncCompilation=false; }
        [TearDown] public void RestoreShaders()=>UnityEditor.ShaderUtil.allowAsyncCompilation=_async;
#endif
        static IEnumerator Frames(int n) { while(n-->0)yield return null; }
        static void Size(int w,int h)=>typeof(ScreenshotPlayModeTest).GetMethod("SetGameViewSize",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{w,h});
        static IEnumerator Shot(string name)=>(IEnumerator)typeof(ScreenshotPlayModeTest).GetMethod("Shot",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{Path.Combine(Directory.GetCurrentDirectory(),"Screenshots/ProceduralRoadmap"),name});
        static Button Find(string id)=>Object.FindObjectsByType<Button>().FirstOrDefault(b=>b.name=="<button #"+id+">"&&b.gameObject.activeInHierarchy);
        // Boot may resume straight into the map (PendingMenuScreen="Levels" is
        // set when a level was visited); tap the nav button only when needed.
        static RoadmapGraphic Map()
            =>Object.FindObjectsByType<HtmlSurface>().FirstOrDefault(s=>s.name=="MenuOverlay")?.GetComponentInChildren<RoadmapGraphic>();
        // Boot may resume straight into the map (PendingMenuScreen="Levels" is
        // set when a level was visited); tap the nav button only when needed.
        static IEnumerator OpenLevels(int frames=120)
        {
            bool tapped=false;
            while(frames-->0)
            {
                if(Map()!=null)yield break;
                if(!tapped&&Find("levels")!=null){Tap("levels");tapped=true;}
                yield return null;
            }
            if(Map()==null)Debug.Log("[MapQA] map missing; scene="+SceneManager.GetActiveScene().name
                +" surfaces="+string.Join(",",Object.FindObjectsByType<HtmlSurface>().Select(s=>s.name+"(active="+s.gameObject.activeInHierarchy+",scroll="+(s.Element("roadmap-scroll")!=null)+")"))
                +" graphics="+Object.FindObjectsByType<RoadmapGraphic>(FindObjectsInactive.Include).Length);
            Assert.NotNull(Map(),"levels map");
        }
        static void Tap(string id)
        {
            var button=Find(id);Assert.NotNull(button,id);var rect=(RectTransform)button.transform;
            var p=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center));
            var pointer=new PointerEventData(EventSystem.current){position=p};var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(pointer,hits);
            Assert.That(p.x,Is.InRange(1,Screen.width-1),id);Assert.That(p.y,Is.InRange(1,Screen.height-1),id);
            Assert.IsTrue(hits.Count>0&&(hits[0].gameObject.transform==rect||hits[0].gameObject.transform.IsChildOf(rect)),"Covered: "+id);
            ExecuteEvents.Execute(button.gameObject,pointer,ExecuteEvents.pointerClickHandler);
        }
        [UnityTest,Timeout(240000)] public IEnumerator GenerativeMapHasWeatherDepthBoundedGeometryAndWorkingNavigationAcrossScreens()
            =>GenerativeMapForSize(new Vector2Int(720,1600));
        [UnityTest,Timeout(240000)] public IEnumerator GenerativeMapOnHighResolutionPhone()=>GenerativeMapForSize(new Vector2Int(1080,1920));
        [UnityTest,Timeout(240000)] public IEnumerator GenerativeMapOnTablet()=>GenerativeMapForSize(new Vector2Int(1280,800));
        [UnityTest,Timeout(240000)] public IEnumerator GenerativeMapOnUltrawide()=>GenerativeMapForSize(new Vector2Int(2560,1080));
        IEnumerator GenerativeMapForSize(Vector2Int viewportSize)
        {
            if(UnityEngine.Application.isBatchMode)Assert.Ignore("Requires rendered Game View");
            Size(720,1600);yield return SceneManager.LoadSceneAsync("Boot");
            yield return PrivacyBootTestSupport.EnterGame();var deadline=Time.realtimeSinceStartup+30;
            while(Object.FindAnyObjectByType<QuietCampBootstrap>()?.StartupReady!=true&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.IsTrue(Object.FindAnyObjectByType<QuietCampBootstrap>().StartupReady);yield return Frames(20);
            var services=QuietCampBootstrap.ServicesRef;
            services.Tutorial.Skip();
            var completed=services.Progression.CompletedIds.ToArray();var last=services.Progression.LastLevelId;var flags=services.Progression.CosmeticFlags;
            var settings=Newtonsoft.Json.JsonConvert.SerializeObject(services.Settings);
            services.Progression.Restore(null,null,0);services.Settings.textScale=1.3f;services.ReducedMotion=true;services.LevelMapScroll=-1;
            foreach(var size in new[]{viewportSize})
            {
                Size(size.x,size.y);yield return Frames(16);yield return OpenLevels();yield return Frames(20);
                foreach(var language in new[]{"uk","en","de"})
                {
                    services.Localization.TrySetLanguage(language);yield return Frames(14);Canvas.ForceUpdateCanvases();
                    var overlay=Object.FindObjectsByType<HtmlSurface>().Single(s=>s.name=="MenuOverlay");
                    var map=overlay.GetComponentInChildren<RoadmapGraphic>();Assert.NotNull(map);
                    Assert.AreEqual(270,map.Scenes.Count);
                    Assert.AreEqual(110,overlay.GetComponentsInChildren<Button>(true).Count(b=>b.name.StartsWith("<button #level-")));
                    Assert.AreEqual(BonusCampCatalog.Slots.Count,overlay.GetComponentsInChildren<Button>(true).Count(b=>b.name.StartsWith("<button #bonus-")&&b.name!="<button #bonus-play>"));
                    var scroll=overlay.Element("roadmap-scroll").GetComponent<ScrollRect>();
                    foreach(float fraction in new[]{1f,.65f,.35f,0f})
                    {
                        scroll.verticalNormalizedPosition=fraction;scroll.velocity=Vector2.zero;yield return Frames(6);
                        var mesh=map.canvasRenderer.GetMesh();Assert.Greater(map.VisibleVertices,1000);Assert.Less(mesh.vertexCount,60000);
                        foreach(var chunk in map.GladePool.Where(g=>g.gameObject.activeSelf))
                            Assert.Less(chunk.canvasRenderer.GetMesh().vertexCount,16000,"An individual glade exceeded its geometry budget");
                        Assert.That(map.VisibleGlades,Is.InRange(1,15));Assert.Greater(map.PaintedModels,10);Assert.Greater(map.ShadowCasters,5);
                        Assert.AreEqual(0,map.TruncatedModels,"Visible models were cut off by the vertex budget");
                        Debug.Log("[ProceduralMapQA] "+size+" "+language+" scroll="+fraction+" vertices="+map.VisibleVertices+" glades="+map.VisibleGlades+" pool="+map.GladePool.Count+" models="+map.PaintedModels+" shadows="+map.ShadowCasters);
                    }
                    scroll.verticalNormalizedPosition=1;scroll.velocity=Vector2.zero;yield return Frames(5);
                    services.ReducedMotion=false;
                    foreach(int tier in new[]{0,1,2})
                    {
                        scroll.verticalNormalizedPosition=1;scroll.velocity=Vector2.zero;
                        services.Settings.quality=tier+1;services.EffectiveQuality=tier;map.SetWeatherMoment(25);yield return Frames(6);
                        var weather=map.GetComponentInChildren<RoadmapWeatherGraphic>();Assert.AreEqual(0,weather.RainGlades);
                        if(tier>0)Assert.Greater(weather.SunlitGlades,0);
                        if(language=="uk"&&tier==1)yield return Shot("sun_"+size.x+"x"+size.y);
                        int rainy=Enumerable.Range(0,map.Scenes.Count).First(i=>map.SceneAt(i).Level.environment?.weatherId=="rain");
                        float viewport=scroll.viewport.rect.height;
                        scroll.verticalNormalizedPosition=1-Mathf.Clamp01((RoadmapLayout.MainY(rainy)-viewport*.5f)/(scroll.content.rect.height-viewport));
                        scroll.velocity=Vector2.zero;map.SetWeatherMoment(80);yield return Frames(6);Assert.Greater(weather.RainGlades,0);
                        Assert.Greater(weather.RainDrops,0);Assert.AreEqual(0,map.TruncatedModels);
                        if(language=="uk")yield return Shot("rain_tier"+tier+"_"+size.x+"x"+size.y);
                    }
                    // Start animation and verify the weather mesh stays bounded.
                    services.ReducedMotion=false;map.SetWeatherMoment(-1);yield return Frames(8);
                    Assert.Less(map.GetComponentInChildren<RoadmapWeatherGraphic>().canvasRenderer.GetMesh().vertexCount,5000);
                    if(language=="de")yield return Shot("cycle_"+size.x+"x"+size.y+"_de");
                    services.ReducedMotion=true;
                    map.SetWeatherMoment(80);yield return Frames(6);Assert.AreEqual(0,map.GetComponentInChildren<RoadmapWeatherGraphic>().RainDrops);
                    // Eight completions put the first glade (after level 10)
                    // inside the reveal horizon so its preview can open.
                    var revealIds=LevelLoader.MvpLevelIds();
                    services.Progression.Restore(revealIds.Take(8),revealIds[7],0);yield return Frames(2);
                    var slot=BonusCampCatalog.Slots[0];float height=scroll.viewport.rect.height;
                    scroll.verticalNormalizedPosition=1-Mathf.Clamp01((RoadmapLayout.BonusY(slot)-height*.4f)/(scroll.content.rect.height-height));
                    scroll.velocity=Vector2.zero;yield return Frames(6);float remembered=scroll.verticalNormalizedPosition;
                    if(language=="uk")yield return Shot("bonus_"+size.x+"x"+size.y);
                    Tap("bonus-10");yield return Frames(12);Assert.NotNull(overlay.GetComponentInChildren<BonusCampPreviewGraphic>());
                    Assert.IsFalse(Find("bonus-play").interactable);Tap("back");yield return Frames(14);
                    scroll=overlay.Element("roadmap-scroll").GetComponent<ScrollRect>();Assert.AreEqual(remembered,scroll.verticalNormalizedPosition,.02f,$"scroll restore: remembered={remembered:R} now={scroll.verticalNormalizedPosition:R} saved={services.LevelMapScroll:R}");
                    Assert.AreEqual(8,services.Progression.CompletedCount,"Preview must not grant progress");
                    services.Progression.Restore(null,null,0);
                }
                Tap("back");yield return Frames(12);
                Assert.IsEmpty(Object.FindObjectsByType<RoadmapGraphic>());Assert.IsEmpty(Object.FindObjectsByType<RoadmapWeatherGraphic>());
            }
            // The enlarged miniature must still open its actual current level.
            services.LevelMapScroll=-1;services.Localization.TrySetLanguage("uk");yield return OpenLevels();yield return Frames(18);
            Tap("level-0");deadline=Time.realtimeSinceStartup+20;
            while(SceneManager.GetActiveScene().name!="Camp"&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.AreEqual("Camp",SceneManager.GetActiveScene().name);yield return Frames(12);
            Assert.AreEqual("QC001",QuietCamp.Presentation.World.CampSceneHost.Current.Session.Level.id);
            // Leave no suspended run behind: a stored session would resume into
            // the Camp scene on the next boot instead of showing the menu.
            services.Save.Session=new SessionSaveData();services.Save.Save();
            services.Progression.Restore(completed,last,flags);
            Newtonsoft.Json.JsonConvert.PopulateObject(settings,services.Settings);
        }
        [UnityTest,Timeout(240000)] public IEnumerator ShoresRemnantsAndWinterSnowStayVisibleAcrossPhoneAndWideViews()
        {
            if(UnityEngine.Application.isBatchMode)Assert.Ignore("Requires rendered Game View");
            Size(720,1600);yield return SceneManager.LoadSceneAsync("Boot");yield return PrivacyBootTestSupport.EnterGame();yield return Frames(30);
            var services=QuietCampBootstrap.ServicesRef;Assert.NotNull(services);services.Tutorial.Skip();services.ReducedMotion=false;
            yield return OpenLevels();yield return Frames(15);
            var overlay=Object.FindObjectsByType<HtmlSurface>().Single(s=>s.name=="MenuOverlay");
            var map=overlay.GetComponentInChildren<RoadmapGraphic>();var scroll=overlay.Element("roadmap-scroll").GetComponent<ScrollRect>();
            int cameras=Object.FindObjectsByType<Camera>().Length;
            foreach(var size in new[]{new Vector2Int(720,1600),new Vector2Int(2560,1080)})
            {
                Size(size.x,size.y);yield return Frames(12);
                foreach(var id in new[]{"QC007","gen:qc_camp:14","gen:qc_camp:19"})
                foreach(int tier in new[]{0,2})
                {
                    int index=Enumerable.Range(0,map.Scenes.Count).First(i=>map.SceneAt(i).Level.id==id);
                    var scene=map.SceneAt(index);Assert.NotNull(scene.Level.environment.shore);Assert.IsTrue(scene.Props.Any(p=>p.Geometry!=null));
                    services.Settings.quality=tier+1;services.EffectiveQuality=tier;map.SetWeatherMoment(80);
                    float height=scroll.viewport.rect.height;
                    scroll.verticalNormalizedPosition=1-Mathf.Clamp01((RoadmapLayout.MainY(index)-height*.5f)/(scroll.content.rect.height-height));scroll.velocity=Vector2.zero;
                    yield return Frames(12);Canvas.ForceUpdateCanvases();
                    Assert.IsTrue(map.GladePool.Any(g=>g.gameObject.activeSelf&&g.SceneIndex==index));Assert.AreEqual(0,map.TruncatedModels);
                    foreach(var chunk in map.GladePool.Where(g=>g.gameObject.activeSelf))Assert.Less(chunk.canvasRenderer.GetMesh().vertexCount,16000);
                    var weather=map.GetComponentInChildren<RoadmapWeatherGraphic>();
                    if(scene.Winter){Assert.AreEqual(0,weather.RainDrops);Assert.Greater(weather.Snowflakes,0);Assert.Greater(weather.SnowGlades,0);}
                    if(scene.Level.environment.weatherId=="rain")Assert.Greater(weather.RainDrops,0);
                    Assert.AreEqual(cameras,Object.FindObjectsByType<Camera>().Length,"Map created a rendering camera");
                    yield return Shot("season_"+id.Replace(':','_')+"_tier"+tier+"_"+size.x+"x"+size.y);
                    // A visible shoreline must keep its scene slot alive even
                    // when the clearing's centre has already left the viewport.
                    float gap=Mathf.Min(260,map.SceneExtent(index)-1);
                    float offset=scene.Level.environment.shore.side=="left"||scene.Level.environment.shore.side=="back"
                        ?RoadmapLayout.MainY(index)+gap:RoadmapLayout.MainY(index)-height-gap;
                    float range=scroll.content.rect.height-height;
                    if(gap>230&&offset>0&&offset<range)
                    {
                        scroll.verticalNormalizedPosition=1-offset/range;scroll.velocity=Vector2.zero;yield return Frames(3);
                        Assert.IsTrue(map.GladePool.Any(g=>g.gameObject.activeSelf&&g.SceneIndex==index),"Visible shore was culled with the clearing centre");
                    }
                    services.ReducedMotion=true;weather.SetVerticesDirty();yield return Frames(3);Assert.AreEqual(0,weather.Snowflakes);Assert.AreEqual(0,weather.RainDrops);
                    services.ReducedMotion=false;
                }
            }
            Tap("back");yield return Frames(30);Assert.IsEmpty(Object.FindObjectsByType<RoadmapGraphic>());
        }
    }
}
