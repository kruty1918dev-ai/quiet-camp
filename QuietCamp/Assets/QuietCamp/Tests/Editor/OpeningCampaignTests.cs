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
        [Test] public void AllHundredStoriesFollowCampaignOrderSeasonsAndTheEnding()
        {
            var entries=OpeningCampaignStory.Read(Resources.Load<TextAsset>("QuietCamp/opening_campaign_story").text);
            Assert.AreEqual(100,entries.Length);
            for(int i=0;i<100;i++)
            {
                var level=LevelLoader.Load(entries[i].id);
                Assert.AreEqual(i+1,level.order);
                Assert.AreEqual(level.environment.seasonId,SeasonAtmosphereCatalog.Get(entries[i].weather).season);
                Assert.AreEqual(i==99?null:entries[i+1].id,entries[i].nextId);
            }
        }
        [Test] public void EveryLocationReservesADifferentPrimaryObject()
        {
            var entries=CampaignLandmarkCatalog.Read();
            Assert.AreEqual(100,entries.Select(e=>e.modelId).Distinct().Count());
            var models=CampaignLandmarkCatalog.Models();
            Assert.AreEqual(78,models.Length);
            foreach(var model in models)
            {
                var bounds=AuthoringPropCatalog.ModelBounds(model);
                Assert.Greater(bounds.size.magnitude,.5f,model.id);
                Assert.LessOrEqual(model.vertices.Length/9,4500,model.id);
                Assert.IsTrue(model.vertices.All(v=>!float.IsNaN(v)&&!float.IsInfinity(v)),model.id);
            }
            var art=new CampArtDefinition{primaryLandmarkId=entries[0].modelId,primaryLandmarkInstanceId="primary",instances=new[]{new CampArtDefinition.Instance{id="primary",modelId="clone"}},models=new[]{new CampArtDefinition.Model{id="clone",sourceModelId="some-other-model"}}};
            Assert.Throws<System.IO.InvalidDataException>(()=>CampaignLandmarkCatalog.Validate(art,1));
        }
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
