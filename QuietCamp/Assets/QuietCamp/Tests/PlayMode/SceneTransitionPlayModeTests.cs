using System.Collections;
using Kruty1918.UiFoundation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace QuietCamp.Tests
{
    /// <summary>
    /// Every transition style must cover and reveal on real frames: the cover
    /// overlay activates, its style visual reaches full coverage, and the
    /// reveal deactivates the overlay. A broken style would silently skip
    /// blocking input during scene loads.
    /// </summary>
    public class SceneTransitionPlayModeTests
    {
        SceneTransitionService _service;

        [SetUp]
        public void SetUp() => _service = new SceneTransitionService();

        [UnityTest]
        public IEnumerator EveryStyle_CoversThenReveals()
        {
            foreach (SceneTransitionStyle style in
                System.Enum.GetValues(typeof(SceneTransitionStyle)))
            {
                yield return CoverOnce(style);
                var overlay = GameObject.Find("MoyvaSceneTransition");
                Assert.IsNotNull(overlay, $"Overlay missing after {style} cover");
                var group = overlay.GetComponent<CanvasGroup>();
                Assert.IsTrue(group.blocksRaycasts, $"{style}: cover must block input");
                AssertStyleCovered(style, overlay);

                yield return Wait(_service.RevealAsync(style));
                Assert.IsFalse(overlay.activeSelf, $"{style}: overlay stays after reveal");
            }
        }

        IEnumerator CoverOnce(SceneTransitionStyle style)
        {
            yield return Wait(_service.CoverAsync(style));
        }

        static IEnumerator Wait(System.Threading.Tasks.Task task)
        {
            while (!task.IsCompleted) yield return null;
            if (task.IsFaulted)
                Assert.Fail(task.Exception?.GetBaseException().Message);
        }

        static void AssertStyleCovered(SceneTransitionStyle style, GameObject overlay)
        {
            Transform Find(string n)
            {
                foreach (var t in overlay.GetComponentsInChildren<Transform>(true))
                    if (t.name == n) return t;
                return null;
            }
            switch (style)
            {
                case SceneTransitionStyle.Stripes:
                    var s0 = Find("Stripe_00");
                    Assert.IsNotNull(s0, "Stripes visual missing");
                    Assert.AreEqual(1f, s0.localScale.y, 0.01f, "Stripe not fully scaled");
                    break;
                case SceneTransitionStyle.Fade:
                    var f = Find("Fade");
                    Assert.IsNotNull(f, "Fade visual missing");
                    Assert.AreEqual(1f, f.GetComponent<UnityEngine.UI.Image>().color.a, 0.01f);
                    break;
                case SceneTransitionStyle.Iris:
                    var i = Find("Iris");
                    Assert.IsNotNull(i, "Iris visual missing");
                    Assert.Greater(i.localScale.x, 0.9f, "Iris not expanded");
                    break;
                case SceneTransitionStyle.Doors:
                    var l = Find("Door_L");
                    Assert.IsNotNull(l, "Doors visual missing");
                    Assert.AreEqual(1f, l.localScale.x, 0.01f, "Door not closed");
                    break;
                case SceneTransitionStyle.Curtain:
                    var c = Find("Curtain");
                    Assert.IsNotNull(c, "Curtain visual missing");
                    Assert.AreEqual(1f, c.localScale.y, 0.01f, "Curtain not raised");
                    break;
            }
        }
    }
}
