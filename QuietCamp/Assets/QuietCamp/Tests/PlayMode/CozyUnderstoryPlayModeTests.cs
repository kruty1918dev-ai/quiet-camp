using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using QuietCamp.Infrastructure;
using QuietCamp.Presentation;
using QuietCamp.Presentation.World;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

namespace QuietCamp.Tests
{
    public class CozyUnderstoryPlayModeTests
    {
        [UnityTest]
        public IEnumerator AllThirtyClearingsHaveBoundedRootedGreeneryWithoutBlockingTrails()
        {
            var library=CozyVegetationLibrary.Load();Assert.NotNull(library);
            int minimum=int.MaxValue,maximum=0,triangleBudget=0;
            foreach(var id in LevelLoader.MvpLevelIds())
            {
                var level=LevelLoader.Load(id);string before=JsonUtility.ToJson(level);
                var go=new GameObject("cozy-content-test");var greenery=go.AddComponent<CozyUnderstory>();greenery.Build(level);
                // This component now supplies only trail accents. Ordinary
                // greenery belongs to the culled, continuous forest floor.
                var season=SeasonProfile.For(level);
                if(season.Winter)Assert.AreEqual(0,greenery.PlantRoots.Count,id+" winter must not retain living green trail accents");
                else if(season.Autumn)Assert.Less(greenery.PlantRoots.Count,QuietCamp.Domain.CampAccess.Points(level).Count()*34*season.ShrubWeight+8,id+" autumn verge must thin with leaf shedding");
                else Assert.Greater(greenery.PlantRoots.Count,8,id);
                Assert.LessOrEqual(greenery.PlantRoots.Count,QuietCamp.Domain.CampAccess.Points(level).Count()*34,id+" duplicated near-board vegetation");
                minimum=Mathf.Min(minimum,greenery.PlantRoots.Count);maximum=Mathf.Max(maximum,greenery.PlantRoots.Count);
                foreach(var p in greenery.PlantRoots)
                {
                    Assert.IsTrue(Mathf.Abs(p.x)>level.width*.5f+.18f||Mathf.Abs(p.z)>level.height*.5f+.18f,id+" decor enters puzzle");
                    Assert.IsFalse(CampTrail.IsCorridor(level,p,.1f),id+" blocked trail");
                }
                var renderers=go.GetComponentsInChildren<MeshRenderer>(true);
                Assert.LessOrEqual(renderers.Length,24,id+" too many draws");
                var meshes=renderers.Select(r=>r.GetComponent<MeshFilter>().sharedMesh).ToArray();
                Assert.LessOrEqual(meshes.Sum(m=>m.triangles.Length/3),10000,id+" trail accent geometry budget");
                triangleBudget=Mathf.Max(triangleBudget,meshes.Sum(m=>m.triangles.Length/3));
                foreach(var mesh in meshes)foreach(var vertex in mesh.vertices)
                    Assert.IsTrue(Mathf.Abs(vertex.x)>level.width*.5f+.15f||Mathf.Abs(vertex.z)>level.height*.5f+.15f,id+" a rotated leaf overlaps puzzle");
                foreach(var r in renderers)
                {
                    Assert.AreEqual(ShadowCastingMode.On,r.shadowCastingMode);Assert.IsTrue(r.receiveShadows);
                    Assert.AreEqual("QuietCamp/FoliageLit",r.sharedMaterial.shader.name);Assert.AreEqual(1,r.sharedMaterial.GetFloat("_ClusterWind"));
                    var plants=new System.Collections.Generic.List<Vector4>();r.GetComponent<MeshFilter>().sharedMesh.GetUVs(1,plants);
                    Assert.IsTrue(plants.All(p=>p.w>0&&p.w<1),"Every scaled plant must retain a positive, bounded wind height");
                }
                Assert.IsEmpty(go.GetComponentsInChildren<Collider>(true));Assert.AreEqual(before,JsonUtility.ToJson(level),"Decor modified puzzle");
                var mats=renderers.Select(r=>r.sharedMaterial).ToArray();Object.Destroy(go);yield return null;
                Assert.IsTrue(meshes.All(m=>m==null));Assert.IsTrue(mats.All(m=>m==null));
                Assert.IsTrue(library.plants.All(p=>p.mesh!=null),"Destroyed shared source mesh");
            }
            Debug.Log("[CozyQA] campaign plants="+minimum+".."+maximum+" maximum triangles="+triangleBudget);
        }
        [UnityTest]
        public IEnumerator LowQualityTrailEdgesStayAliveBeyondTheClearing()
        {
            foreach(var id in LevelLoader.MvpLevelIds())
            {
                var level=LevelLoader.Load(id);var root=new GameObject("trail-side-greenery-test");
                root.AddComponent<CozyUnderstory>().Build(level);
                var plants=new System.Collections.Generic.HashSet<Vector4>();
                foreach(var filter in root.transform.Find("UnderstoryTier0").GetComponentsInChildren<MeshFilter>(true))
                {
                    var stream=new System.Collections.Generic.List<Vector4>();filter.sharedMesh.GetUVs(1,stream);
                    foreach(var p in stream)plants.Add(p);
                }
                foreach(var access in QuietCamp.Domain.CampAccess.Points(level))
                {
                    var distant=CampTrail.Centre(level,access,6);
                    Assert.GreaterOrEqual(plants.Count(p=>p.w<=.37f && Vector2.Distance(new Vector2(p.x,p.y),new Vector2(distant.x,distant.z))<1.7f),2,id+" distant trail has no low-tier verge plants");
                }
                Object.Destroy(root);yield return null;
            }
        }
        [UnityTest]
        public IEnumerator QualityPreservesLiveGreeneryAndPortraitLandscapeMenusShareIt()
        {
            yield return SceneManager.LoadSceneAsync("Boot");
            yield return PrivacyBootTestSupport.EnterGame();for(int i=0;i<80;i++)yield return null;
            var services=QuietCampBootstrap.ServicesRef;Assert.NotNull(services);services.PendingLevelId="QC003";services.ReducedMotion=false;
            yield return SceneManager.LoadSceneAsync("Camp");for(int i=0;i<50;i++)yield return null;
            var details=Object.FindAnyObjectByType<CozyUnderstory>();Assert.NotNull(details);
            int previous=0;
            var folder=Path.Combine(Directory.GetCurrentDirectory(),"Screenshots/CozyVegetation");
            foreach(int quality in new[]{1,2,3})
            {
                services.Settings.quality=quality;for(int i=0;i<20;i++)yield return null;
                Assert.Greater(details.ActivePlantCount,previous);
                previous=details.ActivePlantCount;
                Debug.Log("[CozyQA] tier="+quality+" plants="+previous+" draws="+details.GetComponentsInChildren<MeshRenderer>().Length);
                foreach(var p in CampSceneHost.Current.Session.Level.witness)
                    if(!CampSceneHost.Current.Session.State.Placements.Any(t=>t.guestId==p.guestId))
                        CampSceneHost.Current.Session.TryCommit(QuietCamp.Application.PlacementCommand.Place(p.guestId,p.x,p.z,p.rotation),out _);
                CampSceneHost.Current.Session.Select(null);CampSceneHost.Current.SetAtmospherePhase("day");
                Size(720,1600);for(int i=0;i<8;i++)yield return null;yield return Shot(folder,"portrait-tier"+quality);
            }
            Size(1280,800);for(int i=0;i<20;i++)yield return null;yield return Shot(folder,"landscape-tablet");
            services.Settings.quality=2;Size(720,1600);
            yield return SceneManager.LoadSceneAsync("MainMenu");for(int i=0;i<40;i++)yield return null;
            Assert.NotNull(Object.FindAnyObjectByType<CozyUnderstory>());yield return Shot(folder,"main-menu");
        }
        const BindingFlags Private=BindingFlags.Static|BindingFlags.NonPublic;
        static void Size(int width,int height)=>typeof(ScreenshotPlayModeTest).GetMethod("SetGameViewSize",Private).Invoke(null,new object[]{width,height});
        static IEnumerator Shot(string folder,string name)=>(IEnumerator)typeof(ScreenshotPlayModeTest).GetMethod("Shot",Private).Invoke(null,new object[]{folder,name});
    }
}
