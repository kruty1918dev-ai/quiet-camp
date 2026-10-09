
using QuietCamp.Presentation.UI.Prepared;
using RoadmapGladeGraphic = QuietCamp.Presentation.UI.Prepared.RoadmapGladeGraphic;
using RoadmapGraphic = QuietCamp.Presentation.UI.Prepared.RoadmapGraphic;
using RoadmapLayout = QuietCamp.Presentation.UI.Prepared.RoadmapLayout;
using RoadmapSceneGenerator = QuietCamp.Presentation.UI.Prepared.RoadmapSceneGenerator;
using RoadmapWeatherGraphic = QuietCamp.Presentation.UI.Prepared.RoadmapWeatherGraphic;
#if UNITY_EDITOR
using System.Collections;
using System.IO;
using NUnit.Framework;
using QuietCamp.Presentation;
using QuietCamp.Presentation.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;
namespace QuietCamp.Tests.Prepared
{
    public sealed class RoadmapBlendPlayModeTests
    {
        bool _async;
        [SetUp] public void Setup(){_async=UnityEditor.ShaderUtil.allowAsyncCompilation;UnityEditor.ShaderUtil.allowAsyncCompilation=false;QuietCampBootstrap.EditorDisableAudio=true;}
        [UnityTearDown] public IEnumerator Cleanup(){QuietCampBootstrap.EditorDisableAudio=false;UnityEditor.ShaderUtil.allowAsyncCompilation=_async;var harness=Object.FindAnyObjectByType<RoadmapBenchmarkHarness>();if(harness!=null)Object.Destroy(harness.gameObject);yield return null;}
        static string DirectoryFor(string name)=>Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath,"../../TestResults/roadmap-blend-2026-10-07",name));
        static IEnumerator Shot(string name,string folder)
        {
            yield return RoadmapStreamingPlayModeTests.Shot("blend_"+name);
            string source=Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath,"../../TestResults/roadmap-production-2026-10-07/blend_"+name+".png"));
            string destination=Path.Combine(DirectoryFor(folder),name+".png");if(File.Exists(destination))File.Delete(destination);File.Move(source,destination);
        }
        static void Position(RoadmapBenchmarkHarness harness,float distance)
        {harness.Scroll.StopMovement();harness.Scroll.verticalNormalizedPosition=1-Mathf.Clamp01(distance/Mathf.Max(1,harness.Map.Data.Height-harness.Scroll.viewport.rect.height));}
        IEnumerator Route(string fixture)
        {
            if(UnityEngine.Application.isBatchMode)Assert.Ignore("Requires rendered Game View");
            RoadmapStreamingPlayModeTests.Size(720,1600);Directory.CreateDirectory(DirectoryFor(fixture));
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/QuietCamp/Tests/Fixtures/Roadmap/RoadmapBenchmark.unity",new LoadSceneParameters(LoadSceneMode.Single));
            var harness=Object.FindAnyObjectByType<RoadmapBenchmarkHarness>();harness.autoRun=false;harness.fixtureName="transition-"+fixture;harness.summaryName=harness.fixtureName+"-summaries";harness.completedLevels=99;
            harness.outputDirectoryOverride=DirectoryFor(fixture);harness.Build();var map=harness.Map;
            yield return RoadmapStreamingPlayModeTests.Ready(map);
            for(int r=1;r<map.Data.Definition.regions.Length;r++)
            {
                float middle=map.Data.RegionStarts[r];float half=map.Data.Definition.regions[r].transition.length*.5f;
                Position(harness,middle-half*.65f-map.VisibleArea.height*.5f);yield return RoadmapStreamingPlayModeTests.Ready(map);
                yield return Shot("region_"+r+"_incoming",fixture);
                Position(harness,middle-map.VisibleArea.height*.5f);yield return RoadmapStreamingPlayModeTests.Ready(map);
                yield return Shot("region_"+r+"_middle",fixture);
                Position(harness,middle+half*.65f-map.VisibleArea.height*.5f);yield return RoadmapStreamingPlayModeTests.Ready(map);
                yield return Shot("region_"+r+"_outgoing",fixture);
                Assert.LessOrEqual(map.ActiveChunks,3);Assert.AreEqual(1,map.WorldRenderer.MaterialCount);Assert.AreEqual(1,map.WorldRenderer.TextureCount);
            }
            Position(harness,map.Data.RegionStarts[1]-map.VisibleArea.height*.5f);yield return RoadmapStreamingPlayModeTests.Ready(map);
            yield return Shot("motion_a",fixture);yield return new WaitForSecondsRealtime(.75f);yield return Shot("motion_b",fixture);
            var a=new Texture2D(2,2);a.LoadImage(File.ReadAllBytes(Path.Combine(DirectoryFor(fixture),"motion_a.png")));
            var b=new Texture2D(2,2);b.LoadImage(File.ReadAllBytes(Path.Combine(DirectoryFor(fixture),"motion_b.png")));
            var pixelsA=a.GetPixels32();var pixelsB=b.GetPixels32();int changed=0;for(int i=0;i<pixelsA.Length;i++)if(!pixelsA[i].Equals(pixelsB[i]))changed++;
            Object.Destroy(a);Object.Destroy(b);Assert.Greater(changed,100,"Animated foliage/weather should change the rendered image");
            harness.durationSeconds=20;yield return harness.Run();Assert.IsTrue(map.IsReady);
            RoadmapStreamingPlayModeTests.Size(1280,800);yield return RoadmapStreamingPlayModeTests.Frames(15);yield return RoadmapStreamingPlayModeTests.Ready(map);yield return Shot("tablet",fixture);
            harness.Services.Progression.Restore(new[]{map.Data.Nodes[0].levelId,map.Data.Nodes[1].levelId},map.Data.Nodes[0].levelId,0);
            yield return null;Position(harness,map.Data.Height);yield return RoadmapStreamingPlayModeTests.Ready(map);
            Assert.AreEqual(2,map.Frontier);Assert.LessOrEqual(map.RevealState.LastKnown,4);
            Assert.LessOrEqual((1-harness.Scroll.verticalNormalizedPosition)*Mathf.Max(1,map.Data.Height-harness.Scroll.viewport.rect.height),map.MaxScrollDistance+2);
            yield return Shot("hidden_future",fixture);
        }
        [UnityTest,Timeout(240000)] public IEnumerator SummerAutumnWorldBlend()=>Route("summer-autumn");
        [UnityTest,Timeout(240000)] public IEnumerator WinterThawWorldBlend()=>Route("winter-thaw");
    }
}
#endif
