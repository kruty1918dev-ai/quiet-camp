using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using QuietCamp.Application;
using QuietCamp.Presentation;
using QuietCamp.Presentation.World;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace QuietCamp.Tests
{
    public sealed class BoardRimPlayModeTests
    {
        const BindingFlags Hidden = BindingFlags.NonPublic | BindingFlags.Static;
        static IEnumerator Frames(int n) { for(int i=0;i<n;i++)yield return null; }
        static void Size(int w,int h) => typeof(ScreenshotPlayModeTest).GetMethod("SetGameViewSize",Hidden).Invoke(null,new object[]{w,h});
        static IEnumerator Shot(string name) => (IEnumerator)typeof(ScreenshotPlayModeTest).GetMethod("Shot",Hidden).Invoke(null,new object[]{Path.Combine(Directory.GetCurrentDirectory(),"Screenshots/BoardRim"),name});
        [UnityTest, Timeout(240000)] public IEnumerator LitBoundaryFitsSquareAndRectangularFieldsWithoutInteractionChanges()
        {
            if(UnityEngine.Application.isBatchMode)Assert.Ignore("Requires rendered Game View");
            Size(720,1600);yield return SceneManager.LoadSceneAsync("Boot");
            yield return PrivacyBootTestSupport.EnterGame();yield return Frames(120);
            var services=QuietCampBootstrap.ServicesRef;services.ReducedMotion=true;services.Settings.textScale=1;
            services.Localization.TrySetLanguage("uk");
            foreach(var id in new[]{"QC001","QC008","QC009"})
            {
                services.PendingLevelId=id;services.Save.Session=new SessionSaveData();
                yield return SceneManager.LoadSceneAsync("Camp");yield return Frames(50);
                var session=CampSceneHost.Current.Session;var original=JsonUtility.ToJson(session.Level);
                session.DebugApplyWitness();yield return Frames(20);
                var rim=Object.FindAnyObjectByType<BoardRim>();Assert.NotNull(rim);
                Assert.AreEqual(1,Object.FindObjectsByType<BoardRim>().Length);Assert.IsEmpty(rim.GetComponentsInChildren<Collider>());
                var mesh=rim.GetComponent<MeshFilter>().sharedMesh;Assert.LessOrEqual(mesh.triangles.Length/3,128);
                var renderer=rim.GetComponent<MeshRenderer>();Assert.IsTrue(renderer.receiveShadows);
                Assert.Greater(renderer.bounds.min.y, MeadowSurface.GroundY, "Boundary is buried below the meadow");
                Assert.IsFalse(renderer.sharedMaterial.IsKeywordEnabled("_EMISSION"));
                foreach(var size in new[]{new Vector2Int(720,1600),new Vector2Int(1280,800)})
                {
                    Size(size.x,size.y);yield return Frames(20);Assert.AreSame(session,CampSceneHost.Current.Session);
                    foreach(var x in new[]{mesh.bounds.min.x,mesh.bounds.max.x})
                    foreach(var z in new[]{mesh.bounds.min.z,mesh.bounds.max.z})
                    {
                        var point=Camera.main.WorldToScreenPoint(rim.transform.TransformPoint(new Vector3(x,.027f,z)));
                        Assert.That(point.x,Is.InRange(-2f,Screen.width+2f));Assert.That(point.y,Is.InRange(-2f,Screen.height+2f));
                    }
                    Assert.AreEqual(original,JsonUtility.ToJson(session.Level),"Presentation modified authored rules");
                    yield return Shot("camp_boundary_"+id+"_"+size.x+"x"+size.y);
                }
            }
        }
    }
}
