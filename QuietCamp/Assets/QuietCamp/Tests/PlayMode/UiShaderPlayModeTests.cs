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
        public IEnumerator SettingsWidgets_AreSpriteBacked()
        {
            var canvasGo = new GameObject("canvas", typeof(Canvas));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var root = new GameObject("content", typeof(RectTransform));
            root.transform.SetParent(canvas.transform, false);
            var rt = (RectTransform)root.transform;
            rt.sizeDelta = new Vector2(800, 1200);
            var layout = root.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            yield return null;

            var changed = -1f;
            var slider = SettingsPanel.AddSliderRow(rt, "settings.music", 0.5f, v => changed = v);
            Assert.IsNotNull(slider.handleRect.GetComponent<Image>().sprite,
                "Slider handle sprite missing");
            slider.value = 0.8f;
            Assert.AreEqual(0.8f, changed, 0.001f, "Slider onChange not fired");

            var toggled = false;
            var toggle = SettingsPanel.AddToggleRow(rt, "settings.calm", false, v => toggled = v);
            toggle.isOn = true;
            Assert.IsTrue(toggled, "Toggle onValueChanged not fired");
            var imgs = toggle.GetComponentsInChildren<Image>(true);
            Assert.IsTrue(imgs.Length >= 2, "Toggle lacks box+check images");

            var picked = -1;
            var opts = SettingsPanel.AddChoiceRow(rt, "settings.language",
                new[] { "Українська", "English", "Deutsch" },
                new[] { "uk", "en", "de" }, "en", v => picked = v);
            Assert.AreEqual(3, opts.Length, "Choice row must render every option");
            Assert.AreEqual(QcUi.GreenDark, opts[1].GetComponent<Image>().color,
                "Current language must be the highlighted segment");
            opts[2].onClick.Invoke();
            Assert.AreEqual(2, picked, "Choice row onChange not fired");

            Object.Destroy(canvasGo);
        }
    }
}
