using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using QuietCamp.Domain;
using QuietCamp.Infrastructure;
using QuietCamp.Presentation.World;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace QuietCamp.Tests
{
    public sealed class ExteriorTrailRenderingPlayModeTests
    {
        static LevelData Level() => new LevelData
        {
            id = "trail-rendering", schemaVersion = 1, ruleVersion = 2,
            width = 6, height = 5, decorSeed = 19, entry = new[] { 0, 2 },
            exteriorWalkable = new[] { new[] { -1, 1 }, new[] { -1, 2 }, new[] { -1, 3 } },
            blocked = new[] { new[] { 0, 1 } }, shade = Array.Empty<int[]>(), noise = Array.Empty<int[]>(),
            guests = Array.Empty<GuestData>(), friends = Array.Empty<string[]>(), witness = Array.Empty<Placement>()
        };

        [UnityTest]
        public IEnumerator AuthoredCellsBlendOnceAndScenicContinuationDoesNotDoubleTheSoil()
        {
            var level = Level();
            var root = new GameObject("authored-trail-test");
            int cached = RainSurface.CachedMeshCount;
            CampTrail.BuildAll(level, root.transform);
            try
            {
                var authored = root.transform.Find("AuthoredExteriorTrail");
                var mesh = authored.GetComponent<MeshFilter>().sharedMesh;
                Assert.AreEqual(level.exteriorWalkable.Length * 4, mesh.vertexCount,
                    "A cell must own one quad, rather than overlapping joint and arm ribbons.");
                Assert.AreEqual(1, authored.GetComponent<Renderer>().sharedMaterial.GetFloat("_AuthoredNetwork"));
                var joins = new List<Vector4>(); mesh.GetUVs(1, joins);
                var vertices = mesh.vertices; var owners = new HashSet<Cell>();
                for (int i = 0; i < vertices.Length; i += 4)
                {
                    var centre = (vertices[i] + vertices[i + 1] + vertices[i + 2] + vertices[i + 3]) * .25f;
                    var cell = BoardMath.CellOf(level, centre);
                    Assert.IsTrue(owners.Add(cell), "Authored geometry draws a cell twice.");
                    Assert.IsTrue(CampWalkability.Contains(level, cell));
                    for (int corner = 0; corner < 4; corner++)
                    {
                        Assert.AreEqual(.5f, Mathf.Abs(vertices[i + corner].x - centre.x), .0001f);
                        Assert.AreEqual(.5f, Mathf.Abs(vertices[i + corner].z - centre.z), .0001f);
                        Assert.AreEqual(joins[i], joins[i + corner]);
                    }
                    if (cell.Equals(new Cell(-1, 1)))
                        Assert.AreEqual(0, joins[i].y, "A trail must not suggest a route through the blocked board neighbour.");
                    if (cell.Equals(new Cell(-1, 2)))
                        Assert.AreEqual(1, joins[i].w, "The scenic exit must meet the outer edge of its authored cell.");
                }
                var scenic = root.GetComponentInChildren<CampTrail>();
                var start = BoardMath.CellCenterWorld(level, scenic.AccessCell);
                var forward = CampTrail.Outward(level, scenic.AccessCell);
                foreach (var vertex in scenic.GetComponent<MeshFilter>().sharedMesh.vertices)
                {
                    float distance = Vector3.Dot(vertex - start, forward);
                    Assert.IsFalse(distance > .5001f && distance < 1.4999f,
                        "Scenic soil overlaps the authored cell's alpha-blended soil.");
                }
                Assert.That(Vector3.Distance(CampTrail.Centre(level, scenic.AccessCell, 1.5f),
                    start + forward * 1.5f + Vector3.up * .024f), Is.LessThan(.0001f));
            }
            finally { Object.Destroy(root); }
            yield return null;
            Assert.AreEqual(cached, RainSurface.CachedMeshCount, "Trail geometry query cache leaked.");
        }

        [UnityTest]
        public IEnumerator BlockedExteriorRouteShowsTheSameAttemptAndBlockingCellsAsTheEvaluator()
        {
            var level = Level(); var root = new GameObject("route-evidence-rendering-test");
            Transform Child(string name)
            {
                var child = new GameObject(name); child.transform.SetParent(root.transform, false); return child.transform;
            }
            var overlays = Child("overlays");
            var renderer = new BoardRenderer(level, AssetCatalog.Load(), Child("base"), Child("grid"),
                Child("obstacles"), Child("tents"), overlays);
            renderer.ConfigureMotion(true, 1);
            try
            {
                var goal = new Cell(-1, 1);
                var route = CampWalkability.ExplainRoute(level, new HashSet<Cell> { goal, new Cell(0, 1) },
                    new Cell(level.entry[0], level.entry[1]), goal, "g1");
                Assert.AreEqual(RouteFailureReason.Blocked, route.Reason);
                renderer.ShowRoute(route);
                Cell[] Drawn(string name) => overlays.Find(name).Cast<Transform>()
                    .Where(t => t.gameObject.activeSelf).Select(t => BoardMath.CellOf(level, t.position)).ToArray();
                CollectionAssert.AreEquivalent(route.Cells, Drawn("PathOverlay"));
                CollectionAssert.AreEquivalent(route.BlockingCells, Drawn("RouteBreak"));
                Assert.Contains(goal, Drawn("RouteBreak"));
                renderer.ShowPlacementPreview(new Placement { guestId = "g1", x = 0, z = 1, rotation = 3 },
                    new RuleReport());
                var dot = overlays.Find("PreviewDoorDot");
                Assert.IsTrue(dot.gameObject.activeSelf);
                Assert.AreEqual(new Cell(-1, 2), BoardMath.CellOf(level, dot.position));
                renderer.HidePath(); renderer.HidePlacementPreview();
                Assert.IsFalse(overlays.Find("PathOverlay").gameObject.activeSelf);
                Assert.IsFalse(overlays.Find("RouteBreak").gameObject.activeSelf);
                Assert.IsFalse(dot.gameObject.activeSelf);
            }
            finally { renderer.ClearAll(); Object.Destroy(root); }
            yield return null;
        }
    }
}
