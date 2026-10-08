using QuietCamp.Infrastructure;
using UnityEngine;
using UnityEngine.UI;

namespace QuietCamp.Presentation
{
    /// <summary>Curved, folded leaves carried by a shared breeze. One UI mesh,
    /// no per-leaf objects, textures or frame allocations.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed partial class LeafCurtainGraphic : MaskableGraphic
    {
        public float Travel { get; private set; }
        float _elapsed;
        int _segments = 8;
        Color _tint = new Color(.35f, .45f, .38f);

        public void ConfigureQuality(int tier)
        {
            int segments = tier <= 0 ? 6 : tier == 1 ? 8 : 10;
            if (_segments == segments) return;
            _segments = segments;
            _gpuVertices = null;
            SetVerticesDirty();
        }

        // Linear phase time: 0–1 entry, 1 hold, 1–2 exit. The breeze clock
        // continues across loading so neither shape nor orientation jumps.
        public void SetFrame(float travel, Color tint, float elapsed = 0f)
        {
            using var audit = PerformanceAudit.Measure("QC.LeafCurtainGraphic.SetFrame");
            travel = Mathf.Clamp(travel, 0f, 2f);
            if (Travel == travel && _tint == tint && _elapsed == elapsed) return;
            bool visibilityChanged = (Travel > 0f && Travel < 2f) != (travel > 0f && travel < 2f);
            Travel = travel;
            _tint = tint;
            _elapsed = elapsed;
            if (UpdateGpuFrame())
            {
                if (visibilityChanged) SetVerticesDirty();
            }
            else SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            using var audit = PerformanceAudit.Measure("QC.LeafCurtainGraphic.OnPopulateMesh");
            vh.Clear();
            if (UpdateGpuFrame())
            {
                PopulateGpuMesh(vh);
                return;
            }
            if (Travel <= 0f || Travel >= 2f) return;
            var rect = rectTransform.rect;
            if (rect.width <= 0f || rect.height <= 0f) return;
            float unit = Mathf.Min(rect.width, rect.height) * .16f;
            // Keep leaves small while bounding the single uGUI mesh on ultrawide screens.
            // Shadows use fewer strips than the lit face; no extra Canvas or camera.
            while (LeafCount(rect, unit) > 720) unit *= 1.04f;
            float margin = unit * 1.8f;
            const float spacing = .72f;
            int rows = Mathf.CeilToInt((rect.height + 2 * margin) / (unit * spacing));
            int cols = Mathf.CeilToInt((rect.width + 2 * margin) / (unit * spacing));
            for (int layer = 0; layer < 2; layer++)
            for (int row = 0; row <= rows; row++)
            for (int col = 0; col <= cols; col++)
            {
                float seed = Hash(row, col, layer + 11);
                float variation = Hash(row, col, layer + 23);
                float depth = layer == 0 ? .88f : 1.08f;
                float px = rect.xMin - margin + col * unit * spacing
                    + ((row % 2) * .36f + layer * .24f + (seed - .5f) * .40f) * unit;
                float py = rect.yMin - margin + row * unit * spacing
                    + (layer * .38f + (variation - .5f) * .40f) * unit;
                float across = Mathf.InverseLerp(rect.xMin - margin, rect.xMax + margin, px);
                float enterDelay = .10f * across + .08f * seed + layer * .035f;
                float exitDelay = .10f * across + .18f * variation + layer * .06f;
                float incoming = Smooth((Travel - enterDelay) / (.70f + .06f * variation));
                float outgoing = Smooth((Travel - 1f - exitDelay) / (.70f + .04f * seed));
                float flight = 1f - incoming;
                float moving = flight + outgoing;
                float length = unit * (2.20f + variation * .48f);
                // Shared wind direction, with depth-dependent lift and drag.
                // Damped settling preserves zero velocity at both joins.
                float startX = rect.xMin - length * 1.4f - unit * seed;
                float endX = rect.xMax + length * 1.4f + unit * variation;
                float drift = (Hash(row, col, layer + 41) - .5f) * unit * 2.3f;
                float lift = Mathf.Sin(Mathf.PI * incoming) * flight
                    + Mathf.Sin(Mathf.PI * outgoing) * outgoing;
                float breeze = _elapsed * (1.15f + variation * .55f) + px / unit * .24f + py / unit * .18f;
                float flutter = breeze * (2.3f + seed) + seed * Mathf.PI * 2f;
                var centre = new Vector2(
                    Mathf.Lerp(startX, px, incoming) + (endX - px) * outgoing,
                    py + drift * moving + unit * (.45f + seed) * lift);
                centre += new Vector2(Mathf.Sin(breeze) * .045f,
                    Mathf.Cos(breeze * .83f + seed) * .065f) * unit * depth;
                float angle = -68f + seed * 136f
                    + (seed - .5f) * 42f * flight
                    + (variation - .5f) * 45f * outgoing
                    + Mathf.Sin(breeze + seed * 5f) * 5f
                    + Mathf.Sin(flutter) * 4f * moving;
                float scale = 1f - .16f * moving + .025f * Mathf.Sin(breeze + seed);
                float fold = .90f + .07f * Mathf.Sin(flutter)
                    - .18f * moving * Mathf.Abs(Mathf.Sin(flutter));
                float bend = .035f * Mathf.Sin(breeze + seed * 6f)
                    + .065f * moving * Mathf.Sin(flutter);
                // Muted facets, warm tips, and dark undersides follow the
                // low-poly world palette instead of a uniform paper fill.
                var shade = Shade(_tint, (layer == 0 ? .76f : .96f) + seed * .12f);
                // Occlusion shadow precedes each leaf, preserving layered depth.
                var shadowTint=Shade(shade,.45f);shadowTint.a=.24f;
                Leaf(vh, centre + new Vector2(unit*.022f,-unit*.035f),length*scale*1.025f,angle,
                    shadowTint,variation,fold,bend,3,true);
                Leaf(vh, centre, length * scale, angle, shade, variation, fold, bend, _segments);
            }
        }

        static int LeafCount(Rect rect, float unit)
        {
            float margin = unit * 1.8f, spacing = unit * .72f;
            return 2 * (Mathf.CeilToInt((rect.height + 2 * margin) / spacing) + 1)
                * (Mathf.CeilToInt((rect.width + 2 * margin) / spacing) + 1);
        }

        static float Smooth(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * t * (t * (t * 6f - 15f) + 10f);
        }

        static float Hash(int row, int col, int layer)
        {
            uint n = unchecked((uint)(row * 374761393 + col * 668265263 + layer * 144269));
            n = (n ^ (n >> 13)) * 1274126177u;
            return (n & 65535) / 65535f;
        }

        static Color Shade(Color c, float value)
            => new Color(c.r * value, c.g * value, c.b * value, c.a);

        static void Leaf(VertexHelper vh, Vector2 centre, float length, float degrees,
            Color color, float shape, float fold, float bend, int segments, bool shadow=false)
        {
            float a = degrees * Mathf.Deg2Rad;
            var axis = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            var normal = new Vector2(-axis.y, axis.x);
            int start = vh.currentVertCount;
            // Longitudinal strips let the midrib bow and the tip lag behind
            // the body; fold changes the projected width of each side.
            for (int i = 0; i <= segments; i++)
            {
                float t = i / (float)segments;
                float silhouette = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(Mathf.PI * t)), .82f + shape * .35f);
                float width = length * (.265f + shape * .04f) * silhouette;
                float curve = length * bend * Mathf.Sin(Mathf.PI * t) * (t + .2f);
                var rib = centre + axis * ((t - .5f) * length) + normal * curve;
                var warm = new Color(color.r * 1.06f, color.g * 1.025f, color.b * .91f, color.a);
                var face = Color.Lerp(color, warm, t * .65f);
                float light = shadow?1:.85f+.18f*silhouette;
                float underside=shadow?1:Mathf.Lerp(.65f,.91f,fold)*(.94f+.06f*Mathf.Cos(a));
                vh.AddVert(rib - normal * width * fold * (.92f + shape * .12f),
                    Shade(face, underside * light), Vector2.zero);
                vh.AddVert(rib, Shade(face, (shadow?1:1.10f) * light), Vector2.zero);
                vh.AddVert(rib + normal * width * fold,
                    Shade(face, .99f * light), Vector2.zero);
                if (i == 0) continue;
                int p = start + (i - 1) * 3;
                vh.AddTriangle(p, p + 3, p + 1);
                vh.AddTriangle(p + 1, p + 3, p + 4);
                vh.AddTriangle(p + 1, p + 4, p + 2);
                vh.AddTriangle(p + 2, p + 4, p + 5);
            }
            if (!shadow)
            {
                // A restrained midrib and side veins connect the facets, rather than
                // making each leaf read as two flat paper triangles.
                for (int v = 0; v < 3; v++)
                {
                    float t = .27f + v * .18f;
                    var rib = centre + axis * ((t - .5f) * length)
                        + normal * (length * bend * Mathf.Sin(Mathf.PI * t) * (t + .2f));
                    float width = length * (.265f + shape * .04f)
                        * Mathf.Pow(Mathf.Sin(Mathf.PI * t), .82f + shape * .35f) * fold;
                    Vein(vh, rib, rib + normal * width * .79f + axis * length * .09f,
                        length * .0018f, Shade(color, .90f));
                    Vein(vh, rib, rib - normal * width * .76f + axis * length * .09f,
                        length * .0018f, Shade(color, .78f));
                }
            }
            int stem = vh.currentVertCount;
            var root = centre - axis * length * .50f;
            var tip = centre - axis * length * .62f - normal * bend * length * .3f;
            var vein = Shade(color, .72f);
            var thickness = normal * length * .008f;
            vh.AddVert(root - thickness, vein, Vector2.zero);
            vh.AddVert(root + thickness, vein, Vector2.zero);
            vh.AddVert(tip + thickness * .45f, vein, Vector2.zero);
            vh.AddVert(tip - thickness * .45f, vein, Vector2.zero);
            vh.AddTriangle(stem, stem + 1, stem + 2);
            vh.AddTriangle(stem, stem + 2, stem + 3);
        }
        static void Vein(VertexHelper vh, Vector2 from, Vector2 to, float width, Color color)
        {
            var delta = to - from;
            var side = new Vector2(-delta.y, delta.x).normalized * width;
            int start = vh.currentVertCount;
            vh.AddVert(from - side, color, Vector2.zero);
            vh.AddVert(from + side, color, Vector2.zero);
            vh.AddVert(to + side * .35f, color, Vector2.zero);
            vh.AddVert(to - side * .35f, color, Vector2.zero);
            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start, start + 2, start + 3);
        }
    }
}
