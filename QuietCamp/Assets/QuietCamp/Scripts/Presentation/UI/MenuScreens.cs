using System;
using System.Collections.Generic;
using Kruty1918.UIActions.API;
using QuietCamp.Infrastructure;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace QuietCamp.Presentation.UI
{
    /// <summary>
    /// Main-menu screen set built at runtime under CanvasRoot/SafeArea:
    /// MainMenu, LevelPath, Settings, Album, DemoComplete. All button actions
    /// dispatch through the shared UiAction ids so hotkeys and touches agree.
    /// </summary>
    public sealed class MenuScreens
    {
        readonly GameServices _services;
        readonly RectTransform _safeArea;
        readonly Dictionary<string, CanvasGroup> _screens = new Dictionary<string, CanvasGroup>();
        readonly Stack<string> _history = new Stack<string>();

        public string Current { get; private set; } = "Main";

        public MenuScreens(GameServices services, RectTransform safeArea)
        {
            _services = services;
            _safeArea = safeArea;
            BuildMain();
            BuildLevelPath();
            BuildSettings();
            BuildAlbum();
            BuildDemoComplete();
            Show("Main");
        }

        void Action(string id, object payload = null)
        {
            _services.Audio?.Play("ui.click");
            _services.Actions.Execute(new UiActionRequest(
                new UiActionId(id), UiActionSource.Button, "Menu", null, payload));
        }

        // ─── Main ────────────────────────────────────────────────────────────

        void BuildMain()
        {
            var root = Screen("Main");
            var rt = root.transform as RectTransform;

            // Header — two-line title + tagline over the clean sky band of the
            // illustrated background (top ~7%–28% of the canvas).
            var header = QcUi.Anchor(rt, "Header",
                new Vector2(0.06f, 1f), new Vector2(0.94f, 1f),
                new Vector2(0f, -560f), new Vector2(0f, -120f));
            var headerLayout = header.gameObject.AddComponent<VerticalLayoutGroup>();
            headerLayout.childAlignment = TextAnchor.UpperCenter;
            headerLayout.childForceExpandWidth = true;
            headerLayout.childForceExpandHeight = false;
            headerLayout.spacing = 0f;
            TitleLabel(header, "menu.title.line1");
            TitleLabel(header, "menu.title.line2");
            QcUi.Label(header, "menu.tagline", 42f,
                TextAlignmentOptions.Center, MenuArt.ForestText)
                .gameObject.AddComponent<LayoutElement>().minHeight = 90;

            // Settings lives in the top corner — one primary action and two
            // secondaries form the bottom column per the late design.
            var settingsBtn = QcUi.MenuButton(rt, "menu.settings",
                () => Action("qc.settings"), MenuArt.Cream, MenuArt.Forest, 34f);
            var srt = (RectTransform)settingsBtn.transform;
            srt.anchorMin = new Vector2(1, 1); srt.anchorMax = new Vector2(1, 1);
            srt.pivot = new Vector2(1, 1);
            srt.sizeDelta = new Vector2(300f, 96f);
            srt.anchoredPosition = new Vector2(-16f, -16f);

            // Actions — 84% wide column hugging the bottom safe area.
            var actions = QcUi.Anchor(rt, "Actions",
                new Vector2(0.08f, 0f), new Vector2(0.92f, 0f),
                new Vector2(0f, 56f), new Vector2(0f, 56f + 560f));
            var layout = actions.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 22;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.childControlHeight = true;
            layout.childAlignment = TextAnchor.LowerCenter;

            var hasSave = _services.Progression.CompletedCount > 0 || _services.Save.HasSave;
            var primary = QcUi.MenuButton(actions,
                hasSave ? "menu.continue" : "menu.start",
                () => Action("qc.continue"), MenuArt.Forest, MenuArt.Cream,
                58f, QcUi.IconPlay);
            var p = primary.gameObject.AddComponent<LayoutElement>();
            p.minHeight = 162; p.preferredHeight = 162;
            var secondary = QcUi.Anchor(actions, "Secondary",
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var se = secondary.gameObject.AddComponent<LayoutElement>();
            se.minHeight = 144; se.preferredHeight = 144; se.flexibleHeight = 0;
            var row = secondary.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.spacing = 20;
            row.childForceExpandWidth = true;
            // No forceExpandHeight — an expanding child would report the row
            // as flexible to the parent layout and inflate it.
            row.childForceExpandHeight = false;
            row.childControlHeight = true;
            foreach (var key in new[] { "menu.levels", "menu.album" })
            {
                var id = key == "menu.levels" ? "qc.levels" : "qc.album";
                var btn = QcUi.MenuButton(secondary, key, () => Action(id),
                    MenuArt.Cream, MenuArt.Forest, 48f);
                var le = btn.gameObject.AddComponent<LayoutElement>();
                le.minHeight = 144; le.preferredHeight = 144;
            }
        }

        static void TitleLabel(RectTransform parent, string key)
        {
            var label = QcUi.Label(parent, key, 118f,
                TextAlignmentOptions.Center, MenuArt.Forest);
            var tmp = label.GetComponent<TextMeshProUGUI>();
            tmp.fontStyle = FontStyles.Bold;
            tmp.characterSpacing = -1.2f;
            tmp.lineSpacing = -18f;
            label.gameObject.AddComponent<LayoutElement>().minHeight = 150;
        }



        // ─── LevelPath ───────────────────────────────────────────────────────

        void BuildLevelPath()
        {
            var root = Screen("Levels");
            QcUi.Image(root.transform as RectTransform, "bg", new Color(0f, 0f, 0f, 0.45f));
            var card = QcUi.Anchor(root.transform as RectTransform, "Card",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-460, -700), new Vector2(460, 700));
            QcUi.PanelImage(card, "bg", QcUi.Cream);
            var col = QcUi.Stretch(card, "col");
            var layout = col.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(36, 36, 30, 30);
            layout.spacing = 18;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            QcUi.Label(col, "menu.levels", QcUi.TextTitle, TextAlignmentOptions.Center, QcUi.Ink)
                .gameObject.AddComponent<LayoutElement>().minHeight = 80;

            var scroll = QcUi.Root(col, "scroll");
            scroll.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1;
            var sr = scroll.gameObject.AddComponent<ScrollRect>();
            sr.horizontal = false;
            scroll.gameObject.AddComponent<RectMask2D>();
            var grid = QcUi.Anchor(scroll, "grid",
                new Vector2(0, 1), new Vector2(1, 1), Vector2.zero, Vector2.zero);
            grid.pivot = new Vector2(0.5f, 1f); // CSF grows down from top
            sr.content = grid; sr.viewport = scroll;
            sr.verticalNormalizedPosition = 1f;
            var gridLayout = grid.gameObject.AddComponent<GridLayoutGroup>();
            gridLayout.cellSize = new Vector2(250f, 130f);
            gridLayout.spacing = new Vector2(16f, 16f);
            gridLayout.childAlignment = TextAnchor.UpperCenter;
            var fitter = grid.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            _levelGrid = grid;
            RebuildLevelButtons();
            AddBtn(col, "action.back", () => Back(), QcUi.Brown, 110);
        }

        RectTransform _levelGrid;
        readonly List<Button> _levelButtons = new List<Button>();

        public void RebuildLevelButtons()
        {
            if (_levelGrid == null) return;
            foreach (var b in _levelButtons) if (b != null) UnityEngine.Object.Destroy(b.gameObject);
            _levelButtons.Clear();
            var ids = LevelLoader.MvpLevelIds();
            var next = _services.Progression.ContinueTarget(ids) ?? ids[0];
            for (var i = 0; i < ids.Count; i++)
            {
                var id = ids[i];
                var unlocked = _services.Progression.IsUnlocked(id, ids);
                var completed = _services.Progression.IsCompleted(id);
                var current = !completed && unlocked && id == next;
                // Display name only — the technical id stays in the data layer.
                var status = completed ? "level.done" : current ? "level.current" : unlocked ? null : "level.locked";
                var img = QcUi.Sliced(_levelGrid, "level_" + i, QcUi.BtnSecondary,
                    completed ? new Color(0.80f, 0.87f, 0.76f)
                    : current ? new Color(1f, 0.86f, 0.60f)
                    : unlocked ? QcUi.Cream : QcUi.Disabled);
                img.raycastTarget = true;
                var btn = img.gameObject.AddComponent<Button>();
                btn.targetGraphic = img;
                var nameRect = QcUi.Anchor(img.rectTransform, "name",
                    new Vector2(0, 0.45f), new Vector2(1, 1), Vector2.zero, Vector2.zero);
                var name = QcUi.PlainText(nameRect,
                    LevelDisplay.Title(id, _services.Localization),
                    QcUi.TextBody, TextAlignmentOptions.Center,
                    unlocked ? QcUi.Ink : QcUi.Disabled);
                name.fontStyle = FontStyles.Bold;
                var sub = QcUi.Anchor(img.rectTransform, "sub",
                    new Vector2(0, 0), new Vector2(1, 0.45f), Vector2.zero, Vector2.zero);
                if (status != null)
                    QcUi.Label(sub, status, QcUi.TextSmall - 4f,
                        TextAlignmentOptions.Center,
                        completed ? QcUi.GreenDark : unlocked ? QcUi.Brown : QcUi.Disabled);
                var levelId = id;
                if (unlocked)
                {
                    btn.onClick.AddListener(() => Action("qc.play", levelId));
                }
                else
                {
                    // Locked levels stay tappable so the game can explain why.
                    btn.onClick.AddListener(() => _services.Notifications?.Show(
                        _services.Localization.T("level.locked.hint"),
                        Kruty1918.Notifications.API.GameplayNotificationKind.Info,
                        dedupKey: "locked." + levelId));
                }
                _levelButtons.Add(btn);
            }
        }

        // ─── Settings ────────────────────────────────────────────────────────

        void BuildSettings()
        {
            var root = Screen("Settings");
            QcUi.Image(root.transform as RectTransform, "bg", new Color(0f, 0f, 0f, 0.45f));
            var card = QcUi.Anchor(root.transform as RectTransform, "Card",
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
                TextAlignmentOptions.Center, QcUi.Ink)
                .gameObject.AddComponent<LayoutElement>().minHeight = 80;

            SettingsPanel.BuildRows(_services, content);

            AddBtn(content, "settings.reset", () =>
            {
                _services.Progression.Restore(null, null, 0);
                _services.Save.Progress = new Application.ProgressSaveData();
                _services.Save.Album = new Application.AlbumSaveData();
                _services.Save.Save();
                RebuildLevelButtons();
            }, QcUi.Danger, 110);
            AddBtn(content, "action.back", () => Back(), QcUi.Brown, 110);
        }

        // ─── Album ───────────────────────────────────────────────────────────

        RectTransform _albumList;

        void BuildAlbum()
        {
            var root = Screen("Album");
            QcUi.Image(root.transform as RectTransform, "bg", new Color(0f, 0f, 0f, 0.45f));
            var card = QcUi.Anchor(root.transform as RectTransform, "Card",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-460, -700), new Vector2(460, 700));
            QcUi.PanelImage(card, "bg", QcUi.Cream);
            var col = QcUi.Stretch(card, "col");
            var layout = col.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(36, 36, 30, 30);
            layout.spacing = 16;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            QcUi.Label(col, "menu.album", QcUi.TextTitle, TextAlignmentOptions.Center, QcUi.Ink)
                .gameObject.AddComponent<LayoutElement>().minHeight = 80;
            var scroll = QcUi.Root(col, "scroll");
            scroll.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1;
            var sr = scroll.gameObject.AddComponent<ScrollRect>();
            sr.horizontal = false;
            var content = QcUi.Anchor(scroll, "Content",
                new Vector2(0, 1), new Vector2(1, 1), Vector2.zero, Vector2.zero);
            content.pivot = new Vector2(0.5f, 1f);
            sr.content = content; sr.viewport = scroll;
            sr.verticalNormalizedPosition = 1f;
            var vlayout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            vlayout.childForceExpandWidth = true;
            vlayout.childForceExpandHeight = false;
            vlayout.spacing = 12;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit =
                ContentSizeFitter.FitMode.PreferredSize;
            _albumList = content;
            RebuildAlbum();
            AddBtn(col, "action.back", () => Back(), QcUi.Brown, 110);
        }

        public void RebuildAlbum()
        {
            if (_albumList == null) return;
            for (var i = _albumList.childCount - 1; i >= 0; i--)
                UnityEngine.Object.Destroy(_albumList.GetChild(i).gameObject);
            var entries = _services.Save.Album.entries;
            if (entries == null || entries.Length == 0)
            {
                var empty = QcUi.Root(_albumList, "empty");
                empty.sizeDelta = new Vector2(0, 160);
                QcUi.Label(empty, "album.empty", QcUi.TextSmall,
                    TextAlignmentOptions.Center, QcUi.Brown);
                return;
            }
            foreach (var e in entries)
            {
                var row = QcUi.Root(_albumList, "entry_" + e.levelId);
                row.sizeDelta = new Vector2(0, 118);
                QcUi.PanelImage(row, "bg", QcUi.Cream);
                var btn = row.gameObject.AddComponent<Button>();
                var levelId = e.levelId;
                btn.onClick.AddListener(() => Action("qc.play", levelId));
                var name = QcUi.Anchor(row, "name",
                    new Vector2(0, 0.5f), new Vector2(1, 1),
                    new Vector2(24, 0), new Vector2(-24, 0));
                var title = QcUi.PlainText(name,
                    LevelDisplay.Title(e.levelId, _services.Localization),
                    QcUi.TextBody, TextAlignmentOptions.MidlineLeft, QcUi.Ink);
                title.fontStyle = FontStyles.Bold;
                var sub = QcUi.Anchor(row, "sub",
                    new Vector2(0, 0), new Vector2(1, 0.5f),
                    new Vector2(24, 0), new Vector2(-24, 0));
                var status = LevelDisplay.IsTest(e.levelId)
                    ? _services.Localization.T("level.test")
                    : _services.Localization.T("level.done");
                QcUi.PlainText(sub, status, QcUi.TextSmall - 4f,
                    TextAlignmentOptions.MidlineLeft, QcUi.Brown);
            }
        }

        // ─── DemoComplete ────────────────────────────────────────────────────

        void BuildDemoComplete()
        {
            var root = Screen("DemoComplete");
            QcUi.Image(root.transform as RectTransform, "bg", new Color(0f, 0f, 0f, 0.5f));
            var card = QcUi.Anchor(root.transform as RectTransform, "Card",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-400, -320), new Vector2(400, 320));
            QcUi.PanelImage(card, "bg", QcUi.Cream);
            var col = QcUi.Stretch(card, "col");
            var layout = col.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(36, 36, 36, 36);
            layout.spacing = 22;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            QcUi.Label(col, "demo.complete", QcUi.TextTitle,
                TextAlignmentOptions.Center, QcUi.Ink)
                .gameObject.AddComponent<LayoutElement>().flexibleHeight = 1;
            AddBtn(col, "menu.album", () => Action("qc.album"), QcUi.Green, 110);
            AddBtn(col, "action.back", () => Back(), QcUi.Brown, 110);
        }

        // ─── Shared builders ─────────────────────────────────────────────────

        CanvasGroup Screen(string name)
        {
            var rt = QcUi.Stretch(_safeArea, "Screen_" + name);
            var group = rt.gameObject.AddComponent<CanvasGroup>();
            _screens[name] = group;
            rt.gameObject.SetActive(false);
            return group;
        }

        public void Show(string name)
        {
            foreach (var kv in _screens) kv.Value.gameObject.SetActive(false);
            if (_screens.TryGetValue(name, out var g))
            {
                g.gameObject.SetActive(true);
                g.alpha = 1f;
                g.interactable = true;
                g.blocksRaycasts = true;
                // Gentle fade-in between screens (UiMotion honors reduced motion).
                _services.Motion?.SetPanelVisible(g, g.transform as RectTransform, true, 0.16f);
            }
            if (Current != name && Current != null) _history.Push(Current);
            Current = name;
            if (name == "Album") RebuildAlbum();
            if (name == "Levels") RebuildLevelButtons();
        }

        public void Back()
        {
            _services.Audio?.Play("ui.back");
            var target = _history.Count > 0 ? _history.Pop() : "Main";
            Current = null; // avoid re-push
            foreach (var kv in _screens) kv.Value.gameObject.SetActive(false);
            if (_screens.TryGetValue(target, out var g))
            {
                g.gameObject.SetActive(true);
                g.alpha = 1f;
                g.interactable = true;
                g.blocksRaycasts = true;
                _services.Motion?.SetPanelVisible(g, g.transform as RectTransform, true, 0.16f);
            }
            Current = target;
        }

        public void ShowDemoComplete() => Show("DemoComplete");

        void AddBtn(RectTransform parent, string key, Action onClick, Color color, float height)
        {
            var btn = QcUi.Button(parent, key, onClick, color);
            btn.gameObject.AddComponent<LayoutElement>().minHeight = height;
        }

    }
}
