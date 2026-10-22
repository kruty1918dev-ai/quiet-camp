using System;
using QuietCamp.Application;
using UnityEngine;

namespace QuietCamp.Infrastructure
{
    // Generated allow-listed client snapshot. Owner input stays outside Assets.
    [Serializable] public sealed class OwnerClientConfiguration
    {
        public int schemaVersion = 1;
        public string mode = "development", androidPackageName = "com.quietcamp.game";
        public string proProductId = "quietcamp_pro", verificationEndpoint = "", operationalMetricsEndpoint = "";
        public bool billingReviewed, operationalMetricsReviewed;
        public int firstInterstitialAfterCompletedLevels = 5, completedLevelsBetweenInterstitials = 4;
        public int minimumInterstitialIntervalSeconds = 180, interstitialCooldownAfterRewardSeconds = 180, maximumInterstitialsPerSession = 3;
        public static OwnerClientConfiguration Load()
        {
            var source = Resources.Load<TextAsset>("QuietCamp/owner_client");
            return source == null ? new OwnerClientConfiguration() : JsonUtility.FromJson<OwnerClientConfiguration>(source.text) ?? new OwnerClientConfiguration();
        }
        public bool BillingConfigured(LegalConfiguration legal)
            => schemaVersion == 1 && billingReviewed && legal.Published && LegalConfiguration.PublishedUrl(legal.termsUrl)
                && LegalConfiguration.PublishedUrl(verificationEndpoint)
                && !string.IsNullOrWhiteSpace(proProductId);
        public bool BillingAvailable(LegalConfiguration legal, SettingsSaveData settings)
            => BillingConfigured(legal) && PrivacyEligibility.PurchasesAllowed(settings);
    }

    // Native providers register here without adding SDK references to core asmdefs.
    public static class OptionalBilling
    {
        public static Func<OwnerClientConfiguration, Func<bool>, IPurchaseProvider> Factory;
        public static Func<bool> ClearLocalHistory;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() { Factory = null; ClearLocalHistory = null; }
        public static IPurchaseProvider Create(OwnerClientConfiguration config, LegalConfiguration legal, SettingsSaveData settings)
            => Factory != null && config.BillingConfigured(legal) ? Factory(config, () => config.BillingAvailable(legal, settings)) : new UnavailablePurchaseProvider();
    }
}
