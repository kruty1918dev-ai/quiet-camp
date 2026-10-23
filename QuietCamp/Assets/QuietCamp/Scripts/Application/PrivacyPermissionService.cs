using System;

namespace QuietCamp.Application
{
    public struct PrivacyPermissionInputs
    {
        public string Context;
        public bool AnalyticsAvailable, DiagnosticsAvailable, AdvertisingAvailable, PurchasesAvailable;
        public bool AnalyticsChoice, DiagnosticsChoice, AdvertisingChoice, RestrictedAdvertising;
        public bool Adult, AdultPersonalizationReviewed, Pro;
    }

    public readonly struct PrivacyPermissions
    {
        public readonly bool AnalyticsAllowed, DiagnosticsAllowed, CanStartAdConsent, PurchasesAllowed;
        public readonly bool FreshUmp, CanRequestAds, AdStorage, AdUserData, AdPersonalization;
        public PrivacyPermissions(PrivacyPermissionInputs input, bool fresh, bool request, string mode)
        {
            AnalyticsAllowed = input.AnalyticsAvailable && input.AnalyticsChoice;
            DiagnosticsAllowed = input.DiagnosticsAvailable && input.DiagnosticsChoice;
            PurchasesAllowed = input.PurchasesAvailable;
            CanStartAdConsent = input.AdvertisingAvailable && !input.Pro && (input.AdvertisingChoice || input.RestrictedAdvertising);
            FreshUmp = fresh;
            CanRequestAds = CanStartAdConsent && fresh && request;
            // UMP 3.2+/4: ad_storage, ad_user_data, ad_personalization,
            // analytics_storage. Only 1 means GRANTED. 0/2/3/4 are not grants.
            bool valid = mode != null && mode.Length == 4;
            if (valid) foreach (char digit in mode) if (digit < '0' || digit > '4') { valid = false; break; }
            bool advertising = CanRequestAds && input.Adult && valid;
            AdStorage = advertising && mode[0] == '1';
            AdUserData = advertising && mode[1] == '1';
            AdPersonalization = advertising && input.AdultPersonalizationReviewed && AdStorage && AdUserData && mode[2] == '1';
            // The fourth UMP digit never grants Analytics: that console mode is
            // disabled, and the game's independent Analytics choice is authoritative.
        }
    }

    /// <summary>Session-owned decisions. Adapters project this state; no SDK or persistent UMP cache here.</summary>
    public sealed class PrivacyPermissionService
    {
        readonly Func<PrivacyPermissionInputs> _inputs;
        string _umpContext, _mode;
        bool _fresh, _canRequest;
        public long AdvertisingEpoch { get; private set; }
        public event Action Changed;
        public PrivacyPermissionService(Func<PrivacyPermissionInputs> inputs) { _inputs = inputs ?? throw new ArgumentNullException(nameof(inputs)); }
        public PrivacyPermissions Decisions
        {
            get {
                var input = _inputs();
                return new PrivacyPermissions(input, _fresh && input.Context == _umpContext, _canRequest, _mode);
            }
        }
        public void InvalidateAdvertising()
        { AdvertisingEpoch++; _fresh = _canRequest = false; _mode = _umpContext = null; Refresh(); }
        public void AcceptUmp(bool canRequest, string modeValues)
        { _umpContext = _inputs().Context; _fresh = true; _canRequest = canRequest; _mode = modeValues; Refresh(); }
        public void Refresh() { try { Changed?.Invoke(); } catch { } }
    }
}
