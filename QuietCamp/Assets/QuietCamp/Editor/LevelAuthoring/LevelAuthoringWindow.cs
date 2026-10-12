using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using QuietCamp.Domain;
using QuietCamp.Infrastructure;
using UnityEditor;
using UnityEngine;

namespace QuietCamp.Editor.LevelAuthoring
{
    public sealed class LevelAuthoringWindow : EditorWindow
    {
        enum FieldTool { None, Stone, Tree, Fire, Erase, Entry, Exit, Path, Tent }
        [SerializeField] LevelAuthoringState _state;
        [SerializeField] string _path;
        [SerializeField] int _tab, _category, _guest, _rotation;
        [SerializeField] bool _dirty;
        LevelAuthoringWorkspace _workspace;
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
        static readonly string[] Categories = { "Дерева", "Кущі", "Трава", "Квіти", "Каміння", "Паркани", "Містки", "Декор" };
        public LevelAuthoringWorkspace Workspace => _workspace;
        public LevelAuthoringDocument Document => _state?.Read();

        [MenuItem("Quiet Camp/Майстерня рівнів")]
        public static LevelAuthoringWindow OpenWindow()
        {
            var window = GetWindow<LevelAuthoringWindow>(); window.titleContent = new GUIContent("Рівні QuietCamp"); window.minSize = new Vector2(440, 480); return window;
        }
        void OnEnable()
        {
            SceneView.duringSceneGui += DuringScene; Undo.undoRedoPerformed += UndoChanged;
            EditorApplication.update += Tick; EditorApplication.hierarchyChanged += HierarchyChanged;
            if (_state != null && !EditorApplication.isPlayingOrWillChangePlaymode) EditorApplication.delayCall += Recover;
        }
        void OnDisable()
        {
            SceneView.duringSceneGui -= DuringScene; Undo.undoRedoPerformed -= UndoChanged;
            EditorApplication.update -= Tick; EditorApplication.hierarchyChanged -= HierarchyChanged;
            PlacementToolIntegration.Adapter?.Stop();
            if (_workspace != null && !EditorApplication.isPlayingOrWillChangePlaymode)
            {
                _state.Write(_workspace.Capture()); _workspace.Dispose(); _workspace = null;
            }
        }
        void Recover()
        {
            if (this == null || _workspace != null || _state == null) return;
            Try(() => _workspace = new LevelAuthoringWorkspace(_state.Read()));
        }
        public void PreparePlay()
        {
            CancelSolver(); PlacementToolIntegration.Adapter?.Stop();
            if (_workspace == null) return;
            _state.Write(_workspace.Capture()); _workspace.Dispose(); _workspace = null;
        }
        public void RestoreAfterPlay() => Recover();
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
            SetDocument(LevelAuthoringDocument.Create(generated)); _path = null; _dirty = true;
            _message = generated ? "Основа з QC007. Об’єкти вже можна редагувати вручну." : "Порожнє поле. Додайте ігрові об’єкти та гостей, потім намалюйте оточення.";
        }
        public void SetDocument(LevelAuthoringDocument document)
        {
            CancelSolver(); PlacementToolIntegration.Adapter?.Stop(); _fieldTool = FieldTool.None;
            _refreshing = true;
            try
            {
                _workspace?.Dispose();
                if (_state == null) { _state = CreateInstance<LevelAuthoringState>(); _state.hideFlags = HideFlags.HideAndDontSave; }
                _state.Write(document); _workspace = new LevelAuthoringWorkspace(document);
                _state.Write(_workspace.Capture());
                _sceneryCount = _workspace.SceneryCount;
            }
            finally { _refreshing = false; }
            Repaint();
        }
        void Edit(string description, Action<LevelAuthoringDocument> action)
        {
            CancelSolver(); var document = Document; Undo.RecordObject(_state, description);
            action(document); document.ReconcileObjects(); _state.Write(document);
            _refreshing = true; try { _workspace.ApplyDocument(document); } finally { _refreshing = false; }
            _dirty = true; Repaint(); SceneView.RepaintAll();
        }
        public void Save()
        {
            if (_workspace == null) return;
            string path = _path;
            if (string.IsNullOrEmpty(path)) path = EditorUtility.SaveFilePanel("Зберегти чернетку", Path.GetFullPath("Assets/QuietCamp/Authoring/Levels"), Document.level.id + ".draft", "json");
            if (string.IsNullOrEmpty(path)) return;
            var document = _workspace.Capture(); document.Save(path); _state.Write(document); _path = path; _dirty = false; _message = "Чернетку збережено.";
        }
        void OpenDraft()
        {
            if (!ConfirmReplace()) return;
            string path = EditorUtility.OpenFilePanel("Відкрити чернетку", Path.GetFullPath("Assets/QuietCamp/Authoring/Levels"), "json");
            if (string.IsNullOrEmpty(path)) return;
            SetDocument(LevelAuthoringDocument.Open(path)); _path = path; _dirty = false;
        }
        public void BeginSolve()
        {
            var snapshot = _workspace.Capture(); var errors = snapshot.Validate(false);
            if (errors.Count > 0) { _message = string.Join("\n", errors); return; }
            _solveLevel = LevelAuthoringDocument.Copy(snapshot.level); _solveLevel.witness = null;
            _solver = new CampSolver(_solveLevel, Array.Empty<Placement>(), 20);
            _message = "Шукаю рішення… Можна скасувати пошук.";
        }
        public void CancelSolver() { _solver = null; _solveLevel = null; }
        void Validate()
        {
            var errors = _workspace.Capture().Validate(true);
            _message = errors.Count == 0 ? "✓ Пазл і композиція коректні. Рішення виконує всі умови." : string.Join("\n", errors);
        }
        void Export()
        {
            string directory = EditorUtility.OpenFolderPanel("Експорт перевіреного рівня", Path.GetFullPath("Assets/QuietCamp/Authoring"), "");
            if (string.IsNullOrEmpty(directory)) return;
            _workspace.Capture().Export(directory); _message = "Експорт готовий. Кампанія не перепубліковувалася.";
        }
        void Try(Action action)
        {
            try { action(); } catch (Exception e) { _message = e.Message; Debug.LogWarning("[LevelAuthoring] " + e.Message); }
            Repaint();
        }
        void OnGUI()
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
                    if (GUILayout.Button("Грати", EditorStyles.toolbarButton)) Try(() => LevelAuthoringPlayDriver.Start(_workspace.Capture()));
                    if (GUILayout.Button("Експорт", EditorStyles.toolbarButton)) Try(Export);
                }
            }
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorGUILayout.HelpBox("Тестовий запуск із тимчасовим прогресом. Зупиніть Play Mode, щоб продовжити редагування.", MessageType.Info);
                if (GUILayout.Button("Зупинити тест")) EditorApplication.isPlaying = false; return;
            }
            if (_workspace == null)
            {
                EditorGUILayout.Space(16); GUILayout.Label("Складіть свій табір", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox("Оберіть початок. Поле й умови гри редагуються тут, оточення — готовими інструментами PWB у Scene View.", MessageType.Info);
                if (GUILayout.Button("Порожнє поле", GUILayout.Height(36))) Try(() => NewDocument(false));
                if (GUILayout.Button("Взяти згенерований табір", GUILayout.Height(36))) Try(() => NewDocument(true));
                if (!string.IsNullOrEmpty(_message)) EditorGUILayout.HelpBox(_message, MessageType.Warning); return;
            }
            _tab = GUILayout.Toolbar(_tab, new[] { "Поле", "Сцена", "Гості та перевірка" });
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            using (new EditorGUI.DisabledScope(_solver != null))
            {
                if (_tab == 0) DrawField(); else if (_tab == 1) DrawScenery(); else DrawGuests();
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
        }
        void DrawScenery()
        {
            if (PlacementToolIntegration.Adapter == null) { EditorGUILayout.HelpBox("PWB ще не підключений. Імпортуйте наданий пакет і дочекайтеся компіляції.", MessageType.Warning); return; }
            _category = GUILayout.SelectionGrid(_category, Categories, 4);
            var entries = LevelAuthoringPalette.Entries.Where(e => e.category == Categories[_category]).ToArray();
            for (int row = 0; row < entries.Length; row += 3)
            {
                using (new EditorGUILayout.HorizontalScope()) for (int i = row; i < Math.Min(row + 3, entries.Length); i++)
                {
                    var entry = entries[i]; var image = AssetPreview.GetAssetPreview(entry.prefab) ?? AssetPreview.GetMiniThumbnail(entry.prefab);
                    var content = new GUIContent(entry.label + "\nдекор", image, entry.id);
                    if (GUILayout.Button(content, _asset == entry.id ? EditorStyles.helpBox : GUI.skin.button, GUILayout.Height(72))) _asset = entry.id;
                }
            }
            _radius = EditorGUILayout.Slider("Розмір пензля", _radius, .15f, 4);
            _density = EditorGUILayout.Slider("Густота", _density, 1, 100);
            _spacing = EditorGUILayout.Slider("Мін. відстань", _spacing, .03f, 2);
            _variation = EditorGUILayout.Slider("Варіація масштабу", _variation, 0, .5f);
            using (new EditorGUILayout.HorizontalScope()) foreach (var pair in new[] { ("Пензель", PlacementTool.Paint), ("Один об’єкт", PlacementTool.Place), ("Лінія", PlacementTool.Line), ("Гумка", PlacementTool.Erase) })
                if (GUILayout.Button(pair.Item1)) Try(() => { _fieldTool = FieldTool.None; PlacementToolIntegration.Adapter.Activate(pair.Item2, LevelAuthoringPalette.Get(_asset).prefab, _workspace.Scenery, _workspace.Surface, _radius, _density, _spacing, _variation); });
            if (GUILayout.Button("Завершити інструмент / перемістити об’єкти")) PlacementToolIntegration.Adapter.Stop();
            EditorGUILayout.HelpBox("Це декор: його колайдери не є перешкодами пазла. Ігрові дерево, камінь і вогнище додаються у вкладці «Поле». Великий декор залишайте поза сіткою.", MessageType.None);
            var art = Document.art;
            EditorGUI.BeginChangeCheck(); float intensity = EditorGUILayout.Slider("Світло", art.sunIntensity, .4f, 2);
            float sunYaw = EditorGUILayout.Slider("Напрям сонця", art.sunEuler[1], 0, 360);
            float size = EditorGUILayout.Slider("Масштаб камери", art.orthographicSize, 4, 18);
            if (EditorGUI.EndChangeCheck()) Edit("Світло й камера", d => { d.art.sunIntensity = intensity; d.art.sunEuler[1] = sunYaw; d.art.orthographicSize = size; });
            EditorGUILayout.Space(); GUILayout.Label("Шаблон: річкова галявина", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            float riverWidth = EditorGUILayout.Slider("Ширина річки", art.streamWidth, .8f, 5);
            float riverCentre = EditorGUILayout.Slider("Положення річки", art.streamCentre, 4, 12);
            float bend = EditorGUILayout.Slider("Вигин русла", art.streamBend, 0, 1);
            float relief = EditorGUILayout.Slider("Рельєф берега", art.meadowRelief, 0, .5f);
            float depth = EditorGUILayout.Slider("Глибина берегів", art.waterDepth, .2f, 1.4f);
            if (EditorGUI.EndChangeCheck()) Edit("Змінити річку й берег", d => { d.art.streamWidth = riverWidth; d.art.streamCentre = riverCentre; d.art.streamBend = bend; d.art.meadowRelief = relief; d.art.waterDepth = depth; });
            if (GUILayout.Button("Детальні налаштування PWB")) PlacementToolIntegration.Adapter.OpenAdvanced();
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
                    int selected = EditorGUILayout.Popup("Намет", Mathf.Max(0, old), tents.Select(e => e.assetId).ToArray());
                    if (EditorGUI.EndChangeCheck()) Edit("Вимоги гостя", d => { d.level.guests[index].shade = shade; d.level.guests[index].quiet = quiet; d.level.guests[index].assetId = tents[selected].assetId; });
                    if (GUILayout.Button("Поставити / перемістити намет у Scene View")) { PlacementToolIntegration.Adapter?.Stop(); _fieldTool = FieldTool.Tent; _guest = i; _tab = 0; }
                }
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(level.guests.Length >= 6)) if (GUILayout.Button("Додати гостя")) Edit("Додати гостя", d => { var g = LevelAuthoringDocument.Copy(d.level.guests[0]); g.id = "guest_" + Guid.NewGuid().ToString("N").Substring(0, 8); d.level.guests = d.level.guests.Concat(new[] { g }).ToArray(); });
                using (new EditorGUI.DisabledScope(level.guests.Length <= 2)) if (GUILayout.Button("Прибрати останнього")) Edit("Прибрати гостя", d => { string id = d.level.guests.Last().id; d.level.guests = d.level.guests.Take(d.level.guests.Length - 1).ToArray(); d.level.friends = d.level.friends.Where(p => !p.Contains(id)).ToArray(); d.level.witness = (d.level.witness ?? Array.Empty<Placement>()).Where(p => p.guestId != id).ToArray(); });
            }
            var names = level.guests.Select((g, i) => "Гість " + (i + 1)).ToArray();
            _friendA = EditorGUILayout.Popup("Друг", Mathf.Clamp(_friendA, 0, names.Length - 1), names); _friendB = EditorGUILayout.Popup("Друг", Mathf.Clamp(_friendB, 0, names.Length - 1), names);
            using (new EditorGUI.DisabledScope(_friendA == _friendB)) if (GUILayout.Button("З’єднати друзів")) Edit("Друзі", d => { var pair = new[] { d.level.guests[_friendA].id, d.level.guests[_friendB].id }; if (!d.level.friends.Any(p => p.Contains(pair[0]) && p.Contains(pair[1]))) d.level.friends = d.level.friends.Concat(new[] { pair }).ToArray(); });
            for (int i = 0; i < level.friends.Length; i++) { int index = i; if (GUILayout.Button("Прибрати зв’язок: " + string.Join(" ↔ ", level.friends[i]))) Edit("Прибрати друзів", d => d.level.friends = d.level.friends.Where((p, n) => n != index).ToArray()); }
            if (GUILayout.Button("Знайти еталонне рішення", GUILayout.Height(32))) Try(BeginSolve);
            if (GUILayout.Button("Очистити еталонні намети")) Edit("Очистити рішення", d => d.level.witness = null);
            if (GUILayout.Button("Перевірити всі умови")) Try(Validate);
        }
        void DuringScene(SceneView view)
        {
            if (_workspace == null || EditorApplication.isPlayingOrWillChangePlaymode) return;
            var level = Document.level;
            foreach (var cell in level.shade ?? Array.Empty<int[]>()) DrawCell(level, new Cell(cell[0], cell[1]), new Color(.3f, .65f, 1, .13f));
            var placements = level.witness ?? Array.Empty<Placement>();
            var report = RuleEvaluator.Evaluate(level, placements, false);
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
                if (_fieldTool == FieldTool.Entry) level.entry = new[] { cell.X, cell.Z };
                else level.accessPoints = level.accessPoints.Where(a => a.x != cell.X || a.z != cell.Z).Concat(new[] { new AccessPointData { id = "exit_" + cell.X + "_" + cell.Z, kind = "exit", x = cell.X, z = cell.Z } }).ToArray(); return;
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
            _sceneryCount = count; _hierarchyChangedAt = EditorApplication.timeSinceStartup;
        }
        void UndoChanged()
        {
            if (_workspace == null || _state == null) return;
            _refreshing = true; try { _workspace.ApplyDocument(Document); _workspace.RefreshPlantBatches(); } finally { _refreshing = false; }
            _dirty = true; Repaint();
        }
        void Tick()
        {
            if (_workspace != null && _workspace.Root != null && !EditorApplication.isPlayingOrWillChangePlaymode)
                foreach (var selected in Selection.transforms)
                    if (selected.IsChildOf(_workspace.Scenery) && selected.hasChanged)
                    { selected.hasChanged = false; _hierarchyChangedAt = EditorApplication.timeSinceStartup; }
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
                try { _workspace.RefreshPlantBatches(); _dirty = true; } finally { _refreshing = false; }
            }
        }
    }
}
