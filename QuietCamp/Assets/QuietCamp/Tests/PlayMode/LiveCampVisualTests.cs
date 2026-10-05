using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using QuietCamp.Application;
using QuietCamp.Infrastructure;
using QuietCamp.Presentation;
using QuietCamp.Presentation.UI;
using QuietCamp.Presentation.World;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
namespace QuietCamp.Tests
{
    public class LiveCampVisualTests
    {
        InputSettings.BackgroundBehavior _background;
        InputSettings.EditorInputBehaviorInPlayMode _editorInput;
        [SetUp] public void SyntheticInput()
        {
            _background=InputSystem.settings.backgroundBehavior;_editorInput=InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        }
        [TearDown] public void RestoreInput()
        {InputSystem.settings.backgroundBehavior=_background;InputSystem.settings.editorInputBehaviorInPlayMode=_editorInput;}
        static readonly BindingFlags Private=BindingFlags.NonPublic|BindingFlags.Static;
        static void Size(int w,int h)=>typeof(ScreenshotPlayModeTest).GetMethod("SetGameViewSize",Private).Invoke(null,new object[]{w,h});
        static IEnumerator Shot(string name)=>(IEnumerator)typeof(ScreenshotPlayModeTest).GetMethod("Shot",Private).Invoke(null,new object[]{Path.Combine(Directory.GetCurrentDirectory(),"Screenshots/LiveCamp"),name});
        static IEnumerator Frames(int n){for(int i=0;i<n;i++)yield return null;}
        static IEnumerator PaletteShot(FoliageDiveTransition dive,string name,Color expected)
        {
            dive.BeginReveal();yield return new WaitForSecondsRealtime(.3f);
            Assert.AreEqual(FoliageDiveTransition.State.Preparing,dive.Current);
            Assert.That(dive.LeafTint.r,Is.EqualTo(expected.r).Within(.01));
            Assert.That(dive.LeafTint.g,Is.EqualTo(expected.g).Within(.01));
            Assert.That(dive.LeafTint.b,Is.EqualTo(expected.b).Within(.01));
            yield return Shot(name);
            var reveal=dive.RevealAsync();while(!reveal.IsCompleted)yield return null;
        }
        static Button Button(string id)=>UnityEngine.Object.FindObjectsByType<Button>().FirstOrDefault(b=>b.name=="<button #"+id+">");
        static void Click(string id){var b=Button(id);Assert.NotNull(b,id);Assert.IsTrue(b.interactable,id);b.onClick.Invoke();}
        static void Layer(GameObject go,int layer){go.layer=layer;foreach(Transform t in go.transform)Layer(t.gameObject,layer);}
        static IEnumerator RenderTentArt(GameServices services)
        {
#if UNITY_EDITOR
            var path="Assets/QuietCamp/Resources/QuietCamp/UI/Tents";Directory.CreateDirectory(path);
            foreach(var asset in new[]{"tent_smallOpen","tent_detailedOpen"})
            {
                var studio=new GameObject("ThumbnailStudio");studio.transform.position=Vector3.down*200;
                var model=UnityEngine.Object.Instantiate(services.Assets.Prefab(asset),studio.transform);Layer(model,31);
                var camera=new GameObject("PreviewCamera").AddComponent<Camera>();camera.transform.SetParent(studio.transform,false);
                camera.transform.localPosition=new Vector3(3,2.5f,3);camera.transform.LookAt(studio.transform.position+Vector3.up*.55f);
                camera.orthographic=true;camera.orthographicSize=1.55f;camera.cullingMask=1<<31;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.clear;
                var texture=new RenderTexture(256,256,16,RenderTextureFormat.ARGB32);texture.Create();camera.targetTexture=texture;
                var light=new GameObject("PreviewSun").AddComponent<Light>();light.transform.SetParent(studio.transform,false);light.type=LightType.Directional;light.intensity=1.1f;light.transform.rotation=Quaternion.Euler(45,-35,0);light.cullingMask=1<<31;
                var previous=RenderSettings.sun;RenderSettings.sun=light;
                yield return Frames(4);
                var old=RenderTexture.active;RenderTexture.active=texture;var pixels=new Texture2D(256,256,TextureFormat.RGBA32,false);pixels.ReadPixels(new Rect(0,0,256,256),0,0);pixels.Apply();RenderTexture.active=old;
                File.WriteAllBytes(Path.Combine(path,asset+".png"),pixels.EncodeToPNG());RenderSettings.sun=previous;
                UnityEngine.Object.Destroy(pixels);camera.targetTexture=null;texture.Release();UnityEngine.Object.Destroy(texture);UnityEngine.Object.Destroy(studio);
            }
            UnityEditor.AssetDatabase.Refresh();yield return Frames(5);
#endif
        }

        [UnityTest, Timeout(600000)] public IEnumerator MenusLanguagesSizesAndAtmosphere()
        {
            if(UnityEngine.Application.isBatchMode)Assert.Ignore("Requires rendered Game View");
            Size(720,1600);yield return SceneManager.LoadSceneAsync("Boot");
            yield return PrivacyBootTestSupport.EnterGame();yield return Frames(120);
            if(SceneManager.GetActiveScene().name!="MainMenu")yield return SceneManager.LoadSceneAsync("MainMenu");
            yield return Frames(60);var s=QuietCampBootstrap.ServicesRef;Assert.NotNull(s);s.Settings.quality=2;
            var album=new[]{"QC003","QC009","gen:qc_camp:18"}.Select(id=>{var l=LevelLoader.Load(id);return new AlbumSaveData.Entry{levelId=id,levelSnapshot=l,contentHash=l.contentHash,placements=l.witness};}).ToArray();
            s.Save.Album.entries=album;
            yield return RenderTentArt(s);
            foreach(var width in new[]{720,1080})foreach(var language in new[]{"uk","en","de"})
            {
                Size(width,width==720?1600:1920);s.Localization.TrySetLanguage(language);s.Settings.textScale=1.3f;
                yield return SceneManager.LoadSceneAsync("MainMenu");yield return Frames(45);
                var suffix=width+"_"+language+"_130";yield return Shot("menu_"+suffix);
                Click("levels");yield return Frames(30);
                Assert.AreEqual(30,UnityEngine.Object.FindObjectsByType<Button>().Count(b=>b.name.StartsWith("<button #level-")));
                var scroll=UnityEngine.Object.FindObjectsByType<MenuMapBinding>().Single().GetComponentInChildren<ScrollRect>();
                Assert.NotNull(scroll);scroll.verticalNormalizedPosition=.27f;yield return Frames(10);yield return Shot("map_"+suffix);
                Click("back");yield return Frames(30);Click("levels");yield return Frames(20);
                scroll=UnityEngine.Object.FindObjectsByType<MenuMapBinding>().Single().GetComponentInChildren<ScrollRect>();Assert.That(scroll.verticalNormalizedPosition,Is.EqualTo(.27f).Within(.025));
                Click("back");yield return Frames(30);Click("album");yield return Frames(40);yield return Shot("album_"+suffix);
                Assert.AreEqual(1,UnityEngine.Object.FindObjectsByType<AlbumDiorama>().Length);
                Click("album-1");yield return Frames(20);Assert.AreEqual(1,SceneManager.GetActiveScene().GetRootGameObjects().Count(g=>g.name=="AlbumDioramaWorld"));
                Click("back");yield return Frames(30);Click("settings");yield return Frames(20);Click("set-cat-comfort");yield return Frames(20);
                yield return Shot("switch_"+suffix);
                foreach(var t in UnityEngine.Object.FindObjectsByType<Toggle>())
                {
                    var knob=(RectTransform)t.transform.Find("Knob");Assert.NotNull(knob);Assert.That(knob.rect.width,Is.EqualTo(knob.rect.height).Within(.5));
                }
                s.PendingLevelId="QC003";yield return SceneManager.LoadSceneAsync("Camp");yield return Frames(35);yield return Shot("hud_"+suffix);
                var pause=Button("pause");var bars=pause.GetComponentsInChildren<RectTransform>().Where(r=>r.name.Contains("pause-bar")).ToArray();
                Assert.AreEqual(2,bars.Length);foreach(var bar in bars)Assert.Greater(bar.rect.height/bar.rect.width,3);
            }
            s.Settings.textScale=1;s.Localization.TrySetLanguage("uk");s.PendingLevelId="QC009";
            yield return SceneManager.LoadSceneAsync("Camp");yield return Frames(60);
            foreach(var quality in new[]{1,2,3})
            {
                s.Settings.quality=quality;yield return Frames(30);
                var forest=UnityEngine.Object.FindObjectsByType<LivingForest>().Single();
                Assert.AreEqual(quality==1?0:quality,forest.FogCount);
                Assert.AreEqual(4,forest.BirdCount,"The pool remains available without visible parked birds.");
                Assert.LessOrEqual(forest.FlyingBirdCount,quality==1?0:quality==2?2:4);
                Assert.AreEqual(quality>1,Camera.main.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>().requiresDepthTexture);
                yield return Shot("camp_quality_"+quality);
            }
            var life=UnityEngine.Object.FindObjectsByType<LivingForest>().Single();life.SendMessage("Gust");yield return Frames(5);yield return Shot("gust");
            var cloth=Shader.Find("QuietCamp/TentCloth");Assert.IsTrue(cloth.isSupported);
            var fog=Shader.Find("QuietCamp/ForestVolume");Assert.IsTrue(fog.isSupported);
            CampSceneHost.Current.Session.DebugApplyWitness();yield return Frames(30);yield return Shot("camp_tents");
            var dive=FoliageDiveTransition.Ensure(s);s.ReducedMotion=false;
            var source=FoliageDiveTransition.CaptureLighting();var task=dive.CoverAsync();yield return Frames(5);yield return Shot("leaves_cover");
            while(!task.IsCompleted)yield return null;Assert.That(dive.LeafTint.r,Is.EqualTo(source.r).Within(.01));
            yield return SceneManager.LoadSceneAsync("MainMenu");yield return Frames(40);
            yield return PaletteShot(dive,"leaves_menu_palette",FoliageDiveTransition.CaptureLighting());
            yield return Frames(10);
            var dark=FoliageDiveTransition.CaptureLighting();task=dive.CoverAsync();while(!task.IsCompleted)yield return null;
            s.PendingLevelId="gen:qc_camp:10";yield return SceneManager.LoadSceneAsync("Camp");yield return Frames(30);
            var bright=FoliageDiveTransition.CaptureLighting();Assert.Greater(bright.grayscale,dark.grayscale+.02f);
            yield return PaletteShot(dive,"leaves_day_palette",bright);
            life=UnityEngine.Object.FindObjectsByType<LivingForest>().Single();typeof(LivingForest).GetField("_clock",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(life,20f);
            life.SendMessage("Gust");yield return Frames(3);yield return Shot("day_fog_rays_birds");
            var placement=UnityEngine.Object.FindObjectsByType<PlacementController>().Single();var session=CampSceneHost.Current.Session;
            var card=Button("guest-card");Assert.NotNull(card);var corners=new Vector3[4];((RectTransform)card.transform).GetWorldCorners(corners);
            var mouse=InputSystem.AddDevice<Mouse>();mouse.MakeCurrent();
            try
            {
                InputSystem.QueueStateEvent(mouse,new MouseState{position=(Vector2)(corners[0]+corners[2])*.5f}.WithButton(MouseButton.Left,true));
                yield return Frames(22);Assert.IsTrue(placement.HasPreview,"Holding the actual UI card must begin a drag");
                var pose=session.Level.witness[0];var at=(Vector2)Camera.main.WorldToScreenPoint(BoardMath.CellCenter(session.Level,pose.x,pose.z));
                InputSystem.QueueStateEvent(mouse,new MouseState{position=at}.WithButton(MouseButton.Left,true));yield return Frames(8);yield return Shot("held_tent_card");
                InputSystem.QueueStateEvent(mouse,new MouseState{position=at}.WithButton(MouseButton.Left,false));yield return Frames(5);
                Assert.AreEqual(1,session.State.Count);Assert.IsTrue(session.Undo());Assert.AreEqual(0,session.State.Count);Assert.IsFalse(session.Undo());Assert.IsTrue(session.Redo());
            }
            finally{placement.Cancel();InputSystem.RemoveDevice(mouse);}
            session.DebugApplyWitness();yield return Frames(20);yield return Shot("day_tents");
        }

        [UnityTest] public IEnumerator TransitionCapturesBothPalettesUnderFullCover()
        {
            if(UnityEngine.Application.isBatchMode)Assert.Ignore("Requires rendered Game View");
            Size(1080,1920);yield return SceneManager.LoadSceneAsync("Boot");
            yield return PrivacyBootTestSupport.EnterGame();yield return Frames(120);
            if(SceneManager.GetActiveScene().name!="MainMenu")yield return SceneManager.LoadSceneAsync("MainMenu");
            yield return Frames(40);var s=QuietCampBootstrap.ServicesRef;s.Settings.quality=3;s.ReducedMotion=false;
            var dive=FoliageDiveTransition.Ensure(s);var dark=FoliageDiveTransition.CaptureLighting();
            var cover=dive.CoverAsync();while(!cover.IsCompleted)yield return null;
            s.PendingLevelId="gen:qc_camp:10";yield return SceneManager.LoadSceneAsync("Camp");yield return Frames(30);
            var bright=FoliageDiveTransition.CaptureLighting();Assert.Greater(bright.grayscale,dark.grayscale+.02f);
            yield return PaletteShot(dive,"leaves_day_palette",bright);
            cover=dive.CoverAsync();while(!cover.IsCompleted)yield return null;
            yield return SceneManager.LoadSceneAsync("MainMenu");yield return Frames(40);
            yield return PaletteShot(dive,"leaves_menu_palette",FoliageDiveTransition.CaptureLighting());
            Assert.IsTrue(dive.IsIdle);
        }
    }
}
