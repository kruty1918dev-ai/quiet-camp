using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using QuietCamp.Application;
using QuietCamp.Presentation;
using QuietCamp.Presentation.World;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

namespace QuietCamp.Tests
{
    public sealed class PuddlesPlayModeTests
    {
#if UNITY_EDITOR
        bool _asyncCompilation;
        [NUnit.Framework.SetUp] public void UseFinishedShadersForVisualChecks()
        {
            _asyncCompilation=UnityEditor.ShaderUtil.allowAsyncCompilation;
            UnityEditor.ShaderUtil.allowAsyncCompilation=false;
        }
        [NUnit.Framework.TearDown] public void RestoreShaderCompilation()
            => UnityEditor.ShaderUtil.allowAsyncCompilation=_asyncCompilation;
#endif
        static IEnumerator Frames(int n){while(n-->0)yield return null;}
        static IEnumerator Shot(string name)
        {
            var folder=Path.Combine(Directory.GetCurrentDirectory(),"Screenshots/Puddles");
            yield return (IEnumerator)typeof(ScreenshotPlayModeTest).GetMethod("Shot",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{folder,name});
            var texture=new Texture2D(2,2);
            try
            {
                texture.LoadImage(File.ReadAllBytes(Path.Combine(folder,name+".png")));
                int cyan=texture.GetPixels32().Count(p=>p.r<20&&p.g>235&&p.b>235);
                Assert.Less(cyan,32,"Editor rendered its unfinished cyan shader placeholder: "+name);
            }
            finally{Object.Destroy(texture);}
        }
        [UnityTest] public IEnumerator RainFormsReflectingWaterAndDryWeatherEvaporatesItWithoutChangingRules()
        {
            if(UnityEngine.Application.isBatchMode)Assert.Ignore("Requires graphics");
            typeof(ScreenshotPlayModeTest).GetMethod("SetGameViewSize",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{1280,800});
            yield return SceneManager.LoadSceneAsync("Boot");
            yield return PrivacyBootTestSupport.EnterGame();var deadline=Time.realtimeSinceStartup+25;
            while(Object.FindAnyObjectByType<QuietCampBootstrap>()?.StartupReady!=true&&Time.realtimeSinceStartup<deadline)yield return null;
            var s=QuietCampBootstrap.ServicesRef;s.ReducedMotion=true;s.Settings.quality=2;s.PendingLevelId="QC003";s.Save.Session=new SessionSaveData();
            yield return SceneManager.LoadSceneAsync("Camp");yield return Frames(40);
            var host=CampSceneHost.Current;host.Session.DebugApplyWitness();host.Session.Select(null);yield return Frames(8);
            var water=host.Atmosphere.Puddles;Assert.NotNull(water);Assert.Greater(water.Centres.Count,10);
            var renderer=water.GetComponent<MeshRenderer>();var mesh=water.GetComponent<MeshFilter>().sharedMesh;
            Assert.IsEmpty(water.GetComponentsInChildren<Collider>());Assert.AreEqual(ShadowCastingMode.Off,renderer.shadowCastingMode);
            var content=JsonUtility.ToJson(host.Session.Level);var layout=JsonUtility.ToJson(s.Save.Session);
            foreach(var p in water.Centres)
            {Assert.IsTrue(Mathf.Abs(p.x)>host.Session.Level.width*.5f+.5f||Mathf.Abs(p.z)>host.Session.Level.height*.5f+.5f);Assert.IsFalse(CampTrail.IsCorridor(host.Session.Level,p,.28f));}
            float start=water.Amount;var weather=host.Atmosphere.Weather;
            for(int second=0;second<150&&weather.RainAmount<.3f;second++)weather.Advance(1,1,true,Vector2.zero);
            Assert.Greater(weather.RainAmount,.3f);water.Advance(2);Assert.Greater(water.Amount,start);Assert.Less(water.Amount,.3f,"Puddles appeared all at once");
            Assert.That(renderer.bounds.min.y,Is.EqualTo(MeadowSurface.GroundY+.003f).Within(.0001f),"Water floats above the meadow");
            yield return Shot("rain_water_forming");
            water.Advance(12);yield return Frames(40);water.Advance(0);Assert.IsTrue(water.HasWorldReflection,"Probe never completed");
            var probe=water.GetComponentInChildren<ReflectionProbe>();Assert.NotNull(probe);Assert.AreEqual(32,probe.resolution);
            Assert.AreEqual(ReflectionProbeRefreshMode.ViaScripting,probe.refreshMode);Assert.AreEqual(ReflectionProbeTimeSlicingMode.IndividualFaces,probe.timeSlicingMode);
            Assert.AreEqual(0,probe.cullingMask&((1<<4)|(1<<BoardRenderer.RuleOverlayLayer)|(1<<0)),"Capture includes water/UI/effect layers");
            int captures=water.CaptureCount;for(int i=0;i<5;i++)water.Advance(.05f);Assert.AreEqual(captures,water.CaptureCount,"Reflections render every frame");
            var camera=Camera.main;var oldTarget=camera.targetTexture;var target=new RenderTexture(640,400,24);
            Texture2D reflected=null,skyOnly=null;
            try
            {
                camera.targetTexture=target;
                Texture2D Capture(string name)
                {
                    camera.Render();var old=RenderTexture.active;RenderTexture.active=target;
                    var texture=new Texture2D(640,400,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,640,400),0,0);texture.Apply();RenderTexture.active=old;
                    var folder=Path.Combine(Directory.GetCurrentDirectory(),"Screenshots/Puddles");Directory.CreateDirectory(folder);File.WriteAllBytes(Path.Combine(folder,name+".png"),texture.EncodeToPNG());return texture;
                }
                reflected=Capture("world_reflection_on");renderer.sharedMaterial.SetFloat("_HasReflection",0);skyOnly=Capture("sky_only");
                int changed=reflected.GetPixels().Zip(skyOnly.GetPixels(),(a,b)=>Mathf.Abs(a.r-b.r)+Mathf.Abs(a.g-b.g)+Mathf.Abs(a.b-b.b)>.015f?1:0).Sum();
                Assert.Greater(changed,20,"Actual puddle pixels never sample the world's reflection");Debug.Log("[PuddlesQA] reflected world pixels="+changed);
            }
            finally{renderer.sharedMaterial.SetFloat("_HasReflection",1);camera.targetTexture=oldTarget;target.Release();Object.Destroy(target);if(reflected!=null)Object.Destroy(reflected);if(skyOnly!=null)Object.Destroy(skyOnly);}
            yield return Shot("rain_reflections_balanced");var daylight=water.SkyColor;
            host.SetAtmospherePhase("night");water.Advance(.05f);
            Assert.IsFalse(water.HasWorldReflection,"Day capture remains visible after the sky changed");
            water.Advance(16);yield return Frames(40);water.Advance(0);
            Assert.Less(water.SkyColor.grayscale,daylight.grayscale*.75f);Assert.IsTrue(water.HasWorldReflection);
            yield return Shot("rain_reflections_night");
            s.Settings.quality=1;yield return Frames(5);water.Advance(0);Assert.IsFalse(water.HasWorldReflection);
            Assert.IsTrue(probe==null,"Low retains the real-time reflection resources");
            Assert.IsNull(water.GetComponentInChildren<ReflectionProbe>());
            yield return Shot("rain_sky_low");
            s.Settings.quality=3;yield return Frames(5);water.Advance(16);yield return Frames(40);water.Advance(0);
            probe=water.GetComponentInChildren<ReflectionProbe>();Assert.NotNull(probe);Assert.AreEqual(64,probe.resolution);
            yield return Shot("rain_reflections_high");
            float wet=water.Amount;
            for(int second=0;second<150&&weather.RainAmount>.05f;second++)weather.Advance(1,2,true,Vector2.zero);
            Assert.Less(weather.RainAmount,.05f);water.Advance(8);Assert.Less(water.Amount,wet);Assert.Greater(water.Amount,0,"Water disappeared immediately");
            water.Advance(120);Assert.AreEqual(0,water.Amount);Assert.IsFalse(renderer.enabled);
            Assert.AreEqual(content,JsonUtility.ToJson(host.Session.Level));Assert.AreEqual(layout,JsonUtility.ToJson(s.Save.Session));
            var material=renderer.sharedMaterial;Object.Destroy(water.gameObject);yield return Frames(2);
            Assert.IsTrue(mesh==null);Assert.IsTrue(material==null);Assert.IsTrue(probe==null);s.ReducedMotion=false;
        }
        [UnityTest] public IEnumerator AllDayPhasesShareWeatherSkyAndOffscreenWaterDoesNotCapture()
        {
            if(UnityEngine.Application.isBatchMode)Assert.Ignore("Requires graphics");
            typeof(ScreenshotPlayModeTest).GetMethod("SetGameViewSize",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{720,1600});
            yield return SceneManager.LoadSceneAsync("Boot");
            yield return PrivacyBootTestSupport.EnterGame();var deadline=Time.realtimeSinceStartup+25;
            while(Object.FindAnyObjectByType<QuietCampBootstrap>()?.StartupReady!=true&&Time.realtimeSinceStartup<deadline)yield return null;
            var services=QuietCampBootstrap.ServicesRef;services.ReducedMotion=true;services.Settings.quality=1;
            services.PendingLevelId="QC003";services.Save.Session=new SessionSaveData();
            yield return SceneManager.LoadSceneAsync("Camp");yield return Frames(40);
            var host=CampSceneHost.Current;var water=host.Atmosphere.Puddles;var weather=host.Atmosphere.Weather;
            var material=water.GetComponent<MeshRenderer>().sharedMaterial;var before=JsonUtility.ToJson(services.Save.Session);
            Color day=default;
            foreach(var phase in new[]{"morning","noon","evening","night"})
            {
                host.SetAtmospherePhase(phase);water.Advance(16);
                var sky=RenderSettings.skybox;Assert.NotNull(sky);Assert.AreEqual("QuietCamp/CozySky",sky.shader.name);
                Assert.Less(((Vector4)(material.GetColor("_Sky")-sky.GetColor("_Zenith"))).magnitude,.001f);
                Assert.Less(((Vector4)(material.GetColor("_Horizon")-sky.GetColor("_Horizon"))).magnitude,.001f);
                Assert.AreEqual(sky.GetFloat("_Cloud"),material.GetFloat("_Cloud"));
                Assert.AreEqual(sky.GetVector("_SunDirection"),material.GetVector("_SunDirection"));
                if(phase=="noon")day=water.SkyColor;
                if(phase=="night")Assert.Less(water.SkyColor.grayscale,day.grayscale*.55f);
            }
            host.SetAtmospherePhase("noon");
            for(int i=0;i<150&&weather.RainAmount<.3f;i++)weather.Advance(1,0,true,Vector2.zero);
            Assert.Greater(weather.RainAmount,.3f);water.Advance(16);yield return Frames(5);
            Assert.Greater(water.Amount,.1f);Assert.Greater(water.VisiblePuddleCount,0);
            Assert.IsNull(water.GetComponentInChildren<ReflectionProbe>());
            Assert.Greater(material.GetFloat("_Cloud"),.3f);Assert.Less(water.SkyColor.b,day.b);
            yield return Shot("phone_overcast_sky_low");
            var camera=Camera.main;var pose=camera.transform.position;
            camera.transform.position+=Vector3.one*1000;water.Advance(0);
            Assert.AreEqual(0,water.VisiblePuddleCount);Assert.IsFalse(water.GetComponent<MeshRenderer>().enabled);
            int count=water.CaptureCount;services.Settings.quality=2;yield return Frames(5);water.Advance(20);
            Assert.AreEqual(count,water.CaptureCount);Assert.IsNull(water.GetComponentInChildren<ReflectionProbe>());
            camera.transform.position=pose;water.Advance(0);yield return Frames(40);water.Advance(0);
            Assert.Greater(water.CaptureCount,count);Assert.IsTrue(water.HasWorldReflection);
            yield return Shot("phone_rain_reflections_balanced");
            Assert.AreEqual(before,JsonUtility.ToJson(services.Save.Session));
            Debug.Log("[PuddlesQA] hidden capture count="+count+" visible capture count="+water.CaptureCount+" visible puddles="+water.VisiblePuddleCount);
            services.ReducedMotion=false;
        }
    }
}
