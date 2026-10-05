using System.Linq;
using NUnit.Framework;
using QuietCamp.Domain;
using QuietCamp.Infrastructure;
using QuietCamp.Presentation;
using QuietCamp.Presentation.World;
using UnityEngine;

namespace QuietCamp.Tests
{
    public sealed class EnvironmentRenderingTests
    {
        [Test] public void QualityRecoversOnlyAfterSustainedHeadroomAndDoesNotOscillate()
        {
            var policy=new CampQualityPolicy();policy.Reset(1);
            for(int i=0;i<4;i++)Assert.IsFalse(policy.Sample(.04f,2));
            for(int i=0;i<3;i++)Assert.IsFalse(policy.Sample(.024f,2));
            Assert.IsTrue(policy.Sample(.024f,2));Assert.AreEqual(0,policy.Tier);
            for(int i=0;i<4;i++)policy.Sample(.0167f,2);
            for(int i=0;i<22;i++)Assert.IsFalse(policy.Sample(.0167f,2));
            Assert.IsTrue(policy.Sample(.0167f,2));Assert.AreEqual(1,policy.Tier);
            for(int i=0;i<3;i++)Assert.IsFalse(policy.Sample(.026f,2));
            Assert.AreEqual(1,policy.Tier,"Warm-up must exclude changes immediately after a profile switch");
        }
        [Test] public void ManualEffectCeilingsPreserveTheSelectedRenderProfileAndFocusRestartsWarmup()
        {
            int original=QualitySettings.GetQualityLevel(),target=UnityEngine.Application.targetFrameRate;
            var root=new GameObject("adaptive profile test");
            using(var services=new GameServices(new SaveAdapter(),null,null,null,null,null,null,null,null,null,null,null,null,null,null,null))
            {
                var adapter=root.AddComponent<AdaptiveCampQuality>();
                try
                {
                    services.Settings.quality=3;adapter.Configure(services);
                    for(int i=0;i<8;i++)adapter.ObserveFrameWindow(.03f,2);
                    Assert.AreEqual(1,services.EffectiveQuality,"Manual High may reduce peripheral work under sustained overload");
                    Assert.AreEqual(2,QualitySettings.GetQualityLevel(),"The selected High render profile must stay selected");
                    typeof(AdaptiveCampQuality).GetMethod("OnApplicationFocus",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(adapter,new object[]{true});
                    for(int i=0;i<4;i++)adapter.ObserveFrameWindow(.03f,2);
                    Assert.AreEqual(1,services.EffectiveQuality,"Focus recovery must exclude the warm-up interval");
                    for(int i=0;i<4;i++)adapter.ObserveFrameWindow(.03f,2);
                    Assert.AreEqual(0,services.EffectiveQuality);Assert.AreEqual(2,QualitySettings.GetQualityLevel());
                    services.Settings.quality=2;adapter.Configure(services);
                    for(int i=0;i<40;i++)adapter.ObserveFrameWindow(.015f,2);
                    Assert.AreEqual(1,services.EffectiveQuality,"Manual Balanced must not recover above its ceiling");
                    Assert.AreEqual(1,QualitySettings.GetQualityLevel());
                    services.Settings.quality=1;adapter.Configure(services);
                    for(int i=0;i<40;i++)adapter.ObserveFrameWindow(.015f,2);
                    Assert.AreEqual(0,services.EffectiveQuality);Assert.AreEqual(0,QualitySettings.GetQualityLevel());
                }
                finally{Object.DestroyImmediate(root);QualitySettings.SetQualityLevel(original,true);UnityEngine.Application.targetFrameRate=target;}
            }
        }
        [Test] public void ProtectedUnderstoryHasLessWindThanExposedCanopyAndNeighbouringRootsAreCoherent()
        {
            var field=new WindShelterField(Enumerable.Repeat(1f,16).ToArray(),4,new Vector2(-5,-5),new Vector2(10,10),4);
            var wind=new WindSim(4,.5f,10,20);wind.SetShelter(field);wind.Advance(1);
            var low=wind.Sample(Vector3.zero,.1f);var high=wind.Sample(Vector3.zero,5);
            Assert.Less(low.Strength,high.Strength*.6f);
            var neighbour=wind.Sample(new Vector3(.1f,0,.1f),.1f);
            Assert.Less(Mathf.Abs(low.Strength-neighbour.Strength),.03f);
            Assert.Greater(wind.Sample(new Vector3(20,0,20),.1f).Strength,low.Strength);
            var zero=WindFieldMath.Evaluate(Vector2.right,0,20,Vector3.zero,2,field);Assert.AreEqual(0,zero.Strength);
        }
        [Test] public void ShelterBilinearSamplingAndTextureShareQuantizedValues()
        {
            var field=new WindShelterField(new[]{0f,1f,1f,0f},2,Vector2.zero,Vector2.one,3);
            Assert.AreEqual(.5f,field.Sample(new Vector3(.5f,0,.5f)),.001f);
            Assert.AreEqual(0,field.Sample(Vector3.left));
            var texture=field.CreateTexture();Assert.AreEqual(2,texture.width);Assert.AreEqual(TextureFormat.RGBA32,texture.format);Object.DestroyImmediate(texture);
        }
        [Test] public void SeasonalCompositionKeepsBigObjectsOutOfPuzzleTrailsAndWater()
        {
            var level=new LevelData{width=6,height=5,decorSeed=3,entry=new[]{0,0},blocked=new int[0][],environment=new EnvironmentCompositionData{seasonId="summer",shore=new ShorelineData{side="left",offset=9,width=7}}};
            Assert.IsFalse(EnvironmentComposer.CanTree(level,new Vector3(0,0,0),3));
            Assert.IsFalse(EnvironmentComposer.CanTree(level,new Vector3(-8,0,0),3));
            Assert.IsFalse(EnvironmentComposer.CanTree(level,CampTrail.Centre(level,new Cell(0,0),5),3));
            Assert.IsFalse(EnvironmentComposer.CanScenicBounds(level,new Bounds(Vector3.zero,new Vector3(3,2,3))));
            Assert.IsFalse(EnvironmentComposer.CanScenicBounds(level,new Bounds(new Vector3(-5,0,9),new Vector3(3,2,3))),"Whole ruin bounds must remain outside the inner bank");
            Assert.IsTrue(EnvironmentComposer.CanScenicBounds(level,new Bounds(new Vector3(-16,0,9),new Vector3(2,1,2))),"Far-bank scenery must remain available beyond the actual water band");
            var spring=SeasonPalette.For("spring");var winter=SeasonPalette.For("winter");
            Assert.Greater(spring.FlowerWeight,winter.FlowerWeight);Assert.AreNotEqual(spring.GrassLight,winter.GrassLight);
        }
        [Test] public void LongEveningShadowCannotCrossThePuzzleAndEndBeyondItsOppositeEdge()
        {
            var previous=RenderSettings.sun;var root=new GameObject("shadow sweep test");var sun=root.AddComponent<Light>();sun.type=LightType.Directional;
            sun.transform.eulerAngles=new Vector3(18,65,0);RenderSettings.sun=sun;
            try
            {
                var level=new LevelData{width=6,height=5,entry=new[]{0,0},blocked=new int[0][]};
                var at=new Vector3(-8,0,-8);float height=5;var toward=-sun.transform.forward;
                var endpoint=at-new Vector3(toward.x,0,toward.z)*(height/toward.y);
                Assert.Greater(endpoint.x,level.width*.5f+1.2f,"Fixture must actually project beyond the far edge");
                Assert.IsFalse(EnvironmentComposer.CanTree(level,at,height),"The shadow crosses the glade even though its endpoint is outside it");
            }
            finally{RenderSettings.sun=previous;Object.DestroyImmediate(root);}
        }
    }
}
