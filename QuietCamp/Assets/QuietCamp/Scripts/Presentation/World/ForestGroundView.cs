using UnityEngine;

namespace QuietCamp.Presentation.World
{
    /// <summary>Finite camera frustum clipped to the forest's local ground slab.
    /// Corner rays alone miss visible ground when the horizon enters the view.</summary>
    public static class ForestGroundView
    {
        public const float MinimumHeight = -.3f, MaximumHeight = 1.5f;
        static readonly int[] Edges = { 0,1, 1,3, 3,2, 2,0, 4,5, 5,7, 7,6, 6,4, 0,4, 1,5, 2,6, 3,7 };

        public static bool TryBounds(Camera camera, Transform ground, Vector3[] corners, out Rect footprint,float maximumHeight=MaximumHeight)
        {
            var min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            var max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            void Include(Vector3 p)
            {
                min = Vector2.Min(min, new Vector2(p.x, p.z));
                max = Vector2.Max(max, new Vector2(p.x, p.z));
            }
            for (int i = 0; i < 8; i++)
            {
                var p = camera.ViewportToWorldPoint(new Vector3(i & 1, (i >> 1) & 1,
                    i < 4 ? camera.nearClipPlane : camera.farClipPlane));
                corners[i] = ground.InverseTransformPoint(p);
                if (corners[i].y >= MinimumHeight && corners[i].y <= maximumHeight) Include(corners[i]);
            }
            for (int i = 0; i < Edges.Length; i += 2)
            {
                var a = corners[Edges[i]]; var b = corners[Edges[i + 1]];
                if (Mathf.Abs(b.y - a.y) < .00001f) continue;
                for (int plane = 0; plane < 2; plane++)
                {
                    float y = plane == 0 ? MinimumHeight : maximumHeight;
                    float t = (y - a.y) / (b.y - a.y);
                    if (t >= 0 && t <= 1) Include(Vector3.LerpUnclamped(a, b, t));
                }
            }
            footprint = float.IsInfinity(min.x) ? default : Rect.MinMaxRect(min.x, min.y, max.x, max.y);
            return !float.IsInfinity(min.x);
        }

        public static Bounds TileBounds(Transform ground, Vector2Int key, float size,float maximumHeight=MaximumHeight)
        {
            float middle=(MinimumHeight+maximumHeight)*.5f;
            var centre = ground.TransformPoint(new Vector3((key.x + .5f) * size, middle, (key.y + .5f) * size));
            // Clump jitter, broad leaves and wind can cross a tile boundary.
            var x = ground.TransformVector(Vector3.right * (size * .5f + 2f));
            var y = ground.TransformVector(Vector3.up * (maximumHeight-MinimumHeight)*.5f);
            var z = ground.TransformVector(Vector3.forward * (size * .5f + 2f));
            var extents = new Vector3(Mathf.Abs(x.x) + Mathf.Abs(y.x) + Mathf.Abs(z.x),
                Mathf.Abs(x.y) + Mathf.Abs(y.y) + Mathf.Abs(z.y), Mathf.Abs(x.z) + Mathf.Abs(y.z) + Mathf.Abs(z.z));
            return new Bounds(centre, extents * 2);
        }
    }
}
