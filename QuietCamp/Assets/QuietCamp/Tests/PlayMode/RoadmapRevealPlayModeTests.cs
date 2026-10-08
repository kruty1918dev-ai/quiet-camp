#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using NUnit.Framework;
using QuietCamp.Application;
using QuietCamp.Domain;
using QuietCamp.Infrastructure;
using QuietCamp.Presentation;
using QuietCamp.Presentation.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace QuietCamp.Tests
{
    public sealed class RoadmapRevealPlayModeTests
    {
        bool _async;
        [SetUp] public void Setup()
        {
            _async=UnityEditor.ShaderUtil.allowAsyncCompilation;UnityEditor.ShaderUtil.allowAsyncCompilation=false;
            QuietCampBootstrap.EditorSaveFactory=()=>new SaveAdapter(memoryOnly:true);QuietCampBootstrap.EditorDisableAudio=true;
            RoadmapRepository.EditorMainPreview=new RoadmapCatalog(RoadmapCompiler.BuildWorld(CampContent.Summaries,null,"reveal-test"));
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            RoadmapRepository.EditorMainPreview=null;QuietCampBootstrap.EditorSaveFactory=null;QuietCampBootstrap.EditorDisableAudio=false;UnityEditor.ShaderUtil.allowAsyncCompilation=_async;
            var boot=Object.FindAnyObjectByType<QuietCampBootstrap>();if(boot!=null)Object.Destroy(boot.gameObject);yield return null;
        }
        [UnityTest,Timeout(240000)] public IEnumerator FreshPlayerCannotScrollIntoUnknownWorldAndCompletionRetreatsFog()
        {
            if(UnityEngine.Application.isBatchMode)Assert.Ignore("Requires rendered Game View");
            RoadmapStreamingPlayModeTests.Size(720,1600);yield return SceneManager.LoadSceneAsync("Boot");
            float deadline=Time.realtimeSinceStartup+90;
            while(Object.FindAnyObjectByType<QuietCampBootstrap>()?.StartupReady!=true&&Time.realtimeSinceStartup<deadline)
            {var ack=PrivacyBootTestSupport.Find("boot-policy-ack");if(BootPrivacyPanel.Current?.Accepted==false&&ack!=null&&ack.interactable)PrivacyBootTestSupport.Tap(ack);yield return null;}
            RoadmapStreamingPlayModeTests.Size(720,1600);
            var services=QuietCampBootstrap.ServicesRef;Assert.NotNull(services);services.Tutorial.Skip();services.Localization.TrySetLanguage("uk");
            yield return RoadmapStreamingPlayModeTests.Frames(25);
            PrivacyBootTestSupport.Tap(PrivacyBootTestSupport.Find("levels"));yield return RoadmapStreamingPlayModeTests.Frames(3);
            var overlay=Object.FindObjectsByType<HtmlSurface>().Single(s=>s.name=="MenuOverlay");var map=overlay.GetComponentInChildren<RoadmapGraphic>();
            yield return RoadmapStreamingPlayModeTests.Ready(map);var scroll=overlay.Element("roadmap-scroll").GetComponent<ScrollRect>();
            Assert.AreEqual(0,map.Frontier);Assert.AreEqual(2,map.RevealState.LastKnown);
            for(int attempt=0;attempt<6;attempt++)
            {
                scroll.verticalNormalizedPosition=0;scroll.velocity=new Vector2(0,100000);
                yield return null;
                float at=(1-scroll.verticalNormalizedPosition)*Mathf.Max(1,map.Data.Height-scroll.viewport.rect.height);
                Assert.LessOrEqual(at,map.MaxScrollDistance+2,"Scroll bypassed progress boundary");
            }
            Assert.IsFalse(PrivacyBootTestSupport.Find("level-20")!=null,"Future control exists");
            Assert.IsTrue(map.ActiveIndices.All(i=>i<=map.Frontier));Assert.LessOrEqual(map.ActiveChunks,3);
            yield return RoadmapStreamingPlayModeTests.Ready(map);yield return RoadmapStreamingPlayModeTests.Shot("reveal_fresh_horizon");
            float before=map.RevealDistance;services.Progression.MarkCompleted(map.Data.Nodes[0].levelId);yield return null;
            Assert.IsTrue(map.RevealAnimating);Assert.AreEqual(1,map.Frontier);Assert.Less(map.RevealDistance,map.Data.Y(1));Assert.GreaterOrEqual(map.RevealDistance,before);Assert.Less(map.RevealOpacity(map.Frontier),1);
            yield return RoadmapStreamingPlayModeTests.Shot("reveal_in_motion");
            deadline=Time.realtimeSinceStartup+30;float last=map.RevealDistance;
            while((map.RevealAnimating||!map.IsReady)&&Time.realtimeSinceStartup<deadline)
            {yield return null;Assert.GreaterOrEqual(map.RevealDistance+.01f,last);last=map.RevealDistance;}
            Assert.IsFalse(map.RevealAnimating);Assert.AreEqual(map.Data.Y(1),map.RevealDistance,.01f);
            yield return RoadmapStreamingPlayModeTests.Shot("reveal_one_completed");
            for(int i=1;i<10;i++)services.Progression.MarkCompleted(map.Data.Nodes[i].levelId);
            yield return null;deadline=Time.realtimeSinceStartup+30;
            while((map.RevealAnimating||!map.IsReady)&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.AreEqual(10,map.Frontier);Assert.AreEqual(12,map.RevealState.LastKnown);
            scroll.verticalNormalizedPosition=0;yield return RoadmapStreamingPlayModeTests.Ready(map);
            yield return RoadmapStreamingPlayModeTests.Shot("reveal_ten_completed");
            int frontier=map.Frontier;services.Progression.MarkCompleted(map.Data.Nodes[0].levelId);yield return null;
            Assert.AreEqual(frontier,map.Frontier);Assert.IsFalse(map.RevealAnimating);
            RoadmapStreamingPlayModeTests.Size(1280,800);yield return RoadmapStreamingPlayModeTests.Frames(15);yield return RoadmapStreamingPlayModeTests.Ready(map);
            scroll.verticalNormalizedPosition=0;yield return RoadmapStreamingPlayModeTests.Ready(map);yield return RoadmapStreamingPlayModeTests.Shot("reveal_tablet_horizon");
            Assert.LessOrEqual((1-scroll.verticalNormalizedPosition)*Mathf.Max(1,map.Data.Height-scroll.viewport.rect.height),map.MaxScrollDistance+2);
            Assert.AreEqual(1,map.WorldRenderer.TextureCount);Assert.AreEqual(1,map.WorldRenderer.MaterialCount);
            // Re-entry restores known world from progress, not a stale far-future anchor.
            services.LevelMapAnchors["main"]=new RoadmapAnchor{journeyId="main",nodeId=map.Data.Nodes[29].id};
            PrivacyBootTestSupport.Tap(PrivacyBootTestSupport.Find("back"));yield return RoadmapStreamingPlayModeTests.Frames(30);
            PrivacyBootTestSupport.Tap(PrivacyBootTestSupport.Find("levels"));yield return RoadmapStreamingPlayModeTests.Frames(5);
            overlay=Object.FindObjectsByType<HtmlSurface>().Single(s=>s.name=="MenuOverlay");map=overlay.GetComponentInChildren<RoadmapGraphic>();yield return RoadmapStreamingPlayModeTests.Ready(map);
            Assert.AreEqual(10,map.Frontier);Assert.IsFalse(map.RevealAnimating);Assert.AreEqual(RoadmapReveal.Hidden,map.Reveal(29));
        }
        [UnityTest,Timeout(180000)] public IEnumerator ProjectedCampaignWaitsForGeometryThenFadesNewGlade()
        {
            if(UnityEngine.Application.isBatchMode)Assert.Ignore("Requires rendered Game View");
            RoadmapStreamingPlayModeTests.Size(720,1600);
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/QuietCamp/Tests/Fixtures/Roadmap/RoadmapBenchmark.unity",new LoadSceneParameters(LoadSceneMode.Single));
            var harness=Object.FindAnyObjectByType<RoadmapBenchmarkHarness>();harness.autoRun=false;harness.completedLevels=0;
            harness.outputDirectoryOverride=Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath,"../../TestResults/roadmap-reveal-2026-10-07"));
            harness.Build();var map=harness.Map;Assert.IsFalse(map.IsWorldDiorama);
            yield return RoadmapStreamingPlayModeTests.Ready(map);
            harness.Scroll.verticalNormalizedPosition=0;yield return RoadmapStreamingPlayModeTests.Ready(map);
            Assert.LessOrEqual((1-harness.Scroll.verticalNormalizedPosition)*Mathf.Max(1,map.Data.Height-harness.Scroll.viewport.rect.height),map.MaxScrollDistance+2);
            harness.Services.Progression.MarkCompleted(map.Data.Nodes[0].levelId);yield return null;
            yield return RoadmapStreamingPlayModeTests.Ready(map);
            var slot=map.GetComponentsInChildren<RoadmapGladeGraphic>().Single(g=>g.SceneIndex==1);
            Assert.Less(slot.GetComponent<CanvasGroup>().alpha,1,"New mesh must not pop at full opacity");
            float deadline=Time.realtimeSinceStartup+15;
            while(map.RevealAnimating&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.IsFalse(map.RevealAnimating);yield return null;
            Assert.AreEqual(1,slot.GetComponent<CanvasGroup>().alpha,.01f);
            yield return RoadmapStreamingPlayModeTests.Shot("reveal_projected_completed");
        }
        [UnityTest,Timeout(180000)] public IEnumerator World360CannotBeExposedByAggressiveScroll()
        {
            if(UnityEngine.Application.isBatchMode)Assert.Ignore("Requires rendered Game View");
            RoadmapStreamingPlayModeTests.Size(720,1600);
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/QuietCamp/Tests/Fixtures/Roadmap/RoadmapBenchmark.unity",new LoadSceneParameters(LoadSceneMode.Single));
            var harness=Object.FindAnyObjectByType<RoadmapBenchmarkHarness>();harness.autoRun=false;harness.fixtureName="benchmark-world360";harness.completedLevels=0;
            harness.outputDirectoryOverride=Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath,"../../TestResults/roadmap-reveal-2026-10-07"));
            harness.Build();var map=harness.Map;yield return RoadmapStreamingPlayModeTests.Ready(map);
            Assert.AreEqual(360,map.Data.Nodes.Length);
            foreach(int index in new[]{20,150,359})
            {
                RoadmapStreamingPlayModeTests.Seek(harness.Scroll,map,index);yield return RoadmapStreamingPlayModeTests.Ready(map);
                Assert.LessOrEqual((1-harness.Scroll.verticalNormalizedPosition)*Mathf.Max(1,map.Data.Height-harness.Scroll.viewport.rect.height),map.MaxScrollDistance+2);
                Assert.IsTrue(map.ActiveIndices.All(i=>i==0));Assert.LessOrEqual(map.ActiveChunks,3);Assert.LessOrEqual(map.ActiveControls,3);
            }
            harness.durationSeconds=20;yield return harness.Run();
            Assert.IsTrue(map.IsReady);Assert.LessOrEqual(map.ActiveChunks,3);Assert.AreEqual(1,map.WorldRenderer.TextureCount);Assert.AreEqual(1,map.WorldRenderer.MaterialCount);
            yield return RoadmapStreamingPlayModeTests.Shot("reveal_world360_boundary");
        }
    }
}
#endif
