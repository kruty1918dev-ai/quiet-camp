using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using QuietCamp.Domain;
using QuietCamp.Infrastructure;
using QuietCamp.Editor.LevelAuthoring;
using QuietCamp.Presentation.World;
using UnityEngine;

namespace QuietCamp.Tests
{
    public sealed class OpeningCampaignTests
    {
        [TestCase(4,4,.5625f,52f,225f)]
        [TestCase(8,8,.5625f,60f,135f)]
        [TestCase(4,8,1.333333f,55f,80f)]
        [TestCase(8,4,1.777778f,58f,310f)]
        [TestCase(8,8,2.388889f,62f,225f)]
        public void GameplayFramingCentresGroundAndKeepsTentsInsideUiViewport(int width,int height,float aspect,float pitch,float yaw)
        {
            var go=new GameObject("Board framing test");
            try
            {
                var camera=go.AddComponent<Camera>();CameraFitter.Configure(camera,new Vector3(pitch,yaw,0));camera.aspect=aspect;
                var level=new LevelData{width=width,height=height};
                var rect=new Rect(.03f,.31f,.94f,.54f);
                Assert.IsTrue(CameraFitter.FitGameplayBoard(camera,level,rect,1.12f));
                var bounds=CameraFitter.GameplayBounds(level);
                var centre=camera.WorldToViewportPoint(Vector3.zero);
                Assert.That(centre.x,Is.EqualTo(rect.center.x).Within(.0001));
                Assert.That(centre.y,Is.EqualTo(rect.center.y).Within(.0001));
                for(int i=0;i<8;i++)
                {
                    var point=bounds.center+new Vector3((i&1)==0?-bounds.extents.x:bounds.extents.x,(i&2)==0?-bounds.extents.y:bounds.extents.y,(i&4)==0?-bounds.extents.z:bounds.extents.z);
                    var view=camera.WorldToViewportPoint(point);
                    Assert.That(view.x,Is.InRange(rect.xMin,rect.xMax));Assert.That(view.y,Is.InRange(rect.yMin,rect.yMax));
                    Assert.That(view.z,Is.GreaterThan(camera.nearClipPlane));
                }
            }
            finally{UnityEngine.Object.DestroyImmediate(go);}
        }

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
                foreach(var text in new[]{entries[i].title,entries[i].arrival,entries[i].finding,entries[i].objective,entries[i].after})
                {
                    Assert.IsNotNull(text,entries[i].id);
                    Assert.Less(text.IndexOf("??",StringComparison.Ordinal),0,entries[i].id+": lossy narrative encoding");
                    Assert.Less(text.IndexOf('\uFFFD'),0,entries[i].id+": invalid UTF-8 replacement");
                }
                foreach(var beat in level.environmentalStory.beats)
                    Assert.Less(beat.storyBeat.IndexOf("??",StringComparison.Ordinal),0,entries[i].id+": corrupted published story");
            }
        }
        [Test] public void EveryLocationReservesADifferentPrimaryObject()
        {
            var entries=CampaignLandmarkCatalog.Read();
            Assert.AreEqual(100,entries.Select(e=>e.modelId).Distinct().Count());
            var models=CampaignLandmarkCatalog.Models();
            Assert.AreEqual(79,models.Length);
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
        [Test] public void LargerLandmarkReplacementKeepsItsWholeFootprintOutsideThePuzzle()
        {
            var document=new LevelAuthoringDocument{level=new LevelData{width=6,height=5}};
            var model=new CampArtDefinition.Model{vertices=new[]{-4f,0,-2f,4f,0,2f}};
            var at=WorldStudyFactory.OutsideField(document,model,0,0,1,90);
            Assert.IsTrue(Mathf.Abs(at.x)>=3+.65f+2-.001f || Mathf.Abs(at.y)>=2.5f+.65f+4-.001f);
            var unchanged=WorldStudyFactory.OutsideField(document,model,12,8,1,90);
            Assert.AreEqual(new Vector2(12,8),unchanged);
        }
        [Test] public void DraftCanBeSavedWhileAReaderAllowsWritingButDeniesReplacement()
        {
            string folder=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"qc-draft-save-"+Guid.NewGuid().ToString("N"));
            string path=System.IO.Path.Combine(folder,"test.draft.json");
            try
            {
                var d=new LevelAuthoringDocument{level=new LevelData{id="QC_READER"},art=new CampArtDefinition(),title="Before"};
                d.Save(path);
                using(var reader=new System.IO.FileStream(path,System.IO.FileMode.Open,System.IO.FileAccess.Read,System.IO.FileShare.ReadWrite))
                {d.title="After";d.Save(path);}
                Assert.AreEqual("After",LevelAuthoringDocument.Open(path).title);
                Assert.IsFalse(System.IO.File.Exists(path+".pending"));
            }
            finally{if(System.IO.Directory.Exists(folder))System.IO.Directory.Delete(folder,true);}
        }
        [Test] public void GroundMarginsPreserveExistingTerrainAndUseUpwardFacingTriangles()
        {
            var d=LevelAuthoringDocument.Open("Assets/QuietCamp/Authoring/WorldStudies/AircraftSeasons/summer.draft.json");
            var original=Newtonsoft.Json.JsonConvert.SerializeObject(d.art.landscape);
            var heights=d.art.landscape.heights.ToArray();var meshes=d.art.landscape.meshes.ToArray();
            d.art.surroundExtent=d.art.landscape.size*.5f+8;
            RiverTerrainAuthoringData.EnsureGroundMargins(d.art);d.art.landscape.Validate();
            CollectionAssert.AreEqual(heights,d.art.landscape.heights);
            foreach(var mesh in meshes)Assert.IsTrue(d.art.landscape.meshes.Contains(mesh));
            var margin=d.art.landscape.meshes.Single(m=>m.name=="Surrounding ground margins");
            Vector3 P(int i)=>new Vector3(margin.vertices[i*3],margin.vertices[i*3+1],margin.vertices[i*3+2]);
            for(int t=0;t<margin.triangles.Length;t+=3)Assert.Greater(Vector3.Cross(P(margin.triangles[t+1])-P(margin.triangles[t]),P(margin.triangles[t+2])-P(margin.triangles[t])).y,0);
            var once=Newtonsoft.Json.JsonConvert.SerializeObject(d.art.landscape);
            RiverTerrainAuthoringData.EnsureGroundMargins(d.art);
            Assert.AreEqual(once,Newtonsoft.Json.JsonConvert.SerializeObject(d.art.landscape));
            d.art.surroundExtent=d.art.landscape.size*.5f;RiverTerrainAuthoringData.EnsureGroundMargins(d.art);
            Assert.AreEqual(original,Newtonsoft.Json.JsonConvert.SerializeObject(d.art.landscape));
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
            Assert.IsTrue(entries[2].mechanic.IndexOf("тінь",StringComparison.OrdinalIgnoreCase)>=0);
            Assert.IsFalse(LevelLoader.Load(entries[3].id).guests.Any(g=>g.quiet));
            Assert.IsEmpty(LevelLoader.Load(entries[4].id).friends);
            Assert.IsTrue(entries[8].mechanic.IndexOf("тиша",StringComparison.OrdinalIgnoreCase)>=0);
            Assert.IsTrue(entries[10].mechanic.IndexOf("друзі",StringComparison.OrdinalIgnoreCase)>=0);
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
