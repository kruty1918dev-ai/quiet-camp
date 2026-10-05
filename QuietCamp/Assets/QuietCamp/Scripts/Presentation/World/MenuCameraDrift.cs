using System;
using QuietCamp.Domain;
using UnityEngine;
using UnityEngine.InputSystem;

namespace QuietCamp.Presentation.World
{
    /// <summary>
    /// Menu camera: owns the fitted pose (CameraFitter into the lower
    /// viewport band) and layers two kinds of life on top — a very slow
    /// positional/rotational drift (~10 s cycles, ±0.15 m / ±0.4°) and a
    /// pointer-follow parallax nudge. Because the scene is real 3D with
    /// world-space forest, the nudge preserves the scenery's perspective.
    /// Reduced motion settles the camera to the plain fitted pose.
    /// </summary>
    public sealed class MenuCameraDrift : MonoBehaviour
    {
        Camera _camera;
        LevelData _level;
        RectTransform _viewport;
        Func<bool> _reducedMotion;
        Func<bool> _suppressed;
        Vector3 _basePos;
        Quaternion _baseRot;
        Vector2 _parallax;
        Vector2 _parallaxTarget;
        Vector2Int _screen;
        Rect _viewportRect;
        Vector3 _viewportPosition;
        bool _dirty = true;
        CampAtmosphere _atmosphere;

        public void Configure(Camera camera, LevelData level, RectTransform viewport,
            Func<bool> reducedMotion, Func<bool> suppressed = null)
        {
            _camera = camera;
            _level = level;
            _viewport = viewport;
            _reducedMotion = reducedMotion;
            _suppressed = suppressed;
            _dirty = true;
            _atmosphere=GetComponent<CampAtmosphere>();
        }

        void LateUpdate()
        {
            if (_camera == null || _level == null) return;
            var size = new Vector2Int(Screen.width, Screen.height);
            var rect = _viewport != null ? _viewport.rect : default;
            var position = _viewport != null ? _viewport.position : Vector3.zero;
            if (size != _screen || _dirty || rect != _viewportRect || position != _viewportPosition)
            {
                _screen = size; _viewportRect = rect; _viewportPosition = position;
                CameraFitter.Fit(_camera, _level, _viewport);
                _basePos = _camera.transform.position;
                _baseRot = _camera.transform.rotation;
                _dirty = false;
            }
            // Frozen under reduced motion and while a scene transition owns
            // the frame — a screen wipe must not fight camera drift.
            if ((_reducedMotion != null && _reducedMotion())
                || (_suppressed != null && _suppressed()))
            {
                _camera.transform.SetPositionAndRotation(_basePos, _baseRot);
                return;
            }
            var pointer = Pointer.current;
            if (pointer != null)
            {
                var p = pointer.position.ReadValue();
                _parallaxTarget = new Vector2(
                    Mathf.Clamp01(p.x / Mathf.Max(1, Screen.width)) * 2f - 1f,
                    Mathf.Clamp01(p.y / Mathf.Max(1, Screen.height)) * 2f - 1f);
            }
            _parallax = Vector2.Lerp(_parallax, _parallaxTarget,
                1f - Mathf.Exp(-Time.deltaTime * 2.5f));
            var t = Time.unscaledTime;
            var right = _baseRot * Vector3.right;
            var up = _baseRot * Vector3.up;
            var offset = right * (Mathf.Sin(t * 0.62f) * 0.09f + Mathf.Sin(t * 0.171f) * 0.06f
                    - _parallax.x * 0.32f)
                + up * (Mathf.Sin(t * 0.53f + 1.7f) * 0.07f + Mathf.Sin(t * 0.147f) * 0.05f
                    - _parallax.y * 0.22f);
            var wind=_atmosphere!=null?_atmosphere.Wind:default;
            offset+=new Vector3(wind.DirectionXZ.x,0,wind.DirectionXZ.y)*Mathf.Sin(wind.PhaseSeconds*.67f)*wind.Strength*.035f;
            _camera.transform.SetPositionAndRotation(_basePos + offset,
                _baseRot * Quaternion.Euler(
                    Mathf.Sin(t * 0.41f + 0.8f) * 0.22f,
                    Mathf.Sin(t * 0.37f) * 0.30f, 0f));
        }
    }
}
