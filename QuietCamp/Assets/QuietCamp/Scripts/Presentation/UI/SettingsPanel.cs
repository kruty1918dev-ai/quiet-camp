using System;
using System.Text;
using System.Collections;
using UnityEngine.UI;
using Kruty1918.Audio;
using QuietCamp.Application;
using UnityEngine;

namespace QuietCamp.Presentation.UI
{
    /// <summary>Which category and optional privacy topic the sheet shows.
    /// Owned by the host so navigation survives
    /// re-renders but resets whenever the sheet re-opens.</summary>
    public sealed class SettingsNav
    {
        public string Category;
        public string Section;
        public bool Advanced, PrivacyDetails, Licenses, DataInformation;
        internal int DisclosureRevision;
        public void Open(string category, string section = null)
        {
            Category = category; Section = section;
            Advanced = PrivacyDetails = Licenses = DataInformation = false;
            DisclosureRevision++;
        }
        public bool Back()
        {
            if (Section != null) { Open(Category); return true; }
            if (Category != null) { Open(null); return true; }
            return false;
        }
    }

    /// <summary>
    /// Mobile-first settings: a category list first (one tap to reach any
    /// group — no endless scroll), then focused sections with full-width
    /// rows. Identical markup in menu and camp, backed by saved settings.
    /// </summary>
    public static class SettingsPanel
    {
        static readonly string[] Categories = { "sound", "comfort", "look", "extras", "privacy" };
        static readonly string[] CatIcons = { "sound", "quiet", "settings", "hint", "lock" };

        public static string CategoryKey(SettingsNav nav)
            => nav?.Category == null ? null : nav.Section != null ? "legal.section." + nav.Section : "settings.cat." + nav.Category;

        public static void Navigate(HtmlSurface surface, SettingsNav nav, string category, string section = null)
        {
            surface.FlushSettingsSave(); nav.Open(category, section); surface.Refresh();
            var revision = nav.DisclosureRevision;
            surface.StartCoroutine(Top());
            IEnumerator Top()
            {
                yield return null; yield return null;
                if (surface == null || nav.DisclosureRevision != revision) yield break;
                var scroll = surface.Element("sheet-scroll")?.GetComponent<ScrollRect>();
                if (scroll != null) { scroll.velocity = Vector2.zero; scroll.verticalNormalizedPosition = 1; }
            }
        }

        public static string Render(GameServices services, HtmlSurface surface, SettingsNav nav, string extra = "")
        {
            var s = services.Settings;
            var html = new StringBuilder();
            string T(string key) => services.Localization.T(key);
            if (nav == null) nav = new SettingsNav();

            void Slider(string key, float value, Action<float> change,
                float min = 0, float max = 1, string format = "percent")
            {
                var lastValue = value;
                surface.Callbacks.BindNumber(key, v => { lastValue = v; change(v); surface.ScheduleSettingsSave(); });
                surface.Callbacks.Bind(key + ".end", () =>
                {
                    RecordSetting(services, key, lastValue);
                    surface.FlushSettingsSave();
                    services.Audio?.Play("ui.select", new AudioPlayOptions(volumeScale: .5f));
                    surface.Refresh();
                });
                html.Append("<view class=\"setting\"><view class=\"s-head\"><text class=\"s-label\">")
                    .Append(HtmlUi.Escape(T(key))).Append("</text></view>")
                    // Apply bounds before value: Unity's default 0..1 would clamp a restored 130% value.
                    .Append("<slider id=\"").Append(key).Append("\" min=\"").Append(HtmlUi.Number(min))
                    .Append("\" max=\"").Append(HtmlUi.Number(max)).Append("\" value=\"").Append(HtmlUi.Number(value))
                    .Append("\" format=\"").Append(format).Append("\" onChange=\"Globals.campUi.Number('").Append(key)
                    .Append("', event)\" onEndChange=\"Globals.campUi.Click('").Append(key).Append(".end')\" /></view>");
            }
            void Toggle(string key, bool value, Action<bool> change, bool sub = false)
            {
                surface.Callbacks.BindToggle(key, v => { change(v); RecordSetting(services, key, v ? 1 : 0); services.Save.Save(); surface.Refresh(); });
                html.Append("<view class=\"toggle-row\"><view class=\"tr-text\"><label for=\"#").Append(key)
                    .Append("\">").Append(HtmlUi.Escape(T(key))).Append("</label>");
                if (sub) html.Append(HtmlUi.Text(T(key + ".sub"), "s-sub"));
                html.Append("</view><switch id=\"").Append(key)
                    .Append("\" checked=\"").Append(value ? "true" : "false")
                    .Append("\" onChange=\"Globals.campUi.Toggle('").Append(key).Append("', event)\" /></view>");
            }

            var navigation = new StringBuilder("<view class=\"set-cats\">");
            var secondaryNavigation = new StringBuilder("<view class=\"settings-secondary\">");
            for (var i = 0; i < Categories.Length; i++)
            {
                var cat = Categories[i]; var cid = "set-cat-" + cat;
                if (cat == "extras" && !services.Tutorial.RewardOwned && !services.Rewards.Owned) continue;
                surface.Callbacks.Bind(cid, () =>
                {
                    services.Audio?.Play("ui.select"); Navigate(surface, nav, cat);
                });
                var groupNavigation = i < 3 ? navigation : secondaryNavigation;
                groupNavigation.Append("<button id=\"").Append(cid).Append("\" class=\"set-cat ")
                    .Append(nav.Category == cat ? "selected" : "")
                    .Append(i >= 3 ? " secondary-category" : "")
                    .Append("\" onClick=\"Globals.campUi.Click('").Append(cid).Append("')\">")
                    .Append("<view class=\"sc-symbol\">").Append(CampIcons.Mark(CatIcons[i])).Append("</view>")
                    .Append("<view class=\"sc-text\">").Append(HtmlUi.Text(T("settings.cat." + cat), "sc-name"))
                    .Append(HtmlUi.Text(T("settings.summary." + cat), "sc-summary")).Append("</view>")
                    .Append("<view class=\"sc-chevron\">").Append(CampIcons.Mark("back")).Append("</view></button>");
            }
            navigation.Append("</view>");
            secondaryNavigation.Append("</view>");
            string Language()
            {
                var output = new StringBuilder(HtmlUi.Text(T("settings.language"), "settings-group-title"));
                output.Append("<view class=\"row language-options\">");
                var languages = new[] { "uk", "en", "de" };
                var names = new[] { "Українська", "English", "Deutsch" };
                for (int i = 0; i < languages.Length; i++)
                {
                    var language = languages[i];
                    output.Append(HtmlUi.Button(surface, "language-" + language, names[i], () =>
                    {
                        services.Analytics.Setting(ComfortSetting.Language, language == "uk" ? 0 : language == "en" ? 1 : 2);
                        services.Localization.TrySetLanguage(language); s.language = services.Localization.CurrentLanguageId;
                        services.Save.Save(); surface.Refresh();
                    }, s.language == language ? "selected" : "quiet"));
                }
                return output.Append("</view>").ToString();
            }
            void Group(string key) => html.Append(HtmlUi.Text(T(key), "settings-group-title"));
            string Layout(string body)
            {
                if (nav.Category == null)
                    return "<scroll id=\"sheet-scroll\" class=\"settings-root-scroll\"><view class=\"settings-home\">"
                        + "<view class=\"settings-home-navigation\">" + navigation + "</view>"
                        + "<view class=\"settings-overview\">" + secondaryNavigation + body + "</view></view></scroll>";
                return "<view class=\"settings-layout\"><scroll id=\"settings-nav-scroll\" class=\"settings-navigation\">" + navigation + secondaryNavigation
                    + "</scroll><scroll id=\"sheet-scroll\" class=\"settings-detail-scroll\"><view class=\"settings-content\">"
                        + body + extra + "</view></scroll></view>";
            }
            if (nav.Category == null)
            {
                html.Append(Language());
                html.Append(HtmlUi.Button(surface, "tutorial-learn-again", T("guide.learn-again"), () =>
                    services.Actions.Execute(new Kruty1918.UIActions.API.UiActionRequest(new Kruty1918.UIActions.API.UiActionId("qc.tutorial.restart"),
                        Kruty1918.UIActions.API.UiActionSource.Button, "Settings")), "quiet guide-learning"));
                html.Append(HtmlUi.Text(T("guide.learning.help"), "s-sub guide-learning-explain"));
                if (!services.Economy.IsPro)
                    html.Append(HtmlUi.Button(surface, "help-supplies", T("economy.title"), () =>
                        services.Actions.Execute(new Kruty1918.UIActions.API.UiActionRequest(new Kruty1918.UIActions.API.UiActionId("qc.economy"),
                            Kruty1918.UIActions.API.UiActionSource.Button, "Settings")), "quiet"));
                return Layout(html.ToString());
            }

            switch (nav.Category)
            {
                case "privacy":
                    html.Append(PrivacyPanel.Render(services, surface, nav));
                    break;
                case "extras":
                    html.Append(HtmlUi.Text(T("settings.extras.help"), "s-sub"));
                    if (services.Tutorial.RewardOwned)
                        Toggle("settings.trailPennant", s.trailPennant, v => s.trailPennant = v, sub: true);
                    if (services.Rewards.Owned)
                        Toggle("settings.lantern", s.fireflyLantern, v => s.fireflyLantern = v, sub: true);
                    if (services.Ads.IsReady)
                    {
                        Toggle("settings.videoBonuses", s.optionalVideoBonuses, v => s.optionalVideoBonuses = v, sub: true);
                        html.Append(VideoBonusPanel.Render(services, surface, "album.lantern"));
                    }
                    if (services.Ads is IAdPrivacyOptions privacy && privacy.PrivacyOptionsRequired)
                        html.Append(HtmlUi.Button(surface, "ads-privacy", T("privacy.adsOptions"), async () => { await privacy.ShowPrivacyOptions(); surface.Refresh(); }, "quiet"));
                    break;
                case "sound":
                    Slider("settings.master", s.master,
                        v => { s.master = v; services.Audio?.SetBusVolume(AudioBus.Master, v); });
                    Slider("settings.music", s.music,
                        v => { s.music = v; services.Audio?.SetBusVolume(AudioBus.Music, v); });
                    Slider("settings.ambience", s.ambience,
                        v => { s.ambience = v; services.Audio?.SetBusVolume(AudioBus.Ambience, v); });
                    Slider("settings.effects", s.effects,
                        v => { s.effects = v; services.Audio?.SetBusVolume(AudioBus.Ui, v); services.Audio?.SetBusVolume(AudioBus.Sfx, v); });
                    break;
                case "comfort":
                    Group("settings.readability");
                    Slider("settings.textSize", s.textScale,
                        v => { s.textScale = Mathf.Clamp(v, .85f, 1.3f); LocalizedLabel.TextScale = s.textScale; }, .85f, 1.3f);
                    Toggle("settings.contrast", s.highContrast, v => s.highContrast = v, sub: true);
                    Group("settings.motionTouch");
                    Toggle("settings.reducedMotion", s.reducedMotion, v => services.ReducedMotion = v, sub: true);
                    Toggle("settings.calm", s.calmMode, v => services.CalmMode = v, sub: true);
                    Toggle("settings.haptics", s.haptics, v => services.HapticsEnabled = v);
                    html.Append(HtmlUi.Button(surface, "settings-advanced", T("settings.additional"), () =>
                    { ChangeDisclosure(surface, nav, () => nav.Advanced = !nav.Advanced); }, "settings-disclosure"));
                    if (nav.Advanced) Slider("settings.scroll", (s.scrollSensitivity - 3f) / 21f, v => s.scrollSensitivity = 3f + v * 21f);
                    break;
                case "look":
                    Group("settings.quality");
                    html.Append(HtmlUi.Text(T("settings.quality.help"), "s-sub"));
                    for (int row = 0; row < 2; row++)
                    {
                        html.Append("<view class=\"row settings-options\">");
                        for (int col = 0; col < 2; col++)
                        {
                            int choice = row * 2 + col;
                            html.Append(HtmlUi.Button(surface, "quality-" + choice, T("quality." + choice), () =>
                            { services.Analytics.Setting(ComfortSetting.Quality, choice); s.quality = choice; surface.ScheduleSettingsSave(); surface.Refresh(); },
                                s.quality == choice ? "selected" : "quiet"));
                        }
                        html.Append("</view>");
                    }
                    Group("settings.orientation");
                    for (int row = 0; row < 2; row++)
                    {
                        html.Append("<view class=\"row settings-options\">");
                        for (int column = 0; column < 2; column++)
                        {
                            int choice = row * 2 + column;
                            html.Append(HtmlUi.Button(surface, "orientation-" + choice, T("orientation." + choice), () =>
                            {
                                World.CampSceneHost.Current?.CancelActiveDrag(); s.orientation = choice;
                                ScreenOrientationPolicy.Apply(choice); services.Save.Save(); surface.Refresh();
                            }, "orientation-choice " + (s.orientation == choice ? "selected" : "quiet")));
                        }
                        html.Append("</view>");
                    }
                    break;
            }
            return Layout(html.ToString());
        }

        public static void ChangeDisclosure(HtmlSurface surface, SettingsNav nav, Action change)
        {
            var scroll = surface.Element("sheet-scroll")?.GetComponent<ScrollRect>();
            var offset = scroll != null && scroll.content != null ? scroll.content.anchoredPosition.y : 0;
            var revision = ++nav.DisclosureRevision;
            var category = nav.Category;
            var section = nav.Section;
            change(); surface.Refresh();
            surface.StartCoroutine(RestoreOffset());
            IEnumerator RestoreOffset()
            {
                // Wait for the refreshed document and its Yoga geometry. Preserve
                // distance from the top, rather than jumping to a longer license's end.
                yield return null; yield return null;
                if (surface == null || nav.DisclosureRevision != revision || nav.Category != category || nav.Section != section) yield break;
                var current = surface.Element("sheet-scroll")?.GetComponent<ScrollRect>();
                if (current == null || current.content == null || current.viewport == null) yield break;
                Canvas.ForceUpdateCanvases();
                var range = Mathf.Max(0, current.content.rect.height - current.viewport.rect.height);
                current.velocity = Vector2.zero;
                current.verticalNormalizedPosition = range > 0 ? Mathf.Clamp01(1 - offset / range) : 1;
            }
        }

        public static string Shell(GameServices services, HtmlSurface surface, SettingsNav nav, string body, Action back, Action close, string exit = "")
        {
            string T(string key) => services.Localization.T(key);
            var category = CategoryKey(nav);
            var header = category == null ? "" : HtmlUi.Text(T(nav.Section == null ? "menu.settings" : "settings.cat." + nav.Category), "settings-context");
            header += HtmlUi.Text(T(category ?? "menu.settings"), "settings-title");
            return "<view class=\"settings-zone\" data-safe-area=\"all\"><view id=\"sheet\" class=\"surface settings-panel "
                + (category == null ? "settings-root" : "settings-category")
                + (nav?.Category == "privacy" && nav.Section == null ? " settings-topic-overview" : "")
                + "\" data-motion-role=\"dialog\"" + exit + ">"
                + "<view class=\"settings-header\">" + CampIcons.Button(surface, "back", "back", back, "nav-back", T("action.back"))
                + "<view class=\"settings-heading\">" + header + "</view>"
                + (category == null ? "" : CampIcons.Button(surface, "settings-close", "remove", close, "settings-close", T("settings.close")))
                + "</view>" + body + "</view></view>";
        }
        static void RecordSetting(GameServices services, string key, float value)
        {
            ComfortSetting? setting = key switch {
                "settings.master" or "settings.music" or "settings.ambience" or "settings.effects" => ComfortSetting.Sound,
                "settings.reducedMotion" => ComfortSetting.Motion, "settings.calm" => ComfortSetting.Calm,
                "settings.contrast" => ComfortSetting.Contrast, "settings.haptics" => ComfortSetting.Haptics,
                "settings.textSize" => ComfortSetting.Text, "settings.scroll" => ComfortSetting.Scroll, _ => null };
            if (setting.HasValue) services.Analytics.Setting(setting.Value,
                Mathf.RoundToInt((setting.Value==ComfortSetting.Text?(value-.85f)/.45f:value) * 10));
        }
    }
}
