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
    public sealed class RoadmapBranchPlayModeTests
    {
        bool _async;
        sealed class NoGrantProvider : IRoadmapBranchUnlockProvider
        {
            public int Requests;
            public bool Available(RoadmapBranchData branch,RoadmapBranchAccessOption option)=>option.method=="rewarded";
            public System.Threading.Tasks.Task<bool> Request(RoadmapBranchData branch,RoadmapBranchAccessOption option){Requests++;return System.Threading.Tasks.Task.FromResult(true);}
        }
        static string Output=>Environment.GetEnvironmentVariable("QC_ROADMAP_QA_OUTPUT") is string output?Path.Combine(output,"branches"):Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath,"../../TestResults/roadmap-branches-2026-10-07"));
        [SetUp]public void Setup(){_async=UnityEditor.ShaderUtil.allowAsyncCompilation;UnityEditor.ShaderUtil.allowAsyncCompilation=false;QuietCampBootstrap.EditorDisableAudio=true;QuietCampBootstrap.EditorSaveFactory=()=>new SaveAdapter(memoryOnly:true);Directory.CreateDirectory(Output);}
        [UnityTearDown]public IEnumerator Cleanup(){QuietCampBootstrap.EditorDisableAudio=false;QuietCampBootstrap.EditorSaveFactory=null;RoadmapRepository.EditorMainPreview=null;UnityEditor.ShaderUtil.allowAsyncCompilation=_async;var boot=Object.FindAnyObjectByType<QuietCampBootstrap>();if(boot!=null)Object.Destroy(boot.gameObject);var harness=Object.FindAnyObjectByType<RoadmapBenchmarkHarness>();if(harness!=null)Object.Destroy(harness.gameObject);yield return null;}
        static IEnumerator Shot(string name){yield return RoadmapStreamingPlayModeTests.Shot("branch_"+name);var source=Path.Combine(RoadmapStreamingPlayModeTests.Output,"branch_"+name+".png");var destination=Path.Combine(Output,name+".png");if(File.Exists(destination))File.Delete(destination);File.Move(source,destination);}
        [UnityTest,Timeout(240000)]public IEnumerator FourIdentitiesStayBoundedAndFutureBranchesHidden()
        {
            if(UnityEngine.Application.isBatchMode)Assert.Ignore("Requires rendered Game View");RoadmapStreamingPlayModeTests.Size(720,1600);
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/QuietCamp/Tests/Fixtures/Roadmap/RoadmapBenchmark.unity",new LoadSceneParameters(LoadSceneMode.Single));
            var harness=Object.FindAnyObjectByType<RoadmapBenchmarkHarness>();harness.autoRun=false;harness.fixtureName="branch-presentation";harness.summaryName="branch-presentation-summaries";harness.outputDirectoryOverride=Output;harness.completedLevels=2;harness.Build();var map=harness.Map;
            for(int r=0;r<4;r++)
            {
                var branch=map.Data.Definition.regions[r].branches[0];int anchor=map.Data.NodeIndex(branch.anchorNodeId);
                harness.Services.Progression.Restore(map.Data.Nodes.Take(anchor+1).Select(n=>n.levelId),map.Data.Nodes[anchor].levelId,0);yield return null;
                float distance=map.Data.RegionStarts[r]+branch.y-harness.Scroll.viewport.rect.height*.5f;
                harness.Scroll.StopMovement();harness.Scroll.verticalNormalizedPosition=1-Mathf.Clamp01(distance/Mathf.Max(1,map.Data.Height-harness.Scroll.viewport.rect.height));yield return RoadmapStreamingPlayModeTests.Ready(map);
                float revealDeadline=Time.realtimeSinceStartup+15;while(map.RevealAnimating&&Time.realtimeSinceStartup<revealDeadline)yield return null;
                Assert.IsFalse(map.RevealAnimating);yield return RoadmapStreamingPlayModeTests.Frames(3);
                Assert.IsTrue(map.BranchVisible(branch));if(r<3)Assert.IsFalse(map.BranchVisible(map.Data.Definition.regions[r+1].branches[0]));
                Assert.LessOrEqual(map.ActiveChunks,3);Assert.AreEqual(1,map.WorldRenderer.MaterialCount);Assert.AreEqual(1,map.WorldRenderer.TextureCount);Assert.LessOrEqual(map.ActiveControls,24);
                yield return Shot(branch.visualIdentity);
            }
            RoadmapStreamingPlayModeTests.Size(1280,800);yield return RoadmapStreamingPlayModeTests.Frames(15);yield return RoadmapStreamingPlayModeTests.Ready(map);yield return Shot("tablet");
            harness.durationSeconds=20;yield return harness.Run();Assert.IsTrue(map.IsReady);
        }
        [UnityTest,Timeout(240000)]public IEnumerator BranchCardKeepsWorldAndShowsAccessOnlyAfterIntent()
        {
            if(UnityEngine.Application.isBatchMode)Assert.Ignore("Requires rendered Game View");RoadmapStreamingPlayModeTests.Size(720,1600);
            var definition=JsonConvert.DeserializeObject<RoadmapDefinition>(Resources.Load<TextAsset>("QuietCamp/Roadmaps/foundation_slice").text);
            var branch=JsonConvert.DeserializeObject<RoadmapDefinition>(File.ReadAllText(Path.Combine(UnityEngine.Application.dataPath,"QuietCamp/Tests/Fixtures/Roadmap/branch-presentation.json"))).regions[0].branches[0];
            branch.anchorNodeId=definition.regions[0].nodePositions[2].id;branch.requires=new[]{definition.regions[0].nodePositions[2].levelId};branch.x=.82f;branch.y=definition.regions[0].nodePositions[2].y+230;branch.radius=80;
            definition.regions[0].branches=new[]{branch};RoadmapRepository.EditorMainPreview=new RoadmapCatalog(definition);
            yield return SceneManager.LoadSceneAsync("Boot");float deadline=Time.realtimeSinceStartup+90;
            while(Object.FindAnyObjectByType<QuietCampBootstrap>()?.StartupReady!=true&&Time.realtimeSinceStartup<deadline){var ack=PrivacyBootTestSupport.Find("boot-policy-ack");if(BootPrivacyPanel.Current?.Accepted==false&&ack!=null&&ack.interactable)PrivacyBootTestSupport.Tap(ack);yield return null;}
            var services=QuietCampBootstrap.ServicesRef;Assert.NotNull(services);services.Tutorial.Skip();services.Localization.TrySetLanguage("uk");services.ReducedMotion=true;
            for(int i=0;i<3;i++)services.Progression.MarkCompleted(definition.regions[0].nodePositions[i].levelId);
            yield return RoadmapStreamingPlayModeTests.Frames(20);PrivacyBootTestSupport.Tap(PrivacyBootTestSupport.Find("levels"));yield return RoadmapStreamingPlayModeTests.Frames(3);
            var overlay=Object.FindObjectsByType<HtmlSurface>().Single(s=>s.name=="MenuOverlay");var map=overlay.GetComponentInChildren<RoadmapGraphic>();yield return RoadmapStreamingPlayModeTests.Ready(map);
            var scroll=overlay.Element("roadmap-scroll").GetComponent<ScrollRect>();RoadmapStreamingPlayModeTests.Seek(scroll,map,2);yield return RoadmapStreamingPlayModeTests.Ready(map);
            var button=map.GetComponentsInChildren<Button>().Single(b=>b.name=="<button #branch-"+branch.id+">");PrivacyBootTestSupport.Tap(button);yield return RoadmapStreamingPlayModeTests.Frames(20);
            Assert.NotNull(overlay.Element("branch-card"));Assert.NotNull(overlay.Element("roadmap-art"));Assert.IsNull(overlay.Element("branch-access-scroll"));Assert.IsFalse(map.InputEnabled);
            deadline=Time.realtimeSinceStartup+20;while(overlay.GetComponentInChildren<RoadmapBranchGraphic>()?.Ready!=true&&Time.realtimeSinceStartup<deadline)yield return null;
            var artwork=overlay.Element("branch-preview-art").GetComponentInChildren<RoadmapBranchGraphic>();
            Assert.IsTrue(artwork.Ready);Assert.Greater(artwork.VertexCount,100);yield return RoadmapStreamingPlayModeTests.Frames(3);
            File.WriteAllText(Path.Combine(Output,"card-preview-state.json"),JsonConvert.SerializeObject(new{size=new[]{artwork.rectTransform.rect.width,artwork.rectTransform.rect.height},vertices=artwork.VertexCount,culled=artwork.canvasRenderer.cull,materials=artwork.canvasRenderer.materialCount,alpha=artwork.canvasRenderer.GetInheritedAlpha(),position=artwork.transform.position.ToString()},Formatting.Indented));
            Assert.Greater(artwork.canvasRenderer.materialCount,0);Assert.IsFalse(artwork.canvasRenderer.cull);
            yield return Shot("card_invitation");PrivacyBootTestSupport.Tap(PrivacyBootTestSupport.Find("branch-explore"));yield return RoadmapStreamingPlayModeTests.Frames(15);
            Assert.NotNull(overlay.Element("branch-access-scroll"));Assert.IsNull(PrivacyBootTestSupport.Find("branch-enter"));Assert.AreEqual(3,services.Progression.CompletedCount);yield return Shot("card_preparing");
            // Synthetic in-memory offer scenario: never publish or save the staging product.
            branch.published=true;branch.bonusId=null;branch.journeyId="lighthouse";services.Journeys.Find("lighthouse").published=true;
            var provider=new NoGrantProvider();services.BranchUnlockProvider=provider;overlay.Refresh();yield return RoadmapStreamingPlayModeTests.Frames(20);
            Assert.NotNull(PrivacyBootTestSupport.Find("branch-access-rewarded"));Assert.IsFalse(PrivacyBootTestSupport.Find("branch-access-permanent-purchase").interactable);
            PrivacyBootTestSupport.Tap(PrivacyBootTestSupport.Find("branch-access-rewarded"));yield return RoadmapStreamingPlayModeTests.Frames(15);
            Assert.AreEqual(1,provider.Requests);Assert.IsFalse(services.CanStart("QC_LH001"));Assert.IsNull(PrivacyBootTestSupport.Find("branch-enter"));Assert.IsEmpty(services.Save.Entitlements.ownedIds);
            yield return Shot("card_access_offers");
            RoadmapStreamingPlayModeTests.Size(1280,800);yield return RoadmapStreamingPlayModeTests.Frames(20);
            deadline=Time.realtimeSinceStartup+20;while(!artwork.Ready&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.IsTrue(artwork.Ready);Assert.Greater(artwork.VertexCount,100);Assert.IsFalse(artwork.canvasRenderer.cull);yield return RoadmapStreamingPlayModeTests.Frames(3);yield return Shot("card_tablet");
            PrivacyBootTestSupport.Tap(PrivacyBootTestSupport.Find("branch-close"));yield return RoadmapStreamingPlayModeTests.Frames(45);
            Assert.IsNull(overlay.Element("branch-card"));map=overlay.GetComponentInChildren<RoadmapGraphic>();yield return RoadmapStreamingPlayModeTests.Ready(map);Assert.IsTrue(map.InputEnabled);Assert.AreEqual(1,map.WorldRenderer.TextureCount);
            yield return Shot("card_return");
        }
    }
}
#endif
