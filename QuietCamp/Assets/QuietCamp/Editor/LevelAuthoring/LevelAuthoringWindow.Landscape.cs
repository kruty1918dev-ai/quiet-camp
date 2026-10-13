using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace QuietCamp.Editor.LevelAuthoring
{
    public sealed partial class LevelAuthoringWindow
    {
        bool _riverEditing;
        bool _stampEditing;
        int _selectedRiverPoint, _selectedStamp;
        void DrawLandscape()
        {
            GUILayout.Label("Річка й земля", EditorStyles.boldLabel);
            if (Document.terrain == null)
            {
                EditorGUILayout.HelpBox("Наявний шаблон збережено. Перейдіть до кривої, щоб пересувати точки русла та додавати пагорби. Об’єкти залишаться на сцені.", MessageType.Info);
                using (new EditorGUI.DisabledScope(RiverTerrainIntegration.Factory == null))
                    if (GUILayout.Button("Перейти до редагованої річки та землі", GUILayout.Height(38))) Try(() => Edit("Редагована річка", d => d.terrain = RiverTerrainAuthoringData.FromLegacy(d.art)));
                if (RiverTerrainIntegration.Factory == null) EditorGUILayout.HelpBox("River Modeler і MicroVerse ще не підключені.", MessageType.Warning);
            }
            else
            {
                var terrain = Document.terrain;
                EditorGUI.BeginChangeCheck();
                float width = EditorGUILayout.Slider(new GUIContent("Ширина води", "Ширина русла в метрах"), terrain.width, .3f, 8);
                float depth = EditorGUILayout.Slider("Глибина дна", terrain.depth, .1f, 3);
                float bank = EditorGUILayout.Slider("Ширина берега", terrain.bankWidth, .1f, 6);
                float softness = EditorGUILayout.Slider("М’якість переходу", terrain.bankSoftness, .05f, 4);
                if (EditorGUI.EndChangeCheck()) Edit("Форма берегів", d => { d.terrain.width = width; d.terrain.depth = depth; d.terrain.bankWidth = bank; d.terrain.bankSoftness = softness; });
                _riverEditing = EditorGUILayout.Toggle("Редагувати точки у Scene View", _riverEditing);
                _selectedRiverPoint = EditorGUILayout.Popup("Вибрана точка", Mathf.Clamp(_selectedRiverPoint, 0, terrain.points.Length - 1), terrain.points.Select((p, i) => "Точка " + (i + 1)).ToArray());
                var selected = terrain.points[_selectedRiverPoint];
                EditorGUI.BeginChangeCheck();
                var riverPoint = EditorGUILayout.Vector2Field("Положення точки X / Z", new Vector2(selected.x, selected.z));
                if (EditorGUI.EndChangeCheck()) Edit("Положення точки річки", d => { d.terrain.points[_selectedRiverPoint].x = riverPoint.x; d.terrain.points[_selectedRiverPoint].z = riverPoint.y; });
                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(terrain.points.Length >= 64)) if (GUILayout.Button("+ Точка")) Edit("Додати точку русла", d =>
                    {
                        int index = Mathf.Clamp(_selectedRiverPoint, 0, d.terrain.points.Length - 1); var a = d.terrain.points[index];
                        var b = d.terrain.points[Mathf.Min(index + 1, d.terrain.points.Length - 1)];
                        var points = d.terrain.points.ToList(); points.Insert(index + 1, new RiverPoint { x = (a.x + b.x) / 2, z = index + 1 == points.Count ? a.z + 3 : (a.z + b.z) / 2 }); d.terrain.points = points.ToArray();
                    });
                    using (new EditorGUI.DisabledScope(terrain.points.Length <= 2)) if (GUILayout.Button("− Точка")) Edit("Прибрати точку русла", d => d.terrain.points = d.terrain.points.Where((p, i) => i != _selectedRiverPoint).ToArray());
                }
                EditorGUILayout.HelpBox("Увімкніть точки й перетягніть їх у Scene View. Вода й береги оновляться після завершення руху. Поле захищене від річки.", MessageType.None);
                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(terrain.stamps.Length >= 100))
                    {
                        if (GUILayout.Button("+ Пагорб")) AddStamp(1);
                        if (GUILayout.Button("+ Низина")) AddStamp(-.5f);
                    }
                }
                if (terrain.stamps.Length > 0)
                {
                    _stampEditing = EditorGUILayout.Toggle("Переміщувати форми у Scene View", _stampEditing);
                    _selectedStamp = EditorGUILayout.Popup("Форма землі", Mathf.Clamp(_selectedStamp, 0, terrain.stamps.Length - 1), terrain.stamps.Select((s, i) => (s.height >= 0 ? "Пагорб " : "Низина ") + (i + 1)).ToArray());
                    var stamp = terrain.stamps[_selectedStamp];
                    EditorGUI.BeginChangeCheck(); var point = EditorGUILayout.Vector2Field("Положення X / Z", new Vector2(stamp.x, stamp.z));
                    float radius = EditorGUILayout.Slider("Розмір", stamp.radius, .25f, 12), stampHeight = EditorGUILayout.Slider("Висота", stamp.height, -4, 4);
                    if (EditorGUI.EndChangeCheck()) Edit("Форма землі", d => { var s = d.terrain.stamps[_selectedStamp]; s.x = point.x; s.z = point.y; s.radius = radius; s.height = stampHeight; });
                    if (GUILayout.Button("Прибрати вибрану форму")) Edit("Прибрати форму землі", d => d.terrain.stamps = d.terrain.stamps.Where((s, i) => i != _selectedStamp).ToArray());
                }
            }
            if (GUILayout.Button("Показати рівень з його камери")) _workspace.Frame();
            _advanced = EditorGUILayout.Foldout(_advanced, "Додатково: камера, сонце та старий шаблон", true);
            if (_advanced) DrawLightingAndLegacyRiver();
        }
        void AddStamp(float height) => Edit("Додати форму землі", d => d.terrain.stamps = d.terrain.stamps.Concat(new[]
            { new LandscapeStamp { x = d.level.width / 2f + 3, z = -d.level.height / 2f - 2, height = height } }).ToArray());
        bool DrawRiverHandles()
        {
            if ((!_riverEditing && !_stampEditing) || Document.terrain == null || Event.current.alt) return false;
            if (_stampEditing)
                for (int i = 0; i < Document.terrain.stamps.Length; i++)
                {
                    int index = i; var stamp = Document.terrain.stamps[i];
                    var point = new Vector3(stamp.x, _workspace.River?.SampleHeight(stamp.x, stamp.z) ?? 0, stamp.z);
                    Handles.color = stamp.height >= 0 ? Color.green : Color.yellow;
                    Handles.DrawWireDisc(point, Vector3.up, stamp.radius);
                    EditorGUI.BeginChangeCheck(); var moved = Handles.PositionHandle(point, Quaternion.identity);
                    if (EditorGUI.EndChangeCheck()) Edit("Перемістити форму землі", d => { d.terrain.stamps[index].x = moved.x; d.terrain.stamps[index].z = moved.z; });
                }
            if (!_riverEditing) return true;
            Handles.color = new Color(.2f, .75f, 1);
            var points = Document.terrain.points;
            Handles.DrawAAPolyLine(4, points.Select(p => new Vector3(p.x, .15f, p.z)).ToArray());
            for (int i = 0; i < points.Length; i++)
            {
                int index = i; var point = new Vector3(points[i].x, .15f, points[i].z);
                Handles.Label(point + Vector3.up * .35f, "Точка " + (i + 1));
                EditorGUI.BeginChangeCheck(); var moved = Handles.FreeMoveHandle(point, HandleUtility.GetHandleSize(point) * .08f, Vector3.zero, Handles.SphereHandleCap);
                if (EditorGUI.EndChangeCheck()) Edit("Пересунути русло", d => { d.terrain.points[index].x = moved.x; d.terrain.points[index].z = moved.z; });
            }
            if (Event.current.type == EventType.MouseUp) _applyAt = EditorApplication.timeSinceStartup;
            return true;
        }
    }
}
