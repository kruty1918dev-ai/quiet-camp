
using QuietCamp.Presentation.UI.Prepared;
using RoadmapGladeGraphic = QuietCamp.Presentation.UI.Prepared.RoadmapGladeGraphic;
using RoadmapGraphic = QuietCamp.Presentation.UI.Prepared.RoadmapGraphic;
using RoadmapLayout = QuietCamp.Presentation.UI.Prepared.RoadmapLayout;
using RoadmapSceneGenerator = QuietCamp.Presentation.UI.Prepared.RoadmapSceneGenerator;
using RoadmapWeatherGraphic = QuietCamp.Presentation.UI.Prepared.RoadmapWeatherGraphic;
using System;
using System.Linq;
using Newtonsoft.Json;
using NUnit.Framework;
using QuietCamp.Application;
using QuietCamp.Domain;
using QuietCamp.Infrastructure;

namespace QuietCamp.Tests.Prepared
{
    public sealed class RoadmapRevealTests
    {
        static RoadmapCatalog Main()=>new RoadmapCatalog(RoadmapCompiler.Build(CampContent.Summaries,null));
        static void Complete(RoadmapCatalog map,ProgressionService progress,int count)
        {for(int i=0;i<count;i++)progress.MarkCompleted(map.Nodes[i].levelId);}
        [TestCase(0),TestCase(1),TestCase(10)]
        public void OnlyCompletedCurrentAndTwoFutureNodesAreKnown(int completed)
        {
            var map=Main();var progress=new ProgressionService();Complete(map,progress,completed);
            var reveal=new RoadmapRevealState(map,progress);
            Assert.AreEqual(completed,reveal.Frontier);Assert.AreEqual(completed+2,reveal.LastKnown);
            for(int i=0;i<map.Nodes.Length;i++)Assert.AreEqual(i<=completed?RoadmapReveal.Revealed:i<=completed+2?RoadmapReveal.Silhouette:RoadmapReveal.Hidden,reveal.Main(i));
            foreach(float height in new[]{450,1100,2000})
                Assert.AreEqual(Math.Max(0,Math.Min(map.Height-height,map.Y(reveal.LastKnown)+180-height*.68f)),reveal.MaxScrollDistance(height),.01f);
        }
        static RoadmapBranchData Journey(RoadmapCatalog map)
        {
            var branch=new RoadmapBranchData{id="trail",type="story-journey",anchorNodeId=map.Nodes[0].id,journeyId="lighthouse",published=true,requires=new[]{map.Nodes[0].levelId},teaserDepth=2,
                nodes=Enumerable.Range(1,8).Select(i=>new RoadmapBranchNodeData{id="branch-node:"+i,levelId="QC_LH"+i.ToString("000")}).ToArray()};
            map.Definition.regions[0].branches=new[]{branch};return branch;
        }
        [Test] public void UnlockNeverRevealsAllEightBranchNodes()
        {
            var map=Main();var branch=Journey(map);var progress=new ProgressionService();var reveal=new RoadmapRevealState(map,progress);
            Assert.IsTrue(reveal.BranchVisible(branch));Assert.AreEqual(RoadmapReveal.Silhouette,reveal.BranchNode(branch,0));
            Complete(map,progress,1);reveal.Refresh();Assert.AreEqual(RoadmapReveal.Revealed,reveal.BranchNode(branch,0));
            Assert.AreEqual(RoadmapReveal.Silhouette,reveal.BranchNode(branch,1));
            for(int i=2;i<8;i++)Assert.AreEqual(RoadmapReveal.Hidden,reveal.BranchNode(branch,i));
        }
        [Test] public void PartialBranchProgressIsIndependentOfMainAndRestoresOnRestart()
        {
            var map=Main();var branch=Journey(map);var progress=new ProgressionService();Complete(map,progress,1);
            foreach(var node in branch.nodes.Take(3))progress.MarkCompleted(node.levelId);
            var restore=new ProgressionService();restore.Restore(JsonConvert.DeserializeObject<string[]>(JsonConvert.SerializeObject(progress.CompletedIds)),branch.nodes[2].levelId,0);
            var reveal=new RoadmapRevealState(map,restore);Assert.AreEqual(1,reveal.Frontier);Assert.AreEqual(3,reveal.BranchFrontier(branch));
            Assert.AreEqual(RoadmapReveal.Hidden,reveal.BranchNode(branch,3),"Main map must keep a short teaser; journey map owns completed detail");
            // The branch's own catalog uses the same policy, with its own level IDs.
            var summaries=branch.nodes.Select((n,i)=>new LevelSummary{id=n.levelId,number=i+1,width=4,height=4}).ToArray();
            var journeyMap=new RoadmapCatalog(RoadmapCompiler.Build(summaries,null,journeyId:"lighthouse"));
            var journeyReveal=new RoadmapRevealState(journeyMap,restore);Assert.AreEqual(3,journeyReveal.Frontier);Assert.AreEqual(RoadmapReveal.Revealed,journeyReveal.Main(2));Assert.AreEqual(RoadmapReveal.Hidden,journeyReveal.Main(6));
        }
        [Test] public void MigratedNonContiguousSaveKeepsItsFrontierAndIgnoresUnknownIds()
        {
            var map=Main();var generated=Array.FindIndex(map.Nodes,n=>n.levelId.StartsWith("gen:qc_camp:"));Assert.GreaterOrEqual(generated,0);
            var legacy="GEN"+map.Nodes[generated].levelId.Substring("gen:qc_camp:".Length);
            var save=new SaveAdapter(memoryOnly:true);save.Progress.completedIds=new[]{map.Nodes[0].levelId,legacy,"deleted-campaign-level"};save.Progress.lastLevelId=map.Nodes[0].levelId;
            CampContent.Migrate(save);var progress=new ProgressionService();progress.Restore(save.Progress.completedIds,save.Progress.lastLevelId,0);
            var reveal=new RoadmapRevealState(map,progress);Assert.AreEqual(generated+1,reveal.Frontier);
            var restarted=new ProgressionService();restarted.Restore(save.Progress.completedIds,legacy,0);Assert.AreEqual(reveal.LastKnown,new RoadmapRevealState(map,restarted).LastKnown);
        }
        [Test] public void ReplayAndStaleScrollNeverMoveTheRevealFrontier()
        {
            var map=Main();var progress=new ProgressionService();Complete(map,progress,10);var reveal=new RoadmapRevealState(map,progress);
            int revision=progress.Revision;Assert.IsFalse(progress.MarkCompleted(map.Nodes[0].levelId));Assert.AreEqual(revision,progress.Revision);Assert.IsFalse(reveal.Refresh());
            Assert.AreEqual(10,reveal.Frontier);Assert.AreEqual(RoadmapReveal.Hidden,reveal.Main(29));
            Assert.AreEqual(reveal.MaxScrollDistance(1100),new RoadmapRevealState(map,progress).MaxScrollDistance(1100));
        }
        [Test] public void OnlyNearestPendingBranchAndCompletedBranchesRemainVisible()
        {
            var map=Main();var a=Journey(map);var b=new RoadmapBranchData{id="bonus",type="bonus",bonusId="bonus-level",anchorNodeId=map.Nodes[2].id,published=true,nodes=new[]{new RoadmapBranchNodeData{levelId="bonus-level"}}};
            map.Definition.regions[0].branches=new[]{a,b};var progress=new ProgressionService();Complete(map,progress,3);
            var reveal=new RoadmapRevealState(map,progress);Assert.IsFalse(reveal.BranchVisible(a));Assert.IsTrue(reveal.BranchVisible(b));
            progress.MarkCompleted("bonus-level");reveal.Refresh();Assert.IsTrue(reveal.BranchVisible(a));Assert.IsTrue(reveal.BranchVisible(b));Assert.AreEqual(RoadmapReveal.Hidden,reveal.BranchNode(b,1));
        }
        [Test] public void RevealedEndBonusRemainsReachableWithoutExposingUnrevealedBranch()
        {
            var map=Main();int r=map.RegionForNode(map.Nodes.Length-1);
            var bonus=new RoadmapBranchData{id="end-bonus",type="bonus",anchorNodeId=map.Nodes[map.Nodes.Length-1].id,y=map.Nodes[map.Nodes.Length-1].y+260,radius=100};
            map.Definition.regions[r].branches=new[]{bonus};
            var progress=new ProgressionService();var fresh=new RoadmapRevealState(map,progress);
            Assert.IsFalse(fresh.BranchVisible(bonus));
            Assert.LessOrEqual(fresh.MaxScrollDistance(500)+340,map.Y(fresh.LastKnown)+180+.01f);
            Complete(map,progress,map.Nodes.Length);var completed=new RoadmapRevealState(map,progress);
            Assert.IsTrue(completed.BranchVisible(bonus));
            float bottom=map.RegionStarts[r]+bonus.y+bonus.radius;
            Assert.GreaterOrEqual(completed.MaxScrollDistance(500)+500,bottom,"The visible bonus must remain inside the reachable viewport");
        }
    }
}
