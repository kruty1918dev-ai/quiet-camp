using System;
using QuietCamp.Infrastructure;
using UnityEditor;
using UnityEngine;

namespace QuietCamp.Editor.LevelAuthoring
{
    public sealed partial class LevelAuthoringWindow
    {
        [SerializeField] int _propPack;
        [SerializeField] bool _showPropParts;
        bool _placingProp, _propWaterMode;
        float _propYaw, _propScale = 1, _propWaterOffset;
        static readonly string[] PropPacks = { "Усі", "Табір", "Сільський двір", "Берег", "Старі споруди" };
        // The current legacy river and Terrain adapter both author their surface at this height.
        const float AuthoringWaterSurface = -.075f;

        void DrawPropFilters()
        {
            if (Categories[_category] == "Пропси") _propPack = EditorGUILayout.Popup("Набір пропсів", _propPack, PropPacks);
            if (Categories[_category] == "Великі об’єкти") _showPropParts = EditorGUILayout.Toggle("Показати окремі частини", _showPropParts);
        }
        bool PropMatchesFilter(AuthoringPaletteEntry entry)
        {
            if (!string.IsNullOrWhiteSpace(_search)) return true;
            if (Categories[_category] == "Пропси" && _propPack > 0) return entry.prop?.pack == PropPacks[_propPack];
            return Categories[_category] != "Великі об’єкти" || _showPropParts || entry.prop?.part != true;
        }
        void SelectPaletteEntry(AuthoringPaletteEntry entry)
        {
            PlacementToolIntegration.Adapter?.Stop(); _placingProp = false; _fieldTool = FieldTool.None; _asset = entry.id;
            _propScale = entry.prop?.recommendedScale ?? 1; _propYaw = 0; _propWaterOffset = 0;
            _propWaterMode = entry.id == "qc_rowboat" || entry.id == "qc_watermill_wheel";
        }
        void DrawPropSettings(AuthoringPaletteEntry entry)
        {
            if (entry.prop == null) return;
            EditorGUILayout.HelpBox(entry.prop.description, MessageType.None);
            _propYaw = EditorGUILayout.Slider("Поворот", _propYaw, -180, 180);
            _propScale = EditorGUILayout.Slider("Масштаб", _propScale, .25f, 2);
            if (entry.prop.waterRelative)
            {
                _propWaterMode = EditorGUILayout.Toggle("Висота від поверхні води", _propWaterMode);
                if (_propWaterMode) _propWaterOffset = EditorGUILayout.FloatField("Зміщення від води, м", _propWaterOffset);
            }
            if (GUILayout.Button(_placingProp ? "Розміщення активне — клацніть у Scene View" : "Поставити об’єкт у Scene View")) BeginPropPlacement();
            if (entry.prop.SingleOnly) EditorGUILayout.HelpBox("Великий об’єкт ставиться по одному. Контур має повністю залишатися поза полем. Esc завершує інструмент.", MessageType.Info);
            var selected = Selection.activeGameObject?.GetComponent<LevelAuthoringAsset>();
            if (entry.prop.waterRelative && selected != null && selected.stableId == entry.id && selected.transform.IsChildOf(_workspace.Scenery))
            {
                float offset = selected.transform.localPosition.y - AuthoringWaterSurface;
                EditorGUI.BeginChangeCheck();
                offset = EditorGUILayout.FloatField("Вибраний об’єкт: висота від води", offset);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObjects(new UnityEngine.Object[] { selected.transform, selected }, "Висота декору від води");
                    var p = selected.transform.localPosition; p.y = AuthoringWaterSurface + offset;
                    selected.transform.localPosition = p; selected.surfaceAttached = false; _dirty = true;
                }
            }
        }
        void BeginPropPlacement()
        {
            PlacementToolIntegration.Adapter?.Stop(); _fieldTool = FieldTool.None; _placingProp = true; SceneView.RepaintAll();
        }
        bool DrawPropPlacement()
        {
            if (!_placingProp || Event.current.alt) return false;
            var e = Event.current;
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape) { _placingProp = false; e.Use(); return true; }
            var entry = LevelAuthoringPalette.Get(_asset); if (entry.prop == null) { _placingProp = false; return false; }
            HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
            var ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            Vector3 point;
            var surface = _workspace.Surface?.GetComponent<Collider>();
            if (!_propWaterMode && surface != null && surface.Raycast(ray, out var hit, 10000)) point = _workspace.Root.transform.InverseTransformPoint(hit.point);
            else
            {
                if (!new Plane(Vector3.up, new Vector3(0, _propWaterMode ? AuthoringWaterSurface : 0, 0)).Raycast(ray, out float distance)) return true;
                point = _workspace.Root.transform.InverseTransformPoint(ray.GetPoint(distance));
                point.y = _propWaterMode ? AuthoringWaterSurface + _propWaterOffset : Document.art.GroundHeight(point.x, point.z);
            }
            var pose = Matrix4x4.TRS(point, Quaternion.Euler(0, _propYaw, 0), Vector3.one * _propScale);
            bool blocked = entry.prop.SingleOnly && AuthoringPropCatalog.OverlapsBoard(entry.prop, pose, Document.level.width, Document.level.height);
            bool hidden = AuthoringPropCatalog.OccludesBoard(entry.prop, _workspace.Root.transform.localToWorldMatrix * pose, _workspace.Camera, Document.level.width, Document.level.height);
            DrawFootprint(entry.prop, pose, blocked ? Color.red : hidden ? new Color(1,.65f,.1f) : new Color(.3f,.9f,.7f));
            Handles.Label(_workspace.Root.transform.TransformPoint(point), blocked ? "Перетинає поле — пересуньте об’єкт" : hidden ? "Може закривати клітини з ігрової камери" : entry.label + " · клацніть для розміщення");
            if (e.type == EventType.MouseDown && e.button == 0)
            {
                if (blocked) _message = "Контур об’єкта перетинає поле. Пересуньте його далі.";
                else Try(() =>
                {
                    Undo.IncrementCurrentGroup(); Undo.SetCurrentGroupName("Поставити " + entry.label);
                    _workspace.Spawn(new CampArtDefinition.Instance { id = "authored/" + Guid.NewGuid().ToString("N"), modelId = entry.id,
                        x = point.x, y = point.y, z = point.z, yaw = _propYaw, scale = _propScale,
                        surfaceAttached = !_propWaterMode, surfaceOffset = point.y - Document.art.GroundHeight(point.x, point.z) }, true);
                    _dirty = true; _message = hidden ? "Об’єкт додано. Перевірте, чи він не закриває клітини в ігровій камері." : "Об’єкт додано. Esc — завершити.";
                });
                e.Use();
            }
            if (e.type == EventType.MouseMove) SceneView.RepaintAll();
            return true;
        }
        void DrawSelectedPropFootprint()
        {
            var marker = Selection.activeGameObject?.GetComponent<LevelAuthoringAsset>();
            if (marker == null || !marker.transform.IsChildOf(_workspace.Scenery)) return;
            var prop = AuthoringPropCatalog.Find(marker.stableId); if (prop == null) return;
            var pose = _workspace.Root.transform.worldToLocalMatrix * marker.transform.localToWorldMatrix;
            bool blocked = prop.SingleOnly && AuthoringPropCatalog.OverlapsBoard(prop, pose, Document.level.width, Document.level.height);
            bool hidden = AuthoringPropCatalog.OccludesBoard(prop, marker.transform.localToWorldMatrix, _workspace.Camera, Document.level.width, Document.level.height);
            DrawFootprint(prop, pose, blocked ? Color.red : hidden ? new Color(1,.65f,.1f) : new Color(.3f,.9f,.7f));
            if (blocked || hidden) Handles.Label(marker.transform.position, blocked ? "Перетинає поле — експорт заблокований" : "Може закривати клітини з ігрової камери");
        }
        void DrawFootprint(AuthoringPropDescriptor prop, Matrix4x4 pose, Color color)
        {
            var corners = AuthoringPropCatalog.Corners(prop, _workspace.Root.transform.localToWorldMatrix * pose);
            Handles.color = color; Handles.DrawAAPolyLine(3, corners[0], corners[1], corners[5], corners[4], corners[0]);
        }
    }
}
