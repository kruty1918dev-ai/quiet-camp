using System.Linq;
using Newtonsoft.Json;
using NUnit.Framework;
using QuietCamp.Infrastructure;
using QuietCamp.Presentation.UI;
using UnityEngine;

namespace QuietCamp.Tests
{
    public sealed class ProceduralRoadmapTests
    {
        [Test] public void EveryPreviewUsesFrozenContentAndTheGameplayAtmosphereResolver()
        {
            var catalog=AtmosphereCatalog.Load();Assert.AreEqual(270,CampContent.Summaries.Count);
            foreach(var summary in CampContent.Summaries)
            {
                var level=LevelLoader.Load(summary.id);
                Assert.AreEqual(level.decorSeed,summary.decorSeed,summary.id);
                Assert.AreEqual(level.lighting,summary.lighting,summary.id);
                Assert.AreEqual(level.environmentPreset,summary.environmentPreset,summary.id);
                Assert.AreEqual(JsonConvert.SerializeObject(level.objects),JsonConvert.SerializeObject(summary.mapObjects),summary.id);
                Assert.AreEqual(JsonConvert.SerializeObject(level.accessPoints),JsonConvert.SerializeObject(summary.accessPoints),summary.id);
                Assert.AreEqual(JsonConvert.SerializeObject(level.canopies),JsonConvert.SerializeObject(summary.canopies),summary.id);
                var scene=RoadmapSceneGenerator.Generate(summary,catalog);
                Assert.AreSame(catalog.Resolve(level.id,level.lighting),scene.Light,summary.id);
                foreach(var prop in level.objects)
                    Assert.IsTrue(scene.Props.Any(p=>p.Asset==prop.assetId&&p.Position==new Vector3(prop.x+.5f-level.width*.5f,0,prop.z+.5f-level.height*.5f)),summary.id+" missing prop");
            }
        }
        [Test] public void SceneGenerationIsDeterministicAndDoesNotUseThePuzzleWitness()
        {
            var catalog=AtmosphereCatalog.Load();var summary=CampContent.Summary("QC003");
            var a=RoadmapSceneGenerator.Generate(summary,catalog);var b=RoadmapSceneGenerator.Generate(summary,catalog);
            Assert.AreEqual(a.Props.Count,b.Props.Count);
            for(int i=0;i<a.Props.Count;i++)
            {
                Assert.AreEqual(a.Props[i].Position,b.Props[i].Position);
                Assert.AreEqual(a.Props[i].Yaw,b.Props[i].Yaw);
                Assert.AreEqual(a.Props[i].Height,b.Props[i].Height);
                if(a.Props[i].Tent)Assert.Less(a.Props[i].Position.z,-summary.height*.5f);
            }
            Assert.Greater(a.Props.Count(p=>p.Sway),15);
            Assert.Greater(RoadmapSceneGenerator.Access(CampContent.Summaries.First(s=>s.accessPoints.Length>0)).Count(),1);
        }
        [Test] public void WeatherPreviewFollowsTheSameTargetsAndEaseAsGameplay()
        {
            foreach(var level in CampContent.Summaries)
            {
                float cloud=0,rain=0;
                for(int i=1;i<=160;i++)
                {
                    var target=CampWeatherTimeline.Target(i*.5f,level.decorSeed);float ease=1-Mathf.Exp(-.5f/5);
                    cloud=Mathf.Lerp(cloud,target.Cloud,ease);rain=Mathf.Lerp(rain,target.Rain,ease);
                }
                var snapshot=CampWeatherTimeline.Preview(level.decorSeed,80);
                Assert.AreEqual(cloud,snapshot.Cloud,.00001f);Assert.AreEqual(rain,snapshot.Rain,.00001f);
                Assert.That(snapshot.Cloud,Is.InRange(0,1));Assert.That(snapshot.Rain,Is.InRange(0,1));
                Assert.Less(CampWeatherTimeline.Preview(level.decorSeed,25).Rain,.01f);
                Assert.Greater(CampWeatherTimeline.Preview(level.decorSeed,80).Rain,.2f);
            }
        }
        [Test] public void BakedGameModelsAreCompleteFiniteAndCanBeReadWithoutMeshReadWrite()
        {
            var library=RoadmapModelLibrary.Load();Assert.GreaterOrEqual(library.Count,16);
            foreach(var entry in AssetCatalog.Load().Entries)
            {
                var model=library.Get(entry.assetId);Assert.NotNull(model,entry.assetId);
                Assert.AreEqual(0,model.data.Length%18);Assert.AreEqual(model.data.Length/18,model.colors.Length);
                Assert.Greater(model.Positions.Length,3);
                foreach(var p in model.Positions)Assert.IsFalse(float.IsNaN(p.x)||float.IsInfinity(p.y));
                Assert.That(model.Positions.Max(p=>p.y),Is.EqualTo(1).Within(.0001f));
                Assert.That(model.Positions.Min(p=>p.y),Is.EqualTo(0).Within(.0001f));
            }
            Assert.NotNull(Resources.Load<Shader>("QuietCamp/RoadmapCanopy"));
        }
        [Test] public void AuthoredRainAndSeasonsStayConsistentWithGameplay()
        {
            var catalog=AtmosphereCatalog.Load();
            foreach(var summary in CampContent.Summaries.Where(s=>s.environment!=null))
            {
                var scene=RoadmapSceneGenerator.Generate(summary,catalog);
                var initial=CampWeatherTimeline.Initial(summary.environment.weatherId);
                // Winter scenes precipitate as snow: rain is suppressed to zero.
                Assert.AreEqual(scene.Winter?0f:initial.Rain,scene.Weather.Rain,.00001f,summary.id);
                Assert.AreEqual(initial.Cloud,scene.Weather.Cloud,.00001f,summary.id);
                float elapsed=scene.WeatherTime+.5f;
                var target=CampWeatherTimeline.Target(elapsed,summary.decorSeed,summary.environment.weatherId);
                scene.Advance(.5f);float ease=1-Mathf.Exp(-.5f/5);
                Assert.AreEqual(scene.Winter?0f:Mathf.Lerp(initial.Rain,target.Rain,ease),scene.Weather.Rain,.00001f,summary.id);
                Assert.AreEqual(Mathf.Lerp(initial.Cloud,target.Cloud,ease),scene.Weather.Cloud,.00001f,summary.id);
                if(summary.environment.weatherId=="clear"||summary.environment.weatherId=="mist")Assert.AreEqual(0,scene.Weather.Rain,summary.id);
            }
            var winter=QuietCamp.Presentation.World.SeasonPalette.For("winter");var summer=QuietCamp.Presentation.World.SeasonPalette.For("summer");
            Assert.AreNotEqual(winter.GrassLight,summer.GrassLight);
        }
        [Test] public void ManualWeatherMomentsRespectAuthoredWeatherAndWinterPrecipitation()
        {
            var catalog=AtmosphereCatalog.Load();
            foreach(var summary in CampContent.Summaries)
            foreach(float time in new[]{0f,25f,80f,140f})
            {
                var scene=RoadmapSceneGenerator.Generate(summary,catalog);scene.SetMoment(time);
                var expected=CampWeatherTimeline.Preview(summary.decorSeed,time,summary.environment?.weatherId,scene.Winter);
                Assert.AreEqual(expected.Cloud,scene.Weather.Cloud,.00001f,summary.id);
                Assert.AreEqual(expected.Rain,scene.Weather.Rain,.00001f,summary.id);
                if(summary.environment.weatherId=="rain"&&!scene.Winter)Assert.Greater(scene.Weather.Rain,.5f,summary.id);
                if(scene.Winter){Assert.AreEqual(0,scene.Weather.Rain);Assert.Greater(scene.Snowfall,0);}
                if(summary.environment.weatherId=="clear")Assert.AreEqual(0,scene.Weather.Rain);
            }
        }
        [Test] public void ShoreBandAndSnowSurfacesMatchTheGameplayGeometryAndSeason()
        {
            foreach(string side in new[]{"left","right","front","back"})
            {
                var shore=new QuietCamp.Domain.ShorelineData{side=side,offset=9,width=4,seed=217};
                for(int i=-24;i<=24;i++)
                {
                    var centre=QuietCamp.Presentation.World.ShorelineGeometry.Point(shore,i,.5f);
                    Assert.IsTrue(QuietCamp.Presentation.World.ShorelineGeometry.Contains(shore,centre));
                    var outside=centre+QuietCamp.Presentation.World.ShorelineGeometry.Side(shore)*3;
                    Assert.IsFalse(QuietCamp.Presentation.World.ShorelineGeometry.Contains(shore,outside));
                }
            }
            var winter=RoadmapSceneGenerator.Generate(CampContent.Summary("gen:qc_camp:14"),AtmosphereCatalog.Load());
            var tree=winter.Props.First(p=>p.Sway&&p.Asset.StartsWith("tree"));
            var top=winter.SurfaceColor(new Color(.2f,.4f,.2f),tree,Vector3.up);
            var sideColor=winter.SurfaceColor(new Color(.2f,.4f,.2f),tree,Vector3.right);
            Assert.Greater(top.grayscale,sideColor.grayscale+.2f);
        }
        [Test] public void StoryMiniaturesReuseTheSameBoundedGeometryWithoutPuzzleOrNativeMeshState()
        {
            var library=RoadmapModelLibrary.Load();var scene=RoadmapSceneGenerator.Generate(CampContent.Summary("gen:qc_camp:3"),AtmosphereCatalog.Load());
            var prop=scene.Props.Single(p=>p.Geometry!=null);var model=prop.Geometry;
            Assert.AreSame(model,library.Story(scene.Level.environment.storyMotifs));
            Assert.AreEqual(model.Positions.Length,model.Normals.Length);Assert.AreEqual(model.Positions.Length/3,model.Colors.Length);
            Assert.Less(model.Positions.Length,5000);Assert.IsTrue(model.Positions.All(p=>!float.IsNaN(p.x)&&!float.IsInfinity(p.y)));
            Assert.IsFalse(QuietCamp.Presentation.World.ShorelineGeometry.Contains(scene.Level.environment.shore,prop.Position,2));
        }
        [Test] public void ViewCullingContainsActualModelFacesAndTheirProjectedShadows()
        {
            var library=RoadmapModelLibrary.Load();
            foreach(var summary in CampContent.Summaries)
            {
                var scene=RoadmapSceneGenerator.Generate(summary,AtmosphereCatalog.Load());
                foreach(var prop in scene.Props)
                {
                    var model=prop.Geometry??library.Get(prop.Asset);if(model==null)continue;
                    var rotation=Quaternion.Euler(0,prop.Yaw,0);
                    foreach(var source in model.Positions.Where((_,index)=>index%17==0))
                    {
                        var p=rotation*new Vector3(source.x*prop.Stretch.x,source.y,source.z*prop.Stretch.y)*prop.Height+prop.Position;
                        Assert.LessOrEqual(Mathf.Abs((p.x+p.z)*.37f+p.y*.86f),scene.VerticalExtent+.001f,summary.id+" model");
                        float sx=p.x-p.y*scene.Sun.x/Mathf.Max(.18f,scene.Sun.y),sz=p.z-p.y*scene.Sun.z/Mathf.Max(.18f,scene.Sun.y);
                        Assert.LessOrEqual(Mathf.Abs((sx+sz)*.37f),scene.VerticalExtent+.001f,summary.id+" shadow");
                    }
                }
            }
        }
    }
}
