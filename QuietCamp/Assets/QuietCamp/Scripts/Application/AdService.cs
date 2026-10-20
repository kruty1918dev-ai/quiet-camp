using System.Threading.Tasks;
namespace QuietCamp.Application
{
    public enum RewardStage { Requested, Completed, Granted }
    public interface IAdConsentInitialization
    {
        Task RefreshConsent();
    }
    /// <summary>
    /// Result of an optional SDK ad request. Completed requires the earned
    /// reward callback and dismissal of the full-screen ad.
    /// </summary>
    public enum AdResult
    {
        Unavailable,
        Completed,
        Skipped,
        Failed
    }

    /// <summary>
    /// Monetization boundary shared by the SDK adapter and offline fallback.
    /// Gameplay code never consumes Google SDK types.
    /// </summary>
    public interface IAdService
    {
        /// <summary>Whether a rewarded request can start; does not imply a preloaded ad.</summary>
        bool IsReady { get; }

        /// <summary>Reserved banner slot — stub does nothing.</summary>
        void ShowBanner(string placementId);
        void HideBanner();

        /// <summary>Request a user-chosen reward; the offline fallback resolves Unavailable.</summary>
        Task<AdResult> ShowRewarded(string placementId);
    }

    /// <summary>
    /// Offline no-op implementation: keeps the game fully playable without
    /// network, reports Unavailable, and never fabricates a reward.
    /// </summary>
    public sealed class AdServiceStub : IAdService
    {
        public bool IsReady => false;

        public void ShowBanner(string placementId)
        {
            UnityEngine.Debug.Log($"[QuietCamp] ads stub: banner '{placementId}' (unavailable)");
        }

        public void HideBanner() { }

        public Task<AdResult> ShowRewarded(string placementId)
        {
            UnityEngine.Debug.Log($"[QuietCamp] ads stub: rewarded '{placementId}' (unavailable)");
            return Task.FromResult(AdResult.Unavailable);
        }
    }
}
