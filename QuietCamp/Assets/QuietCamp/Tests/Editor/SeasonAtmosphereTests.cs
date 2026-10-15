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
        [TestCase(0,false)]
        [TestCase(1,false)]
        [TestCase(2,false)]
        [TestCase(1,true)]
        public void SeasonalRainCreatesOneContactForEachActualGroundOrRoofHit(int tier,bool roof)
        {
            var root=new GameObject("Seasonal rain contact contract");var cameraObject=new GameObject("Rain camera");
            Mesh mesh=null;Material fabric=null;
            try
            {
                var camera=cameraObject.AddComponent<Camera>();CameraFitter.Configure(camera);camera.orthographicSize=8;
                var art=new CampArtDefinition{width=6,height=6,streamCentre=100,models=Array.Empty<CampArtDefinition.Model>(),instances=Array.Empty<CampArtDefinition.Instance>()};
                var preset=SeasonAtmosphereCatalog.Copy(SeasonAtmosphereCatalog.Get("summer_rain"));preset.wind=0;
                var view=new GameObject("Rain").AddComponent<SeasonAtmosphereView>();view.Configure(preset,art,camera,root.transform);
                float y=art.GroundHeight(0,0)+.2f;
                if(roof)
                {
                    var node=new GameObject("Sloped fabric",typeof(MeshFilter),typeof(MeshRenderer));node.transform.SetParent(root.transform,false);
                    mesh=new Mesh{vertices=new[]{new Vector3(-1,.6f,-1),new Vector3(1,1.6f,-1),new Vector3(-1,.6f,1),new Vector3(1,1.6f,1)},triangles=new[]{0,2,1,1,2,3}};
                    mesh.RecalculateNormals();mesh.RecalculateBounds();node.GetComponent<MeshFilter>().sharedMesh=mesh;
                    fabric=new Material(Shader.Find("QuietCamp/TentCloth"));fabric.SetVector("_MeshMin",mesh.bounds.min);fabric.SetVector("_MeshSize",mesh.bounds.size);
                    node.GetComponent<MeshRenderer>().sharedMaterial=fabric;RainSurface.Attach(node);y=1.3f;
                    // EditMode does not invoke the runtime Awake callback.
                    node.GetComponent<RainSurface>().RefreshGeometry();
                }
                view.Advance(0,false,tier);
                var rain=root.GetComponentsInChildren<ParticleSystem>().Single(p=>p.name=="Дощ");
                foreach(var system in root.GetComponentsInChildren<ParticleSystem>())system.Clear();
                rain.Play(false); // Initialize Unity's native particle playback in EditMode.
                rain.Emit(new ParticleSystem.EmitParams{position=new Vector3(0,y,0),startLifetime=2,randomSeed=42},1);
                rain.Pause(false);
                var before=new ParticleSystem.Particle[1200];int beforeCount=rain.GetParticles(before);
                Assert.AreEqual(1,beforeCount,"The test drop must exist before advancing.");
                view.Advance(.04f,false,tier);
                Assert.AreEqual(1,view.RainContactCount,"Only the visible particle crossing may emit a contact.");
                Assert.AreEqual(roof?1:0,view.FabricRainContacts);Assert.AreEqual(roof?0:1,view.GroundRainContacts);
                Assert.AreEqual(1,view.RainContacts.ActiveCount);Assert.LessOrEqual(view.RainContacts.ActiveCount,view.RainContacts.Budget);
                view.Advance(.05f,false,tier);Assert.AreEqual(1,view.RainContactCount,"The consumed drop cannot hit again.");
                view.Advance(0,true,tier);Assert.AreEqual(0,view.RainContacts.ActiveCount,"Reduced motion clears contact animation.");
            }
            finally{UnityEngine.Object.DestroyImmediate(root);UnityEngine.Object.DestroyImmediate(cameraObject);if(mesh!=null)UnityEngine.Object.DestroyImmediate(mesh);if(fabric!=null)UnityEngine.Object.DestroyImmediate(fabric);}
        }

        [Test] public void DryPresetDoesNotInventRainContactsOrSplashes()
        {
            var root=new GameObject("Dry weather contract");var cameraObject=new GameObject("Dry camera");
            try
            {
                var camera=cameraObject.AddComponent<Camera>();CameraFitter.Configure(camera);camera.orthographicSize=8;
                var art=new CampArtDefinition{width=6,height=6,streamCentre=100,models=Array.Empty<CampArtDefinition.Model>(),instances=Array.Empty<CampArtDefinition.Instance>()};
                var view=new GameObject("Dry").AddComponent<SeasonAtmosphereView>();view.Configure(SeasonAtmosphereCatalog.Get("summer_day"),art,camera,root.transform);
                for(int i=0;i<40;i++)view.Advance(.05f);
                Assert.AreEqual(0,view.RainContactCount);Assert.AreEqual(0,view.RainContacts.ActiveCount);
                Assert.AreEqual(0,root.GetComponentsInChildren<ParticleSystem>().Single(p=>p.name=="Бризки від влучання крапель").particleCount);
            }
            finally{UnityEngine.Object.DestroyImmediate(root);UnityEngine.Object.DestroyImmediate(cameraObject);}
        }

        [Test] public void CapturingDraftKeepsWheelAndBoatMotionAcrossRepeatedSaveAndOpen()
        {
            var document=WorldStudyFactory.Create("watermill");SeasonAtmosphereAuthoring.Apply(document,SeasonAtmosphereCatalog.Get("summer_day"));
            using(var workspace=new LevelAuthoringWorkspace(document))
            {
                foreach(var capture in new[]{workspace.Capture(false),workspace.Capture(false)})
                {
                    var opened=JsonConvert.DeserializeObject<LevelAuthoringDocument>(JsonConvert.SerializeObject(capture));
                    foreach(var moving in document.art.instances.Where(i=>i.motion!=null))
                    {
                        var item=opened.art.instances.Single(i=>i.id==moving.id);Assert.AreEqual(moving.motion,item.motion);CollectionAssert.AreEqual(moving.motionPivot,item.motionPivot);
                    }
                }
            }
        }
        [Test] public void SnowIsVisibleOnRoofForBothTriangleWindings()
        {
            var scene=UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();var root=new GameObject("Snow roof fixture");UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root,scene);
            var roof=new GameObject("Two-sided roof");roof.transform.SetParent(root.transform,false);roof.layer=30;
            var mesh=new Mesh{vertices=new[]{new Vector3(-1,0,-1),new Vector3(-1,0,1),new Vector3(1,0,1),new Vector3(1,0,-1)},normals=Enumerable.Repeat(Vector3.up,4).ToArray()};
            mesh.triangles=new[]{0,1,2,0,2,3};mesh.RecalculateBounds();roof.AddComponent<MeshFilter>().sharedMesh=mesh;
            var material=new Material(Resources.Load<Shader>("QuietCamp/TentCloth"));material.SetColor("_BaseColor",new Color(.75f,.1f,.04f));material.SetVector("_MeshMin",new Vector3(-1,0,-1));material.SetVector("_MeshSize",new Vector3(2,.1f,2));roof.AddComponent<MeshRenderer>().sharedMaterial=material;
            var cameraNode=new GameObject("Roof camera");cameraNode.transform.SetParent(root.transform,false);var camera=cameraNode.AddComponent<Camera>();camera.transform.position=new Vector3(0,3,-3);camera.transform.rotation=Quaternion.Euler(45,0,0);camera.orthographic=true;camera.orthographicSize=1.6f;camera.cullingMask=1<<30;camera.cameraType=CameraType.Game;camera.overrideSceneCullingMask=UnityEditor.SceneManagement.EditorSceneManager.GetSceneCullingMask(scene);camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;
            var sunNode=new GameObject("Roof light");sunNode.transform.SetParent(root.transform,false);var sun=sunNode.AddComponent<Light>();sun.type=LightType.Directional;sun.intensity=1;sun.cullingMask=1<<30;sun.transform.rotation=Quaternion.Euler(50,-25,0);
            var target=new RenderTexture(128,128,24,RenderTextureFormat.ARGB32);target.Create();var image=new Texture2D(128,128,TextureFormat.RGB24,false);
            var oldActive=RenderTexture.active;var oldSun=RenderSettings.sun;var oldAmbient=RenderSettings.ambientLight;var oldMode=RenderSettings.ambientMode;float wind=Shader.GetGlobalFloat("_AtmosWindStrength");bool async=ShaderUtil.allowAsyncCompilation;
            try
            {
                ShaderUtil.allowAsyncCompilation=false;Shader.SetGlobalFloat("_AtmosWindStrength",0);RenderSettings.sun=sun;RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=Color.white;camera.targetTexture=target;
                Color Sample(float snow)
                {
                    material.SetFloat("_SnowCover",snow);var request=new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest{destination=target};UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera,request);UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera,request);
                    RenderTexture.active=target;image.ReadPixels(new Rect(0,0,128,128),0,0);image.Apply();return image.GetPixel(64,64);
                }
                foreach(bool reverse in new[]{false,true})
                {
                    mesh.triangles=reverse?new[]{0,2,1,0,3,2}:new[]{0,1,2,0,2,3};var dry=Sample(0);var snowy=Sample(1);
                    Assert.Greater(snowy.g,snowy.r*.65f,"Snow is missing with reversed="+reverse);Assert.Greater(snowy.g,dry.g+.08f,"Roof surface did not change.");
                }
            }
            finally
            {
                RenderTexture.active=oldActive;RenderSettings.sun=oldSun;RenderSettings.ambientMode=oldMode;RenderSettings.ambientLight=oldAmbient;Shader.SetGlobalFloat("_AtmosWindStrength",wind);ShaderUtil.allowAsyncCompilation=async;
                camera.targetTexture=null;UnityEngine.Object.DestroyImmediate(image);target.Release();UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(material);UnityEngine.Object.DestroyImmediate(mesh);UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
            }
        }
        [Test] public void PartialGroundSnowLeavesBothExposedEarthAndCoveredPatches()
        {
            var scene=UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();var node=new GameObject("Snow ground fixture");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(node,scene);node.layer=30;
            var mesh=new Mesh{vertices=new[]{new Vector3(-8,0,-8),new Vector3(-8,0,8),new Vector3(8,0,8),new Vector3(8,0,-8)},triangles=new[]{0,1,2,0,2,3},normals=Enumerable.Repeat(Vector3.up,4).ToArray(),colors=Enumerable.Repeat(Color.white,4).ToArray()};
            node.AddComponent<MeshFilter>().sharedMesh=mesh;
            var material=new Material(Resources.Load<Shader>("QuietCamp/CampArtLit"));node.AddComponent<MeshRenderer>().sharedMaterial=material;
            material.SetFloat("_ArtGround",1);material.SetColor("_ArtAmbient",Color.white);material.SetColor("_GroundLow",new Color(.16f,.22f,.1f));material.SetColor("_GroundHigh",new Color(.3f,.36f,.18f));material.SetColor("_SoilTone",new Color(.3f,.22f,.12f));material.SetVector("_CampFieldSize",new Vector4(20,20,0,0));
            var cameraNode=new GameObject("Snow ground camera");cameraNode.transform.SetParent(node.transform,false);var camera=cameraNode.AddComponent<Camera>();camera.cameraType=CameraType.Game;camera.orthographic=true;camera.orthographicSize=8;camera.transform.position=new Vector3(0,20,0);camera.transform.rotation=Quaternion.Euler(90,0,0);
            camera.cullingMask=1<<30;camera.overrideSceneCullingMask=UnityEditor.SceneManagement.EditorSceneManager.GetSceneCullingMask(scene);camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;
            var target=RenderTexture.GetTemporary(128,128,24);var image=new Texture2D(128,128,TextureFormat.RGB24,false);
            var active=RenderTexture.active;bool async=ShaderUtil.allowAsyncCompilation;
            try
            {
                ShaderUtil.allowAsyncCompilation=false;
                Color[] Sample(float cover)
                {
                    material.SetVector("_CampClimate",new Vector4(0,cover,0,1));camera.targetTexture=target;
                    var request=new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest{destination=target};
                    UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera,request);UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera,request);
                    RenderTexture.active=target;image.ReadPixels(new Rect(0,0,128,128),0,0);image.Apply();return image.GetPixels();
                }
                var dry=Sample(0);var partial=Sample(.45f);var full=Sample(1);
                int earth=0,snow=0;
                for(int i=0;i<partial.Length;i++)
                {
                    float range=full[i].grayscale-dry[i].grayscale;
                    if(range<.08f)continue;
                    float coverage=(partial[i].grayscale-dry[i].grayscale)/range;
                    if(coverage<.15f)earth++;if(coverage>.85f)snow++;
                }
                Assert.Greater(earth,128,"Partial snow must leave visible substrate. Dry/partial/full means: "+dry.Average(c=>c.grayscale)+"/"+partial.Average(c=>c.grayscale)+"/"+full.Average(c=>c.grayscale));Assert.Greater(snow,128,"Partial snow must form covered patches, rather than uniformly greying the ground.");
                CollectionAssert.AreEqual(Enumerable.Repeat(Color.white,4).ToArray(),mesh.colors,"Snow must not accumulate in authored vertex colours.");
            }
            finally{RenderTexture.active=active;ShaderUtil.allowAsyncCompilation=async;camera.targetTexture=null;RenderTexture.ReleaseTemporary(target);UnityEngine.Object.DestroyImmediate(image);UnityEngine.Object.DestroyImmediate(material);UnityEngine.Object.DestroyImmediate(mesh);UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);}
        }
        [Test] public void WheelKeepsPivotWithNonuniformScaleAndAngledPose()
        {
            var root=new GameObject("Scaled wheel contract");
            try
            {
                root.transform.localPosition=new Vector3(4,2,-3);root.transform.localRotation=Quaternion.Euler(12,70,5);root.transform.localScale=new Vector3(1.1f,.8f,1.7f);
                var pivot=new Vector3(1.4f,.7f,.2f);var centre=root.transform.TransformPoint(pivot);var motion=root.AddComponent<WeatherObjectMotion>();
                motion.Configure(new CampArtDefinition.Instance{motion="waterwheel",motionPivot=new[]{pivot.x,pivot.y,pivot.z}});
                for(int i=1;i<200;i++){motion.Advance(i*.1f,.7f+.2f*Mathf.Sin(i*.1f),false);Assert.Less(Vector3.Distance(centre,root.transform.TransformPoint(pivot)),.00001f);}
                var doc=WorldStudyFactory.Create("watermill");var item=doc.art.instances.Single(i=>(doc.art.models.Single(m=>m.id==i.modelId).sourceModelId??doc.art.models.Single(m=>m.id==i.modelId).id)=="qc_watermill_ruin");
                item.rotationEuler=new[]{12f,70f,5f};item.scaleXYZ=new[]{1.1f,.8f,1.7f};WeatherModelRigAuthoring.Prepare(doc);
                var wheel=doc.art.instances.Single(i=>i.id==item.id+"/wheel");CollectionAssert.AreEqual(item.rotationEuler,wheel.rotationEuler);CollectionAssert.AreEqual(item.scaleXYZ,wheel.scaleXYZ);
            }
            finally{UnityEngine.Object.DestroyImmediate(root);}
        }
        [Test] public void WaterUsesDepthAndLeavesSourceMaterialUnchanged()
        {
            var source=Resources.Load<Material>("QuietCamp/Materials/CampWater");
            if(source==null)source=Resources.Load<Material>(WorldStudyFactory.Create("watermill").art.waterMaterial);
            Assert.NotNull(source);float fog=source.GetFloat("_FogSource");var color=source.GetColor("_BaseColor");
            var copy=new Material(source);
            try{RiverWeatherMaterial.Configure(copy);Assert.AreEqual(0,copy.GetFloat("_FogSource"));Assert.AreEqual(0,copy.GetFloat("_IntersectionSource"));Assert.AreEqual(fog,source.GetFloat("_FogSource"));Assert.AreEqual(color,source.GetColor("_BaseColor"));Assert.AreEqual(color,copy.GetColor("_BaseColor"),"Weather must preserve the authored river palette and opacity.");Assert.AreEqual(source.GetColor("_ShallowColor"),copy.GetColor("_ShallowColor"));Assert.AreEqual(source.GetFloat("_DepthVertical"),copy.GetFloat("_DepthVertical"));}
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
        [Test] public void MovingAndReplacingWatermillKeepsOneWheelAtItsOwnedAttachment()
        {
            var d=WorldStudyFactory.Create("watermill");WeatherModelRigAuthoring.Prepare(d);
            var body=d.art.instances.Single(i=>(d.art.models.Single(m=>m.id==i.modelId).sourceModelId??i.modelId)=="qc_watermill_ruin");
            body.x+=3;body.y+=.7f;body.z-=2;body.yaw=135;
            body.rotationEuler=new[]{7f,135f,-4f};body.scaleXYZ=new[]{1.2f,.9f,1.5f};
            WeatherModelRigAuthoring.Prepare(d);
            var wheel=d.art.instances.Single(i=>i.id==body.id+"/wheel");
            Assert.AreEqual(body.x,wheel.x);Assert.AreEqual(body.y,wheel.y);Assert.AreEqual(body.z,wheel.z);
            CollectionAssert.AreEqual(body.rotationEuler,wheel.rotationEuler);CollectionAssert.AreEqual(body.scaleXYZ,wheel.scaleXYZ);
            var complete=LevelAuthoringPalette.Get("qc_watermill_ruin").model;
            d.art.models=d.art.models.Where(m=>m.id!=complete.id).Concat(new[]{complete}).ToArray();body.modelId=complete.id;
            WeatherModelRigAuthoring.Prepare(d);
            Assert.AreEqual(1,d.art.instances.Count(i=>i.id==body.id+"/wheel"));
            Assert.AreEqual(complete.vertices.Length,d.art.models.Single(m=>m.id==body.modelId).vertices.Length+d.art.models.Single(m=>m.id==d.art.instances.Single(i=>i.id==body.id+"/wheel").modelId).vertices.Length);
            var once=JsonConvert.SerializeObject(d.art);WeatherModelRigAuthoring.Prepare(d);Assert.AreEqual(once,JsonConvert.SerializeObject(d.art));
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
