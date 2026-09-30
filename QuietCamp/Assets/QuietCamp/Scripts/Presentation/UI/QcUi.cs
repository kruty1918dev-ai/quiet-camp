using System;
using Kruty1918.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace QuietCamp.Presentation.UI
{
    /// <summary>Runtime uGUI factory: consistent fonts, colors and 48dp targets.</summary>
    public static class QcUi
    {
        public static readonly Color Cream = new Color(0.949f, 0.910f, 0.835f);
        public static readonly Color CreamDark = new Color(0.898f, 0.847f, 0.753f);
        public static readonly Color Green = new Color(0.42f, 0.56f, 0.44f);
        public static readonly Color GreenDark = new Color(0.30f, 0.42f, 0.33f);
        public static readonly Color Brown = new Color(0.35f, 0.27f, 0.20f);
        public static readonly Color Ink = new Color(0.22f, 0.18f, 0.14f);
        public static readonly Color Amber = new Color(0.90f, 0.60f, 0.20f);
        public static readonly Color Danger = new Color(0.75f, 0.32f, 0.28f);
        public static readonly Color Disabled = new Color(0.62f, 0.60f, 0.55f);

        public const float MinTouch = 88f; // ~48 dp at reference scale
        public const float TextBody = 30f, TextButton = 34f, TextTitle = 42f, TextSmall = 26f;

        public static RectTransform Root(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static RectTransform Stretch(Transform parent, string name)
        {
            var r = Root(parent, name);
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
            r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
            return r;
        }

        public static RectTransform Anchor(Transform parent, string name,
            Vector2 min, Vector2 max, Vector2 offMin, Vector2 offMax)
        {
            var r = Root(parent, name);
            r.anchorMin = min; r.anchorMax = max;
            r.offsetMin = offMin; r.offsetMax = offMax;
            return r;
        }

        public static Image Image(RectTransform parent, string name, Color color)
        {
            var r = Stretch(parent, name);
            var img = r.gameObject.AddComponent<UnityEngine.UI.Image>();
            img.color = color;
            return img;
        }

        public static Image PanelImage(RectTransform parent, string name, Color color, float rounding = 0f)
        {
            return Image(parent, name, color);
        }

        /// <summary>Non-interactive localized label. Registers for language refresh.</summary>
        public static LocalizedLabel Label(RectTransform parent, string key,
            float size = TextBody, TextAlignmentOptions align = TextAlignmentOptions.Center,
            Color? color = null)
        {
            var r = Stretch(parent, key.Replace('.', '_'));
            var tmp = r.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.fontSize = size;
            tmp.alignment = align;
            tmp.color = color ?? Ink;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            tmp.raycastTarget = false;
            var loc = r.gameObject.AddComponent<LocalizedLabel>();
            loc.Bind(key);
            return loc;
        }

        public static TextMeshProUGUI PlainText(RectTransform parent, string text,
            float size = TextBody, TextAlignmentOptions align = TextAlignmentOptions.Center,
            Color? color = null)
        {
            var r = Stretch(parent, "Text");
            var tmp = r.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.fontSize = size;
            tmp.alignment = align;
            tmp.color = color ?? Ink;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            tmp.raycastTarget = false;
            tmp.text = text;
            return tmp;
        }

        /// <summary>uGUI Button with a localized child label and press scale animation.</summary>
        public static Button Button(RectTransform parent, string key, Action onClick,
            Color? bg = null, Vector2? minSize = null)
        {
            var img = Image(parent, "Btn_" + key.Replace('.', '_'), bg ?? Green);
            img.raycastTarget = true;
            var btn = img.gameObject.AddComponent<Button>();
            var rt = img.rectTransform;
            if (minSize.HasValue && rt.sizeDelta.y < minSize.Value.y)
                rt.sizeDelta = new Vector2(rt.sizeDelta.x, minSize.Value.y);
            var labelRect = Stretch(rt, "Label");
            var tmp = labelRect.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.fontSize = TextButton;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Cream;
            tmp.raycastTarget = false;
            var loc = labelRect.gameObject.AddComponent<LocalizedLabel>();
            loc.Bind(key);
            var colors = btn.colors;
            colors.highlightedColor = Color.Lerp(bg ?? Green, Color.white, 0.12f);
            colors.pressedColor = Color.Lerp(bg ?? Green, Color.black, 0.12f);
            colors.disabledColor = Disabled;
            btn.colors = colors;
            if (onClick != null) btn.onClick.AddListener(() => onClick());
            return btn;
        }

        /// <summary>CanvasGroup panel used for modals; starts hidden.</summary>
        public static CanvasGroup Modal(RectTransform parent, string name, Color dimmer)
        {
            var rt = Stretch(parent, name);
            var group = rt.gameObject.AddComponent<CanvasGroup>();
            var img = rt.gameObject.AddComponent<UnityEngine.UI.Image>();
            img.color = dimmer;
            img.raycastTarget = true;
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
            rt.gameObject.SetActive(false);
            return group;
        }
    }
}
