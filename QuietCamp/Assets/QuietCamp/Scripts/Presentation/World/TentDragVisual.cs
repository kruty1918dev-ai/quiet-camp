using UnityEngine;

namespace QuietCamp.Presentation.World
{
    /// <summary>Small inertial movement of the carried visual only; never changes the logical pose.</summary>
    public sealed class TentDragVisual : MonoBehaviour
    {
        Transform _visual;
        Quaternion _rest;
        Vector3 _tilt, _velocity, _target;
        bool _reduced;
        void Awake()
        {
            _visual = transform.Find("VisualCenter");
            if (_visual != null) _rest = _visual.localRotation;
        }
        public void Follow(Vector3 worldVelocity, bool reduced)
        {
            _reduced = reduced;
            var local = transform.InverseTransformDirection(worldVelocity);
            _target = new Vector3(Mathf.Clamp(local.z * .7f, -6f, 6f), 0, Mathf.Clamp(-local.x * .7f, -6f, 6f));
        }
        void LateUpdate()
        {
            if (_visual == null) return;
            _tilt = _reduced ? Vector3.zero : Vector3.SmoothDamp(_tilt, _target, ref _velocity, .11f, 90f, Mathf.Min(.04f, Time.unscaledDeltaTime));
            _visual.localRotation = _rest * Quaternion.Euler(_tilt);
            _target *= Mathf.Exp(-Time.unscaledDeltaTime * 7f);
        }
        void OnDisable()
        {
            if (_visual != null) _visual.localRotation = _rest;
            _tilt = _velocity = _target = Vector3.zero;
        }
    }
}
