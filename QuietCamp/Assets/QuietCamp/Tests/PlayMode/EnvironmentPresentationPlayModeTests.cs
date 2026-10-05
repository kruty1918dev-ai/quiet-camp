using System.Collections;
using System.Linq;
using NUnit.Framework;
using QuietCamp.Domain;
using QuietCamp.Infrastructure;
using QuietCamp.Presentation.World;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;

namespace QuietCamp.Tests
{
    public sealed class EnvironmentPresentationPlayModeTests
    {
        [UnityTest] public IEnumerator EntranceClosureFitsScaledFabricAndFacesForwardInsteadOfTheOffsetDoorCell()
        {
            foreach(var asset in new[]{"tent_smallOpen","tent_detailedOpen"})
            {
                var tent=Object.Instantiate(AssetCatalog.Load().Prefab(asset));
                try
                {
                    TentCloth.Apply(tent);
                    var fabric=tent.GetComponentsInChildren<MeshRenderer>().First(r=>r.sharedMaterials.Any(m=>m.shader.name=="QuietCamp/TentCloth"));
                    var material=fabric.sharedMaterials.First(m=>m.shader.name=="QuietCamp/TentCloth");
                    var max=(Vector3)material.GetVector("_MeshMin")+(Vector3)material.GetVector("_MeshSize");
                    float roofTop=fabric.transform.TransformPoint(max).y;
                    TentStoryVisual.Attach(tent,new LevelData{decorSeed=4},"anna");tent.GetComponent<TentStoryVisual>().Close(true);
                    var curtain=tent.transform.Find("Settling entrance cloth");Assert.NotNull(curtain);
                    Assert.That(Vector3.Angle(tent.transform.forward,curtain.forward),Is.LessThan(.01f),"The logical door's sideways offset must not rotate the physical entrance");
                    Assert.AreEqual(roofTop,curtain.GetComponent<MeshRenderer>().bounds.max.y,.015f,"A fixed-size panel leaves the larger scaled tent open");
                    Assert.Greater(curtain.GetComponent<MeshFilter>().sharedMesh.bounds.size.y,.9f);
                }
                finally{Object.Destroy(tent);}
                yield return null;
            }
        }

        [UnityTest] public IEnumerator NewCampaignScenicTreesHaveOneOwnerInsteadOfAnUnvalidatedLegacyRing()
        {
            var root=new GameObject("scenic ownership test");var cameraRoot=new GameObject("scenic camera");
            var camera=cameraRoot.AddComponent<Camera>();camera.enabled=false;CameraFitter.Configure(camera);camera.orthographicSize=7;
            var level=LevelLoader.Load("QC001");var catalog=AssetCatalog.Load();
            try
            {
                DecorSpawner.Spawn(level,catalog,root.transform);
                Assert.IsNull(root.transform.Find("ForestSurround"),"A second forest bypasses the composer's shadow exclusion");
                Assert.IsFalse(root.GetComponentsInChildren<MeshRenderer>().Any(r=>r.name.StartsWith("tree_")),"Near scenic tree instances still duplicate the clustered forest");
                var composed=new GameObject("ComposedEnvironment");composed.transform.SetParent(root.transform,false);
                var environment=composed.AddComponent<EnvironmentComposer>();environment.Configure(level,camera,root.transform,catalog);
                Assert.Greater(environment.TreeCount,0,"Removing the duplicate ring must retain the continuous forest");
                Assert.IsEmpty(composed.GetComponentsInChildren<Collider>());
            }
            finally{Object.Destroy(root);Object.Destroy(cameraRoot);}
            yield return null;
        }

        [UnityTest] public IEnumerator WinterWeatherUsesSlowSnowWithoutRainMarksAndReducedMotionClearsIt()
        {
            var root=new GameObject("winter weather test");
            var camera=root.AddComponent<Camera>();camera.enabled=false;
            var weather=root.AddComponent<CampWeather>();
            try
            {
                weather.Configure(camera,new LevelData{width=5,height=5,decorSeed=17,
                    environment=new EnvironmentCompositionData{seasonId="winter",weatherId="rain"}},false);
                for(int i=0;i<180;i++)weather.Advance(.05f,2,false,Vector2.right);
                Assert.AreEqual(0,weather.RainAmount,"Freezing weather must not run the rain-splash pipeline");
                Assert.Greater(weather.Snowfall,.1f);
                var snow=root.transform.Find("SlowSnow").GetComponent<ParticleSystem>();
                Assert.That(snow.particleCount,Is.InRange(1,36));
                var particles=new ParticleSystem.Particle[36];int count=snow.GetParticles(particles);
                for(int i=0;i<count;i++)
                {Assert.Less(particles[i].velocity.y,-.3f);Assert.Greater(particles[i].velocity.y,-.6f);}
                Assert.AreEqual(0,weather.ContactCount);
                weather.Advance(.05f,0,true,Vector2.right);Assert.AreEqual(0,snow.particleCount);
            }
            finally{Object.Destroy(root);}
            yield return null;
        }

        [UnityTest] public IEnumerator CameraImpulsesAreBoundedAndSuppressedRequestsNeverQueueForLater()
        {
            var root=new GameObject("camera impulse test");var camera=root.AddComponent<Camera>();camera.enabled=false;
            bool reduced=false,aiming=false;var origin=camera.transform.position;
            var motion=root.AddComponent<CameraAtmosphereMotion>();motion.Configure(camera,()=>default,()=>reduced,()=>aiming);
            reduced=true;motion.Impulse(1,Vector3.right);reduced=false;yield return null;
            Assert.AreEqual(origin,camera.transform.position,"A reduced-motion request must not be delayed until motion is enabled");
            aiming=true;motion.Impulse(1,Vector3.right);aiming=false;yield return null;
            Assert.AreEqual(origin,camera.transform.position,"A drag-suppressed impulse must not fire after release");
            motion.Impulse(1,Vector3.right);
            for(int i=0;i<15;i++){yield return null;Assert.LessOrEqual(motion.VisualOffset.magnitude,.0451f);}
            Assert.Greater(motion.VisualOffset.sqrMagnitude,.0000001f);
            Object.Destroy(root);yield return null;
        }
        [UnityTest] public IEnumerator CameraWindRemainsBoundedFreezesWhileAimingAndSettlesUnderReducedMotion()
        {
            var root=new GameObject("wind camera test");var camera=root.AddComponent<Camera>();camera.enabled=false;
            camera.transform.position=new Vector3(0,10,10);camera.transform.rotation=Quaternion.Euler(45,0,0);
            bool aiming=false,reduced=false;float time=0;
            var motion=root.AddComponent<CameraAtmosphereMotion>();
            motion.Configure(camera,()=>new WindSim.Snapshot(Vector2.right,.4f,.5f,.7f,time),()=>reduced,()=>aiming);
            for(int i=0;i<30;i++){time+=.1f;yield return null;Assert.LessOrEqual(motion.VisualOffset.magnitude,.0201f);}
            Assert.Greater(motion.VisualOffset.sqrMagnitude,.0000001f);
            aiming=true;var position=camera.transform.position;var rotation=camera.transform.rotation;
            for(int i=0;i<10;i++){time+=.2f;yield return null;Assert.AreEqual(position,camera.transform.position);Assert.AreEqual(rotation,camera.transform.rotation);}
            aiming=false;reduced=true;yield return new WaitForSecondsRealtime(2);
            Assert.Less(motion.VisualOffset.magnitude,.003f);
            Object.Destroy(root);yield return null;
        }
        [UnityTest] public IEnumerator LowProfileKeepsGradingAndSeasonalForestMeshesWithoutColliders()
        {
            int original=QualitySettings.GetQualityLevel();var root=new GameObject("composition test");var cameraObject=new GameObject("composition camera");var camera=cameraObject.AddComponent<Camera>();camera.enabled=false;
            CameraFitter.Configure(camera);camera.orthographicSize=7;
            var level=new LevelData{width=5,height=5,decorSeed=17,entry=new[]{0,0},blocked=new int[0][],environment=new EnvironmentCompositionData{biomeId="forest",seasonId="spring",weatherId="clear",treeDensity=.8f,clusterSeed=15,meadowSpecies=new[]{"daisy","poppy"},storyMotifs=new[]{"marker"}}};
            var composition=root.AddComponent<EnvironmentComposer>();composition.Configure(level,camera,root.transform,AssetCatalog.Load());
            Assert.Greater(composition.TreeCount,0);Assert.LessOrEqual(composition.VisibleClusterCount,48);Assert.IsEmpty(root.GetComponentsInChildren<Collider>());
            foreach(var filter in root.GetComponentsInChildren<MeshFilter>())
            {Assert.NotNull(filter.sharedMesh);Assert.IsTrue(filter.sharedMesh.vertices.All(p=>float.IsFinite(p.x)&&float.IsFinite(p.y)&&float.IsFinite(p.z)));}
            var post=root.AddComponent<PhasePostFx>();QualitySettings.SetQualityLevel(0,true);post.Configure(camera,0);post.Apply(AtmosphereCatalog.Load().Get("noon"),0);
            Assert.IsTrue(camera.allowHDR);Assert.IsTrue(camera.GetUniversalAdditionalCameraData().renderPostProcessing);
            Assert.IsTrue(post.RuntimeProfile.TryGet<Tonemapping>(out var tonemap)&&tonemap.active);Assert.IsTrue(post.RuntimeProfile.TryGet<ColorAdjustments>(out var color)&&color.active);
            Object.Destroy(root);Object.Destroy(cameraObject);QualitySettings.SetQualityLevel(original,true);yield return null;
        }
        [UnityTest] public IEnumerator TentBelongingsAndClosureArePurelyVisualAndReleaseOwnedMeshes()
        {
            var tent=Object.Instantiate(AssetCatalog.Load().Prefab("tent_smallOpen"));
            var level=new LevelData{decorSeed=4};var colliders=tent.GetComponentsInChildren<Collider>().Length;
            TentStoryVisual.Attach(tent,level,"anna");var story=tent.GetComponent<TentStoryVisual>();Assert.NotNull(story);
            Assert.AreEqual(colliders,tent.GetComponentsInChildren<Collider>().Length);
            var meshes=story.GetComponentsInChildren<MeshFilter>().Select(f=>f.sharedMesh).ToArray();
            story.Close(false);yield return new WaitForSecondsRealtime(.35f);Assert.That(story.Closure,Is.InRange(.1f,.99f));
            story.Close(true);Assert.AreEqual(1,story.Closure);Assert.IsTrue(tent.transform.Find("Settling entrance cloth").gameObject.activeSelf);
            Object.Destroy(tent);yield return null;
        }
        [UnityTest] public IEnumerator EarnedPennantKeepsSupportRigidAndCanBeDisabledWithoutChangingOccupancy()
        {
            var tent=Object.Instantiate(AssetCatalog.Load().Prefab("tent_smallOpen"));bool earned=false;
            int colliders=tent.GetComponentsInChildren<Collider>().Length;
            TentStoryVisual.Attach(tent,new LevelData{decorSeed=29,environment=new EnvironmentCompositionData{seasonId="spring"}},"anna",()=>earned);
            var story=tent.GetComponent<TentStoryVisual>();Assert.IsFalse(story.PennantVisible);
            var belongings=tent.transform.Find("Guest belongings");
            if(belongings==null)belongings=tent.transform.Find("VisualCenter/Guest belongings");
            Assert.NotNull(belongings);var belongingsMesh=belongings.GetComponent<MeshFilter>().sharedMesh;
            Assert.Greater(belongingsMesh.vertexCount,48);Assert.AreEqual(belongingsMesh.vertexCount,belongingsMesh.colors.Length);
            earned=true;yield return new WaitForSecondsRealtime(.6f);Assert.IsTrue(story.PennantVisible);
            var flag=tent.GetComponentsInChildren<MeshRenderer>().First(r=>r.name=="Wind carried fabric");
            Assert.AreEqual("QuietCamp/TentCloth",flag.sharedMaterial.shader.name);
            Assert.AreEqual("Universal Render Pipeline/Simple Lit",flag.transform.parent.GetComponent<MeshRenderer>().sharedMaterial.shader.name);
            Assert.AreEqual(colliders,tent.GetComponentsInChildren<Collider>().Length);
            earned=false;yield return new WaitForSecondsRealtime(.6f);Assert.IsFalse(story.PennantVisible);
            Object.Destroy(tent);yield return null;
        }
    }
}
