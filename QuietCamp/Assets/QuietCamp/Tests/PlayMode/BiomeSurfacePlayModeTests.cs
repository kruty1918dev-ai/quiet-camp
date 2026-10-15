using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System;
using System.IO;
using System.Reflection;
using Newtonsoft.Json;
using NUnit.Framework;
using QuietCamp.Domain;
using QuietCamp.Infrastructure;
using QuietCamp.Presentation.World;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace QuietCamp.Tests
{
    public sealed class BiomeSurfacePlayModeTests
    {
#if UNITY_EDITOR
        bool _async;
        [SetUp] public void CompileSynchronously(){_async=UnityEditor.ShaderUtil.allowAsyncCompilation;UnityEditor.ShaderUtil.allowAsyncCompilation=false;}
        [TearDown] public void RestoreCompilation()=>UnityEditor.ShaderUtil.allowAsyncCompilation=_async;
#endif
        [UnityTest] public IEnumerator WinterThawAndEvergreensHaveActualPhoneAndWideCaptures()
        {
#if UNITY_EDITOR
            if(UnityEngine.Application.isBatchMode || SystemInfo.graphicsDeviceType==GraphicsDeviceType.Null)
                Assert.Ignore("This visual scenario requires the rendered Editor GameView.");
            var helper=typeof(ScreenshotPlayModeTest);
            void Size(int w,int h)=>helper.GetMethod("SetGameViewSize",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{w,h});
            Size(1280,800);yield return SceneManager.LoadSceneAsync("Boot");yield return PrivacyBootTestSupport.EnterGame();
            var services=QuietCamp.Presentation.QuietCampBootstrap.ServicesRef;
            bool reduced=services.ReducedMotion;int quality=services.Settings.quality,tier=services.EffectiveQuality,profile=QualitySettings.GetQualityLevel();
            var adaptive=Object.FindAnyObjectByType<QuietCamp.Presentation.AdaptiveCampQuality>();bool enabled=adaptive!=null&&adaptive.enabled;
            if(adaptive!=null)adaptive.enabled=false;
            try
            {
                services.Tutorial.Skip();services.ReducedMotion=true;
                services.Settings.quality=3;services.EffectiveQuality=2;QualitySettings.SetQualityLevel(2,true);
                services.PendingLevelId="gen:qc_camp:11";yield return SceneManager.LoadSceneAsync("Camp");
                for(int frame=0;frame<25;frame++)yield return null;
                var host=CampSceneHost.Current;Assert.IsTrue(host.IsReady);host.Session.DebugApplyWitness();host.Session.Select(null);
                host.SetAtmospherePhase("morning");
                var details=Object.FindAnyObjectByType<CampGroundDetails>();Assert.NotNull(details);Assert.AreEqual(0,details.PrintCount);Assert.AreEqual(0,details.Residents);
                Assert.Greater(host.Session.Level.noise.Length,0,"The visual fixture must contain a real fire pit");
                var ground=details.GetComponentsInChildren<MeshRenderer>().First(r=>r.name.StartsWith("Cell_")).sharedMaterial;
                Assert.AreEqual(1,ground.GetFloat("_SnowSeason"));Assert.Greater(ground.GetFloat("_HeatMelt"),.9f);
                var fire=BoardMath.CellCenterWorld(host.Session.Level,new Cell(host.Session.Level.noise[0][0],host.Session.Level.noise[0][1]));
                var heat=ground.GetVector("_HeatSite0");Assert.AreEqual(fire.x,heat.x,.01f);Assert.AreEqual(fire.z,heat.y,.01f);
                foreach(var size in new[]{new Vector2Int(1280,800),new Vector2Int(720,1600)})
                {
                    Size(size.x,size.y);for(int frame=0;frame<15;frame++)yield return null;
                    Object.FindAnyObjectByType<VisibleForestFloor>().RefreshNow();
                    foreach(var path in new[]{"QuietCamp/Meadow","QuietCamp/FoliageLit"})
                    {
                        var shader=Resources.Load<Shader>(path);Assert.IsTrue(shader.isSupported,path);
                        Assert.IsEmpty(UnityEditor.ShaderUtil.GetShaderMessages(shader).Where(m=>m.severity.ToString()=="Error").ToArray(),path);
                    }
                    yield return (IEnumerator)helper.GetMethod("Shot",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,
                        new object[]{Path.Combine(Directory.GetCurrentDirectory(),"Screenshots/BiomeGround"),"winter-thaw-"+size.x+"x"+size.y});
                }
            }
            finally
            {
                services.ReducedMotion=reduced;services.Settings.quality=quality;services.EffectiveQuality=tier;QualitySettings.SetQualityLevel(profile,true);
                if(adaptive!=null)adaptive.enabled=enabled;
            }
#else
            Assert.Ignore("Editor capture helper required.");yield return null;
#endif
        }
        [UnityTest] public IEnumerator TentsDoNotSpawnPeopleOrFootstepsInAnySeasonAndReleaseOwnedMeshes()
        {
            foreach(var season in new[]{"spring","summer","autumn","winter"})
            {
                var level=LevelLoader.Load("QC005");level.environment.seasonId=season;
                string before=JsonConvert.SerializeObject(level);
                var root=new GameObject("ground details "+season);
                var material=new Material(Resources.Load<Shader>("QuietCamp/Meadow"));
                Mesh[] meshes=null;
                try
                {
                    var details=root.AddComponent<CampGroundDetails>();details.Configure(level,material);
                    details.Rebuild(level.witness);details.Advance(120,.7f,.5f);
                    Assert.AreEqual(0,details.Residents);Assert.AreEqual(0,details.PrintCount);Assert.AreEqual(0,details.Surface.Steps);
                    Assert.IsEmpty(details.PrintCentres);
                    Assert.IsEmpty(root.GetComponentsInChildren<Collider>());
                    meshes=root.GetComponentsInChildren<MeshFilter>().Select(f=>f.sharedMesh).ToArray();
                    int count=details.PrintCount;details.Rebuild(level.witness);Assert.AreEqual(count,details.PrintCount);
                    CollectionAssert.AreEqual(meshes,root.GetComponentsInChildren<MeshFilter>().Select(f=>f.sharedMesh).ToArray());
                    details.AdvanceHeat(3,0,1);Assert.Greater(details.MeltAmount,.97f,"A quenched fire must not instantly undo its thaw");
                    details.AdvanceHeat(200,0,1);Assert.AreEqual(0,details.MeltAmount);
                    details.AdvanceHeat(12,1,1);Assert.AreEqual(1,details.MeltAmount);
                    Assert.AreEqual(before,JsonConvert.SerializeObject(level));
                }
                finally{Object.Destroy(root);Object.Destroy(material);}
                yield return null;
                Assert.IsTrue(meshes.All(mesh=>mesh==null));
            }
        }
    }
}
