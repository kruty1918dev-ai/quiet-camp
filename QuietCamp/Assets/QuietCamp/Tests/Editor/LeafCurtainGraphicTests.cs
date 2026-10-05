using System.Reflection;
using NUnit.Framework;
using QuietCamp.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace QuietCamp.Tests
{
    public class LeafCurtainGraphicTests
    {
        [TestCase(1080, 1920)]
        [TestCase(1920, 1080)]
        [TestCase(1536, 2048)]
        [TestCase(720, 1600)]
        [TestCase(3440, 1440, 3f, 2)]
        [TestCase(1080, 1920, 0.85f)]
        [TestCase(1080, 1920, 2.7f)]
        [TestCase(1920, 1080, 15f)]
        [TestCase(720, 1600, 7.3f, 0)]
        [TestCase(1080, 1920, 11.4f, 2)]
        public void LoadingFrame_IsOpaqueAcrossEntireViewport(int width, int height, float elapsed = 0f, int tier = 1)
        {
            var go = new GameObject("Canopy", typeof(RectTransform), typeof(LeafCurtainGraphic));
            var mesh = new Mesh();
            try
            {
                go.GetComponent<RectTransform>().sizeDelta = new Vector2(width, height);
                var graphic = go.GetComponent<LeafCurtainGraphic>();
                graphic.ConfigureQuality(tier);
                graphic.SetFrame(1f, new Color(.35f, .45f, .38f), elapsed);
                using (var helper = new VertexHelper())
                {
                    typeof(LeafCurtainGraphic).GetMethod("OnPopulateMesh",
                        BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly).Invoke(graphic, new object[] { helper });
                    helper.FillMesh(mesh);
                }
                Assert.Less(mesh.vertexCount, 65000, "Must fit uGUI's mesh vertex budget.");
                var vertices = mesh.vertices;
                foreach (var vertex in vertices)
                    Assert.IsTrue(float.IsFinite(vertex.x) && float.IsFinite(vertex.y),
                        "Leaf tips and folds must never produce invalid geometry.");
                var triangles = mesh.triangles;
                // Sample corners, edges and interior: no loading-scene pinholes.
                for (int y = 0; y <= 16; y++)
                for (int x = 0; x <= 16; x++)
                {
                    var p = new Vector2((x / 16f - .5f) * width, (y / 16f - .5f) * height);
                    bool covered = false;
                    for (int i = 0; i < triangles.Length && !covered; i += 3)
                        covered = Inside(p, vertices[triangles[i]], vertices[triangles[i + 1]], vertices[triangles[i + 2]]);
                    Assert.IsTrue(covered, $"Uncovered point {p} at {width}x{height}");
                }
                Assert.IsTrue(System.Array.Exists(mesh.colors32,c=>c.a==255),"Leaf faces stay opaque under full coverage.");
                Assert.IsTrue(System.Array.Exists(mesh.colors32,c=>c.a<255),"Overlap shadows use gentle transparency.");
                foreach (float endpoint in new[] { 0f, 2f })
                {
                    graphic.SetFrame(endpoint, Color.green);
                    using (var helper = new VertexHelper())
                    {
                        typeof(LeafCurtainGraphic).GetMethod("OnPopulateMesh",
                            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly).Invoke(graphic, new object[] { helper });
                        Assert.AreEqual(0, helper.currentVertCount, "Endpoint must leave the scene unobstructed.");
                    }
                }
            }
            finally { Object.DestroyImmediate(mesh); Object.DestroyImmediate(go); }
        }

        [Test]
        public void Leaves_MoveIndependently_AndKeepMovingWhileCovered()
        {
            var go = new GameObject("Canopy", typeof(RectTransform), typeof(LeafCurtainGraphic));
            var mesh = new Mesh();
            try
            {
                go.GetComponent<RectTransform>().sizeDelta = new Vector2(1080, 1920);
                var graphic = go.GetComponent<LeafCurtainGraphic>();
                Vector3[] Frame(float travel, float elapsed)
                {
                    graphic.SetFrame(travel, Color.green, elapsed);
                    using (var helper = new VertexHelper())
                    {
                        typeof(LeafCurtainGraphic).GetMethod("OnPopulateMesh",
                            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                            .Invoke(graphic, new object[] { helper });
                        helper.FillMesh(mesh);
                    }
                    return mesh.vertices;
                }
                foreach (float phase in new[] { .4f, 1.4f })
                {
                    var a = Frame(phase, 0f);
                    var b = Frame(phase + .05f, .05f);
                    var firstDelta = b[0] - a[0];
                    int different = 0;
                    for (int i = 1; i < a.Length; i++)
                        if (((b[i] - a[i]) - firstDelta).sqrMagnitude > 1f) different++;
                    Assert.Greater(different, a.Length / 2, "Leaves must not translate as one rigid panel.");
                }
                var held = Frame(1f, 1f);
                var later = Frame(1f, 2f);
                Assert.Greater((held[0] - later[0]).sqrMagnitude, .1f,
                    "Covered leaves should keep swaying during a slow load.");
                // Adjacent outer/rib vertices change separation as the leaf
                // folds, so the animation deforms its body as well as moving it.
                int deforming = 0;
                const int shadowVertices = 16; // Four shadow sections and petiole.
                const int verticesPerLeaf = shadowVertices + 31 + 24; // Lit strips, petiole, six veins.
                for (int i = shadowVertices + 12; i + 1 < held.Length; i += verticesPerLeaf)
                    if (Mathf.Abs(Vector3.Distance(held[i], held[i + 1])
                        - Vector3.Distance(later[i], later[i + 1])) > .1f) deforming++;
                Assert.Greater(deforming, held.Length / verticesPerLeaf / 2,
                    "Most leaves must flex, rather than behaving as rigid cutouts.");
                var beforeJoin = Frame(.9999f, 3f);
                var afterJoin = Frame(1.0001f, 3f);
                for (int i = 0; i < beforeJoin.Length; i++)
                    Assert.Less(Vector3.Distance(beforeJoin[i], afterJoin[i]), .1f,
                        "Loading/reveal joins must not reset the wind or leaf pose.");
            }
            finally { Object.DestroyImmediate(mesh); Object.DestroyImmediate(go); }
        }

        static bool Inside(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float Cross(Vector2 u, Vector2 v) => u.x * v.y - u.y * v.x;
            if (Mathf.Abs(Cross(b - a, c - a)) < .001f) return false;
            float s = Cross(b - a, p - a), t = Cross(c - b, p - b), u = Cross(a - c, p - c);
            return (s >= 0 && t >= 0 && u >= 0) || (s <= 0 && t <= 0 && u <= 0);
        }
    }
}
