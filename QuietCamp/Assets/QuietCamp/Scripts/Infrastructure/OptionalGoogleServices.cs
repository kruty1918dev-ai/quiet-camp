using System;
using QuietCamp.Application;
using UnityEngine;

namespace QuietCamp.Infrastructure
{
    [Serializable]
    public sealed class GoogleServicesConfiguration
    {
        public bool analyticsEnabled;
        public bool rewardedAdsEnabled;
        public string privacyPolicyUrl = "";
        public string deletionRequestUrl = "";
        public string androidRewardedUnitId = "";
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
        public static Func<GoogleServicesConfiguration, IAdService> AdsFactory;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() { AnalyticsFactory = null; AdsFactory = null; }
        public static IComfortTransport Analytics(GoogleServicesConfiguration config)
            => config.analyticsEnabled && config.OnlineServicesApproved && AnalyticsFactory != null ? AnalyticsFactory() : new OfflineComfortTransport();
        public static IAdService Ads(GoogleServicesConfiguration config)
            => config.rewardedAdsEnabled && config.OnlineServicesApproved && !string.IsNullOrWhiteSpace(config.androidRewardedUnitId) && AdsFactory != null ? AdsFactory(config) : new AdServiceStub();
    }
}
