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
            // Volume sliders earn one quiet sample after the drag ends —
            // never a click per tick of onValueChanged.
            void ReleaseCue() => services.Audio?.Play("ui.select",
                new Kruty1918.Audio.AudioPlayOptions(volumeScale: .5f));
            AddGroupHeader(content, "settings.group.audio");
            AddSliderRow(content, "settings.music",
                services.Settings.music, v => SetBus(services, Kruty1918.Audio.AudioBus.Music, v),
                onRelease: ReleaseCue);
            AddSliderRow(content, "settings.ambience",
                services.Settings.ambience, v => SetBus(services, Kruty1918.Audio.AudioBus.Ambience, v),
                onRelease: ReleaseCue);
            AddSliderRow(content, "settings.effects",
                services.Settings.effects, v =>
                {
                    SetBus(services, Kruty1918.Audio.AudioBus.Ui, v);
                    SetBus(services, Kruty1918.Audio.AudioBus.Sfx, v);
                }, onRelease: ReleaseCue);

            AddGroupHeader(content, "settings.group.comfort");
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

            AddGroupHeader(content, "settings.group.look");
            AddChoiceRow(content, "settings.language",
                new[] { "Українська", "English", "Deutsch" },
                new[] { "uk", "en", "de" }, services.Settings.language,
                v =>
                {
                    var ids = new[] { "uk", "en", "de" };
                    services.Localization.TrySetLanguage(ids[Mathf.Clamp(v, 0, 2)]);
                    services.Settings.language = services.Localization.CurrentLanguageId;
                    services.Save.Save();
                });
            AddSliderRow(content, "settings.textSize",
                services.Settings.textScale, v =>
                {
                    services.Settings.textScale = Mathf.Clamp(v, 0.85f, 1.3f);
                    LocalizedLabel.TextScale = services.Settings.textScale;
                    services.Save.Save();
                }, 0.85f, 1.3f);
        }

        /// <summary>Muted section label separating the settings groups.</summary>
        static void AddGroupHeader(RectTransform parent, string key)
        {
            var row = QcUi.Root(parent, "group_" + key);
            row.gameObject.AddComponent<LayoutElement>().minHeight = 64;
            var label = QcUi.Anchor(row, "l",
                new Vector2(0, 0.2f), new Vector2(1, 1), Vector2.zero, Vector2.zero);
            QcUi.Label(label, key, QcUi.TextSmall - 2f,
                TextAlignmentOptions.MidlineLeft, QcUi.GreenDark);
            var line = QcUi.Anchor(row, "line",
                new Vector2(0, 0), new Vector2(1, 0),
                new Vector2(0, 0), new Vector2(0, 2));
            var img = line.gameObject.AddComponent<Image>();
            img.color = new Color(QcUi.Ink.r, QcUi.Ink.g, QcUi.Ink.b, 0.18f);
            img.raycastTarget = false;
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
            Action<float> onChange, float min = 0f, float max = 1f, Action onRelease = null)
        {
            var row = QcUi.Root(parent, "row_" + key);
            row.sizeDelta = new Vector2(0, 96);
            row.gameObject.AddComponent<LayoutElement>().minHeight = 96;
            var label = QcUi.Anchor(row, "l",
                new Vector2(0, 0), new Vector2(0.45f, 1), Vector2.zero, Vector2.zero);
            QcUi.Label(label, key, QcUi.TextSmall, TextAlignmentOptions.MidlineLeft, QcUi.Ink);
            var sliderGo = QcUi.Anchor(row, "s",
                new Vector2(0.45f, 0.2f), new Vector2(1, 0.8f), Vector2.zero, Vector2.zero);
            var slider = BuildSlider(sliderGo, min, max, value, onChange);
            if (onRelease != null)
            {
                var trigger = sliderGo.gameObject.AddComponent<UnityEngine.EventSystems.EventTrigger>();
                var entry = new UnityEngine.EventSystems.EventTrigger.Entry
                {
                    eventID = UnityEngine.EventSystems.EventTriggerType.PointerUp,
                };
                entry.callback.AddListener(_ => onRelease());
                trigger.triggers.Add(entry);
            }
            return slider;
        }

        public static Slider BuildSlider(RectTransform parent, float min, float max,
            float value, Action<float> onChange)
        {
            var slider = parent.gameObject.AddComponent<Slider>();
            slider.minValue = min; slider.maxValue = max;
            // Flat track + fill + one round thumb — the Kenney slide sprites
            // read as decorative dumbbells when sliced at these proportions.
            var track = QcUi.Anchor(parent, "track",
                new Vector2(0, 0.5f), new Vector2(1, 0.5f),
                new Vector2(0, -9), new Vector2(0, 9));
            var bg = track.gameObject.AddComponent<Image>();
            bg.color = new Color(QcUi.Ink.r, QcUi.Ink.g, QcUi.Ink.b, 0.16f);
            bg.raycastTarget = false;
            var fillArea = QcUi.Stretch(parent, "FillArea");
            var fill = QcUi.Anchor(fillArea, "Fill",
                new Vector2(0, 0.5f), new Vector2(0, 0.5f),
                new Vector2(0, -9), new Vector2(0, 9));
            var fillImg = fill.gameObject.AddComponent<Image>();
            fillImg.color = QcUi.GreenDark;
            fillImg.raycastTarget = false;
            var handle = QcUi.Anchor(parent, "handle",
                new Vector2(0, 0.5f), new Vector2(0, 0.5f),
                new Vector2(-30, -30), new Vector2(30, 30));
            var handleImg = handle.gameObject.AddComponent<Image>();
            handleImg.sprite = QcUi.Sprite(QcUi.IconCircle);
            handleImg.preserveAspect = true;
            if (handleImg.sprite == null) handleImg.color = QcUi.GreenDark;
            var rim = QcUi.Anchor(handle, "rim",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-34, -34), new Vector2(34, 34));
            var rimImg = rim.gameObject.AddComponent<Image>();
            rimImg.sprite = QcUi.Sprite(QcUi.IconCircleOutline);
            rimImg.preserveAspect = true;
            rimImg.color = QcUi.Cream;
            rimImg.raycastTarget = false;
            rim.transform.SetAsFirstSibling();
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
                new Vector2(1, 0.5f), new Vector2(1, 0.5f),
                new Vector2(-80, -38), new Vector2(-8, 38));
            var box = boxGo.gameObject.AddComponent<Image>();
            box.sprite = QcUi.Sprite(QcUi.Checkbox);
            if (box.sprite == null) box.color = QcUi.CreamDark;
            var toggle = boxGo.gameObject.AddComponent<Toggle>();
            // The kit ships a pre-composited checked square — show it whole.
            var check = QcUi.Stretch(boxGo, "check");
            var checkImg = check.gameObject.AddComponent<Image>();
            checkImg.sprite = QcUi.Sprite(QcUi.CheckboxChecked);
            checkImg.preserveAspect = true;
            if (checkImg.sprite == null)
            {
                checkImg.sprite = QcUi.Sprite(QcUi.IconCheck);
                checkImg.color = QcUi.Green;
            }
            checkImg.raycastTarget = false;
            toggle.graphic = checkImg;
            toggle.targetGraphic = box;
            toggle.SetIsOnWithoutNotify(value);
            toggle.onValueChanged.AddListener(v => onChange(v));
            return toggle;
        }

        /// <summary>
        /// Segmented choice row: one readable button per option under the row
        /// label, current selection tinted dark — replaces the fragile
        /// TMP_Dropdown template entirely (nothing overlaps, no popup to
        /// escape the safe area). Returns the option buttons in order.
        /// </summary>
        public static Button[] AddChoiceRow(RectTransform parent, string key,
            string[] options, string[] ids, string currentId, Action<int> onChange)
        {
            var row = QcUi.Root(parent, "row_" + key);
            row.gameObject.AddComponent<LayoutElement>().minHeight = 164;
            var label = QcUi.Anchor(row, "l",
                new Vector2(0, 0.6f), new Vector2(1, 1), Vector2.zero, Vector2.zero);
            QcUi.Label(label, key, QcUi.TextSmall, TextAlignmentOptions.MidlineLeft, QcUi.Ink);
            var seg = QcUi.Anchor(row, "seg",
                new Vector2(0, 0), new Vector2(1, 0.6f), Vector2.zero, Vector2.zero);
            var layout = seg.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 10;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;

            var selected = Mathf.Max(0, Array.IndexOf(ids, currentId));
            var buttons = new Button[options.Length];
            void Restyle()
            {
                for (var i = 0; i < buttons.Length; i++)
                {
                    var img = buttons[i].GetComponent<Image>();
                    var tmp = buttons[i].GetComponentInChildren<TextMeshProUGUI>();
                    var on = i == selected;
                    if (img != null) img.color = on ? QcUi.GreenDark : QcUi.Cream;
                    if (tmp != null) tmp.color = on ? QcUi.Cream : QcUi.Ink;
                }
            }
            for (var i = 0; i < options.Length; i++)
            {
                var img = QcUi.Sliced(seg, "opt_" + i, QcUi.BtnSecondary, QcUi.Cream);
                img.raycastTarget = true;
                var btn = img.gameObject.AddComponent<Button>();
                var tmp = QcUi.PlainText(img.rectTransform, options[i],
                    QcUi.TextSmall - 4f, TextAlignmentOptions.Center, QcUi.Ink);
                tmp.textWrappingMode = TextWrappingModes.NoWrap;
                buttons[i] = btn;
                var index = i;
                btn.onClick.AddListener(() =>
                {
                    if (index == selected) return;
                    selected = index;
                    Restyle();
                    onChange(index);
                });
            }
            Restyle();
            return buttons;
        }
    }
}
