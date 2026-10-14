using System;
using System.Linq;
using Newtonsoft.Json;
using NUnit.Framework;
using QuietCamp.Domain;
using QuietCamp.Editor.LevelAuthoring;
using QuietCamp.Infrastructure;
using QuietCamp.Presentation.World;
using UnityEngine;

namespace QuietCamp.Tests
{
    public sealed class CampSurfaceUseTests
    {
        const string Draft="Assets/QuietCamp/Authoring/WorldStudies/AircraftSeasons/spring.draft.json";
        static CampSurfaceUseField Field()=>new CampSurfaceUseField(6,8,new CampSurfaceUseSettings());
        [Test] public void FreshSurfaceHasNoHistoricalTrafficOrTentPads()
        {var f=Field();for(int i=0;i<f.Count;i++)Assert.That(f.Sample(i),Is.EqualTo(Color.clear));Assert.That(f.Steps,Is.Zero);}
        [Test] public void TentPressureDoesNotInventWalkingOrExposeSoil()
        {
            var f=Field();f.Tent(new Rect(-1,-1,2,2),71);var centre=f.Sample(Vector2.zero);
            Assert.That(centre.g,Is.GreaterThan(.8f));Assert.That(centre.r,Is.Zero);Assert.That(centre.b,Is.Zero);Assert.That(f.Steps,Is.Zero);
            Assert.That(f.Sample(new Vector2(1.6f,0)),Is.EqualTo(Color.clear));
            f.ClearOccupancy();f.Advance(200,0,0);Assert.That(f.Sample(Vector2.zero).g,Is.Zero);
        }
        [Test] public void SharedRepeatedTrafficWidensAndExposesOnlyVisitedGround()
        {
            var f=Field();f.Step(Vector2.zero);float first=f.Sample(Vector2.zero).g;
            Assert.That(first,Is.GreaterThan(0));Assert.That(f.Sample(Vector2.zero).r,Is.Zero);
            for(int i=0;i<160;i++)f.Step(new Vector2(i%2==0?-.08f:.08f,0));
            Assert.That(f.Sample(Vector2.zero).r,Is.GreaterThan(.9f));
            Assert.That(f.Sample(new Vector2(.29f,0)).r,Is.GreaterThan(0));Assert.That(f.Sample(new Vector2(2,0)),Is.EqualTo(Color.clear));
        }
        [Test] public void WetMudRequiresBothMoistureAndRepeatedTraffic()
        {
            var f=Field();f.Advance(60,1,0);Assert.That(f.Moisture,Is.EqualTo(1));Assert.That(f.Sample(Vector2.zero).a,Is.Zero);
            f.Step(Vector2.zero);Assert.That(f.Sample(Vector2.zero).a,Is.Zero);
            for(int i=0;i<160;i++)f.Step(Vector2.zero);Assert.That(f.Sample(Vector2.zero).a,Is.GreaterThan(.9f));
            f.Advance(60,0,0);Assert.That(f.Sample(Vector2.zero).a,Is.Zero);Assert.That(f.Sample(Vector2.zero).r,Is.GreaterThan(.9f));
        }
        [Test] public void SnowfallCoversTracksWithoutErasingUnderlyingSoilHistory()
        {
            var f=Field();for(int i=0;i<160;i++)f.Step(Vector2.zero);float wear=f.Sample(Vector2.zero).r;
            Assert.That(f.Sample(Vector2.zero).b,Is.GreaterThan(.9f));f.Advance(240,0,1);
            Assert.That(f.Sample(Vector2.zero).b,Is.Zero);Assert.That(f.Sample(Vector2.zero).r,Is.EqualTo(wear));
        }
        [Test] public void SeparateScenesAndOutOfBoundsStepsCannotShareWear()
        {var a=Field();var b=Field();a.Step(Vector2.zero);a.Step(new Vector2(100,100));Assert.That(a.Steps,Is.EqualTo(1));Assert.That(b.Sample(Vector2.zero),Is.EqualTo(Color.clear));}
        [Test] public void EveryCampaignBoardStartsWithNoInventedStepsAndWetPresetsStartWet()
        {
            var ids=LevelLoader.MvpLevelIds();Assert.That(ids.Count,Is.EqualTo(100));
            foreach(var id in ids){var l=LevelLoader.Load(id);var field=new CampSurfaceUseField(l.width,l.height,new CampSurfaceUseSettings());Assert.That(field.Steps,Is.Zero,id);Assert.That(field.Sample(Vector2.zero),Is.EqualTo(Color.clear),id);}
            var wet=new CampSurfaceUseField(6,8,new CampSurfaceUseSettings(),.9f);Assert.That(wet.Moisture,Is.EqualTo(.9f));Assert.That(wet.Sample(Vector2.zero).a,Is.Zero);
        }
        [Test] public void InvalidBudgetsAndNonFiniteParametersAreRejected()
        {Assert.Throws<ArgumentException>(()=>new CampSurfaceUseSettings{stride=float.NaN}.Validate());Assert.Throws<ArgumentException>(()=>new CampSurfaceUseSettings{resolution=1024}.Validate());Assert.Throws<ArgumentException>(()=>new ObjectOvergrowthSettings{vegetation="anything"}.Validate());}
        [Test] public void MossPrefersMoistShadeContactAndRetentiveMaterials()
        {
            float shaded=0,dry=0,underside=0;
            for(int i=0;i<30;i++)
            {
                var p=new Vector3(i*.37f,.3f,4);
                shaded+=SurfaceWeathering.MossSuitability(p,Vector3.up,1,0,1,"stone","sheltered",419);
                dry+=SurfaceWeathering.MossSuitability(p,Vector3.up,1,1,0,"aluminium","sheltered",419);
                underside+=SurfaceWeathering.MossSuitability(p,Vector3.down,1,0,1,"stone","sheltered",419);
            }
            Assert.That(shaded,Is.GreaterThan(dry));Assert.That(underside,Is.LessThan(shaded));

        }
        [Test] public void PointsInsideCanopyBoundsStillReceiveShelterAndFarObjectsDoNot()
        {
            var crown=new Bounds(Vector3.up,Vector3.one*4);float sheltered=ObjectOvergrowthAuthoring.EstimateLight(Vector3.zero,Vector3.up,Vector3.up,new[]{crown});
            float open=ObjectOvergrowthAuthoring.EstimateLight(Vector3.zero,Vector3.up,Vector3.up,new[]{new Bounds(Vector3.up*30,Vector3.one)});
            Assert.That(sheltered,Is.LessThan(open));Assert.That(open,Is.EqualTo(1));
        }
        [Test] public void OptionalMossMetadataPreservesLegacyJsonAndSeasonalSourceGeometry()
        {
            var d=LevelAuthoringDocument.Open(Draft);var m=d.art.models.First(m=>m.shading=="solid");Assert.That(JsonConvert.SerializeObject(m),Does.Not.Contain("mossCoverage"));
            m.mossCoverage=Enumerable.Repeat(.8f,m.colors.Length).ToArray();string before=JsonConvert.SerializeObject(m);
            var summer=SeasonModelAppearance.Resolve(m,SeasonAtmosphereCatalog.Get("summer_day"));
            Assert.That(JsonConvert.SerializeObject(m),Is.EqualTo(before));Assert.That(summer.colors,Is.Not.EqualTo(m.colors));Assert.That(summer.vertices,Is.EqualTo(m.vertices));
            m.mossCoverage[0]=float.NaN;Assert.Throws<System.IO.InvalidDataException>(()=>d.art.Validate(d.level));
        }
        [Test] public void OvergrowthIsRepeatableAndPreservesPuzzleAndUnselectedObjects()
        {
            var d=LevelAuthoringDocument.Open(Draft);string puzzle=JsonConvert.SerializeObject(d.level),other=JsonConvert.SerializeObject(d.art.instances.First(i=>i.id=="crash/airframe/tail"));
            var settings=ObjectOvergrowthSettings.Defaults();settings.maxPlants=12;var ids=new[]{"crash/airframe/nose"};
            int count=ObjectOvergrowthAuthoring.Apply(d,ids,settings);Assert.That(count,Is.GreaterThan(0));
            Assert.That(d.art.models.First(m=>m.id==d.art.instances.First(i=>i.id==ids[0]).modelId).mossCoverage,Is.Not.Null);
            Assert.That(JsonConvert.SerializeObject(d.level),Is.EqualTo(puzzle));Assert.That(JsonConvert.SerializeObject(d.art.instances.First(i=>i.id=="crash/airframe/tail")),Is.EqualTo(other));
            string plants=JsonConvert.SerializeObject(d.art.instances.Where(i=>i.id.StartsWith("overgrowth/")));
            int repeated=ObjectOvergrowthAuthoring.Apply(d,ids,settings);Assert.That(repeated,Is.EqualTo(count));Assert.That(JsonConvert.SerializeObject(d.art.instances.Where(i=>i.id.StartsWith("overgrowth/"))),Is.EqualTo(plants));d.art.Validate(d.level);
        }
        [Test] public void AttachedGrowthMovesWithUniformOwnerAndSurvivesCapture()
        {
            var d=LevelAuthoringDocument.Open(Draft);d.art.instances=d.art.instances.Where(i=>i.id=="crash/airframe/nose").ToArray();
            var settings=ObjectOvergrowthSettings.Defaults();settings.density=1;settings.maxPlants=4;settings.coverage="base";
            Assert.That(ObjectOvergrowthAuthoring.Apply(d,new[]{"crash/airframe/nose"},settings),Is.GreaterThan(0));
            var before=d.art.instances.First(i=>i.id.StartsWith("overgrowth/"));
            using(var workspace=new LevelAuthoringWorkspace(d))
            {
                var owner=workspace.Scenery.GetComponentsInChildren<LevelAuthoringAsset>().First(m=>m.instanceId=="crash/airframe/nose");
                owner.transform.position+=Vector3.left*.5f;workspace.RefreshPlantBatches(new[]{owner.transform});
                var captured=workspace.Capture(false).art.instances.First(i=>i.id==before.id);Assert.That(captured.x,Is.EqualTo(before.x-.5f).Within(.001f));Assert.That(captured.y,Is.EqualTo(before.y).Within(.001f));
            }
        }
        [Test] public void PlacementRefreshCreatesNoStepsButActualResidentMotionDoes()
        {
            var d=LevelAuthoringDocument.Open(Draft);var go=new GameObject("surface test");var material=new Material(Resources.Load<Shader>("QuietCamp/Meadow"));
            try
            {
                var surface=go.AddComponent<CampGroundDetails>();surface.Configure(d.level,material,d.art);
                surface.Rebuild(Array.Empty<Placement>());Assert.That(surface.Residents,Is.Zero);surface.Advance(60,0,0);Assert.That(surface.Surface.Steps,Is.Zero);
                surface.Rebuild(d.level.witness);Assert.That(surface.Residents,Is.GreaterThan(0));Assert.That(surface.Surface.Steps,Is.Zero);
                surface.Advance(30,0,0);Assert.That(surface.Surface.Steps,Is.GreaterThan(10));int steps=surface.Surface.Steps;
                surface.Rebuild(d.level.witness);Assert.That(surface.Surface.Steps,Is.EqualTo(steps));surface.Advance(30,0,0,true);Assert.That(surface.Surface.Steps,Is.EqualTo(steps));
                surface.Rebuild(Array.Empty<Placement>());Assert.That(surface.Residents,Is.Zero);
            }
            finally{UnityEngine.Object.DestroyImmediate(go);UnityEngine.Object.DestroyImmediate(material);}
        }
    }
}
