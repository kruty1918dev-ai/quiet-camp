
using QuietCamp.Presentation.UI.Prepared;
using RoadmapGladeGraphic = QuietCamp.Presentation.UI.Prepared.RoadmapGladeGraphic;
using RoadmapGraphic = QuietCamp.Presentation.UI.Prepared.RoadmapGraphic;
using RoadmapLayout = QuietCamp.Presentation.UI.Prepared.RoadmapLayout;
using RoadmapSceneGenerator = QuietCamp.Presentation.UI.Prepared.RoadmapSceneGenerator;
using RoadmapWeatherGraphic = QuietCamp.Presentation.UI.Prepared.RoadmapWeatherGraphic;
using System;
using System.IO;
using Newtonsoft.Json;
using NUnit.Framework;
using QuietCamp.Application;
using QuietCamp.Domain;
using QuietCamp.Presentation.UI;
using UnityEngine;
namespace QuietCamp.Tests.Prepared
{
    public sealed class RoadmapBlendTests
    {
        static RoadmapCatalog Load(string fixture)=>new RoadmapCatalog(JsonConvert.DeserializeObject<RoadmapDefinition>(File.ReadAllText(Path.Combine(UnityEngine.Application.dataPath,"QuietCamp/Tests/Fixtures/Roadmap/transition-"+fixture+".json"))));
        static float Difference(Color a,Color b)=>Mathf.Max(Mathf.Abs(a.r-b.r),Mathf.Abs(a.g-b.g),Mathf.Abs(a.b-b.b));
        [TestCase("summer-autumn"),TestCase("winter-thaw")]
        public void PalettePhenologyAndAudioWeightsRemainContinuousAcrossEveryBoundary(string fixture)
        {
            var map=Load(fixture);var sampler=new RoadmapEnvironmentSampler(map);
            for(float y=0;y<map.Height;y+=3)
            {
                var a=sampler.Sample(y);var b=sampler.Sample(y+3);var va=new RoadmapVisualProfile(a);var vb=new RoadmapVisualProfile(b);
                Assert.Less(Difference(va.Palette.GrassLight,vb.Palette.GrassLight),.006f);
                Assert.Less(Difference(va.Palette.CanopyLight,vb.Palette.CanopyLight),.006f);
                Assert.Less(Difference(va.Palette.Fog,vb.Palette.Fog),.006f);
                Assert.Less(Difference(va.Sun,vb.Sun),.006f);
                Assert.Less(Math.Abs(a.Snow-b.Snow),.005f);Assert.Less(Math.Abs(a.Bare-b.Bare),.005f);
                Assert.Less(Math.Abs(a.Trees-b.Trees),.005f);Assert.Less(Math.Abs(a.Leaves-b.Leaves),.005f);Assert.Less(Math.Abs(a.Insects-b.Insects),.005f);
            }
            foreach(float boundary in map.RegionStarts)
            {
                var a=new RoadmapVisualProfile(sampler.Sample(boundary-.01f));var b=new RoadmapVisualProfile(sampler.Sample(boundary+.01f));
                Assert.Less(Difference(a.Palette.GrassLight,b.Palette.GrassLight),.0001f);
            }
        }
        [Test] public void WinterAndThawAccumulateThenMeltSnowWithoutChangingGameplaySummary()
        {
            var map=Load("winter-thaw");var sampler=new RoadmapEnvironmentSampler(map);float last=0;
            for(float y=0;y<map.RegionStarts[2]+850;y+=10){float snow=sampler.Sample(y).Snow;Assert.GreaterOrEqual(snow+.00001f,last);last=snow;}
            Assert.AreEqual(1,last,.001f);
            for(float y=map.RegionStarts[2]+1100;y<map.Height;y+=10){float snow=sampler.Sample(y).Snow;Assert.LessOrEqual(snow,last+.00001f);last=snow;}
            Assert.AreEqual(0,last,.001f);Assert.AreEqual("winter",map.Definition.regions[2].season);
            var frost=new RoadmapVisualProfile(sampler.Sample(map.RegionStarts[1]+850));Assert.AreEqual(.18f,frost.Environment.Snow,.001f);
            Assert.Less(frost.Palette.GrassLight.r,.85f,"First frost must not use a full white winter ground");
        }
        [Test] public void RegionsAndChunksHaveNoIndependentThemeSwitch()
        {
            var map=Load("summer-autumn");var sampler=new RoadmapEnvironmentSampler(map);
            var x=sampler.Sample(map.RegionStarts[1]-600);var y=sampler.Sample(map.RegionStarts[1]+600);
            Assert.Greater(x.Trees,y.Trees);Assert.Greater(x.Leaves,sampler.Sample(map.Height-1).Leaves);
            Assert.Less(x.Wind,y.Wind);Assert.Greater(sampler.Sample(map.RegionStarts[1]).Blend,0);
            var progress=new ProgressionService();var reveal=new RoadmapRevealState(map,progress);
            Assert.AreEqual(RoadmapReveal.Hidden,reveal.Main(10));Assert.Less(reveal.MaxScrollDistance(1100),map.RegionStarts[2]);
        }
        [Test] public void SamplerRoundTripAndRandomAccessAreDeterministic()
        {
            var map=Load("winter-thaw");var again=new RoadmapCatalog(JsonConvert.DeserializeObject<RoadmapDefinition>(JsonConvert.SerializeObject(map.Definition)));
            var a=new RoadmapEnvironmentSampler(map);var b=new RoadmapEnvironmentSampler(again);
            var rng=new System.Random(713);for(int i=0;i<1000;i++){float y=(float)rng.NextDouble()*map.Height;Assert.AreEqual(a.Sample(y).Snow,b.Sample(y).Snow);Assert.AreEqual(a.Sample(y).Trees,b.Sample(y).Trees);}
        }
        [Test] public void InvalidPhaseOrSeasonMismatchIsRejected()
        {
            var map=Load("winter-thaw");map.Definition.regions[1].environmentPhase="lava";Assert.IsTrue(RoadmapValidator.Validate(map.Definition).Contains("invalid-environment-phase:"+map.Definition.regions[1].id));
            map.Definition.regions[1].environmentPhase="summer";Assert.IsTrue(RoadmapValidator.Validate(map.Definition).Contains("invalid-environment-phase:"+map.Definition.regions[1].id));
        }
    }
}
