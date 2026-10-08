using System.IO;
using UnityEditor;
using UnityEngine;

namespace Kruty1918.Atmos.Editor
{
    /// <summary>
    /// Live sky authoring tool: edit a SkySpec with immediate preview on the
    /// scene skybox, then save it as a .mat asset (for RenderSettings.skybox)
    /// or as JSON (for SkyController presets / runtime loading).
    /// Menu: Tools → Atmos → Sky Designer.
    /// </summary>
    public sealed class SkyDesignerWindow : EditorWindow
    {
        SkySpec _spec = new SkySpec();
        Material _preview;
        bool _previewing;
        Vector2 _scroll;

        [MenuItem("Tools/Atmos/Sky Designer")]
        public static void Open() => GetWindow<SkyDesignerWindow>("Sky Designer");

        void OnDisable() => StopPreview();

        void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            EditorGUILayout.LabelField("Sky Spec", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            _spec.zenith = EditorGUILayout.ColorField("Zenith", _spec.zenith);
            _spec.horizon = EditorGUILayout.ColorField("Horizon", _spec.horizon);
            _spec.ground = EditorGUILayout.ColorField("Ground", _spec.ground);
            _spec.sunColor = EditorGUILayout.ColorField("Sun Color", _spec.sunColor);
            _spec.sunDir = EditorGUILayout.Vector3Field("Sun Direction", _spec.sunDir);
            _spec.sunSize = EditorGUILayout.Slider("Sun Size", _spec.sunSize, 0.001f, 0.3f);
            _spec.sunHalo = EditorGUILayout.Slider("Sun Halo", _spec.sunHalo, 0f, 1f);
            _spec.horizonFalloff = EditorGUILayout.Slider("Horizon Falloff", _spec.horizonFalloff, 0.2f, 8f);
            _spec.stars = EditorGUILayout.Slider("Stars", _spec.stars, 0f, 1f);
            _spec.fog = EditorGUILayout.Toggle("Fog", _spec.fog);
            if (_spec.fog)
            {
                _spec.fogColor = EditorGUILayout.ColorField("Fog Color", _spec.fogColor);
                _spec.fogDensity = EditorGUILayout.Slider("Fog Density", _spec.fogDensity, 0f, 0.1f);
            }
            if (EditorGUI.EndChangeCheck() && _previewing) PushPreview();

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Presets", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Day")) _spec = SkySpec.Day;
                if (GUILayout.Button("Evening")) _spec = SkySpec.Evening;
                if (GUILayout.Button("Night")) _spec = SkySpec.Night;
            }

            EditorGUILayout.Space(8);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(_previewing ? "Stop Preview" : "Preview in Scene"))
                    TogglePreview();
                if (GUILayout.Button("Load JSON...")) LoadJson();
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Save Material...")) SaveMaterial();
                if (GUILayout.Button("Save JSON...")) SaveJson();
            }
            EditorGUILayout.EndScrollView();
        }

        void TogglePreview()
        {
            if (_previewing) StopPreview();
            else StartPreview();
        }

        void StartPreview()
        {
            var shader = AtmosShaders.Find(AtmosShaders.SkyGradient);
            if (shader == null)
            {
                EditorUtility.DisplayDialog("Atmos", "Atmos/SkyGradient shader not found.", "OK");
                return;
            }
            if (_preview == null) _preview = new Material(shader);
            _previewing = true;
            PushPreview();
        }

        void PushPreview()
        {
            _spec.ApplyTo(_preview);
            Sky.Apply(_preview, null, _spec);
        }

        void StopPreview()
        {
            if (_previewing) RenderSettings.fog = false;
            _previewing = false;
        }

        void SaveMaterial()
        {
            var path = EditorUtility.SaveFilePanelInProject("Save Sky Material", "Sky.mat", "mat", "Choose where to save the sky material.");
            if (string.IsNullOrEmpty(path)) return;
            var shader = AtmosShaders.Find(AtmosShaders.SkyGradient);
            if (shader == null) return;
            var mat = new Material(shader) { name = Path.GetFileNameWithoutExtension(path) };
            _spec.ApplyTo(mat);
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                EditorUtility.CopySerialized(mat, existing);
                EditorUtility.SetDirty(existing);
            }
            else AssetDatabase.CreateAsset(mat, path);
            AssetDatabase.SaveAssets();
        }

        void SaveJson()
        {
            var path = EditorUtility.SaveFilePanelInProject("Save Sky Spec JSON", "sky.json", "json", "Choose where to save the sky spec.");
            if (string.IsNullOrEmpty(path)) return;
            File.WriteAllText(path, _spec.ToJson());
            AssetDatabase.ImportAsset(path);
        }

        void LoadJson()
        {
            var path = EditorUtility.OpenFilePanel("Load Sky Spec JSON", Application.dataPath, "json");
            if (string.IsNullOrEmpty(path)) return;
            var spec = SkySpec.FromJson(File.ReadAllText(path));
            if (spec != null) _spec = spec;
        }
    }
}
