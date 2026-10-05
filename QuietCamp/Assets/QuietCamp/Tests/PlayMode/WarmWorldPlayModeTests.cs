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
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace QuietCamp.Tests
{
    public class WarmWorldPlayModeTests
    {
        static readonly BindingFlags Private=BindingFlags.NonPublic|BindingFlags.Static;
        static IEnumerator Frames(int n){for(int i=0;i<n;i++)yield return null;}
        static void Size(int width,int height)=>typeof(ScreenshotPlayModeTest).GetMethod("SetGameViewSize",Private).Invoke(null,new object[]{width,height});
        static IEnumerator Shot(string name)=>(IEnumerator)typeof(ScreenshotPlayModeTest).GetMethod("Shot",Private).Invoke(null,new object[]{Path.Combine(Directory.GetCurrentDirectory(),"Screenshots/WarmWorld"),name});
        static void Click(string id){var b=Object.FindObjectsByType<Button>().FirstOrDefault(b=>b.name=="<button #"+id+">");Assert.NotNull(b,id);b.onClick.Invoke();}
        static IEnumerator Camp()
        {
            yield return SceneManager.LoadSceneAsync("Boot");
            yield return PrivacyBootTestSupport.EnterGame();yield return Frames(60);
            QuietCampBootstrap.ServicesRef.PendingLevelId="QC010";
            yield return SceneManager.LoadSceneAsync("Camp");yield return Frames(45);
        }

        [UnityTest] public IEnumerator BirdsNeverParkInAirAndWeatherHonorsBudgets()
        {
            yield return Camp();var services=QuietCampBootstrap.ServicesRef;services.Settings.quality=3;services.ReducedMotion=false;yield return Frames(3);
            var forest=Object.FindAnyObjectByType<LivingForest>();Assert.NotNull(forest);
            Assert.AreEqual(0,forest.FlyingBirdCount,"A new pool must wait before its first flight.");
            foreach(var root in forest.GetComponentsInChildren<Transform>(true).Where(t=>t.name=="ForestBird"))
                foreach(var renderer in root.GetComponentsInChildren<Renderer>(true))Assert.AreEqual(ShadowCastingMode.Off,renderer.shadowCastingMode);
            forest.AdvanceBirds(8,false);
            yield return Frames(1);
            var flying=forest.GetComponentsInChildren<Transform>().FirstOrDefault(t=>t.name=="ForestBird");
            Assert.NotNull(flying);var before=flying.position;yield return Frames(4);Assert.Greater((flying.position-before).sqrMagnitude,.00001f,"A visible bird must keep flying.");
            services.ReducedMotion=true;yield return Frames(2);Assert.AreEqual(0,forest.FlyingBirdCount);
            var weather=forest.GetComponent<CampWeather>();Assert.NotNull(weather);
            weather.Advance(72,1,false,Vector2.right);Assert.Greater(weather.Cloudiness,.5f);Assert.Greater(weather.RainAmount,.2f);
            weather.Advance(0,0,false,Vector2.right);Assert.AreEqual(12,weather.ParticleBudget);
            weather.Advance(0,1,true,Vector2.right);Assert.AreEqual(0,weather.ParticleBudget);
            Assert.AreEqual(0,weather.GetComponentsInChildren<ParticleSystem>().Where(p=>p.name=="GentleRain").Sum(p=>p.particleCount));
            services.ReducedMotion=false;
        }

        [UnityTest] public IEnumerator AlbumRapidSwitchesSettleOnLastChoiceAndBackCleansUp()
        {
            yield return SceneManager.LoadSceneAsync("Boot");
            yield return PrivacyBootTestSupport.EnterGame();yield return Frames(60);
            var s=QuietCampBootstrap.ServicesRef;s.ReducedMotion=false;s.Settings.quality=2;
            s.Save.Album.entries=new[]{"QC001","QC009","QC010"}.Select(id=>{var l=LevelLoader.Load(id);return new AlbumSaveData.Entry{levelId=id,levelSnapshot=l,contentHash=l.contentHash,placements=l.witness};}).ToArray();
            s.AlbumIndex=0;yield return SceneManager.LoadSceneAsync("MainMenu");yield return Frames(40);
            Click("album");yield return Frames(25);var album=Object.FindAnyObjectByType<AlbumDiorama>();Assert.NotNull(album);
            Click("album-1");yield return Frames(2);Assert.IsTrue(album.IsSwitching);Assert.AreEqual(0,album.DisplayedIndex,"Previous diorama stays until the veil covers it.");
            Click("album-2");
            float deadline=Time.realtimeSinceStartup+5;while(album.IsSwitching&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.IsFalse(album.IsSwitching);Assert.AreEqual(2,album.DisplayedIndex);
            Assert.AreEqual(1,SceneManager.GetActiveScene().GetRootGameObjects().Count(g=>g.name=="AlbumDioramaWorld"));
            Click("album-0");yield return Frames(2);Click("back");yield return new WaitForSecondsRealtime(.6f);
            Assert.IsFalse(album.IsSwitching);Assert.AreEqual(0,SceneManager.GetActiveScene().GetRootGameObjects().Count(g=>g.name=="AlbumDioramaWorld"));
        }

        [UnityTest] public IEnumerator WarmLightWeatherAndBeamRenderWithoutMagenta()
        {
            if(UnityEngine.Application.isBatchMode)Assert.Ignore("Needs rendered Game View.");
            Size(1080,1920);yield return Camp();var s=QuietCampBootstrap.ServicesRef;s.Settings.quality=3;s.ReducedMotion=false;yield return Frames(15);
            var forest=Object.FindAnyObjectByType<LivingForest>();
            typeof(LivingForest).GetField("_clock",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(forest,21f);
            yield return Frames(3);yield return Shot("sunshine_beam");
            var path=Path.Combine(Directory.GetCurrentDirectory(),"Screenshots/WarmWorld/sunshine_beam.png");var texture=new Texture2D(2,2);texture.LoadImage(File.ReadAllBytes(path));
            int magenta=texture.GetPixels32().Count(p=>p.r>180&&p.b>180&&p.g<80);Object.Destroy(texture);
            Assert.Less(magenta,10,"Even a thin unsupported beam must fail the visual check.");
            var weather=forest.GetComponent<CampWeather>();weather.Advance(72,2,false,Vector2.right);yield return Frames(30);yield return Shot("warm_drizzle");
            Size(720,1600);s.Settings.quality=1;yield return Frames(20);yield return Shot("low_weather");
            s.Settings.quality=2;yield return Frames(20);yield return Shot("balanced_weather");
        }
    }
}
