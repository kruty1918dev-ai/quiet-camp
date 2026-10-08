#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using NUnit.Framework;
using QuietCamp.Composition;
using QuietCamp.Domain;
using QuietCamp.Infrastructure;
using QuietCamp.Presentation.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
namespace QuietCamp.Tests
{
    public sealed class CompositionRoadmapPlayModeTests
    {
        bool _async;string _output;
        static string Output=>Environment.GetEnvironmentVariable("QC_ROADMAP_QA_OUTPUT")??Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath,"../../TestResults/roadmap-composition-2026-10-08"));
        [UnityTest,Timeout(300000)]public IEnumerator Native360ScrollResizeReentryAndLowMemory()
        {
            if(UnityEngine.Application.isBatchMode)Assert.Ignore("Native Game View required");
            var type=Type.GetType("QuietCamp.Editor.RoadmapCompositionBenchmark, QuietCamp.Editor",true);
            var dataset=type.GetMethod("Create").Invoke(null,null);
            var definition=(RoadmapDefinition)dataset.GetType().GetField("definition").GetValue(dataset);
            var summaries=(LevelSummary[])dataset.GetType().GetField("summaries").GetValue(dataset);
            RoadmapStreamingPlayModeTests.Size(720,1600);
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/QuietCamp/Tests/Fixtures/Roadmap/RoadmapBenchmark.unity",new LoadSceneParameters(LoadSceneMode.Single));
            var harness=UnityEngine.Object.FindAnyObjectByType<RoadmapBenchmarkHarness>();harness.autoRun=false;harness.definitionOverride=definition;harness.summariesOverride=summaries;harness.completedLevels=340;harness.outputDirectoryOverride=Output;harness.Build();
            var map=harness.Map;yield return RoadmapStreamingPlayModeTests.Ready(map);harness.RecordColdReady();
            foreach(int index in Enumerable.Range(0,27).Select(i=>i*12))
            {
                RoadmapStreamingPlayModeTests.Seek(harness.Scroll,map,index);yield return RoadmapStreamingPlayModeTests.Ready(map);
                Assert.LessOrEqual(map.WorldRenderer.ResidentChunks,3);Assert.LessOrEqual(map.WorldRenderer.PresentationBytes,64L*1024*1024);Assert.LessOrEqual(map.WorldRenderer.LiveMeshes,32);Assert.AreEqual(0,map.WorldRenderer.ProceduralBuilds);
                var target=map.WorldRenderer.ScreenTarget(index);Assert.AreEqual(map.Centre(index).x,target.x,2);Assert.AreEqual(map.Centre(index).y,target.y,4);
            }
            RoadmapStreamingPlayModeTests.Size(1280,800);yield return RoadmapStreamingPlayModeTests.Ready(map);
            for(int i=0;i<3;i++){map.gameObject.SetActive(false);yield return RoadmapStreamingPlayModeTests.Frames(2);map.gameObject.SetActive(true);yield return RoadmapStreamingPlayModeTests.Ready(map);}
            // Return while a previous native request may still be waiting; no canceled
            // callback may unload the resource acquired by the next entry.
            RoadmapStreamingPlayModeTests.Seek(harness.Scroll,map,180);yield return null;yield return null;
            map.gameObject.SetActive(false);yield return null;map.gameObject.SetActive(true);yield return RoadmapStreamingPlayModeTests.Ready(map);
            Assert.IsNull(map.WorldRenderer.StreamFault);
            map.TrimForLowMemory();yield return RoadmapStreamingPlayModeTests.Ready(map);Assert.LessOrEqual(map.WorldRenderer.ResidentChunks,3);
            harness.durationSeconds=180;yield return harness.Run();Assert.AreEqual(0,map.WorldRenderer.ProceduralBuilds);
            UnityEngine.Object.Destroy(harness.gameObject);yield return null;
        }

        [SetUp]public void Setup(){_output=Environment.GetEnvironmentVariable("QC_ROADMAP_QA_OUTPUT");Environment.SetEnvironmentVariable("QC_ROADMAP_QA_OUTPUT",Output);_async=UnityEditor.ShaderUtil.allowAsyncCompilation;UnityEditor.ShaderUtil.allowAsyncCompilation=false;}
        [UnityTearDown]public IEnumerator Cleanup(){Environment.SetEnvironmentVariable("QC_ROADMAP_QA_OUTPUT",_output);UnityEditor.ShaderUtil.allowAsyncCompilation=_async;yield return null;}
        [UnityTest,Timeout(600000)]public IEnumerator BakeAndValidateMainGallery()
        {
            if(UnityEngine.Application.isBatchMode)Assert.Ignore("Native Game View required");
            RoadmapStreamingPlayModeTests.Size(720,1600);
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/QuietCamp/Tests/Fixtures/Roadmap/RoadmapBenchmark.unity",new LoadSceneParameters(LoadSceneMode.Single));
            var harness=UnityEngine.Object.FindAnyObjectByType<RoadmapBenchmarkHarness>();harness.autoRun=false;harness.outputDirectoryOverride=Output;
            var baker=Type.GetType("QuietCamp.Editor.RoadmapCompositionBaker, QuietCamp.Editor",true);
            var source=(Dictionary<string,SceneCompositionDocument>)baker.GetMethod("Documents").Invoke(null,null);
            harness.definitionOverride=(RoadmapDefinition)baker.GetMethod("Definition").Invoke(null,new object[]{source});
            var summaries=JsonConvert.DeserializeObject<LevelSummary[]>(Resources.Load<TextAsset>("QuietCamp/level_summaries").text);harness.summariesOverride=summaries;harness.completedLevels=30;harness.Build();
            // Synchronous geometry is explicitly offline authoring, before the tested runtime instance.
            baker.GetMethod("Bake").Invoke(null,new object[]{harness.Map,source});
            UnityEngine.Object.Destroy(harness.gameObject);yield return RoadmapStreamingPlayModeTests.Frames(8);RoadmapModelLibrary.Reset();RoadmapRepository.Reset();
            var host=new GameObject("Baked native gallery");harness=host.AddComponent<RoadmapBenchmarkHarness>();harness.autoRun=false;harness.outputDirectoryOverride=Output;harness.definitionOverride=RoadmapRepository.Main.Definition;harness.summariesOverride=summaries;harness.completedLevels=30;harness.Build();
            var map=harness.Map;harness.Services.EffectiveQuality=2;yield return RoadmapStreamingPlayModeTests.Ready(map);
            Assert.IsTrue(map.HasBakedComposition);Assert.IsFalse(RoadmapModelLibrary.IsLoaded,"Runtime loaded triangle-stream JSON");
            foreach(int index in new[]{0,1,2,3,4,5,6,10,14,17,22,27})
            {
                RoadmapStreamingPlayModeTests.Seek(harness.Scroll,map,index);yield return RoadmapStreamingPlayModeTests.Ready(map);
                Assert.LessOrEqual(map.WorldRenderer.ResidentChunks,3);Assert.LessOrEqual(map.WorldRenderer.PresentationBytes,64L*1024*1024);Assert.AreEqual(0,map.WorldRenderer.ProceduralBuilds);
                var target=map.WorldRenderer.ScreenTarget(index);Assert.AreEqual(map.Centre(index).x,target.x,2);Assert.AreEqual(map.Centre(index).y,target.y,4);
                yield return RoadmapStreamingPlayModeTests.Shot("composition-node-"+(index+1)+"-portrait");
            }
            RoadmapStreamingPlayModeTests.Size(1280,800);yield return RoadmapStreamingPlayModeTests.Frames(12);
            foreach(int index in new[]{2,10,17,22}){RoadmapStreamingPlayModeTests.Seek(harness.Scroll,map,index);yield return RoadmapStreamingPlayModeTests.Ready(map);yield return RoadmapStreamingPlayModeTests.Shot("composition-node-"+(index+1)+"-tablet");}
            var centre=map.VisibleArea.center;float logical=map.rectTransform.rect.yMax-centre.y;
            map.SetZoom(1.8f,centre,new Vector2(map.ZoomOffsetX,logical));yield return RoadmapStreamingPlayModeTests.Ready(map);
            var zoomTarget=map.WorldRenderer.ScreenTarget(22);Assert.AreEqual(map.Centre(22).x,zoomTarget.x,2);Assert.AreEqual(map.Centre(22).y,zoomTarget.y,4);Assert.AreEqual(0,map.WorldRenderer.ProceduralBuilds);
            yield return RoadmapStreamingPlayModeTests.Shot("composition-zoom-tablet");
            foreach(int tier in new[]{0,2})
            {
                harness.Services.EffectiveQuality=tier;harness.Services.ReducedMotion=true;yield return RoadmapStreamingPlayModeTests.Ready(map);
                yield return RoadmapStreamingPlayModeTests.Shot("composition-reduced-tablet-quality-"+tier);Assert.AreEqual(0,map.WorldRenderer.ProceduralBuilds);
            }
            RoadmapStreamingPlayModeTests.Size(1500,1500);yield return RoadmapStreamingPlayModeTests.Ready(map);
            var texture=map.WorldRenderer.WorldCamera.targetTexture;yield return RoadmapStreamingPlayModeTests.Frames(30);
            Assert.AreSame(texture,map.WorldRenderer.WorldCamera.targetTexture,"Stable viewport recreated render texture");
            Assert.LessOrEqual(map.WorldRenderer.TextureBytes,16L*1024*1024);Assert.LessOrEqual(map.WorldRenderer.PresentationBytes,64L*1024*1024);
            Directory.CreateDirectory(Output);File.WriteAllText(Output+"/native-runtime.json",JsonConvert.SerializeObject(new{editorOnly=true,levels=map.Data.Nodes.Length,chunks=map.Data.Chunks.Length,map.WorldRenderer.ResidentChunks,map.WorldRenderer.CacheBytes,map.WorldRenderer.TextureBytes,map.WorldRenderer.PresentationBytes,map.WorldRenderer.ProceduralBuilds,map.WorldRenderer.WorstActivationMs,map.WorldRenderer.AssetActivations,map.WorldRenderer.MaterialCount,map.WorldRenderer.TextureCount},Formatting.Indented));
            harness.Services.Dispose();UnityEngine.Object.Destroy(host);yield return null;
        }
    }
}
#endif
