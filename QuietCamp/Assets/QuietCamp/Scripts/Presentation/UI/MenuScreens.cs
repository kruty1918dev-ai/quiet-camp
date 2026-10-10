using System;
using System.Collections.Generic;
using System.Text;
using Kruty1918.UIActions.API;
using QuietCamp.Infrastructure;
using QuietCamp.Application;
using QuietCamp.Domain;
using UnityEngine;

namespace QuietCamp.Presentation.UI
{
    /// <summary>
    /// Menu screens: the Main actions live on the safe-area surface; sheets
    /// (Levels/Album/Settings/DemoComplete) render into a full-screen overlay
    /// on CanvasRoot — a dim button covers the whole display and an outside
    /// tap backs out with the declarative exit motion.
    /// </summary>
    public sealed class MenuScreens : IDisposable
    {
        readonly GameServices _services;
        readonly HtmlSurface _surface;
        readonly HtmlSurface _overlay;
        readonly HtmlSurface _bleed;
        readonly CanvasGroup _menuVisibility, _atmosphereVisibility;
        public HtmlSurface Overlay => _overlay;
        public bool UiReady => _surface != null && _surface.IsUsable
            && (Current == "Main" || Current == "AlbumQuiet" || (_overlay?.IsUsable ?? false));
        public bool PreparationReady => _surface != null && _surface.IsUsable
            && (_overlay?.IsUsable ?? true) && (_bleed?.IsUsable ?? true);
        public event Action<string> ScreenChanged;
        public event Action<int> AlbumSelected;
        public event Action OverlayMounted;

        readonly Stack<string> _history = new Stack<string>();
        readonly SettingsNav _setNav = new SettingsNav();
        bool _confirmReset;
        bool _confirmErase, _erasing;
        bool _closing;
        EconomyPanel _economyPanel;
        public event Action MemoryReplay;
        public string Current { get; private set; } = "Main";

        public MenuScreens(GameServices services, RectTransform safeArea)
        {
            _services = services;
            services.Tutorial.Changed += RefreshAll;
            services.MonetizationChanged += RefreshAll;
            var controls=QcUi.Stretch(safeArea,"Menu controls visibility");
            _menuVisibility=controls.gameObject.AddComponent<CanvasGroup>();
            _surface = HtmlSurface.Create(controls, "MenuHtml", services, Render);
            _surface.LayoutChanged += RefreshAll;
            _surface.Mounted += UpdateMenuVisibility;
            var canvasRoot = safeArea.parent as RectTransform;
            if (canvasRoot != null)
            {
                var atmosphere=QcUi.Stretch(canvasRoot,"Menu atmosphere visibility");
                atmosphere.SetSiblingIndex(0);
                _atmosphereVisibility=atmosphere.gameObject.AddComponent<CanvasGroup>();
                _bleed = HtmlSurface.Create(atmosphere, "MenuAtmosphere", services, () =>
                    "<view class=\"app\"><view class=\"menu-atmosphere\" /></view>");
                _bleed.transform.SetSiblingIndex(0);
                _bleed.Mounted += UpdateMenuVisibility;
                _overlay = HtmlSurface.Create(canvasRoot, "MenuOverlay", services, RenderOverlay);
                _economyPanel = new EconomyPanel(services, _overlay);
                _overlay.Mounted += OnOverlayMounted;
                _overlay.Motion.ExitFinished += OnExitFinished;
            }
            if (services.Analytics.Available && !services.Analytics.ChoiceCurrent && !services.PrivacyNoticePresented)
            {
                services.PrivacyNoticePresented = true;
                Show("PrivacyNotice");
            }
        }
        string T(string key) => _services.Localization.T(key);
        string OverlayButton(string id, string key, Action click, string cls = "", bool enabled = true)
            => HtmlUi.Button(_overlay, id, T(key), click, cls, enabled);
        void Action(string id, object payload = null)
        {
            _services.Audio?.Play("ui.click");
            _services.Actions.Execute(new UiActionRequest(new UiActionId(id), UiActionSource.Button, "Menu", null, payload));
        }
        static void Visibility(CanvasGroup group,bool visible)
        {
            if(group==null)return;
            // Native wrappers live outside the HTML-owned root. Keep masked
            // Graphics enabled so their stencil/gradient cache keys remain stable.
            group.alpha=visible?1:0;group.interactable=visible;group.blocksRaycasts=visible;
        }
        void UpdateMenuVisibility()
        {
            Visibility(_menuVisibility,Current=="Main");
            Visibility(_atmosphereVisibility,Current!="Levels"&&Current!="Album"&&Current!="AlbumQuiet");
        }
        void RefreshAll() { UpdateMenuVisibility(); _surface.Refresh(); _overlay?.Refresh(); _bleed?.Refresh(); }
        string Render()
        {
            // Keep the camp visible: a small wordmark above the clearing,
            // one primary action and two compact destinations below it.
            // All sheets live on the separate full-screen overlay.
            var levels = RoadmapPilotPolicy.LevelIds;
            var nextId = _services.ContinueLevel() ?? levels[0];
            int done=0;foreach(var id in levels)if(_services.PilotCompleted(id))done++;
            var albumCount = _services.Save.Album.entries?.Length ?? 0;
            var ctaTitle = done > 0 || !string.IsNullOrEmpty(_services.Save.Session.levelId) ? T("menu.continue") : T("menu.start");
            var ctaSub = string.Format(T("menu.cta.progress"),
                Mathf.Min(done + 1, levels.Count), LevelDisplay.Title(nextId, _services.Localization, _services.Journeys));
            var content =
                ""
                + "<view class=\"top menu-top\" id=\"menu-header\" data-motion-role=\"edge-top\">"
                + OrbButton("settings", "⚙", T("menu.settings"), () => Action("qc.settings"))
                + "</view>"
                + "<view class=\"brand\" id=\"menu-brand\" data-motion-role=\"edge-top\" data-motion-delay=\"0.04\">"
                + "<img class=\"brand-sprig\" src=\"res:QuietCamp/UI/Atlas/leaf_c\"/>"
                + HtmlUi.Text(T("menu.title.line1"), "brand-title")
                + HtmlUi.Text(T("menu.title.line2"), "brand-title") + "</view>"
                + "<view class=\"grow\"></view>"
                + "<view class=\"menu-actions\" id=\"menu-actions\" data-motion-role=\"edge-bottom\" data-motion-delay=\"0.08\">"
                + (_services.Tutorial.NeedsMenuIntro ? MenuGuideIntro() : "")
                + Cta(ctaTitle, ctaSub)
                + "<view class=\"menu-cards\">"
                + (true ? MenuCard("levels", "map", T("menu.levels"), done + "/" + levels.Count,
                    () => Action("qc.levels"), levels.Count > 0 ? (float)done / levels.Count : 0) : "")
                + (_services.Economy.IsPro || albumCount > 0 || _services.Tutorial.AlbumUnlocked ? MenuCard("album", "tent", T("menu.album.short"), albumCount == 0 ? T("menu.album.empty") : string.Format(T("menu.album.saved"), albumCount),
                    () => Action("qc.album")) : "")
                + "</view></view>";
            return HtmlUi.Template("Menu", "content", content);
        }

        /// <summary>One-time orientation for fresh players: where to play next
        /// and what the personal camp is. Dismissed once, persisted.</summary>
        string MenuGuideIntro()
        {
            void Dismiss() { _services.Tutorial.MarkMenuIntroSeen(); RefreshAll(); }
            return "<view id=\"menu-guide\" class=\"guide-card menu-guide\">"
                + "<img class=\"guide-portrait\" src=\"res:QuietCamp/UI/Mentor/welcome\"/>"
                + "<view class=\"guide-copy\">" + HtmlUi.Text(T("guide.name"), "guide-name")
                + HtmlUi.Text(T("guide.menu.intro"), "guide-instruction")
                + "</view>"
                + HtmlUi.Button(_surface, "menu-guide-ok", T("guide.menu.ok"), Dismiss, "primary guide-ok")
                + "</view>";
        }

        string OrbButton(string id, string icon, string tooltip, Action click)
        {
            var scale = _surface.Viewport.PixelScale;
            var dpi = Screen.dpi > 0 ? Screen.dpi / 160f : 2.5f;
            var diameter = Mathf.Max(144, 48 * dpi / Mathf.Max(.1f, Mathf.Min(scale.x > 0 ? scale.x : 1, scale.y > 0 ? scale.y : 1)));
            var size = HtmlUi.Number(diameter);
            return CampIcons.Button(_surface, id, "settings", click, "orb quiet", tooltip: tooltip)
                .Replace("class=\"orb quiet\"", "class=\"orb quiet\" style=\"width:" + size + "px;min-width:" + size
                    + "px;height:" + size + "px;min-height:" + size + "px;border-radius:" + HtmlUi.Number(diameter * .5f) + "px\"");
        }

        string Cta(string title, string sub)
        {
            _surface.Callbacks.Bind("continue", () => Action("qc.continue"));
            return "<button id=\"continue\" class=\"cta primary\" onClick=\"Globals.campUi.Click('continue')\">"
                + "<view class=\"cta-symbol\">" + CampIcons.Mark("play") + "</view><view class=\"cta-text\">"
                + HtmlUi.Text(title, "cta-title") + HtmlUi.Text(sub, "cta-sub")
                + "</view></button>";
        }

        string MenuCard(string id, string icon, string title, string sub, Action click, float progress = -1)
        {
            _surface.Callbacks.Bind(id, click);
            return "<button id=\"" + id + "\" class=\"menu-card\" onClick=\"Globals.campUi.Click('"
                + id + "')\">" + "<view class=\"mc-main\">"
                + "<view class=\"mc-symbol\">" + CampIcons.Mark(icon) + "</view>"
                + "<view class=\"mc-text\">" + HtmlUi.Text(title, "mc-title")
                + HtmlUi.Text(sub, "mc-sub")
                + (progress >= 0 ? "<view class=\"mc-progress\"><view class=\"mc-progress-fill\" style=\"width:" + HtmlUi.Number(Mathf.Clamp01(progress) * 100) + "%\" /></view>" : "")
                + "</view></view></button>";
        }
        string RenderOverlay()
        {
            if (Current == "Main") return "<view />";
            if (Current == "Levels") return Roadmap();
            if (Current == "Album") return FullMenu();
            if (Current == "AlbumQuiet") return "<view class=\"app album-quiet\" data-safe-area=\"all\" id=\"sheet\" data-motion-role=\"dialog\""
                + (_closing ? " data-motion=\"exit\"" : "") + "><view id=\"album-viewport\" class=\"quiet-viewport\" />"
                + OverlayButton("quiet-back", "action.back", Back, "quiet quiet-back") + "</view>";
            var exit = _closing ? " data-motion=\"exit\"" : "";
            var sb = new StringBuilder("<view class=\"app dimroot\">");
            sb.Append("<button id=\"dim\" class=\"dim\" data-motion-role=\"scrim\"" + exit
                + " onClick=\"Globals.campUi.Click('dim')\"></button>");
            if (Current == "Settings")
            {
                _overlay.Callbacks.Bind("dim", Back);
                return sb.Append(SettingsPanel.Shell(_services, _overlay, _setNav, Settings(), SheetBack, Back, exit))
                    .Append("</view>").ToString();
            }
            var catKey = Current == "Settings" ? SettingsPanel.CategoryKey(_setNav) : null;
            var title = catKey ?? (Current == "Economy" ? "economy.title" : Current == "PrivacyNotice" ? "settings.cat.privacy" : Current == "Levels" ? "menu.levels" : Current == "Album" ? "menu.album" : Current == "Settings" ? "menu.settings" : "demo.complete");
            var body = Current == "Economy" ? _economyPanel.Render() : Current == "PrivacyNotice" ? PrivacyPanel.Notice(_services, _overlay, Back) : Current == "Album" ? Album() : Current == "Settings" ? Settings() : OverlayButton("demo-album", "menu.album", () => Action("qc.album"));
            // Top ‹ is the sole back control; inside a settings category it
            // steps up to the category list instead of leaving the sheet.
            sb.Append(HtmlUi.Template("Sheet",
                    "nav", HtmlUi.Button(_overlay, "back", "‹", SheetBack, "nav-back",
                        tooltip: T("action.back")),
                    "title", HtmlUi.Escape(T(title)), "body", body, "footer", "",
                    "body-id", "menu-body-" + Current + "-" + (_setNav.Category ?? "root"))
                .Replace("data-motion-role=\"edge-bottom\"",
                    "data-motion-role=\"edge-bottom\"" + exit));
            sb.Append("</view>");
            _overlay.Callbacks.Bind("dim", () => { if (Current != "Main") Back(); });
            return sb.ToString();
        }
        void OnOverlayMounted() => OverlayMounted?.Invoke();
        string Roadmap()
        {
            var scale=_overlay.Viewport.PixelScale;
            float dpi=Screen.dpi>0?Screen.dpi/160f:2.5f;
            float size=Mathf.Max(144,48*dpi/Mathf.Max(.1f,Mathf.Min(scale.x>0?scale.x:1,scale.y>0?scale.y:1)));
            return "<view id=\"sheet\" class=\"app cinematic-roadmap\" data-safe-area=\"all\">"
                + CampIcons.Button(_overlay,"back","back",Back,"roadmap-exit",T("action.back"))
                    .Replace("class=\"roadmap-exit\"","class=\"roadmap-exit\" style=\"width:"+HtmlUi.Number(size)+"px;height:"+HtmlUi.Number(size)+"px;min-height:"+HtmlUi.Number(size)+"px;min-width:"+HtmlUi.Number(size)+"px;border-radius:"+HtmlUi.Number(size*.5f)+"px\"")
                    .Replace(CampIcons.Mark("back"),"<view class=\"roadmap-exit-disc\" style=\"width:"+HtmlUi.Number(size*.72f)+"px;height:"+HtmlUi.Number(size*.72f)+"px;border-radius:"+HtmlUi.Number(size*.36f)+"px\">"+CampIcons.Mark("back")+"</view>")
                + "</view>";
        }
        string FullMenu()
        {
            return "<view id=\"sheet\" class=\"app full-menu album-menu\" data-safe-area=\"all\" data-motion-role=\"dialog\""
                + (_closing ? " data-motion=\"exit\"" : "") + ">"
                + "<view class=\"full-header\">" + CampIcons.Button(_overlay,"back","back",Back,"nav-back",T("action.back"))
                + HtmlUi.Text(T("menu.album"),"full-title") + "</view>" + Album() + "</view>";
        }
        string Album()
        {
            var entries = _services.Save.Album.entries;
            if (entries == null || entries.Length == 0)
                return "<view class=\"album-empty\">" + CampIcons.Mark("tent") + HtmlUi.Text(T("album.empty")) + "</view>";
            _services.AlbumIndex = Mathf.Clamp(_services.AlbumIndex,0,entries.Length-1);
            var selected = entries[_services.AlbumIndex];
            var html = new StringBuilder("<view id=\"album-viewport\" class=\"album-viewport\" /><view class=\"album-info\">");
            html.Append(HtmlUi.Text(LevelDisplay.Title(selected.levelId,_services.Localization,_services.Journeys),"album-name"));
            if (_services.CanStart(selected.levelId)) html.Append(HtmlUi.Button(_overlay,"album-replay",T("action.replay"),()=>
            { _services.PendingMenuScreen="Album"; Action("qc.play",selected.levelId); },"primary"));
            html.Append(OverlayButton("album-stay", "memory.stay", () => Show("AlbumQuiet"), "quiet"));
            if (selected.cared) html.Append(OverlayButton("album-memory", "memory.replay", () => MemoryReplay?.Invoke(), "quiet"));
            html.Append("</view><scroll class=\"album-strip\" direction=\"horizontal\"><view class=\"album-thumbnails\">");
            for (int i=0;i<entries.Length;i++)
            {
                var index=i; var id=entries[i].levelId;
                _overlay.Callbacks.Bind("album-"+i,()=>
                { _services.AlbumIndex=index; AlbumSelected?.Invoke(index); _overlay.Refresh(); });
                html.Append("<button id=\"album-").Append(i).Append("\" class=\"album-thumb ")
                    .Append(i==_services.AlbumIndex?"selected":"").Append("\" onClick=\"Globals.campUi.Click('album-").Append(i).Append("')\">")
                    .Append("<view id=\"album-thumb-"+i+"\" class=\"album-thumb-art\"/>").Append("</button>");
            }
            return html.Append("</view></scroll>").ToString();
        }
        public void NavigateBack() => SheetBack();
        void SheetBack()
        {
            if (Current == "Settings" && _setNav.Back())
            {
                _confirmReset = false; _confirmErase = false;
                _services.Audio?.Play("ui.back");
                _overlay.Refresh();
                return;
            }
            Back();
        }
        string Settings()
        {
            string extra = "";
            if (_setNav.Category == "privacy" && _setNav.Section == "data")
            {
                extra = "<view class=\"settings-reset\">" + HtmlUi.Text(T("settings.reset.section"), "settings-group-title");
                if (!_confirmReset)
                    extra += OverlayButton("reset", "settings.reset", () => { _confirmReset = true; _confirmErase = false; _overlay.Refresh(); }, "legal-link danger");
                else extra += HtmlUi.Text(T("settings.reset.confirm"), "s-sub") + "<view class=\"row\">" +
                    OverlayButton("reset-confirm", "settings.reset", () =>
                    {
                        // A local reset also clears the unfinished layout; earned cosmetics remain owned.
                        _services.Progression.Restore(null, null, _services.Progression.CosmeticFlags);
                        _services.Save.Progress = new Application.ProgressSaveData();
                        _services.Save.Session = new Application.SessionSaveData();
                        _services.Save.Album = new Application.AlbumSaveData();
                        _services.Save.Save(); _confirmReset = false; RefreshAll();
                    }, "danger") + OverlayButton("reset-cancel", "action.cancel", () => { _confirmReset = false; _overlay.Refresh(); }, "quiet") + "</view>";
                extra += "</view>";
                extra += "<view class=\"settings-reset\">" + HtmlUi.Text(T("legal.erase.section"), "settings-group-title");
                if (!_confirmErase)
                    extra += HtmlUi.Button(_overlay, "local-data-erase", T("legal.erase"), () =>
                    { _confirmErase = true; _confirmReset = false; _overlay.Refresh(); }, "legal-link danger", !_erasing);
                else
                    extra += HtmlUi.Text(T("legal.erase.confirm"), "s-sub")
                        + HtmlUi.Text(T("legal.erase.scope"), "s-sub")
                        + HtmlUi.Button(_overlay, "local-data-erase-confirm", T("legal.erase"), async () =>
                        {
                            if (_erasing) return;
                            _overlay.FlushSettingsSave(); _surface.FlushSettingsSave();
                            _erasing = true; _overlay.Refresh();
                            var erased = await _services.EraseLocalGameData();
                            _erasing = false; _confirmErase = false;
                            _services.Notifications?.Show(T(erased ? "legal.erase.done" : "legal.erase.failed"),
                                erased ? Kruty1918.Notifications.API.GameplayNotificationKind.Info : Kruty1918.Notifications.API.GameplayNotificationKind.Error);
                            RefreshAll();
                        }, "legal-link danger", !_erasing)
                        + HtmlUi.Button(_overlay, "local-data-erase-cancel", T("action.cancel"), () =>
                        { _confirmErase = false; _overlay.Refresh(); }, "quiet", !_erasing);
                extra += "</view>";
            }
            else { _confirmReset = false; _confirmErase = false; }
            if (_setNav.Category == null) extra += HtmlUi.Button(_overlay, "settings-purchase-restore", T("purchase.restore"), async () =>
            {
                var state = await _services.Purchases.Restore();
                _services.Notifications?.Show(T("purchase." + state), Kruty1918.Notifications.API.GameplayNotificationKind.Info);
                RefreshAll();
            }, "quiet", !_services.MonetizationBusy);
            return SettingsPanel.Render(_services, _overlay, _setNav, extra);
        }

        /// <summary>The full-frame atmospheric veil keeps the wordmark readable in every phase.</summary>
        public void SetDarkSky(bool dark)
        {
            _surface?.Refresh();
        }

        public void Show(string name)
        {
            if (!_services.Economy.IsPro && (name == "Album" && !_services.Tutorial.AlbumUnlocked && (_services.Save.Album.entries?.Length ?? 0) == 0)) return;
            if (name != Current) _history.Push(Current);
            if (Current == "Economy" || name == "Economy") _economyPanel?.ResetConfirmation();
            Current = name; _confirmReset = false; _confirmErase = false; _closing = false;
            _services.Audio?.Play("sfx.page");
            if (Enum.TryParse<Application.ComfortScreen>(name, out var screen)) _services.Analytics.Screen(screen);
            _setNav.Open(null);
            RefreshAll();
            ScreenChanged?.Invoke(Current);
        }
        /// <summary>Sheets play their declarative exit; the real navigation
        /// happens when Motion.ExitFinished settles.</summary>
        public void Back()
        {
            if (Current == "Main" || _closing) return;
            if(Current=="Levels")
            {
                Current="Main";_history.Clear();RefreshAll();ScreenChanged?.Invoke(Current);return;
            }
            _closing = true;
            _services.Audio?.Play("ui.back");
            _overlay?.Refresh();
        }
        void OnExitFinished(string id)
        {
            if (!_closing || id != "sheet") return;
            _closing = false;
            Current = _history.Count > 0 ? _history.Pop() : "Main";
            _confirmReset = false; _confirmErase = false;
            RefreshAll();
            ScreenChanged?.Invoke(Current);
        }
        public void ShowDemoComplete() => Show("DemoComplete");
        public void Dispose()
        {
            _services.Tutorial.Changed -= RefreshAll;
            _services.MonetizationChanged -= RefreshAll;
            _economyPanel?.Dispose();
            _surface.LayoutChanged -= RefreshAll;
            _surface.Mounted -= UpdateMenuVisibility;
            if (_bleed != null) _bleed.Mounted -= UpdateMenuVisibility;
            if (_atmosphereVisibility != null) UnityEngine.Object.Destroy(_atmosphereVisibility.gameObject);
            if (_menuVisibility != null) UnityEngine.Object.Destroy(_menuVisibility.gameObject);
            if (_overlay != null) UnityEngine.Object.Destroy(_overlay.gameObject);
        }
    }
}
