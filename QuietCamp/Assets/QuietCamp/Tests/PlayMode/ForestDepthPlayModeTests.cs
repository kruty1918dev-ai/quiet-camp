using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using QuietCamp.Application;
using QuietCamp.Presentation;
using QuietCamp.Presentation.World;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

namespace QuietCamp.Tests
{
    public sealed class ForestDepthPlayModeTests
    {
        static IEnumerator Frames(int n){while(n-->0)yield return null;}
        static void Size(int w,int h)=>typeof(ScreenshotPlayModeTest).GetMethod("SetGameViewSize",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{w,h});
        static IEnumerator Shot(string name)=>(IEnumerator)typeof(ScreenshotPlayModeTest).GetMethod("Shot",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{Path.Combine(Directory.GetCurrentDirectory(),"Screenshots/ForestDepth"),name});
        [UnityTest] public IEnumerator ForestDepthAdaptsToPhaseRainViewportAndQualityWithoutChangingPlay()
        {
            if(UnityEngine.Application.isBatchMode)Assert.Ignore("Requires rendered Game View");
            Size(720,1600);yield return SceneManager.LoadSceneAsync("Boot");
            yield return PrivacyBootTestSupport.EnterGame();
            var deadline=Time.realtimeSinceStartup+25;
            while(Object.FindAnyObjectByType<QuietCampBootstrap>()?.StartupReady!=true&&Time.realtimeSinceStartup<deadline)yield return null;
            var s=QuietCampBootstrap.ServicesRef;Assert.NotNull(s);s.PendingLevelId="QC009";s.ReducedMotion=true;
            s.Save.Session=new SessionSaveData();s.Settings.quality=2;
            yield return SceneManager.LoadSceneAsync("Camp");yield return Frames(40);
            var host=CampSceneHost.Current;host.Session.DebugApplyWitness();host.Session.Select(null);yield return Frames(8);
            var edge=host.Atmosphere.Edges;Assert.NotNull(edge);
            string content=JsonUtility.ToJson(host.Session.Level),layout=JsonUtility.ToJson(s.Save.Session);
            Assert.IsEmpty(edge.GetComponentsInChildren<Collider>(true));Assert.IsEmpty(edge.GetComponentsInChildren<Light>(true));Assert.IsEmpty(edge.GetComponentsInChildren<Camera>(true));
            var resources=edge.GetComponentsInChildren<MeshFilter>(true).Select(f=>f.sharedMesh).ToArray();
            var materials=edge.GetComponentsInChildren<Renderer>(true).Select(r=>r.sharedMaterial).ToArray();
            foreach(var size in new[]{new Vector2Int(720,1600),new Vector2Int(1280,800)})
            {
                Size(size.x,size.y);yield return Frames(16);
                foreach(string phase in new[]{"noon","morning","evening","night"})
                {
                    host.SetAtmospherePhase(phase);edge.Advance(12);yield return Frames(20);
                    foreach(int quality in new[]{1,2,3})
                    {
                        s.Settings.quality=quality;yield return Frames(6);edge.Advance(0);
                        Assert.IsTrue(edge.UsesVolume,"Every profile retains world-space forest depth");
                        Assert.AreEqual(1,edge.GetComponentsInChildren<Renderer>().Length,"Unexpected overlapping fog draws");
                        Assert.LessOrEqual(edge.Density,.50f);Assert.Greater(edge.Density,.025f);
                        var beforePose=Camera.main.transform.position;var beforeSize=Camera.main.orthographicSize;
                        edge.Advance(1);Assert.AreEqual(beforePose,Camera.main.transform.position);Assert.AreEqual(beforeSize,Camera.main.orthographicSize);
                        yield return Shot(phase+"_"+size.x+"x"+size.y+"_q"+quality);
                    }
                    Assert.AreEqual(host.Session.Level.width*.5f+ForestEdgeAtmosphere.ClearMargin,edge.ClearHalfSize.x,.001f);
                    Assert.AreEqual(host.Session.Level.height*.5f+ForestEdgeAtmosphere.ClearMargin,edge.ClearHalfSize.y,.001f);
                }
            }
            s.Settings.quality=2;host.SetAtmospherePhase("night");edge.Advance(12);var night=edge.Tint;
            host.SetAtmospherePhase("morning");var immediate=edge.Tint;Assert.AreEqual(night,immediate,"Phase change flashed the haze");
            edge.Advance(12);Assert.Greater(edge.Tint.grayscale,night.grayscale+.20f);
            host.SetAtmospherePhase("noon");edge.Advance(12);float dry=edge.Density;
            // Compare actual camera pixels with/without the world volume:
            // distant forest changes, the protected centre stays readable.
            var camera=Camera.main;var oldTarget=camera.targetTexture;
            var target=new RenderTexture(512,320,24);Texture2D on=null,off=null;
            try
            {
                camera.targetTexture=target;
                Texture2D Capture(string name)
                {
                    camera.Render();var old=RenderTexture.active;RenderTexture.active=target;
                    var texture=new Texture2D(512,320,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,512,320),0,0);texture.Apply();RenderTexture.active=old;
                    var folder=Path.Combine(Directory.GetCurrentDirectory(),"Screenshots/ForestDepth");Directory.CreateDirectory(folder);
                    File.WriteAllBytes(Path.Combine(folder,name+".png"),texture.EncodeToPNG());return texture;
                }
                on=Capture("volume_on");edge.gameObject.SetActive(false);off=Capture("volume_off");
                int changed=on.GetPixels().Zip(off.GetPixels(),(a,b)=>Mathf.Abs(a.r-b.r)+Mathf.Abs(a.g-b.g)+Mathf.Abs(a.b-b.b)>.035f?1:0).Sum();
                Assert.Greater(changed,100,"Forest volume exists but has no visible depth effect");
                var centre=camera.WorldToViewportPoint(Vector3.zero);float centreDifference=0;
                int px=Mathf.RoundToInt(centre.x*512),py=Mathf.RoundToInt(centre.y*320);
                for(int y=-4;y<=4;y++)for(int x=-4;x<=4;x++)
                {var a=on.GetPixel(px+x,py+y);var b=off.GetPixel(px+x,py+y);centreDifference+=Mathf.Abs(a.r-b.r)+Mathf.Abs(a.g-b.g)+Mathf.Abs(a.b-b.b);}
                Assert.Less(centreDifference/(81*3),.008f,"Fog obscures the centre of the puzzle");
                Debug.Log("[ForestDepthQA] changed forest pixels="+changed+" centre difference="+centreDifference/(81*3));
            }
            finally{edge.gameObject.SetActive(true);camera.targetTexture=oldTarget;target.Release();Object.Destroy(target);if(on!=null)Object.Destroy(on);if(off!=null)Object.Destroy(off);}
            var weather=host.Atmosphere.Weather;weather.Advance(20,1,true,Vector2.zero);
            edge.Advance(12);Assert.GreaterOrEqual(edge.Density,.06f);
            Assert.That(weather.RainAmount,Is.InRange(0,1));
            yield return Shot("rain_haze");
            float clock=materials[0].GetFloat("_LightTime");edge.Advance(3);Assert.AreEqual(clock,materials[0].GetFloat("_LightTime"),"Reduced motion keeps fog advection running");
            Assert.AreEqual(content,JsonUtility.ToJson(host.Session.Level));Assert.AreEqual(layout,JsonUtility.ToJson(s.Save.Session));
            Object.Destroy(edge.gameObject);yield return Frames(2);Assert.IsTrue(resources.All(m=>m==null));Assert.IsTrue(materials.All(m=>m==null));
            s.ReducedMotion=false;
        }
    }
}
