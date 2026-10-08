#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using NUnit.Framework;
using QuietCamp.Application;
using QuietCamp.Domain;
using QuietCamp.Infrastructure;
using QuietCamp.Presentation;
using QuietCamp.Presentation.UI;
using QuietCamp.Presentation.World;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace QuietCamp.Tests
{
    /// <summary>Native tests with RAM-only progress. Does not change product name or use the real save slot.</summary>
    public sealed class RoadmapStreamingPlayModeTests
    {
        internal static string Output=>Environment.GetEnvironmentVariable("QC_ROADMAP_QA_OUTPUT")??Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath,"../../TestResults/roadmap-production-2026-10-07"));
        bool _async;
        static int _captureWidth,_captureHeight;
        [SetUp] public void Setup(){_async=UnityEditor.ShaderUtil.allowAsyncCompilation;UnityEditor.ShaderUtil.allowAsyncCompilation=false;QuietCampBootstrap.EditorSaveFactory=()=>new SaveAdapter(memoryOnly:true);QuietCampBootstrap.EditorDisableAudio=true;}
        [UnityTearDown] public IEnumerator Cleanup()
        {
            QuietCampBootstrap.EditorSaveFactory=null;QuietCampBootstrap.EditorDisableAudio=false;UnityEditor.ShaderUtil.allowAsyncCompilation=_async;
            var bootstrap=Object.FindAnyObjectByType<QuietCampBootstrap>();if(bootstrap!=null)Object.Destroy(bootstrap.gameObject);
            yield return null;
        }
        internal static IEnumerator Frames(int count){while(count-->0)yield return null;}
        internal static void Size(int width,int height)
        {
            _captureWidth=width;_captureHeight=height;
            if(!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("QC_ROADMAP_QA_OUTPUT")))
            {UnityEditor.EditorApplication.update-=CloseQaHierarchy;UnityEditor.EditorApplication.update+=CloseQaHierarchy;}
            var assembly=typeof(UnityEditor.Editor).Assembly;
            // The saved Editor layout may contain a Device Simulator. Its screen shim
            // can keep reporting a simulated phone resolution while Game View captures
            // another resolution, producing cropped UI and false coordinate assertions.
            foreach(var window in Resources.FindObjectsOfTypeAll<UnityEditor.EditorWindow>())
                if(window.GetType().FullName=="UnityEditor.DeviceSimulation.SimulatorWindow")window.Close();
            CloseQaHierarchy();
            var type=assembly.GetType("UnityEditor.GameViewSizes");
            var singleton=typeof(UnityEditor.ScriptableSingleton<>).MakeGenericType(type);
            var sizes=singleton.GetProperty("instance").GetValue(null);
            var viewType=assembly.GetType("UnityEditor.GameView");var view=UnityEditor.EditorWindow.GetWindow(viewType);
            var groupType=assembly.GetType("UnityEditor.GameViewSizeGroupType");
            var groupId=viewType.GetProperty("currentSizeGroupType",BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic)?.GetValue(null)??Enum.Parse(groupType,"Standalone");
            var group=type.GetMethod("GetGroup").Invoke(sizes,new[]{groupId});
            var sizeType=assembly.GetType("UnityEditor.GameViewSize");var kind=assembly.GetType("UnityEditor.GameViewSizeType");
            var size=Activator.CreateInstance(sizeType,new[]{Enum.Parse(kind,"FixedResolution"),(object)width,height,"Roadmap isolated QA"});
            group.GetType().GetMethod("AddCustomSize").Invoke(group,new[]{size});
            int count=(int)group.GetType().GetMethod("GetTotalCount").Invoke(group,null);
            if(!view.docked)view.position=new Rect(50,50,1024,768);
            viewType.GetProperty("selectedSizeIndex",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).SetValue(view,count-1);
            view.Focus();view.Repaint();UnityEngine.Application.runInBackground=true;
        }
        static void CloseQaHierarchy()
        {
            if(string.IsNullOrEmpty(Environment.GetEnvironmentVariable("QC_ROADMAP_QA_OUTPUT")))return;
            // Closing a maximized Simulator restores its underlying layout after initial
            // startup. Close newly restored Hierarchy subclasses before their GUI repaint;
            // this QA fixture does not suppress any engine/runtime assertion.
            foreach(var window in Resources.FindObjectsOfTypeAll<UnityEditor.EditorWindow>())
                if(window.GetType().FullName.IndexOf("Hierarchy",StringComparison.OrdinalIgnoreCase)>=0)window.Close();
        }
        internal static IEnumerator Ready(RoadmapGraphic map)
        {
            yield return null; // Allow the viewport/window to invalidate before observing readiness.
            float deadline=Time.realtimeSinceStartup+30;
            while(!map.IsReady&&Time.realtimeSinceStartup<deadline){Assert.IsNull(map.WorldRenderer?.StreamFault,"Native streaming failed");yield return null;}
            Assert.IsTrue(map.IsReady,"Measured viewport and prepared map never became ready");yield return Frames(3);Canvas.ForceUpdateCanvases();
        }
        internal static IEnumerator Shot(string name)
        {
            Assert.AreEqual(_captureWidth,Screen.width,"Device Simulator screen shim differs from capture width");
            Assert.AreEqual(_captureHeight,Screen.height,"Device Simulator screen shim differs from capture height");
            Directory.CreateDirectory(Output);string path=Path.Combine(Output,name+".png");if(File.Exists(path))File.Delete(path);
            ScreenCapture.CaptureScreenshot(path);float deadline=Time.realtimeSinceStartup+15;
            while(!File.Exists(path)&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.IsTrue(File.Exists(path),"Missing composed capture: "+name);
            var texture=new Texture2D(2,2);try
            {
                Assert.IsTrue(texture.LoadImage(File.ReadAllBytes(path)));Assert.AreEqual(_captureWidth,texture.width,"Capture resolution does not match workload");Assert.AreEqual(_captureHeight,texture.height);
                var pixels=texture.GetPixels32();int colored=0,magenta=0,n=0;
                for(int i=0;i<pixels.Length;i+=97){n++;var p=pixels[i];if(p.r>3||p.g>3||p.b>3)colored++;if(p.r>180&&p.b>180&&p.g<90)magenta++;}
                Assert.Greater((float)colored/n,.1f,"Blank capture");Assert.Less((float)magenta/n,.02f,"Missing shader");
            }finally{Object.Destroy(texture);}
        }
        internal static void Seek(ScrollRect scroll,RoadmapGraphic map,int index)
        {scroll.StopMovement();scroll.verticalNormalizedPosition=1-Mathf.Clamp01((map.Data.Y(index)-scroll.viewport.rect.height*.45f)/Mathf.Max(1,map.Data.Height-scroll.viewport.rect.height));}
        static IEnumerator NavigationIdle()
        {
            float deadline=Time.realtimeSinceStartup+30;
            while(Object.FindAnyObjectByType<FoliageDiveTransition>()?.IsIdle==false&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.IsFalse(Object.FindAnyObjectByType<FoliageDiveTransition>()?.IsIdle==false,"Transition did not release input");
            yield return Frames(2);
        }
        static void Bounds(RoadmapGraphic map)
        {
            Assert.LessOrEqual(map.ActiveChunks,3);Assert.LessOrEqual(map.ActiveIndices.Count,30);Assert.LessOrEqual(map.ActiveControls,24);
            Assert.AreEqual(30,map.AvailableSceneLeases+map.ActiveIndices.Count,"Scene leases leaked");
            Assert.AreEqual(map.IsWorldDiorama?0:16,map.GladePool.Count);Assert.AreEqual(0,map.TruncatedModels,"Mesh silently truncated");
            if(map.IsWorldDiorama)
            {Assert.LessOrEqual(map.WorldRenderer.LiveMeshes,27);Assert.AreEqual(1,map.WorldRenderer.MaterialCount);Assert.AreEqual(1,map.WorldRenderer.TextureCount);}
            foreach(var index in map.ActiveIndices)Assert.AreEqual(RoadmapReveal.Revealed,map.Reveal(index),"Hidden future scene was activated");
        }
        [UnityTest,Timeout(480000)] public IEnumerator A_Benchmark360KeepsNativePoolsBoundedAcrossScrollResizeReentryAndMemoryPressure()
        {
            if(UnityEngine.Application.isBatchMode)Assert.Ignore("Needs rendered Game View");
            Size(720,1600);yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/QuietCamp/Tests/Fixtures/Roadmap/RoadmapBenchmark.unity",new LoadSceneParameters(LoadSceneMode.Single));
            var harness=Object.FindAnyObjectByType<RoadmapBenchmarkHarness>();harness.autoRun=false; // Start has not run before scene callback completes.
            if(harness.Map==null)harness.Build();
            var map=harness.Map;yield return Ready(map);harness.RecordColdReady();Bounds(map);
            for(int i=0;i<27;i++){Seek(harness.Scroll,map,i*12);yield return Ready(map);Bounds(map);}
            foreach(var size in new[]{new Vector2Int(1280,800),new Vector2Int(2560,1080),new Vector2Int(720,1600)})
            {Size(size.x,size.y);yield return Frames(10);yield return Ready(map);Bounds(map);}
            for(int i=0;i<3;i++){map.gameObject.SetActive(false);yield return Frames(2);map.gameObject.SetActive(true);yield return Ready(map);Bounds(map);}
            int sceneCountBefore=map.ActiveIndices.Count;
            var visibleBefore=map.ActiveIndices.Where(i=>map.InView(map.Centre(i).y,map.SceneExtent(i))).ToArray();
            map.TrimForLowMemory();yield return Ready(map);Assert.LessOrEqual(map.ActiveChunks,3);Assert.LessOrEqual(map.ActiveIndices.Count,sceneCountBefore);Bounds(map);
            foreach(int i in visibleBefore)Assert.NotNull(map.Scenes[i],"Low-memory trim removed visible geometry");
            // Start benchmark from first region, not the tail visited by the smoke loop.
            Seek(harness.Scroll,map,0);yield return Ready(map);harness.durationSeconds=180;yield return harness.Run();yield return Ready(map);Bounds(map);
            foreach(var pair in new[]{("clean_early_spring",0),("autumn_transition",15),("snow_transition",20)})
            {Seek(harness.Scroll,map,pair.Item2);yield return Ready(map);
                if(pair.Item2==20)
                {
                    var scene=map.Scenes[20];var source=scene.Level;
                    var terrain=new LevelData{width=source.width,height=source.height,ruleVersion=source.ruleVersion,decorSeed=source.decorSeed,entry=source.entry,accessPoints=source.accessPoints,exteriorWalkable=source.exteriorWalkable,environment=source.environment,noise=source.noise};
                    for(int x=-8;x<=8;x+=2)for(int z=-8;z<=8;z+=2)
                    {var p=new Vector3(x+.13f,0,z+.29f);Assert.AreEqual(scene.Season.SnowDepth(terrain,p),scene.SnowDepth(p),.00001f,"Cached walking network changed snow field");}
                }
                yield return Shot(pair.Item1);}
            Seek(harness.Scroll,map,342);yield return Ready(map);yield return Shot("hidden_future");
            Seek(harness.Scroll,map,9);yield return Ready(map);yield return Shot("visible_branch");
        }
        [UnityTest,Timeout(180000)] public IEnumerator C_HistoricalFixturesUseSharedLightingAndModelsWithoutPublishingUnapprovedContent()
        {
            if(UnityEngine.Application.isBatchMode)Assert.Ignore("Needs rendered Game View");
            foreach(var name in new[]{"mh17_inspired","black_sea","ship_level","kakhovka_inspired","benchmark-story-180"})
            {
                Size(720,1600);yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/QuietCamp/Tests/Fixtures/Roadmap/RoadmapBenchmark.unity",new LoadSceneParameters(LoadSceneMode.Single));
                var harness=Object.FindAnyObjectByType<RoadmapBenchmarkHarness>();harness.autoRun=false;harness.fixtureName=name;harness.summaryName=name=="benchmark-story-180"?"benchmark-story-summaries":name+"-summaries";harness.Build();
                yield return Ready(harness.Map);Bounds(harness.Map);yield return Shot(name);
                if(name!="benchmark-story-180")Assert.IsTrue(harness.Map.Scenes[0].Props.Any(p=>p.Asset.StartsWith("story_")),"Missing story geometry");
            }
        }
        [UnityTest,Timeout(480000)] public IEnumerator B_MainRoadmapBranchGameplayAndAlbumUseIsolatedStateAndWorkingRaycasts()
        {
            if(UnityEngine.Application.isBatchMode)Assert.Ignore("Needs rendered Game View");
            Size(720,1600);yield return SceneManager.LoadSceneAsync("Boot");
            float deadline=Time.realtimeSinceStartup+90;
            while(Object.FindAnyObjectByType<QuietCampBootstrap>()?.StartupReady!=true&&Time.realtimeSinceStartup<deadline)
            {var ack=PrivacyBootTestSupport.Find("boot-policy-ack");if(BootPrivacyPanel.Current?.Accepted==false&&ack!=null&&ack.interactable)PrivacyBootTestSupport.Tap(ack);yield return null;}
            var services=QuietCampBootstrap.ServicesRef;Assert.NotNull(services);Assert.IsTrue(services.Save.MemoryOnly);
            Size(720,1600);services.Tutorial.Skip();services.ReducedMotion=true;yield return Frames(30);
            var settings=PrivacyBootTestSupport.Find("settings");Assert.NotNull(settings,"Settings must remain visible");PrivacyBootTestSupport.Tap(settings);yield return Frames(10);
            PrivacyBootTestSupport.Tap(PrivacyBootTestSupport.Find("back"));yield return Frames(16);yield return Shot("main_menu");
            services.Progression.Restore(LevelLoader.MvpLevelIds().Take(25),null,0);
            PrivacyBootTestSupport.Tap(PrivacyBootTestSupport.Find("levels"));yield return Frames(3);
            var overlay=Object.FindObjectsByType<HtmlSurface>().Single(s=>s.name=="MenuOverlay");
            var map=overlay.GetComponentInChildren<RoadmapGraphic>();Assert.NotNull(map);yield return Ready(map);Bounds(map);yield return Shot("roadmap_current");
            var scroll=overlay.Element("roadmap-scroll").GetComponent<ScrollRect>();
            var branch=map.Data.Definition.regions.SelectMany(r=>r.branches).First(b=>!string.IsNullOrEmpty(b.bonusId)&&map.BranchVisible(b));
            int region=map.Data.RegionForNode(map.Data.NodeIndex(branch.anchorNodeId));
            scroll.StopMovement();scroll.verticalNormalizedPosition=1-Mathf.Clamp01((map.Data.RegionStarts[region]+branch.y-scroll.viewport.rect.height*.45f)/Mathf.Max(1,map.Data.Height-scroll.viewport.rect.height));
            yield return Ready(map);yield return Frames(5);float before=scroll.verticalNormalizedPosition;
            var branchButton=Object.FindObjectsByType<Button>().First(b=>b.name=="<button #branch-"+branch.id+">"&&b.gameObject.activeInHierarchy);
            PrivacyBootTestSupport.Tap(branchButton);yield return Frames(12);
            Assert.AreSame(map,overlay.GetComponentInChildren<RoadmapGraphic>(),"Branch preview destroyed its roadmap");Assert.IsFalse(map.InputEnabled);
            var artwork=overlay.Element("branch-preview-art").GetComponentInChildren<RoadmapBranchGraphic>();Assert.NotNull(artwork);
            deadline=Time.realtimeSinceStartup+20;while(!artwork.Ready&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.IsTrue(artwork.Ready);Assert.Greater(artwork.VertexCount,100);yield return Shot("bonus_preview");
            PrivacyBootTestSupport.Tap(PrivacyBootTestSupport.Find("branch-close")??PrivacyBootTestSupport.Find("back"));yield return Frames(14);Assert.AreSame(map,overlay.GetComponentInChildren<RoadmapGraphic>());Assert.AreEqual(before,scroll.verticalNormalizedPosition,.01);
            foreach(var language in new[]{"uk","en","de"})
            {
                float anchorBefore=scroll.verticalNormalizedPosition;
                services.Localization.TrySetLanguage(language);services.Settings.textScale=1.3f;overlay.Refresh();yield return Frames(10);
                // Changing CSS (text scale) intentionally remounts the UnityHTML context. Native components
                // belong to that context; retaining their previous references is invalid after remount.
                map=overlay.GetComponentInChildren<RoadmapGraphic>();Assert.NotNull(map);yield return Ready(map);
                scroll=overlay.Element("roadmap-scroll").GetComponent<ScrollRect>();
                Assert.AreEqual(anchorBefore,scroll.verticalNormalizedPosition,.01,"CSS remount lost the map anchor");
                Bounds(map);yield return Shot("roadmap_"+language);
            }
            Size(1280,800);yield return Frames(20);yield return Ready(map);Bounds(map);yield return Shot("roadmap_tablet");
            PrivacyBootTestSupport.Tap(PrivacyBootTestSupport.Find("map-journeys"));yield return Frames(12);
            var reveal=new RoadmapRevealState(map.Data,services.Progression);
            foreach(var entry in map.Data.Definition.regions.SelectMany(r=>r.branches))
                Assert.AreEqual(reveal.BranchVisible(entry),PrivacyBootTestSupport.Find("trail-"+entry.id)!=null,"Story Trails disagrees with reveal: "+entry.id);
            PrivacyBootTestSupport.Tap(PrivacyBootTestSupport.Find("back"));yield return Frames(15);
            map=overlay.GetComponentInChildren<RoadmapGraphic>();Assert.NotNull(map);yield return Ready(map);scroll=overlay.Element("roadmap-scroll").GetComponent<ScrollRect>();
            Size(720,1600);yield return Frames(16);Seek(scroll,map,0);yield return Ready(map);
            PrivacyBootTestSupport.Tap(PrivacyBootTestSupport.Find("level-0"));deadline=Time.realtimeSinceStartup+30;
            while(SceneManager.GetActiveScene().name!="Camp"&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.AreEqual("Camp",SceneManager.GetActiveScene().name);yield return NavigationIdle();yield return Frames(20);Assert.AreEqual("QC001",CampSceneHost.Current.Session.Level.id);
            // Return via registered action, not scene injection.
            services.Actions.Execute(new Kruty1918.UIActions.API.UiActionRequest(new Kruty1918.UIActions.API.UiActionId("qc.levels"),Kruty1918.UIActions.API.UiActionSource.Button,"QA"));
            deadline=Time.realtimeSinceStartup+30;while(SceneManager.GetActiveScene().name!="MainMenu"&&Time.realtimeSinceStartup<deadline)yield return null;
            yield return Frames(25);overlay=Object.FindObjectsByType<HtmlSurface>().Single(s=>s.name=="MenuOverlay");map=overlay.GetComponentInChildren<RoadmapGraphic>();Assert.NotNull(map);yield return Ready(map);
            yield return NavigationIdle();yield return Shot("gameplay_return");
            var level=LevelLoader.Load("QC001");services.Save.Album.entries=new[]{new AlbumSaveData.Entry{levelId=level.id,levelSnapshot=level,contentHash=level.contentHash,lighting=level.lighting,placements=level.witness,order=1}};
            PrivacyBootTestSupport.Tap(PrivacyBootTestSupport.Find("back"));yield return Frames(25);
            PrivacyBootTestSupport.Tap(PrivacyBootTestSupport.Find("album"));yield return Frames(30);yield return NavigationIdle();yield return Shot("my_camps_return");
            PrivacyBootTestSupport.Tap(PrivacyBootTestSupport.Find("album-replay"));deadline=Time.realtimeSinceStartup+30;
            while(SceneManager.GetActiveScene().name!="Camp"&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.AreEqual("Camp",SceneManager.GetActiveScene().name);yield return NavigationIdle();yield return Frames(12);Assert.AreEqual("QC001",CampSceneHost.Current.Session.Level.id);
        }
    }
}
#endif
