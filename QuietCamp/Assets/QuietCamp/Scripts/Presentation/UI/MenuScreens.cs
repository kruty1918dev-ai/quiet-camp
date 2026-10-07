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
        public HtmlSurface Overlay => _overlay;
        public bool UiReady => _surface != null && _surface.IsUsable;
        public event Action<string> ScreenChanged;
        public event Action<int> AlbumSelected;
        public event Action OverlayMounted;

        readonly Stack<string> _history = new Stack<string>();
        readonly SettingsNav _setNav = new SettingsNav();
        bool _confirmReset;
        bool _confirmErase, _erasing;
        bool _closing;
        BonusCampDefinition _bonusPreview;
        EconomyPanel _economyPanel;
        string _journeyConfirmation;
        public JourneyDefinition SelectedJourney { get; private set; }
        public event Action MemoryReplay;
        public string Current { get; private set; } = "Main";

        public MenuScreens(GameServices services, RectTransform safeArea)
        {
            _services = services;
            services.Tutorial.Changed += RefreshAll;
            services.MonetizationChanged += RefreshAll;
            _surface = HtmlSurface.Create(safeArea, "MenuHtml", services, Render);
            _surface.LayoutChanged += RefreshAll;
            var canvasRoot = safeArea.parent as RectTransform;
            if (canvasRoot != null)
            {
                _bleed = HtmlSurface.Create(canvasRoot, "MenuAtmosphere", services, () =>
                    Current == "Levels" || Current == "Album" || Current == "AlbumQuiet" || Current == "JourneyPreview" || Current == "BonusPreview" ? "<view />" : "<view class=\"app\"><view class=\"menu-atmosphere\" /></view>");
                _bleed.transform.SetSiblingIndex(0);
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
        void RefreshAll() { _surface.Refresh(); _overlay?.Refresh(); _bleed?.Refresh(); }
        string Render()
        {
            // Keep the camp visible: a small wordmark above the clearing,
            // one primary action and two compact destinations below it.
            // All sheets live on the separate full-screen overlay.
            if (Current != "Main") return "<view />";
            var levels = LevelLoader.MvpLevelIds();
            var nextId = _services.ContinueLevel() ?? levels[0];
            int done=0;foreach(var id in levels)if(_services.Progression.IsCompleted(id))done++;
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
                + ((_services.Economy.IsPro || _services.Tutorial.RoadmapUnlocked) ? MenuCard("levels", "map", T("menu.levels"), done + "/" + levels.Count,
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
            if (Current == "Main") return "<view class=\"app dimroot\"></view>";
            if (Current == "Levels" || Current == "Album") return FullMenu();
            if (Current == "AlbumQuiet") return "<view class=\"app album-quiet\" data-safe-area=\"all\" id=\"sheet\" data-motion-role=\"dialog\""
                + (_closing ? " data-motion=\"exit\"" : "") + "><view id=\"album-viewport\" class=\"quiet-viewport\" />"
                + OverlayButton("quiet-back", "action.back", Back, "quiet quiet-back") + "</view>";
            var exit = _closing ? " data-motion=\"exit\"" : "";
            var sb = new StringBuilder("<view class=\"app dimroot\">");
            if(Current=="BonusPreview")sb.Append(FullMenu());
            sb.Append("<button id=\"dim\" class=\"dim\" data-motion-role=\"scrim\"" + exit
                + " onClick=\"Globals.campUi.Click('dim')\"></button>");
            if (Current == "Settings")
            {
                _overlay.Callbacks.Bind("dim", Back);
                return sb.Append(SettingsPanel.Shell(_services, _overlay, _setNav, Settings(), SheetBack, Back, exit))
                    .Append("</view>").ToString();
            }
            var catKey = Current == "Settings" ? SettingsPanel.CategoryKey(_setNav) : null;
            var title = catKey ?? (Current == "Economy" ? "economy.title" : Current == "Journeys" ? "journey.title" : Current == "JourneyPreview" ? SelectedJourney?.titleKey : Current == "BonusPreview" ? _bonusPreview.titleKey : Current == "PrivacyNotice" ? "settings.cat.privacy" : Current == "Levels" ? "menu.levels" : Current == "Album" ? "menu.album" : Current == "Settings" ? "menu.settings" : "demo.complete");
            var body = Current == "Economy" ? _economyPanel.Render() : Current == "Journeys" ? Journeys() : Current == "JourneyPreview" ? JourneyPreview() : Current == "BonusPreview" ? BonusPreview() : Current == "PrivacyNotice" ? PrivacyPanel.Notice(_services, _overlay, Back) : Current == "Levels" ? Levels() : Current == "Album" ? Album() : Current == "Settings" ? Settings() : OverlayButton("demo-album", "menu.album", () => Action("qc.album"));
            // Top ‹ is the sole back control; inside a settings category it
            // steps up to the category list instead of leaving the sheet.
            sb.Append(HtmlUi.Template("Sheet",
                    "nav", HtmlUi.Button(_overlay, "back", "‹", SheetBack, "nav-back",
                        tooltip: T("action.back")),
                    "title", HtmlUi.Escape(T(title)), "body", body, "footer", Current=="BonusPreview"?BonusPlayButton():"",
                    "body-id", "menu-body-" + Current + "-" + (_setNav.Category ?? "root"))
                .Replace("data-motion-role=\"edge-bottom\"",
                    "data-motion-role=\"edge-bottom\"" + exit));
            sb.Append("</view>");
            _overlay.Callbacks.Bind("dim", () => { if (Current != "Main") Back(); });
            return sb.ToString();
        }
        int _mapArtWatch;
        bool _mapArtWatching;
        void MountMapArt()
        {
            var art = _overlay.Element("roadmap-art");
            if (art == null || art.GetComponentInChildren<RoadmapGraphic>() != null) return;
            var native=QcUi.Stretch(art,"RoadmapMesh");
            var graphic=native.gameObject.AddComponent<RoadmapGraphic>();
            graphic.raycastTarget=false;graphic.Configure(_services);
        }
        // Large maps can settle their DOM a frame after Mounted fires: a stale
        // subtree still reports an art element, then gets replaced and takes the
        // mesh with it. Keep re-checking through a short settling window.
        System.Collections.IEnumerator MapArtWatch()
        {
            while (_mapArtWatch-- > 0 && (Current == "Levels" || Current == "BonusPreview"))
            {
                MountMapArt();
                yield return null;
            }
            _mapArtWatching = false;
        }
        void OnOverlayMounted()
        {
            if (Current == "Levels" || Current == "BonusPreview")
            {
                MountMapArt();
                _mapArtWatch = 90;
                if (!_mapArtWatching)
                {
                    _mapArtWatching = true;
                    _overlay.StartCoroutine(MapArtWatch());
                }
                var scroll = _overlay.Element("roadmap-scroll");
                if (scroll != null)
                {
                    var binding = scroll.GetComponent<MenuMapBinding>();
                    if (binding == null) binding = scroll.gameObject.AddComponent<MenuMapBinding>();
                    binding.Configure(_services,()=>Current=="Levels"&&!_closing);
                }
            }
            if(Current=="BonusPreview")
            {
                var art=_overlay.Element("bonus-preview-art");
                if(art!=null&&art.GetComponentInChildren<BonusCampPreviewGraphic>()==null)
                    QcUi.Stretch(art,"BonusCampArtwork").gameObject.AddComponent<BonusCampPreviewGraphic>().Configure(_bonusPreview);
            }
            OverlayMounted?.Invoke();
        }
        string FullMenu()
        {
            bool map=Current=="Levels"||Current=="BonusPreview",preview=Current=="BonusPreview";
            var title = T(map ? "menu.levels" : "menu.album");
            var stories = map ? OverlayButton("journeys", "journey.title", () => Show("Journeys"), "quiet map-stories") : "";
            return "<view id=\""+(preview?"bonus-map-background":"sheet")+"\" class=\"app full-menu " + (map ? "map-menu" : "album-menu")
                + "\" data-safe-area=\"all\""+(preview?"":" data-motion-role=\"dialog\"") + (_closing&&!preview ? " data-motion=\"exit\"" : "") + ">"
                + "<view class=\"full-header\">" + CampIcons.Button(_overlay,preview?"map-back":"back","back",Back,"nav-back",T("action.back"))
                + HtmlUi.Text(title,"full-title") + stories + "</view>"
                + (map ? Levels() : Album()) + "</view>";
        }
        string Levels()
        {
            var ids = LevelLoader.MvpLevelIds();
            var next = _services.JourneyAccess.ContinueTarget("main");
            var districts = LevelLoader.Districts();
            var worldMap = WorldMapBuilder.Build(ids, districts, BonusCampCatalog.Slots, _services.Journeys.Journeys, CampContent.Summary);
            var reveal = new WorldMapRevealService(worldMap, _services.Progression, _services.JourneyAccess);
            var revealedBranches = new HashSet<string>(StringComparer.Ordinal);
            foreach (var branch in reveal.RevealedBranches()) revealedBranches.Add(branch.Id);
            var districtAt = new Dictionary<int, DistrictDefinition>();
            foreach (var d in districts) districtAt[d.from] = d;
            var html = new StringBuilder("<scroll id=\"roadmap-scroll\" class=\"map-scroll\"><view class=\"roadmap\" style=\"height:" + HtmlUi.Number(RoadmapLayout.Height(ids.Count)) + "px\"><view id=\"roadmap-art\" class=\"roadmap-art\" />");
            for (var i = 0; i < ids.Count; i++)
            {
                var id = ids[i];
                var unlocked = _services.CanStart(id);
                var done = _services.Progression.IsCompleted(id);
                var nodeState = reveal.MainState(worldMap.Nodes[i]);
                var hidden = nodeState == WorldNodeState.Hidden;
                var x = RoadmapGraphic.NodeX(i)*100;
                var summary = hidden ? null : CampContent.Summary(id);
                if (districtAt.TryGetValue(i + 1, out var district))
                    html.Append("<view class=\"map-district act-").Append(district.act)
                        .Append("\" style=\"top:").Append(HtmlUi.Number(RoadmapLayout.MainY(i)+34))
                        .Append("px\">").Append(HtmlUi.Text(T(district.TitleKey), "map-district-name"))
                        .Append("</view>");
                _overlay.Callbacks.Bind("level-"+i, () =>
                {
                    if (unlocked) { _services.PendingMenuScreen="Levels"; Action("qc.play",id); }
                    else _services.Notifications?.Show(T("level.locked.hint"),Kruty1918.Notifications.API.GameplayNotificationKind.Info,dedupKey:"locked."+id);
                });
                html.Append("<view class=\"map-stop\" style=\"left:").Append(HtmlUi.Number(x)).Append("%;top:").Append(HtmlUi.Number(RoadmapLayout.MainY(i)+100))
                    .Append("px\"><button id=\"level-").Append(i).Append("\" class=\"map-node ")
                    .Append(hidden ? "hidden" : done ? "done" : id==next ? "selected" : unlocked ? "" : "locked")
                    .Append("\" data-tooltip=\"").Append(HtmlUi.Escape(hidden?T("map.veiled"):LevelDisplay.Title(id,_services.Localization,_services.Journeys)))
                    .Append("\" onClick=\"Globals.campUi.Click('level-").Append(i).Append("')\">")
                    .Append(hidden ? "" : HtmlUi.Text((i+1).ToString(),"node-number"))
                    .Append(done ? CampIcons.Mark("check","node-status") : !unlocked&&!hidden ? CampIcons.Mark("lock","node-status") : "")
                    .Append("</button><view class=\"map-wishes\">");
                if (summary?.shade==true) html.Append(CampIcons.Mark("shade"));
                if (summary?.quiet==true) html.Append(CampIcons.Mark("quiet"));
                if (summary?.friends==true) html.Append(CampIcons.Mark("guests"));
                if (summary?.fire==true) html.Append(CampIcons.Mark("fire"));
                html.Append("</view></view>");
            }
            foreach(var slot in BonusCampCatalog.Slots)
            {
                if(slot.afterLevel>ids.Count)continue;
                var veiled=slot.afterLevel>reveal.Horizon;
                var access=_services.BonusCamps.Evaluate(slot);
                string callback="bonus-"+slot.afterLevel;
                // The preview card itself is the teaser — veiling only strips
                // the node's map decoration until the road reaches it.
                _overlay.Callbacks.Bind(callback,()=>{_bonusPreview=slot;Show("BonusPreview");});
                html.Append("<view class=\"map-bonus-stop\" style=\"left:").Append(HtmlUi.Number(RoadmapLayout.BonusX(slot)*100))
                    .Append("%;top:").Append(HtmlUi.Number(RoadmapLayout.BonusY(slot)+64)).Append("px\">")
                    .Append("<button id=\"").Append(callback).Append("\" class=\"map-bonus-node ")
                    .Append(veiled?"veiled":access.CanPlay?"bonus-ready":"").Append("\" data-tooltip=\"").Append(HtmlUi.Escape(T(veiled?"map.veiled":slot.titleKey)))
                    .Append("\" onClick=\"Globals.campUi.Click('").Append(callback).Append("')\">")
                    .Append("<view class=\"bonus-heading\">").Append(CampIcons.Mark("bonus"))
                    .Append(veiled?"":HtmlUi.Text(T("map.bonus.label"),"bonus-eyebrow")).Append("</view>")
                    .Append(veiled?"":HtmlUi.Text(T(slot.titleKey),"bonus-name"))
                    .Append(CampIcons.Mark(veiled?"bonus":access.State==BonusCampState.Locked?"lock":access.State==BonusCampState.Completed?"check":"hint","node-status"))
                    .Append("</button>")
                    .Append(veiled?"":HtmlUi.Text(T(access.Published?"map.bonus.sideRoute":"map.bonus.soon"),"bonus-caption"))
                    .Append("</view>");
            }
            foreach(var journey in _services.Journeys.Journeys)
            {
                // Story branches fork off the main road where they unlock.
                // The slot is visible ahead of time — a promise, not a gate.
                if(journey.id=="main"||journey.id=="qa"||journey.id.StartsWith("bonus.")||!journey.published&&journey.requiredCompletions<=0)continue;
                if(journey.requiredCompletions>ids.Count)continue;
                var veiled=!revealedBranches.Contains(journey.id);
                var state=journey.levelIds.Length>0?_services.JourneyAccess.Evaluate(journey.levelIds[0]).State:JourneyAccessState.MissingContent;
                string callback="branch-"+journey.id;
                _overlay.Callbacks.Bind(callback,()=>{SelectedJourney=journey;_journeyConfirmation=null;Show("JourneyPreview");});
                html.Append("<view class=\"map-branch-stop\" style=\"left:").Append(HtmlUi.Number(RoadmapLayout.BranchX(journey)*100))
                    .Append("%;top:").Append(HtmlUi.Number(RoadmapLayout.BranchY(journey))).Append("px\">")
                    .Append("<button id=\"").Append(callback).Append("\" class=\"map-branch-node")
                    .Append(veiled?" veiled":state==JourneyAccessState.Available?" branch-ready":"").Append("\" data-tooltip=\"").Append(HtmlUi.Escape(T(veiled?"map.veiled":journey.titleKey)))
                    .Append("\" onClick=\"Globals.campUi.Click('").Append(callback).Append("')\">")
                    .Append(CampIcons.Mark("path"))
                    .Append(veiled?"":HtmlUi.Text(T(journey.titleKey),"branch-name"))
                    .Append(CampIcons.Mark(!veiled&&state==JourneyAccessState.Available?"hint":"lock","node-status"))
                    .Append("</button>")
                    .Append(veiled?"":HtmlUi.Text(journey.published?T("map.branch.sideRoute"):T("map.bonus.soon"),"bonus-caption"))
                    .Append("</view>");
            }
            return html.Append("</view></scroll>").ToString();
        }
        string BonusPreview()
        {
            var slot=_bonusPreview;var access=_services.BonusCamps.Evaluate(slot);
            var html=new StringBuilder("<view class=\"bonus-preview\"><view id=\"bonus-preview-art\" class=\"bonus-preview-art theme-"+HtmlUi.Escape(slot.theme)+"\" />");
            html.Append(HtmlUi.Text(T(slot.descriptionKey),"bonus-description"));
            html.Append("<view class=\"bonus-requirements\">");
            if(access.Required>0)
            {
                html.Append("<view class=\"row\">").Append(CampIcons.Mark(access.Completed>=access.Required?"check":"path"))
                    .Append(HtmlUi.Text(string.Format(T("map.bonus.requirement"),slot.afterLevel-9,slot.afterLevel),"bonus-condition"))
                    .Append(HtmlUi.Text(Mathf.Min(access.Completed,access.Required)+"/"+access.Required,"bonus-count")).Append("</view>")
                    .Append("<view class=\"bonus-progress\"><view class=\"bonus-progress-fill\" style=\"width:")
                    .Append(HtmlUi.Number(Mathf.Clamp01((float)access.Completed/access.Required)*100)).Append("%\" /></view>");
            }
            if(slot.requiresPremium)
                html.Append("<view class=\"row\">").Append(CampIcons.Mark(access.HasPremium?"check":"lock"))
                    .Append(HtmlUi.Text(T("map.bonus.premiumRequirement"),"bonus-condition")).Append("</view>");
            if(!string.IsNullOrEmpty(slot.seasonId))
                html.Append("<view class=\"row\">").Append(CampIcons.Mark(access.SeasonMatch?"check":"lock"))
                    .Append(HtmlUi.Text(string.Format(T("map.bonus.seasonRequirement"),T("season."+slot.seasonId)),"bonus-condition")).Append("</view>");
            html.Append("</view>");
            if(!access.Published)html.Append(HtmlUi.Text(T("map.bonus.preparing"),"bonus-note"));
            return html.Append("</view>").ToString();
        }
        string BonusPlayButton()
        {
            var slot=_bonusPreview;var access=_services.BonusCamps.Evaluate(slot);
            var canPlay = _services.CanStart(slot.levelId);
            return HtmlUi.Button(_overlay,"bonus-play",T(canPlay?"menu.start":!access.Published?"map.bonus.soon":"map.bonus.locked"),()=>
            {
                if(!_services.CanStart(slot.levelId))return;
                _services.PendingMenuScreen="Levels";Action("qc.play",slot.levelId);
            },"primary bonus-play",enabled:canPlay);
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
            html.Append(HtmlUi.Button(_overlay,"album-replay",T("action.replay"),()=>
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
        string Journeys()
        {
            var html = new StringBuilder("<view class=\"column\">" + HtmlUi.Text(T("journey.intro"), "s-sub"));
            bool additional = false;
            foreach (var journey in _services.Journeys.Journeys)
            {
                if (journey.id == "qa" || !journey.published) continue;
                html.Append(HtmlUi.Button(_overlay, "journey-" + journey.id, T(journey.titleKey), () =>
                { SelectedJourney = journey; _journeyConfirmation = null; Show("JourneyPreview"); }, "quiet"));
                html.Append(HtmlUi.Text(T(journey.descriptionKey), "s-sub"));
                if (journey.id != "main") additional = true;
            }
            if (!additional) html.Append(HtmlUi.Text(T("journey.empty"), "s-sub"));
            return html.Append("</view>").ToString();
        }
        string JourneyPreview()
        {
            var journey = SelectedJourney;
            if (journey == null) return HtmlUi.Text(T("journey.preparing"));
            var html = new StringBuilder("<view class=\"column\"><view id=\"journey-viewport\" class=\"journey-viewport\" />");
            html.Append(HtmlUi.Text(T(journey.descriptionKey), "s-sub"));
            html.Append(HtmlUi.Text(T("journey.intro"), "s-sub"));
            html.Append(HtmlUi.Text(string.Format(T("journey.contents"), journey.levelIds.Length), "s-sub"));
            if (!journey.published) return html.Append(HtmlUi.Text(T("journey.preparing"), "s-sub")).Append("</view>").ToString();
            // A progress-gated branch says "keep walking the path" instead of
            // pretending there is something to buy.
            if (journey.requiredCompletions > 0 && journey.levelIds.Length > 0
                && _services.JourneyAccess.Evaluate(journey.levelIds[0]).State == JourneyAccessState.Predecessor)
                return html.Append(HtmlUi.Text(
                    string.Format(T("journey.progressRequirement"), journey.requiredCompletions), "s-sub"))
                    .Append("</view>").ToString();
            var first = _services.JourneyAccess.ContinueTarget(journey.id);
            if (first != null) html.Append(HtmlUi.Button(_overlay, "journey-play", T("menu.start"), () =>
            { _services.PendingMenuScreen = "Journeys"; Action("qc.play", first); }, "primary"));
            else
            {
                html.Append(HtmlUi.Text(T("journey.oneTime"), "s-sub"));
                html.Append(HtmlUi.Button(_overlay, "journey-unlock", string.Format(T("journey.unlock"), journey.currencyCost), () =>
                {
                    if (_services.MonetizationBusy) return;
                    _journeyConfirmation = journey.id; _overlay.Refresh();
                }, "primary", _services.Economy.Currency >= journey.currencyCost && !_services.MonetizationBusy));
                if (_journeyConfirmation == journey.id)
                    html.Append("<view class=\"surface column\">").Append(HtmlUi.Text(T("economy.confirm"), "s-sub"))
                        .Append(OverlayButton("journey-confirm", "action.confirm", () =>
                        {
                            if (_services.MonetizationBusy) return;
                            _journeyConfirmation = null;
                            var result = _services.BuyJourney(journey.id);
                            _services.Notifications?.Show(T("economy.result." + result), Kruty1918.Notifications.API.GameplayNotificationKind.Info);
                            RefreshAll();
                        }, "primary", !_services.MonetizationBusy))
                        .Append(OverlayButton("journey-cancel", "action.cancel", () => { _journeyConfirmation = null; _overlay.Refresh(); }, "quiet"))
                        .Append("</view>");
                html.Append(OverlayButton("journey-shop", "economy.title", () => Show("Economy"), "quiet"));
            }
            return html.Append("</view>").ToString();
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
            if (!_services.Economy.IsPro && (name == "Levels" && !_services.Tutorial.RoadmapUnlocked
                || name == "Album" && !_services.Tutorial.AlbumUnlocked && (_services.Save.Album.entries?.Length ?? 0) == 0)) return;
            if(Current=="Levels"&&name!=Current)_overlay?.GetComponentInChildren<MenuMapBinding>()?.Freeze();
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
            if(Current=="Levels")_overlay?.GetComponentInChildren<MenuMapBinding>()?.Freeze();
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
            if (_bleed != null) UnityEngine.Object.Destroy(_bleed.gameObject);
            if (_surface != null) UnityEngine.Object.Destroy(_surface.gameObject);
            if (_overlay != null) UnityEngine.Object.Destroy(_overlay.gameObject);
        }
    }
}
