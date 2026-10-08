using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace Kruty1918.LevelKit.EditorTools
{
    /// <summary>
    /// Hand-authoring level designer: paints named cell layers on a grid,
    /// edits typed entities, and keeps project-specific extras in a raw
    /// JSON props block. Reads and writes through the same profile-aware
    /// codec the runtime uses, so authored files are valid game input by
    /// construction. Everything is driven by the selected LevelProfile —
    /// the window itself is game-agnostic.
    /// </summary>
    public sealed class LevelDesignerWindow : EditorWindow
    {
        const int CellMin = 26;
        const int CellMax = 72;

        LevelDocument _doc;
        string _filePath;
        bool _dirty;
        int _cellPx = 44;
        int _paintLayer;
        Vector2 _scroll;
        Vector2 _entitiesScroll;
        string _propsText = "{}";
        bool _propsInvalid;
        readonly List<LevelIssue> _issues = new List<LevelIssue>();
        readonly List<string> _entityDataText = new List<string>();
        readonly List<bool> _entityDataInvalid = new List<bool>();
        readonly List<LevelEntity> _entitiesMirror = new List<LevelEntity>();

        List<DiscoveredProfile> _profiles;
        int _profileIndex;

        struct DiscoveredProfile
        {
            public string Label;
            public LevelProfile Profile;
        }

        LevelProfile Profile =>
            _profiles != null && _profileIndex >= 0 && _profileIndex < _profiles.Count
                ? _profiles[_profileIndex].Profile : LevelProfile.Canonical;

        [MenuItem("Tools/Level Kit/Level Designer")]
        public static void Open()
        {
            var w = GetWindow<LevelDesignerWindow>(false, "Level Designer", true);
            w.minSize = new Vector2(720, 420);
            w.Show();
        }

        void OnEnable() => RefreshProfiles();

        void RefreshProfiles()
        {
            _profiles = new List<DiscoveredProfile>
            {
                new DiscoveredProfile { Label = "Canonical (LevelKit)", Profile = LevelProfile.Canonical },
            };
            foreach (var guid in AssetDatabase.FindAssets("t:TextAsset levelprofile"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
                if (asset == null) continue;
                try
                {
                    var p = LevelProfile.FromJson(asset.text);
                    _profiles.Add(new DiscoveredProfile
                    {
                        Label = string.IsNullOrEmpty(p.GameId) ? Path.GetFileName(path) : p.GameId,
                        Profile = p,
                    });
                }
                catch { Debug.LogWarning($"[LevelKit] Bad profile at {path} — skipped."); }
            }
        }

        // ─── GUI ────────────────────────────────────────────────────────────

        void OnGUI()
        {
            DrawToolbar();
            if (_doc == null)
            {
                EditorGUILayout.HelpBox(
                    "New or Open a level JSON to start authoring.", MessageType.Info);
                return;
            }
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            DrawMeta();
            EditorGUILayout.Space(8);
            EditorGUILayout.BeginHorizontal();
            DrawGridArea();
            DrawSidePanel();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(6);
            DrawIssues();
            EditorGUILayout.EndScrollView();
        }

        void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            var labels = _profiles.Select(p => p.Label).ToArray();
            var idx = EditorGUILayout.Popup(_profileIndex, labels, EditorStyles.toolbarPopup,
                GUILayout.Width(180));
            if (idx != _profileIndex) { _profileIndex = idx; }
            if (GUILayout.Button("Rescan", EditorStyles.toolbarButton, GUILayout.Width(60)))
                RefreshProfiles();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("New", EditorStyles.toolbarButton, GUILayout.Width(44))) NewDoc();
            if (GUILayout.Button("Open…", EditorStyles.toolbarButton, GUILayout.Width(56))) OpenDoc();
            GUI.enabled = _doc != null;
            if (GUILayout.Button("Save", EditorStyles.toolbarButton, GUILayout.Width(48))) Save(false);
            if (GUILayout.Button("Save As…", EditorStyles.toolbarButton, GUILayout.Width(70))) Save(true);
            if (GUILayout.Button("Validate", EditorStyles.toolbarButton, GUILayout.Width(64))) ValidateNow();
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();

            var title = string.IsNullOrEmpty(_filePath) ? "untitled" : _filePath;
            EditorGUILayout.LabelField((_dirty ? "● " : "") + title, EditorStyles.miniLabel);
        }

        void DrawMeta()
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Level", EditorStyles.boldLabel);
            var newId = EditorGUILayout.TextField("Id", _doc.Id);
            if (newId != _doc.Id) { _doc.Id = newId; _dirty = true; }
            var newOrder = EditorGUILayout.IntField("Order", _doc.Order);
            if (newOrder != _doc.Order) { _doc.Order = newOrder; _dirty = true; }
            var newVer = EditorGUILayout.IntField("Schema version", _doc.SchemaVersion);
            if (newVer != _doc.SchemaVersion && newVer >= 1) { _doc.SchemaVersion = newVer; _dirty = true; }
            if (Profile.HasGrid)
            {
                EditorGUILayout.BeginHorizontal();
                var w = EditorGUILayout.IntField("Width", _doc.Width);
                var h = EditorGUILayout.IntField("Height", _doc.Height);
                EditorGUILayout.EndHorizontal();
                if ((w != _doc.Width || h != _doc.Height) && w > 0 && h > 0)
                { _doc.Width = w; _doc.Height = h; _dirty = true; }
            }
            _cellPx = EditorGUILayout.IntSlider("Cell px", _cellPx, CellMin, CellMax);
            EditorGUILayout.EndVertical();
        }

        // ─── Grid painting ──────────────────────────────────────────────────

        List<LayerProfileDef> EffectiveLayers()
        {
            var list = new List<LayerProfileDef>(Profile.Layers);
            foreach (var l in _doc.Layers)
            {
                if (list.All(d => d.Name != l.Name))
                    list.Add(new LayerProfileDef { Name = l.Name, Color = "#999999" });
            }
            return list;
        }

        void DrawGridArea()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(_doc.Width * _cellPx + 60));
            EditorGUILayout.LabelField("Layers", EditorStyles.boldLabel);
            var defs = EffectiveLayers();
            for (var i = 0; i < defs.Count; i++)
            {
                var d = defs[i];
                EditorGUILayout.BeginHorizontal();
                var prev = GUI.color;
                GUI.color = ParseColor(d.Color, Color.gray);
                GUILayout.Box("", GUILayout.Width(18), GUILayout.Height(18));
                GUI.color = prev;
                var selected = _paintLayer == i;
                if (GUILayout.Toggle(selected, d.Name + (d.Multiple ? "" : " (single)"),
                    "Button", GUILayout.Width(150)))
                    _paintLayer = i;
                if (i != _paintLayer && selected) _paintLayer = i;
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.Space(6);

            if (_doc.HasGrid)
            {
                var rect = GUILayoutUtility.GetRect(_doc.Width * _cellPx,
                    _doc.Height * _cellPx, GUILayout.ExpandWidth(false));
                DrawGrid(rect, defs);
                HandleGridInput(rect, defs);
            }
            else EditorGUILayout.HelpBox("Profile has no grid — entities only.", MessageType.None);
            EditorGUILayout.EndVertical();
        }

        void DrawGrid(Rect rect, List<LayerProfileDef> defs)
        {
            EditorGUI.DrawRect(rect, new Color(0.32f, 0.34f, 0.30f));
            var hovered = CellAt(rect, Event.current.mousePosition);
            for (var z = _doc.Height - 1; z >= 0; z--)
            for (var x = 0; x < _doc.Width; x++)
            {
                var cell = CellRect(rect, x, z);
                var baseCol = ((x + z) & 1) == 0
                    ? new Color(0.55f, 0.64f, 0.48f) : new Color(0.50f, 0.59f, 0.43f);
                EditorGUI.DrawRect(cell, baseCol);
                var pos = new CellPos(x, z);
                for (var i = 0; i < defs.Count; i++)
                {
                    var layer = _doc.LayerOrNull(defs[i].Name);
                    if (layer == null || !layer.Contains(pos)) continue;
                    var c = ParseColor(defs[i].Color, Color.gray);
                    c.a = defs[i].Multiple ? 0.75f : 0.95f;
                    EditorGUI.DrawRect(cell, c);
                }
            }
            // Entity position markers.
            foreach (var e in _doc.Entities)
            {
                if (!e.HasPosition || e.X >= _doc.Width || e.Z >= _doc.Height) continue;
                var cell = CellRect(rect, e.X, e.Z);
                GUI.Label(new Rect(cell.x + 3, cell.y + cell.height / 2 - 9, cell.width - 4, 18),
                    e.Id, EditorStyles.whiteMiniLabel);
            }
            // Grid lines + hover highlight.
            for (var x = 0; x <= _doc.Width; x++)
                EditorGUI.DrawRect(new Rect(rect.x + x * _cellPx, rect.y, 1, rect.height),
                    new Color(0, 0, 0, 0.35f));
            for (var z = 0; z <= _doc.Height; z++)
                EditorGUI.DrawRect(new Rect(rect.x, rect.y + (_doc.Height - z) * _cellPx,
                    rect.width, 1), new Color(0, 0, 0, 0.35f));
            if (hovered.HasValue && Event.current.type == EventType.Repaint)
            {
                var hr = CellRect(rect, hovered.Value.X, hovered.Value.Z);
                EditorGUI.DrawRect(hr, new Color(1, 1, 1, 0.15f));
            }
        }

        void HandleGridInput(Rect rect, List<LayerProfileDef> defs)
        {
            var e = Event.current;
            var cell = CellAt(rect, e.mousePosition);
            if (!cell.HasValue) return;
            if (e.type == EventType.MouseDown || e.type == EventType.MouseDrag)
            {
                if (e.button == 0 && defs.Count > 0)
                {
                    var def = defs[Mathf.Clamp(_paintLayer, 0, defs.Count - 1)];
                    var layer = _doc.Layer(def.Name);
                    bool changed;
                    if (def.Multiple) changed = layer.Add(cell.Value);
                    else if (!layer.Contains(cell.Value)) { layer.SetSingle(cell.Value); changed = true; }
                    else changed = false;
                    if (changed) { _dirty = true; Repaint(); }
                    e.Use();
                }
                else if (e.button == 1)
                {
                    foreach (var l in _doc.Layers)
                        if (l.Remove(cell.Value)) _dirty = true;
                    Repaint();
                    e.Use();
                }
            }
        }

        CellPos? CellAt(Rect rect, Vector2 mouse)
        {
            if (!rect.Contains(mouse)) return null;
            var x = Mathf.FloorToInt((mouse.x - rect.x) / _cellPx);
            var zFromTop = Mathf.FloorToInt((mouse.y - rect.y) / _cellPx);
            var z = _doc.Height - 1 - zFromTop;
            if (x < 0 || x >= _doc.Width || z < 0 || z >= _doc.Height) return null;
            return new CellPos(x, z);
        }

        Rect CellRect(Rect rect, int x, int z)
            => new Rect(rect.x + x * _cellPx, rect.y + (_doc.Height - 1 - z) * _cellPx,
                _cellPx, _cellPx);

        static Color ParseColor(string hex, Color fallback)
            => ColorUtility.TryParseHtmlString(hex, out var c) ? c : fallback;

        // ─── Entities / props / issues ──────────────────────────────────────

        void DrawSidePanel()
        {
            EditorGUILayout.BeginVertical();
            EditorGUILayout.LabelField("Entities", EditorStyles.boldLabel);
            SyncEntityMirror();
            _entitiesScroll = EditorGUILayout.BeginScrollView(_entitiesScroll,
                GUILayout.MinHeight(140), GUILayout.MaxHeight(280));
            for (var i = 0; i < _entitiesMirror.Count; i++) DrawEntityRow(i);
            EditorGUILayout.EndScrollView();
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("+ Entity")) AddEntity("");
            foreach (var def in Profile.EntityKinds)
                if (GUILayout.Button("+ " + def.Kind)) AddEntity(def.Kind);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Extra props (JSON object)", EditorStyles.boldLabel);
            var style = new GUIStyle(EditorStyles.textArea) { wordWrap = false };
            var newProps = EditorGUILayout.TextArea(_propsText, style,
                GUILayout.MinHeight(70), GUILayout.MaxHeight(120));
            if (newProps != _propsText) { _propsText = newProps; _dirty = true; }
            if (_propsInvalid)
                EditorGUILayout.HelpBox("Props JSON is invalid — fix before saving.",
                    MessageType.Error);
            EditorGUILayout.EndVertical();
        }

        void SyncEntityMirror()
        {
            // Rebuild the mirror when the doc's list shape changed externally.
            if (_entitiesMirror.Count == _doc.Entities.Count
                && !_entitiesMirror.Where((e, i) => e != _doc.Entities[i]).Any())
                return;
            _entitiesMirror.Clear();
            _entitiesMirror.AddRange(_doc.Entities);
            _entityDataText.Clear();
            _entityDataInvalid.Clear();
            foreach (var e in _entitiesMirror)
            {
                _entityDataText.Add(e.Data != null && e.Data.Count > 0
                    ? e.Data.ToString(Newtonsoft.Json.Formatting.Indented) : "{}");
                _entityDataInvalid.Add(false);
            }
        }

        void DrawEntityRow(int i)
        {
            var e = _entitiesMirror[i];
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.BeginHorizontal();
            var kinds = Profile.EntityKinds.Select(k => k.Kind).ToList();
            var kindIdx = kinds.IndexOf(e.Kind);
            var newKindIdx = EditorGUILayout.Popup(kindIdx < 0 ? 0 : kindIdx,
                kinds.Count > 0 ? kinds.ToArray() : new[] { e.Kind },
                GUILayout.Width(110));
            var newKind = kinds.Count > 0 ? kinds[Mathf.Clamp(newKindIdx, 0, kinds.Count - 1)] : e.Kind;
            if (newKind != e.Kind) { e.Kind = newKind; _dirty = true; }
            var newId = EditorGUILayout.TextField(e.Id);
            if (newId != e.Id) { e.Id = newId; _dirty = true; }
            if (GUILayout.Button("×", GUILayout.Width(22)))
            {
                _doc.Entities.Remove(e);
                _entitiesMirror.RemoveAt(i);
                _entityDataText.RemoveAt(i);
                _entityDataInvalid.RemoveAt(i);
                _dirty = true;
                GUIUtility.ExitGUI();
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            var x = EditorGUILayout.IntField("x", e.X, GUILayout.Width(90));
            var z = EditorGUILayout.IntField("z", e.Z, GUILayout.Width(90));
            var r = EditorGUILayout.IntField("rot", e.Rotation, GUILayout.Width(90));
            if (x != e.X || z != e.Z || r != e.Rotation)
            { e.X = x; e.Z = z; e.Rotation = r; _dirty = true; }
            EditorGUILayout.EndHorizontal();
            var style = new GUIStyle(EditorStyles.textArea) { wordWrap = false };
            var newText = EditorGUILayout.TextArea(_entityDataText[i], style,
                GUILayout.MinHeight(36), GUILayout.MaxHeight(80));
            if (newText != _entityDataText[i])
            {
                _entityDataText[i] = newText;
                try
                {
                    var parsed = JObject.Parse(newText);
                    e.Data = parsed;
                    _entityDataInvalid[i] = false;
                    _dirty = true;
                }
                catch { _entityDataInvalid[i] = true; }
            }
            if (_entityDataInvalid[i])
                EditorGUILayout.HelpBox("Invalid JSON", MessageType.Error);
            EditorGUILayout.EndVertical();
        }

        void AddEntity(string kind)
        {
            var e = new LevelEntity { Id = "e" + (_doc.Entities.Count + 1), Kind = kind };
            _doc.Entities.Add(e);
            _entitiesMirror.Clear(); // force resync
            _dirty = true;
        }

        void DrawIssues()
        {
            if (_issues.Count == 0) return;
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Validation", EditorStyles.boldLabel);
            foreach (var i in _issues)
            {
                var icon = i.Severity == LevelIssueSeverity.Error ? MessageType.Error
                    : i.Severity == LevelIssueSeverity.Warning ? MessageType.Warning
                    : MessageType.Info;
                EditorGUILayout.HelpBox(i.ToString(), icon);
            }
            EditorGUILayout.EndVertical();
        }

        // ─── Document lifecycle ─────────────────────────────────────────────

        void NewDoc()
        {
            _doc = new LevelDocument { Id = "level_new", SchemaVersion = 1 };
            if (Profile.HasGrid) { _doc.Width = 6; _doc.Height = 6; }
            foreach (var def in Profile.Layers) _doc.Layer(def.Name);
            _filePath = null;
            _propsText = "{}";
            _propsInvalid = false;
            _entitiesMirror.Clear();
            _issues.Clear();
            _dirty = true;
        }

        void OpenDoc()
        {
            var path = EditorUtility.OpenFilePanel("Open level JSON", "Assets", "json");
            if (string.IsNullOrEmpty(path)) return;
            var result = LevelJson.Parse(File.ReadAllText(path), Profile);
            _issues.Clear();
            _issues.AddRange(result.Issues);
            if (result.Document == null) return;
            _doc = result.Document;
            _filePath = path;
            _dirty = false;
            _propsText = _doc.Props != null && _doc.Props.Count > 0
                ? _doc.Props.ToString(Newtonsoft.Json.Formatting.Indented) : "{}";
            _propsInvalid = false;
            _entitiesMirror.Clear();
            Repaint();
        }

        void ApplyProps()
        {
            _propsInvalid = false;
            try { _doc.Props = JObject.Parse(_propsText); }
            catch { _propsInvalid = true; }
        }

        void ValidateNow()
        {
            ApplyProps();
            _issues.Clear();
            if (_propsInvalid)
                _issues.Add(new LevelIssue(LevelIssueSeverity.Error, "props",
                    "extra props JSON is invalid"));
            if (_entityDataInvalid.Any(v => v))
                _issues.Add(new LevelIssue(LevelIssueSeverity.Error, "entities",
                    "entity data JSON is invalid"));
            _issues.AddRange(LevelJson.Validate(_doc, Profile));
        }

        void Save(bool saveAs)
        {
            ApplyProps();
            if (_propsInvalid || _entityDataInvalid.Any(v => v))
            {
                EditorUtility.DisplayDialog("Level Designer",
                    "Fix invalid JSON blocks before saving.", "OK");
                return;
            }
            if (saveAs || string.IsNullOrEmpty(_filePath))
            {
                var dir = !string.IsNullOrEmpty(_filePath)
                    ? Path.GetDirectoryName(_filePath) : "Assets";
                var path = EditorUtility.SaveFilePanel("Save level JSON", dir,
                    string.IsNullOrEmpty(_doc.Id) ? "level" : _doc.Id, "json");
                if (string.IsNullOrEmpty(path)) return;
                _filePath = path;
            }
            File.WriteAllText(_filePath, LevelJson.Write(_doc, Profile));
            _dirty = false;
            if (_filePath.StartsWith(Application.dataPath.Replace('/', '\\'))
                || _filePath.StartsWith(Application.dataPath))
                AssetDatabase.Refresh();
            _issues.Clear();
            _issues.AddRange(LevelJson.Validate(_doc, Profile));
        }
    }
}
