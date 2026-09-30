using System;
using System.IO;
using NUnit.Framework;
using QuietCamp.Infrastructure;
using QuietCamp.Domain;
using QuietCamp.Presentation.World;
using UnityEngine;

namespace QuietCamp.Tests.Editor
{
    public class AtmosphereCatalogTests
    {
        static string Json => File.ReadAllText("Assets/QuietCamp/Resources/QuietCamp/atmosphere.json");

        [Test] public void LegacyLevelsAndUnknownPhasesRemainLoadable()
        {
            var catalog = AtmosphereCatalog.Parse(Json);
            Assert.AreEqual("noon", catalog.Resolve("custom", "day").Id);
            Assert.AreEqual("evening", catalog.Resolve("custom", "evening").Id);
            Assert.AreEqual("noon", catalog.Resolve(null, null).Id);
            Assert.AreEqual("noon", catalog.Get("unknown").Id);
            Assert.AreEqual("morning", catalog.Resolve("QC001", "day").Id);
            Assert.AreEqual("night", catalog.Resolve("QC005", "day").Id);
            Assert.AreEqual(0, catalog.Get("night").BirdMax);
        }

        [Test] public void InvalidConfigurationIsRejectedBeforeConsumption()
        {
            Assert.Throws<InvalidOperationException>(() => AtmosphereCatalog.Parse(Json.Replace("\"schemaVersion\": 2", "\"schemaVersion\": 9")));
            Assert.Throws<InvalidOperationException>(() => AtmosphereCatalog.Parse(Json.Replace("#FFE3BE", "not-a-color")));
            Assert.Throws<InvalidOperationException>(() => AtmosphereCatalog.Parse(Json.Replace("\"QC001\": \"morning\"", "\"QC001\": \"missing\"")));
            Assert.Throws<InvalidOperationException>(() => AtmosphereCatalog.Parse(Json.Replace("\"bloom\": 0.04", "\"bloom\": 7")));
            Assert.Throws<InvalidOperationException>(() => AtmosphereCatalog.Parse(Json.Replace("\"gustMax\": 45", "\"gustMax\": 5")));
        }

        [Test] public void PhaseProfilesCarryAmbienceAndPostFxParameters()
        {
            var catalog = AtmosphereCatalog.Parse(Json);
            var night = catalog.Get("night");
            Assert.AreEqual(0, night.BirdMax);
            Assert.Greater(night.OwlMax, 0f, "Night must schedule the owl.");
            Assert.AreEqual(0, catalog.Get("noon").OwlMax, "Day phases stay silent of owls.");
            Assert.GreaterOrEqual(catalog.Get("morning").Dust, 0);
            Assert.Greater(catalog.Get("evening").Fireflies, 0);
            Assert.That(night.Bloom, Is.InRange(0f, 1f));
            Assert.AreEqual("morning", catalog.Get("morning").Id);
            Assert.IsTrue(catalog.Get("morning").Mist);
        }

        [TestCase(.4615f, 6, 6)]
        [TestCase(.5625f, 10, 8)]
        [TestCase(1.3333f, 12, 12)]
        public void FullScreenCameraFitsCornersAndRaycastsBackToTheBoard(float aspect, int width, int height)
        {
            var go = new GameObject("CameraFitTest");
            try
            {
                var camera = go.AddComponent<Camera>();
                CameraFitter.Configure(camera);
                camera.aspect = aspect;
                var level = new LevelData { width = width, height = height };
                CameraFitter.Fit(camera, level, null);
                Assert.AreEqual(new Rect(0, 0, 1, 1), camera.rect);
                foreach (int x in new[] { 0, width - 1 })
                foreach (int z in new[] { 0, height - 1 })
                {
                    var point = BoardMath.CellCenter(level, x, z);
                    var projected = camera.WorldToViewportPoint(point);
                    Assert.That(projected.x, Is.InRange(0f, 1f));
                    Assert.That(projected.y, Is.InRange(0f, 1f));
                    var ray = camera.ViewportPointToRay(projected);
                    Assert.IsTrue(new Plane(Vector3.up, Vector3.zero).Raycast(ray, out var distance));
                    Assert.AreEqual(new Cell(x, z), BoardMath.CellOf(level, ray.GetPoint(distance)));
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }
    }
}

