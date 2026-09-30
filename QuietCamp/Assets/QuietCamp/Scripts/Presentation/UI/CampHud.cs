using System;
using System.Collections.Generic;
using DG.Tweening;
using Kruty1918.Audio;
using Kruty1918.InputRouting.API;
using Kruty1918.UIActions.API;
using QuietCamp.Application;
using QuietCamp.Domain;
using UnityEngine;
using UnityEngine.UI;
namespace QuietCamp.Presentation.UI
{
    /// <summary>
    /// Runtime gameplay HUD under CanvasRoot/SafeArea: header, rule chips,
    /// guest tray, action panel, bottom nav, modal and toast layers.
    /// Displays state only — no BFS or win math here.
    /// </summary>
    public sealed class CampHud
    {
        readonly GameServices _services;
        readonly CampSession _session;
        readonly RectTransform _safeArea;
        readonly ScreenRouter _router;
        readonly List<Button> _guestCards = new List<Button>();
        readonly List<Image> _guestMarks = new List<Image>();
        readonly Dictionary<string, Image> _chips = new Dictionary<string, Image>();
        readonly List<IDisposable> _contexts = new List<IDisposable>();
        readonly Dictionary<CanvasGroup, List<IDisposable>> _modalContexts =
            new Dictionary<CanvasGroup, List<IDisposable>>();
        readonly List<CanvasGroup> _openStack = new List<CanvasGroup>();

        Button _checkButton, _rotateButton, _undoButton, _redoButton, _removeButton, _hintButton;
        CanvasGroup _pausePanel, _hintPanel, _completionPanel, _settingsPanel;
        TextMeshProUGUICompat _statusText;
        RuleReport _lastReport;
        HintService _hint;

        public RectTransform ToastLayer { get; private set; }
        public bool HasModalOpen =>
            (_pausePanel != null && _pausePanel.gameObject.activeSelf)
            || (_hintPanel != null && _hintPanel.gameObject.activeSelf)
            || (_completionPanel != null && _completionPanel.gameObject.activeSelf)
            || (_settingsPanel != null && _settingsPanel.gameObject.activeSelf);
        public event Action<string> GuestSelected;

        public CampHud(GameServices services, CampSession session, RectTransform safeArea,
            ScreenRouter router)
        {
            _services = services;
            _session = session;
            _safeArea = safeArea;
            _router = router;
            _hint = new HintService(session.Level);
            Build();
            session.Evented += OnSessionEvent;
            _services.Localization.LanguageChanged += SetLevelTitle;
            RefreshAll(RuleEvaluator.Evaluate(session.Level, session.State.Placements));
        }

        // ─── Layout ──────────────────────────────────────────────────────────

        void Build()
        {
            var gameplay = _safeArea.Find("Gameplay");
            var root = gameplay != null ? (RectTransform)gameplay : _safeArea;
            BuildHeader(root);
            BuildRulesRow(root);
            BuildMessageSlot(root);
            BuildTray(root);
            BuildDock(root);
            BuildModals();
            // The camera renders full-screen scenery, but interactive geometry stays clear of the HUD.
            var viewport = root.Find("BoardViewport") as RectTransform;
            if (viewport != null)
            {
                viewport.anchorMin = Vector2.zero;
                viewport.anchorMax = Vector2.one;
                viewport.offsetMin = new Vector2(16, 340);
                viewport.offsetMax = new Vector2(-16, -196);
            }
        }

        void BuildHeader(RectTransform root)
        {
            var header = QcUi.Anchor(root, "Header",
                new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(16, -108), new Vector2(-16, -20));
            var title = QcUi.Anchor(header, "Title",
                new Vector2(0.5f, 0), new Vector2(0.5f, 1),
                new Vector2(-370, 0), new Vector2(370, 0));
            var plateImg = QcUi.PanelImage(title, "bg",
                new Color(QcUi.Cream.r, QcUi.Cream.g, QcUi.Cream.b, 0.85f));
            plateImg.raycastTarget = false;
            var tmp = QcUi.PlainText(title, "", QcUi.TextBody,
                TMPro.TextAlignmentOptions.Center, QcUi.Ink);
            tmp.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
            _statusText = new TextMeshProUGUICompat(tmp.gameObject);
            SetLevelTitle();
            var pause = QcUi.Button(header, "qc.pause.button",
                () => _services.Actions.Execute(new UiActionId("qc.pause"), UiActionSource.Button, "Gameplay"),
                QcUi.Cream);
            var pauseRt = (RectTransform)pause.transform;
            pauseRt.anchorMin = new Vector2(1, 0.5f); pauseRt.anchorMax = new Vector2(1, 0.5f);
            pauseRt.pivot = new Vector2(1, 0.5f);
            pauseRt.sizeDelta = new Vector2(160f, 76f);
            pauseRt.anchoredPosition = Vector2.zero;
        }

        void BuildRulesRow(RectTransform root)
        {
            var row = QcUi.Anchor(root, "RulesRow",
                new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(16, -176), new Vector2(-16, -118));
            var used = new List<string>();
            foreach (var g in _session.Level.guests)
            {
                if (g.shade && !used.Contains("rule.shade")) used.Add("rule.shade");
                if (g.quiet && !used.Contains("rule.quiet")) used.Add("rule.quiet");
            }
            if (_session.Level.friends != null && _session.Level.friends.Length > 0)
                used.Add("rule.friends");
            used.Add("rule.path");
            float w = 1f / Mathf.Max(1, used.Count);
            var chipBg = new Color(QcUi.CreamDark.r, QcUi.CreamDark.g, QcUi.CreamDark.b, 0.72f);
            for (var i = 0; i < used.Count; i++)
            {
                var chip = QcUi.Anchor(row, "chip_" + used[i],
                    new Vector2(i * w + 0.004f, 0), new Vector2((i + 1) * w - 0.004f, 1),
                    Vector2.zero, Vector2.zero);
                var img = QcUi.PanelImage(chip, "bg", chipBg);
                _chips[used[i]] = img;
                // Compact chips carry the short wish word; the full sentence
                // lives in the message slot when the rule actually fails.
                var chipLbl = QcUi.Label(chip, "wish." + used[i].Substring("rule.".Length),
                    QcUi.TextSmall - 4f, TMPro.TextAlignmentOptions.Center, QcUi.Ink);
                var chipTmp = chipLbl.GetComponent<TMPro.TMP_Text>();
                if (chipTmp != null)
                {
                    chipTmp.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
                    chipTmp.overflowMode = TMPro.TextOverflowModes.Ellipsis;
                }
            }
        }

        void BuildTray(RectTransform root)
        {
            var tray = QcUi.Anchor(root, "GuestTray",
                new Vector2(0, 0), new Vector2(1, 0),
                new Vector2(16, 140), new Vector2(-16, 268));
            var scroll = QcUi.Stretch(tray, "Scroll");
            var scrollRect = scroll.gameObject.AddComponent<ScrollRect>();
            scrollRect.horizontal = true;
            scrollRect.vertical = false;
            scrollRect.decelerationRate = 0.08f;
            scrollRect.scrollSensitivity = _services.Settings.scrollSensitivity;
            var content = QcUi.Anchor(scroll, "Content",
                new Vector2(0, 0), new Vector2(1, 1), Vector2.zero, Vector2.zero);
            scrollRect.content = content;
            scrollRect.viewport = scroll;
            scroll.gameObject.AddComponent<UnityEngine.UI.Image>().color =
                new Color(1f, 1f, 1f, 0.06f);
            var mask = scroll.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = true;

            var layout = content.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 12;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childAlignment = TextAnchor.MiddleLeft;

            foreach (var g in _session.Level.guests)
            {
                var card = QcUi.Root(content, "card_" + g.id);
                card.sizeDelta = new Vector2(260f, 118f);
                var img = QcUi.PanelImage(card, "bg", QcUi.Cream);
                img.raycastTarget = true;
                var btn = card.gameObject.AddComponent<Button>();
                btn.targetGraphic = img;
                var guestId = g.id;
                btn.onClick.AddListener(() => _services.Actions.Execute(
                    new UiActionRequest(new UiActionId("qc.select"), UiActionSource.Button, "Gameplay", guestId, guestId)));
                // Selection marker: an explicit badge, not colour alone.
                var mark = QcUi.Icon(card, QcUi.IconCheck, 40f, QcUi.GreenDark);
                mark.rectTransform.anchorMin = new Vector2(1, 1);
                mark.rectTransform.anchorMax = new Vector2(1, 1);
                mark.rectTransform.pivot = new Vector2(1, 0.5f);
                mark.rectTransform.anchoredPosition = new Vector2(-10, -34);
                mark.gameObject.SetActive(false);
                _guestMarks.Add(mark);
                var name = QcUi.Anchor(card, "Name",
                    new Vector2(0, 0.5f), new Vector2(1, 1), Vector2.zero, Vector2.zero);
                var nameLbl = QcUi.Label(name, g.nameKey, QcUi.TextBody,
                    TMPro.TextAlignmentOptions.Center, QcUi.Ink);
                var nameTmp = nameLbl.GetComponent<TMPro.TMP_Text>();
                if (nameTmp != null) nameTmp.fontStyle = TMPro.FontStyles.Bold;
                var wishes = QcUi.Anchor(card, "Wishes",
                    new Vector2(0, 0), new Vector2(1, 0.5f), Vector2.zero, Vector2.zero);
                QcUi.PlainText(wishes, WishText(g), QcUi.TextSmall - 2f,
                    TMPro.TextAlignmentOptions.Center, QcUi.GreenDark);
                _guestCards.Add(btn);
            }
        }

        /// <summary>Compact wish summary — every wish listed, "·" separated.</summary>
        string WishText(GuestData g)
        {
            var parts = new List<string>();
            if (g.shade) parts.Add(_services.Localization.T("wish.shade"));
            if (g.quiet) parts.Add(_services.Localization.T("wish.quiet"));
            if (_session.Level.friends != null)
                foreach (var pair in _session.Level.friends)
                    if (pair != null && System.Array.IndexOf(pair, g.id) >= 0)
                    {
                        parts.Add(_services.Localization.T("wish.friends"));
                        break;
                    }
            if (parts.Count == 0) parts.Add(_services.Localization.T("wish.path"));
            return string.Join(" · ", parts);
        }

        /// <summary>
        /// Message slot: the single surface for rule issues. Tap highlights
        /// the guest's footprint on the board so the player sees *where* the
        /// problem is, not only what it is. Hidden while no issue exists.
        /// </summary>
        void BuildMessageSlot(RectTransform root)
        {
            var slot = QcUi.Anchor(root, "MessageSlot",
                new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(24, -264), new Vector2(-24, -196));
            var img = QcUi.PanelImage(slot, "bg", QcUi.Cream);
            img.raycastTarget = true;
            var btn = slot.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(ShowIssueArea);
            _messageText = QcUi.PlainText(slot, "", QcUi.TextSmall,
                TMPro.TextAlignmentOptions.Center, new Color(0.55f, 0.30f, 0.18f));
            slot.gameObject.SetActive(false);
            _messageSlot = slot.gameObject;
        }

        GameObject _messageSlot;
        TMPro.TMP_Text _messageText;

        /// <summary>
        /// Context dock — the single bottom action area. Idle state shows
        /// only the calm verbs (undo/redo/hint/check); object verbs (rotate,
        /// remove) appear once a guest or tent is actually selected.
        /// </summary>
        void BuildDock(RectTransform root)
        {
            var dock = QcUi.Anchor(root, "ContextDock",
                new Vector2(0, 0), new Vector2(1, 0),
                new Vector2(16, 20), new Vector2(-16, 128));
            var bg = QcUi.PanelImage(dock, "bg",
                new Color(QcUi.Cream.r, QcUi.Cream.g, QcUi.Cream.b, 0.92f));
            bg.raycastTarget = true;

            var row = QcUi.Stretch(dock, "row");
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(10, 10, 8, 8);
            layout.spacing = 10;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;

            _undoButton = QcUi.IconButton(row, QcUi.IconUndo,
                () => Action("qc.undo"), QcUi.Cream, 96f, QcUi.Brown);
            _redoButton = QcUi.IconButton(row, QcUi.IconRepeat,
                () => Action("qc.redo"), QcUi.Cream, 96f, QcUi.Brown);
            _rotateButton = QcUi.Button(row, "action.rotate",
                () => Action("qc.rotate"), QcUi.Cream);
            _rotateButton.gameObject.AddComponent<LayoutElement>().preferredWidth = 210f;
            _removeButton = QcUi.Button(row, "action.remove",
                () => Action("qc.remove"), QcUi.Cream);
            _removeButton.gameObject.AddComponent<LayoutElement>().preferredWidth = 250f;
            _hintButton = QcUi.Button(row, "action.hint",
                () => Action("qc.hint"), QcUi.Cream);
            _hintButton.gameObject.AddComponent<LayoutElement>().preferredWidth = 190f;
            _checkButton = QcUi.Button(row, "action.check",
                () => Action("qc.check"), QcUi.Green);
            _checkButton.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1.4f;
        }

        void BuildModals()
        {
            var modalLayer = QcUi.Stretch(_safeArea, "ModalLayer");
            modalLayer.SetAsLastSibling();
            _pausePanel = BuildPausePanel(modalLayer);
            _hintPanel = BuildHintPanel(modalLayer);
            _completionPanel = BuildCompletionPanel(modalLayer);
            _settingsPanel = BuildSettingsPanel(modalLayer);
            ToastLayer = QcUi.Stretch(_safeArea, "ToastLayer");
            ToastLayer.SetAsLastSibling();
        }

        /// <summary>Pause is a compact bottom sheet — the camp stays visible
        /// behind it, one primary action returns to play.</summary>
        CanvasGroup BuildPausePanel(RectTransform layer)
        {
            var group = QcUi.Modal(layer, "PausePanel", new Color(0.1f, 0.08f, 0.06f, 0.45f));
            var card = QcUi.Anchor(group.transform as RectTransform, "Card",
                new Vector2(0, 0), new Vector2(1, 0),
                new Vector2(16, 20), new Vector2(-16, 20 + 560));
            QcUi.PanelImage(card, "bg", QcUi.Cream);
            var col = QcUi.Stretch(card, "col");
            var layout = col.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(28, 28, 24, 28);
            layout.spacing = 14;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            var title = QcUi.Label(col, "qc.pause.title", QcUi.TextTitle,
                TMPro.TextAlignmentOptions.Center, QcUi.Ink);
            title.gameObject.AddComponent<LayoutElement>().minHeight = 72;
            var resume = QcUi.Button(col, "qc.resume.button",
                () => Action("qc.resume"), QcUi.Green);
            resume.gameObject.AddComponent<LayoutElement>().minHeight = 120;
            var row = QcUi.Root(col, "row");
            row.gameObject.AddComponent<LayoutElement>().minHeight = 96;
            var rl = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            rl.spacing = 12;
            rl.childForceExpandWidth = true;
            rl.childForceExpandHeight = true;
            QcUi.Button(row, "action.hint", () =>
            {
                CloseModal(_pausePanel);
                ShowHint();
            }, QcUi.Cream);
            QcUi.Button(row, "menu.settings", () => Action("qc.settings"), QcUi.Cream);
            var exit = QcUi.Button(col, "menu.main", () => Action("qc.levels"), QcUi.Brown);
            exit.gameObject.AddComponent<LayoutElement>().minHeight = 96;
            return group;
        }

        CanvasGroup BuildHintPanel(RectTransform layer)
        {
            var group = QcUi.Modal(layer, "HintPanel", new Color(0.1f, 0.08f, 0.06f, 0.45f));
            var card = QcUi.Anchor(group.transform as RectTransform, "Card",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-340, -220), new Vector2(340, 220));
            QcUi.PanelImage(card, "bg", QcUi.Cream);
            var col = QcUi.Stretch(card, "col");
            var layout = col.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(30, 30, 30, 30);
            layout.spacing = 16;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;
            QcUi.Label(col, "action.hint", QcUi.TextTitle, TMPro.TextAlignmentOptions.Center, QcUi.Ink);
            var body = QcUi.Anchor(col, "Body", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            body.gameObject.AddComponent<LayoutElement>().flexibleHeight = 2;
            _hintText = QcUi.PlainText(body, "", QcUi.TextBody,
                TMPro.TextAlignmentOptions.Center, QcUi.Ink);
            QcUi.Button(col, "hint.more", () => NextHint(), QcUi.Cream);
            QcUi.Button(col, "action.back", () => CloseModal(_hintPanel), QcUi.Brown);
            return group;
        }

        CanvasGroup BuildCompletionPanel(RectTransform layer)
        {
            var group = QcUi.Modal(layer, "CompletionPanel", new Color(0.1f, 0.08f, 0.06f, 0.55f));
            var card = QcUi.Anchor(group.transform as RectTransform, "Card",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-360, -280), new Vector2(360, 280));
            QcUi.PanelImage(card, "bg", QcUi.Cream);
            var col = QcUi.Stretch(card, "col");
            var layout = col.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(30, 30, 30, 30);
            layout.spacing = 18;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;
            QcUi.Label(col, "rule.ok", QcUi.TextTitle, TMPro.TextAlignmentOptions.Center, QcUi.GreenDark);
            QcUi.Button(col, "action.next", () => Action("qc.next"), QcUi.Green);
            QcUi.Button(col, "menu.album", () => Action("qc.album"), QcUi.Cream);
            QcUi.Button(col, "menu.main", () => Action("qc.levels"), QcUi.Brown);
            return group;
        }

        /// <summary>In-game settings: same rows as the menu Settings screen.</summary>
        CanvasGroup BuildSettingsPanel(RectTransform layer)
        {
            var group = QcUi.Modal(layer, "SettingsPanel", new Color(0.1f, 0.08f, 0.06f, 0.6f));
            var card = QcUi.Anchor(group.transform as RectTransform, "Card",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-440, -720), new Vector2(440, 720));
            QcUi.PanelImage(card, "bg", QcUi.Cream);
            var scroll = QcUi.Stretch(card, "scroll");
            var scrollRect = scroll.gameObject.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scroll.gameObject.AddComponent<RectMask2D>();
            var content = QcUi.Anchor(scroll, "Content",
                new Vector2(0, 1), new Vector2(1, 1), Vector2.zero, Vector2.zero);
            content.pivot = new Vector2(0.5f, 1f);
            scrollRect.content = content;
            scrollRect.viewport = scroll;
            scrollRect.verticalNormalizedPosition = 1f;
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(36, 36, 28, 28);
            layout.spacing = 14;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit =
                ContentSizeFitter.FitMode.PreferredSize;

            QcUi.Label(content, "menu.settings", QcUi.TextTitle,
                TMPro.TextAlignmentOptions.Center, QcUi.Ink)
                .gameObject.AddComponent<LayoutElement>().minHeight = 80;

            SettingsPanel.BuildRows(_services, content);

            var back = QcUi.Button(content, "action.back",
                () => CloseModal(_settingsPanel), QcUi.Brown);
            back.gameObject.AddComponent<LayoutElement>().minHeight = 110;
            return group;
        }

        TMPro.TMP_Text _hintText;
        string _hintFocusGuest;

        // ─── Modal plumbing ──────────────────────────────────────────────────

        void OpenModal(CanvasGroup panel, string contextId)
        {
            if (panel.gameObject.activeSelf) return;
            panel.gameObject.SetActive(true);
            var leases = new List<IDisposable>();
            var block = _services.InputPolicy?.AcquireBlock(GameplayInputKind.AllPointer, this);
            if (block != null) leases.Add(block);
            _services.Motion.SetPanelVisible(panel, panel.transform as RectTransform, true, 0.18f);
            var ctx = _services.ContextStack.Push(new UiContextRegistration(
                contextId, UiContextLayer.Modal, 100, () => panel.gameObject.activeSelf,
                new UiActionId("qc.back"), blocksLowerHotkeys: true,
                allowedHotkeyActionIds: new[] { new UiActionId("qc.back") }));
            leases.Add(ctx);
            _modalContexts[panel] = leases;
            _openStack.Remove(panel);
            _openStack.Add(panel);
        }

        void CloseModal(CanvasGroup panel)
        {
            if (panel == null || !panel.gameObject.activeSelf) return;
            _services.Audio?.Play("ui.back");
            const float fade = 0.12f;
            _services.Motion.SetPanelVisible(panel, panel.transform as RectTransform, false, fade);
            _openStack.Remove(panel);
            if (_modalContexts.TryGetValue(panel, out var leases))
            {
                // The closing tap must not leak into the board — pointer
                // blocking and the modal context stay until the fade ends.
                _modalContexts.Remove(panel);
                var captured = leases;
                DOVirtual.DelayedCall(fade + 0.05f, () =>
                {
                    foreach (var b in captured) b.Dispose();
                }, true);
            }
        }

        // ─── Events & refresh ────────────────────────────────────────────────

        void Action(string id)
        {
            _services.Audio?.Play("ui.click");
            _services.Actions.Execute(new UiActionId(id), UiActionSource.Button, "Gameplay");
        }

        void OnSessionEvent(CampEvent e)
        {
            switch (e.Kind)
            {
                case CampEventKind.BoardCommitted:
                    _hint.Reset();
                    RefreshCards();
                    break;
                case CampEventKind.RulesChanged:
                    _lastReport = e.Report;
                    RefreshChips(e.Report);
                    break;
                case CampEventKind.SelectionChanged:
                    RefreshCards();
                    break;
                case CampEventKind.LevelCompleted:
                    RefreshCards();
                    ShowCompletion();
                    break;
            }
        }

        void RefreshAll(RuleReport report)
        {
            RefreshCards();
            RefreshChips(report);
            SetLevelTitle();
        }

        void SetLevelTitle()
        {
            _statusText?.Set($"{LevelDisplay.Title(_session.Level.id, _services.Localization)} · {_services.Localization.T("chapter." + _session.Level.chapter)}");
        }

        void RefreshCards()
        {
            for (var i = 0; i < _guestCards.Count && i < _session.Level.guests.Length; i++)
            {
                var g = _session.Level.guests[i];
                var placed = _session.State.Contains(g.id);
                var selected = _session.SelectedGuestId == g.id;
                var img = _guestCards[i].GetComponentInChildren<Image>();
                img.color = placed
                    ? new Color(0.72f, 0.84f, 0.70f)
                    : selected ? QcUi.Amber : QcUi.Cream;
                // Placed = check badge; selected = raised pointer — never colour alone.
                var mark = _guestMarks[i];
                mark.gameObject.SetActive(placed || selected);
                mark.sprite = QcUi.Sprite(placed ? QcUi.IconCheck : QcUi.IconArrowUp);
                mark.color = placed ? QcUi.GreenDark : QcUi.Brown;
            }
            _undoButton.interactable = _session.CanUndo;
            _redoButton.interactable = _session.CanRedo;
            // Object verbs exist only while an object is chosen — the idle
            // dock shows just the calm verbs, not a disabled toolbar.
            var sel = _session.SelectedGuestId;
            var selPlaced = sel != null && _session.State.Contains(sel);
            if (_rotateButton != null) _rotateButton.gameObject.SetActive(sel != null);
            if (_removeButton != null) _removeButton.gameObject.SetActive(selPlaced);
        }

        void RefreshChips(RuleReport report)
        {
            _lastReport = report;
            foreach (var kv in _chips)
            {
                var code = kv.Key.Substring("rule.".Length);
                var failing = report != null && report.Issues.Exists(i => i.Code == code
                    || (code == "friends" && i.Code == "friends-missing"));
                kv.Value.color = failing
                    ? new Color(0.93f, 0.62f, 0.40f)
                    : new Color(0.66f, 0.82f, 0.62f);
            }
            RefreshMessage();
        }

        /// <summary>One issue at a time in the message slot — tapping it
        /// highlights the affected tent so the cause is visible, not just text.</summary>
        void RefreshMessage()
        {
            if (_messageSlot == null) return;
            var issue = _lastReport != null && _lastReport.Issues.Count > 0
                && !_lastReport.IsSolved ? _lastReport.Issues[0] : null;
            _messageSlot.SetActive(issue != null);
            if (issue != null)
            {
                _messageText.text = _services.Localization.T("rule." + issue.Code);
                _messageIssue = issue;
            }
        }

        RuleIssue _messageIssue;

        void ShowIssueArea()
        {
            var issue = _messageIssue;
            if (issue == null) return;
            // Concrete cells first (blocked door, unshaded cells) — that is
            // the spot the player must fix; otherwise the guest's footprint.
            if (issue.Cells != null && issue.Cells.Length > 0)
            {
                AreaShown?.Invoke(issue.Cells);
                return;
            }
            var p = issue.GuestId == null ? null : _session.State.Find(issue.GuestId);
            if (p != null) AreaShown?.Invoke(RuleEvaluator.Footprint(p));
            else if (issue.GuestId != null)
                _services.Actions.Execute(new UiActionRequest(
                    new UiActionId("qc.select"), UiActionSource.Button,
                    "Gameplay", issue.GuestId, issue.GuestId));
        }

        // ─── Public API used by CampSceneHost ────────────────────────────────

        public void SelectGuest(string guestId) => _session.Select(guestId);
        public void ShowPause() => OpenModal(_pausePanel, "Pause");
        public void ShowSettings() => OpenModal(_settingsPanel, "Settings");
        public void HidePause() => CloseModal(_pausePanel);

        /// <summary>Closes only the topmost modal — used by Android Back / Escape.</summary>
        public bool CloseTopModal()
        {
            while (_openStack.Count > 0)
            {
                var top = _openStack[_openStack.Count - 1];
                if (top != null && top.gameObject.activeSelf)
                {
                    CloseModal(top);
                    return true;
                }
                _openStack.RemoveAt(_openStack.Count - 1);
            }
            return false;
        }
        public void CloseAllModals()
        {
            CloseModal(_pausePanel);
            CloseModal(_hintPanel);
            CloseModal(_completionPanel);
            CloseModal(_settingsPanel);
        }

        public void ShowHint()
        {
            if (!(_hintPanel.gameObject.activeSelf)) OpenModal(_hintPanel, "Hint");
            NextHint();
        }

        void NextHint()
        {
            // A soft tick, not the completion chime — chime is reserved for
            // the victory afterglow so the two cues never blend.
            _services.Audio?.Play("ui.select", new AudioPlayOptions(volumeScale: .7f));
            var level = _session.Level;
            switch (_hint.NextStage)
            {
                case HintService.Stage.Explain:
                    _hintText.text = _services.Localization.T(
                        _hint.Explain(_lastReport ?? RuleEvaluator.Evaluate(level, _session.State.Placements)));
                    _hint.AdvanceStage();
                    break;
                case HintService.Stage.Area:
                    _hintFocusGuest = PickHintGuest();
                    _hintText.text = _services.Localization.T("hint.searching");
                    _hint.AdvanceStage();
                    _area = _hint.AreaFor(_session, _hintFocusGuest);
                    AreaShown?.Invoke(_area);
                    _hintText.text = _area.Length > 0
                        ? _services.Localization.T(_hint.Explain(_lastReport))
                        : _services.Localization.T("hint.timeout");
                    break;
                case HintService.Stage.Move:
                    _hintText.text = _services.Localization.T("hint.searching");
                    _hint.BeginMoveSearch(_session);
                    break;
            }
        }

        Cell[] _area = new Cell[0];
        public event Action<Cell[]> AreaShown;

        string PickHintGuest()
        {
            foreach (var g in _session.Level.guests)
                if (!_session.State.Contains(g.id)) return g.id;
            return _session.Level.guests[0].id;
        }

        /// <summary>Frame pump for the hint solver; call every Update while searching.</summary>
        public void PumpHint()
        {
            _hint.Pump(0.004);
            if (_hint.Status == HintService.SearchStatus.Ready && _hint.MoveRevision == _session.State.Revision)
            {
                var move = _hint.Move;
                if (move != null)
                {
                    _hintText.text = $"{_services.Localization.T(GuestName(move.guestId))} → ({move.x},{move.z}) ↻{move.rotation * 90}°";
                    MoveShown?.Invoke(move);
                }
                _hint.Reset();
            }
            else if (_hint.Status == HintService.SearchStatus.Timeout
                || _hint.Status == HintService.SearchStatus.Unsatisfiable)
            {
                _hintText.text = _services.Localization.T("hint.timeout");
                _hint.Reset();
            }
        }

        string GuestName(string id)
        {
            var g = Array.Find(_session.Level.guests, x => x.id == id);
            return g?.nameKey ?? "guest.1";
        }

        public event Action<Placement> MoveShown;

        void ShowCompletion() => OpenModal(_completionPanel, "Completion");

        public void Dispose()
        {
            _session.Evented -= OnSessionEvent;
            _services.Localization.LanguageChanged -= SetLevelTitle;
            foreach (var kv in _modalContexts)
                foreach (var c in kv.Value) c.Dispose();
            _modalContexts.Clear();
            _contexts.Clear();
        }

        // Small adapter keeping TMP usage terse in this file.
        sealed class TextMeshProUGUICompat
        {
            readonly GameObject _go;
            public TextMeshProUGUICompat(GameObject go) => _go = go;
            public void Set(string text)
            {
                var tmp = _go.GetComponent<TMPro.TMP_Text>();
                if (tmp != null) tmp.text = text;
            }
        }
    }
}
