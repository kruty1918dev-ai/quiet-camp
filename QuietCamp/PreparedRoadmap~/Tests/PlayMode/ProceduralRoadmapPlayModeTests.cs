
using QuietCamp.Presentation.UI.Prepared;
using RoadmapGladeGraphic = QuietCamp.Presentation.UI.Prepared.RoadmapGladeGraphic;
using RoadmapGraphic = QuietCamp.Presentation.UI.Prepared.RoadmapGraphic;
using RoadmapLayout = QuietCamp.Presentation.UI.Prepared.RoadmapLayout;
using RoadmapSceneGenerator = QuietCamp.Presentation.UI.Prepared.RoadmapSceneGenerator;
using RoadmapWeatherGraphic = QuietCamp.Presentation.UI.Prepared.RoadmapWeatherGraphic;
#if UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using QuietCamp.Presentation.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace QuietCamp.Tests.Prepared
{
    /// <summary>Streaming replaces the legacy all-nodes/all-scenes assertions. Full navigation lives in RoadmapStreamingPlayModeTests.</summary>
    public sealed class ProceduralRoadmapPlayModeTests
    {
        [UnityTest,Timeout(120000)] public IEnumerator VisibleGeometryAndWeatherStayBoundedOnPhoneTabletAndWideScreen()
        {
            if(UnityEngine.Application.isBatchMode)Assert.Ignore("Requires rendered Game View");
            RoadmapStreamingPlayModeTests.Size(720,1600);
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/QuietCamp/Tests/Fixtures/Roadmap/RoadmapBenchmark.unity",new LoadSceneParameters(LoadSceneMode.Single));
            var harness=Object.FindAnyObjectByType<RoadmapBenchmarkHarness>();harness.autoRun=false;harness.Build();
            var map=harness.Map;
            foreach(var size in new[]{new Vector2Int(720,1600),new Vector2Int(1080,1920),new Vector2Int(1280,800),new Vector2Int(2560,1080)})
            {
                RoadmapStreamingPlayModeTests.Size(size.x,size.y);yield return RoadmapStreamingPlayModeTests.Frames(10);
                foreach(int index in new[]{0,6,15,20,30,180,339,345})
                {
                    RoadmapStreamingPlayModeTests.Seek(harness.Scroll,map,index);yield return RoadmapStreamingPlayModeTests.Ready(map);
                    Assert.LessOrEqual(map.ActiveChunks,3);Assert.LessOrEqual(map.ActiveIndices.Count,30);
                    Assert.LessOrEqual(map.ActiveControls,24);Assert.AreEqual(0,map.TruncatedModels);
                    Assert.Less(map.canvasRenderer.GetMesh().vertexCount,60000);
                    foreach(var g in map.GladePool)if(g.gameObject.activeSelf)Assert.Less(g.canvasRenderer.GetMesh().vertexCount,60000);
                    foreach(var active in map.ActiveIndices)Assert.AreEqual(QuietCamp.Domain.RoadmapReveal.Revealed,map.Reveal(active));
                    var weather=map.GetComponentInChildren<RoadmapWeatherGraphic>();Assert.Less(weather.canvasRenderer.GetMesh()?.vertexCount??0,6000);
                }
            }
        }
    }
}
#endif
