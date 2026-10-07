using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using QuietCamp.Application;
using QuietCamp.Infrastructure;
using QuietCamp.Presentation;
using QuietCamp.Presentation.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace QuietCamp.Tests
{
    public class BonusRoadmapPlayModeTests
    {
        static IEnumerator Frames(int n){for(int i=0;i<n;i++)yield return null;}
        static void Size(int w,int h)=>typeof(ScreenshotPlayModeTest).GetMethod("SetGameViewSize",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{w,h});
        static IEnumerator Shot(string name)=>(IEnumerator)typeof(ScreenshotPlayModeTest).GetMethod("Shot",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{Path.Combine(Directory.GetCurrentDirectory(),"Screenshots/BonusRoadmap"),name});
        static Button Button(string id)=>Object.FindObjectsByType<Button>().FirstOrDefault(b=>b.name=="<button #"+id+">"&&b.gameObject.activeInHierarchy);
        static void Tap(string id)
        {
            var button=Button(id);Assert.NotNull(button,id);Assert.IsTrue(button.interactable,id);
            var rect=(RectTransform)button.transform;var position=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center));
            var pointer=new PointerEventData(EventSystem.current){position=position};var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(pointer,hits);
            Assert.That(position.x,Is.InRange(1,Screen.width-1));Assert.That(position.y,Is.InRange(1,Screen.height-1));
            Assert.IsTrue(hits.Count>0&&(hits[0].gameObject.transform==rect||hits[0].gameObject.transform.IsChildOf(rect)),"Covered control: "+id);
            ExecuteEvents.Execute(button.gameObject,pointer,ExecuteEvents.pointerClickHandler);
        }
        [UnityTest] public IEnumerator BonusBranchesHaveSafePreviewsAndPreserveCampaignAndScrollAcrossScreens()
        {
            if(UnityEngine.Application.isBatchMode)Assert.Ignore("Requires rendered Game View");
            Size(720,1600);yield return SceneManager.LoadSceneAsync("Boot");
            yield return PrivacyBootTestSupport.EnterGame();
            var deadline=Time.realtimeSinceStartup+30;
            while(Object.FindAnyObjectByType<QuietCampBootstrap>()?.StartupReady!=true&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.IsTrue(Object.FindAnyObjectByType<QuietCampBootstrap>().StartupReady);yield return Frames(18);
            var services=QuietCampBootstrap.ServicesRef;var saved=services.Progression.CompletedIds.ToArray();var last=services.Progression.LastLevelId;var flags=services.Progression.CosmeticFlags;
            var allIds=LevelLoader.MvpLevelIds();
            // A completed world keeps every slot inside the reveal horizon;
            // the veiled-tap case is exercised explicitly below.
            services.Progression.Restore(allIds,allIds[allIds.Count-1],0);services.ReducedMotion=true;services.Settings.textScale=1.3f;services.LevelMapScroll=-1;
            // Restoring progression does not rebuild mounted HTML — refresh the
            // overlay so cards gated by progress (like the map entry) appear.
            foreach(var surface in Object.FindObjectsByType<HtmlSurface>())surface.Refresh();yield return Frames(15);
            var doneBefore=services.Progression.CompletedCount;
            foreach(var size in new[]{new Vector2Int(720,1600),new Vector2Int(1280,800),new Vector2Int(2560,1080)})
            {
                Size(size.x,size.y);yield return Frames(15);Tap("levels");yield return Frames(15);
                var overlay=Object.FindObjectsByType<HtmlSurface>().Single(s=>s.name=="MenuOverlay");
                foreach(var language in new[]{"uk","en","de"})
                {
                    services.Localization.TrySetLanguage(language);yield return Frames(15);
                    Assert.AreEqual(110,overlay.GetComponentsInChildren<Button>(true).Count(b=>b.name.StartsWith("<button #level-")));
                    Assert.AreEqual(21,overlay.GetComponentsInChildren<Button>(true).Count(b=>b.name.StartsWith("<button #bonus-")&&b.name!="<button #bonus-play>"));
                    var graphic=overlay.GetComponentInChildren<RoadmapGraphic>();Assert.Less(graphic.canvasRenderer.GetMesh().vertexCount,60000);
                    Debug.Log("[BonusRoadmapQA] "+size+" "+language+" vertices="+graphic.canvasRenderer.GetMesh().vertexCount);
                    foreach(var slot in BonusCampCatalog.Slots)
                    {
                        var scroll=overlay.Element("roadmap-scroll").GetComponent<ScrollRect>();
                        float height=scroll.viewport.rect.height;
                        float fraction=1-Mathf.Clamp01((RoadmapLayout.BonusY(slot)-height*.4f)/(scroll.content.rect.height-height));
                        scroll.verticalNormalizedPosition=fraction;scroll.velocity=Vector2.zero;yield return Frames(6);
                        var labels=Button("bonus-"+slot.afterLevel).GetComponentsInChildren<TMP_Text>();
                        foreach(var text in labels)
                        {
                            Assert.LessOrEqual(text.GetRenderedValues().x,text.rectTransform.rect.width+2,"Bonus label overflows in "+language);
                            var card=(RectTransform)Button("bonus-"+slot.afterLevel).transform;var corners=new Vector3[4];text.rectTransform.GetWorldCorners(corners);
                            foreach(var corner in corners)
                            {
                                var local=card.InverseTransformPoint(corner);
                                Assert.That(local.x,Is.InRange(card.rect.xMin-2,card.rect.xMax+2),"Text escapes bonus card in "+language);
                            }
                        }
                        // Preview taps + screenshots cover one slot per family:
                        // a seasonal gate, a premium overlook and a challenge den.
                        bool deep=new[]{40,45,80}.Contains(slot.afterLevel);
                        if(deep&&(language=="uk"||language=="de"))yield return Shot("branch_"+slot.afterLevel+"_"+size.x+"x"+size.y+"_"+language);
                        if(!deep)continue;
                        Tap("bonus-"+slot.afterLevel);yield return Frames(12);
                        Assert.NotNull(overlay.GetComponentInChildren<BonusCampPreviewGraphic>());
                        // Play stays bound to the slot's real access state —
                        // the preview card itself never decides playability.
                        Assert.AreEqual(services.BonusCamps.Evaluate(slot).CanPlay,Button("bonus-play").interactable);
                        var playRect=(RectTransform)Button("bonus-play").transform;var playCorners=new Vector3[4];playRect.GetWorldCorners(playCorners);
                        foreach(var corner in playCorners)Assert.That(corner.y,Is.InRange(0,Screen.height),"Preview action escaped the screen");
                        if(!Button("bonus-play").interactable)
                        { var before=services.PendingLevelId;Button("bonus-play").onClick.Invoke();Assert.AreEqual(before,services.PendingLevelId,"A draft launched a missing level"); }
                        Assert.AreEqual(doneBefore,services.Progression.CompletedCount,"Preview must not grant progress");
                        if(language=="de"||size.x==720)yield return Shot("preview_"+slot.afterLevel+"_"+size.x+"x"+size.y+"_"+language);
                        Tap("back");yield return Frames(15);
                        scroll=overlay.Element("roadmap-scroll").GetComponent<ScrollRect>();Assert.AreEqual(fraction,scroll.verticalNormalizedPosition,.02f,"Preview reset map scroll");
                    }
                }
                Tap("back");yield return Frames(15);
            }
            var ids=allIds;
            // Near-fresh reveal (QC001 done so the map opens): nothing past the
            // horizon turns playable — the teaser card stays view-only.
            services.Progression.Restore(ids.Take(1),ids[0],0);services.LevelMapScroll=-1;
            foreach(var surface in Object.FindObjectsByType<HtmlSurface>())surface.Refresh();yield return Frames(12);
            Tap("levels");yield return Frames(15);
            var mapOverlay=Object.FindObjectsByType<HtmlSurface>().Single(s=>s.name=="MenuOverlay");
            var scrollVeiled=mapOverlay.Element("roadmap-scroll").GetComponent<ScrollRect>();
            var veiledSlot=BonusCampCatalog.Slots.First(s=>s.afterLevel==40);
            float vh=scrollVeiled.viewport.rect.height;
            scrollVeiled.verticalNormalizedPosition=1-Mathf.Clamp01((RoadmapLayout.BonusY(veiledSlot)-vh*.4f)/(scrollVeiled.content.rect.height-vh));
            scrollVeiled.velocity=Vector2.zero;yield return Frames(6);
            Tap("bonus-40");yield return Frames(10);
            // The teaser card may open, but a veiled slot can never be played.
            Assert.NotNull(mapOverlay.GetComponentInChildren<BonusCampPreviewGraphic>());
            var pendingBefore=services.PendingLevelId;
            Button("bonus-play").onClick.Invoke();
            Assert.AreEqual(pendingBefore,services.PendingLevelId,"A veiled slot became playable");
            Tap("back");yield return Frames(12);
            // Preview-back lands on the map — one more back to reach Main.
            Tap("back");yield return Frames(12);
            services.Progression.Restore(ids.Take(20),ids[19],0);services.LevelMapScroll=-1;
            foreach(var surface in Object.FindObjectsByType<HtmlSurface>())surface.Refresh();yield return Frames(12);
            Tap("levels");yield return Frames(15);
            var node=(RectTransform)Button("level-20").transform;var p=RectTransformUtility.WorldToScreenPoint(null,node.TransformPoint(node.rect.center));
            Assert.That(p.y,Is.InRange(1,Screen.height-1),"Current level was not visible after bonus gaps");
            Assert.AreEqual(BonusCampState.Available,services.BonusCamps.Evaluate(BonusCampCatalog.Slots[0]).State);
            Assert.IsTrue(services.Progression.IsUnlocked(ids[20],ids));
            yield return Shot("current_level_21");Tap("back");yield return Frames(15);
            services.Progression.Restore(saved,last,flags);services.Settings.textScale=1;services.Localization.TrySetLanguage("uk");services.ReducedMotion=false;
        }
    }
}
