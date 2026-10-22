using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace QuietCamp.Application
{
    // Session-owned frequency authority; each limit is evaluated independently.
    public sealed class InterstitialService
    {
        readonly IAdService _ads;
        readonly Func<bool> _allowed;
        readonly Func<double> _seconds;
        readonly int _first, _between, _maximum;
        readonly double _interval, _rewardCooldown;
        int _observedCompletions, _lastShownCompletions, _shown;
        double _lastAd = double.NegativeInfinity, _lastReward = double.NegativeInfinity;
        public bool Busy { get; private set; }
        public InterstitialService(IAdService ads, Func<bool> allowed, int initialCompletions,
            int first = 5, int between = 4, double interval = 180, double rewardCooldown = 180,
            int maximum = 3, Func<double> seconds = null)
        {
            if (first < 0 || between < 1 || interval < 0 || rewardCooldown < 0 || maximum < 0) throw new ArgumentException("Invalid interstitial limits");
            _ads = ads; _allowed = allowed; _observedCompletions = initialCompletions;
            _first = first; _between = between; _interval = interval; _rewardCooldown = rewardCooldown; _maximum = maximum;
            var clock = Stopwatch.StartNew(); _seconds = seconds ?? (() => clock.Elapsed.TotalSeconds);
        }
        public void Rewarded() { _lastReward = _seconds(); }
        public async Task<AdResult> AtLevelTransition(int completedCount)
        {
            if (Busy || completedCount <= _observedCompletions) return AdResult.Unavailable;
            _observedCompletions = completedCount;
            double now = _seconds();
            if (!_allowed() || !(_ads is IInterstitialAdService provider) || !_ads.IsReady || _shown >= _maximum
                || completedCount < _first || (_shown > 0 && completedCount - _lastShownCompletions < _between)
                || now - _lastAd < _interval || now - _lastReward < _rewardCooldown) return AdResult.Unavailable;
            Busy = true;
            // Reserve before awaiting: failures never permit a rapid second request.
            _shown++; _lastAd = now; _lastShownCompletions = completedCount;
            try { return await provider.ShowInterstitial("camp.interstitial"); }
            catch { return AdResult.Failed; }
            finally { Busy = false; }
        }
    }
}
