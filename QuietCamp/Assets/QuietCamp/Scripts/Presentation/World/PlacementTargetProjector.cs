using QuietCamp.Domain;
using UnityEngine;

namespace QuietCamp.Presentation.World
{
    /// <summary>One screen-to-board projection for preview and release. Input ownership always uses the raw pointer.</summary>
    public sealed class PlacementTargetProjector
    {
        public const float MinimumTouchLiftDp = 96f;
        readonly Camera _camera;
        readonly LevelData _level;
        readonly Plane _ground = new Plane(Vector3.up, Vector3.zero);
        Vector2 _grab;
        int _width, _height;
        Rect _safeArea;
        public float LiftPixels { get; private set; }
        public float DensityScale { get; private set; } = 1f;
        public bool LayoutUnchanged => _width == Screen.width && _height == Screen.height && _safeArea == Screen.safeArea;

        public PlacementTargetProjector(Camera camera, LevelData level) { _camera = camera; _level = level; }

        public static float ScreenDpScale()
        {
            var dpi = Screen.dpi;
            return dpi >= 80f && dpi <= 640f ? Mathf.Max(1f, dpi / 160f)
                : Mathf.Clamp(Mathf.Min(Screen.width, Screen.height) / 420f, 1f, 3f);
        }

        public void Begin(bool touchDrag, Vector2 grabInFootprint, Vector3 tentCenter)
        {
            _width = Screen.width; _height = Screen.height; _safeArea = Screen.safeArea;
            _grab = grabInFootprint; DensityScale = ScreenDpScale(); LiftPixels = 0f;
            if (!touchDrag || _camera == null) return;
            float lowest = float.PositiveInfinity;
            for (int x = -1; x <= 1; x += 2)
            for (int z = -1; z <= 1; z += 2)
                lowest = Mathf.Min(lowest, _camera.WorldToScreenPoint(tentCenter + new Vector3(x, -tentCenter.y, z)).y);
            var grabbed = tentCenter + new Vector3(_grab.x - 1f, -tentCenter.y, _grab.y - 1f);
            float belowAim = Mathf.Max(0f, _camera.WorldToScreenPoint(grabbed).y - lowest);
            LiftPixels = Mathf.Max(MinimumTouchLiftDp * DensityScale, belowAim + 32f * DensityScale);
        }

        public bool TryProject(Vector2 pointer, bool dragging, Placement current, out PlacementTarget target)
        {
            var aim = pointer + Vector2.up * (dragging ? LiftPixels : 0f);
            target = new PlacementTarget { AimScreen = aim };
            if (_camera == null || !LayoutUnchanged || !Finite(pointer.x) || !Finite(pointer.y)) return false;
            var bounds = new Rect(0, 0, Screen.width, Screen.height);
            var safe = _safeArea.width > 0 && _safeArea.height > 0 ? _safeArea : bounds;
            if (!bounds.Contains(pointer) || !safe.Contains(aim) || !_camera.pixelRect.Contains(aim)) return false;
            var ray = _camera.ScreenPointToRay(aim);
            if (!_ground.Raycast(ray, out var distance)) return false;
            var point = ray.GetPoint(distance);
            float ax = point.x + _level.width * .5f - _grab.x;
            float az = point.z + _level.height * .5f - _grab.y;
            int x = Mathf.FloorToInt(ax + .5f), z = Mathf.FloorToInt(az + .5f);
            if (current != null)
            {
                if (Mathf.Abs(ax - current.x) < .5f + BoardMath.GrabHysteresis) x = current.x;
                if (Mathf.Abs(az - current.z) < .5f + BoardMath.GrabHysteresis) z = current.z;
            }
            target.GroundPoint = point; target.Anchor = new Cell(x, z);
            return true;
        }

        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }

    public struct PlacementTarget
    {
        public Vector2 AimScreen;
        public Vector3 GroundPoint;
        public Cell Anchor;
    }
}
