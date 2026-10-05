using NUnit.Framework;
using QuietCamp.Presentation.World;
using UnityEngine;

namespace QuietCamp.Tests
{
    public sealed class ForestGroundViewTests
    {
        GameObject _cameraRoot, _ground;
        Camera _camera;
        [SetUp] public void SetUp()
        {
            _cameraRoot = new GameObject("Coverage camera"); _camera = _cameraRoot.AddComponent<Camera>();
            _camera.nearClipPlane = .1f; _camera.farClipPlane = 60; _camera.aspect = 2.4f;
            _camera.orthographicSize = 10; _camera.fieldOfView = 65;
            _ground = new GameObject("Coverage ground");
        }
        [TearDown] public void TearDown()
        {
            Object.DestroyImmediate(_cameraRoot); Object.DestroyImmediate(_ground);
        }
        [TestCase(true, 58f)] [TestCase(false, 58f)]
        [TestCase(true, 12f)] [TestCase(false, 12f)]
        public void BoundsContainVisibleGroundIncludingViewsAcrossTheHorizon(bool orthographic, float pitch)
        {
            _camera.orthographic = orthographic;
            _camera.transform.rotation = Quaternion.Euler(pitch, 23, 0);
            _camera.transform.position = -_camera.transform.forward * 20;
            Assert.IsTrue(ForestGroundView.TryBounds(_camera, _ground.transform, new Vector3[8], out var rect));
            int visible = 0;
            foreach (float height in new[] { 0f, .7f, 1.4f })
            for (int y = 0; y <= 12; y++) for (int x = 0; x <= 12; x++)
            {
                var ray = _camera.ViewportPointToRay(new Vector3(x / 12f, y / 12f, 0));
                if (!new Plane(Vector3.up, Vector3.up * height).Raycast(ray, out float distance)) continue;
                var p = ray.GetPoint(distance); var screen = _camera.WorldToViewportPoint(p);
                if (screen.z < _camera.nearClipPlane || screen.z > _camera.farClipPlane) continue;
                Assert.That(p.x, Is.InRange(rect.xMin - .001f, rect.xMax + .001f));
                Assert.That(p.z, Is.InRange(rect.yMin - .001f, rect.yMax + .001f)); visible++;
            }
            Assert.Greater(visible, 20);
        }
        [Test] public void GroundBeyondFarClipAndSkyOnlyHaveNoVisibleFootprint()
        {
            _camera.orthographic = true; _camera.transform.rotation = Quaternion.Euler(90, 0, 0);
            _camera.transform.position = Vector3.up * 100;
            Assert.IsFalse(ForestGroundView.TryBounds(_camera, _ground.transform, new Vector3[8], out _));
            _camera.transform.rotation = Quaternion.Euler(-90, 0, 0); _camera.transform.position = Vector3.up * 20;
            Assert.IsFalse(ForestGroundView.TryBounds(_camera, _ground.transform, new Vector3[8], out _));
        }
        [Test] public void CoverageAndWindBoundsFollowTranslatedRotatedScaledGround()
        {
            _ground.transform.SetPositionAndRotation(new Vector3(17, 3, -9), Quaternion.Euler(0, 37, 0));
            _ground.transform.localScale = new Vector3(1.4f, 1.2f, .8f);
            _camera.orthographic = true; _camera.transform.rotation = Quaternion.Euler(58, 225, 0);
            _camera.transform.position = _ground.transform.position - _camera.transform.forward * 20;
            Assert.IsTrue(ForestGroundView.TryBounds(_camera, _ground.transform, new Vector3[8], out var rect));
            for (int y = 0; y <= 8; y++) for (int x = 0; x <= 8; x++)
            {
                var ray = _camera.ViewportPointToRay(new Vector3(x / 8f, y / 8f, 0));
                Assert.IsTrue(new Plane(_ground.transform.up, _ground.transform.position).Raycast(ray, out float distance));
                var p = _ground.transform.InverseTransformPoint(ray.GetPoint(distance));
                Assert.That(p.x, Is.InRange(rect.xMin - .001f, rect.xMax + .001f));
                Assert.That(p.z, Is.InRange(rect.yMin - .001f, rect.yMax + .001f));
            }
            var key = new Vector2Int(2, -3); var bounds = ForestGroundView.TileBounds(_ground.transform, key, 6);
            foreach (float x in new[] { 10.001f, 19.999f })
            foreach (float y in new[] { -.299f, 1.499f })
            foreach (float z in new[] { -19.999f, -10.001f })
                Assert.IsTrue(bounds.Contains(_ground.transform.TransformPoint(new Vector3(x, y, z))));
        }
    }
}
