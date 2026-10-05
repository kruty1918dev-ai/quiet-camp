using UnityEngine;
using UnityEngine.Rendering;

namespace QuietCamp.Presentation.World
{
    /// <summary>Continuous world-space ground beneath the board and forest.</summary>
    [DefaultExecutionOrder(210)]
    public sealed class MeadowSurface : MonoBehaviour
    {
        public const float GroundY = -.012f;
        Mesh _mesh;
        Material _material;
        readonly Vector3[] _vertices = new Vector3[4];
        readonly Vector3[] _frustumCorners = new Vector3[8];
        Vector2 _extent;
        Camera _camera;
        Matrix4x4 _lastView, _lastGround;
        float _nextFit;
        public void SetPalette(SeasonPalette palette)
        {
            if(_material==null)return;
            _material.SetColor("_GroundLow",palette.GrassDark);_material.SetColor("_GroundHigh",palette.GrassLight);_material.SetColor("_SoilTone",palette.Soil);
        }

        public void Build(float halfWidth, float halfHeight)
        {
            _mesh = new Mesh { name = "Continuous meadow" };
            SetExtent(new Vector2(halfWidth + 24f, halfHeight + 24f));
            _mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            _mesh.RecalculateNormals();
            gameObject.AddComponent<MeshFilter>().sharedMesh = _mesh;
            _material = new Material(Resources.Load<Shader>("QuietCamp/Meadow"));
            _material.SetVector("_ClearingHalfSize", new Vector4(halfWidth, halfHeight, 0, 0));
            var renderer = gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = _material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = true;
        }

        /// <summary>Keep the ground beyond all four screen corners, including
        /// narrow screens and the menu's camera drift. Color remains anchored
        /// to world coordinates when the mesh grows.</summary>
        public void FitToCamera(Camera camera)
        {
            if (_mesh == null || camera == null) return;
            _camera = camera; _lastView = camera.projectionMatrix * camera.worldToCameraMatrix;
            _lastGround = transform.localToWorldMatrix;
            if (!ForestGroundView.TryBounds(camera, transform, _frustumCorners, out var footprint)) return;
            var extent = new Vector2(Mathf.Max(_extent.x, Mathf.Max(Mathf.Abs(footprint.xMin), Mathf.Abs(footprint.xMax)) + 4),
                Mathf.Max(_extent.y, Mathf.Max(Mathf.Abs(footprint.yMin), Mathf.Abs(footprint.yMax)) + 4));
            if (extent != _extent) SetExtent(extent);
        }
        void LateUpdate()
        {
            // Orbit/drift can change the footprint without changing the UI
            // viewport. Extend the same quad; never reframe the camera here.
            if(Time.unscaledTime<_nextFit)return;_nextFit=Time.unscaledTime+.4f;
            if (_camera != null && (_lastView != _camera.projectionMatrix * _camera.worldToCameraMatrix
                || _lastGround != transform.localToWorldMatrix)) FitToCamera(_camera);
        }

        void SetExtent(Vector2 extent)
        {
            _extent = extent;
            _vertices[0] = new Vector3(-extent.x, GroundY, -extent.y);
            _vertices[1] = new Vector3(extent.x, GroundY, -extent.y);
            _vertices[2] = new Vector3(extent.x, GroundY, extent.y);
            _vertices[3] = new Vector3(-extent.x, GroundY, extent.y);
            _mesh.vertices = _vertices;
            _mesh.RecalculateBounds();
        }

        void OnDestroy()
        {
            if (_mesh != null) Destroy(_mesh);
            if (_material != null) Destroy(_material);
        }
    }
}
