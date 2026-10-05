using System;
using QuietCamp.Infrastructure;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace QuietCamp.Presentation.World
{
    /// <summary>Shared grading and tonemapping on every device. Only effect sampling cost scales.</summary>
    public sealed class PhasePostFx : MonoBehaviour
    {
        Volume _volume;
        VolumeProfile _profile;
        ColorAdjustments _color;
        WhiteBalance _white;
        Bloom _bloom;
        Vignette _vignette;
        Tonemapping _tonemapping;
        Camera _camera;
        bool _originalPost, _originalHdr;
        int _tier;
        Func<bool> _reducedMotion;
        float _celebration;
        public bool RestoreCameraOnDestroy=true;
        public VolumeProfile RuntimeProfile => _profile;

        public void Configure(Camera camera, int tier, Func<bool> reducedMotion = null)
        {
            if (_profile != null || camera == null) return;
            _camera = camera;
            _reducedMotion = reducedMotion;
            _originalHdr = camera.allowHDR;
            _originalPost = camera.GetUniversalAdditionalCameraData().renderPostProcessing;
            _profile = ScriptableObject.CreateInstance<VolumeProfile>();
            _color = _profile.Add<ColorAdjustments>(true);
            _color.postExposure.Override(0f);
            _color.saturation.overrideState = true;
            _color.contrast.overrideState = true;
            _color.colorFilter.Override(Color.white);
            _white = _profile.Add<WhiteBalance>(true);
            _white.temperature.overrideState = true;
            _bloom = _profile.Add<Bloom>(true);
            _bloom.intensity.overrideState = true;
            _bloom.threshold.Override(1.05f);
            _bloom.scatter.Override(.42f);
            _bloom.highQualityFiltering.Override(false);
            _vignette = _profile.Add<Vignette>(true);
            _vignette.color.Override(new Color(.12f, .19f, .16f));
            _vignette.intensity.Override(.12f);
            _vignette.smoothness.Override(.65f);
            _tonemapping = _profile.Add<Tonemapping>(true);
            _tonemapping.mode.Override(TonemappingMode.Neutral);

            var go = new GameObject("PhaseVolume");
            go.transform.SetParent(transform, false);
            _volume = go.AddComponent<Volume>();
            _volume.isGlobal = true;
            _volume.priority = 10f;
            _volume.sharedProfile = _profile;
            SetTier(tier);
        }

        public void SetSuspended(bool suspended) { if(_volume!=null)_volume.enabled=!suspended; enabled=!suspended; }

        void SetTier(int tier)
        {
            _tier = Mathf.Clamp(tier, 0, 2);
            _camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            _camera.allowHDR = true;
            _volume.enabled = true;
            _vignette.active = true;
            _tonemapping.active = true;
            _bloom.downscale.Override(BloomDownscaleMode.Quarter);
            _bloom.maxIterations.Override(_tier>=2?4:_tier==1?2:1);
        }

        public void Apply(AtmosphereCatalog.Profile profile, int tier)
        {
            if (_volume == null || profile == null) return;
            SetTier(tier);
            _white.temperature.value = profile.WhiteBalance;
            _color.saturation.value = profile.Saturation;
            _color.contrast.value = profile.Contrast;
            // Forest volumes provide depth. Keep the screen vignette faint,
            // especially in dawn mist, so the rim feels airy rather than ominous.
            _vignette.intensity.value = profile.Mist ? .025f
                : profile.Id == "night" ? .07f : profile.Id == "evening" ? .055f : .035f;
            _bloom.intensity.value = Mathf.Max(.055f,profile.Bloom)*.8f;
            _bloom.active = true;
            _color.active = true;
            _white.active = true;
        }

        /// <summary>A slow warm tint; no exposure flash, blur or camera shake.</summary>
        public void Celebrate()
        {
            if (_tier <= 0 || (_reducedMotion?.Invoke() ?? false)) return;
            _celebration = 1.6f;
        }

        void Update()
        {
            if (_color == null || _celebration <= 0f) return;
            if (_reducedMotion?.Invoke() ?? false) _celebration = 0f;
            else _celebration = Mathf.Max(0f, _celebration - Time.unscaledDeltaTime);
            float weight = Mathf.Sin((_celebration / 1.6f) * Mathf.PI) * .12f;
            _color.colorFilter.value = Color.Lerp(Color.white, new Color(1f, .85f, .6f), weight);
        }

        void OnDestroy()
        {
            if (_camera != null && RestoreCameraOnDestroy)
            {
                _camera.allowHDR = _originalHdr;
                _camera.GetUniversalAdditionalCameraData().renderPostProcessing = _originalPost;
            }
            if (_volume != null) Destroy(_volume.gameObject);
            if (_profile != null)
            {
                foreach (var component in _profile.components)
                    if (component != null) Destroy(component);
                Destroy(_profile);
            }
        }
    }
}
