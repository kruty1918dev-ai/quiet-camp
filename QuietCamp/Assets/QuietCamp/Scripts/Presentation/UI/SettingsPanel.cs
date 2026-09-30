using System;
using QuietCamp.Infrastructure;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace QuietCamp.Presentation.UI
{
    /// <summary>
    /// Shared settings content — one builder used by both the main-menu
    /// Settings screen and the in-game settings modal so every control
    /// (language, volume buses, text scale, scroll, motion/calm/contrast/
    /// haptics toggles) behaves identically in both places.
    /// Caller supplies a vertical-layout content rect; rows attach via
    /// LayoutElement heights.
    /// </summary>
    public static class SettingsPanel
    {
        public static void BuildRows(GameServices services, RectTransform content)
        {
            AddDropdownRow(content, "settings.language",
                new[] { "Українська", "English", "Deutsch" }, services.Settings.language,
                v =>
                {
                    var ids = new[] { "uk", "en", "de" };
                    services.Localization.TrySetLanguage(ids[Mathf.Clamp(v, 0, 2)]);
                    services.Settings.language = services.Localization.CurrentLanguageId;
                    services.Save.Save();
                });

            AddSliderRow(content, "settings.music",
                services.Settings.music, v => SetBus(services, Kruty1918.Audio.AudioBus.Music, v));
            AddSliderRow(content, "settings.ambience",
                services.Settings.ambience, v => SetBus(services, Kruty1918.Audio.AudioBus.Ambience, v));
            AddSliderRow(content, "settings.effects",
                services.Settings.effects, v =>
                {
                    SetBus(services, Kruty1918.Audio.AudioBus.Ui, v);
                    SetBus(services, Kruty1918.Audio.AudioBus.Sfx, v);
                });
            AddSliderRow(content, "settings.textSize",
                services.Settings.textScale, v =>
                {
                    services.Settings.textScale = Mathf.Clamp(v, 0.85f, 1.3f);
                    LocalizedLabel.TextScale = services.Settings.textScale;
                    services.Save.Save();
                }, 0.85f, 1.3f);
            AddSliderRow(content, "settings.scroll",
                services.Settings.scrollSensitivity, v =>
                {
                    services.Settings.scrollSensitivity = v;
                    services.Save.Save();
                }, 3f, 24f);

            AddToggleRow(content, "settings.reducedMotion",
                services.Settings.reducedMotion, v =>
                {
                    services.ReducedMotion = v;
                    services.Save.Save();
                });
            AddToggleRow(content, "settings.calm",
                services.Settings.calmMode, v =>
                {
                    services.CalmMode = v;
                    services.Save.Save();
                });
            AddToggleRow(content, "settings.contrast",
                services.Settings.highContrast, v =>
                {
                    services.Settings.highContrast = v;
                    services.Save.Save();
                });
            AddToggleRow(content, "settings.haptics",
                services.Settings.haptics, v =>
                {
                    services.Settings.haptics = v;
                    services.Save.Save();
                });
        }

        static void SetBus(GameServices services, Kruty1918.Audio.AudioBus bus, float v)
        {
            services.Audio?.SetBusVolume(bus, v);
            switch (bus)
            {
                case Kruty1918.Audio.AudioBus.Music: services.Settings.music = v; break;
                case Kruty1918.Audio.AudioBus.Ambience: services.Settings.ambience = v; break;
                default: services.Settings.effects = v; break;
            }
            services.Save.Save();
        }

        // ─── Row builders (sprite-backed controls) ──────────────────────────

        public static Slider AddSliderRow(RectTransform parent, string key, float value,
            Action<float> onChange, float min = 0f, float max = 1f)
        {
            var row = QcUi.Root(parent, "row_" + key);
            row.sizeDelta = new Vector2(0, 96);
            row.gameObject.AddComponent<LayoutElement>().minHeight = 96;
            var label = QcUi.Anchor(row, "l",
                new Vector2(0, 0), new Vector2(0.45f, 1), Vector2.zero, Vector2.zero);
            QcUi.Label(label, key, QcUi.TextSmall, TextAlignmentOptions.MidlineLeft, QcUi.Ink);
            var sliderGo = QcUi.Anchor(row, "s",
                new Vector2(0.45f, 0.2f), new Vector2(1, 0.8f), Vector2.zero, Vector2.zero);
            return BuildSlider(sliderGo, min, max, value, onChange);
        }

        public static Slider BuildSlider(RectTransform parent, float min, float max,
            float value, Action<float> onChange)
        {
            var slider = parent.gameObject.AddComponent<Slider>();
            slider.minValue = min; slider.maxValue = max;
            var track = QcUi.Anchor(parent, "track",
                new Vector2(0, 0.35f), new Vector2(1, 0.65f), Vector2.zero, Vector2.zero);
            var bg = track.gameObject.AddComponent<Image>();
            bg.sprite = QcUi.Sprite(QcUi.SlideTrack);
            bg.type = Image.Type.Sliced;
            if (bg.sprite == null) bg.color = QcUi.CreamDark;
            var handle = QcUi.Anchor(parent, "handle",
                new Vector2(0, 0), new Vector2(0, 1),
                new Vector2(-18, -4), new Vector2(18, 4));
            var handleImg = handle.gameObject.AddComponent<Image>();
            handleImg.sprite = QcUi.Sprite(QcUi.SlideHandle);
            handleImg.preserveAspect = true;
            if (handleImg.sprite == null) handleImg.color = QcUi.Green;
            var fillArea = QcUi.Stretch(parent, "FillArea");
            var fill = QcUi.Anchor(fillArea, "Fill",
                new Vector2(0, 0.35f), new Vector2(0, 0.65f), Vector2.zero, Vector2.zero);
            var fillImg = fill.gameObject.AddComponent<Image>();
            fillImg.sprite = QcUi.Sprite(QcUi.SlideFill);
            fillImg.type = Image.Type.Sliced;
            if (fillImg.sprite == null) fillImg.color = QcUi.GreenDark;
            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = handleImg;
            slider.direction = Slider.Direction.LeftToRight;
            slider.onValueChanged.AddListener(v => onChange(v));
            slider.SetValueWithoutNotify(value);
            return slider;
        }

        public static Toggle AddToggleRow(RectTransform parent, string key, bool value,
            Action<bool> onChange)
        {
            var row = QcUi.Root(parent, "row_" + key);
            row.gameObject.AddComponent<LayoutElement>().minHeight = 96;
            var label = QcUi.Anchor(row, "l",
                new Vector2(0, 0), new Vector2(0.75f, 1), Vector2.zero, Vector2.zero);
            QcUi.Label(label, key, QcUi.TextSmall, TextAlignmentOptions.MidlineLeft, QcUi.Ink);
            var boxGo = QcUi.Anchor(row, "t",
                new Vector2(0.8f, 0.1f), new Vector2(1, 0.9f), Vector2.zero, Vector2.zero);
            var box = boxGo.gameObject.AddComponent<Image>();
            box.sprite = QcUi.Sprite(QcUi.Checkbox);
            if (box.sprite == null) box.color = QcUi.CreamDark;
            var toggle = boxGo.gameObject.AddComponent<Toggle>();
            var check = QcUi.Anchor(boxGo, "check",
                new Vector2(0.15f, 0.15f), new Vector2(0.85f, 0.85f), Vector2.zero, Vector2.zero);
            var checkImg = check.gameObject.AddComponent<Image>();
            checkImg.sprite = QcUi.Sprite(QcUi.IconCheck);
            checkImg.preserveAspect = true;
            if (checkImg.sprite == null) checkImg.color = QcUi.Green;
            checkImg.raycastTarget = false;
            toggle.graphic = checkImg;
            toggle.targetGraphic = box;
            toggle.SetIsOnWithoutNotify(value);
            toggle.onValueChanged.AddListener(v => onChange(v));
            return toggle;
        }

        public static TMP_Dropdown AddDropdownRow(RectTransform parent, string key,
            string[] options, string currentId, Action<int> onChange)
        {
            var row = QcUi.Root(parent, "row_" + key);
            row.gameObject.AddComponent<LayoutElement>().minHeight = 96;
            var label = QcUi.Anchor(row, "l",
                new Vector2(0, 0), new Vector2(0.45f, 1), Vector2.zero, Vector2.zero);
            QcUi.Label(label, key, QcUi.TextSmall, TextAlignmentOptions.MidlineLeft, QcUi.Ink);
            var dropGo = QcUi.Anchor(row, "d",
                new Vector2(0.45f, 0.15f), new Vector2(1, 0.85f), Vector2.zero, Vector2.zero);
            var bg = dropGo.gameObject.AddComponent<Image>();
            bg.sprite = QcUi.Sprite(QcUi.BtnSecondary);
            bg.type = Image.Type.Sliced;
            bg.color = QcUi.Cream;
            if (bg.sprite == null) bg.color = QcUi.CreamDark;
            var drop = dropGo.gameObject.AddComponent<TMP_Dropdown>();
            var captionGo = QcUi.Stretch(dropGo, "Caption");
            var caption = captionGo.gameObject.AddComponent<TextMeshProUGUI>();
            caption.fontSize = QcUi.TextSmall;
            caption.color = QcUi.Ink;
            caption.alignment = TextAlignmentOptions.Center;
            drop.captionText = caption;
            var arrowGo = QcUi.Anchor(dropGo, "arrow",
                new Vector2(1, 0.5f), new Vector2(1, 0.5f),
                new Vector2(-52, -16), new Vector2(-20, 16));
            var arrowImg = arrowGo.gameObject.AddComponent<Image>();
            arrowImg.sprite = QcUi.Sprite(QcUi.IconArrowDown);
            arrowImg.preserveAspect = true;
            arrowImg.raycastTarget = false;
            var templateGo = QcUi.Anchor(dropGo, "Template",
                new Vector2(0, 0), new Vector2(1, 0),
                new Vector2(0, -options.Length * 90f), new Vector2(0, 0));
            templateGo.gameObject.SetActive(false);
            var templateBg = templateGo.gameObject.AddComponent<Image>();
            templateBg.sprite = QcUi.Sprite(QcUi.CardSurface);
            templateBg.type = Image.Type.Sliced;
            if (templateBg.sprite == null) templateBg.color = QcUi.Cream;
            var templateScroll = templateGo.gameObject.AddComponent<ScrollRect>();
            var item = QcUi.Anchor(templateGo, "Item",
                new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(0, -90), new Vector2(0, 0));
            var itemToggle = item.gameObject.AddComponent<Toggle>();
            var itemLabelGo = QcUi.Stretch(item, "Label");
            var itemLabel = itemLabelGo.gameObject.AddComponent<TextMeshProUGUI>();
            itemLabel.fontSize = QcUi.TextSmall;
            itemLabel.color = QcUi.Ink;
            itemLabel.alignment = TextAlignmentOptions.Center;
            drop.template = templateGo;
            drop.itemText = itemLabel;
            var content = QcUi.Anchor(templateGo, "Content",
                new Vector2(0, 1), new Vector2(1, 1), Vector2.zero, Vector2.zero);
            var vl = content.gameObject.AddComponent<VerticalLayoutGroup>();
            vl.childForceExpandWidth = true;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit =
                ContentSizeFitter.FitMode.PreferredSize;
            templateScroll.content = content;
            templateScroll.viewport = templateGo;
            item.SetParent(content, false);
            itemToggle.graphic = null;
            drop.options.Clear();
            foreach (var o in options) drop.options.Add(new TMP_Dropdown.OptionData(o));
            var ids = new[] { "uk", "en", "de" };
            drop.SetValueWithoutNotify(Math.Max(0, Array.IndexOf(ids, currentId)));
            drop.RefreshShownValue();
            drop.onValueChanged.AddListener(v => onChange(v));
            return drop;
        }
    }
}
