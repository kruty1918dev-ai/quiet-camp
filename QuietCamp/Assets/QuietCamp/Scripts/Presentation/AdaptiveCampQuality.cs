using UnityEngine;

namespace QuietCamp.Presentation
{
    /// <summary>Changes peripheral effect cost while retaining the shared lighting and shadow floor.</summary>
    public sealed class AdaptiveCampQuality : MonoBehaviour
    {
        GameServices _services;
        readonly CampQualityPolicy _policy = new CampQualityPolicy();
        int _manual = -1;
        public void Configure(GameServices services)
        {
            _services = services; _manual = services.Settings.quality;
            ResetPreference();
        }
        void ResetPreference()
        {int selected=_manual>0?Mathf.Clamp(_manual-1,0,2):1;_policy.Reset(selected,_manual>0?selected:2);Apply(_policy.Tier);}
        void Apply(int tier)
        {
            _services.EffectiveQuality = Mathf.Clamp(tier, 0, 2);
            // A manual preference keeps its render profile; the adaptive value
            // only controls peripheral effect work below that preference.
            int renderTier=_manual>0?Mathf.Clamp(_manual-1,0,2):_services.EffectiveQuality;
            if(QualitySettings.GetQualityLevel()!=renderTier)QualitySettings.SetQualityLevel(renderTier,true);
            UnityEngine.Application.targetFrameRate = 60;
            Debug.Log("[LiveCamp] Effect quality " + _services.EffectiveQuality + " (manual=" + _services.Settings.quality + ", target=60)");
        }
        void OnEnable() => UnityEngine.SceneManagement.SceneManager.sceneLoaded += SceneReady;
        void OnDisable() => UnityEngine.SceneManagement.SceneManager.sceneLoaded -= SceneReady;
        void SceneReady(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
        { _policy.WarmUp(); }
        void OnApplicationPause(bool paused)
        { _policy.WarmUp(); }
        void OnApplicationFocus(bool focused)
        { _policy.WarmUp(); }
        void Update()
        {
            if (_services == null) return;
            if (_manual != _services.Settings.quality)
            {
                _manual = _services.Settings.quality;
                ResetPreference();
            }
            if (!UnityEngine.Application.isFocused) return;
            if (_policy.ObserveFrame(Time.unscaledDeltaTime)) Apply(_policy.Tier);
        }
        /// <summary>Observe a measured frame window. This boundary also lets device diagnostics
        /// exercise overload/recovery without rewriting frame-rate preferences.</summary>
        public void ObserveFrameWindow(float secondsPerFrame,float duration)
        {if(_services!=null&&_policy.Sample(secondsPerFrame,duration))Apply(_policy.Tier);}
    }

}
