using QuietCamp.Infrastructure;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace QuietCamp.Presentation.World
{
    /// <summary>
    /// Scene-owned global Volume with a runtime VolumeProfile instance —
    /// never mutates the shared QC_GlobalVolumeProfile asset. Applies the
    /// phase's white balance / saturation / contrast on Balanced and High;
    /// Low gets no post pass at all. Bloom stays at the profile's tiny value
    /// only on High and only when the camera pipeline still runs LDR, so it
    /// is effectively decorative — HDR + real bloom is an explicit future
    /// opt-in per the atmosphere spec, not enabled here.
    /// </summary>
    public sealed class PhasePostFx : MonoBehaviour
    {
        Volume _volume;
        VolumeProfile _profile;
        ColorAdjustments _color;
        WhiteBalance _white;
        Bloom _bloom;

        public void Configure(Camera camera, int tier)
        {
            var additional = camera.GetUniversalAdditionalCameraData();
            additional.renderPostProcessing = tier >= 1;
            if (tier <= 0) return; // Low: zero post passes.

            _profile = ScriptableObject.CreateInstance<VolumeProfile>();
            _color = _profile.Add<ColorAdjustments>(true);
            _color.postExposure.overrideState = true;
            _color.postExposure.value = 0f;
            _color.saturation.overrideState = true;
            _color.contrast.overrideState = true;
            _white = _profile.Add<WhiteBalance>(true);
            _white.temperature.overrideState = true;
            _bloom = _profile.Add<Bloom>(true);
            _bloom.intensity.overrideState = true;
            _bloom.threshold.overrideState = true;
            _bloom.threshold.value = 1.05f;

            var go = new GameObject("PhaseVolume");
            go.transform.SetParent(transform, false);
            _volume = go.AddComponent<Volume>();
            _volume.isGlobal = true;
            _volume.priority = 0f;
            _volume.profile = _profile;
        }

        public void Apply(AtmosphereCatalog.Profile profile, int tier)
        {
            if (_volume == null) return;
            bool on = tier >= 1;
            _volume.enabled = on;
            if (!on) return;
            _white.temperature.value = profile.WhiteBalance;
            _color.saturation.value = profile.Saturation;
            _color.contrast.value = profile.Contrast;
            _bloom.intensity.value = tier >= 2 ? profile.Bloom : 0f;
            _bloom.active = tier >= 2 && profile.Bloom > 0f;
            _color.active = true;
            _white.active = true;
        }

        void OnDestroy()
        {
            if (_volume != null) Destroy(_volume.gameObject);
            if (_profile != null) Destroy(_profile);
        }
    }
}
