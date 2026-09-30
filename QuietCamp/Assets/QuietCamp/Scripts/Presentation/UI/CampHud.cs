using System;
using System.Collections.Generic;
using DG.Tweening;
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
            RefreshAll(RuleEvaluator.Evaluate(session.Level, session.State.Placements));
        }

        // ─── Layout ──────────────────────────────────────────────────────────

        void Build()
        {
            var gameplay = _safeArea.Find("Gameplay");
            var root = gameplay != null ? (RectTransform)gameplay : _safeArea;
            BuildHeader(root);
            BuildRulesRow(root);
            BuildTray(root);
            BuildActions(root);
            BuildNav(root);
            BuildModals();
        }

        void BuildHeader(RectTransform root)
        {
            var header = QcUi.Anchor(root, "Header",
                new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(16, -116), new Vector2(-16, -20));
            var back = QcUi.IconButton(header, QcUi.IconBack,
                () => _services.Actions.Execute(new UiActionId("qc.back"), UiActionSource.Button, "Gameplay"),
                QcUi.Cream, 96f);
            var backRt = (RectTransform)back.transform;
            backRt.anchorMin = new Vector2(0, 0.5f); backRt.anchorMax = new Vector2(0, 0.5f);
            backRt.pivot = new Vector2(0, 0.5f);
            backRt.anchoredPosition = Vector2.zero;
            var title = QcUi.Anchor(header, "Title",
                new Vector2(0.5f, 0), new Vector2(0.5f, 1),
                new Vector2(-300, 0), new Vector2(300, 0));
            _statusText = new TextMeshProUGUICompat(
                QcUi.PlainText(title, "", QcUi.TextButton, TMPro.TextAlignmentOptions.Center, QcUi.Ink).gameObject);
            SetLevelTitle();
            var pause = QcUi.Button(header, "qc.pause.button",
                () => _services.Actions.Execute(new UiActionId("qc.pause"), UiActionSource.Button, "Gameplay"),
                QcUi.Cream);
            var pauseRt = (RectTransform)pause.transform;
            pauseRt.anchorMin = new Vector2(1, 0.5f); pauseRt.anchorMax = new Vector2(1, 0.5f);
            pauseRt.pivot = new Vector2(1, 0.5f);
            pauseRt.sizeDelta = new Vector2(190f, 96f);
            pauseRt.anchoredPosition = Vector2.zero;
        }

        void BuildRulesRow(RectTransform root)
        {
            var row = QcUi.Anchor(root, "RulesRow",
                new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(16, -192), new Vector2(-16, -128));
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
                QcUi.Label(chip, used[i], QcUi.TextSmall - 4f,
                    TMPro.TextAlignmentOptions.Center, QcUi.Ink);
            }
        }

        void BuildTray(RectTransform root)
        {
            var tray = QcUi.Anchor(root, "GuestTray",
                new Vector2(0, 0), new Vector2(1, 0),
                new Vector2(16, 252), new Vector2(-16, 378));
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
                card.sizeDelta = new Vector2(200f, 118f);
                var img = QcUi.PanelImage(card, "bg", QcUi.Cream);
                img.raycastTarget = true;
                var btn = card.gameObject.AddComponent<Button>();
                btn.targetGraphic = img;
                var guestId = g.id;
                btn.onClick.AddListener(() => _services.Actions.Execute(
                    new UiActionRequest(new UiActionId("qc.select"), UiActionSource.Button, "Gameplay", guestId, guestId)));
                var name = QcUi.Anchor(card, "Name",
                    new Vector2(0, 0.55f), new Vector2(1, 1), Vector2.zero, Vector2.zero);
                QcUi.Label(name, g.nameKey, QcUi.TextSmall, TMPro.TextAlignmentOptions.Center, QcUi.Ink);
                var wishes = QcUi.Anchor(card, "Wishes",
                    new Vector2(0, 0), new Vector2(1, 0.55f), Vector2.zero, Vector2.zero);
                var wishKey = WishKey(g);
                QcUi.Label(wishes, wishKey, QcUi.TextSmall - 4f,
                    TMPro.TextAlignmentOptions.Center, QcUi.GreenDark);
                _guestCards.Add(btn);
            }
        }

        static string WishKey(GuestData g)
        {
            if (g.shade) return "rule.shade";
            if (g.quiet) return "rule.quiet";
            return "rule.path";
        }

        void BuildActions(RectTransform root)
        {
            var panel = QcUi.Anchor(root, "ActionStrip",
                new Vector2(0, 0), new Vector2(1, 0),
                new Vector2(16, 148), new Vector2(-16, 240));
            var bg = QcUi.Image(panel, "bg", new Color(0.85f, 0.80f, 0.70f, 0.30f));
            bg.raycastTarget = false;

            var row = QcUi.Stretch(panel, "row");
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(6, 6, 6, 6);
            layout.spacing = 8;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;

            _rotateButton = QcUi.Button(row, "action.rotate",
                () => Action("qc.rotate"), QcUi.GreenDark);
            _undoButton = QcUi.Button(row, "action.undo",
                () => Action("qc.undo"), QcUi.GreenDark);
            _redoButton = QcUi.Button(row, "action.redo",
                () => Action("qc.redo"), QcUi.GreenDark);
            _removeButton = QcUi.Button(row, "action.remove",
                () => Action("qc.remove"), QcUi.Cream);
        }

        void BuildNav(RectTransform root)
        {
            var nav = QcUi.Anchor(root, "BottomNav",
                new Vector2(0, 0), new Vector2(1, 0),
                new Vector2(16, 28), new Vector2(-16, 132));
            var layout = nav.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 12;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;
            _hintButton = QcUi.Button(nav, "action.hint",
                () => Action("qc.hint"), QcUi.Amber);
            _hintButton.gameObject.AddComponent<LayoutElement>().flexibleWidth = 0.8f;
            _checkButton = QcUi.Button(nav, "action.check",
                () => Action("qc.check"), QcUi.Green);
            _checkButton.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1.6f;
            var levels = QcUi.Button(nav, "menu.levels",
                () => Action("qc.levels"), QcUi.Cream);
            levels.gameObject.AddComponent<LayoutElement>().flexibleWidth = 0.8f;
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

        CanvasGroup BuildPausePanel(RectTransform layer)
        {
            var group = QcUi.Modal(layer, "PausePanel", new Color(0.1f, 0.08f, 0.06f, 0.6f));
            var card = QcUi.Anchor(group.transform as RectTransform, "Card",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-330, -260), new Vector2(330, 260));
            QcUi.PanelImage(card, "bg", Color.white);
            var col = QcUi.Stretch(card, "col");
            var layout = col.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(30, 30, 30, 30);
            layout.spacing = 18;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;
            QcUi.Label(col, "qc.pause.title", QcUi.TextTitle, TMPro.TextAlignmentOptions.Center, QcUi.Ink);
            QcUi.Button(col, "qc.resume.button", () => Action("qc.resume"), QcUi.Green);
            QcUi.Button(col, "menu.settings", () => Action("qc.settings"), QcUi.GreenDark);
            QcUi.Button(col, "menu.levels", () => Action("qc.back"), QcUi.Brown);
            return group;
        }

        CanvasGroup BuildHintPanel(RectTransform layer)
        {
            var group = QcUi.Modal(layer, "HintPanel", new Color(0.1f, 0.08f, 0.06f, 0.45f));
            var card = QcUi.Anchor(group.transform as RectTransform, "Card",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-340, -220), new Vector2(340, 220));
            QcUi.PanelImage(card, "bg", Color.white);
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
            QcUi.Button(col, "action.next", () => NextHint(), QcUi.Amber);
            QcUi.Button(col, "action.back", () => CloseModal(_hintPanel), QcUi.Brown);
            return group;
        }

        CanvasGroup BuildCompletionPanel(RectTransform layer)
        {
            var group = QcUi.Modal(layer, "CompletionPanel", new Color(0.1f, 0.08f, 0.06f, 0.55f));
            var card = QcUi.Anchor(group.transform as RectTransform, "Card",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-360, -280), new Vector2(360, 280));
            QcUi.PanelImage(card, "bg", Color.white);
            var col = QcUi.Stretch(card, "col");
            var layout = col.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(30, 30, 30, 30);
            layout.spacing = 18;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;
            QcUi.Label(col, "rule.ok", QcUi.TextTitle, TMPro.TextAlignmentOptions.Center, QcUi.GreenDark);
            QcUi.Button(col, "action.next", () => Action("qc.next"), QcUi.Green);
            QcUi.Button(col, "menu.album", () => Action("qc.album"), QcUi.GreenDark);
            QcUi.Button(col, "menu.levels", () => Action("qc.back"), QcUi.Brown);
            return group;
        }

        /// <summary>In-game settings: same rows as the menu Settings screen.</summary>
        CanvasGroup BuildSettingsPanel(RectTransform layer)
        {
            var group = QcUi.Modal(layer, "SettingsPanel", new Color(0.1f, 0.08f, 0.06f, 0.6f));
            var card = QcUi.Anchor(group.transform as RectTransform, "Card",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-440, -720), new Vector2(440, 720));
            QcUi.PanelImage(card, "bg", Color.white);
            var scroll = QcUi.Stretch(card, "scroll");
            var scrollRect = scroll.gameObject.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            var content = QcUi.Anchor(scroll, "Content",
                new Vector2(0, 1), new Vector2(1, 1), Vector2.zero, Vector2.zero);
            scrollRect.content = content;
            scrollRect.viewport = scroll;
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
            _services.Motion.SetPanelVisible(panel, panel.transform as RectTransform, false, 0.12f);
            _openStack.Remove(panel);
            if (_modalContexts.TryGetValue(panel, out var leases))
            {
                foreach (var b in leases) b.Dispose();
                _modalContexts.Remove(panel);
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
            _statusText?.Set($"{_session.Level.id} · {_services.Localization.T("chapter." + _session.Level.chapter)}");
        }

        void RefreshCards()
        {
            for (var i = 0; i < _guestCards.Count && i < _session.Level.guests.Length; i++)
            {
                var g = _session.Level.guests[i];
                var placed = _session.State.Contains(g.id);
                var img = _guestCards[i].GetComponentInChildren<Image>();
                img.color = placed
                    ? new Color(0.72f, 0.84f, 0.70f)
                    : _session.SelectedGuestId == g.id ? QcUi.Amber : QcUi.Cream;
            }
            _undoButton.interactable = _session.CanUndo;
            _redoButton.interactable = _session.CanRedo;
            _removeButton.interactable = _session.SelectedGuestId != null
                && _session.State.Contains(_session.SelectedGuestId);
        }

        void RefreshChips(RuleReport report)
        {
            foreach (var kv in _chips)
            {
                var code = kv.Key.Substring("rule.".Length);
                var failing = report != null && report.Issues.Exists(i => i.Code == code
                    || (code == "friends" && i.Code == "friends-missing"));
                kv.Value.color = failing
                    ? new Color(0.93f, 0.62f, 0.40f)
                    : new Color(0.66f, 0.82f, 0.62f);
            }
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
            _services.Audio?.Play("sfx.chime");
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
