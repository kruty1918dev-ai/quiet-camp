using UnityEngine;

namespace QuietCamp.Presentation
{
    /// <summary>Changes peripheral effect cost while retaining the shared lighting and shadow floor.</summary>
    public sealed class AdaptiveCampQuality : MonoBehaviour
    {
        GameServices _services;
        readonly CampQualityPolicy _policy = new CampQualityPolicy();
        int _manual = -1;
        float _elapsed, _sum;
        int _frames;
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
            _elapsed = _sum = 0; _frames = 0;
            Debug.Log("[LiveCamp] Effect quality " + _services.EffectiveQuality + " (manual=" + _services.Settings.quality + ", target=60)");
        }
        void OnEnable() => UnityEngine.SceneManagement.SceneManager.sceneLoaded += SceneReady;
        void OnDisable() => UnityEngine.SceneManagement.SceneManager.sceneLoaded -= SceneReady;
        void SceneReady(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
        { _policy.WarmUp(); _elapsed = _sum = 0; _frames = 0; }
        void OnApplicationPause(bool paused)
        { _policy.WarmUp(); _elapsed = _sum = 0; _frames = 0; }
        void OnApplicationFocus(bool focused)
        { _policy.WarmUp(); _elapsed = _sum = 0; _frames = 0; }
        void Update()
        {
            if (_services == null) return;
            if (_manual != _services.Settings.quality)
            {
                _manual = _services.Settings.quality;
                ResetPreference();
            }
            if (!UnityEngine.Application.isFocused) return;
            var dt = Time.unscaledDeltaTime;
            if (dt <= 0 || dt > .25f) { _policy.WarmUp();_elapsed=_sum=0;_frames=0;return; }
            _elapsed += dt; _sum += dt; _frames++;
            if (_elapsed < 2) return;
            var duration = _elapsed; var average = _sum / Mathf.Max(1, _frames);
            _elapsed = _sum = 0; _frames = 0;
            ObserveFrameWindow(average,duration);
        }
        /// <summary>Observe a measured frame window. This boundary also lets device diagnostics
        /// exercise overload/recovery without rewriting frame-rate preferences.</summary>
        public void ObserveFrameWindow(float secondsPerFrame,float duration)
        {if(_services!=null&&_policy.Sample(secondsPerFrame,duration))Apply(_policy.Tier);}
    }

    /// <summary>Pure timing policy: exclude loading and require sustained headroom before recovery.</summary>
    public sealed class CampQualityPolicy
    {
        float _warmup, _overload, _headroom;
        public int Tier { get; private set; } = 1;
        public int MaximumTier {get;private set;}=2;
        public void Reset(int tier,int maximumTier=2) {MaximumTier=Mathf.Clamp(maximumTier,0,2);Tier=Mathf.Clamp(tier,0,MaximumTier);WarmUp();}
        public void WarmUp() { _warmup = 8; _overload = _headroom = 0; }
        public bool Sample(float secondsPerFrame, float duration)
        {
            if (duration <= 0 || float.IsNaN(duration)||float.IsInfinity(duration)||secondsPerFrame<=0||float.IsNaN(secondsPerFrame) || float.IsInfinity(secondsPerFrame)) return false;
            if (_warmup > 0) { _warmup = Mathf.Max(0, _warmup - duration); return false; }
            _overload = secondsPerFrame > .020f ? _overload + duration : Mathf.Max(0, _overload - duration * 2);
            _headroom = secondsPerFrame < .0175f ? _headroom + duration : 0;
            if (_overload >= 8 && Tier > 0) { Tier--; WarmUp(); return true; }
            if (_headroom >= 45 && Tier < MaximumTier) { Tier++; WarmUp(); return true; }
            return false;
        }
    }
}
