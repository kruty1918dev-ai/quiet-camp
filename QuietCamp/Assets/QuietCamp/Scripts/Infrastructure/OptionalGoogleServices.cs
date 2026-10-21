using System;
using QuietCamp.Application;
using UnityEngine;

namespace QuietCamp.Infrastructure
{
    [Serializable]
    public sealed class GoogleServicesConfiguration
    {
        public bool analyticsEnabled;
        public bool crashlyticsEnabled;
        public bool rewardedAdsEnabled;
        public bool useTestAds;
        public string privacyPolicyUrl = "";
        public string deletionRequestUrl = "";
        public string androidRewardedUnitId = "";
        public string iosRewardedUnitId = "";
        public const string AndroidTestRewardedUnitId = "ca-app-pub-3940256099942544/5224354917";
        public const string IosTestRewardedUnitId = "ca-app-pub-3940256099942544/1712485313";
        public bool TestAdsActive => useTestAds && (UnityEngine.Application.isEditor || Debug.isDebugBuild);
        [NonSerialized] public SettingsSaveData PrivacySettings;
        [NonSerialized] public string ConsentRevision;
        [NonSerialized] public Action<bool> RememberPrivacyOptions;
        public void BindPrivacy(SettingsSaveData settings, string revision) { PrivacySettings = settings; ConsentRevision = revision; }
        public bool Eligible => PrivacyEligibility.Allowed(PrivacySettings);
        public bool AnalyticsAllowed => Eligible && analyticsEnabled && OnlineServicesApproved && PrivacySettings.analyticsConsent
            && PrivacySettings.analyticsPolicyVersion == ComfortAnalytics.PolicyVersion && PrivacySettings.analyticsPublicationRevision == ConsentRevision;
        public bool CrashesAllowed => Eligible && crashlyticsEnabled && OnlineServicesApproved && PrivacySettings.crashReportingConsent
            && PrivacySettings.crashPolicyVersion == CrashDiagnostics.PolicyVersion && PrivacySettings.crashPublicationRevision == ConsentRevision;
        public bool AdvertisingAllowed => PrivacyEligibility.AdvertisingAllowed(PrivacySettings, ConsentRevision)
            && PrivacySettings.optionalVideoBonuses && CanUseRewardedAds(UnityEngine.Application.isEditor || Debug.isDebugBuild);
        // A prototype can exercise Google sample inventory before publication.
        // Release players still require the normal publication and consent gates.
        public bool CanUseRewardedAds(bool developmentEnvironment)
            => rewardedAdsEnabled && (useTestAds ? developmentEnvironment : OnlineServicesApproved);
        public string RewardedUnitId(bool ios)
            => useTestAds ? (ios ? IosTestRewardedUnitId : AndroidTestRewardedUnitId)
                : (ios ? iosRewardedUnitId : androidRewardedUnitId);
        public string CurrentRewardedUnitId => RewardedUnitId(UnityEngine.Application.platform == RuntimePlatform.IPhonePlayer);
        // Enabling collection also requires published disclosures and a way to
        // request server-side deletion. Empty templates are intentionally inert.
        public bool PrivacyConfigured => Https(privacyPolicyUrl) && Https(deletionRequestUrl);
        LegalConfiguration _legal;
        public bool OnlineServicesApproved => _legal != null && _legal.OnlineServicesApproved && PrivacyConfigured;
        static bool Https(string value) => LegalConfiguration.PublishedUrl(value);
        public static GoogleServicesConfiguration Load(LegalConfiguration publication = null)
        {
            var json = Resources.Load<TextAsset>("QuietCamp/google_services");
            var config = json != null ? JsonUtility.FromJson<GoogleServicesConfiguration>(json.text) ?? new GoogleServicesConfiguration() : new GoogleServicesConfiguration();
            var legal = publication ?? LegalConfiguration.Load();
            config.ResolveLegal(legal);
            return config;
        }
        public void ResolveLegal(LegalConfiguration legal)
        {
            _legal = legal;
            // Preserve existing keys, with the central publication config taking precedence.
            if (legal != null && !string.IsNullOrWhiteSpace(legal.privacyPolicyUrl)) privacyPolicyUrl = legal.privacyPolicyUrl;
            if (legal != null && !string.IsNullOrWhiteSpace(legal.dataRequestUrl)) deletionRequestUrl = legal.dataRequestUrl;
        }
    }

    /// <summary>Adapters register by compile-time SDK availability before Boot.
    /// The core assemblies never load an SDK by reflection or require sign-in.</summary>
    public static class OptionalGoogleServices
    {
        public static Func<IComfortTransport> AnalyticsFactory;
        public static Func<ICrashTransport> CrashFactory;
        public static Func<System.Threading.Tasks.Task<bool>> FirebaseInitialization;
        public static Func<GoogleServicesConfiguration, IAdService> AdsFactory;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() { AnalyticsFactory = null; AdsFactory = null; CrashFactory = null; FirebaseInitialization = null; }
        public static System.Threading.Tasks.Task<bool> InitializeFirebase(GoogleServicesConfiguration config)
            => (config.AnalyticsAllowed || config.CrashesAllowed)
                && FirebaseInitialization != null ? FirebaseInitialization() : System.Threading.Tasks.Task.FromResult(false);
        public static ICrashTransport Crashes(GoogleServicesConfiguration config)
            => new PrivacyCrashTransport(() => config.Eligible && config.crashlyticsEnabled && config.OnlineServicesApproved && CrashFactory != null,
                () => config.CrashesAllowed, () => CrashFactory());
        public static IComfortTransport Analytics(GoogleServicesConfiguration config)
            => new PrivacyComfortTransport(() => config.Eligible && config.analyticsEnabled && config.OnlineServicesApproved && AnalyticsFactory != null,
                () => config.AnalyticsAllowed, () => AnalyticsFactory());
        public static IAdService Ads(GoogleServicesConfiguration config)
            => new PrivacyAdService(() => config.AdvertisingAllowed
                && !string.IsNullOrWhiteSpace(config.CurrentRewardedUnitId) && AdsFactory != null,
                () => config.Eligible, () => AdsFactory(config),
                () => !config.useTestAds && config.PrivacySettings.googlePrivacyOptionsRequired && AdsFactory != null && config.CanUseRewardedAds(UnityEngine.Debug.isDebugBuild || UnityEngine.Application.isEditor));
    }
}
