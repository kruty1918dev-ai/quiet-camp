using System.IO;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace Kruty1918.LevelGen.Editor
{
    /// <summary>
    /// Recipe playground: paste or load a recipe JSON, generate with any seed,
    /// see the grid as colored layers, and save the result as GenLevel JSON.
    /// Menu: Tools → LevelGen → Generator Playground.
    /// </summary>
    public sealed class LevelGenWindow : EditorWindow
    {
        TextAsset _recipeAsset;
        string _recipeJson = "";
        int _seed;
        GenLevel _level;
        string _status = "";
        Vector2 _scroll;
        float _cell = 14f;

        static readonly Color[] Palette =
        {
            new Color(0.95f, 0.45f, 0.30f), new Color(0.30f, 0.65f, 0.95f),
            new Color(0.45f, 0.85f, 0.45f), new Color(0.95f, 0.80f, 0.25f),
            new Color(0.75f, 0.45f, 0.95f), new Color(0.35f, 0.85f, 0.75f),
            new Color(0.95f, 0.55f, 0.75f), new Color(0.60f, 0.60f, 0.60f),
        };

        [MenuItem("Tools/LevelGen/Generator Playground")]
        public static void Open() => GetWindow<LevelGenWindow>("LevelGen");

        void OnGUI()
        {
            EditorGUILayout.LabelField("Recipe", EditorStyles.boldLabel);
            var prev = _recipeAsset;
            _recipeAsset = (TextAsset)EditorGUILayout.ObjectField("Recipe JSON", _recipeAsset, typeof(TextAsset), false);
            if (_recipeAsset != prev && _recipeAsset != null) _recipeJson = _recipeAsset.text;

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            _recipeJson = EditorGUILayout.TextArea(_recipeJson, GUILayout.MinHeight(90));
            EditorGUILayout.EndScrollView();

            using (new EditorGUILayout.HorizontalScope())
            {
                _seed = EditorGUILayout.IntField("Seed", _seed);
                if (GUILayout.Button("Generate", GUILayout.Width(90))) Generate();
                if (GUILayout.Button("Save JSON...", GUILayout.Width(90))) Save();
            }

            if (_status.Length > 0) EditorGUILayout.HelpBox(_status, MessageType.Info);
            if (_level != null) DrawGrid();
        }

        void Generate()
        {
            try
            {
                var recipe = GenRecipe.FromJson(_recipeJson);
                var result = LevelGenerator.Generate(recipe, _seed);
                _level = result.Level;
                _status = result.Ok
                    ? $"OK — {result.Level.width}x{result.Level.height}, seed {result.Level.seed}, attempts {result.Attempts}, " +
                      $"{result.Level.Entities.Count} entities, {result.Level.Layers.Count} layers"
                    : "failed: " + string.Join("; ", result.Issues);
            }
            catch (GenRecipeException e) { _status = "recipe error: " + e.Message; _level = null; }
        }

        void DrawGrid()
        {
            _cell = EditorGUILayout.Slider("Cell px", _cell, 6f, 28f);
            var l = _level;
            var rect = GUILayoutUtility.GetRect(l.width * _cell, l.height * _cell);
            var layerNames = new System.Collections.Generic.List<string>(l.Layers.Keys);
            for (var y = 0; y < l.height; y++)
                for (var x = 0; x < l.width; x++)
                {
                    var c = new Rect(rect.x + x * _cell, rect.y + y * _cell, _cell - 1, _cell - 1);
                    var col = new Color(0.16f, 0.16f, 0.16f);
                    for (var i = 0; i < layerNames.Count; i++)
                        if (l.Layers[layerNames[i]].Contains(new GenCell(x, y)))
                            col = Palette[i % Palette.Length];
                    EditorGUI.DrawRect(c, col);
                }
            foreach (var e in l.Entities)
            {
                var c = new Rect(rect.x + e.x * _cell + _cell * 0.2f, rect.y + e.y * _cell + _cell * 0.2f,
                    _cell * 0.6f, _cell * 0.6f);
                EditorGUI.DrawRect(c, Color.white);
            }
            EditorGUILayout.LabelField("Layers: " + string.Join(", ", layerNames) +
                $"   •   white dots = entities ({l.Entities.Count})");
        }

        void Save()
        {
            if (_level == null) { _status = "nothing to save — generate first"; return; }
            var path = EditorUtility.SaveFilePanelInProject("Save Generated Level",
                "level.json", "json", "Choose where to save the generated level.");
            if (string.IsNullOrEmpty(path)) return;
            File.WriteAllText(path, GenLevelJson.Write(_level));
            AssetDatabase.ImportAsset(path);
            _status = "saved: " + path;
        }
    }
}
