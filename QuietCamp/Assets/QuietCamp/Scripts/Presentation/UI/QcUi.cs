using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace QuietCamp.Presentation.UI
{
    /// <summary>
    /// Native mount roots, Kenney UI sprite resources and the small uGUI
    /// widgets kept for compatibility tests and mount-failure recovery.
    /// Regular player-facing screens are authored in HTML (HtmlSurface).
    /// </summary>
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

        // ─── Kenney UI sprites (mirrored to Resources by KenneyUiImporter) ────

        const string UiRoot = "QuietCamp/UI/";
        const string BtnGreen = UiRoot + "Green/Default/button_rectangle_depth_flat";
        const string BtnGrey = UiRoot + "Grey/Default/button_rectangle_depth_flat";
        const string BtnRed = UiRoot + "Red/Default/button_rectangle_depth_flat";
        const string CardSprite = UiRoot + "Extra/Default/input_rectangle";
        public const string IconCheck = UiRoot + "Green/Default/icon_checkmark";
        public const string IconCross = UiRoot + "Red/Default/icon_cross";
        public const string IconRepeat = UiRoot + "Extra/Default/icon_repeat_dark";
        public const string IconPlay = UiRoot + "Extra/Default/icon_play_light";
        public const string IconArrowUp = UiRoot + "Extra/Default/icon_arrow_up_dark";
        public const string IconArrowDown = UiRoot + "Extra/Default/icon_arrow_down_dark";
        public const string IconBack = UiRoot + "Grey/Default/arrow_basic_w";
        public const string IconUndo = UiRoot + "Extra/Default/icon_repeat_light";
        public const string IconPause = UiRoot + "Green/Default/icon_square";
        public const string SlideTrack = UiRoot + "Grey/Default/slide_horizontal_grey";
        public const string SlideFill = UiRoot + "Green/Default/slide_horizontal_color";
        public const string SlideHandle = UiRoot + "Grey/Default/slide_hangle";
        public const string Checkbox = UiRoot + "Grey/Default/check_square_grey";
        public const string CheckboxChecked = UiRoot + "Grey/Default/check_square_grey_checkmark";
        public const string IconCircle = UiRoot + "Green/Default/icon_circle";
        public const string IconCircleOutline = UiRoot + "Green/Default/icon_outline_circle";
        public const string BtnSecondary = BtnGrey;
        public const string CardSurface = CardSprite;

        static readonly Dictionary<string, Sprite> _sprites = new Dictionary<string, Sprite>();

        public static Sprite Sprite(string resourcePath)
        {
            if (_sprites.TryGetValue(resourcePath, out var s)) return s;
            s = Resources.Load<Sprite>(resourcePath);
            if (s == null)
            {
                Debug.LogWarning($"[QuietCamp] UI sprite missing: {resourcePath}");
            }
            _sprites[resourcePath] = s;
            return s;
        }

        // ─── Layout helpers ──────────────────────────────────────────────────

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

        // ─── Images ──────────────────────────────────────────────────────────

        /// <summary>Sliced Kenney sprite stretched under a parent.</summary>
        public static Image Sliced(RectTransform parent, string name,
            string spritePath, Color tint)
        {
            var r = Stretch(parent, name);
            var img = r.gameObject.AddComponent<UnityEngine.UI.Image>();
            img.sprite = Sprite(spritePath);
            img.type = UnityEngine.UI.Image.Type.Sliced;
            img.color = tint;
            return img;
        }

        /// <summary>Card/panel surface — light outlined input_rectangle.</summary>
        public static Image PanelImage(RectTransform parent, string name, Color color, float rounding = 0f)
        {
            return Sliced(parent, name, CardSprite, color);
        }

        /// <summary>Free-positioned icon image.</summary>
        public static Image Icon(RectTransform parent, string spritePath,
            float size = 64f, Color? tint = null)
        {
            var r = Root(parent, "icon");
            r.sizeDelta = new Vector2(size, size);
            var img = r.gameObject.AddComponent<UnityEngine.UI.Image>();
            img.sprite = Sprite(spritePath);
            img.preserveAspect = true;
            img.color = tint ?? Color.white;
            img.raycastTarget = false;
            return img;
        }

        // ─── Buttons ─────────────────────────────────────────────────────────

        /// <summary>
        /// uGUI Button backed by a sliced Kenney sprite. The Color argument picks
        /// the semantic variant: Green/Amber/GreenDark → green sprite tinted,
        /// Brown/Disabled → light secondary, Danger → red sprite.
        /// </summary>
        public static Button Button(RectTransform parent, string key, Action onClick,
            Color? bg = null, Vector2? minSize = null)
        {
            var c = bg ?? Green;
            var isLight = c == Brown || c == Disabled || c == Cream || c == CreamDark;
            var sprite = c == Danger ? BtnRed : isLight ? BtnGrey : BtnGreen;
            var tint = c == Green ? Color.white
                : c == GreenDark ? new Color(0.62f, 0.72f, 0.62f)
                : c == Amber ? new Color(1f, 0.78f, 0.45f)
                : c == Danger ? Color.white
                : isLight ? Cream : Color.white;
            var img = Sliced(parent, "Btn_" + key.Replace('.', '_'), sprite, tint);
            img.raycastTarget = true;
            var btn = img.gameObject.AddComponent<Button>();
            var rt = img.rectTransform;
            if (minSize.HasValue && rt.sizeDelta.y < minSize.Value.y)
                rt.sizeDelta = new Vector2(rt.sizeDelta.x, minSize.Value.y);
            var labelRect = Stretch(rt, "Label");
            var tmp = labelRect.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.fontSize = TextButton;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = isLight ? Ink : Cream;
            tmp.raycastTarget = false;
            var loc = labelRect.gameObject.AddComponent<LocalizedLabel>();
            loc.Bind(key);
            var colors = btn.colors;
            colors.highlightedColor = new Color(1.1f, 1.1f, 1.1f);
            colors.pressedColor = new Color(0.82f, 0.82f, 0.82f);
            colors.disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.7f);
            colors.fadeDuration = 0.08f;
            btn.colors = colors;
            if (onClick != null) btn.onClick.AddListener(() => onClick());
            return btn;
        }

        /// <summary>Icon button — square sprite + icon child.</summary>
        public static Button IconButton(RectTransform parent, string spritePath,
            Action onClick, Color? bg = null, float size = 96f, Color? iconTint = null)
        {
            var c = bg ?? Green;
            var sprite = c == Danger || c == Cream ? BtnGrey : BtnGreen;
            var img = Sliced(parent, "IconBtn", sprite,
                c == Green ? Color.white : c == Cream ? Color.white : c);
            img.raycastTarget = true;
            var rt = img.rectTransform;
            rt.sizeDelta = new Vector2(size, size);
            var btn = img.gameObject.AddComponent<Button>();
            var icon = Icon(rt, spritePath, size * 0.55f,
                iconTint ?? (c == Cream ? Ink : Color.white));
            icon.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            icon.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            icon.rectTransform.anchoredPosition = Vector2.zero;
            var colors = btn.colors;
            colors.pressedColor = new Color(0.82f, 0.82f, 0.82f);
            colors.disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.7f);
            colors.fadeDuration = 0.08f;
            btn.colors = colors;
            if (onClick != null) btn.onClick.AddListener(() => onClick());
            return btn;
        }
    }
}
