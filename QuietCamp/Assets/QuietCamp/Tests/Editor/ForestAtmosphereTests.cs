using System;
using System.Linq;
using Newtonsoft.Json;
using NUnit.Framework;
using QuietCamp.Editor.LevelAuthoring;
using QuietCamp.Infrastructure;
using QuietCamp.Presentation.World;
using UnityEngine;

namespace QuietCamp.Tests
{
    public sealed class ForestAtmosphereTests
    {
        const string Draft="Assets/QuietCamp/Authoring/WorldStudies/AircraftSeasons/spring.draft.json";
        [Test] public void NewOptionalWeatherChannelsDoNotRewriteLegacyPresetSnapshots()
        {
            var p=new SeasonAtmospherePreset{id="legacy",label="legacy",season="spring"};
            var text=JsonConvert.SerializeObject(p);
            foreach(var name in new[]{"pollen","dust","leafLoss","airLight"})Assert.That(text,Does.Not.Contain("\""+name+"\""));
            Assert.That(JsonConvert.DeserializeObject<SeasonAtmospherePreset>(text).airLight,Is.EqualTo(.25f));
            p.pollen=.2f;Assert.That(JsonConvert.SerializeObject(p),Does.Contain("\"pollen\""));
        }
        [Test] public void AutumnDropsWholeLeafPiecesAndRemapsAllVertexChannels()
        {
            var v=new System.Collections.Generic.List<float>();
            for(int cloud=0;cloud<4;cloud++)
            {
                var a=new Vector3(cloud*4,2,0);var b=a+Vector3.right;var c=a+Vector3.up;var d=a+Vector3.forward;
                foreach(var point in new[]{a,b,c,a,c,d,a,d,b,b,d,c})v.AddRange(new[]{point.x,point.y,point.z});
            }
            var source=new CampArtDefinition.Model{id="clumps",seasonalRole="deciduous",shading="solid",vertices=v.ToArray(),normals=Enumerable.Repeat(new[]{0f,1,0},48).SelectMany(x=>x).ToArray(),colors=Enumerable.Repeat(0x559944,48).ToArray(),seasonalLeafTriangles=Enumerable.Range(0,16).ToArray(),weatherExposure=Enumerable.Repeat(.6f,48).ToArray(),uv=new float[96]};
            var before=JsonConvert.SerializeObject(source);var p=SeasonAtmosphereCatalog.Copy(SeasonAtmosphereCatalog.Get("autumn_golden"));p.leafLoss=.5f;
            var result=SeasonModelAppearance.Resolve(source,p);
            Assert.That(result.colors.Length,Is.EqualTo(24));Assert.That(result.seasonalLeafTriangles,Is.EqualTo(Enumerable.Range(0,8)));
            Assert.That(result.uv.Length,Is.EqualTo(48));Assert.That(result.weatherExposure.Length,Is.EqualTo(24));
            foreach(var cloud in Enumerable.Range(0,8).GroupBy(t=>Mathf.FloorToInt(result.vertices[t*9]/4)))Assert.That(cloud.Count(),Is.EqualTo(4));
            Assert.That(JsonConvert.SerializeObject(source),Is.EqualTo(before));
        }
        [Test] public void RegrowthAvoidsFrozenWaterInsteadOfPlantingAcrossTheRiver()
        {
            var d=LevelAuthoringDocument.Open(Draft);
            var water=new LandscapeMesh{name="test water",water=true,vertices=new[]{8f,0,8,12,0,8,12,0,12,8,0,12},triangles=new[]{0,2,1,0,3,2},normals=new[]{0f,1,0,0,1,0,0,1,0,0,1,0},uv=new float[8],colors=Enumerable.Repeat(1f,16).ToArray()};
            d.art.landscape.meshes=d.art.landscape.meshes.Concat(new[]{water}).ToArray();
            Assert.That(ForestRegrowthAuthoring.Apply(d,new ForestRegrowthSettings{attempts=1000,extent=12,density=1}),Is.GreaterThan(50));
            Assert.That(d.art.instances.Where(i=>i.id.StartsWith("regrowth/")).Any(i=>i.x>=8&&i.x<=12&&i.z>=8&&i.z<=12),Is.False);
        }
        [Test] public void SeasonalGrowthChangesGeometryWithoutMutatingTheAuthoredPlant()
        {
            var model=new CampArtDefinition.Model{id="test",shading="foliage",seasonalRole="flower",vertices=new[]{0f,0,0,.1f,1,0,0,1,.1f},normals=new[]{0f,1,0,0,1,0,0,1,0},colors=new[]{0xffffff,0xffffff,0xffffff},seasonalGrowth=new[]{1f,.3f,.1f,0f}};
            var before=JsonConvert.SerializeObject(model);
            var summer=SeasonModelAppearance.Resolve(model,SeasonAtmosphereCatalog.Get("summer_day"));
            Assert.That(summer.vertices[4],Is.EqualTo(.3f).Within(.0001f));
            Assert.That(SeasonModelAppearance.Resolve(model,SeasonAtmosphereCatalog.Get("spring_sunny")).vertices,Is.EqualTo(model.vertices));
            Assert.That(SeasonModelAppearance.Resolve(model,SeasonAtmosphereCatalog.Get("winter_snowfall")).vertices,Is.Empty);
            Assert.That(JsonConvert.SerializeObject(model),Is.EqualTo(before));
            model.seasonalGrowth=null;Assert.That(JsonConvert.SerializeObject(model),Does.Not.Contain("seasonalGrowth"));
        }
        [Test] public void RegrowthIsRepeatableKeepsThePuzzleAndLeavesWalkableUseZones()
        {
            var d=LevelAuthoringDocument.Open(Draft);var puzzle=JsonConvert.SerializeObject(d.level);var originals=d.art.instances.Where(i=>!(i.id??"").StartsWith("regrowth/")).ToArray();
            var settings=new ForestRegrowthSettings{attempts=700,extent=12,density=1,seed=42};
            int count=ForestRegrowthAuthoring.Apply(d,settings);Assert.That(count,Is.GreaterThan(50));
            Assert.That(JsonConvert.SerializeObject(d.level),Is.EqualTo(puzzle));
            foreach(var i in d.art.instances.Where(i=>i.id.StartsWith("regrowth/")))
            {
                // Low compressible cover may occupy the field; solid props still cannot.
                if(Mathf.Abs(i.x)<d.level.width*.5f+.2f&&Mathf.Abs(i.z)<d.level.height*.5f+.2f)Assert.That(AuthoringPropCatalog.ModelBounds(d.art.models.First(m=>m.id==i.modelId)).size.y*i.scale,Is.LessThanOrEqualTo(.24f));
                Assert.That(i.surfaceAttached,Is.True);
            }
            foreach(var original in originals)
            {
                var saved=d.art.instances.FirstOrDefault(i=>i.id==original.id);
                if(saved!=null)Assert.That(JsonConvert.SerializeObject(saved),Is.EqualTo(JsonConvert.SerializeObject(original)));
                else
                {
                    var land=d.art.landscape;Assert.That(original.surfaceAttached,Is.True);
                    Assert.That(original.x<land.originX+.3f||original.z<land.originZ+.3f||original.x>land.originX+land.size-.3f||original.z>land.originZ+land.size-.3f,Is.True);
                }
            }
            var plants=JsonConvert.SerializeObject(d.art.instances.Where(i=>i.id.StartsWith("regrowth/")));
            Assert.That(ForestRegrowthAuthoring.Apply(d,settings),Is.EqualTo(count));
            Assert.That(JsonConvert.SerializeObject(d.art.instances.Where(i=>i.id.StartsWith("regrowth/"))),Is.EqualTo(plants));
        }
        [Test] public void FieldCoverConformsToUnevenGroundAcrossItsWholeFootprint()
        {
            var d=LevelAuthoringDocument.Open(Draft);var land=d.art.landscape;
            for(int z=0;z<land.resolution;z++)for(int x=0;x<land.resolution;x++)
            {
                float px=land.originX+x*land.size/(land.resolution-1),pz=land.originZ+z*land.size/(land.resolution-1);
                land.heights[z*land.resolution+x]=.45f*Mathf.Sin(px*2.1f)*Mathf.Cos(pz*1.8f);
            }
            ForestRegrowthAuthoring.Apply(d,new ForestRegrowthSettings{attempts=3000,extent=8,density=1,seed=4637});
            var cover=d.art.instances.Where(i=>i.id.StartsWith("regrowth/")&&Mathf.Abs(i.x)<d.level.width*.5f+.2f&&Mathf.Abs(i.z)<d.level.height*.5f+.2f).ToArray();
            Assert.That(cover.Length,Is.GreaterThan(30));
            foreach(var plant in cover)Assert.IsTrue(d.art.IsCompressibleCover(plant,d.art.models.Single(m=>m.id==plant.modelId)),plant.id);
        }
        [Test] public void InvalidGrowthAndNonFiniteRegrowthSettingsAreRejected()
        {
            var d=LevelAuthoringDocument.Open(Draft);d.art.models[0].seasonalGrowth=new[]{1f,float.NaN,0f,0f};
            Assert.Throws<System.IO.InvalidDataException>(()=>d.art.Validate(d.level));
            Assert.Throws<ArgumentException>(()=>new ForestRegrowthSettings{spacing=float.NaN}.Validate());
            Assert.Throws<ArgumentException>(()=>new ForestRegrowthSettings{attempts=50001}.Validate());
        }
        [Test] public void SunnyAndWetPresetsCoordinateShadowsAirAndSurfaceState()
        {
            SeasonAtmosphereCatalog.Reset();foreach(var p in SeasonAtmosphereCatalog.All)p.Validate();
            foreach(var p in SeasonAtmosphereCatalog.All.Where(p=>p.rain>.1f))
            {Assert.That(p.rays,Is.Zero);Assert.That(p.shadows,Is.LessThanOrEqualTo(.4f));Assert.That(p.wetness,Is.GreaterThan(0));}
            foreach(var id in new[]{"spring_sunny","summer_day"})
            {var p=SeasonAtmosphereCatalog.Get(id);Assert.That(p.fog,Is.GreaterThan(0));Assert.That(p.rays,Is.GreaterThan(.5f));Assert.That(p.shadows,Is.GreaterThan(.8f));}
        }
        [Test] public void ShaftQualityChangesWorkWithoutChangingSunDirection()
        {
            var d=LevelAuthoringDocument.Open(Draft);var root=new GameObject("test shafts");var cameraNode=new GameObject("test camera");var camera=cameraNode.AddComponent<Camera>();camera.transform.rotation=Quaternion.Euler(52,225,0);
            var preset=SeasonAtmosphereCatalog.Get("summer_day");
            using(var rays=new SeasonLightShafts(root.transform,camera,d.art,preset,Enumerable.Range(0,9).Select(i=>new Vector3(-8-i,7,8)).ToArray()))
            {
                try
                {
                    rays.Advance(1,Vector2.right,0,false);Assert.That(rays.VisibleCount,Is.EqualTo(3));
                    rays.Advance(1,Vector2.right,2,false);Assert.That(rays.VisibleCount,Is.EqualTo(9));
                    var direction=Quaternion.Euler(CampArtDefinition.Vector(preset.sunEuler))*Vector3.forward;Assert.That(Vector3.Dot(rays.Direction,direction),Is.GreaterThan(.999f));
                }
                finally{UnityEngine.Object.DestroyImmediate(cameraNode);}
            }
            UnityEngine.Object.DestroyImmediate(root);
        }
        [Test] public void FireEffectsOnlyActivateWhenTheExistingGameplayFireIsBurning()
        {
            var d=LevelAuthoringDocument.Open(Draft);var root=new GameObject("test weather");var cameraNode=new GameObject("test camera");var camera=cameraNode.AddComponent<Camera>();camera.orthographic=true;camera.transform.SetPositionAndRotation(new Vector3(10,15,10),Quaternion.Euler(52,225,0));
            var node=new GameObject("weather");var weather=node.AddComponent<SeasonAtmosphereView>();
            try
            {
                weather.Configure(SeasonAtmosphereCatalog.Get("summer_day"),d.art,camera,root.transform);
                Assert.That(weather.ActiveFire,Is.False);weather.SetFire(Vector3.zero,true);weather.Advance(.05f);
                Assert.That(weather.ActiveFire,Is.True);Assert.That(weather.GetComponentsInChildren<Light>().Single().enabled,Is.True);
                weather.SetFire(Vector3.zero,false);Assert.That(weather.GetComponentsInChildren<Light>(),Is.Empty);
            }
            finally{UnityEngine.Object.DestroyImmediate(root);UnityEngine.Object.DestroyImmediate(cameraNode);}
        }
        [Test] public void QualityChangesReuseMeshesKeepTheAuthoredCompositionAndPreserveCorePlants()
        {
            var d=LevelAuthoringDocument.Open(Draft);ForestRegrowthAuthoring.Apply(d,new ForestRegrowthSettings{attempts=600,extent=12,density=1});
            var before=JsonConvert.SerializeObject(d.art);var root=new GameObject("quality root");var cameraNode=new GameObject("quality camera");cameraNode.AddComponent<Camera>();var view=cameraNode.AddComponent<CampArtView>();
            try
            {
                view.Configure(d.art,d.level,root.transform,null);
                var renderers=root.GetComponentsInChildren<MeshRenderer>().Where(r=>r.name.StartsWith("ForestRegrowth")).ToArray();var meshes=renderers.Select(r=>r.GetComponent<MeshFilter>().sharedMesh).ToArray();
                Assert.That(renderers.Length,Is.EqualTo(3));view.SetQualityTier(0);Assert.That(renderers.Count(r=>r.enabled),Is.EqualTo(1));
                view.SetQualityTier(2);Assert.That(renderers.All(r=>r.enabled),Is.True);
                Assert.That(renderers.Select(r=>r.GetComponent<MeshFilter>().sharedMesh).ToArray(),Is.EqualTo(meshes));
                Assert.That(JsonConvert.SerializeObject(d.art),Is.EqualTo(before));
            }
            finally{UnityEngine.Object.DestroyImmediate(cameraNode);UnityEngine.Object.DestroyImmediate(root);}
        }
        [Test] public void DryingPanelsMoveAroundFixedAttachmentsWhileSupportsRemainRigid()
        {
            var d=LevelAuthoringDocument.Open(Draft);WeatherModelRigAuthoring.Prepare(d);
            var item=d.art.instances.Single(i=>i.id=="crash/drying/cloth0");Assert.That(d.art.instances.Count(i=>i.motion=="cloth"),Is.EqualTo(2));
            Assert.That(d.art.instances.Single(i=>i.id=="crash/drying").motion,Is.Null);
            var node=new GameObject("pinned cloth");node.transform.localPosition=new Vector3(item.x,item.y,item.z);node.transform.localScale=Vector3.one*item.scale;node.transform.localRotation=Quaternion.Euler(0,item.yaw,0);
            try
            {
                var motion=node.AddComponent<WeatherObjectMotion>();motion.Configure(item);var pivot=CampArtDefinition.Vector(item.motionPivot);var before=node.transform.TransformPoint(pivot);var rotation=node.transform.localRotation;
                motion.Advance(2,.8f,false,Vector2.up);Assert.That(Quaternion.Angle(rotation,node.transform.localRotation),Is.GreaterThan(.1f));
                Assert.That(Vector3.Distance(before,node.transform.TransformPoint(pivot)),Is.LessThan(.0001f));
                motion.Advance(4,.8f,true,Vector2.up);Assert.That(Quaternion.Angle(rotation,node.transform.localRotation),Is.LessThan(.0001f));
                int count=d.art.instances.Length;WeatherModelRigAuthoring.Prepare(d);Assert.That(d.art.instances.Length,Is.EqualTo(count));
            }
            finally{UnityEngine.Object.DestroyImmediate(node);}
        }
    }
}
