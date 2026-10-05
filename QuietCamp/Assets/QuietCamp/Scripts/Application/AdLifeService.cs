using System;
using System.Threading.Tasks;

namespace QuietCamp.Application
{
    public sealed class AdLifeService
    {
        readonly IAdService _ads;
        readonly CampEconomy _economy;
        readonly Func<bool> _allowed;
        string _pendingReward;
        public bool Busy { get; private set; }
        public bool Available => !Busy && !_economy.IsPro && _economy.AdLivesRemaining > 0 && _allowed() && (_pendingReward != null || _ads.IsReady);
        public event Action Changed;
        public AdLifeService(IAdService ads, CampEconomy economy, Func<bool> allowed)
        { _ads = ads; _economy = economy; _allowed = allowed; }
        public async Task<EconomyResult> Request()
        {
            if (Busy) return EconomyResult.Busy;
            if (_economy.AdLivesRemaining == 0) return EconomyResult.DailyLimit;
            if (!Available) return EconomyResult.Unavailable;
            Busy = true; Changed?.Invoke();
            try
            {
                if (_pendingReward == null)
                {
                    if (await _ads.ShowRewarded("camp.life") != AdResult.Completed) return EconomyResult.Unavailable;
                    _pendingReward = "ad:" + Guid.NewGuid().ToString("N");
                }
                var result = _economy.GrantAdLife(_pendingReward);
                if (result == EconomyResult.Applied || result == EconomyResult.AlreadyApplied) _pendingReward = null;
                return result;
            }
            catch { return EconomyResult.Unavailable; }
            finally { Busy = false; Changed?.Invoke(); }
        }
    }
}
