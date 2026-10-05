using System.Collections;
using NUnit.Framework;
using QuietCamp.Presentation.UI;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace QuietCamp.Tests
{
    /// <summary>
    /// Runtime-side guards for the two device defects we hit:
    /// 1) shaders stripped on device → magenta primitives (assert Shader.Find works)
    /// 2) sprite-less UI → assert every Kenney sprite path resolves and
    ///    factory-built widgets carry sliced sprites.
    /// </summary>
    public class UiShaderPlayModeTests
    {
        [Test]
        public void Shaders_UsedByRuntime_AllResolve()
        {
            var names = new[]
            {
                "Universal Render Pipeline/Lit",
                "Universal Render Pipeline/Simple Lit",
                "Universal Render Pipeline/Unlit",
                "Universal Render Pipeline/Particles/Unlit",
                "Universal Render Pipeline/Particles/Simple Lit"
            };
            foreach (var n in names)
                Assert.IsNotNull(Shader.Find(n), $"Shader missing at runtime: {n}");

            var prim = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var mat = prim.GetComponent<Renderer>().sharedMaterial;
            Assert.IsNotNull(mat, "CreatePrimitive produced null material");
            Assert.IsFalse(mat.name.Contains("Hidden") || mat.shader.name.Contains("Error"),
                $"CreatePrimitive material looks like an error shader: {mat.shader.name}");
            Object.DestroyImmediate(prim);
        }

        [Test]
        public void UiSprites_AllResolve()
        {
            var paths = new[]
            {
                QcUi.BtnSecondary, QcUi.CardSurface, QcUi.IconCheck, QcUi.IconCross,
                QcUi.IconRepeat, QcUi.IconPlay, QcUi.IconArrowUp, QcUi.IconArrowDown,
                QcUi.SlideTrack, QcUi.SlideFill, QcUi.SlideHandle, QcUi.Checkbox
            };
            foreach (var p in paths)
            {
                var s = QcUi.Sprite(p);
                Assert.IsNotNull(s, $"Sprite missing: {p}");
            }
        }

        [UnityTest]
        public IEnumerator Widgets_CarrySlicedSprites()
        {
            var canvasGo = new GameObject("canvas", typeof(Canvas));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var root = new GameObject("root", typeof(RectTransform));
            root.transform.SetParent(canvas.transform, false);
            var rt = (RectTransform)root.transform;
            rt.sizeDelta = new Vector2(600, 600);
            yield return null;

            var btn = QcUi.Button(rt, "action.back", null);
            var img = btn.GetComponent<Image>();
            Assert.IsNotNull(img.sprite, "Button has no sprite");
            Assert.AreEqual(Image.Type.Sliced, img.type);

            var panel = QcUi.PanelImage(rt, "panel", Color.white);
            Assert.IsNotNull(panel.sprite, "PanelImage has no sprite");
            Assert.AreEqual(Image.Type.Sliced, panel.type);

            var iconBtn = QcUi.IconButton(rt, QcUi.IconRepeat, null);
            Assert.IsNotNull(iconBtn.GetComponent<Image>().sprite, "IconButton bg sprite missing");
            var icon = iconBtn.GetComponentInChildren<Image>();
            Assert.IsNotNull(icon);

            Object.Destroy(canvasGo);
        }

        [UnityTest]
        public IEnumerator HtmlControls_DispatchValuesAndSurviveReconciliation()
        {
            var canvasGo = new GameObject("html-test", typeof(RectTransform), typeof(Canvas));
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            HtmlSurface surface = null;
            var changed = -1f;
            var toggled = false;
            var clicked = 0;
            surface = HtmlSurface.Create(canvasGo.transform, "test-document", null, () =>
            {
                surface.Callbacks.BindNumber("volume", v => changed = v);
                surface.Callbacks.BindToggle("calm", v => toggled = v);
                return "<view class=\"app\"><slider id=\"volume\" value=\"0.5\" onChange=\"Globals.campUi.Number('volume', event)\" />" +
                    "<toggle id=\"calm\" checked=\"false\" onChange=\"Globals.campUi.Toggle('calm', event)\" />" +
                    HtmlUi.Button(surface, "click", "Continue", () => clicked++) + "</view>";
            });
            yield return null;
            yield return null;
            var slider = surface.GetComponentInChildren<Slider>();
            Assert.IsNotNull(slider, "HTML slider did not mount");
            slider.value = .8f;
            Assert.AreEqual(.8f, changed, .001f);
            var toggle = surface.GetComponentInChildren<Toggle>();
            Assert.IsNotNull(toggle);
            toggle.isOn = true;
            Assert.IsTrue(toggled);
            var button = surface.GetComponentInChildren<Button>();
            button.onClick.Invoke();
            Assert.AreEqual(1, clicked);
            surface.Refresh();
            yield return null;
            yield return null;
            Assert.AreSame(button, surface.GetComponentInChildren<Button>(), "Reconciliation replaced a stable control");
            button.onClick.Invoke();
            Assert.AreEqual(2, clicked, "Reconciliation stacked event listeners");
            Object.Destroy(canvasGo);
            yield return null;
        }
    }
}
