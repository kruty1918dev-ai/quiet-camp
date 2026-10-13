using System;
using System.Collections;
using System.Linq;
using Newtonsoft.Json;
using NUnit.Framework;
using Kruty1918.Audio;
using QuietCamp.Editor.LevelAuthoring;
using QuietCamp.Infrastructure;
using QuietCamp.Presentation.World;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace QuietCamp.Tests
{
    public sealed class SeasonAtmosphereTests
    {
        [Test] public void WaterUsesDepthAndLeavesSourceMaterialUnchanged()
        {
            var source=Resources.Load<Material>("QuietCamp/Materials/CampWater");
            if(source==null)source=Resources.Load<Material>(WorldStudyFactory.Create("watermill").art.waterMaterial);
            Assert.NotNull(source);float fog=source.GetFloat("_FogSource");var color=source.GetColor("_BaseColor");
            var copy=new Material(source);
            try{RiverWeatherMaterial.Configure(copy);Assert.AreEqual(0,copy.GetFloat("_FogSource"));Assert.AreEqual(0,copy.GetFloat("_IntersectionSource"));Assert.AreEqual(fog,source.GetFloat("_FogSource"));Assert.AreEqual(color,source.GetColor("_BaseColor"));}
            finally{UnityEngine.Object.DestroyImmediate(copy);}
        }
        [Test] public void WeatherRigIsIdempotentAndKeepsBuildingGeometryAndPuzzle()
        {
            var d=WorldStudyFactory.Create("watermill");var puzzle=JsonConvert.SerializeObject(d.level);
            int vertices=d.art.instances.Sum(i=>d.art.models.Single(m=>m.id==i.modelId).vertices.Length);
            WeatherModelRigAuthoring.Prepare(d);int after=d.art.instances.Sum(i=>d.art.models.Single(m=>m.id==i.modelId).vertices.Length);
            Assert.AreEqual(vertices,after);Assert.AreEqual(puzzle,JsonConvert.SerializeObject(d.level));
            var once=JsonConvert.SerializeObject(d.art);WeatherModelRigAuthoring.Prepare(d);Assert.AreEqual(once,JsonConvert.SerializeObject(d.art));
            Assert.IsTrue(d.art.instances.Any(i=>i.motion=="waterwheel"));Assert.IsTrue(d.art.instances.Any(i=>i.motion=="boat"));
            var opened=JsonConvert.DeserializeObject<LevelAuthoringDocument>(JsonConvert.SerializeObject(d));opened.art.Validate(opened.level);
            var rotor=opened.art.instances.First(i=>i.motion=="waterwheel");rotor.motionPivot=new[]{float.NaN,0,0};Assert.Throws<System.IO.InvalidDataException>(()=>opened.art.Validate(opened.level));
        }
        [Test] public void CanvasClimateKeepsSnowAndAfterRainWetness()
        {
            var root=new GameObject("Climate canvas");
            try{var cloth=root.AddComponent<TentCloth>();cloth.ApplyClimate("winter",0,1);Assert.Greater(cloth.SnowCover,.8f);cloth.AdvanceSurface(10,0,0,.1f);Assert.Greater(cloth.SnowCover,.8f);cloth.ApplyClimate("spring",.8f,0);Assert.AreEqual(.8f,cloth.Wetness);cloth.AdvanceSurface(1,0,0,0);Assert.Greater(cloth.Wetness,.7f);}
            finally{UnityEngine.Object.DestroyImmediate(root);}
        }
        [Test] public void TwentyPresetsHaveFivePerSeasonAndResolvableSound()
        {
            SeasonAtmosphereCatalog.Reset();var all=SeasonAtmosphereCatalog.All;Assert.AreEqual(20,all.Length);
            foreach(var group in all.GroupBy(p=>p.season))Assert.AreEqual(5,group.Count());
            var audio=QuietCampAudioCatalog.Load();Assert.NotNull(audio);
            foreach(var p in all){p.Validate();Assert.IsNotEmpty(p.audioKeys);foreach(var key in p.audioKeys){Assert.IsTrue(audio.TryGet(key,out var sound),key);Assert.IsTrue(sound.Clip!=null||sound.Variants.Any(c=>c!=null),key);}}
        }
        [Test] public void WinterAndAutumnAreReversibleAndKeepConifersGreen()
        {
            var original=LevelAuthoringPalette.Get("qc_kenney_nature_tree_oak").model;
            var d=LevelAuthoringDocument.Create(false);d.art.models=new[]{original};var before=JsonConvert.SerializeObject(original);
            SeasonAtmosphereAuthoring.Apply(d,SeasonAtmosphereCatalog.Get("winter_clear"));var winter=SeasonModelAppearance.Resolve(d.art.models[0],d.art.atmosphere);
            Assert.AreEqual("deciduous",d.art.models[0].seasonalRole);Assert.Less(winter.colors.Length,original.colors.Length);
            Assert.AreEqual(before,JsonConvert.SerializeObject(original));
            var summer=SeasonModelAppearance.Resolve(d.art.models[0],SeasonAtmosphereCatalog.Get("summer_day"));Assert.AreEqual(original.colors.Length,summer.colors.Length);
            var conifer=LevelAuthoringDocument.Copy(LevelAuthoringPalette.Get("qc_kenney_nature_tree_pineTallA").model);conifer.seasonalRole="conifer";
            CollectionAssert.AreEqual(conifer.colors,SeasonModelAppearance.Resolve(conifer,SeasonAtmosphereCatalog.Get("autumn_golden")).colors);
            var weights=PlantWeatherResponse.Weights(d.art.models[0]);Assert.IsTrue(weights.Any(w=>w.x>0));Assert.IsTrue(weights.Any(w=>w.x==0));
        }
        [Test] public void VegetationPresetsRecognizeAnchorsKeepPuzzleAndReplaceOnlyTheirOwnGrowth()
        {
            var d=WorldStudyFactory.Create("village");var puzzle=JsonConvert.SerializeObject(d.level);var original=d.art.instances.Select(i=>i.id).ToArray();
            var p=VegetationAuthoring.Presets.Single(v=>v.id=="enrich");Assert.Greater(VegetationAuthoring.Apply(d,p,42),10);
            var once=JsonConvert.SerializeObject(d.art.instances);VegetationAuthoring.Apply(d,p,42);Assert.AreEqual(once,JsonConvert.SerializeObject(d.art.instances));
            Assert.AreEqual(puzzle,JsonConvert.SerializeObject(d.level));CollectionAssert.IsSubsetOf(original,d.art.instances.Select(i=>i.id).ToArray());
            foreach(var i in d.art.instances.Where(i=>(i.id??"").StartsWith("vegetation/")))Assert.IsTrue(Mathf.Abs(i.x)>=d.level.width*.5f+.65f||Mathf.Abs(i.z)>=d.level.height*.5f+.65f);
        }
        [Test] public void SunflowerDimensionsAndNormalsAreMetricAndFinite()
        {
            var m=SunflowerGeometry.Create();var bounds=AuthoringPropCatalog.ModelBounds(m);
            Assert.That(bounds.size.y,Is.InRange(1.8f,2.15f));Assert.That(bounds.size.x,Is.InRange(1.1f,1.5f));Assert.AreEqual("flower",m.seasonalRole);
            Assert.IsTrue(m.normals.All(n=>!float.IsNaN(n)&&!float.IsInfinity(n)));Assert.IsTrue(m.colors.Any(c=>((c>>16)&255)>220));
        }
        [Test] public void WeatherFollowsPanZoomAndRotatedCameraWithoutFixedOriginPatch()
        {
            var root=new GameObject("Weather coverage contract");var cameraObject=new GameObject("Weather camera");
            try
            {
                var camera=cameraObject.AddComponent<Camera>();camera.orthographic=true;camera.aspect=1.5f;camera.orthographicSize=8;camera.transform.rotation=Quaternion.Euler(52,135,0);camera.transform.position=-camera.transform.forward*18;
                var first=WeatherCoverage.Ground(camera,root.transform,4);camera.transform.position+=new Vector3(30,0,-20);
                var moved=WeatherCoverage.Ground(camera,root.transform,4);Assert.That(moved.center.x-first.center.x,Is.EqualTo(30).Within(.01f));Assert.That(moved.center.y-first.center.y,Is.EqualTo(-20).Within(.01f));
                camera.orthographicSize=14;var zoom=WeatherCoverage.Ground(camera,root.transform,4);Assert.Greater(zoom.width,moved.width);Assert.Greater(zoom.height,moved.height);
            }
            finally{UnityEngine.Object.DestroyImmediate(root);UnityEngine.Object.DestroyImmediate(cameraObject);}
        }
        [Test] public void WeatherSurfaceKeepsRainAboveRoofsAndBoatMotionPreservesAuthoredPose()
        {
            var doc=WorldStudyFactory.Create("watermill");AtmosphereDemoComposition.Prepare(doc);SeasonAtmosphereAuthoring.Apply(doc,SeasonAtmosphereCatalog.Get("summer_rain"));
            var field=new WeatherSurfaceField(doc.art);Assert.Greater(field.Height(-10,2.4f,-.045f),3,"Open mill still has roof segments where geometry exists.");
            var boat=doc.art.instances.Single(i=>i.motion=="boat");var node=new GameObject("Boat response");
            try
            {
                node.transform.localPosition=new Vector3(boat.x,boat.y,boat.z);var original=node.transform.localPosition;
                var motion=node.AddComponent<WeatherObjectMotion>();motion.Configure(boat);motion.Advance(2,.8f,false);Assert.AreNotEqual(original,node.transform.localPosition);
                motion.Advance(2,.8f,true);Assert.AreEqual(original,node.transform.localPosition);
                var rotor=doc.art.instances.Single(i=>i.motion=="waterwheel");Assert.NotNull(rotor.motionPivot);
                Assert.IsEmpty(doc.Validate(true));
            }
            finally{UnityEngine.Object.DestroyImmediate(node);}
        }
        [Test] public void VegetationUndoAndTerrainEditKeepOneAtmosphereAndExactObjects()
        {
            var d=WorldStudyFactory.Create("watermill");SeasonAtmosphereAuthoring.Apply(d,SeasonAtmosphereCatalog.Get("summer_day"));
            var state=ScriptableObject.CreateInstance<LevelAuthoringState>();state.Write(d);
            using(var workspace=new LevelAuthoringWorkspace(d))
            {
                int original=d.art.instances.Length;
                Undo.RecordObject(state,"Vegetation test");var edit=state.Read();VegetationAuthoring.Apply(edit,VegetationAuthoring.Presets.First(),51);state.Write(edit);Undo.FlushUndoRecordObjects();workspace.ApplyDocument(edit);
                Assert.Greater(workspace.Capture().art.instances.Length,original);
                Undo.PerformUndo();workspace.ApplyDocument(state.Read());Assert.AreEqual(original,workspace.Capture().art.instances.Length);
                Undo.PerformRedo();workspace.ApplyDocument(state.Read());Assert.AreEqual(edit.art.instances.Length,workspace.Capture().art.instances.Length);
                var terrain=workspace.Document.EditingCopy();terrain.art.streamBend+=.1f;workspace.ApplyDocument(terrain);
                Assert.AreEqual(1,workspace.Root.GetComponentsInChildren<SeasonAtmosphereView>().Length,"One owner after geometry rebuild.");
                var snapshot=workspace.Capture();var restored=JsonConvert.DeserializeObject<LevelAuthoringDocument>(JsonConvert.SerializeObject(snapshot));
                Assert.AreEqual(JsonConvert.SerializeObject(snapshot.art.instances),JsonConvert.SerializeObject(restored.art.instances));
            }
            Undo.ClearUndo(state);UnityEngine.Object.DestroyImmediate(state);
        }
        [Test] public void RegeneratedCampaignDraftsMatchPublishedArtAndRetainValidSolutions()
        {
            var folder="Assets/QuietCamp/Authoring/CampaignLevels";
            if(!System.IO.Directory.Exists(folder))Assert.Ignore("Run the staged campaign regeneration first.");
            Assert.AreEqual(100,System.IO.Directory.GetFiles(folder,"*.draft.json").Length);
            foreach(var card in LevelAuthoringCampaign.Cards)
            {
                var saved=LevelAuthoringDocument.Open(System.IO.Path.Combine(folder,card.id.Replace(':','_')+".draft.json"));
                var opened=LevelAuthoringCampaign.Open(card.id);
                Assert.IsEmpty(opened.Validate(true),card.id);Assert.AreEqual(saved.art.instances.Length,opened.art.instances.Length,card.id);
                Assert.AreEqual(JsonConvert.SerializeObject(saved.art.instances),JsonConvert.SerializeObject(opened.art.instances),card.id);
                Assert.AreEqual(saved.art.atmosphere.id,opened.art.atmosphere.id,card.id);
                Assert.IsTrue(QuietCamp.Domain.RuleEvaluator.Evaluate(opened.level,opened.level.witness).IsSolved,card.id);
            }
        }
        [UnityTest] public IEnumerator WorkspaceSeasonUndoAndAudioRestoreWithoutLeakingSources()
        {
            var d=WorldStudyFactory.Create("watermill");SeasonAtmosphereAuthoring.Apply(d,SeasonAtmosphereCatalog.Get("summer_rain"));
            var state=ScriptableObject.CreateInstance<LevelAuthoringState>();state.Write(d);
            using(var workspace=new LevelAuthoringWorkspace(d))
            {
                Assert.NotNull(workspace.Mood);workspace.Mood.Advance(.05f);Assert.Greater(workspace.Mood.LiveParticles,0);
                Undo.RecordObject(state,"Atmosphere test");var winter=state.Read();SeasonAtmosphereAuthoring.Apply(winter,SeasonAtmosphereCatalog.Get("winter_clear"));state.Write(winter);Undo.FlushUndoRecordObjects();workspace.ApplyDocument(winter);
                Assert.AreEqual("winter_clear",workspace.Mood.Preset.id);Undo.PerformUndo();workspace.ApplyDocument(state.Read());Assert.AreEqual("summer_rain",workspace.Mood.Preset.id);Undo.PerformRedo();workspace.ApplyDocument(state.Read());Assert.AreEqual("winter_clear",workspace.Mood.Preset.id);
                var catalog=QuietCampAudioCatalog.Load();var audio=new AudioService(catalog,catalog);audio.Initialize();
                try{workspace.Mood.BindAudio(audio);for(int i=0;i<30;i++){workspace.Mood.Advance(.05f);audio.Tick();yield return null;}Assert.Greater(workspace.Mood.OwnedVoices,0);workspace.Mood.StopAudio();Assert.AreEqual(0,workspace.Mood.OwnedVoices);}
                finally{audio.Dispose();}
            }
            Undo.ClearUndo(state);UnityEngine.Object.DestroyImmediate(state);
        }
    }
}
