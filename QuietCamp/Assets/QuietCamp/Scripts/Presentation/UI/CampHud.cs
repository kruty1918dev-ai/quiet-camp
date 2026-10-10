using System;
using QuietCamp.Infrastructure;
using System.Collections.Generic;
using System.Text;
using DG.Tweening;
using Kruty1918.Audio;
using Kruty1918.InputRouting.API;
using Kruty1918.UIActions.API;
using QuietCamp.Application;
using QuietCamp.Domain;
using UnityEngine;

namespace QuietCamp.Presentation.UI
{
    /// <summary>
    /// Contextual HTML HUD; the board remains the primary interaction surface.
    /// Two documents: a safe-area HUD (header orbs, guest card, tent chips,
    /// tutorial pill) and a full-screen overlay on CanvasRoot (scrim + bottom
    /// sheets + the completion card) so dimming covers the whole display.
    /// Sheets close via an outside tap on the scrim with a declarative exit
    /// motion — Motion.ExitFinished performs the actual unmount.
    /// </summary>
    public sealed class CampHud : IDisposable
    {
        const float TagSeconds = 1.9f;

        readonly GameServices _services;
        readonly CampSession _session;
        readonly HtmlSurface _surface;
        readonly HtmlSurface _overlay;
        readonly List<string> _modals = new List<string>();
        readonly List<IDisposable> _modalLeases = new List<IDisposable>();
        Tween _release;
        RuleReport _lastReport;
        string _tutorialKey;
        readonly SettingsNav _setNav = new SettingsNav();
        readonly Dictionary<string, Vector2> _projectedAnchors = new Dictionary<string, Vector2>();
        readonly List<(string Guest, RectTransform Rect, Vector2 At)> _anchorRects = new List<(string, RectTransform, Vector2)>();
        RectTransform _boardViewport, _safeArea;
        UnityHTML.Runtime.UnityHtmlViewport _boardLayout;
        string _boardLanguage;
        float _boardTextScale;
        bool _showIssue;
        bool _closing;
        bool _celebrating, _storyPlaying;
        Action _skipStory;
        EconomyPanel _economyPanel;
        IDisposable _completionBlock;
        RuleReport _previewReport;
        string _tagGuest;
        float _tagUntil = -1f;
        float _hintUntil = -1f;

        bool SideJourney => _services.Journeys.ForLevel(_session.Level.id)?.id is string id && id != "main";
        bool HintsAvailable => _services.Economy.IsPro || _services.Tutorial.HintsUnlocked || SideJourney;
        bool HistoryAvailable => _services.Economy.IsPro || _services.Tutorial.HistoryUnlocked || SideJourney;
        bool GuestListAvailable => _services.Economy.IsPro || _services.Tutorial.GuestListUnlocked || SideJourney;
        public bool HasModalOpen => _modals.Count > 0 || _closing;
        public bool UiReady => _surface != null && _surface.IsUsable;
        public event Action<string> GuestSelected;
        public event Action<Cell[]> AreaShown;
        /// <summary>The HUD document's transform — the projection rect for
        /// world-anchored chips.</summary>
        public RectTransform SurfaceRect => _surface == null ? null : (RectTransform)_surface.transform;

        /// <summary>World → CSS px inside the HUD surface; null when off-screen.
        /// Wired by the scene host (camera + surface rect).</summary>
        public Func<Vector3, Vector2?> ProjectToHud;
        /// <summary>Guest id → world anchor just above its tent; null when the
        /// guest has no tent on the board.</summary>
        public Func<string, Vector3?> GuestAnchor;

        public CampHud(GameServices services, CampSession session, RectTransform safeArea)
        {
            _services = services; _session = session;
            services.Tutorial.Changed += OnTutorialChanged;
            services.MonetizationChanged += RefreshAll;
            _lastReport = RuleEvaluator.Evaluate(session.Level, session.State.Placements);
            _surface = HtmlSurface.Create(safeArea, "CampHtml", services, Render);
            _surface.Mounted += CacheAnchors;
            _surface.Mounted += AdaptBoardViewport;
            _surface.LayoutChanged += AdaptBoardViewport;
            _safeArea = safeArea;
            var canvasRoot = safeArea.parent as RectTransform;
            if (canvasRoot != null)
            {
                _overlay = HtmlSurface.Create(canvasRoot, "CampOverlay", services, RenderOverlay);
                _economyPanel = new EconomyPanel(services, _overlay);
                _overlay.Motion.ExitFinished += OnExitFinished;
            }
            var viewport = safeArea.Find("Gameplay/BoardViewport") as RectTransform;
            _boardViewport = viewport;
            if (viewport != null)
            {
                viewport.anchorMin = Vector2.zero; viewport.anchorMax = Vector2.one;
                // The casual HUD reserves only the edge orbs and the guest
                // card strip — the sheet/dim live on the full-screen overlay.
                // Kept tight: on portrait phones the board must dominate.
                viewport.offsetMin = new Vector2(6, 360);
                viewport.offsetMax = new Vector2(-6, -180);
            }
            session.Evented += OnSessionEvent;
        }

        void AdaptBoardViewport()
        {
            if (_boardViewport == null || _safeArea == null || HasModalOpen) return;
            var layout = _surface.Viewport;
            if (!_surface.IsMounted || !layout.IsValid) return;
            var language = _services.Localization.CurrentLanguageId;
            var textScale = _services.Settings.textScale;
            // A preview, tutorial step or next guest may change the dock's
            // height. Keep the playable frame fixed throughout the interaction:
            // refitting here moves the board under the player's finger.
            // Recalculate only for display/safe-area or accessibility changes.
            if (_boardLayout.Equals(layout) && _boardLanguage == language && _boardTextScale == textScale) return;
            var bottom = _surface.Element("bottom")?.transform as RectTransform;
            if (bottom == null || bottom.rect.height < 1) return;
            if (layout.IsWide)
            {
                var reserved = Mathf.Min(_safeArea.rect.width * .43f, bottom.rect.width + 64);
                _boardViewport.offsetMin = new Vector2(24, 40);
                _boardViewport.offsetMax = new Vector2(-reserved, -180);
            }
            else
            {
                var reserved = Mathf.Min(_safeArea.rect.height * .42f, bottom.rect.height + 48);
                _boardViewport.offsetMin = new Vector2(6, reserved);
                _boardViewport.offsetMax = new Vector2(-6, -180);
            }
            _boardLayout = layout;
            _boardLanguage = language;
            _boardTextScale = textScale;
        }

        string T(string key) => _services.Localization.T(key);
        string Button(string id, string key, Action action, string cls = "", bool enabled = true)
            => HtmlUi.Button(_surface, id, T(key), action, cls, enabled);
        void Action(string id, object payload = null)
        {
            _services.Audio?.Play("ui.click");
            _services.Actions.Execute(new UiActionRequest(new UiActionId(id), UiActionSource.Button, "Gameplay", null, payload));
            if (id == "qc.check") { _showIssue = true; _surface.Refresh(); }
        }
        void RefreshAll() { _surface.Refresh(); _overlay?.Refresh(); }
        string OverlayButton(string id, string key, Action action, string cls = "", bool enabled = true)
            => HtmlUi.Button(_overlay, id, T(key), action, cls, enabled);

        // ─── Safe-area HUD document ──────────────────────────────────────────

        string Render()
        {
            // While a modal owns the screen the gameplay orbs stay hidden —
            // two competing HUD layers read as broken UI under the dim.
            var header = HasModalOpen ? "" :
                (HintsAvailable ? HtmlUi.Button(_surface, "hint",
                    T("action.hint") + (_services.Economy.IsPro ? "" : " · " + _services.Economy.Hints),
                    () => Action("qc.hint"), "quiet hint-control") : "") +
                "<view class=\"grow\"></view>" +
                CampIcons.Button(_surface, "pause", "pause", () => Action("qc.pause"), "orb quiet",
                    tooltip: T("hud.pause"));
            var message = "";
            var tutorial = "";
            if (_celebrating || HasModalOpen) { }
            else if (_previewReport != null)
            {
                var key = PreviewKey(_previewReport);
                tutorial = GuideMarkup() + "<view id=\"placement-status\" class=\"tutorial preview-feedback\"><view class=\"pill\">"
                    + HtmlUi.Text(T(key)) + "</view></view>";
            }
            else if (_tutorialKey != null)
                tutorial = GuideMarkup();
            else if (_showIssue && _lastReport != null && !_lastReport.IsSolved && _lastReport.Issues.Count > 0)
                message = "<view class=\"message\">"
                    + HtmlUi.Button(_surface, "issue", T(IssueKey(_lastReport, _lastReport.Issues[0])), ShowIssueArea)
                    + "</view>";
            return HtmlUi.Template("Gameplay", "header", header, "message", message,
                "tutorial", tutorial, "context", "", "dock", HasModalOpen || _celebrating ? "" : Dock(),
                "anchors", HasModalOpen || _celebrating ? "" : Anchors());
        }

        string GuideMarkup()
        {
            if (_tutorialKey == null) return "";
            var portrait = _services.Tutorial.Portrait;
            return "<view id=\"tutorial\" class=\"guide-card\" data-motion-role=\"toast\">"
                + "<img id=\"guide-portrait\" class=\"guide-portrait\" src=\"res:QuietCamp/UI/Mentor/" + portrait + "\"/>"
                + "<view class=\"guide-copy\">" + HtmlUi.Text(T("guide.name"), "guide-name")
                + HtmlUi.Text(T(_tutorialKey), "guide-instruction")
                + (_services.Tutorial.Guiding(_session.Level.id)
                    ? HtmlUi.Button(_surface, "guide-skip", T("guide.skip"), () => OpenModal("GuideSkip"), "guide-skip")
                    : "")
                + "</view></view>";
        }

        string GuideFocus(string target) => _tutorialKey != null && _services.Tutorial.Target == target ? " guide-focus" : "";

        /// <summary>History and check sit above the full guest card, keeping
        /// the card closest to the bottom edge without squeezing its wishes.</summary>
        string Dock()
        {
            var dock = new StringBuilder("<view class=\"dock\">");
            var next = NextUnplaced();
            var placed = _session.State.Count;
            var total = _session.Level.guests.Length;
            dock.Append("<view class=\"dock-tools\">");
            if (GuestListAvailable)
                dock.Append(CampIcons.Button(_surface, "guests", "guests", () => OpenModal("Guests"), "icon quiet guest-list-button", T("hud.guests")));
            if (HistoryAvailable)
                dock.Append(CampIcons.Button(_surface, "undo", "undo", () => Action("qc.undo"), "icon quiet" + GuideFocus("undo"), T("action.undo"), _session.CanUndo));
            if (_session.CanRedo)
                dock.Append(CampIcons.Button(_surface, "redo", "redo", () => Action("qc.redo"), "icon quiet", T("action.redo")));
            if (placed == total)
                dock.Append(HtmlUi.Button(_surface, "check", T("hud.ready"), () => Action("qc.check"),
                    "primary check-button" + GuideFocus("check"), _services.Tutorial.CanCompleteLevel(_session.Level.id)));
            dock.Append("</view>");
            if (next != null)
            {
                var selected = _session.SelectedGuestId == next.id;
                dock.Append("<button id=\"guest-card\" class=\"guest-card" + (selected ? " selected" : "") + GuideFocus("guest-card")
                    + "\" onClick=\"Globals.campUi.Click('guest-card')\">"
                    + "<img class=\"tent-art\" src=\"res:QuietCamp/UI/Tents/"+next.assetId+"\"/>" + "<view class=\"gc-text\">"
                    + HtmlUi.Text(T(next.nameKey), "gc-name")
                    + CampIcons.Wishes(_session.Level,next)
                    + "</view><text class=\"gc-count\">" + placed + "/" + total + "</text></button>");
                _surface.Callbacks.Bind("guest-card", () => SelectGuest(next.id));
            }
            else
            {
                dock.Append("<view class=\"guest-card idle\"><view class=\"gc-text\">"
                    + HtmlUi.Text(T("hud.allplaced"), "gc-name")
                    + HtmlUi.Text(T(_lastReport?.IsSolved == true ? "rule.ok" : "hud.checkme"), "gc-wish")
                    + "</view><text class=\"gc-count\">" + placed + "/" + total + "</text></view>");
            }
            if (placed == total) dock.Append(HtmlUi.Text(_services.Economy.IsPro
                ? T("economy.check.notice.pro")
                : _services.Tutorial.Guiding(_session.Level.id)
                    ? T("guide.check.free")
                    : string.Format(T("economy.check.notice"), _services.Economy.Lives), "check-notice"));
            dock.Append("</view>");
            return dock.ToString();
        }

        /// <summary>World-anchored chips: a transient name tag over a freshly
        /// placed tent, and rotate/remove orbs over the selected one.</summary>
        string Anchors()
        {
            _projectedAnchors.Clear();
            if (_previewReport != null || ProjectToHud == null || GuestAnchor == null) return "";
            var sb = new StringBuilder();
            var selected = _session.SelectedGuestId;
            var selPlaced = selected != null && _session.State.Contains(selected);
            if (_tagGuest != null && _tagGuest != selected && Time.unscaledTime < _tagUntil)
                Chip(sb, _tagGuest, tagOnly: true);
            if (selPlaced)
                Chip(sb, selected, tagOnly: false);
            return sb.ToString();
        }

        void Chip(StringBuilder sb, string guestId, bool tagOnly)
        {
            var anchor = GuestAnchor(guestId);
            if (!anchor.HasValue) return;
            var at = ProjectToHud(anchor.Value + Vector3.up * 1.15f);
            if (!at.HasValue) return;
            _projectedAnchors[guestId] = at.Value;
            var guest = Array.Find(_session.Level.guests, g => g.id == guestId);
            sb.Append("<view class=\"tent-chip\" id=\"tent-chip-" + HtmlUi.Escape(guestId)
                + "\" data-motion-role=\"dialog\" style=\"left: " + HtmlUi.Number(at.Value.x)
                + "px; top: " + HtmlUi.Number(at.Value.y) + "px\">");
            if (guest != null)
                sb.Append("<view class=\"tag\"><text>" + HtmlUi.Escape(T(guest.nameKey)) + "</text>"
                    + (tagOnly ? "" : CampIcons.Wishes(_session.Level,guest)) + "</view>");
            if (!tagOnly)
            {
                sb.Append("<view class=\"verbs\">");
                sb.Append(CampIcons.Button(_surface, "rotate", "rotate", () => Action("qc.rotate"), "verb" + GuideFocus("rotate"),
                    tooltip: T("action.rotate")));
                if (HistoryAvailable)
                    sb.Append(CampIcons.Button(_surface, "remove", "remove", () => Action("qc.remove"), "verb",
                        tooltip: T("action.remove")));
                sb.Append("</view>");
            }
            sb.Append("</view>");
        }

        GuestData NextUnplaced()
        {
            var selected = Array.Find(_session.Level.guests, g => g.id == _session.SelectedGuestId);
            if (selected != null && !_session.State.Contains(selected.id)) return selected;
            foreach (var g in _session.Level.guests)
                if (!_session.State.Contains(g.id)) return g;
            return null;
        }

        string WishText(GuestData guest)
        {
            var wishes = new List<string> { T("wish.path") };
            if (guest.shade) wishes.Add(T("wish.shade"));
            if (guest.quiet) wishes.Add(T("wish.quiet"));
            if (_session.Level.friends != null)
                foreach (var pair in _session.Level.friends)
                    if (pair != null && Array.IndexOf(pair, guest.id) >= 0)
                    { wishes.Add(T("wish.friends")); break; }
            return string.Join(" · ", wishes);
        }

        // ─── Full-screen overlay document (scrim / sheets / celebrate) ───────

        string RenderOverlay()
        {
            if (!HasModalOpen && !_celebrating) return "<view />";
            var sb = new StringBuilder("<view class=\"app dimroot\">");
            if (HasModalOpen)
            {
                var exit = _closing ? " data-motion=\"exit\"" : "";
                sb.Append("<button id=\"dim\" class=\"dim" + (_modals.Contains("Guests") ? " clear" : "")
                    + "\" data-motion-role=\"scrim\"" + exit
                    + " onClick=\"Globals.campUi.Click('dim')\"></button>");
                sb.Append(Sheet());
                _overlay.Callbacks.Bind("dim", () => CloseTopModal());
            }
            else if (_celebrating && _storyPlaying)
            {
                sb.Append("<view class=\"story-skip\" data-safe-area=\"all\">")
                    .Append(OverlayButton("story-skip", "memory.skip", () => _skipStory?.Invoke(), "quiet")).Append("</view>");
            }
            else if (_celebrating)
            {
                sb.Append("<view class=\"celebrate\"><view id=\"sheet\" class=\"card\" data-motion-role=\"dialog\">"
                    + HtmlUi.Text("✓", "big win")
                    + HtmlUi.Text(T("rule.ok"), "big")
                    + HtmlUi.Text(T("celebrate.next"), "sub")
                    + (_services.Tutorial.RewardOwned && _session.Level.id == "QC005"
                        ? "<view class=\"guide-farewell\"><img class=\"guide-portrait\" src=\"res:QuietCamp/UI/Mentor/farewell\"/>"
                            + HtmlUi.Text(T("guide.farewell"), "guide-instruction") + "</view>"
                            + HtmlUi.Text(T("guide.reward.earned"), "guide-reward") : "")
                    + OverlayButton("memory-album", "menu.album", () => Action("qc.album"), "quiet")
                    + OverlayButton("next", "menu.levels", () => Action("qc.next"), "primary")
                    + "</view></view>");
            }
            sb.Append("</view>");
            return sb.ToString();
        }

        string Sheet()
        {
            var modal = _modals[_modals.Count - 1];
            var exit = _closing ? " data-motion=\"exit\"" : "";
            string title, body;
            switch (modal)
            {
                case "Economy":
                    title = T("economy.title"); body = _economyPanel.Render(); break;
                case "GuideSkip":
                    title = T("guide.skip.title");
                    body = HtmlUi.Text(T("guide.skip.explain"), "s-sub")
                        + OverlayButton("guide-keep", "guide.keep", () => CloseTopModal(), "primary")
                        + OverlayButton("guide-skip-confirm", "guide.skip", () => { _services.Tutorial.Skip(); CloseTopModal(); }, "quiet");
                    break;
                case "Settings":
                    var catKey = SettingsPanel.CategoryKey(_setNav);
                    title = catKey != null ? T(catKey) : T("menu.settings");
                    body = SettingsPanel.Render(_services, _overlay, _setNav);
                    break;
                case "WishLegend":
                    title=T("guide.signs.title");
                    var legend=new StringBuilder("<view class=\"column\">");
                    foreach(var sign in new[]{"bounds","overlap","path","shade","quiet","friends"})
                        legend.Append("<view class=\"wish-explanation\">").Append(CampIcons.Mark(SignIcon(sign))).Append(HtmlUi.Text(T("guide.sign."+sign))).Append("</view>");
                    body=legend.Append("</view>").ToString();break;
                case "Guests":
                    title = T("hud.guests");
                    body = GuestList()+CampIcons.Button(_overlay,"wish-legend","hint",()=>OpenModal("WishLegend"),"icon quiet",T("hud.wishes"));
                    break;
                default:
                    title = T("qc.pause.title");
                    body = OverlayButton("resume", "qc.resume.button", () => Action("qc.resume"), "primary")
                        + OverlayButton("signs", "guide.signs.title", () => OpenModal("WishLegend"), "quiet")
                        + OverlayButton("settings", "menu.settings", ShowSettings, "quiet")
                        + OverlayButton("menu", "menu.main", () => Action("qc.menu"), "quiet");
                    break;
            }
            if (modal == "Settings")
                return SettingsPanel.Shell(_services, _overlay, _setNav, body, SheetBack, () => CloseTopModal(), exit);
            // Sheet rides inside .sheet-zone; its padding keeps content inside
            // the safe area while the dim covers the entire display. The top
            // ‹ is the only back control — inside a settings category it steps
            // up to the category list before closing the sheet.
            return HtmlUi.Template("Sheet",
                    "nav", HtmlUi.Button(_overlay, "back", "‹", SheetBack, "nav-back",
                        tooltip: T("action.back")),
                    "title", HtmlUi.Escape(title), "body", body, "footer", "",
                    "body-id", "camp-body-" + modal + "-" + (_setNav.Category ?? "root"))
                .Replace("data-motion-role=\"edge-bottom\"",
                    "data-motion-role=\"edge-bottom\"" + exit);
        }

        public void NavigateBack() => SheetBack();
        void SheetBack()
        {
            if (_modals.Count > 0 && _modals[_modals.Count - 1] == "Settings"
                && _setNav.Back())
            {
                _services.Audio?.Play("ui.back");
                _overlay.Refresh();
                return;
            }
            CloseTopModal();
        }

        string GuestList()
        {
            var rows = new StringBuilder();
            for (var i = 0; i < _session.Level.guests.Length; i++)
            {
                var guest = _session.Level.guests[i];
                var placed = _session.State.Contains(guest.id);
                var selected = _session.SelectedGuestId == guest.id;
                var key = "guest-" + i;
                _overlay.Callbacks.Bind(key, () => { SelectGuest(guest.id); CloseTopModal(); });
                rows.Append("<button id=\""+key+"\" class=\"guest-row "+(selected ? "selected" : placed ? "done" : "quiet")+"\" onClick=\"Globals.campUi.Click('"+key+"')\">")
                    .Append(CampIcons.Mark(placed ? "check" : "tent")).Append(HtmlUi.Text(T(guest.nameKey)))
                    .Append(CampIcons.Wishes(_session.Level,guest)).Append("</button>");
            }
            return "<view class=\"column\">" + rows + "</view>";
        }

        void OnExitFinished(string id)
        {
            if (!_closing || id != "sheet") return;
            _closing = false;
            _modals.RemoveAt(_modals.Count - 1);
            RefreshAll();
            if (_modals.Count == 0)
            {
                _release?.Kill();
                _release = DOVirtual.DelayedCall(.12f, ReleaseModalLeases, true);
            }
        }

        // ─── Modal stack ─────────────────────────────────────────────────────

        void OpenModal(string modal)
        {
            if (Enum.TryParse<ComfortScreen>(modal, out var screen)) _services.Analytics.Screen(screen);
            _release?.Kill(); _release = null;
            _closing = false;
            if (modal == "Settings") _setNav.Open(null);
            if (_modalLeases.Count == 0)
            {
                var block = _services.InputPolicy?.AcquireBlock(GameplayInputKind.AllPointer, this);
                if (block != null) _modalLeases.Add(block);
                _modalLeases.Add(_services.ContextStack.Push(new UiContextRegistration("CampHtmlModal", UiContextLayer.Modal, 100,
                    () => HasModalOpen, new UiActionId("qc.back"), blocksLowerHotkeys: true,
                    allowedHotkeyActionIds: new[] { new UiActionId("qc.back") })));
            }
            _modals.Remove(modal); _modals.Add(modal);
            RefreshAll();
        }

        /// <summary>Outside scrim tap / back: the sheet stays mounted under
        /// data-motion="exit" until Motion.ExitFinished settles the close.</summary>
        public bool CloseTopModal()
        {
            if (!HasModalOpen || _closing) return false;
            _closing = true;
            _services.Audio?.Play("ui.back");
            RefreshAll();
            return true;
        }

        void ReleaseModalLeases()
        {
            foreach (var lease in _modalLeases) lease.Dispose();
            _modalLeases.Clear();
        }

        public void CloseAllModals()
        {
            _modals.Clear(); _closing = false;
            RefreshAll();
            _release?.Kill();
            _release = DOVirtual.DelayedCall(.12f, ReleaseModalLeases, true);
        }

        public void ShowPause() { _services.Audio?.Play("sfx.pause"); OpenModal("Pause"); }
        public void ShowSettings() => OpenModal("Settings");
        public void ShowEconomy() { _economyPanel?.ResetConfirmation(); OpenModal("Economy"); }
        public void BeginStory(Action skip) { _storyPlaying = true; _skipStory = skip; RefreshAll(); }
        public void EndStory() { _storyPlaying = false; _skipStory = null; RefreshAll(); }
        public void HidePause()
        {
            if (_modals.Contains("Pause")) CloseTopModal();
        }

        // ─── Gameplay-facing API ─────────────────────────────────────────────

        public void SelectGuest(string id)
        {
            _services.Actions.Execute(new UiActionRequest(new UiActionId("qc.select"), UiActionSource.Button, "Gameplay", id, id));
            GuestSelected?.Invoke(id);
            _surface.Refresh();
        }

        /// <summary>Briefly floats the guest's name over its tent — the
        /// "was placed" beat after a manual commit.</summary>
        public void FlashTag(string guestId)
        {
            _tagGuest = guestId;
            _tagUntil = Time.unscaledTime + TagSeconds;
            _surface.Refresh();
        }

        public void SetTutorial(string key)
        {
            var changed = _tutorialKey != key;
            _tutorialKey = key;
            if (changed && key != null)
                _services.Audio?.Play("ui.select", new Kruty1918.Audio.AudioPlayOptions(volumeScale: .65f));
            _surface.Refresh();
        }

        /// <summary>Transient mentor advice — a mistake while guiding, or a
        /// first-seen sign on any glade. Sits on the guide card for a few
        /// seconds, then yields to the step cue again.</summary>
        public void ShowGuideHint(string key, float seconds = 5f)
        {
            if (key == null) return;
            SetTutorial(key);
            _hintUntil = Time.unscaledTime + seconds;
        }

        void OnTutorialChanged()
        {
            SetTutorial(_services.Tutorial.Cue(_session.Level.id));
            _overlay?.Refresh();
        }

        public void SetPlacementPreview(RuleReport report)
        {
            var changed = PreviewKey(_previewReport) != PreviewKey(report);
            _previewReport = report;
            if (changed) _surface.Refresh();
        }

        string PreviewKey(RuleReport report)
        {
            if (report == null) return null;
            var issue = report.Issues.Find(i => i.GuestId == _session.SelectedGuestId)
                ?? (report.Issues.Count > 0 ? report.Issues[0] : null);
            return issue == null ? "hud.preview.ok" : IssueKey(report, issue);
        }

        static string IssueKey(RuleReport report, RuleIssue issue)
        {
            if (issue.Code == "path")
            {
                var route = report.Routes.Find(r => !r.Reachable && (issue.GuestId == null || r.GuestId == issue.GuestId));
                if (route != null)
                    return route.Reason == RouteFailureReason.OutsideTrail ? "rule.path.outside-trail"
                        : route.Reason == RouteFailureReason.Blocked ? "rule.path.blocked" : "rule.path.disconnected";
            }
            return "rule." + issue.Code;
        }

        static string SignIcon(string sign) => sign switch
        {
            "bounds" => "tent", "overlap" => "remove", "friends" => "guests",
            _ => sign,
        };

        /// <summary>The finished camp stays available to enjoy until Next is tapped.</summary>
        void BeginCelebration()
        {
            _celebrating = true;
            _completionBlock = _services.InputPolicy?.AcquireBlock(GameplayInputKind.AllPointer, this);
            RefreshAll();
        }

        public void Tick()
        {
            if (_tagGuest != null && Time.unscaledTime >= _tagUntil)
            { _tagGuest = null; _surface.Refresh(); }
            if (_hintUntil > 0 && Time.unscaledTime >= _hintUntil)
            {
                _hintUntil = -1;
                SetTutorial(_services.Tutorial.Cue(_session.Level.id));
            }
        }

        World.PlacementController _placement;
        public void BindPlacement(World.PlacementController placement) { _placement = placement; }
        void CacheAnchors()
        {
            var card = _surface.Element("guest-card");
            var guest = NextUnplaced();
            if (card != null && guest != null && _placement != null)
            {
                var input = card.GetComponent<GuestCardInput>() ?? card.gameObject.AddComponent<GuestCardInput>();
                input.Configure(_placement,guest.id);
                var button = card.GetComponent<UnityEngine.UI.Button>();
                if (button != null) button.interactable = !_placement.HasPreview;
            }
            _anchorRects.Clear();
            foreach (var rect in _surface.GetComponentsInChildren<RectTransform>())
                foreach (var pair in _projectedAnchors)
                    if (rect.name == "<view #tent-chip-" + pair.Key + ">")
                        _anchorRects.Add((pair.Key, rect, pair.Value));
        }

        /// <summary>Follow animated tents without rebuilding the HTML document every frame.</summary>
        public void TickAnchors()
        {
            if (GuestAnchor == null || ProjectToHud == null) return;
            for (var i = 0; i < _anchorRects.Count; i++)
            {
                var item = _anchorRects[i];
                var anchor = GuestAnchor(item.Guest);
                if (item.Rect == null || !anchor.HasValue) continue;
                var at = ProjectToHud(anchor.Value + Vector3.up * 1.15f);
                if (!at.HasValue) continue;
                // A tent at the far edge may project behind the header. Keep the
                // whole tag and its touch controls inside the playable viewport.
                // Track the clamped target so it also releases smoothly from an edge.
                var target=at.Value;
                var size=item.Rect.rect.size;var viewport=SurfaceRect.rect.size;
                float left=Mathf.Max(24,_boardViewport?.offsetMin.x??24);
                float right=Mathf.Max(24,-(_boardViewport?.offsetMax.x??-24));
                float bottom=Mathf.Max(24,_boardViewport?.offsetMin.y??24);
                float minX=left+size.x*.5f,maxX=viewport.x-right-size.x*.5f;
                float minY=180+size.y,maxY=viewport.y-bottom;
                target.x=maxX>=minX?Mathf.Clamp(target.x,minX,maxX):viewport.x*.5f;
                target.y=maxY>=minY?Mathf.Clamp(target.y,minY,maxY):Mathf.Min(viewport.y-24,minY);
                var delta = target - item.At;
                item.Rect.anchoredPosition += new Vector2(delta.x, -delta.y);
                _anchorRects[i] = (item.Guest, item.Rect, target);
            }
        }

        void ShowIssueArea()
        {
            if (_lastReport == null || _lastReport.Issues.Count == 0) return;
            var issue = _lastReport.Issues[0];
            if (issue.Cells != null && issue.Cells.Length > 0) AreaShown?.Invoke(issue.Cells);
            else
            {
                var placement = _session.State.Find(issue.GuestId);
                if (placement != null) AreaShown?.Invoke(RuleEvaluator.Footprint(placement));
            }
            if (issue.GuestId != null) SelectGuest(issue.GuestId);
        }

        void OnSessionEvent(CampEvent e)
        {
            if (e.Kind == CampEventKind.BoardCommitted) _showIssue = false;
            if (e.Kind == CampEventKind.RulesChanged) _lastReport = e.Report;
            if (e.Kind == CampEventKind.LevelCompleted) BeginCelebration();
            RefreshAll();
        }

        public void Dispose()
        {
            _services.Tutorial.Changed -= OnTutorialChanged;
            _services.MonetizationChanged -= RefreshAll;
            _economyPanel?.Dispose();
            _session.Evented -= OnSessionEvent;
            if (_surface != null)
            {
                _surface.Mounted -= CacheAnchors;
                _surface.Mounted -= AdaptBoardViewport;
                _surface.LayoutChanged -= AdaptBoardViewport;
            }
            _release?.Kill(); ReleaseModalLeases();
            _completionBlock?.Dispose();
            if (_surface != null) UnityEngine.Object.Destroy(_surface.gameObject);
            if (_overlay != null) UnityEngine.Object.Destroy(_overlay.gameObject);
        }
    }
}
