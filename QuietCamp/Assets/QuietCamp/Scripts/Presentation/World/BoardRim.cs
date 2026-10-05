using System.Collections.Generic;
using QuietCamp.Domain;
using UnityEngine;
using UnityEngine.Rendering;

namespace QuietCamp.Presentation.World
{
    /// <summary>A small lit, bevelled meadow edge. No collision or logical occupancy.</summary>
    public sealed class BoardRim : MonoBehaviour
    {
        const float Inner = .008f, Ridge = .032f, Outer = .067f;
        Mesh _mesh;
        Material _material;
        public static BoardRim Create(LevelData level, Transform parent)
        {
            var go = new GameObject("BoardBoundary", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false); go.layer = BoardRenderer.DecorLayer;
            var rim = go.AddComponent<BoardRim>(); rim.Build(level); return rim;
        }
        void Build(LevelData level)
        {
            var vertices = new List<Vector3>(); var triangles = new List<int>();
            for (int side = 0; side < 4; side++)
            {
                float length = side % 2 == 0 ? level.width : level.height;
                var gaps = new List<Vector2>();
                foreach (var access in CampAccess.Points(level))
                {
                    var direction = CampTrail.Outward(level, access);
                    int accessSide = direction.z < -.5f ? 0 : direction.x > .5f ? 1 : direction.z > .5f ? 2 : 3;
                    if (accessSide != side) continue;
                    float centre = side == 0 ? access.X + .5f : side == 1 ? access.Z + .5f
                        : side == 2 ? length - access.X - .5f : length - access.Z - .5f;
                    gaps.Add(new Vector2(Mathf.Max(0, centre - .44f), Mathf.Min(length, centre + .44f)));
                }
                gaps.Sort((a,b) => a.x.CompareTo(b.x));
                float cursor = 0;
                foreach (var gap in gaps)
                {
                    if (gap.x > cursor) Segment(side, cursor / length, gap.x / length);
                    cursor = Mathf.Max(cursor, gap.y);
                }
                if (cursor < length) Segment(side, cursor / length, 1);
            }
            _mesh = new Mesh { name = "Clearing bevelled edge" };
            _mesh.SetVertices(vertices); _mesh.SetTriangles(triangles, 0);
            _mesh.RecalculateNormals(); _mesh.RecalculateBounds();
            GetComponent<MeshFilter>().sharedMesh = _mesh;
            _material = new Material(Shader.Find("Universal Render Pipeline/Simple Lit"));
            var palette=SeasonPalette.For(level);
            _material.SetColor("_BaseColor",Color.Lerp(palette.GrassLight,palette.Soil,palette.SnowCoverage>.5f?.10f:.42f));
            var renderer = GetComponent<MeshRenderer>(); renderer.sharedMaterial = _material;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = true;

            void Segment(int side, float from, float to)
            {
                int first = vertices.Count;
                vertices.Add(Point(side, from, Inner, .016f)); vertices.Add(Point(side, to, Inner, .016f));
                vertices.Add(Point(side, from, Ridge, .027f)); vertices.Add(Point(side, to, Ridge, .027f));
                vertices.Add(Point(side, from, Outer, .003f)); vertices.Add(Point(side, to, Outer, .003f));
                foreach (var index in new[] { 0,1,2, 1,3,2, 2,3,4, 3,5,4 }) triangles.Add(first + index);
            }
            Vector3 Point(int side, float t, float margin, float y)
            {
                float x = level.width * .5f + margin, z = level.height * .5f + margin;
                var start = side == 0 ? new Vector3(-x,y,-z) : side == 1 ? new Vector3(x,y,-z)
                    : side == 2 ? new Vector3(x,y,z) : new Vector3(-x,y,z);
                var end = side == 0 ? new Vector3(x,y,-z) : side == 1 ? new Vector3(x,y,z)
                    : side == 2 ? new Vector3(-x,y,z) : new Vector3(-x,y,-z);
                return Vector3.Lerp(start, end, t);
            }
        }
        void OnDestroy()
        {
            if (_mesh != null) Destroy(_mesh);
            if (_material != null) Destroy(_material);
        }
    }
}
