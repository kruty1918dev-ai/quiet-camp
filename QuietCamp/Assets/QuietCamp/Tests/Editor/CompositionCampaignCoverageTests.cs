using System;
using System.Linq;
using NUnit.Framework;
using QuietCamp.Application;
using QuietCamp.Domain;

namespace QuietCamp.Tests
{
    public sealed class CompositionCampaignCoverageTests
    {
        static RoadmapDefinition Map(string[] ids)=>new RoadmapDefinition
        {regions=new[]{new RoadmapRegionData{nodePositions=ids.Select(id=>new RoadmapNodeData{levelId=id}).ToArray()}}};

        [Test] public void PublicationRequiresEveryCampaignIdInItsExistingOrder()
        {
            var ids=Enumerable.Range(1,110).Select(i=>"test:"+i).ToArray();
            Assert.DoesNotThrow(()=>RoadmapCompositionAdapter.RequireCampaignCoverage(Map(ids),ids));
            Assert.Throws<InvalidOperationException>(()=>RoadmapCompositionAdapter.RequireCampaignCoverage(Map(ids.Take(30).ToArray()),ids));
            var reordered=(string[])ids.Clone();(reordered[3],reordered[4])=(reordered[4],reordered[3]);
            Assert.Throws<InvalidOperationException>(()=>RoadmapCompositionAdapter.RequireCampaignCoverage(Map(reordered),ids));
            var duplicated=(string[])ids.Clone();duplicated[109]=duplicated[108];
            Assert.Throws<InvalidOperationException>(()=>RoadmapCompositionAdapter.RequireCampaignCoverage(Map(duplicated),ids));
        }
    }
}
