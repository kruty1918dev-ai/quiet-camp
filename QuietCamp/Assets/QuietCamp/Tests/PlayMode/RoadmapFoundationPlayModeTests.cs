#if UNITY_EDITOR
using System;
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
    public sealed class RoadmapFoundationPlayModeTests
    {
        bool _async;
        [SetUp] public void Setup()
        {
            _async=UnityEditor.ShaderUtil.allowAsyncCompilation;UnityEditor.ShaderUtil.allowAsyncCompilation=false;
            QuietCampBootstrap.EditorSaveFactory=()=>new SaveAdapter(memoryOnly:true);
            var definition=JsonConvert.DeserializeObject<RoadmapDefinition>(Resources.Load<TextAsset>("QuietCamp/Roadmaps/foundation_slice").text);
            RoadmapRepository.EditorMainPreview=new RoadmapCatalog(definition);
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            RoadmapRepository.EditorMainPreview=null;QuietCampBootstrap.EditorSaveFactory=null;UnityEditor.ShaderUtil.allowAsyncCompilation=_async;
            var boot=Object.FindAnyObjectByType<QuietCampBootstrap>();if(boot!=null)Object.Destroy(boot.gameObject);yield return null;
        }
        static void Snapshot(RoadmapGraphic map,ScrollRect scroll,string name)
        {
            var camera=map.WorldRenderer.WorldCamera;var back=PrivacyBootTestSupport.Find("back");
            var corners=new Vector3[4];scroll.viewport.GetWorldCorners(corners);
            var point=RectTransformUtility.WorldToScreenPoint(null,((RectTransform)back.transform).TransformPoint(((RectTransform)back.transform).rect.center));
            var root=RoadmapStreamingPlayModeTests.Output;Directory.CreateDirectory(root);
            File.WriteAllText(Path.Combine(root,"foundation-layout-"+name+".json"),JsonConvert.SerializeObject(new{screen=new[]{Screen.width,Screen.height},back=new[]{point.x,point.y},normalized=scroll.verticalNormalizedPosition,viewport=new[]{scroll.viewport.rect.width,scroll.viewport.rect.height},content=new[]{scroll.content.rect.width,scroll.content.rect.height},map=new[]{map.rectTransform.rect.width,map.rectTransform.rect.height},viewportCorners=corners.Select(v=>new[]{v.x,v.y,v.z}),cameraView=new[]{map.WorldRenderer.CameraView.x,map.WorldRenderer.CameraView.y,map.WorldRenderer.CameraView.width,map.WorldRenderer.CameraView.height},cameraTarget=camera.targetTexture!=null,cameraPose=new[]{camera.transform.position.x,camera.transform.position.y,camera.transform.position.z},cameraSize=camera.orthographicSize},Formatting.Indented));
        }
        [UnityTest,Timeout(300000)] public IEnumerator MainUsesAuthoredOpeningAndOneBounded3DWorld()
        {
            if(UnityEngine.Application.isBatchMode)Assert.Ignore("Requires rendered Game View");
            // Test the shipped catalog, not the Editor-only seven-level override.
            RoadmapRepository.EditorMainPreview=null;
            RoadmapStreamingPlayModeTests.Size(720,1600);yield return SceneManager.LoadSceneAsync("Boot");
            float deadline=Time.realtimeSinceStartup+90;
            while(Object.FindAnyObjectByType<QuietCampBootstrap>()?.StartupReady!=true&&Time.realtimeSinceStartup<deadline)
            {
                var ack=PrivacyBootTestSupport.Find("boot-policy-ack");
                if(BootPrivacyPanel.Current?.Accepted==false&&ack!=null&&ack.interactable)PrivacyBootTestSupport.Tap(ack);yield return null;
            }
            var services=QuietCampBootstrap.ServicesRef;Assert.NotNull(services);services.Tutorial.Skip();services.ReducedMotion=true;services.Localization.TrySetLanguage("uk");services.Settings.textScale=1;
            var data=RoadmapRepository.Main;services.Progression.MarkCompleted(data.Nodes[0].levelId);services.Progression.MarkCompleted(data.Nodes[1].levelId);
            yield return RoadmapStreamingPlayModeTests.Frames(25);
            Assert.NotNull(PrivacyBootTestSupport.Find("settings"));
            yield return RoadmapStreamingPlayModeTests.Shot("foundation_main_menu");
            PrivacyBootTestSupport.Tap(PrivacyBootTestSupport.Find("levels"));yield return RoadmapStreamingPlayModeTests.Frames(3);
            var overlay=Object.FindObjectsByType<HtmlSurface>().Single(s=>s.name=="MenuOverlay");
            var map=overlay.GetComponentInChildren<RoadmapGraphic>();Assert.NotNull(map);Assert.IsTrue(map.IsWorldDiorama);
            yield return RoadmapStreamingPlayModeTests.Ready(map);
            Assert.AreEqual(30,map.Data.Nodes.Length);Assert.AreEqual(8,map.Data.Chunks.Length);
            Assert.AreEqual("world-ukrainian-rural-3",map.Data.Definition.revision);
            Assert.IsTrue(map.HasCulturalLandscape);
            Assert.NotNull(RoadmapModelLibrary.Load().Get("ua_power_pylon"));
            Assert.AreEqual("story_trail_shelter",map.Data.Definition.regions[0].landmark);
            Assert.AreEqual(2,map.Data.Definition.regions[0].branches.Length);
            Assert.AreEqual(RoadmapNodeState.Completed,data.State(0,services.Progression));Assert.AreEqual(RoadmapNodeState.Current,data.State(2,services.Progression));
            Assert.AreEqual(RoadmapNodeState.Next,data.State(3,services.Progression));Assert.AreEqual(RoadmapNodeState.Locked,data.State(4,services.Progression));
            Assert.AreEqual(1,map.WorldRenderer.MaterialCount);Assert.AreEqual(1,map.WorldRenderer.TextureCount);Assert.LessOrEqual(map.WorldRenderer.LiveMeshes,27);
            Assert.LessOrEqual(map.ActiveChunks,3);Assert.LessOrEqual(map.ActiveControls,24);
            Assert.AreEqual(1,Object.FindObjectsByType<Camera>().Count(c=>c.name=="Roadmap single world camera"&&c.enabled));
            Assert.AreEqual(0,map.WorldRenderer.WorldCamera.cullingMask&~(1<<28));
            var scroll=overlay.Element("roadmap-scroll").GetComponent<ScrollRect>();
            RoadmapStreamingPlayModeTests.Seek(scroll,map,0);yield return RoadmapStreamingPlayModeTests.Ready(map);Snapshot(map,scroll,"hero");yield return RoadmapStreamingPlayModeTests.Shot("foundation_hero_landmark");
            RoadmapStreamingPlayModeTests.Seek(scroll,map,2);yield return RoadmapStreamingPlayModeTests.Ready(map);Snapshot(map,scroll,"current");yield return RoadmapStreamingPlayModeTests.Shot("foundation_current_branch");
            var backButton=PrivacyBootTestSupport.Find("back");var backRect=(RectTransform)backButton.transform;
            var backPoint=RectTransformUtility.WorldToScreenPoint(null,backRect.TransformPoint(backRect.rect.center));
            Assert.That(backPoint.x,Is.InRange(0,Screen.width));Assert.That(backPoint.y,Is.InRange(0,Screen.height));
            var worldMaterial=map.WorldRenderer.WorldRoot.GetComponentInChildren<MeshRenderer>().sharedMaterial;
            Assert.IsTrue(worldMaterial.shader.isSupported);Assert.Greater(map.WorldRenderer.Vertices,5000);
            foreach(int i in map.ActiveIndices)
            {
                var at=map.WorldRenderer.ScreenTarget(i);Assert.AreEqual(map.Centre(i).x,at.x,2);
                Assert.AreEqual(map.Centre(i).y,at.y,4,"3D scene and touch target use different projection");
            }
            int rebuilds=map.WorldRenderer.Rebuilds;
            yield return RoadmapStreamingPlayModeTests.Frames(35);Assert.AreEqual(rebuilds,map.WorldRenderer.Rebuilds,"Stationary world regenerates geometry");
            services.Progression.MarkCompleted(data.Nodes[2].levelId);services.Progression.MarkCompleted(data.Nodes[3].levelId);
            yield return RoadmapStreamingPlayModeTests.Frames(5);
            RoadmapStreamingPlayModeTests.Seek(scroll,map,4);yield return RoadmapStreamingPlayModeTests.Ready(map);
            float revealDeadline=Time.realtimeSinceStartup+15;
            while(map.RevealAnimating&&Time.realtimeSinceStartup<revealDeadline)yield return null;
            Assert.IsFalse(map.RevealAnimating);yield return RoadmapStreamingPlayModeTests.Ready(map);
            Assert.IsTrue(map.BranchVisible(data.Definition.regions[0].branches[0]));
            yield return RoadmapStreamingPlayModeTests.Shot("foundation_journey_teaser");
            RoadmapStreamingPlayModeTests.Size(1280,800);yield return RoadmapStreamingPlayModeTests.Frames(12);yield return RoadmapStreamingPlayModeTests.Ready(map);
            yield return RoadmapStreamingPlayModeTests.Shot("foundation_tablet");Assert.AreEqual(1,map.WorldRenderer.TextureCount);
            foreach(int seasonal in new[]{10,17,22,27})
            {
                for(int n=0;n<seasonal;n++)services.Progression.MarkCompleted(data.Nodes[n].levelId);
                map.RefreshProgress();yield return RoadmapStreamingPlayModeTests.Frames(5);
                RoadmapStreamingPlayModeTests.Seek(scroll,map,seasonal);yield return RoadmapStreamingPlayModeTests.Ready(map);
                float seasonalDeadline=Time.realtimeSinceStartup+15;
                while(map.RevealAnimating&&Time.realtimeSinceStartup<seasonalDeadline)yield return null;
                Assert.IsFalse(map.RevealAnimating);yield return RoadmapStreamingPlayModeTests.Ready(map);
                yield return RoadmapStreamingPlayModeTests.Shot("ukrainian_season_node_"+(seasonal+1));
                Assert.LessOrEqual(map.ActiveChunks,3);Assert.AreEqual(1,map.WorldRenderer.MaterialCount);
            }
            foreach(var locale in new[]{"uk","en","de"})
            {
                services.Localization.TrySetLanguage(locale);services.Settings.textScale=1.3f;
                // Existing HtmlSurface remount handling must retain a single live world camera.
                var host=Object.FindAnyObjectByType<QuietCamp.Presentation.World.MenuSceneHost>();
                var screens=(MenuScreens)typeof(QuietCamp.Presentation.World.MenuSceneHost).GetField("_screens",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(host);
                screens.Show("Main");yield return RoadmapStreamingPlayModeTests.Frames(20);screens.Show("Levels");yield return RoadmapStreamingPlayModeTests.Frames(5);
                overlay=Object.FindObjectsByType<HtmlSurface>().Single(s=>s.name=="MenuOverlay");map=overlay.GetComponentInChildren<RoadmapGraphic>();yield return RoadmapStreamingPlayModeTests.Ready(map);
                Assert.AreEqual(1,Object.FindObjectsByType<Camera>().Count(c=>c.name=="Roadmap single world camera"&&c.enabled));
            }
            map.TrimForLowMemory();yield return RoadmapStreamingPlayModeTests.Ready(map);Assert.LessOrEqual(map.ActiveChunks,3);
            PrivacyBootTestSupport.Tap(PrivacyBootTestSupport.Find("back"));yield return RoadmapStreamingPlayModeTests.Frames(35);
            Assert.IsFalse(Object.FindObjectsByType<Camera>().Any(c=>c.name=="Roadmap single world camera"&&c.enabled),"Roadmap camera survived navigation back");
            yield return RoadmapStreamingPlayModeTests.Shot("foundation_menu_return");
        }
        [UnityTest,Timeout(300000)] public IEnumerator World360ScrollUsesThreeChunksAndOneCamera()
        {
            if(UnityEngine.Application.isBatchMode)Assert.Ignore("Requires rendered Game View");
            RoadmapStreamingPlayModeTests.Size(720,1600);
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/QuietCamp/Tests/Fixtures/Roadmap/RoadmapBenchmark.unity",new LoadSceneParameters(LoadSceneMode.Single));
            var harness=Object.FindAnyObjectByType<RoadmapBenchmarkHarness>();harness.autoRun=false;harness.ukrainianRural=true;harness.fixtureName="benchmark-world360";harness.outputDirectoryOverride=RoadmapStreamingPlayModeTests.Output;harness.Build();
            var map=harness.Map;yield return RoadmapStreamingPlayModeTests.Ready(map);
            for(int index=0;index<=300;index+=30)
            {
                RoadmapStreamingPlayModeTests.Seek(harness.Scroll,map,index);yield return RoadmapStreamingPlayModeTests.Ready(map);
                Assert.LessOrEqual(map.ActiveChunks,3);Assert.LessOrEqual(map.ActiveIndices.Count,30);Assert.LessOrEqual(map.WorldRenderer.LiveMeshes,27);
                Assert.AreEqual(1,map.WorldRenderer.TextureCount);Assert.AreEqual(1,map.WorldRenderer.MaterialCount);
            }
            RoadmapStreamingPlayModeTests.Seek(harness.Scroll,map,0);yield return RoadmapStreamingPlayModeTests.Ready(map);
            harness.durationSeconds=40;yield return harness.Run();Assert.IsTrue(map.IsReady);yield return RoadmapStreamingPlayModeTests.Shot("foundation_world360");
        }
    }
}
#endif
