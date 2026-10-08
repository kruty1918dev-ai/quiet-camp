#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using NUnit.Framework;
using QuietCamp.Presentation.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace QuietCamp.Tests
{
    public sealed class BonusRoadmapPlayModeTests
    {
        [UnityTest,Timeout(120000)] public IEnumerator RecycledBranchControlsBindTheCurrentIdentityAndRespectModalInputGate()
        {
            if(UnityEngine.Application.isBatchMode)Assert.Ignore("Requires rendered Game View");
            RoadmapStreamingPlayModeTests.Size(720,1600);
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/QuietCamp/Tests/Fixtures/Roadmap/RoadmapBenchmark.unity",new LoadSceneParameters(LoadSceneMode.Single));
            var harness=Object.FindAnyObjectByType<RoadmapBenchmarkHarness>();harness.autoRun=false;harness.Build();
            var map=harness.Map;int selected=0;string id=null;map.BranchSelected+=branch=>{selected++;id=branch.id;};
            foreach(int order in new[]{9,179,329,9})
            {
                var branch=map.Data.Definition.regions.SelectMany(r=>r.branches).First(b=>map.Data.NodeIndex(b.anchorNodeId)==order);
                int r=map.Data.RegionForNode(order);yield return RoadmapStreamingPlayModeTests.Ready(map);
                harness.Scroll.verticalNormalizedPosition=1-Mathf.Clamp01((map.Data.RegionStarts[r]+branch.y-harness.Scroll.viewport.rect.height*.45f)/(map.Data.Height-harness.Scroll.viewport.rect.height));
                yield return RoadmapStreamingPlayModeTests.Ready(map);yield return RoadmapStreamingPlayModeTests.Frames(3);
                var button=map.GetComponentsInChildren<Button>().Single(b=>b.name=="<button #branch-"+branch.id+">");
                int before=selected;map.InputEnabled=false;button.onClick.Invoke();Assert.AreEqual(before,selected);
                map.InputEnabled=true;button.onClick.Invoke();Assert.AreEqual(before+1,selected);Assert.AreEqual(branch.id,id);
            }
        }
    }
}
#endif
