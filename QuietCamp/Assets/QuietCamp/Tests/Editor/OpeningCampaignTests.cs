using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using QuietCamp.Domain;
using QuietCamp.Infrastructure;
using QuietCamp.Editor.LevelAuthoring;
using UnityEngine;

namespace QuietCamp.Tests
{
    public sealed class OpeningCampaignTests
    {
        [Test] public void StoriesFollowTheActualFirstTenAndDoNotInventAnotherCampaign()
        {
            var entries=OpeningCampaignStory.Read(Resources.Load<TextAsset>("QuietCamp/opening_campaign_story").text);
            for(int i=0;i<10;i++)
            {
                var level=LevelLoader.Load(entries[i].id);
                Assert.AreEqual(i+1,level.order);
                Assert.AreEqual("spring",level.environment.seasonId);
                Assert.IsTrue(SeasonAtmosphereCatalog.Get(entries[i].weather).season=="spring");
                if(i<9)Assert.AreEqual(entries[i+1].id,entries[i].nextId);
                Assert.IsNotEmpty(entries[i].canonicalBeat);Assert.IsNotEmpty(entries[i].proposedAdditions);
            }
            Assert.IsTrue(entries[2].mechanic.Contains("Тінь"));Assert.IsTrue(entries[3].mechanic.Contains("Тиша"));Assert.IsTrue(entries[4].mechanic.Contains("Друзі"));
        }
        [Test] public void HistoricalLevelsDoNotAcquireNewFindingsAndInvalidCatalogsAreRejected()
        {
            Assert.IsNull(OpeningCampaignStory.For(new LevelData{id="QC001",order=1}));
            Assert.Throws<InvalidOperationException>(()=>OpeningCampaignStory.Read("{\"schemaVersion\":1,\"entries\":[]}"));
            var catalog=JObject.Parse(Resources.Load<TextAsset>("QuietCamp/opening_campaign_story").text);
            catalog["entries"][1]["id"]="QC001";
            Assert.Throws<InvalidOperationException>(()=>OpeningCampaignStory.Read(catalog.ToString()));
        }
        [Test] public void CompactTerrainSamplingPreservesTheLegacySizeAndRejectsInvalidResolution()
        {
            var art=CampArtDefinition.Load(LevelLoader.Load("QC001"));
            var data=RiverTerrainAuthoringData.FromLegacy(art,129);
            Assert.AreEqual(129*129,data.baseHeights.Length);Assert.AreEqual(art.surroundExtent*2,data.size);
            Assert.Throws<ArgumentException>(()=>RiverTerrainAuthoringData.FromLegacy(art,128));
        }
    }
}
