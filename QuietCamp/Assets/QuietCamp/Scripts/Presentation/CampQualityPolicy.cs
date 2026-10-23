using UnityEngine;

namespace QuietCamp.Presentation
{
    /// <summary>Pure timing policy: exclude loading and require sustained headroom before recovery.</summary>
    public sealed class CampQualityPolicy
    {
        float _warmup, _overload, _headroom;
        float _windowSeconds;
        int _windowFrames;
        public int Tier { get; private set; } = 1;
        public int MaximumTier {get;private set;}=2;
        public void Reset(int tier,int maximumTier=2) {MaximumTier=Mathf.Clamp(maximumTier,0,2);Tier=Mathf.Clamp(tier,0,MaximumTier);WarmUp();}
        public void WarmUp() { _warmup = 8; _overload = _headroom = 0; _windowSeconds=0;_windowFrames=0; }
        public bool ObserveFrame(float seconds)
        {
            if(seconds<=0||float.IsNaN(seconds)||float.IsInfinity(seconds))return false;
            // Sustained 3 FPS is overload, not a loading event. Pause/focus reset
            // warmup explicitly; only a long suspension/loading stall does so here.
            if(seconds>5){WarmUp();return false;}
            _windowSeconds+=seconds;_windowFrames++;
            if(_windowSeconds<2)return false;
            float duration=_windowSeconds,average=duration/_windowFrames;
            _windowSeconds=0;_windowFrames=0;
            return Sample(average,duration);
        }
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
