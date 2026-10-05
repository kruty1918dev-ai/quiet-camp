using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.IO;
using NUnit.Framework;
using QuietCamp.Domain;
using QuietCamp.Infrastructure;
using QuietCamp.Presentation.UI;
using QuietCamp.Presentation.World;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace QuietCamp.Tests
{
    public sealed class SeasonPresentationPlayModeTests
    {
#if UNITY_EDITOR
        bool _async;
        [SetUp] public void CompileShadersSynchronously(){_async=UnityEditor.ShaderUtil.allowAsyncCompilation;UnityEditor.ShaderUtil.allowAsyncCompilation=false;}
        [TearDown] public void RestoreCompilation()=>UnityEditor.ShaderUtil.allowAsyncCompilation=_async;
#endif
        static bool Green(Color c)=>c.g>c.r*1.03f&&c.g>c.b*1.03f;
        static LevelData Level(string season,int order=18)=>new LevelData{width=6,height=5,order=order,ruleVersion=2,
            entry=new[]{0,0},decorSeed=41,blocked=Array.Empty<int[]>(),noise=Array.Empty<int[]>(),
            environment=new EnvironmentCompositionData{seasonId=season,biomeId="meadow",treeDensity=.75f,clusterSeed=41,meadowSpecies=new[]{"daisy","poppy"}}};
        [UnityTest] public IEnumerator AuthoredDeciduousTreesKeepCollidersAndPinnedRootsButLoseWinterCrowns()
        {
            var root=Object.Instantiate(AssetCatalog.Load().Prefab("tree_default"));
            try
            {
                var source=SeasonalTreeGeometry.Read(root);int colliders=root.GetComponentsInChildren<Collider>().Length;
                Kruty1918.Atmos.FoliageSway.Shared.ApplySlots(root,new[]{Kruty1918.Atmos.FoliageSway.Species.Trunk,Kruty1918.Atmos.FoliageSway.Species.Canopy});
                SeasonalTreeVisual.Apply(root,Level("winter",23),"tree_default");
                var owner=root.GetComponent<SeasonalTreeVisual>();Assert.NotNull(owner);
                SeasonalTreeVisual.Apply(root,Level("winter",23),"tree_default");Assert.AreEqual(1,root.GetComponents<SeasonalTreeVisual>().Length);
                Assert.AreEqual(colliders,root.GetComponentsInChildren<Collider>().Length);
                var renderer=root.GetComponentsInChildren<MeshRenderer>().Single(r=>r.enabled);
                var mesh=renderer.GetComponent<MeshFilter>().sharedMesh;
                Assert.IsFalse(mesh.colors.Any(Green));Assert.Greater(mesh.vertexCount,30);
                Assert.AreEqual(ShadowCastingMode.On,renderer.shadowCastingMode);Assert.IsTrue(renderer.receiveShadows);
                Assert.GreaterOrEqual(renderer.sharedMaterial.FindPass("DepthOnly"),0);
                var anchors=new System.Collections.Generic.List<Vector4>();mesh.GetUVs(1,anchors);
                Assert.IsTrue(anchors.All(a=>Mathf.Abs(a.z-source.Bounds.min.y)<.001f&&Mathf.Abs(a.w-source.Height)<.001f));
            }
            finally{Object.Destroy(root);}yield return null;
        }
        [UnityTest] public IEnumerator WinterFloorHasDriftsAndNoGreenAndRebuildsWhenSeasonChanges()
        {
            var root=new GameObject("season floor test");var cameraRoot=new GameObject("season camera");
            var camera=cameraRoot.AddComponent<Camera>();camera.enabled=false;CameraFitter.Configure(camera);camera.orthographicSize=12;camera.aspect=1.6f;
            var winter=Level("winter",23);var floor=root.AddComponent<VisibleForestFloor>();
            try
            {
                floor.Configure(winter,camera);Assert.Greater(floor.VisibleTileCount,0);Assert.LessOrEqual(floor.VisibleTileCount,VisibleForestFloor.MaximumTiles);
                Assert.AreEqual(0,floor.VisibleRichPlantCount);
                var meshes=root.GetComponentsInChildren<MeshFilter>().Select(f=>f.sharedMesh).ToArray();Assert.Greater(meshes.Max(m=>m.bounds.max.y),1);
                Assert.IsFalse(meshes.SelectMany(m=>m.colors).Any(Green));
                foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>())
                {Assert.IsTrue(renderer.receiveShadows);Assert.AreEqual(ShadowCastingMode.Off,renderer.shadowCastingMode);}
                var autumn=Level("autumn",20);floor.Configure(autumn,camera);
                var colors=root.GetComponentsInChildren<MeshFilter>().SelectMany(m=>m.sharedMesh.colors).ToArray();
                Assert.IsTrue(colors.Any(c=>c.r>c.g*1.2f),"Fallen russet leaves were not rebuilt over the previous snow tiles");
                Assert.LessOrEqual(floor.VisibleTriangleCount,floor.VisibleTileCount*6000);
                var tufts=new GameObject("winter trail accents");tufts.transform.SetParent(root.transform);
                var accents=tufts.AddComponent<CozyUnderstory>();accents.Build(winter);Assert.AreEqual(0,accents.ActivePlantCount);
            }
            finally{Object.Destroy(root);Object.Destroy(cameraRoot);}yield return null;
        }
        [UnityTest] public IEnumerator WinterPropsKeepGeometryAndReleaseOnlyTheirOwnSnowMaterials()
        {
            foreach(var id in new[]{"stone_largeA","stump_round","log"})
            {
                var root=Object.Instantiate(AssetCatalog.Load().Prefab(id));
                var filters=root.GetComponentsInChildren<MeshFilter>();var geometry=filters.Select(f=>f.sharedMesh).ToArray();
                var originals=root.GetComponentsInChildren<MeshRenderer>().SelectMany(r=>r.sharedMaterials).ToArray();
                int colliders=root.GetComponentsInChildren<Collider>().Length;
                SeasonalPropVisual.Apply(root,Level("winter",23),id);SeasonalPropVisual.Apply(root,Level("winter",23),id);
                Assert.AreEqual(1,root.GetComponents<SeasonalPropVisual>().Length);
                Assert.AreEqual(colliders,root.GetComponentsInChildren<Collider>().Length);
                CollectionAssert.AreEqual(geometry,filters.Select(f=>f.sharedMesh).ToArray());
                var owned=root.GetComponentsInChildren<MeshRenderer>().SelectMany(r=>r.sharedMaterials).ToArray();
                foreach(var material in owned)
                {
                    Assert.AreEqual("QuietCamp/FoliageLit",material.shader.name);Assert.AreEqual(1,material.GetFloat("_SnowCover"));
                    Assert.AreEqual(0,material.GetFloat("_SwayAmp"));Assert.AreEqual(0,material.GetFloat("_FlutterAmp"));
                    Assert.GreaterOrEqual(material.FindPass("ShadowCaster"),0);Assert.GreaterOrEqual(material.FindPass("DepthOnly"),0);
                }
                Object.Destroy(root);yield return null;yield return null;
                Assert.IsTrue(owned.All(m=>m==null));Assert.IsTrue(originals.All(m=>m!=null));Assert.IsTrue(geometry.All(m=>m!=null));
            }
        }
        [UnityTest] public IEnumerator AutumnLeavesUseRealCanopySourcesRespectBudgetAndClearUnderReducedMotion()
        {
            var root=new GameObject("autumn source test");var cameraRoot=new GameObject("autumn camera");
            var camera=cameraRoot.AddComponent<Camera>();camera.enabled=false;CameraFitter.Configure(camera);camera.orthographicSize=13;camera.aspect=1.5f;
            var level=Level("autumn",18);var forest=root.AddComponent<EnvironmentComposer>();
            var host=root.AddComponent<CampAtmosphere>();host.RestoreEnvironmentOnDestroy=false;
            typeof(CampAtmosphere).GetField("_windSim",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(host,new WindSim(41,.6f,20,30));
            var leafRoot=new GameObject("leaf pool");leafRoot.transform.SetParent(root.transform,false);var leaves=leafRoot.AddComponent<SeasonalLeafFall>();
            try
            {
                forest.Configure(level,camera,root.transform,AssetCatalog.Load());leaves.Configure(host,forest,level,camera);
                Assert.Greater(leaves.Sources.Count,0);var crowns=forest.LeafSources.ToArray();
                foreach(var source in leaves.Sources)Assert.Contains(source,crowns);
                for(int frame=0;frame<100;frame++)leaves.Advance(.05f,2,false);
                Assert.That(leaves.ActiveLeafCount,Is.InRange(1,SeasonalLeafFall.MaximumLeaves));
                Assert.AreEqual(1,leafRoot.GetComponentsInChildren<MeshRenderer>().Length);Assert.IsEmpty(leafRoot.GetComponentsInChildren<Collider>());
                leaves.Advance(.05f,0,false);Assert.LessOrEqual(leaves.ActiveLeafCount,6);
                leaves.Advance(.05f,0,true);Assert.AreEqual(0,leaves.ActiveLeafCount);Assert.AreEqual(0,leafRoot.GetComponent<MeshFilter>().sharedMesh.vertexCount);
                Assert.IsFalse(leafRoot.GetComponent<MeshRenderer>().enabled);
            }
            finally{Object.Destroy(root);Object.Destroy(cameraRoot);}yield return null;
        }
        [UnityTest] public IEnumerator SeasonalRoadmapFitsMobileVertexLimits()
        {
            var painterType=typeof(RoadmapSceneGenerator).Assembly.GetType("QuietCamp.Presentation.UI.RoadmapPainter");
            var painter=Activator.CreateInstance(painterType,new object[]{RoadmapModelLibrary.Load()});
            foreach(var season in new[]{"spring","summer","autumn","winter"})
            {
                var scene=RoadmapSceneGenerator.Generate(new LevelSummary{id="season-map",number=18,width=8,height=8,entry=new[]{0,0},decorSeed=41,lighting="noon",
                    environment=new EnvironmentCompositionData{seasonId=season,biomeId="meadow",treeDensity=1}},AtmosphereCatalog.Load());
                using(var vertices=new VertexHelper())
                {
                    painterType.GetMethod("Glade",BindingFlags.Public|BindingFlags.Static).Invoke(null,new object[]{vertices,scene,Vector2.zero,20f});
                    foreach(var prop in scene.Props)painterType.GetMethod("Shadow").Invoke(painter,new object[]{vertices,scene,prop,Vector2.zero,20f});
                    foreach(var prop in scene.Props)painterType.GetMethod("Model").Invoke(painter,new object[]{vertices,scene,prop,Vector2.zero,20f});
                    Assert.Less(vertices.currentVertCount,55000,season);Assert.AreEqual(0,painterType.GetProperty("TruncatedModels").GetValue(painter),season);
                }
            }
            yield return null;
        }
        [UnityTest,Timeout(240000)] public IEnumerator FourSeasonsHaveRenderedPhoneAndWideCapturesOnLowAndHigh()
        {
            if(UnityEngine.Application.isBatchMode)Assert.Ignore("Requires rendered Game View in an isolated QA project");
            var helper=typeof(ScreenshotPlayModeTest);
            void Size(int w,int h)=>helper.GetMethod("SetGameViewSize",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{w,h});
            IEnumerator Shot(string name)=>(IEnumerator)helper.GetMethod("Shot",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{Path.Combine(Directory.GetCurrentDirectory(),"Screenshots/Seasons"),name});
            yield return SceneManager.LoadSceneAsync("Boot");yield return PrivacyBootTestSupport.EnterGame();
            var services=QuietCamp.Presentation.QuietCampBootstrap.ServicesRef;Assert.NotNull(services);services.Tutorial.Skip();
            var adaptive=Object.FindAnyObjectByType<QuietCamp.Presentation.AdaptiveCampQuality>();bool adaptiveEnabled=adaptive!=null&&adaptive.enabled;if(adaptive!=null)adaptive.enabled=false;
            int quality=services.Settings.quality,effective=services.EffectiveQuality,renderProfile=QualitySettings.GetQualityLevel();bool reduced=services.ReducedMotion;
            try
            {
                foreach(var season in new[]{"spring","summer","autumn","winter"})
                {
                    var summary=CampContent.Summaries.First(s=>s.environment?.seasonId==season);
                    services.PendingLevelId=summary.id;services.Save.Session=new QuietCamp.Application.SessionSaveData();
                    yield return SceneManager.LoadSceneAsync("Camp");for(int i=0;i<35;i++)yield return null;
                    var host=CampSceneHost.Current;host.Session.DebugApplyWitness();host.Session.Select(null);host.SetAtmospherePhase("morning");
                    services.ReducedMotion=false;
                    foreach(int tier in new[]{1,3})foreach(var size in new[]{new Vector2Int(720,1600),new Vector2Int(1280,800)})
                    {
                        services.Settings.quality=tier;services.EffectiveQuality=tier-1;QualitySettings.SetQualityLevel(tier-1,true);
                        Size(size.x,size.y);for(int i=0;i<16;i++)yield return null;
                        var floor=Object.FindAnyObjectByType<VisibleForestFloor>();floor.RefreshNow();
                        Assert.LessOrEqual(floor.VisibleTileCount,VisibleForestFloor.MaximumTiles);
                        if(season=="winter")Assert.AreEqual(0,floor.VisibleRichPlantCount);
                        yield return Shot(season+"-"+(tier==1?"low":"high")+"-"+size.x+"x"+size.y);
                    }
                }
            }
            finally{services.Settings.quality=quality;services.EffectiveQuality=effective;QualitySettings.SetQualityLevel(renderProfile,true);services.ReducedMotion=reduced;if(adaptive!=null)adaptive.enabled=adaptiveEnabled;}
        }
    }
}
