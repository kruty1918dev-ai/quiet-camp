using QuietCamp.Infrastructure;
using UnityEngine;
using UnityEngine.UI;

namespace QuietCamp.Presentation.UI
{
    /// <summary>A visible scene slot with one owned, bounded mesh. Scrolling
    /// reuses its projection; scene, layout, quality or weather invalidate it.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class RoadmapGladeGraphic : MaskableGraphic
    {
        RoadmapGraphic _map;
        int _index = -1, _cachedIndex = -1, _cachedTier = -1, _weatherRevision = -1;
        Rect _cachedRect;
        Mesh _mesh;
        public int SceneIndex => _index;
        public int Models { get; internal set; }
        public int Shadows { get; internal set; }
        public int Truncated { get; internal set; }
        public void Configure(RoadmapGraphic map, int index)
        {
            if (_map == map && _index == index) return;
            _map = map; _index = index; SetVerticesDirty();
        }
        public void InvalidateWeather() => _weatherRevision = -1;
        protected override void UpdateGeometry()
        {
            using var audit = PerformanceAudit.Measure("QC.RoadmapGladeGraphic.OnPopulateMesh");
            if (_map == null || _index < 0) { canvasRenderer.Clear(); return; }
            if (_mesh == null) _mesh = new Mesh { name = "Cached roadmap glade" };
            if (_cachedIndex != _index || _cachedTier != _map.QualityTier
                || _cachedRect != _map.rectTransform.rect || _weatherRevision != _map.WeatherRevision)
            {
                using var helper = new VertexHelper();
                _map.PaintGlade(helper, _index, this); helper.FillMesh(_mesh);
                _cachedIndex = _index; _cachedTier = _map.QualityTier;
                _cachedRect = _map.rectTransform.rect; _weatherRevision = _map.WeatherRevision;
            }
            // Layout marks the tall map RectTransform dirty while scrolling.
            // Submit the owned mesh directly; do not copy through UIVertex lists
            // and the shared uGUI worker mesh when its geometry is unchanged.
            canvasRenderer.SetMesh(_mesh);
        }
        protected override void OnPopulateMesh(VertexHelper helper)
        {
            helper.Clear(); if (_map != null && _index >= 0) _map.PaintGlade(helper, _index, this);
        }
        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (_mesh != null)
            {
                if (UnityEngine.Application.isPlaying) Destroy(_mesh); else DestroyImmediate(_mesh);
            }
        }
    }
}
