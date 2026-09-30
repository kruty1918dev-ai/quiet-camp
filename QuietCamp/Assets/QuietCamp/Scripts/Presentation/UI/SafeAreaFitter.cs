using UnityEngine;
namespace QuietCamp.Presentation.UI
{
    /// <summary>
    /// Applies Screen.safeArea to the attached RectTransform every time the
    /// inset or resolution changes: anchors move to the cutout-free rect and
    /// a small additional padding keeps interactive content off the bezel.
    /// Backgrounds stay outside this rect (full-bleed); every interactive
    /// screen lives inside it. Attached by scene hosts to CanvasRoot/SafeArea.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class SafeAreaFitter : MonoBehaviour
    {
        /// <summary>Extra padding beyond the system inset: x=left, y=bottom,
        /// z=right, w=top, in reference pixels.</summary>
        public Vector4 Padding = new Vector4(16f, 12f, 16f, 12f);

        RectTransform _rt;
        Rect _lastInsets = new Rect(-1f, -1f, 0f, 0f);
        Vector2 _lastResolution;

        void Awake()
        {
            _rt = (RectTransform)transform;
            Apply(true);
        }

        void Update()
        {
            if (_lastInsets != Screen.safeArea
                || _lastResolution.x != Screen.width
                || _lastResolution.y != Screen.height)
            {
                Apply(true);
            }
        }

        /// <summary>Recomputes anchors now; call after scene/UI rebuilds.</summary>
        public void Apply(bool force = false)
        {
            if (_rt == null) _rt = (RectTransform)transform;
            var safe = Screen.safeArea;
            var w = Screen.width;
            var h = Screen.height;
            if (w <= 0 || h <= 0 || safe.width <= 0f || safe.height <= 0f) return;
            _lastInsets = safe;
            _lastResolution = new Vector2(w, h);
            _rt.anchorMin = new Vector2(safe.xMin / w, safe.yMin / h);
            _rt.anchorMax = new Vector2(safe.xMax / w, safe.yMax / h);
            _rt.offsetMin = new Vector2(Padding.x, Padding.y);
            _rt.offsetMax = new Vector2(-Padding.z, -Padding.w);
        }
    }
}
