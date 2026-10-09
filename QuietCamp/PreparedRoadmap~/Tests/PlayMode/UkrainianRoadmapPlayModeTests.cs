
using QuietCamp.Presentation.UI.Prepared;
using RoadmapGladeGraphic = QuietCamp.Presentation.UI.Prepared.RoadmapGladeGraphic;
using RoadmapGraphic = QuietCamp.Presentation.UI.Prepared.RoadmapGraphic;
using RoadmapLayout = QuietCamp.Presentation.UI.Prepared.RoadmapLayout;
using RoadmapSceneGenerator = QuietCamp.Presentation.UI.Prepared.RoadmapSceneGenerator;
using RoadmapWeatherGraphic = QuietCamp.Presentation.UI.Prepared.RoadmapWeatherGraphic;
#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using NUnit.Framework;
using QuietCamp.Infrastructure;
using QuietCamp.Presentation;
using QuietCamp.Presentation.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace QuietCamp.Tests.Prepared
{
    /// <summary>Isolated native cultural gallery; real shipped map, RAM-only progress, no menu world.</summary>
    public sealed class UkrainianRoadmapPlayModeTests
    {
        bool _async;
        [SetUp] public void Setup()
        {_async=UnityEditor.ShaderUtil.allowAsyncCompilation;UnityEditor.ShaderUtil.allowAsyncCompilation=false;}
        [UnityTearDown] public IEnumerator Cleanup()
        {UnityEditor.ShaderUtil.allowAsyncCompilation=_async;yield return null;}
        [UnityTest,Timeout(240000)] public IEnumerator MainCultureSeasonsAndOrientation()
        {
            if(UnityEngine.Application.isBatchMode)Assert.Ignore("Requires a rendered Game View");
            RoadmapStreamingPlayModeTests.Size(720,1600);
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/QuietCamp/Tests/Fixtures/Roadmap/RoadmapBenchmark.unity",new LoadSceneParameters(LoadSceneMode.Single));
            var harness=Object.FindAnyObjectByType<RoadmapBenchmarkHarness>();harness.autoRun=false;
            harness.definitionOverride=RoadmapRepository.Main.Definition;
            harness.summariesOverride=RoadmapRepository.Main.Nodes.Select(n=>RoadmapRepository.Summary(n.levelId)).ToArray();
            harness.completedLevels=30;harness.outputDirectoryOverride=RoadmapStreamingPlayModeTests.Output;harness.Build();
            var map=harness.Map;yield return RoadmapStreamingPlayModeTests.Ready(map);
            Assert.IsTrue(map.HasCulturalLandscape);Assert.AreEqual(30,map.Data.Nodes.Length);
            foreach(int index in new[]{2,10,17,22,27})
            {
                RoadmapStreamingPlayModeTests.Seek(harness.Scroll,map,index);yield return RoadmapStreamingPlayModeTests.Ready(map);
                Assert.LessOrEqual(map.ActiveChunks,3);Assert.AreEqual(1,map.WorldRenderer.MaterialCount);
                Assert.AreEqual(1,map.WorldRenderer.TextureCount);Assert.Greater(map.WorldRenderer.Vertices,5000);
                var target=map.WorldRenderer.ScreenTarget(index);Assert.AreEqual(map.Centre(index).x,target.x,2);Assert.AreEqual(map.Centre(index).y,target.y,4);
                yield return RoadmapStreamingPlayModeTests.Shot("cultural_node_"+(index+1)+"_portrait");
            }
            RoadmapStreamingPlayModeTests.Seek(harness.Scroll,map,10);yield return RoadmapStreamingPlayModeTests.Ready(map);
            int before=map.WorldRenderer.Rebuilds;
            RoadmapStreamingPlayModeTests.Size(1280,800);yield return RoadmapStreamingPlayModeTests.Frames(12);yield return RoadmapStreamingPlayModeTests.Ready(map);
            Assert.AreEqual(before,map.WorldRenderer.Rebuilds,"Orientation regenerated cultural world geometry");
            foreach(int index in new[]{10,17,22})
            {RoadmapStreamingPlayModeTests.Seek(harness.Scroll,map,index);yield return RoadmapStreamingPlayModeTests.Ready(map);yield return RoadmapStreamingPlayModeTests.Shot("cultural_node_"+(index+1)+"_tablet");}
        }
    }
}
#endif
