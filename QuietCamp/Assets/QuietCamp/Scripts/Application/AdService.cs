using System.Threading.Tasks;
namespace QuietCamp.Application
{
    /// <summary>
    /// Result of an ad request. Ads are an optional future monetization slot;
    /// the MVP ships with a disabled stub — never a fake "reward" UI.
    /// </summary>
    public enum AdResult
    {
        Unavailable,
        Completed,
        Skipped,
        Failed
    }

    /// <summary>
    /// Monetization boundary. Templates for a later rewarded-ad / banner
    /// integration: implement with a real SDK adapter, keep this surface
    /// stable so gameplay code never sees SDK types.
    /// </summary>
    public interface IAdService
    {
        /// <summary>False while no SDK is configured (offline MVP default).</summary>
        bool IsReady { get; }

        /// <summary>Reserved banner slot — stub does nothing.</summary>
        void ShowBanner(string placementId);
        void HideBanner();

        /// <summary>Reserved rewarded slot — stub resolves Unavailable.</summary>
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
