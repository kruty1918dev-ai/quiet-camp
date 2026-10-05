using System;
using System.Collections;
using System.Linq;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using QuietCamp.Domain;
using QuietCamp.Infrastructure;
using QuietCamp.Presentation.World;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

namespace QuietCamp.Tests
{
    public sealed class TentLivingSpacePlayModeTests
    {
#if UNITY_EDITOR
        bool _async;
        [SetUp] public void CompileShadersSynchronously(){_async=UnityEditor.ShaderUtil.allowAsyncCompilation;UnityEditor.ShaderUtil.allowAsyncCompilation=false;}
        [TearDown] public void RestoreCompilation()=>UnityEditor.ShaderUtil.allowAsyncCompilation=_async;
#endif
        [UnityTest,Timeout(240000)] public IEnumerator WeatherAndInteriorHaveRenderedPhoneAndWideCaptures()
        {
            if(UnityEngine.Application.isBatchMode)Assert.Ignore("Needs rendered Game View");
            var helper=typeof(ScreenshotPlayModeTest);
            void Size(int w,int h)=>helper.GetMethod("SetGameViewSize",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{w,h});
            IEnumerator Shot(string name)=>(IEnumerator)helper.GetMethod("Shot",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{Path.Combine(Directory.GetCurrentDirectory(),"Screenshots/TentLivingSpace"),name});
            Size(720,1600);yield return SceneManager.LoadSceneAsync("Boot");yield return PrivacyBootTestSupport.EnterGame();
            var services=QuietCamp.Presentation.QuietCampBootstrap.ServicesRef;services.Tutorial.Skip();
            var adaptive=Object.FindAnyObjectByType<QuietCamp.Presentation.AdaptiveCampQuality>();bool enabled=adaptive!=null&&adaptive.enabled;
            if(adaptive!=null)adaptive.enabled=false;
            int quality=services.Settings.quality,tierBefore=services.EffectiveQuality,profileBefore=QualitySettings.GetQualityLevel();bool reduced=services.ReducedMotion;
            try
            {
                services.ReducedMotion=true;
                foreach(var size in new[]{new Vector2Int(720,1600),new Vector2Int(1280,800)})
                foreach(var id in new[]{"QC007","gen:qc_camp:9","gen:qc_camp:14"})
                foreach(int tier in new[]{0,2})
                {
                    Size(size.x,size.y);services.Settings.quality=tier+1;services.EffectiveQuality=tier;QualitySettings.SetQualityLevel(tier,true);
                    services.PendingLevelId=id;yield return SceneManager.LoadSceneAsync("Camp");
                    for(int f=0;f<20;f++)yield return null;
                    var host=CampSceneHost.Current;Assert.NotNull(host);host.Session.DebugApplyWitness();
                    for(int f=0;f<15;f++)yield return null;
                    var atmosphere=host.Atmosphere;Assert.NotNull(atmosphere);
                    var stories=Object.FindObjectsByType<TentStoryVisual>();Assert.Greater(stories.Length,0);
                    float rain=id=="QC007"?.7f:0,snow=id=="gen:qc_camp:14"?.8f:0;
                    atmosphere.RainShelter.Advance(60,rain);
                    foreach(var story in stories)
                    {
                        Assert.IsTrue(story.Settled);var cloth=story.GetComponent<TentCloth>();cloth.AdvanceSurface(60,rain,snow,.4f);
                        if(rain>0)Assert.Greater(cloth.Wetness,.4f);if(snow>0)Assert.Greater(cloth.SnowCover,.4f);
                        if(id=="gen:qc_camp:9")Assert.Greater(cloth.LeafCover,0);
                        foreach(var renderer in story.GetComponentsInChildren<MeshRenderer>())
                        foreach(var material in renderer.sharedMaterials)
                        {
                            Assert.IsTrue(material.shader.isSupported,renderer.name);
#if UNITY_EDITOR
                            Assert.IsEmpty(UnityEditor.ShaderUtil.GetShaderMessages(material.shader).Where(m=>m.severity.ToString()=="Error").ToArray(),material.shader.name);
#endif
                        }
                    }
                    for(int frame=0;frame<3;frame++)yield return null;
                    var hud=Object.FindObjectsByType<QuietCamp.Presentation.UI.HtmlSurface>().First(s=>s.name=="CampHtml");
                    foreach(var chip in hud.GetComponentsInChildren<RectTransform>().Where(r=>r.name.StartsWith("<view #tent-chip-")))
                    {
                        var corners=new Vector3[4];chip.GetWorldCorners(corners);
                        foreach(var corner in corners)
                        {
                            var point=RectTransformUtility.WorldToScreenPoint(null,corner);
                            Assert.That(point.x,Is.InRange(-1f,Screen.width+1f),"Selected tent controls clip horizontally");
                            Assert.That(point.y,Is.InRange(-1f,Screen.height+1f),"Selected tent controls clip vertically");
                        }
                    }
                    yield return Shot(id.Replace(':','_')+"_tier"+tier+"_"+size.x+"x"+size.y);
                }
            }
            finally
            {
                services.Settings.quality=quality;services.EffectiveQuality=tierBefore;services.ReducedMotion=reduced;
                QualitySettings.SetQualityLevel(profileBefore,true);
                if(adaptive!=null)adaptive.enabled=enabled;
            }
        }
        static BoardRenderer Board(LevelData level,GameObject world)
        {
            Transform Root(string name){var root=new GameObject(name);root.transform.SetParent(world.transform,false);return root.transform;}
            return new BoardRenderer(level,AssetCatalog.Load(),Root("Base"),Root("Grid"),Root("Obstacle"),Root("Tents"),Root("Overlay"));
        }
        [UnityTest] public IEnumerator BelongingsWaitForTheTentAndExteriorDoesNotTravelDuringAGrabOrCancellation()
        {
            var level=LevelLoader.Load("QC001");level.width=level.height=8;
            var world=new GameObject("tent life lifecycle");var board=Board(level,world);
            try
            {
                var placement=new[]{new Placement{guestId=level.guests[0].id,x=3,z=3}};board.SyncPlacements(placement,level);
                var presenter=board.Tents[placement[0].guestId];var story=presenter.Root.GetComponent<TentStoryVisual>();
                Assert.IsFalse(story.Settled);Assert.IsNull(story.OutdoorRoot,"Outdoor things must not be attached to the moving model");
                yield return new WaitForSecondsRealtime(.55f);Assert.IsTrue(story.Settled);
                Assert.Greater(story.OutdoorRects.Count(r=>r.width>0),0);
                var outside=story.OutdoorRoot;
                if(outside!=null)
                {
                    Assert.IsFalse(outside.IsChildOf(presenter.Root.transform));var before=outside.position;bool active=outside.gameObject.activeSelf;
                    presenter.SetHeldCard(true);presenter.SetLifted(true,true);yield return null;
                    Assert.AreEqual(before,outside.position);Assert.AreEqual(active,outside.gameObject.activeSelf);
                    presenter.SetHeldCard(false);presenter.SetLifted(false,true);yield return null;
                    Assert.AreEqual(before,outside.position);Assert.IsTrue(story.Settled,"Cancellation must not restart packing or create placement commands");
                }
                var moved=placement[0].Copy();moved.x=4;moved.rotation=1;
                presenter.SetHeldCard(true);board.SyncPlacements(new[]{moved},level,1,true);presenter.SetHeldCard(false);
                yield return null;
                var interior=presenter.Root.transform.Find("LiftNode/Guest belongings");
                Assert.IsTrue(interior.gameObject.activeInHierarchy);Assert.IsTrue(interior.GetComponent<MeshRenderer>().enabled,"The committed drag left the interior invisible");
                var second=new[]{moved,new Placement{guestId=level.guests[1].id,x=0,z=3}};board.SyncPlacements(second,level,1,true);yield return null;
                var all=board.Tents.Values.SelectMany(t=>t.Root.GetComponent<TentStoryVisual>().OutdoorRects).Where(r=>r.width>0).ToArray();
                for(int a=0;a<all.Length;a++)for(int b=a+1;b<all.Length;b++)Assert.IsFalse(all[a].Overlaps(all[b]));
                foreach(var item in all){Assert.GreaterOrEqual(item.xMin,-level.width*.5f);Assert.LessOrEqual(item.xMax,level.width*.5f);Assert.GreaterOrEqual(item.yMin,-level.height*.5f);Assert.LessOrEqual(item.yMax,level.height*.5f);}
                string removed=second[0].guestId;var removedOutside=board.Tents[removed].Root.GetComponent<TentStoryVisual>().OutdoorRoot;
                board.SyncPlacements(second.Skip(1).ToArray(),level,1,true);yield return null;yield return null;
                Assert.IsTrue(removedOutside==null,"Removing a tent must release its independent exterior belongings");
            }
            finally{board.ClearAll();Object.Destroy(world);}
            yield return null;
        }
        [UnityTest] public IEnumerator InteriorFitsBothActualScaledPrefabsAndSensitiveItemsAreAlwaysSheltered()
        {
            foreach(var asset in new[]{"tent_smallOpen","tent_detailedOpen"})
            {
                var tent=Object.Instantiate(AssetCatalog.Load().Prefab(asset));
                try
                {
                    int colliders=tent.GetComponentsInChildren<Collider>().Length;
                    TentStoryVisual.Attach(tent,new LevelData{environment=new EnvironmentCompositionData{seasonId="summer"}},"anna");
                    var story=tent.GetComponent<TentStoryVisual>();yield return new WaitForSecondsRealtime(.5f);
                    var inside=tent.transform.Find("Guest belongings");Assert.NotNull(inside);
                    Assert.IsTrue(inside.gameObject.activeSelf);Assert.AreEqual(2,story.ShelteredOutdoorCount);
                    var mesh=inside.GetComponent<MeshFilter>().sharedMesh;Assert.Greater(mesh.vertexCount,250);
                    var room=story.LivingSpace.Bounds;
                    foreach(var vertex in mesh.vertices)
                    {
                        Assert.That(vertex.x,Is.InRange(room.min.x+.01f,room.max.x-.01f));
                        Assert.That(vertex.z,Is.InRange(room.min.z+.01f,room.max.z-.01f));
                        Assert.GreaterOrEqual(vertex.y,room.min.y);
                        float roof=room.min.y+room.size.y*(1-Mathf.Abs(vertex.x-room.center.x)/(room.size.x*.5f));
                        Assert.LessOrEqual(vertex.y,roof+.025f,"An item pierced the sloping canvas");
                    }
                    Assert.AreEqual(colliders,tent.GetComponentsInChildren<Collider>().Length);
                    var glow=tent.transform.Find("TentInteriorGlow");Assert.NotNull(glow);
                    Assert.Less(glow.GetComponent<Renderer>().bounds.size.x,room.size.x);
                    Assert.Less(glow.GetComponent<Renderer>().bounds.size.z,room.size.z);
                }
                finally{Object.Destroy(tent);}
                yield return null;
            }
        }
        [UnityTest] public IEnumerator PartialPackingSheltersTheMissingItemWithoutDuplicatingTheOutdoorItem()
        {
            var world=new GameObject("partial packing");
            try
            {
                foreach(var asset in new[]{"tent_smallOpen","tent_detailedOpen"})
                foreach(int outdoorItem in new[]{0,1})
                {
                    var tent=Object.Instantiate(AssetCatalog.Load().Prefab(asset),world.transform);
                    try
                    {
                        var level=new LevelData{width=8,height=8,environment=new EnvironmentCompositionData{seasonId="spring"}};
                        TentStoryVisual.Attach(tent,level,"anna");var story=tent.GetComponent<TentStoryVisual>();
                        var items=new Rect[2];items[outdoorItem]=new Rect(new Vector2(outdoorItem==0?1.3f:-1.5f,-.2f),TentBelongingsLayout.Sizes[outdoorItem]);
                        story.Schedule(new Placement{guestId="anna",x=3,z=3},items,world.transform,0,true);
                        Assert.AreEqual(1,story.ShelteredOutdoorCount);Assert.AreEqual(3^(1<<outdoorItem),story.ShelteredOutdoorMask);
                        var inside=tent.transform.Find("Guest belongings").GetComponent<MeshFilter>().sharedMesh;
                        // The pot's iron is visible indoors precisely when the
                        // stool, rather than the pot, found an outside place.
                        bool HasIron(Mesh mesh)=>mesh.colors.Any(c=>Mathf.Abs(c.r-.30f)<1e-5f&&Mathf.Abs(c.g-.34f)<1e-5f&&Mathf.Abs(c.b-.31f)<1e-5f);
                        Assert.AreEqual(outdoorItem==0,HasIron(inside));
                        var outside=story.OutdoorRoot.GetComponent<MeshFilter>().sharedMesh;
                        Assert.AreEqual(outdoorItem==1,HasIron(outside));
                        Assert.IsTrue(inside.colors.Any(c=>c.a>.1f),"Interior warmth must reach nearby possessions");
                        Assert.IsTrue(outside.colors.All(c=>c.a==0),"Sheltered lamp fill must not light outdoor objects");
                        foreach(var p in outside.vertices)
                        {Assert.That(p.x,Is.InRange(items[outdoorItem].xMin,items[outdoorItem].xMax));Assert.That(p.z,Is.InRange(items[outdoorItem].yMin,items[outdoorItem].yMax));}
                    }
                    finally{Object.Destroy(tent);}
                    yield return null;
                }
            }
            finally{Object.Destroy(world);}
            yield return null;
        }
        [UnityTest] public IEnumerator InactiveTentDefersOwnedResourcesAndRepeatedEnableDoesNotDuplicateItsInterior()
        {
            var parent=new GameObject("inactive tent pool");parent.SetActive(false);
            try
            {
                var tent=Object.Instantiate(AssetCatalog.Load().Prefab("tent_smallOpen"),parent.transform);
                int baseline=tent.GetComponentsInChildren<MeshFilter>(true).Length;
                TentStoryVisual.Attach(tent,new LevelData{environment=new EnvironmentCompositionData{seasonId="spring"}},"anna");
                Assert.AreEqual(baseline,tent.GetComponentsInChildren<MeshFilter>(true).Length,"An inactive tent must not allocate owned meshes");
                parent.SetActive(true);yield return null;
                var mesh=tent.transform.Find("Guest belongings").GetComponent<MeshFilter>().sharedMesh;
                int filters=tent.GetComponentsInChildren<MeshFilter>(true).Length;
                parent.SetActive(false);parent.SetActive(true);yield return null;
                Assert.AreEqual(filters,tent.GetComponentsInChildren<MeshFilter>(true).Length);
                Assert.AreSame(mesh,tent.transform.Find("Guest belongings").GetComponent<MeshFilter>().sharedMesh);
                var story=tent.GetComponent<TentStoryVisual>();story.BeginRemoval();
                yield return new WaitForSecondsRealtime(.5f);
                Assert.IsNull(story.OutdoorRoot);Assert.IsFalse(tent.transform.Find("Guest belongings").gameObject.activeSelf,"A retired tent must not finish deferred packing");
            }
            finally{Object.Destroy(parent);}
            yield return null;
        }
        [UnityTest] public IEnumerator StoragePressureAnchorStaysInTheFabricWhileSelectionLiftsAndScalesTheVisual()
        {
            var world=new GameObject("cloth-local storage");var level=LevelLoader.Load("QC001");var board=Board(level,world);
            try
            {
                var placement=new Placement{guestId=level.guests[0].id,x=0,z=1};board.SyncPlacements(new[]{placement},level,1,true);
                var tent=board.Tents[placement.guestId];var story=tent.Root.GetComponent<TentStoryVisual>();
                story.Schedule(placement,new Rect[2],world.transform,0,true);
                var cloth=tent.Root.GetComponent<TentCloth>();cloth.AdvanceSurface(1,0,0,0);
                var fabric=tent.Root.transform.Find("LiftNode/VisualCenter").GetComponent<MeshRenderer>().sharedMaterials.First(m=>m.shader.name=="QuietCamp/TentCloth");
                var before=fabric.GetVector("_StorageBulge");Assert.Less(before.x,fabric.GetVector("_MeshMin").x+fabric.GetVector("_MeshSize").x*.5f);
                tent.SetLifted(true,true);tent.Root.transform.Find("LiftNode").localScale=Vector3.one*.82f;
                cloth.SetStorage(1,story.LivingSpace.Bounds,true);
                Assert.Less((before-fabric.GetVector("_StorageBulge")).magnitude,.00001f,"Tweened lifting must not move packing pressure relative to the canvas");
            }
            finally{board.ClearAll();Object.Destroy(world);}
            yield return null;
        }
        [UnityTest] public IEnumerator PackedTentWeatherKeepsRigidSupportsAndUsesOneDeformationForRainAndShadows()
        {
            var tent=Object.Instantiate(AssetCatalog.Load().Prefab("tent_smallOpen"));
            try
            {
                TentStoryVisual.Attach(tent,new LevelData{environment=new EnvironmentCompositionData{seasonId="winter"}},"anna");
                var story=tent.GetComponent<TentStoryVisual>();yield return new WaitForSecondsRealtime(.5f);
                var cloth=tent.GetComponent<TentCloth>();cloth.AdvanceSurface(30,.7f,.8f,.3f);
                cloth.SetShelterWarmth(.7f);
                Assert.Greater(cloth.StoragePressure,.9f);Assert.Greater(cloth.Wetness,.4f);Assert.Greater(cloth.SnowCover,.3f);
                foreach(var renderer in tent.GetComponentsInChildren<MeshRenderer>())
                foreach(var material in renderer.sharedMaterials)
                    if(material.shader.name=="QuietCamp/TentCloth")
                    {Assert.AreEqual(cloth.Wetness,material.GetFloat("_ClothWetness"));Assert.AreEqual(cloth.SnowCover,material.GetFloat("_SnowCover"));Assert.GreaterOrEqual(material.FindPass("ShadowCaster"),0);}
                RainSurface.Attach(tent);RainSurface.CaptureWind();
                var at=tent.transform.TransformPoint(story.LivingSpace.Bounds.center+Vector3.right*(story.LivingSpace.Width*.2f)+Vector3.up*3);
                var contact=RainSurface.Cast(new Ray(at,Vector3.down),0,tent.transform,6);Assert.IsTrue(contact.IsFabric);
                Assert.IsTrue(contact.Resolve(out var point,out var normal));Assert.Less((point-contact.point).magnitude,.001f);Assert.Greater(normal.y,0);
                contact.surface.RefreshGeometry();Assert.IsFalse(contact.Resolve(out _,out _),"A rebuilt mesh must invalidate rain attached to its old triangles");
                var fresh=RainSurface.Cast(new Ray(at,Vector3.down),0,tent.transform,6);Assert.IsTrue(fresh.IsFabric);Assert.IsTrue(fresh.Resolve(out _,out _));
                Assert.IsFalse(tent.GetComponentsInChildren<MeshRenderer>().SelectMany(r=>r.sharedMaterials).Any(m=>m.shader.name=="Hidden/InternalErrorShader"));
                var inside=tent.transform.Find("Guest belongings").GetComponent<MeshRenderer>().sharedMaterial;
                Assert.AreEqual("QuietCamp/TentBelongings",inside.shader.name);Assert.AreEqual(.7f,inside.GetFloat("_ShelterGlow"));
            }
            finally{Object.Destroy(tent);}
            yield return null;
        }
    }
}
