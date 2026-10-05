using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using QuietCamp.Infrastructure;
using QuietCamp.Presentation;
using QuietCamp.Presentation.World;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace QuietCamp.Tests
{
    public class RainShelterPlayModeTests
    {
        [UnityTest]
        public IEnumerator RainContactsUseSlopedTrianglesAndFollowMovedSurfaces()
        {
            int cached=RainSurface.CachedMeshCount;
            var root=new GameObject("rain-slope");var mesh=new Mesh();
            mesh.vertices=new[]{new Vector3(-1,0,-1),new Vector3(1,1,-1),new Vector3(-1,0,1),new Vector3(1,1,1)};
            mesh.triangles=new[]{0,2,1,1,2,3};mesh.RecalculateNormals();mesh.RecalculateBounds();
            var slope=new GameObject("roof",typeof(MeshFilter),typeof(MeshRenderer));slope.transform.SetParent(root.transform,false);
            slope.GetComponent<MeshFilter>().sharedMesh=mesh;
            var mat=new Material(Shader.Find("Universal Render Pipeline/Unlit"));slope.GetComponent<Renderer>().sharedMaterial=mat;
            mat.SetColor("_BaseColor",new Color(.12f,.22f,.16f));slope.layer=30;
            RainSurface.Attach(root);yield return null;
            var hit=RainSurface.Cast(new Ray(new Vector3(0,4,0),Vector3.down),0,root.transform);
            Assert.IsNotNull(hit.surface);Assert.That(hit.point.y,Is.EqualTo(.5f).Within(.001f));
            Assert.That(hit.normal.y,Is.InRange(.8f,.95f));
            var marks=new GameObject("marks").AddComponent<RainImpacts>();marks.Configure();marks.SetQuality(2,false);marks.Emit(hit,.12f);marks.Advance(.2f);
            marks.gameObject.layer=30;CaptureContact();
            var before=marks.GetComponent<MeshFilter>().sharedMesh.vertices;
            var centre=before.Take(24).Aggregate(Vector3.zero,(sum,v)=>sum+v)/24;
            Assert.That(Vector3.Distance(centre,hit.point+hit.normal*.009f),Is.LessThan(.01f),"A coplanar triangle seam cut the contact ring in half.");
            slope.transform.position=Vector3.right*2+Vector3.up;
            Assert.IsTrue(hit.Resolve(out var moved,out _));Assert.That(Vector3.Distance(moved,hit.point+Vector3.right*2+Vector3.up),Is.LessThan(.001f));
            marks.Advance(0);var after=marks.GetComponent<MeshFilter>().sharedMesh.vertices;
            Assert.That(Vector3.Distance(after[0]-before[0],new Vector3(2,1,0)),Is.LessThan(.001f));
            marks.SetQuality(0,true);marks.Advance(0);Assert.AreEqual(0,marks.ActiveCount);
            Assert.IsFalse(marks.GetComponent<Renderer>().enabled,"An empty rain pool must not submit a draw.");
            Object.Destroy(root);Object.Destroy(marks.gameObject);Object.Destroy(mesh);Object.Destroy(mat);yield return null;
            Assert.AreEqual(cached,RainSurface.CachedMeshCount,"Shared rain geometry leaked.");
        }
        static void CaptureContact()
        {
            var go=new GameObject("rain-contact-camera");var camera=go.AddComponent<Camera>();
            camera.orthographic=true;camera.orthographicSize=1.5f;camera.cullingMask=1<<30;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.08f,.12f,.10f);
            camera.transform.position=new Vector3(-3,5,4);camera.transform.LookAt(new Vector3(0,.5f,0));
            var target=RenderTexture.GetTemporary(384,384,24);var previous=RenderTexture.active;
            camera.targetTexture=target;camera.Render();RenderTexture.active=target;
            var image=new Texture2D(384,384,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,384,384),0,0);image.Apply();
            var folder=Path.Combine(Directory.GetCurrentDirectory(),"Screenshots/RainContacts");Directory.CreateDirectory(folder);
            File.WriteAllBytes(Path.Combine(folder,"01_sloped_roof_contact.png"),image.EncodeToPNG());
            RenderTexture.active=previous;camera.targetTexture=null;RenderTexture.ReleaseTemporary(target);Object.Destroy(image);Object.Destroy(go);
        }

        [UnityTest]
        public IEnumerator DeviceTentMeshesRemainReadableForExactRoofContacts()
        {
            var catalog=AssetCatalog.Load();
            foreach(var id in new[]{"tent_smallOpen","tent_detailedOpen"})
            {
                var root=Object.Instantiate(catalog.Prefab(id));RainSurface.Attach(root);TentCloth.Apply(root);
                var mesh=root.GetComponentInChildren<MeshFilter>().sharedMesh;Assert.IsTrue(mesh.isReadable,id);
                var renderer=root.GetComponentInChildren<Renderer>();var bounds=renderer.bounds;bool roof=false;
                for(int x=1;x<8;x++)for(int z=1;z<8;z++)
                {
                    var p=new Vector3(Mathf.Lerp(bounds.min.x,bounds.max.x,x/8f),bounds.max.y+2,Mathf.Lerp(bounds.min.z,bounds.max.z,z/8f));
                    var hit=RainSurface.Cast(new Ray(p,Vector3.down),0,root.transform);
                    if(hit.surface!=null&&hit.normal.y>.2f&&hit.normal.y<.95f&&hit.point.y>.2f)roof=true;
                }
                Assert.IsTrue(roof,"Rain query used a box instead of the sloped tent roof: "+id);
                Object.Destroy(root);yield return null;
            }
        }

        [UnityTest]
        public IEnumerator RainQuenchesFireWarmsTentsAndLeavesPuzzleAndHistoryStable()
        {
            yield return SceneManager.LoadSceneAsync("Boot");
            yield return PrivacyBootTestSupport.EnterGame();
            var deadline=Time.realtimeSinceStartup+20;
            while(!Object.FindAnyObjectByType<QuietCampBootstrap>().StartupReady&&Time.realtimeSinceStartup<deadline)yield return null;
            var services=QuietCampBootstrap.ServicesRef;services.PendingLevelId="QC_TEST";services.ReducedMotion=false;
            yield return SceneManager.LoadSceneAsync("Camp");
            deadline=Time.realtimeSinceStartup+20;
            while((CampSceneHost.Current==null||!CampSceneHost.Current.IsReady)&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.IsNotNull(CampSceneHost.Current);Assert.IsTrue(CampSceneHost.Current.IsReady);
            var host=CampSceneHost.Current;host.Session.Restore(System.Array.Empty<QuietCamp.Domain.Placement>(),null);
            foreach(var p in host.Session.Level.witness)Assert.IsTrue(host.Session.TryCommit(QuietCamp.Application.PlacementCommand.Place(p.guestId,p.x,p.z,p.rotation),out _));
            host.SetAtmospherePhase("evening");
            var before=host.Session.State.Placements.Select(p=>p.Copy()).ToArray();var noise=Newtonsoft.Json.JsonConvert.SerializeObject(host.Session.Level.noise);
            var shelter=host.Atmosphere.RainShelter;Assert.IsNotNull(shelter);
            shelter.Advance(5,.7f);yield return null;
            Assert.IsTrue(shelter.Extinguished);Assert.AreEqual(0,shelter.FireStrength);
            Assert.IsFalse(host.Atmosphere.HasFire);
            Assert.IsTrue(Object.FindObjectsByType<Kruty1918.Atmos.FireVisual>(FindObjectsInactive.Include,FindObjectsSortMode.None).All(f=>!f.gameObject.activeSelf));
            Assert.IsTrue(Object.FindObjectsByType<TentCloth>().All(t=>t.ShelterWarmth>.3f));
            host.SetAtmospherePhase("night");shelter.Advance(4,0);yield return null;
            Assert.IsFalse(host.Atmosphere.HasFire,"Dry weather must not magically relight a quenched fire.");
            Assert.AreEqual(noise,Newtonsoft.Json.JsonConvert.SerializeObject(host.Session.Level.noise));
            Assert.AreEqual(Newtonsoft.Json.JsonConvert.SerializeObject(before),Newtonsoft.Json.JsonConvert.SerializeObject(host.Session.State.Placements));
            Assert.IsTrue(QuietCamp.Domain.RuleEvaluator.Evaluate(host.Session.Level,host.Session.State.Placements).IsSolved);Assert.IsTrue(host.Session.CanUndo);
            host.Session.Undo();host.Session.Redo();Assert.IsTrue(QuietCamp.Domain.RuleEvaluator.Evaluate(host.Session.Level,host.Session.State.Placements).IsSolved);
            var weather=host.Atmosphere.Weather;weather.Advance(78,2,false,Vector2.right);
            var rain=weather.transform.Find("GentleRain").GetComponent<ParticleSystem>();
            for(int i=0;i<80;i++)
            {rain.Simulate(.05f,false,false,false);weather.Advance(.05f,2,false,Vector2.right);}
            Assert.Greater(weather.Impacts.ActiveCount,0);
            weather.Advance(0,0,true,Vector2.zero);Assert.AreEqual(0,weather.Impacts.ActiveCount);
            foreach(var boot in Object.FindObjectsByType<QuietCampBootstrap>())Object.Destroy(boot.gameObject);
            yield return null;
        }
    }
}
