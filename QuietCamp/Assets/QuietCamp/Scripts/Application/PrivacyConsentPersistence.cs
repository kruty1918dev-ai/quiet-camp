using System;

namespace QuietCamp.Application
{
    // The session-wide persistence boundary: a failed decision resets all
    // permissions and stops already initialized transports without saving again.
    public sealed class PrivacyConsentPersistence
    {
        readonly SettingsSaveData _settings;
        readonly Func<bool> _write;
        ComfortAnalytics _analytics;
        CrashDiagnostics _crashes;
        IAdService _ads;

        public PrivacyConsentPersistence(SettingsSaveData settings, Func<bool> write)
        { _settings = settings; _write = write; }

        public void Attach(ComfortAnalytics analytics, CrashDiagnostics crashes, IAdService ads)
        { _analytics = analytics; _crashes = crashes; _ads = ads; }

        public bool Save()
        {
            // Age changes revoke the model before invoking this boundary.
            // Apply that revocation before attempting disk IO as well.
            if (_analytics != null && !_analytics.Consented) _analytics.Suspend();
            if (_crashes != null && !_crashes.Consented) _crashes.Suspend();
            if (!_settings.optionalVideoBonuses || !_settings.advertisingConsent || !PrivacyEligibility.Allowed(_settings)) CancelAds();
            try { if (_write()) return true; } catch { }
            PrivacyEligibility.Reset(_settings);
            SuspendAll();
            return false;
        }

        public void SuspendAll()
        {
            // Each service contains its transport failures; one failing SDK
            // must never prevent revocation of the other purposes.
            _analytics?.Suspend();
            _crashes?.Suspend();
            CancelAds();
        }

        void CancelAds()
        { try { (_ads as IAdRequestCancellation)?.CancelPending(); } catch { } }
    }
}
