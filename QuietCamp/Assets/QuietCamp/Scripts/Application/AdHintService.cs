using System;
using System.Threading.Tasks;

namespace QuietCamp.Application
{
    public sealed class AdHintService
    {
        readonly IAdService _ads;
        readonly CampEconomy _economy;
        readonly Func<bool> _allowed;
        string _pendingReward => _economy.PendingAdReward(true);
        public bool Busy { get; private set; }
        public bool Available => !Busy && !_economy.IsPro && (_pendingReward != null || (_economy.AdHintsRemaining > 0 && _allowed() && _ads.IsReady));
        public event Action Changed;
        public event Action<RewardStage> RewardObserved;
        void Observe(RewardStage stage) { try { RewardObserved?.Invoke(stage); } catch { } }
        public AdHintService(IAdService ads, CampEconomy economy, Func<bool> allowed)
        { _ads = ads; _economy = economy; _allowed = allowed; }
        public async Task<EconomyResult> Request()
        {
            if (Busy) return EconomyResult.Busy;
            if (_economy.AdHintsRemaining == 0) return EconomyResult.DailyLimit;
            if (!Available) return EconomyResult.Unavailable;
            Busy = true; Changed?.Invoke();
            try
            {
                if (_pendingReward == null)
                {
                    Observe(RewardStage.Requested);
                    if (await _ads.ShowRewarded("camp.hint") != AdResult.Completed) return EconomyResult.Unavailable;
                    Observe(RewardStage.Completed);
                    _economy.RememberEarnedAdReward(true);
                }
                var result = _economy.GrantAdHint(_pendingReward);
                if (result == EconomyResult.Applied) Observe(RewardStage.Granted);
                return result;
            }
            catch { return EconomyResult.Unavailable; }
            finally { Busy = false; Changed?.Invoke(); }
        }
    }
}
