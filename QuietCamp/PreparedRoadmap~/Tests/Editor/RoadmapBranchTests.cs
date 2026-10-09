
using QuietCamp.Presentation.UI.Prepared;
using RoadmapGladeGraphic = QuietCamp.Presentation.UI.Prepared.RoadmapGladeGraphic;
using RoadmapGraphic = QuietCamp.Presentation.UI.Prepared.RoadmapGraphic;
using RoadmapLayout = QuietCamp.Presentation.UI.Prepared.RoadmapLayout;
using RoadmapSceneGenerator = QuietCamp.Presentation.UI.Prepared.RoadmapSceneGenerator;
using RoadmapWeatherGraphic = QuietCamp.Presentation.UI.Prepared.RoadmapWeatherGraphic;
using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using NUnit.Framework;
using QuietCamp.Application;
using QuietCamp.Domain;
using QuietCamp.Presentation.UI;
using UnityEngine;
namespace QuietCamp.Tests.Prepared
{
    public sealed class RoadmapBranchTests
    {
        static RoadmapDefinition Fixture()=>JsonConvert.DeserializeObject<RoadmapDefinition>(File.ReadAllText(Path.Combine(UnityEngine.Application.dataPath,"QuietCamp/Tests/Fixtures/Roadmap/branch-presentation.json")));
        [TestCase(0,1)][TestCase(1,12)][TestCase(2,4)][TestCase(3,3)]
        public void EveryTypeKeepsItsLongBranchHidden(int region,int length)
        {
            var definition=Fixture();var branch=definition.regions[region].branches[0];branch.published=true;
            var map=new RoadmapCatalog(definition);var progress=new ProgressionService();
            int anchor=map.NodeIndex(branch.anchorNodeId);for(int i=0;i<=anchor;i++)progress.MarkCompleted(map.Nodes[i].levelId);
            var reveal=new RoadmapRevealState(map,progress);
            Assert.AreEqual(length,branch.nodes.Length);Assert.IsTrue(reveal.BranchVisible(branch));
            Assert.AreEqual(RoadmapReveal.Revealed,reveal.BranchNode(branch,0));
            if(length>1)Assert.AreEqual(RoadmapReveal.Silhouette,reveal.BranchNode(branch,1));
            for(int i=2;i<length;i++)Assert.AreEqual(RoadmapReveal.Hidden,reveal.BranchNode(branch,i));
            Assert.LessOrEqual(RoadmapBranchPolicy.TeaserCount(branch),2);
            foreach(var node in branch.nodes)progress.MarkCompleted(node.levelId);
            reveal.Refresh();for(int i=2;i<length;i++)Assert.AreEqual(RoadmapReveal.Hidden,reveal.BranchNode(branch,i));
            Assert.AreEqual(length,RoadmapBranchPolicy.Completed(branch,progress));
        }
        [Test] public void IdentityAndAccessOffersRoundTripWithoutAnyProgressMutation()
        {
            var data=Fixture();var copy=JsonConvert.DeserializeObject<RoadmapDefinition>(JsonConvert.SerializeObject(data));
            var progress=new ProgressionService();var library=RoadmapModelLibrary.Load();
            foreach(var region in copy.regions)
            {
                var branch=region.branches[0];Assert.NotNull(library.Get(RoadmapBranchPolicy.Landmark(branch)));Assert.AreEqual(6,branch.accessOptions.Length);
                foreach(var option in branch.accessOptions)Assert.IsTrue(RoadmapBranchPolicy.ValidAccess(option.method));
                var scene=RoadmapBranchArt.Create(branch);Assert.Greater(scene.Props.Count,1);Assert.LessOrEqual(scene.Props.Count,41);Assert.AreEqual(0,RoadmapBranchPolicy.Completed(branch,progress));
            }
            Assert.AreEqual(0,progress.CompletedCount);
        }
        [Test] public void InvalidLengthProviderConfigurationAndMissingHeroFailValidation()
        {
            var data=Fixture();var branch=data.regions[2].branches[0];branch.nodes=branch.nodes.Take(2).ToArray();
            Assert.IsTrue(RoadmapValidator.Validate(data).Contains("invalid-branch-length:"+branch.id));
            branch.accessOptions[2].placement=null;Assert.IsTrue(RoadmapValidator.Validate(data).Contains("invalid-branch-access:"+branch.id));
            branch.heroLandmark="missing";Assert.IsTrue(RoadmapValidator.Validate(data,modelExists:id=>id!="missing").Contains("missing-model:missing"));
        }
        [Test] public void JourneyPrerequisiteAndSubscriptionAreAuthoritativeAndRevocable()
        {
            var journey=new JourneyDefinition{id="story",published=true,entitlementId="story.owned",subscriptionEntitlementId="story.active",requiredLevelIds=new[]{"main.1"},levelIds=new[]{"story.1","story.2"}};
            var progress=new ProgressionService();bool active=true;var grants=new EntitlementSaveData();
            var gate=new JourneyAccessService(new JourneyCatalog(new[]{journey}),progress,grants,()=>false,id=>id=="story.active"&&active);
            Assert.IsFalse(gate.Evaluate("story.1").CanStart);progress.MarkCompleted("main.1");Assert.IsTrue(gate.Evaluate("story.1").CanStart);Assert.IsFalse(gate.Evaluate("story.2").CanStart);
            active=false;Assert.IsFalse(gate.Evaluate("story.1").CanStart);grants.ownedIds=new[]{"story.owned"};Assert.IsTrue(gate.Evaluate("story.1").CanStart);
            journey.published=false;Assert.IsFalse(gate.Evaluate("story.1").CanStart);
        }
        [Test] public void FreeStoryStillRequiresProgressAndDoesNotCreatePaidGrant()
        {
            var journey=new JourneyDefinition{id="free",published=true,requiredLevelIds=new[]{"main.1"},levelIds=new[]{"free.1","free.2"}};
            var progress=new ProgressionService();var grants=new EntitlementSaveData();var gate=new JourneyAccessService(new JourneyCatalog(new[]{journey}),progress,grants,()=>false);
            Assert.IsFalse(gate.Evaluate("free.1").CanStart);progress.MarkCompleted("main.1");Assert.IsTrue(gate.Evaluate("free.1").CanStart);Assert.IsFalse(gate.Evaluate("free.2").CanStart);Assert.IsEmpty(grants.ownedIds);
        }
    }
}
