using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using QuietCamp.Application;
using QuietCamp.Infrastructure;
using QuietCamp.Presentation;
using QuietCamp.Presentation.World;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace QuietCamp.Tests
{
    public class ForestLightingPlayModeTests
    {
        static IEnumerator Frames(int n) { for (int i = 0; i < n; i++) yield return null; }
        static readonly BindingFlags StaticPrivate = BindingFlags.NonPublic | BindingFlags.Static;
        static void Size(int w, int h) => typeof(ScreenshotPlayModeTest).GetMethod("SetGameViewSize", StaticPrivate).Invoke(null, new object[] { w,h });
        static IEnumerator Shot(string name) => (IEnumerator)typeof(ScreenshotPlayModeTest).GetMethod("Shot", StaticPrivate).Invoke(null, new object[] { Path.Combine(Directory.GetCurrentDirectory(), "Screenshots/ForestLighting"), name });
        static void Click(string id) => Object.FindObjectsByType<Button>().Single(b => b.name == "<button #" + id + ">").onClick.Invoke();
        static IEnumerator Camp()
        {
            yield return SceneManager.LoadSceneAsync("Boot");
            yield return PrivacyBootTestSupport.EnterGame(); yield return Frames(65);
            QuietCampBootstrap.ServicesRef.Tutorial.Skip(); // Lighting/album QA uses the complete UI, independently of onboarding saves.
            QuietCampBootstrap.ServicesRef.PendingLevelId = "QC010";
            yield return SceneManager.LoadSceneAsync("Camp"); yield return Frames(40);
        }
        [UnityTest] public IEnumerator ForestProbeIsDirectionalAndHasNoPlayerTextureDependency()
        {
            var environment = ForestLightingEnvironment.Load(); Assert.NotNull(environment);
            Assert.AreEqual(27, environment.coefficients.Length);
            Assert.IsTrue(environment.coefficients.All(c => !float.IsNaN(c) && !float.IsInfinity(c)));
            var directions = new[] {Vector3.up,Vector3.down,Vector3.right,Vector3.left,Vector3.forward,Vector3.back};
            var values = new Color[6]; environment.Probe(Color.white,0).Evaluate(directions,values);
            Assert.Greater(Mathf.Abs(values[0].r-values[1].r)+Mathf.Abs(values[0].g-values[1].g)+Mathf.Abs(values[0].b-values[1].b), .08f, "Sky and reflected forest-floor light must have different colors.");
            var catalog = AtmosphereCatalog.Load(); var day = new Color[6]; var night = new Color[6];
            environment.Probe(catalog.Get("noon").Ambient,0).Evaluate(directions,day);
            environment.Probe(catalog.Get("night").Ambient,0).Evaluate(directions,night);
            Assert.Less(night[0].grayscale,day[0].grayscale*.7f);
            Assert.IsNull(Resources.Load<Texture>("QuietCamp/sunset_forest_1k"));
            yield break;
        }
        [UnityTest] public IEnumerator ShaftsReachClearingRespectQualityWeatherAndReleaseResources()
        {
            yield return Camp(); var host = CampSceneHost.Current; var s = QuietCampBootstrap.ServicesRef;
            s.ReducedMotion = false; var light = host.Atmosphere.Sunlight; Assert.NotNull(light);
            foreach (var p in light.Landings)
                Assert.IsTrue(float.IsFinite(p.x)&&float.IsFinite(p.z));
            Assert.IsTrue(light.Landings.Any(p=>Mathf.Abs(p.x)>host.Session.Level.width*.5f||Mathf.Abs(p.z)>host.Session.Level.height*.5f),"Sun shafts must reach the surrounding landscape");
            string before = JsonUtility.ToJson(host.Session.Level);
            foreach (var pair in new[] {new[]{1,6},new[]{2,6},new[]{3,6}})
            {
                s.Settings.quality=pair[0]; yield return Frames(3); light.Advance(8);
                Assert.AreEqual(pair[1],light.ActiveShafts);
            }
            Assert.IsEmpty(light.GetComponentsInChildren<Collider>(true)); Assert.IsEmpty(light.GetComponentsInChildren<Light>(true));
            var meshes=light.GetComponentsInChildren<MeshFilter>(true).Select(f=>f.sharedMesh).Distinct().ToArray();Assert.AreEqual(1,meshes.Length);
            var materials=light.GetComponentsInChildren<Renderer>(true).Select(r=>r.sharedMaterial).ToArray();
            s.ReducedMotion=true;yield return Frames(2);light.Advance(3);
            float time=materials[0].GetFloat("_LightTime");light.Advance(3);Assert.AreEqual(time,materials[0].GetFloat("_LightTime"));Assert.AreEqual(6,light.ActiveShafts);
            float sunny=light.Visibility;host.Atmosphere.Weather.Advance(72,2,true,Vector2.zero);light.Advance(8);
            Assert.Less(host.Atmosphere.Weather.RainAmount,.1f,"The clear level must not silently become a rain level");
            Assert.Greater(light.Visibility,.1f,"Clear weather must retain soft sunlight");
            host.SetAtmospherePhase("night");light.Advance(15);Assert.AreEqual(0,light.ActiveShafts);
            Assert.AreEqual(before,JsonUtility.ToJson(host.Session.Level));
            Object.Destroy(light);yield return Frames(2);Assert.IsTrue(meshes.All(m=>m==null));Assert.IsTrue(materials.All(m=>m==null));
            s.ReducedMotion=false;
        }
        [UnityTest] public IEnumerator ActualScatteringPixelsDisappearBehindAShadowCaster()
        {
            if(UnityEngine.Application.isBatchMode)Assert.Ignore("Requires graphics.");
            yield return Camp(); var owners=Object.FindObjectsByType<CampAtmosphere>();foreach(var o in owners)o.enabled=false;
            var otherLights=Object.FindObjectsByType<Light>().Where(l=>l.enabled).ToArray();foreach(var l in otherLights)l.enabled=false;
            var oldSun=RenderSettings.sun;int oldQuality=QualitySettings.GetQualityLevel();QualitySettings.SetQualityLevel(2,true);
            var root=new GameObject("volumetric-occlusion-test");root.transform.position=new Vector3(3000,0,0);
            var cameraObject=new GameObject("Volume camera");cameraObject.transform.SetParent(root.transform,false);
            var camera=cameraObject.AddComponent<Camera>();camera.enabled=false;camera.cullingMask=1<<30;camera.orthographic=true;camera.orthographicSize=2.2f;
            camera.transform.localPosition=new Vector3(4,3,5);camera.transform.LookAt(root.transform.position+Vector3.up*1.5f);camera.farClipPlane=20;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;camera.allowHDR=false;
            var data=camera.GetUniversalAdditionalCameraData();data.requiresDepthTexture=true;data.renderPostProcessing=false;
            var target=new RenderTexture(256,256,24);camera.targetTexture=target;
            var sunObject=new GameObject("Test sun");sunObject.transform.SetParent(root.transform,false);var sun=sunObject.AddComponent<Light>();
            sun.type=LightType.Directional;sun.intensity=1;sun.shadows=LightShadows.Hard;sun.shadowBias=0;sun.shadowNormalBias=0;sun.cullingMask=1<<30;
            sun.transform.eulerAngles=new Vector3(90,0,0);RenderSettings.sun=sun;
            var shaft=GameObject.CreatePrimitive(PrimitiveType.Cube);shaft.transform.SetParent(root.transform,false);shaft.layer=30;
            shaft.transform.localPosition=Vector3.up*1.5f;shaft.transform.localScale=new Vector3(1,3,1);
            var material=new Material(Resources.Load<Shader>("QuietCamp/ForestVolume"));material.SetFloat("_Shaft",1);material.SetFloat("_Density",1.2f);material.SetFloat("_Steps",12);material.SetColor("_FogColor",new Color(1,.8f,.5f));
            shaft.GetComponent<Renderer>().sharedMaterial=material;shaft.GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.Off;
            var blocker=GameObject.CreatePrimitive(PrimitiveType.Cube);blocker.transform.SetParent(root.transform,false);blocker.layer=30;
            blocker.transform.localPosition=Vector3.up*3.2f;blocker.transform.localScale=new Vector3(2,.1f,2);
            blocker.GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.ShadowsOnly;blocker.SetActive(false);
            Color[] Capture(string name)
            {
                camera.Render();var previous=RenderTexture.active;RenderTexture.active=target;
                var image=new Texture2D(256,256,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,256,256),0,0);image.Apply();
                var folder=Path.Combine(Directory.GetCurrentDirectory(),"Screenshots/ForestLighting");Directory.CreateDirectory(folder);
                File.WriteAllBytes(Path.Combine(folder,name+".png"),image.EncodeToPNG());var pixels=image.GetPixels();Object.Destroy(image);RenderTexture.active=previous;return pixels;
            }
            try
            {
                yield return Frames(3);var open=Capture("volume-open");blocker.SetActive(true);yield return Frames(2);var occluded=Capture("volume-shadowed");
                int visible=open.Count(p=>p.r>.035f);Assert.Greater(visible,80,"Volume exists but produces no visible scattering.");
                Assert.Less(occluded.Sum(p=>p.r),open.Sum(p=>p.r)*.25f,"Sunlight passes through a solid canopy.");
                Debug.Log("[ForestLightingQA] scattering pixels="+visible+" shadow ratio="+occluded.Sum(p=>p.r)/open.Sum(p=>p.r));
            }
            finally
            {
                camera.targetTexture=null;target.Release();Object.Destroy(target);Object.Destroy(root);Object.Destroy(material);
                foreach(var l in otherLights)if(l!=null)l.enabled=true;foreach(var o in owners)if(o!=null)o.enabled=true;
                RenderSettings.sun=oldSun;QualitySettings.SetQualityLevel(oldQuality,true);
            }
        }
        [UnityTest] public IEnumerator SharedLightingRendersGameplayMenuAndAlbum()
        {
            if(UnityEngine.Application.isBatchMode)Assert.Ignore("Requires graphics.");
            Size(720,1600);yield return Camp();var s=QuietCampBootstrap.ServicesRef;s.Settings.quality=2;s.ReducedMotion=false;
            var host=CampSceneHost.Current;foreach(var p in host.Session.Level.witness)host.Session.TryCommit(PlacementCommand.Place(p.guestId,p.x,p.z,p.rotation),out _);
            host.Session.Select(null);host.Atmosphere.Sunlight.Advance(8);yield return Frames(10);
            yield return Shot("balanced-portrait");
            Size(1280,800);s.Settings.quality=3;yield return Frames(15);yield return Shot("high-tablet");
            host.SetAtmospherePhase("morning");yield return new WaitForSecondsRealtime(4);yield return Shot("morning-tablet");
            host.SetAtmospherePhase("night");host.Atmosphere.Sunlight.Advance(15);yield return new WaitForSecondsRealtime(4);yield return Shot("night-tablet");
            Size(720,1600);s.Settings.quality=1;yield return Frames(10);yield return Shot("low-night");
            s.Save.Album.entries=new[]{new AlbumSaveData.Entry{levelId=host.Session.Level.id,levelSnapshot=host.Session.Level,contentHash=host.Session.Level.contentHash,lighting="morning",placements=host.Session.Level.witness}};
            s.Settings.quality=2;yield return SceneManager.LoadSceneAsync("MainMenu");yield return Frames(35);yield return Shot("main-menu");
            Click("album");yield return Frames(35);var album=Object.FindAnyObjectByType<AlbumDiorama>();Assert.NotNull(album?.Atmosphere?.Sunlight);
            album.Atmosphere.Sunlight.Advance(8);yield return Frames(10);yield return Shot("album-morning");
            Click("back");yield return Frames(5);
            Assert.AreEqual(1,Object.FindObjectsByType<CanopySunlight>().Length,"Album left a second illumination owner alive.");
            Assert.AreEqual(AmbientMode.Custom,RenderSettings.ambientMode);
        }
    }
}
