using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace QuietCamp.Presentation
{
    public sealed partial class LeafCurtainGraphic
    {
        Material _flightMaterial;
        List<UIVertex> _gpuVertices;
        List<int> _gpuIndices;
        Rect _gpuRect;
        static readonly int TravelId = Shader.PropertyToID("_Travel");
        static readonly int ElapsedId = Shader.PropertyToID("_BreezeTime");
        static readonly int TintId = Shader.PropertyToID("_LeafTint");

        // One immutable mesh per size/tier. Flight and folding are vertex-shader
        // parameters: loading no longer rebuilds/uploads 55k vertices every frame.
        // Keep the CPU path for unsupported shaders and canvas-free geometry tests.
        bool UpdateGpuFrame()
        {
            if (!UnityEngine.Application.isPlaying || canvas == null) return false;
            if (_flightMaterial == null)
            {
                var shader = Resources.Load<Shader>("QuietCamp/LeafFlight");
                if (shader == null || !shader.isSupported) return false;
                _flightMaterial = new Material(shader) { name = "Leaf flight (owned)" };
                material = _flightMaterial;
                canvas.additionalShaderChannels |= AdditionalCanvasShaderChannels.TexCoord1 | AdditionalCanvasShaderChannels.TexCoord2 | AdditionalCanvasShaderChannels.TexCoord3;
                SetVerticesDirty();
            }
            SetFlightParameters(_flightMaterial);
            var masked = materialForRendering;
            if (masked != _flightMaterial) SetFlightParameters(masked);
            return true;
        }
        void SetFlightParameters(Material target)
        {
            if (target == null) return;
            target.SetFloat(TravelId, Travel);
            target.SetFloat(ElapsedId, _elapsed);
            target.SetColor(TintId, _tint);
        }
        void PopulateGpuMesh(VertexHelper output)
        {
            if (Travel <= 0f || Travel >= 2f) return;
            var rect = rectTransform.rect;
            if (rect.width <= 0 || rect.height <= 0) return;
            if (_gpuVertices == null || _gpuRect != rect)
            {
                _gpuRect = rect;
                _gpuVertices = new List<UIVertex>(56000);
                _gpuIndices = new List<int>(100000);
                float unit = Mathf.Min(rect.width, rect.height) * .16f;
                while (LeafCount(rect, unit) > 720) unit *= 1.04f;
                float margin = unit * 1.8f;
                int rows = Mathf.CeilToInt((rect.height + 2 * margin) / (unit * .72f));
                int cols = Mathf.CeilToInt((rect.width + 2 * margin) / (unit * .72f));
                using var template = new VertexHelper();
                using var mesh = new MeshLease();
                var topology = new Dictionary<int, int[]>();
                for (int layer = 0; layer < 2; layer++)
                for (int row = 0; row <= rows; row++)
                for (int col = 0; col <= cols; col++)
                {
                    float seed = Hash(row, col, layer + 11), shape = Hash(row, col, layer + 23);
                    float px = rect.xMin - margin + col * unit * .72f
                        + ((row % 2) * .36f + layer * .24f + (seed - .5f) * .40f) * unit;
                    float py = rect.yMin - margin + row * unit * .72f
                        + (layer * .38f + (shape - .5f) * .40f) * unit;
                    for (int shadow = 1; shadow >= 0; shadow--)
                    {
                        int segments = shadow == 1 ? 3 : _segments;
                        var shade = Shade(new Color(.5f, .5f, .5f, 1), (layer == 0 ? .76f : .96f) + seed * .12f);
                        if (shadow == 1) { shade = Shade(shade, .45f); shade.a = .24f; }
                        template.Clear();
                        Leaf(template, Vector2.zero, 1, 0, shade, shape, 1, 0, segments, shadow == 1);
                        int start = _gpuVertices.Count;
                        for (int i = 0; i < template.currentVertCount; i++)
                        {
                            var v = UIVertex.simpleVert; template.PopulateUIVertex(ref v, i);
                            int strips = (segments + 1) * 3;
                            float curve = 0;
                            bool underside = shadow == 0 && i < strips && i % 3 == 0;
                            if (i < strips)
                            {
                                float t = (i / 3) / (float)segments;
                                curve = Mathf.Sin(Mathf.PI * t) * (t + .2f);
                            }
                            else if (shadow == 0 && i < strips + 24)
                            {
                                float t = .27f + ((i - strips) / 8) * .18f;
                                curve = Mathf.Sin(Mathf.PI * t) * (t + .2f);
                            }
                            else if (i >= template.currentVertCount - 2) curve = -.3f;
                            v.uv3 = new Vector4(Hash(row, col, layer + 41) - .5f, 0, 0, 0);
                            v.uv0 = new Vector4(px, py, seed, shape);
                            v.uv1 = new Vector4(unit, layer, curve, underside ? 1 : 0);
                            v.uv2 = new Vector4(shadow == 1 ? 1.025f : 1, shadow, rect.xMin, rect.xMax);
                            _gpuVertices.Add(v);
                        }
                        // Retain indexed vertices, rather than a triangle stream which
                        // would exceed uGUI's 65k budget and duplicate every facet.
                        if (!topology.TryGetValue(segments * 2 + shadow, out var triangles))
                        {
                            template.FillMesh(mesh.Value);
                            triangles = mesh.Value.triangles;
                            topology.Add(segments * 2 + shadow, triangles);
                        }
                        foreach (int index in triangles) _gpuIndices.Add(start + index);
                    }
                }
            }
            output.AddUIVertexStream(_gpuVertices, _gpuIndices);
        }
        sealed class MeshLease : System.IDisposable
        {
            public readonly Mesh Value = new Mesh();
            public void Dispose() => Destroy(Value);
        }
        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (_flightMaterial != null) Destroy(_flightMaterial);
        }
    }
}
