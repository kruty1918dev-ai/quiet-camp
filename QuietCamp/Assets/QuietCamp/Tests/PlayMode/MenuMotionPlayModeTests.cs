using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using QuietCamp.Presentation;
using QuietCamp.Presentation.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace QuietCamp.Tests
{
    public sealed class MenuMotionPlayModeTests
    {
        static Button Find(string id) => Object.FindObjectsByType<Button>().First(b => b.name == "<button #" + id + ">");
        static void Size(int width, int height) => typeof(ScreenshotPlayModeTest)
            .GetMethod("SetGameViewSize", BindingFlags.Static | BindingFlags.NonPublic)
            .Invoke(null, new object[] { width, height });
        static void Tap(string id)
        {
            Canvas.ForceUpdateCanvases();
            var button = Find(id); var rect = (RectTransform)button.transform;
            var pointer = new PointerEventData(EventSystem.current)
            { position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center)) };
            var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, hits);
            Assert.IsTrue(hits.Count > 0 && hits[0].gameObject.transform.IsChildOf(rect),
                "Covered button: " + id + "; first hit=" + (hits.Count>0?hits[0].gameObject.name:"none") + "; viewport="+Screen.width+"x"+Screen.height);
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler);
        }
        static IEnumerator Shot(string name)=>(IEnumerator)typeof(ScreenshotPlayModeTest)
            .GetMethod("Shot",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{Path.Combine(Directory.GetCurrentDirectory(),"Screenshots/MenuMotion"),name});
        static void VisibleSettings()
        {
            var button = Find("settings");
            foreach (var group in button.GetComponentsInParent<CanvasGroup>())
                Assert.GreaterOrEqual(group.alpha, .99f, "Hidden settings ancestor: " + group.name + " alpha=" + group.alpha);
            Assert.IsTrue(button.IsInteractable());
            var corners=new Vector3[4];((RectTransform)button.transform).GetWorldCorners(corners);
            var a=RectTransformUtility.WorldToScreenPoint(null,corners[0]);var b=RectTransformUtility.WorldToScreenPoint(null,corners[2]);
            var width=Mathf.Abs(b.x-a.x);var height=Mathf.Abs(b.y-a.y);
            var density=Screen.dpi>0?Screen.dpi/160f:2.5f;
            Assert.GreaterOrEqual(Mathf.Min(width,height),48*density-1,"Settings target must remain at least 48 dp after adaptation.");
            Assert.That(width/height,Is.InRange(.95f,1.05f),"Settings icon must retain a round target.");
        }
        [UnityTest, Timeout(180000)] public IEnumerator AnimatedMenuHeaderSurvivesOrientationAndFullScreenMenus()
        {
            Assert.IsTrue(UnityEngine.Application.productName.Contains("QA"), "Use isolated QA saves");
            if (UnityEngine.Application.isBatchMode) Assert.Ignore("Requires rendered Game View");
            Size(1080, 2400);
            yield return SceneManager.LoadSceneAsync("Boot");
            yield return PrivacyBootTestSupport.EnterGame();
            var services = QuietCampBootstrap.ServicesRef;
            services.Tutorial.Skip(); // This navigation regression deliberately exercises the complete menu.
            services.ReducedMotion = false; services.Settings.textScale = 1.3f;
            services.Localization.TrySetLanguage("de");
            foreach (var surface in Object.FindObjectsByType<HtmlSurface>()) surface.Refresh();
            yield return new WaitForSecondsRealtime(1);
            yield return Shot("01-settings-button-de130-portrait");
            VisibleSettings(); Tap("settings"); yield return new WaitForSecondsRealtime(.6f);
            Tap("set-cat-look"); yield return new WaitForSecondsRealtime(.6f);
            Tap("orientation-1"); Size(2400, 1080); yield return new WaitForSecondsRealtime(.8f);
            // A size selection can render asynchronously in Editor. Verify
            // the composed new viewport before hitting its restored controls.
            yield return Shot("02-settings-de130-landscape");
            Tap("settings-close"); yield return new WaitForSecondsRealtime(1);
            yield return Shot("03-settings-button-de130-landscape");
            VisibleSettings();
            foreach (var id in new[] { "levels", "album" })
            {
                Tap(id); yield return new WaitForSecondsRealtime(1);
                Tap("back"); yield return new WaitForSecondsRealtime(1);
                VisibleSettings();
            }
            Tap("settings"); yield return new WaitForSecondsRealtime(.6f);
            Assert.NotNull(Find("set-cat-sound"));
        }
    }
}
