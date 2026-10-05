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
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace QuietCamp.Tests
{
    public sealed class ResponsiveUiPlayModeTests
    {
        const BindingFlags Hidden = BindingFlags.NonPublic | BindingFlags.Static;
        static IEnumerator Frames(int n) { for (var i=0;i<n;i++) yield return null; }
        static void Size(int w,int h) => typeof(ScreenshotPlayModeTest).GetMethod("SetGameViewSize",Hidden).Invoke(null,new object[]{w,h});
        static IEnumerator Shot(string name) => (IEnumerator)typeof(ScreenshotPlayModeTest).GetMethod("Shot",Hidden).Invoke(null,new object[]{Path.Combine(Directory.GetCurrentDirectory(),"Screenshots/ResponsiveUi"),name});
        static Button Button(string id) => Object.FindObjectsByType<Button>().FirstOrDefault(b=>b.name=="<button #"+id+">");
        static void Tap(string id)
        {
            Canvas.ForceUpdateCanvases();
            var button = Button(id); Assert.NotNull(button,id); Assert.IsTrue(button.interactable,id);
            var rect=(RectTransform)button.transform;
            var pointer=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center))};
            var hits=new System.Collections.Generic.List<RaycastResult>();EventSystem.current.RaycastAll(pointer,hits);
            Assert.IsTrue(hits.Count>0&&(hits[0].gameObject.transform==rect||hits[0].gameObject.transform.IsChildOf(rect)),"Covered control: "+id);
            Assert.Greater(pointer.position.x,0);Assert.Less(pointer.position.x,Screen.width);
            Assert.Greater(pointer.position.y,0);Assert.Less(pointer.position.y,Screen.height);
            ExecuteEvents.Execute(button.gameObject,pointer,ExecuteEvents.pointerClickHandler);
        }
        static IEnumerator Boot()
        {
            yield return SceneManager.LoadSceneAsync("Boot");
            yield return PrivacyBootTestSupport.EnterGame(); yield return Frames(120);
            if(SceneManager.GetActiveScene().name!="MainMenu")yield return SceneManager.LoadSceneAsync("MainMenu");
            yield return Frames(40);
        }
        [UnityTest] public IEnumerator MenusSettingsMapAndAlbumAcrossWindowShapes()
        {
            if(UnityEngine.Application.isBatchMode)Assert.Ignore("Requires rendered Game View");
            Size(720,1600);yield return Boot();var services=QuietCampBootstrap.ServicesRef;
            services.Tutorial.Skip(); // This scenario verifies every menu and settings category.
            services.ReducedMotion=true;services.Settings.textScale=1.3f;
            services.Save.Album.entries=new[]{"QC003","QC009","gen:qc_camp:18"}.Select(id=>{var l=LevelLoader.Load(id);return new AlbumSaveData.Entry{levelId=id,levelSnapshot=l,contentHash=l.contentHash,placements=l.witness};}).ToArray();
            var sizes=new[]{new Vector2Int(720,1600),new Vector2Int(1080,1920),new Vector2Int(1600,2560),new Vector2Int(2560,1600),new Vector2Int(2048,1536),new Vector2Int(2560,1080),new Vector2Int(1024,768),new Vector2Int(1600,720)};
            foreach(var size in sizes)
            {
                Size(size.x,size.y);yield return Frames(12);
                foreach(var language in new[]{"uk","en","de"})
                {
                    services.Localization.TrySetLanguage(language);
                    foreach(var item in Object.FindObjectsByType<HtmlSurface>())item.Refresh();
                    yield return Frames(12);
                    Assert.NotNull(Button("continue"));Assert.NotNull(Button("levels"));Assert.NotNull(Button("album"));
                    // Game View applies requested sizes asynchronously. Verify a composed
                    // frame before testing hits after resize, rather than assuming that
                    // twelve simulation frames also produced the new editor viewport.
                    if(language=="uk"||language=="de")yield return Shot("menu_"+language+"130_"+size.x+"x"+size.y);
                    Tap("settings");yield return Frames(10);Tap("set-cat-comfort");yield return Frames(10);
                    var surface=Object.FindObjectsByType<HtmlSurface>().First(s=>s.name=="MenuOverlay");
                    var navigation=surface.Element("settings-nav-scroll");
                    if(navigation!=null&&navigation.gameObject.activeInHierarchy&&((RectTransform)navigation.transform).rect.width>100)
                    foreach(var category in new[]{"sound","comfort","look","extras","privacy"})
                    {
                        var title=Button("set-cat-"+category).GetComponentsInChildren<TMPro.TMP_Text>()
                            .First(t=>t.text==services.Localization.T("settings.cat."+category));
                        title.ForceMeshUpdate();
                        Assert.LessOrEqual(title.textInfo.lineCount,2,"Sidebar splits a category into isolated letters: "+title.text);
                    }
                    var switches=surface.GetComponentsInChildren<Toggle>();Assert.GreaterOrEqual(switches.Length,3);
                    foreach(var toggle in switches)
                    {
                        var r=(RectTransform)toggle.transform;Assert.Greater(r.rect.width,r.rect.height*1.5f,"Distorted switch");
                    }
                    if(language=="de")yield return Shot("settings_de130_"+size.x+"x"+size.y);
                    Tap("back");yield return Frames(8);Tap("back");yield return Frames(8);
                }
            }
            Size(2560,1600);yield return Frames(12);Tap("levels");yield return Frames(15);
            var overlay=Object.FindObjectsByType<HtmlSurface>().First(s=>s.name=="MenuOverlay");
            var scroll=overlay.Element("roadmap-scroll").GetComponent<ScrollRect>();Assert.NotNull(scroll);
            Assert.AreEqual(30,overlay.GetComponentsInChildren<Button>(true).Count(b=>b.name.StartsWith("<button #level-")));
            foreach(var id in new[]{"bonus-10","bonus-20","bonus-30"})
            {
                var bonus=Button(id);Assert.NotNull(bonus);
                var title=bonus.GetComponentsInChildren<TMPro.TMP_Text>().First(t=>t.text.Contains(id=="bonus-10"?"Taufrisches":id=="bonus-20"?"Bernstein":"Glühwürmchen"));
                title.ForceMeshUpdate();Assert.LessOrEqual(title.textInfo.lineCount,2,"Bonus title is fragmented: "+title.text);
                if(id=="bonus-20"&&title.textInfo.lineCount>1)
                {
                    var line=title.textInfo.lineInfo[0];
                    var word=new string(title.textInfo.characterInfo.Skip(line.firstCharacterIndex)
                        .Take(line.lastCharacterIndex-line.firstCharacterIndex+1).Select(c=>c.character).ToArray());
                    Assert.AreEqual("Bernstein",word.Trim().TrimEnd('-', '\u00ad'),"Compound words must wrap at the authored boundary.");
                }
            }
            scroll.verticalNormalizedPosition=.35f;scroll.velocity=Vector2.zero;yield return Frames(3);
            Size(1600,2560);yield return Frames(12);Assert.AreEqual(.35f,scroll.verticalNormalizedPosition,.02f);
            yield return Shot("map_tablet_portrait_de130");
            Tap("back");yield return Frames(12);Tap("levels");yield return Frames(12);
            scroll=overlay.Element("roadmap-scroll").GetComponent<ScrollRect>();Assert.AreEqual(.35f,scroll.verticalNormalizedPosition,.02f);
            Tap("back");yield return Frames(12);Size(2560,1600);yield return Frames(12);Tap("album");yield return Frames(50);
            yield return Shot("album_tablet_landscape_de130");Tap("album-1");yield return Frames(40);
            Assert.AreEqual(1,SceneManager.GetActiveScene().GetRootGameObjects().Count(g=>g.name=="AlbumDioramaWorld"));
            Tap("back");yield return Frames(12);
            services.Settings.textScale=1f;services.Localization.TrySetLanguage("uk");
        }
        [UnityTest] public IEnumerator GameplayResizesInPlaceAndKeepsBoardAwayFromDock()
        {
            if(UnityEngine.Application.isBatchMode)Assert.Ignore("Requires rendered Game View");
            Size(720,1600);yield return Boot();var services=QuietCampBootstrap.ServicesRef;
            services.Tutorial.Skip(); // This scenario verifies every menu and settings category.
            services.ReducedMotion=true;services.Settings.textScale=1.3f;services.Localization.TrySetLanguage("de");services.PendingLevelId="QC_TEST";
            yield return SceneManager.LoadSceneAsync("Camp");yield return Frames(45);
            var session=CampSceneHost.Current.Session;session.DebugApplyWitness();yield return Frames(10);
            var hud=Object.FindObjectsByType<HtmlSurface>().First(s=>s.name=="CampHtml");
            foreach(var size in new[]{new Vector2Int(720,1600),new Vector2Int(2560,1600),new Vector2Int(2048,1536),new Vector2Int(1024,768),new Vector2Int(1600,720),new Vector2Int(1080,1920)})
            {
                var beforePlacements = session.State.Placements.Select(p => p.Copy()).ToArray();
                var pauseBefore=Button("pause");Size(size.x,size.y);yield return Frames(18);
                Assert.AreSame(session,CampSceneHost.Current.Session);Assert.AreSame(pauseBefore,Button("pause"),"Resize remounted HUD");
                Assert.AreEqual(beforePlacements.Length, session.State.Placements.Count);
                for (var i=0; i<beforePlacements.Length; i++)
                {
                    var before=beforePlacements[i]; var after=session.State.Placements[i];
                    Assert.AreEqual(before.guestId, after.guestId); Assert.AreEqual(before.x, after.x);
                    Assert.AreEqual(before.z, after.z); Assert.AreEqual(before.rotation, after.rotation);
                }
                var cues = Object.FindObjectsByType<Kruty1918.GameplayViewport.ViewportVisualScale>();
                Assert.AreEqual(session.State.Placements.Count, cues.Length);
                foreach (var cue in cues)
                {
                    Assert.NotNull(cue.Viewport); Assert.AreEqual("DoorDot", cue.Visual.name);
                    Assert.That(cue.Visual.localScale.x, Is.InRange(.1399f, .2381f));
                    Assert.AreEqual(Vector3.one, cue.transform.localScale);
                }
                var viewport=GameObject.Find("CanvasRoot/SafeArea/Gameplay/BoardViewport").GetComponent<RectTransform>();
                Assert.Greater(viewport.rect.width,250);Assert.Greater(viewport.rect.height,180);
                var bottom=(RectTransform)hud.Element("bottom").transform;
                var a=new Vector3[4];var b=new Vector3[4];viewport.GetWorldCorners(a);bottom.GetWorldCorners(b);
                yield return Shot("camp_de130_"+size.x+"x"+size.y);
                if(size.x>size.y*1.1f)Assert.LessOrEqual(a[2].x,b[0].x+2,"Board overlaps side dock");
                else Assert.GreaterOrEqual(a[0].y,b[1].y-2,"Board overlaps bottom dock");
                Tap("pause");yield return Frames(12);Tap("resume");yield return Frames(12);
                Assert.IsNull(Button("resume"));
            }
            services.Settings.textScale=1f;services.Localization.TrySetLanguage("uk");
        }
    }
}
