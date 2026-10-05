using System;
using System.Threading.Tasks;

namespace QuietCamp.Application
{
    public interface IAdPrivacyOptions
    {
        bool PrivacyOptionsRequired { get; }
        Task ShowPrivacyOptions();
    }
    public interface IAdRequestCancellation
    {
        bool CanCancelPending { get; }
        void CancelPending();
    }
    public enum CozyRewardResult { Granted, AlreadyOwned, Unavailable, Skipped, Failed, Busy }

    /// <summary>One optional cosmetic, also earned through normal play. No
    /// hint/progression purchase, no automatic request, no forced ad format.</summary>
    public sealed class CozyRewardService
    {
        public const int LanternFlag = 4;
        public const int FreeCompletionCount = 3;
        readonly IAdService _ads;
        readonly SettingsSaveData _settings;
        readonly ProgressionService _progression;
        readonly Func<bool> _persist;
        public bool Busy { get; private set; }
        public bool Owned => (_progression.CosmeticFlags & LanternFlag) != 0;
        public bool Available => _settings.optionalVideoBonuses && _ads.IsReady && !Owned && !Busy;
        public event Action Changed;

        public CozyRewardService(IAdService ads, SettingsSaveData settings, ProgressionService progression, Func<bool> persist)
        { _ads = ads; _settings = settings; _progression = progression; _persist = persist; }

        public void EarnFromPlay()
        {
            if (!Owned && _progression.CompletedCount >= FreeCompletionCount) Grant();
        }
        public async Task<CozyRewardResult> Request(string placement)
        {
            if (Busy) return CozyRewardResult.Busy;
            if (Owned) return CozyRewardResult.AlreadyOwned;
            if (placement != "camp.complete.lantern" && placement != "album.lantern") return CozyRewardResult.Unavailable;
            if (!Available) return CozyRewardResult.Unavailable;
            Busy = true; Changed?.Invoke();
            try
            {
                var result = await _ads.ShowRewarded(placement);
                // The provider returns Completed only after the SDK reward
                // callback AND full-screen dismissal. Close alone earns nothing.
                if (result == AdResult.Completed)
                {
                    if (!Owned && !Grant()) return CozyRewardResult.Failed;
                    return CozyRewardResult.Granted;
                }
                return result == AdResult.Skipped ? CozyRewardResult.Skipped : result == AdResult.Unavailable ? CozyRewardResult.Unavailable : CozyRewardResult.Failed;
            }
            catch { return CozyRewardResult.Failed; }
            finally { Busy = false; Changed?.Invoke(); }
        }
        bool Grant()
        {
            int flags = _progression.CosmeticFlags;
            bool selected = _settings.fireflyLantern;
            _progression.CosmeticFlags |= LanternFlag;
            _settings.fireflyLantern = true;
            if (_persist != null && !_persist())
            { _progression.CosmeticFlags = flags; _settings.fireflyLantern = selected; return false; }
            Changed?.Invoke(); return true;
        }
    }
}
