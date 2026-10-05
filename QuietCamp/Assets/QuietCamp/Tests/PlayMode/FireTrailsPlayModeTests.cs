using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using QuietCamp.Domain;
using QuietCamp.Infrastructure;
using QuietCamp.Presentation;
using QuietCamp.Presentation.World;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

namespace QuietCamp.Tests
{
    public class FireTrailsPlayModeTests
    {
        static IEnumerator Camp(string id)
        {
            yield return SceneManager.LoadSceneAsync("Boot");
            yield return PrivacyBootTestSupport.EnterGame();
            float deadline=Time.realtimeSinceStartup+25;
            while((QuietCampBootstrap.ServicesRef==null||!Object.FindAnyObjectByType<QuietCampBootstrap>().StartupReady)&&Time.realtimeSinceStartup<deadline)yield return null;
            var services=QuietCampBootstrap.ServicesRef;Assert.NotNull(services);services.PendingLevelId=id;services.ReducedMotion=false;services.Settings.quality=2;
            yield return SceneManager.LoadSceneAsync("Camp");deadline=Time.realtimeSinceStartup+25;
            while((CampSceneHost.Current==null||!CampSceneHost.Current.IsReady)&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.IsTrue(CampSceneHost.Current.IsReady);
        }
        [UnityTest]
        public IEnumerator EveryCampaignAccessHasAnOpenContinuousGroundTrail()
        {
            foreach(var id in LevelLoader.MvpLevelIds())
            {
                int cached=RainSurface.CachedMeshCount;
                var level=LevelLoader.Load(id);var root=new GameObject("trail-test");CampTrail.BuildAll(level,root.transform);
                var trails=root.GetComponentsInChildren<CampTrail>();Assert.AreEqual(CampAccess.Points(level).Count(),trails.Length,id);
                foreach(var trail in trails)
                {
                    var outward=CampTrail.Outward(level,trail.AccessCell);
                    var start=BoardMath.CellCenterWorld(level,trail.AccessCell);
                    Assert.Greater(Vector3.Dot(trail.OuterEnd-start,outward),8,id);
                    Assert.IsTrue(Mathf.Abs(trail.OuterEnd.x)>level.width*.5f || Mathf.Abs(trail.OuterEnd.z)>level.height*.5f,id);
                    var mesh=trail.GetComponent<MeshFilter>().sharedMesh;
                    Assert.Greater(mesh.vertexCount,40);Assert.IsTrue(mesh.normals.All(n=>n.y>.9f));Assert.That(mesh.bounds.size.y,Is.LessThan(.03f));
                    Assert.IsTrue(trail.GetComponent<Renderer>().receiveShadows);Assert.IsEmpty(trail.GetComponentsInChildren<Collider>());
                    for(float distance=0;distance<8;distance+=.5f)
                        Assert.IsTrue(CampTrail.IsCorridor(level,CampTrail.Centre(level,trail.AccessCell,distance),.1f));
                    var materials=trail.GetComponentsInChildren<Renderer>().Select(r=>r.sharedMaterial).ToArray();
                    Object.Destroy(trail.gameObject);yield return null;
                    Assert.IsTrue(mesh==null);Assert.IsTrue(materials.All(m=>m==null),"Trail resources leaked.");
                }
                Object.Destroy(root);yield return null;Assert.AreEqual(cached,RainSurface.CachedMeshCount,"Trail rain query cache leaked.");
            }
        }
        [UnityTest]
        public IEnumerator DryFireRemainsVisibleInAllPhasesAndRainQuenchesIt()
        {
            yield return Camp("QC004");var host=CampSceneHost.Current;var site=Object.FindAnyObjectByType<CampfireSite>();Assert.NotNull(site);
            var cell=host.Session.Level.noise.Single();
            Assert.Less(Vector3.Distance(site.transform.position,BoardMath.CellCenterWorld(host.Session.Level,new Cell(cell[0],cell[1]))),.03f);
            var solid=site.transform.Find("campfire_stones").GetComponentsInChildren<Renderer>().Single().bounds;
            Assert.LessOrEqual(Mathf.Max(solid.size.x,solid.size.z),.87f);
            var logs=site.transform.Find("log_stack").GetComponentsInChildren<Renderer>().Single().bounds;
            Assert.LessOrEqual(logs.size.y,.181f);
            Assert.Less(logs.max.y,site.transform.Find("CampfireFx/Flame0").position.y,"Wood stack hides the flames.");
            Assert.IsTrue(RuleEvaluator.Evaluate(host.Session.Level,host.Session.Level.witness).IsSolved);
            foreach(var phase in new[]{"morning","day","evening","night"})
            {
                host.SetAtmospherePhase(phase);yield return null;
                Assert.IsTrue(site.Visual.gameObject.activeInHierarchy,phase);Assert.IsTrue(host.Atmosphere.HasFire,phase);
            }
            foreach(int quality in new[]{1,2,3})
            {
                QuietCampBootstrap.ServicesRef.Settings.quality=quality;yield return null;
                Assert.IsTrue(site.Visual.gameObject.activeInHierarchy);
                Assert.AreEqual(quality>1,site.GetComponentInChildren<Light>(true).enabled);
            }
            var effects=site.Visual.GetComponentsInChildren<Renderer>(true).Select(r=>r.sharedMaterial).ToArray();
            var ash=site.transform.Find("CharcoalBed").GetComponent<Renderer>().sharedMaterial;
            host.Atmosphere.RainShelter.Advance(5,.7f);yield return null;
            Assert.IsFalse(site.Visual.gameObject.activeInHierarchy);Assert.IsFalse(host.Atmosphere.HasFire);
            Assert.IsTrue(site.transform.Find("campfire_stones").gameObject.activeInHierarchy);
            Assert.IsTrue(site.transform.Find("log_stack").gameObject.activeInHierarchy);
            foreach(var phase in new[]{"day","evening","night"}){host.SetAtmospherePhase(phase);Assert.IsFalse(site.Visual.gameObject.activeSelf);}
            Object.Destroy(site.gameObject);yield return null;Assert.IsTrue(ash==null);Assert.IsTrue(effects.All(m=>m==null),"Fire effect materials leaked.");
        }
        [UnityTest]
        public IEnumerator PathsReplaceGlyphsAndForestTrunksLeaveThePassageOpen()
        {
            yield return Camp("QC009");var level=CampSceneHost.Current.Session.Level;
            var trails=Object.FindObjectsByType<CampTrail>();Assert.AreEqual(CampAccess.Points(level).Count(),trails.Length);
            foreach(var t in Object.FindObjectsByType<Transform>())
                Assert.IsFalse(t.name=="EntryMarker" || t.name.StartsWith("Access_exit") || t.name=="ExitChevron" || t.name.StartsWith("TrailSign_"),"Access still uses a glyph: "+t.name);
            var decor=Object.FindObjectsByType<Transform>().Single(t=>t.name=="DecorRoot");
            foreach(var t in decor.GetComponentsInChildren<Transform>(true).Where(t=>t.name.StartsWith("tree_")&&t.GetComponent<MeshFilter>()==null))
                Assert.IsFalse(CampTrail.IsCorridor(level,t.position,.5f),"A decorative tree trunk blocks the forest trail: "+t.name);
        }
        [UnityTest]
        public IEnumerator RenderedGameplayShowsDaytimeFireAndTrailsOnAllEdges()
        {
            const BindingFlags flags=BindingFlags.NonPublic|BindingFlags.Static;
            typeof(ScreenshotPlayModeTest).GetMethod("SetGameViewSize",flags).Invoke(null,new object[]{720,1600});
            var folder=Path.Combine(Directory.GetCurrentDirectory(),"Screenshots/FireTrails");
            foreach(var id in new[]{"QC004","QC005","QC007","QC009","gen:qc_camp:4"})
            {
                yield return Camp(id);var host=CampSceneHost.Current;
                foreach(var p in host.Session.Level.witness)host.Session.TryCommit(QuietCamp.Application.PlacementCommand.Place(p.guestId,p.x,p.z,p.rotation),out _);
                host.Session.Select(null);host.SetAtmospherePhase("day");for(int i=0;i<20;i++)yield return null;
                string name=id.Replace(':','_');
                yield return (IEnumerator)typeof(ScreenshotPlayModeTest).GetMethod("Shot",flags).Invoke(null,new object[]{folder,name});
                if(id=="QC004")
                {
                    var site=Object.FindAnyObjectByType<CampfireSite>();Assert.IsTrue(site.Visual.gameObject.activeInHierarchy);
                    var camera=Object.FindObjectsByType<Camera>().First(c=>c.orthographic&&c.gameObject.scene==host.gameObject.scene);
                    var at=camera.WorldToViewportPoint(site.transform.position+Vector3.up*.35f);
                    var image=new Texture2D(2,2);image.LoadImage(File.ReadAllBytes(Path.Combine(folder,name+".png")));
                    int hot=0;int cx=Mathf.RoundToInt(at.x*image.width),cy=Mathf.RoundToInt(at.y*image.height);
                    for(int x=Mathf.Max(0,cx-35);x<Mathf.Min(image.width,cx+35);x++)for(int y=Mathf.Max(0,cy-40);y<Mathf.Min(image.height,cy+40);y++)
                    {var p=image.GetPixel(x,y);if(p.r>.6f&&p.r>p.g*1.15f&&p.g>p.b*1.25f)hot++;}
                    Object.Destroy(image);Assert.Greater(hot,15,"A daytime fire exists in hierarchy but is not visibly warm in the rendered image.");
                }
            }
        }
    }
}
