using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using QuietCamp.Domain;
using QuietCamp.Infrastructure;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
#if QUIETCAMP_ODIN
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
#endif

namespace QuietCamp.Editor.LevelAuthoring
{
    public sealed partial class LevelAuthoringWindow : EditorWindow
    {
        enum FieldTool { None, Stone, Tree, Fire, Erase, Entry, Exit, Path, Tent }
        [SerializeField] LevelAuthoringState _state;
        [SerializeField] string _path;
        [SerializeField] int _tab, _category, _guest, _rotation;
        [SerializeField] int _generationIndex = 1;
        [SerializeField] int _entryType;
        [SerializeField] bool _dirty;
        LevelAuthoringWorkspace _workspace;
        [NonSerialized] LevelAuthoringDocument _displayDocument;
        [NonSerialized] string _displayJson;
        FieldTool _fieldTool;
        Vector2 _scroll;
        string _message = "";
        string _asset = "ua_orchard_tree";
        float _radius = 1, _density = 65, _spacing = .2f, _variation = .15f;
        int _friendA, _friendB = 1;
        CampSolver _solver;
        LevelData _solveLevel;
        double _hierarchyChangedAt = -1;
        bool _refreshing;
        int _sceneryCount;
        Cell? _lastPaint;
        string _search = "", _levelSearch = "";
        bool _favoritesOnly, _advanced, _tips = true;
        string _galleryQuery, _paletteQuery;
        AuthoringCampaignCard[] _gallery;
        AuthoringPaletteEntry[] _filteredPalette;
        readonly Dictionary<GameObject, Texture> _thumbnails = new Dictionary<GameObject, Texture>();
        [NonSerialized] RuleReport _overlay;
        [NonSerialized] string _overlayJson;
        double _applyAt = -1;
        [NonSerialized] LevelAuthoringDocument _pendingDocument;
        string _pendingEditName;
        readonly List<Transform> _movedTransforms = new List<Transform>();
        bool _structureDirty;
        static readonly string[] Pages = { "Рівні", "Поле", "Об’єкти", "Річка та земля", "Гості", "Перевірка" };
        static readonly string[] Categories = { "Дерева", "Кущі", "Трава", "Квіти", "Каміння", "Паркани", "Містки", "Будівлі", "Декор", "Пропси", "Великі об’єкти" };
        static readonly Unity.Profiling.ProfilerMarker PanelTiming = new Unity.Profiling.ProfilerMarker("QuietCamp.Authoring.Panel"),
            SceneTiming = new Unity.Profiling.ProfilerMarker("QuietCamp.Authoring.SceneView"),
            ThumbnailTiming = new Unity.Profiling.ProfilerMarker("QuietCamp.Authoring.Thumbnail"),
            RulesTiming = new Unity.Profiling.ProfilerMarker("QuietCamp.Authoring.Rules");
        public LevelAuthoringWorkspace Workspace => _workspace;
        public LevelAuthoringDocument Document
        {
            get
            {
                if (_pendingDocument != null) return _pendingDocument;
                if(_state==null)return null;
                // IMGUI and Scene View ask repeatedly within each frame. Parsing
                // a large scenery JSON every time makes a 10k-object editor stall.
                if(_displayDocument==null||!ReferenceEquals(_displayJson,_state.json))
                {_displayJson=_state.json;_displayDocument=_state.Read();}
                return _displayDocument;
            }
        }
        public bool IsSearching => _solver != null;

        [MenuItem("Quiet Camp/Майстерня рівнів")]
        public static LevelAuthoringWindow OpenWindow()
        {
            var window = GetWindow<LevelAuthoringWindow>(); window.titleContent = new GUIContent("Майстерня QuietCamp"); window.minSize = new Vector2(760, 540); window.Show(); return window;
        }
#if QUIETCAMP_ODIN
        OdinMenuTree MenuTree;
        PropertyTree _pageInspector;
        WorkshopPage _inspectedPage;
        OdinMenuTree BuildMenuTree()
        {
            var tree = new OdinMenuTree(false);
            for (int i = 0; i < Pages.Length; i++) tree.Add(Pages[i], new WorkshopPage(this, i));
            return tree;
        }
        sealed class WorkshopPage
        {
            readonly LevelAuthoringWindow _window; readonly int _index;
            public WorkshopPage(LevelAuthoringWindow window, int index) { _window = window; _index = index; }
            [OnInspectorGUI] void Draw() { _window._tab = _index; _window.DrawWorkshop(); }
        }
#endif
        void OnEnable() => EnableWorkshop();
        void OnGUI()
        {
            using var timing = PanelTiming.Auto();
#if QUIETCAMP_ODIN
            if (MenuTree == null) { MenuTree = BuildMenuTree(); MenuTree.MenuItems[Mathf.Clamp(_tab, 0, MenuTree.MenuItems.Count - 1)].Select(); }
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(165))) MenuTree.DrawMenuTree();
                using (new EditorGUILayout.VerticalScope(GUILayout.ExpandWidth(true)))
                {
                    var page = MenuTree.Selection.FirstOrDefault()?.Value as WorkshopPage;
                    if (page != _inspectedPage) { _pageInspector?.Dispose(); _inspectedPage = page; _pageInspector = page == null ? null : PropertyTree.Create(page); }
                    _pageInspector?.Draw(false);
                }
            }
#else
            _tab = GUILayout.Toolbar(_tab, Pages); DrawWorkshop();
#endif
        }
        void EnableWorkshop()
        {
            // Unity hot reload also serializes private Serializable fields. A JSON
            // cache must be rebuilt rather than restored as a partial Unity DTO.
            _displayDocument = null; _displayJson = null; _overlay = null; _overlayJson = null;
            saveChangesMessage = "Зберегти зміни рівня перед закриттям майстерні?";
            SceneView.duringSceneGui += DuringScene; Undo.undoRedoPerformed += UndoChanged;
            EditorApplication.update += Tick; EditorApplication.hierarchyChanged += HierarchyChanged;
            EditorSceneManager.sceneClosing += WorkspaceClosing;
            if (_state != null && !EditorApplication.isPlayingOrWillChangePlaymode) EditorApplication.delayCall += Recover;
        }
        void OnDisable()
        {
#if QUIETCAMP_ODIN
            _pageInspector?.Dispose(); _pageInspector = null; _inspectedPage = null;
#endif
            SceneView.duringSceneGui -= DuringScene; Undo.undoRedoPerformed -= UndoChanged;
            EditorApplication.update -= Tick; EditorApplication.hierarchyChanged -= HierarchyChanged;
            EditorSceneManager.sceneClosing -= WorkspaceClosing;
            PlacementToolIntegration.Adapter?.Stop();
            if (_workspace != null && !EditorApplication.isPlayingOrWillChangePlaymode)
            {
                try { if (_workspace.Root != null) _state.Write(CaptureDraft(false)); }
                finally { _workspace.Dispose(); _workspace = null; }
            }
        }
        void Recover()
        {
            _displayDocument = null; _displayJson = null; _overlay = null;
            if (SessionState.GetBool("QuietCamp.Authoring.Verifying", false)) return;
            if (this == null || _state == null || _workspace?.Root != null) return;
            _workspace?.Dispose(); _workspace = null;
            LevelAuthoringWorkspace.CloseOrphanedScenes();
            Try(() => { _workspace = new LevelAuthoringWorkspace(_state.Read()); _message = ""; });
        }
        void WorkspaceClosing(UnityEngine.SceneManagement.Scene scene,bool removing)
        {
            if (_refreshing || _workspace?.Root == null || _workspace.Scene != scene) return;
            CancelSolver(); PlacementToolIntegration.Adapter?.Stop();
            _state.Write(CaptureDraft());
        }
        public void PreparePlay()
        {
            CancelSolver(); PlacementToolIntegration.Adapter?.Stop();
            if (_workspace == null) return;
            if (_workspace.Root != null) _state.Write(CaptureDraft(false));
            _workspace.Dispose(); _workspace = null;
        }
        public void RestoreAfterPlay() => Recover();
        internal sealed class VerificationSession
        {
            internal LevelAuthoringDocument document;
            internal string path;
            internal bool dirty;
        }
        internal VerificationSession PrepareVerification()
        {
            if (_state == null) return null;
            var session = new VerificationSession { document = _workspace?.Root != null ? CaptureDraft() : _state.Read(), path = _path, dirty = _dirty };
            PreparePlay(); return session;
        }
        internal void RestoreVerification(VerificationSession session)
        { SetDocument(session.document); _path = session.path; _dirty = session.dirty; }
        bool ConfirmReplace()
        {
            if (!_dirty) return true;
            int action = EditorUtility.DisplayDialogComplex("Незбережений рівень", "Зберегти поточну чернетку перед відкриттям іншої?", "Зберегти", "Скасувати", "Не зберігати");
            if (action == 1) return false;
            if (action == 0) { Save(); return !_dirty; } return true;
        }
        public void NewDocument(bool generated)
        {
            if (!ConfirmReplace()) return;
            SetDocument(LevelAuthoringDocument.Create(generated, generationIndex:_generationIndex)); _path = null; _dirty = true;
            _message = generated ? "Згенерований пазл і табір уже можна редагувати вручну." : "Порожнє поле. Додайте ігрові об’єкти та гостей, потім намалюйте оточення.";
        }
        public void SetDocument(LevelAuthoringDocument document)
        {
            _message = "";
            _pendingDocument = null; _applyAt = -1;
            CancelSolver(); PlacementToolIntegration.Adapter?.Stop(); _fieldTool = FieldTool.None; _placingProp = false;
            _refreshing = true;
            try
            {
                _workspace?.Dispose();
                if (_state == null) { _state = CreateInstance<LevelAuthoringState>(); _state.hideFlags = HideFlags.HideAndDontSave; }
                _state.Write(document); _workspace = new LevelAuthoringWorkspace(document);
                _state.Write(CaptureDraft(false));
                _sceneryCount = _workspace.SceneryCount;
            }
            finally { _refreshing = false; }
            _tab = 1;
#if QUIETCAMP_ODIN
            if (MenuTree != null && MenuTree.MenuItems.Count > 1) MenuTree.MenuItems[1].Select();
#endif
            Repaint();
        }
        public void ShowPage(int page)
        {
            _tab = Mathf.Clamp(page, 0, Pages.Length - 1); _scroll = Vector2.zero;
#if QUIETCAMP_ODIN
            if (MenuTree != null) MenuTree.MenuItems[_tab].Select();
#endif
            Repaint();
        }
        public void ShowPaletteCategory(int category)
        {
            _category = Mathf.Clamp(category, 0, Categories.Length - 1); _search = ""; ShowPage(2);
        }
        void Edit(string description, Action<LevelAuthoringDocument> action)
        {
            CancelSolver(); var document = Document.EditingCopy();
            action(document); document.ReconcileObjects();
            _pendingDocument = document; _pendingEditName = description; _overlay = null;
            _applyAt = EditorApplication.timeSinceStartup + .3;
            if (Event.current == null || Event.current.type == EventType.MouseUp) FlushEdits();
            _dirty = true; Repaint(); SceneView.RepaintAll();
        }
        void FlushEdits()
        {
            if (_pendingDocument == null) return;
            var document = _pendingDocument;
            try { RiverTerrainIntegration.Validate?.Invoke(document); }
            catch (Exception error)
            {
                _pendingDocument = null; _applyAt = -1; _displayDocument = null; _message = error.Message;
                Repaint(); SceneView.RepaintAll(); return;
            }
            Undo.RecordObject(_state, _pendingEditName); _state.Write(document);
            _pendingDocument = null; _applyAt = -1; _displayJson = _state.json; _displayDocument = document;
            _refreshing = true; try { _workspace?.ApplyDocument(document); } finally { _refreshing = false; }
        }
        LevelAuthoringDocument CaptureDraft(bool bakeLandscape = true) { FlushEdits(); return _workspace.Capture(bakeLandscape); }
        public void Save()
        {
            FlushEdits();
            if (_state == null) return;
            string path = _path;
            if (string.IsNullOrEmpty(path)) path = EditorUtility.SaveFilePanel("Зберегти чернетку", Path.GetFullPath("Assets/QuietCamp/Authoring/Levels"), Document.level.id + ".draft", "json");
            if (string.IsNullOrEmpty(path)) return;
            var document = _workspace?.Root != null ? CaptureDraft() : Document;
            document.Save(path); _state.Write(document); _path = path; _dirty = false; _message = "Чернетку збережено.";
        }
        public override void SaveChanges() { Try(Save); if (!_dirty) base.SaveChanges(); }
        public override void DiscardChanges() { _dirty = false; base.DiscardChanges(); }
        void OpenDraft()
        {
            if (!ConfirmReplace()) return;
            string path = EditorUtility.OpenFilePanel("Відкрити чернетку", Path.GetFullPath("Assets/QuietCamp/Authoring/Levels"), "json");
            if (string.IsNullOrEmpty(path)) return;
            SetDocument(LevelAuthoringDocument.Open(path)); _path = path; _dirty = false;
        }
        public void BeginSolve()
        {
            FlushEdits();
            var snapshot = CaptureDraft(); var errors = snapshot.Validate(false);
            if (errors.Count > 0) { _message = string.Join("\n", errors); return; }
            _solveLevel = LevelAuthoringDocument.Copy(snapshot.level); _solveLevel.witness = null;
            _solver = new CampSolver(_solveLevel, Array.Empty<Placement>(), 20);
            _message = "Шукаю рішення… Можна скасувати пошук.";
        }
        public void CancelSolver() { _solver = null; _solveLevel = null; }
        void Validate()
        {
            var errors = CaptureDraft().Validate(true);
            _message = errors.Count == 0 ? "✓ Пазл і композиція коректні. Рішення виконує всі умови." : string.Join("\n", errors);
        }
        void Export()
        {
            string directory = EditorUtility.OpenFolderPanel("Експорт перевіреного рівня", Path.GetFullPath("Assets/QuietCamp/Authoring"), "");
            if (string.IsNullOrEmpty(directory)) return;
            CaptureDraft().Export(directory); _message = "Експорт готовий. Кампанія не перепубліковувалася.";
        }
        void Try(Action action)
        {
            try { action(); } catch (Exception e) { _message = e.Message; Debug.LogWarning("[LevelAuthoring] " + e); }
            Repaint();
        }
        void DrawWorkshop()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
                {
                if (GUILayout.Button("Новий ▾", EditorStyles.toolbarButton))
                {
                    var menu = new GenericMenu(); menu.AddItem(new GUIContent("Порожнє поле"), false, () => Try(() => NewDocument(false)));
                    menu.AddItem(new GUIContent("Основа зі згенерованого табору"), false, () => Try(() => NewDocument(true))); menu.ShowAsContext();
                }
                if (GUILayout.Button("Відкрити", EditorStyles.toolbarButton)) Try(OpenDraft);
                }
                using (new EditorGUI.DisabledScope(_workspace == null || EditorApplication.isPlayingOrWillChangePlaymode))
                {
                    if (GUILayout.Button("Зберегти" + (_dirty ? " *" : ""), EditorStyles.toolbarButton)) Try(Save);
                    if (GUILayout.Button("Перевірити", EditorStyles.toolbarButton)) Try(Validate);
                    if (GUILayout.Button("Грати", EditorStyles.toolbarButton)) Try(() => LevelAuthoringPlayDriver.Start(CaptureDraft()));
                    if (GUILayout.Button("Експорт", EditorStyles.toolbarButton)) Try(Export);
                }
                if (GUILayout.Button(new GUIContent("Допомога", "Покрокова інструкція з картинками"), EditorStyles.toolbarButton)) OpenHelp();
            }
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorGUILayout.HelpBox("Тестовий запуск із тимчасовим прогресом. Зупиніть Play Mode, щоб продовжити редагування.", MessageType.Info);
                if (GUILayout.Button("Зупинити тест")) EditorApplication.isPlaying = false; return;
            }
            if (_tab == 0) { DrawLevels(); return; }
            if (_workspace == null)
            {
                EditorGUILayout.Space(16); GUILayout.Label("Складіть свій табір", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox("Оберіть початок. Поле й умови гри редагуються тут, оточення — готовими інструментами PWB у Scene View.", MessageType.Info);
                _generationIndex = EditorGUILayout.IntSlider("Варіант генерації", _generationIndex, 1, 80);
                if (GUILayout.Button("Порожнє поле", GUILayout.Height(36))) Try(() => NewDocument(false));
                if (GUILayout.Button("Взяти згенерований табір", GUILayout.Height(36))) Try(() => NewDocument(true));
                if (!string.IsNullOrEmpty(_message)) EditorGUILayout.HelpBox(_message, MessageType.Warning); return;
            }
            if (_workspace.Root == null)
            {
                EditorGUILayout.HelpBox("Робочу сцену закрито. Чернетка залишилася в майстерні.",MessageType.Info);
                if(GUILayout.Button("Відновити відкритий рівень"))Recover();return;
            }
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            using (new EditorGUI.DisabledScope(_solver != null))
            {
                if (_tips)
                {
                    EditorGUILayout.HelpBox("Спочатку оберіть інструмент тут, потім працюйте у Scene View. Alt — огляд, Ctrl+Z — скасування. «Допомога» відкриває приклади з картинками.", MessageType.Info);
                    if (GUILayout.Button("Зрозуміло — сховати вступні підказки")) _tips = false;
                }
                if (_tab == 1) DrawField(); else if (_tab == 2) DrawScenery(); else if (_tab == 3) DrawLandscape(); else if (_tab == 4) DrawGuests(); else DrawChecks();
            }
            if (_solver != null && GUILayout.Button("Скасувати пошук")) { CancelSolver(); _message = "Пошук скасовано."; }
            if (!string.IsNullOrEmpty(_message)) EditorGUILayout.HelpBox(_message, MessageType.Info);
            EditorGUILayout.EndScrollView();
        }
        void DrawField()
        {
            var document = Document; var level = document.level;
            EditorGUI.BeginChangeCheck(); string title = EditorGUILayout.TextField("Назва", document.title);
            int width = EditorGUILayout.IntSlider("Ширина", level.width, 4, 8), height = EditorGUILayout.IntSlider("Висота", level.height, 4, 8);
            if (EditorGUI.EndChangeCheck()) Try(() => Edit("Змінити поле", d => { d.title = title; if (width != d.level.width || height != d.level.height) d.Resize(width, height); }));
            EditorGUILayout.HelpBox("Оберіть інструмент і клацайте клітини в Scene View. Намет займає 2×2. Alt залишає керування камерою.", MessageType.None);
            int tool = GUILayout.SelectionGrid((int)_fieldTool, new[] { "Вибір", "Камінь", "Дерево", "Вогнище", "Гумка", "Вхід", "Вихід", "Прохід", "Намет" }, 3, GUILayout.Height(84));
            if (tool != (int)_fieldTool) { PlacementToolIntegration.Adapter?.Stop(); _fieldTool = (FieldTool)tool; SceneView.RepaintAll(); }
            if (_fieldTool == FieldTool.Entry)
                _entryType = EditorGUILayout.Popup("Тип входу", _entryType, new[] { "Основний", "Додатковий" });
            if (_fieldTool == FieldTool.Tent)
            {
                _guest = EditorGUILayout.Popup("Гість", Mathf.Clamp(_guest, 0, level.guests.Length - 1), level.guests.Select((g, i) => "Гість " + (i + 1)).ToArray());
                _rotation = EditorGUILayout.Popup("Двері", _rotation, new[] { "Північ", "Схід", "Південь", "Захід" });
            }
            foreach (var canopy in level.canopies)
            {
                EditorGUI.BeginChangeCheck(); float radius = EditorGUILayout.Slider("Крона (" + canopy.x + ", " + canopy.z + ")", canopy.radiusX, .5f, 4);
                if (EditorGUI.EndChangeCheck()) Edit("Змінити тінь", d => { var c = d.level.canopies.First(x => x.x == canopy.x && x.z == canopy.z); c.radiusX = c.radiusZ = radius; });
            }
            if (GUILayout.Button("Показати поле й оточення")) _workspace.Frame();
            EditorGUILayout.Space();
            _generationIndex = EditorGUILayout.IntSlider("Варіант нового табору", _generationIndex, 1, 80);
            EditorGUILayout.LabelField("Застосовується лише до наступного «Новий → Згенерований табір».",EditorStyles.wordWrappedMiniLabel);
        }
        static void OpenHelp()
        {
            string path = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "../../Design/LevelAuthoring/index.html"));
            if (!File.Exists(path)) { EditorUtility.DisplayDialog("Довідка", "HTML-довідка ще не встановлена: " + path, "Гаразд"); return; }
            UnityEngine.Application.OpenURL(new Uri(path).AbsoluteUri);
        }
        void DrawLevels()
        {
            GUILayout.Label("З чого почнемо?", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Порожнє поле", GUILayout.Height(38))) Try(() => NewDocument(false));
                if (GUILayout.Button("Згенерувати основу", GUILayout.Height(38))) Try(() => NewDocument(true));
                if (GUILayout.Button("Відкрити чернетку", GUILayout.Height(38))) Try(OpenDraft);
            }
            _generationIndex = EditorGUILayout.IntSlider(new GUIContent("Варіант основи", "Застосовується лише при створенні нової заготовки"), _generationIndex, 1, 80);
            EditorGUILayout.Space(); GUILayout.Label("Наявні рівні кампанії", EditorStyles.boldLabel);
            _levelSearch = EditorGUILayout.TextField(new GUIContent("Знайти рівень", "Введіть номер або ID"), _levelSearch);
            if (_gallery == null || _galleryQuery != _levelSearch)
            {
                _galleryQuery = _levelSearch;
                _gallery = LevelAuthoringCampaign.Cards.Where(c => string.IsNullOrEmpty(_levelSearch) || c.id.IndexOf(_levelSearch, StringComparison.OrdinalIgnoreCase) >= 0 || c.number.ToString().Contains(_levelSearch)).ToArray();
            }
            EditorGUILayout.LabelField(_gallery.Length + " рівнів · відкриття створює вашу чернетку", EditorStyles.wordWrappedMiniLabel);
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            const float height = 68;
            int first = Mathf.Max(0, (int)(_scroll.y / height) - 1), last = Mathf.Min(_gallery.Length, first + Mathf.CeilToInt(position.height / height) + 2);
            GUILayout.Space(first * height);
            for (int i = first; i < last; i++)
            {
                var card = _gallery[i];
                if (GUILayout.Button("Рівень " + card.number + "    ·    " + card.width + " × " + card.height + "    ·    гостей: " + card.guests + "\n" + card.id,
                    GUILayout.Height(height - 4))) Try(() => { if (!ConfirmReplace()) return; SetDocument(LevelAuthoringCampaign.Open(card.id)); _path = null; _dirty = true; });
            }
            GUILayout.Space((_gallery.Length - last) * height); EditorGUILayout.EndScrollView();
            if (!string.IsNullOrEmpty(_message)) EditorGUILayout.HelpBox(_message, MessageType.Info);
        }
        void DrawChecks()
        {
            GUILayout.Label("Перед грою", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Перевірте рішення, відкрийте тестову гру, потім збережіть чернетку або застосуйте її до початкового рівня.", MessageType.Info);
            if (GUILayout.Button("Перевірити поле, гостей і оточення", GUILayout.Height(38))) Try(Validate);
            if (GUILayout.Button("Знайти рішення", GUILayout.Height(38))) Try(BeginSolve);
            if (GUILayout.Button("Зіграти з тимчасовим прогресом", GUILayout.Height(38))) Try(() => LevelAuthoringPlayDriver.Start(CaptureDraft()));
            EditorGUILayout.Space();
            if (!string.IsNullOrEmpty(Document.sourceLevelId))
            {
                EditorGUILayout.LabelField("Початковий рівень", Document.sourceLevelId);
                if (GUILayout.Button("Застосувати до рівня кампанії", GUILayout.Height(38))) Try(() =>
                {
                    var snapshot = CaptureDraft(); var errors = snapshot.Validate(true);
                    if (errors.Count > 0) throw new InvalidDataException(string.Join("\n", errors));
                    if (!EditorUtility.DisplayDialog("Застосувати перевірені зміни", "Замінити " + snapshot.sourceLevelId + "?\nПоле: " + snapshot.level.width + " × " + snapshot.level.height + "; гостей: " + snapshot.level.guests.Length + ".\nПопередня ревізія зберігається для відновлення старих ігор. Інші рівні залишаються без змін.", "Застосувати", "Залишити чернеткою")) return;
                    LevelAuthoringCampaign.Apply(snapshot); _state.Write(snapshot); _gallery = null; _dirty = true;
                    _message = "Рівень кампанії оновлено. Збережіть чернетку для подальшого редагування.";
                });
            }
            else EditorGUILayout.HelpBox("Новий табір можна експортувати окремо. Для заміни кампанії відкрийте її рівень у розділі «Рівні».", MessageType.None);
            if (GUILayout.Button("Експортувати готовий набір")) Try(Export);
            if (GUILayout.Button("Показувати вступні підказки")) _tips = true;
        }
        void DrawScenery()
        {
            if (PlacementToolIntegration.Adapter == null) { EditorGUILayout.HelpBox("PWB ще не підключений. Імпортуйте наданий пакет і дочекайтеся компіляції.", MessageType.Warning); return; }
            _category = GUILayout.SelectionGrid(_category, Categories, 4);
            DrawPropFilters();
            _search = EditorGUILayout.TextField("Пошук об’єктів", _search);
            _favoritesOnly = EditorGUILayout.Toggle("Лише обране ★", _favoritesOnly);
            string query = _category + "/" + _search + "/" + _favoritesOnly + "/" + _propPack + "/" + _showPropParts;
            if (_filteredPalette == null || _paletteQuery != query)
            {
                _paletteQuery = query;
                _filteredPalette = LevelAuthoringPalette.Entries.Where(e => (string.IsNullOrWhiteSpace(_search) ? e.category == Categories[_category] : true)
                    && (string.IsNullOrEmpty(_search) || e.label.IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0 || e.id.IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0)
                    && (!_favoritesOnly || EditorPrefs.GetBool("QuietCamp.Authoring.Favorite." + e.id))
                    && PropMatchesFilter(e)).ToArray();
            }
            var entries = _filteredPalette;
            for (int row = 0; row < entries.Length; row += 3)
            {
                var rowRect = EditorGUILayout.GetControlRect(false, 72);
                if (rowRect.yMax < _scroll.y || rowRect.y > _scroll.y + position.height) continue;
                float cardWidth = (rowRect.width - 8) / 3;
                for (int i = row; i < Math.Min(row + 3, entries.Length); i++)
                {
                    var entry = entries[i]; var key = entry.prefab;
                    if (!_thumbnails.TryGetValue(key, out var image) || image == null)
                    {
                        using var thumbnailTiming = ThumbnailTiming.Auto();
                        image = AssetPreview.GetAssetPreview(entry.prefab);
                        if (image != null) _thumbnails[key] = image;
                        else image = AssetPreview.GetMiniThumbnail(entry.prefab);
                    }
                    var content = new GUIContent(entry.label + "\nдекор", image, entry.id);
                    var card = new Rect(rowRect.x + (i - row) * (cardWidth + 4), rowRect.y, cardWidth, 72);
                    if (GUI.Button(card, content, _asset == entry.id ? EditorStyles.helpBox : GUI.skin.button)) SelectPaletteEntry(entry);
                }
            }
            var selectedEntry = LevelAuthoringPalette.Get(_asset);
            bool singleOnly = selectedEntry.prop?.SingleOnly == true;
            if (!singleOnly) _radius = EditorGUILayout.Slider("Розмір пензля", _radius, .15f, 4);
            DrawPropSettings(selectedEntry);
            if (!string.IsNullOrEmpty(_asset))
            {
                bool favorite = EditorPrefs.GetBool("QuietCamp.Authoring.Favorite." + _asset);
                if (GUILayout.Button(favorite ? "★ Прибрати вибране з обраного" : "☆ Додати вибране до обраного"))
                { EditorPrefs.SetBool("QuietCamp.Authoring.Favorite." + _asset, !favorite); _filteredPalette = null; }
            }
            var custom = (GameObject)EditorGUILayout.ObjectField(new GUIContent("Додати свій prefab", "Перетягніть модель із Project; вона стане декоративним об’єктом"), null, typeof(GameObject), false);
            if (custom != null) Try(() => { LevelAuthoringPalette.RegisterPrefab(custom); _filteredPalette = null; _message = "Власний prefab додано до декору."; });
            if (!singleOnly)
            {
            _density = EditorGUILayout.Slider("Густота", _density, 1, 100);
            _spacing = EditorGUILayout.Slider("Мін. відстань", _spacing, .03f, 2);
            _variation = EditorGUILayout.Slider("Варіація масштабу", _variation, 0, .5f);
            using (new EditorGUILayout.HorizontalScope()) foreach (var pair in new[] { ("Пензель", PlacementTool.Paint), ("Один об’єкт", PlacementTool.Place), ("Лінія", PlacementTool.Line), ("Гумка", PlacementTool.Erase) })
                if (GUILayout.Button(pair.Item1)) Try(() => { _placingProp = false; _fieldTool = FieldTool.None; if (pair.Item2 == PlacementTool.Place && selectedEntry.prop != null) BeginPropPlacement(); else PlacementToolIntegration.Adapter.Activate(pair.Item2, selectedEntry.prefab, _workspace.Scenery, _workspace.Surface, _radius, _density, _spacing, _variation); });
            }
            if (GUILayout.Button("Завершити інструмент / перемістити об’єкти")) { _placingProp = false; PlacementToolIntegration.Adapter.Stop(); }
            if (GUILayout.Button("Детальні налаштування PWB")) PlacementToolIntegration.Adapter.OpenAdvanced();
            EditorGUILayout.HelpBox("Це декор: його колайдери не є перешкодами пазла. Ігрові дерево, камінь і вогнище додаються у вкладці «Поле». Великий декор залишайте поза сіткою.", MessageType.None);
        }
        void DrawLightingAndLegacyRiver()
        {
            var art = Document.art;
            EditorGUI.BeginChangeCheck(); float intensity = EditorGUILayout.Slider("Світло", art.sunIntensity, .4f, 2);
            float sunYaw = EditorGUILayout.Slider("Напрям сонця", art.sunEuler[1], 0, 360);
            float size = EditorGUILayout.Slider("Масштаб камери", art.orthographicSize, 4, 18);
            float cameraPitch = EditorGUILayout.Slider("Нахил камери", art.cameraEuler[0], 25, 80);
            float cameraYaw = EditorGUILayout.Slider("Поворот камери", art.cameraEuler[1], 0, 360);
            var target = EditorGUILayout.Vector3Field("Центр кадру", CampArtDefinition.Vector(art.cameraTarget));
            float blur = EditorGUILayout.Slider("Розмиття країв", art.blurPixels, 0, 12);
            if (EditorGUI.EndChangeCheck()) Edit("Світло й камера", d => { d.art.sunIntensity = intensity; d.art.sunEuler[1] = sunYaw; d.art.orthographicSize = size; d.art.cameraEuler[0] = cameraPitch; d.art.cameraEuler[1] = cameraYaw; d.art.cameraTarget = new[]{target.x,target.y,target.z}; d.art.blurPixels=blur; });
            if (GUILayout.Button("Подивитися з камери рівня")) _workspace.Frame();
            if (Document.terrain != null) return;
            EditorGUILayout.Space(); GUILayout.Label("Шаблон: річкова галявина", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            float riverWidth = EditorGUILayout.Slider("Ширина річки", art.streamWidth, .8f, 5);
            float riverCentre = EditorGUILayout.Slider("Положення річки", art.streamCentre, 4, 12);
            float bend = EditorGUILayout.Slider("Вигин русла", art.streamBend, 0, 1);
            float relief = EditorGUILayout.Slider("Рельєф берега", art.meadowRelief, 0, .5f);
            float depth = EditorGUILayout.Slider("Глибина берегів", art.waterDepth, .2f, 1.4f);
            if (EditorGUI.EndChangeCheck()) Edit("Змінити річку й берег", d => { d.art.streamWidth = riverWidth; d.art.streamCentre = riverCentre; d.art.streamBend = bend; d.art.meadowRelief = relief; d.art.waterDepth = depth; });
        }
        void DrawGuests()
        {
            var level = Document.level;
            for (int i = 0; i < level.guests.Length; i++)
            {
                int index = i; var guest = level.guests[i];
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    GUILayout.Label("Гість " + (i + 1), EditorStyles.boldLabel);
                    EditorGUI.BeginChangeCheck(); bool shade = EditorGUILayout.Toggle("Потрібна тінь", guest.shade), quiet = EditorGUILayout.Toggle("Потрібна тиша", guest.quiet);
                    var tents = AssetCatalog.Load().Entries.Where(e => e.assetId.StartsWith("tent", StringComparison.Ordinal)).ToArray();
                    int old = Array.FindIndex(tents, e => e.assetId == guest.assetId);
                    int selected = EditorGUILayout.Popup("Намет", Mathf.Max(0, old), tents.Select(e => e.assetId == "tent_smallOpen" ? "Малий відкритий намет" : e.assetId == "tent_detailedOpen" ? "Туристичний відкритий намет" : e.prefab.name).ToArray());
                    if (EditorGUI.EndChangeCheck()) Edit("Вимоги гостя", d => { d.level.guests[index].shade = shade; d.level.guests[index].quiet = quiet; d.level.guests[index].assetId = tents[selected].assetId; });
                    if (GUILayout.Button("Поставити / перемістити намет у Scene View")) { PlacementToolIntegration.Adapter?.Stop(); _fieldTool = FieldTool.Tent; _guest = i; }
                }
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(level.guests.Length >= 6)) if (GUILayout.Button("Додати гостя")) Edit("Додати гостя", d => { var g = LevelAuthoringDocument.Copy(d.level.guests[0]); g.id = "guest_" + Guid.NewGuid().ToString("N").Substring(0, 8); d.level.guests = d.level.guests.Concat(new[] { g }).ToArray(); });
                using (new EditorGUI.DisabledScope(level.guests.Length <= 2)) if (GUILayout.Button("Прибрати останнього")) Edit("Прибрати гостя", d => { string id = d.level.guests.Last().id; d.level.guests = d.level.guests.Take(d.level.guests.Length - 1).ToArray(); d.level.friends = d.level.friends.Where(p => !p.Contains(id)).ToArray(); d.level.witness = (d.level.witness ?? Array.Empty<Placement>()).Where(p => p.guestId != id).ToArray(); });
            }
            var names = level.guests.Select((g, i) => "Гість " + (i + 1)).ToArray();
            string GuestLabel(string id) { int index = Array.FindIndex(level.guests, g => g.id == id); return index < 0 ? "Невідомий гість" : names[index]; }
            _friendA = EditorGUILayout.Popup("Друг", Mathf.Clamp(_friendA, 0, names.Length - 1), names); _friendB = EditorGUILayout.Popup("Друг", Mathf.Clamp(_friendB, 0, names.Length - 1), names);
            using (new EditorGUI.DisabledScope(_friendA == _friendB)) if (GUILayout.Button("З’єднати друзів")) Edit("Друзі", d => { var pair = new[] { d.level.guests[_friendA].id, d.level.guests[_friendB].id }; if (!d.level.friends.Any(p => p.Contains(pair[0]) && p.Contains(pair[1]))) d.level.friends = d.level.friends.Concat(new[] { pair }).ToArray(); });
            for (int i = 0; i < level.friends.Length; i++) { int index = i; if (GUILayout.Button("Прибрати зв’язок: " + string.Join(" ↔ ", level.friends[i].Select(GuestLabel)))) Edit("Прибрати друзів", d => d.level.friends = d.level.friends.Where((p, n) => n != index).ToArray()); }
            if (GUILayout.Button("Знайти еталонне рішення", GUILayout.Height(32))) Try(BeginSolve);
            if (GUILayout.Button("Очистити еталонні намети")) Edit("Очистити рішення", d => d.level.witness = null);
            if (GUILayout.Button("Перевірити всі умови")) Try(Validate);
        }
        void DuringScene(SceneView view)
        {
            using var timing = SceneTiming.Auto();
            if (_workspace?.Root == null || EditorApplication.isPlayingOrWillChangePlaymode) return;
            var level = Document.level;
            foreach (var cell in level.exteriorWalkable ?? Array.Empty<int[]>()) DrawCell(level, new Cell(cell[0], cell[1]), new Color(.65f, .6f, .35f, .24f));
            DrawCell(level, new Cell(level.entry[0], level.entry[1]), new Color(.2f, .85f, 1, .3f));
            foreach (var access in level.accessPoints ?? Array.Empty<AccessPointData>()) DrawCell(level, new Cell(access.x, access.z), new Color(.2f, .85f, 1, .3f));
            foreach (var cell in level.shade ?? Array.Empty<int[]>()) DrawCell(level, new Cell(cell[0], cell[1]), new Color(.25f, .72f, .35f, .14f));
            var placements = level.witness ?? Array.Empty<Placement>();
            if (_overlay == null || !ReferenceEquals(_overlayJson, _state.json))
            {
                using var rulesTiming = RulesTiming.Auto();
                _overlayJson = _state.json; _overlay = RuleEvaluator.Evaluate(level, placements, false);
            }
            var report = _overlay;
            foreach (var issue in report.Issues) foreach (var cell in issue.Cells) DrawCell(level, cell, new Color(1, .2f, .15f, .25f));
            for (int z = 0; z < level.height; z++) for (int x = 0; x < level.width; x++)
                if (RuleEvaluator.IsNoisy(level, new Cell(x, z))) DrawCell(level, new Cell(x, z), new Color(1, .6f, .1f, .14f));
            foreach (var route in report.Routes)
            {
                Handles.color = route.Reachable ? new Color(.2f, .85f, 1, .9f) : Color.red;
                Vector3 Point(Cell c) => new Vector3(c.X - level.width * .5f + .5f, .3f, c.Z - level.height * .5f + .5f);
                if (route.Cells.Length > 1) Handles.DrawAAPolyLine(4, route.Cells.Select(Point).ToArray());
                foreach (var c in route.BlockingCells) DrawCell(level, c, new Color(1, .1f, .1f, .5f));
                Handles.Label(Point(route.Goal), route.GuestId ?? route.AccessPointId ?? "Вхід");
            }
            DrawSelectedPropFootprint();
            if (_tab == 2 && DrawPropPlacement()) return;
            if (_tab == 3 && DrawRiverHandles()) return;
            if (_fieldTool == FieldTool.None || Event.current.alt) return;
            HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
            var ray = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);
            if (!new Plane(Vector3.up, Vector3.zero).Raycast(ray, out float distance)) return;
            var point = ray.GetPoint(distance); var picked = new Cell(Mathf.FloorToInt(point.x + level.width * .5f), Mathf.FloorToInt(point.z + level.height * .5f));
            DrawCell(level, picked, new Color(.9f, .8f, .2f, .3f));
            if (Event.current.type == EventType.MouseUp) _lastPaint = null;
            if ((Event.current.type != EventType.MouseDown && Event.current.type != EventType.MouseDrag) || Event.current.button != 0 || (_lastPaint.HasValue && _lastPaint.Value.Equals(picked))) return;
            _lastPaint = picked;
            Try(() => Edit("Редагувати клітину", d => Paint(d, picked))); Event.current.Use();
        }
        void Paint(LevelAuthoringDocument document, Cell cell)
        {
            var level = document.level;
            if (_fieldTool == FieldTool.Path)
            {
                if (RuleEvaluator.Inside(level, cell) || cell.X < -2 || cell.Z < -2 || cell.X > level.width + 1 || cell.Z > level.height + 1) throw new ArgumentException("Зовнішній прохід: до двох клітин поза полем.");
                if (!level.exteriorWalkable.Any(c => c[0] == cell.X && c[1] == cell.Z)) level.exteriorWalkable = level.exteriorWalkable.Concat(new[] { new[] { cell.X, cell.Z } }).ToArray(); return;
            }
            if (_fieldTool == FieldTool.Erase && !RuleEvaluator.Inside(level, cell)) { level.exteriorWalkable = level.exteriorWalkable.Where(c => c[0] != cell.X || c[1] != cell.Z).ToArray(); return; }
            if (!RuleEvaluator.Inside(level, cell)) throw new ArgumentException("Оберіть клітину поля.");
            if (_fieldTool == FieldTool.Entry || _fieldTool == FieldTool.Exit)
            {
                if (cell.X != 0 && cell.Z != 0 && cell.X != level.width - 1 && cell.Z != level.height - 1) throw new ArgumentException("Вхід і вихід мають бути на краю поля.");
                level.accessPoints=level.accessPoints.Where(a=>a.x!=cell.X||a.z!=cell.Z).ToArray();
                if (_fieldTool == FieldTool.Entry && _entryType == 0) level.entry = new[] { cell.X, cell.Z };
                else
                {
                    if(level.entry[0]==cell.X&&level.entry[1]==cell.Z)throw new ArgumentException("Основний вхід уже займає цю клітину.");
                    string kind=_fieldTool==FieldTool.Entry?"entry":"exit";
                    level.accessPoints=level.accessPoints.Concat(new[]{new AccessPointData{id=kind+"_"+cell.X+"_"+cell.Z,kind=kind,x=cell.X,z=cell.Z}}).ToArray();
                }
                return;
            }
            if (_fieldTool == FieldTool.Tent) { document.PutTent(level.guests[_guest].id, cell, _rotation); return; }
            if (_fieldTool == FieldTool.Erase)
            {
                document.RemoveObject(cell); level.accessPoints = level.accessPoints.Where(a => a.x != cell.X || a.z != cell.Z).ToArray();
                level.witness = (level.witness ?? Array.Empty<Placement>()).Where(p => !RuleEvaluator.Footprint(p).Contains(cell)).ToArray(); return;
            }
            document.RemoveObject(cell);
            document.PlaceObject(cell, _fieldTool == FieldTool.Fire ? "campfire_stones" : _fieldTool == FieldTool.Tree ? "tree_default" : "stone_largeA");
            if (_fieldTool == FieldTool.Tree) level.canopies = level.canopies.Concat(new[] { new ShadeCanopyData { x = cell.X, z = cell.Z, radiusX = 2.4f, radiusZ = 2.4f } }).ToArray();
        }
        static void DrawCell(LevelData level, Cell cell, Color color)
        {
            var p = new Vector3(cell.X - level.width * .5f, .22f, cell.Z - level.height * .5f);
            Handles.DrawSolidRectangleWithOutline(new[] { p, p + Vector3.right, p + Vector3.right + Vector3.forward, p + Vector3.forward }, color, new Color(color.r, color.g, color.b, .4f));
        }
        void HierarchyChanged()
        {
            if (_refreshing || _workspace == null || _workspace.Root == null) return;
            int count = _workspace.SceneryCount;
            if (count == _sceneryCount) return;
            _sceneryCount = count; _structureDirty = true; _hierarchyChangedAt = EditorApplication.timeSinceStartup;
        }
        void UndoChanged()
        {
            _pendingDocument = null; _applyAt = -1; _displayDocument = null; _overlay = null;
            if (_workspace?.Root == null || _state == null) return;
            _refreshing = true; try { _workspace.ApplyDocument(Document); _workspace.RefreshPlantBatches(); } finally { _refreshing = false; }
            _dirty = true; Repaint();
        }
        void Tick()
        {
            hasUnsavedChanges = _dirty;
            if (_applyAt >= 0 && EditorApplication.timeSinceStartup >= _applyAt && _workspace?.Root != null)
            {
                _applyAt = -1; _refreshing = true;
                try { if (GUIUtility.hotControl == 0) FlushEdits(); else _applyAt = EditorApplication.timeSinceStartup + .2; } catch (Exception e) { _message = e.Message; }
                finally { _refreshing = false; }
            }
            if (_workspace?.Root == null) _hierarchyChangedAt = -1;
            if (_workspace != null && _workspace.Root != null && !EditorApplication.isPlayingOrWillChangePlaymode)
                foreach (var selected in Selection.transforms)
                    if (selected.IsChildOf(_workspace.Scenery) && selected.hasChanged)
                    { selected.hasChanged = false; if (!_movedTransforms.Contains(selected)) _movedTransforms.Add(selected); _hierarchyChangedAt = EditorApplication.timeSinceStartup; }
            if (_solver != null)
            {
                var status = _solver.Step(.002);
                if (status != CampSolver.Status.Searching)
                {
                    var solution = status == CampSolver.Status.Solved ? _solver.Solution : null; CancelSolver();
                    if (solution != null) { Edit("Знайдене рішення", d => d.level.witness = solution); _message = "✓ Рішення знайдено й показано на полі."; }
                    else _message = status == CampSolver.Status.Timeout ? "Час пошуку вичерпано. Це не доказ відсутності рішення." : "Пазл не має рішення. Перевірте вимоги й проходи.";
                }
                Repaint();
            }
            if (_hierarchyChangedAt >= 0 && EditorApplication.timeSinceStartup - _hierarchyChangedAt > .35 && !EditorApplication.isPlayingOrWillChangePlaymode)
            {
                _hierarchyChangedAt = -1; _refreshing = true;
                try
                {
                    if (_structureDirty) _workspace.RefreshPlantBatches(); else _workspace.RefreshPlantBatches(_movedTransforms);
                    _structureDirty = false; _movedTransforms.Clear(); _dirty = true;
                }
                finally { _refreshing = false; }
            }
        }
    }
}
