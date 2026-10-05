using System;
using System.Xml;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace QuietCamp.Presentation.UI
{
    /// <summary>Last-resort uGUI controls when a document cannot mount. Same C#
    /// actions and saved state; no scripting, CSS, Yoga or generated UI required.</summary>
    internal static class HtmlRecoveryControls
    {
        public static void Show(HtmlSurface surface, string html, HtmlCallbacks callbacks)
        {
            // Mount teardown uses deferred Destroy. Retire the old fallback now,
            // otherwise a same-frame failed retry can leave only that doomed panel.
            var previous = surface.transform.Find("UiRecovery");
            if (previous != null)
            {
                previous.name = "RetiredUiRecovery";
                previous.gameObject.SetActive(false);
                UnityEngine.Object.Destroy(previous.gameObject);
            }
            var document = new XmlDocument { XmlResolver = null };
            try { document.LoadXml("<root>" + html + "</root>"); }
            catch (XmlException) { return; }
            var controls = document.SelectNodes("//*[@onClick or @onChange]");
            if (controls == null || controls.Count == 0) return;

            var panel = QcUi.Stretch(surface.transform, "UiRecovery");
            panel.anchorMax = new Vector2(1, .45f);
            panel.offsetMin = new Vector2(24, 24); panel.offsetMax = new Vector2(-24, -24);
            panel.gameObject.AddComponent<Image>().color = QcUi.Cream;
            var scroll = panel.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            var viewport = QcUi.Stretch(panel, "Viewport");
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = QcUi.Root(viewport, "Controls");
            content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one;
            content.pivot = new Vector2(.5f, 1); content.sizeDelta = Vector2.zero;
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(20, 20, 20, 20); layout.spacing = 12;
            layout.childControlHeight = true; layout.childForceExpandHeight = false;
            layout.childControlWidth = true; layout.childForceExpandWidth = true;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport; scroll.content = content;

            foreach (XmlElement element in controls)
            {
                var id = element.GetAttribute("id");
                if (id == "dim" || string.IsNullOrEmpty(id)) continue;
                var title = element.InnerText.Trim();
                if (title.Length == 0) title = element.GetAttribute("data-tooltip");
                if (title.Length == 0) title = id;
                var row = QcUi.Root(content, id);
                row.gameObject.AddComponent<LayoutElement>().preferredHeight = QcUi.MinTouch;
                if (element.HasAttribute("onClick"))
                {
                    var image = row.gameObject.AddComponent<Image>(); image.color = QcUi.GreenDark;
                    var button = row.gameObject.AddComponent<Button>(); button.targetGraphic = image;
                    button.interactable = element.GetAttribute("disabled") != "true";
                    button.onClick.AddListener(() => { callbacks.Click(id); surface.Refresh(); });
                    Label(row, title, Color.white);
                }
                else if (element.Name == "switch" || element.Name == "toggle")
                {
                    var image = row.gameObject.AddComponent<Image>();
                    var toggle = row.gameObject.AddComponent<Toggle>(); toggle.targetGraphic = image;
                    var value = element.GetAttribute("checked") == "true";
                    toggle.SetIsOnWithoutNotify(value);
                    image.color = value ? QcUi.GreenDark : QcUi.CreamDark;
                    toggle.onValueChanged.AddListener(v => { callbacks.Toggle(id, v); surface.Refresh(); });
                    Label(row, title, QcUi.Ink);
                }
                else if (element.Name == "slider")
                {
                    var image = row.gameObject.AddComponent<Image>(); image.color = QcUi.CreamDark;
                    var slider = row.gameObject.AddComponent<Slider>(); slider.targetGraphic = image;
                    slider.minValue = Parse(element, "min", 0); slider.maxValue = Parse(element, "max", 1);
                    slider.SetValueWithoutNotify(Parse(element, "value", 0));
                    var handle = QcUi.Root(row, "Handle"); handle.sizeDelta = new Vector2(40, 40);
                    handle.gameObject.AddComponent<Image>().color = QcUi.GreenDark;
                    slider.handleRect = handle;
                    slider.onValueChanged.AddListener(v => callbacks.Number(id, v));
                }
            }
        }

        static float Parse(XmlElement element, string key, float fallback) =>
            float.TryParse(element.GetAttribute(key), System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var value) ? value : fallback;

        static void Label(RectTransform parent, string title, Color color)
        {
            var rect = QcUi.Stretch(parent, "Label");
            rect.offsetMin = new Vector2(12, 4); rect.offsetMax = new Vector2(-12, -4);
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = Resources.Load<TMP_FontAsset>("Fonts/DejaVuSans SDF");
            text.text = title; text.color = color; text.fontSize = 30;
            text.alignment = TextAlignmentOptions.Center; text.raycastTarget = false;
        }
    }
}
