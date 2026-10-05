using System.Collections;
using System.Reflection;
using Kruty1918.InputRouting.API;
using Kruty1918.InputRouting.Runtime;
using NUnit.Framework;
using QuietCamp.Domain;
using QuietCamp.Infrastructure;
using QuietCamp.Presentation;
using QuietCamp.Presentation.UI;
using QuietCamp.Presentation.World;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace QuietCamp.Tests
{
    public class ControlMotionPlayModeTests
    {
        [UnityTest]
        public IEnumerator Controls_RapidActionsKeepIdentityAndRespectReducedMotion()
        {
            var services = new GameServices(new SaveAdapter(), QuietCampLocalization.Create(),
                null, null, null, null, null, null, null, null, null, null, null, null, null, null);
            var canvas = new GameObject("motion-controls", typeof(RectTransform), typeof(Canvas));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            HtmlSurface surface = null;
            var clicks = 0;
            var on = false;
            try
            {
                surface = HtmlSurface.Create(canvas.transform, "motion", services, () =>
                {
                    surface.Callbacks.BindToggle("switch", value => on = value);
                    return "<view class=\"app\">" + HtmlUi.Button(surface, "action", "Continue", () => clicks++)
                        + "<toggle id=\"switch\" checked=\"" + (on ? "true" : "false")
                        + "\" onChange=\"Globals.campUi.Toggle('switch', event)\"/></view>";
                });
                for (var i = 0; i < 8; i++) yield return null;
                var button = surface.GetComponentInChildren<Button>();
                var feedback = button.GetComponent<CampControlFeedback>();
                Assert.IsNotNull(feedback);
                for (var i = 0; i < 10; i++)
                {
                    button.onClick.Invoke();
                    surface.Refresh();
                    yield return null;
                    Assert.AreSame(button, surface.GetComponentInChildren<Button>(), "Reconciliation replaced the animated control");
                }
                Assert.AreEqual(10, clicks, "Motion must neither delay nor duplicate input");
                yield return new WaitForSecondsRealtime(.5f);
                Assert.AreEqual(1f, button.transform.localScale.x, .001f, "Repeated actions accumulated scale");

                var toggle = surface.GetComponentInChildren<Toggle>();
                var thumb = toggle.transform.Find("CampSwitchThumb") as RectTransform;
                Assert.IsNotNull(thumb);
                Assert.Less(thumb.anchoredPosition.x, 0f);
                toggle.isOn = true;
                Assert.IsTrue(on);
                yield return new WaitForSecondsRealtime(.4f);
                Assert.Greater(thumb.anchoredPosition.x, 0f, "Switch did not settle in its on position");

                services.ReducedMotion = true;
                surface.Refresh(); yield return null; yield return null;
                button.onClick.Invoke();
                Assert.AreEqual(11, clicks);
                Assert.IsFalse(feedback.IsAnimating);
                Assert.AreEqual(Vector3.one, button.transform.localScale);
                toggle.isOn = false;
                Assert.Less(thumb.anchoredPosition.x, 0f, "Reduced motion must apply a switch immediately");
                Assert.IsFalse(toggle.GetComponent<CampControlFeedback>().IsAnimating);
            }
            finally { Object.Destroy(canvas); services.Dispose(); }
            yield return null;
        }

        [UnityTest]
        public IEnumerator TentActions_CancelOldTweensAndDisableRetiringHitTargets()
        {
            var root = new GameObject("motion-tent", typeof(BoxCollider));
            new GameObject("VisualCenter").transform.SetParent(root.transform, false);
            var level = LevelLoader.Load("QC001");
            var presenter = new TentPresenter(root, level, level.guests[0].id);
            try
            {
                var pose = new Placement { guestId = level.guests[0].id, x = 1, z = 1 };
                presenter.ApplyPlacement(pose, true);
                presenter.Appear(false, 1f);
                presenter.SetLifted(false, false);
                yield return new WaitForSecondsRealtime(.5f);
                Assert.AreEqual(1f, root.transform.Find("LiftNode").localScale.x, .001f,
                    "Lift animation interrupted the appearance tween");
                pose.x = 2;
                presenter.ApplyPlacement(pose, false);
                pose.x = 0;
                presenter.ApplyPlacement(pose, true);
                yield return new WaitForSecondsRealtime(.4f);
                Assert.AreEqual(BoardMath.TentCenter(level, 0, 1), root.transform.localPosition,
                    "A stale tween overrode an instant/reduced-motion placement");
                presenter.Disappear(false, 1f);
                Assert.IsFalse(root.GetComponent<Collider>().enabled,
                    "A departing tent must not intercept a new drag or redo");
                yield return new WaitForSecondsRealtime(.4f);
                Assert.IsTrue(root == null, "Removed tent remained after its exit animation");
            }
            finally { if (root != null) presenter.Dispose(); }
        }

        [UnityTest]
        public IEnumerator ReleasedDrag_RechecksUiInsteadOfKeepingTheOldPointerCapture()
        {
            var events = new GameObject("capture-events", typeof(EventSystem));
            var canvas = new GameObject("capture-ui", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var button = new GameObject("capture-button", typeof(RectTransform), typeof(Image), typeof(Button));
            button.transform.SetParent(canvas.transform, false);
            ((RectTransform)button.transform).sizeDelta = new Vector2(200, 200);
            var controllerGo = new GameObject("capture-controller");
            var controller = controllerGo.AddComponent<PlacementController>();
            controller.enabled = false;
            try
            {
                yield return null;
                Canvas.ForceUpdateCanvases();
                var policy = new GameplayInputPolicy(events.GetComponent<EventSystem>());
                const int pointer = 77;
                Assert.IsTrue(policy.TryBeginPointerCapture(GameplayInputKind.Placement, new Vector2(-100, -100), pointer));
                var ui = new Vector2(Screen.width * .5f, Screen.height * .5f);
                Assert.IsTrue(policy.IsPointerOverUi(ui, pointer));
                var flags = BindingFlags.NonPublic | BindingFlags.Instance;
                typeof(PlacementController).GetField("_policy", flags).SetValue(controller, policy);
                typeof(PlacementController).GetField("_pointerId", flags).SetValue(controller, pointer);
                typeof(PlacementController).GetField("_captured", flags).SetValue(controller, true);
                typeof(PlacementController).GetMethod("ReleasePointer", flags).Invoke(controller, null);
                Assert.IsFalse(policy.CanProcess(GameplayInputKind.Placement, ui, pointer),
                    "After drag release, tapping a UI button must not place a tent behind it");
                Assert.IsTrue(policy.TryBeginPointerCapture(GameplayInputKind.Placement, new Vector2(-100, -100), pointer),
                    "The same pointer must remain usable for the next board drag");
                policy.EndPointerCapture(GameplayInputKind.Placement, pointer);
            }
            finally { Object.Destroy(controllerGo); Object.Destroy(canvas); Object.Destroy(events); }
            yield return null;
        }
    }
}
