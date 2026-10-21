using System;
using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;
using QuietCamp.Application;
using QuietCamp.Infrastructure;

namespace QuietCamp.Tests.Editor
{
    public sealed class OptionalPrivacyTests
    {
        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(99)]
        public async Task NoFactoryOrRequestBeforeIndependentPermission(int band)
        {
            var settings = new SettingsSaveData { privacyAgeBand = band, privacyAgeVersion = 1 };
            var config = new GoogleServicesConfiguration { useTestAds = true, rewardedAdsEnabled = true };
            config.BindPrivacy(settings, "current");
            int factories = 0;
            var previous = OptionalGoogleServices.AdsFactory;
            try
            {
                OptionalGoogleServices.AdsFactory = _ => { factories++; return new AdServiceStub(); };
                var ads = OptionalGoogleServices.Ads(config);
                Assert.AreEqual(AdResult.Unavailable, await ads.ShowRewarded("camp.life"));
                await ((IAdConsentInitialization)ads).RefreshConsent();
                Assert.Zero(factories);
            }
            finally { OptionalGoogleServices.AdsFactory = previous; }
        }
        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(99)]
        public void RestrictedOrCorruptAgeRejectsEvenForgedCurrentChoices(int band)
        {
            var s = new SettingsSaveData { privacyAgeBand = band, privacyAgeVersion = 1,
                analyticsConsent = true, analyticsPolicyVersion = ComfortAnalytics.PolicyVersion,
                crashReportingConsent = true, crashPolicyVersion = CrashDiagnostics.PolicyVersion,
                advertisingConsent = true, advertisingPolicyVersion = 1, advertisingRevision = "current" };
            Assert.IsFalse(PrivacyEligibility.Allowed(s));
            Assert.IsFalse(PrivacyEligibility.AdvertisingAllowed(s, "current"));
            using var analytics = new ComfortAnalytics(s, new OfflineComfortTransport());
            using var crash = new CrashDiagnostics(s, new OfflineCrashTransport(), () => true, "");
            Assert.IsFalse(analytics.Consented); Assert.IsFalse(crash.Consented);
        }
        [Test] public void AgeChangeAndFailedSaveRevokeAllPermissions()
        {
            var s = Adult(); s.analyticsConsent = s.crashReportingConsent = s.advertisingConsent = true;
            Assert.IsTrue(PrivacyEligibility.ChangeAge(s, PrivacyAgeBand.From16To17, () => true));
            Assert.IsFalse(s.analyticsConsent || s.crashReportingConsent || s.advertisingConsent);
            Assert.IsFalse(PrivacyEligibility.ChangeAge(s, PrivacyAgeBand.Adult, () => false));
            Assert.AreEqual(PrivacyAgeBand.Unknown, PrivacyEligibility.Band(s));
        }
        [Test] public void VideoAvailabilityAndStaleReceiptNeverGrantAdConsent()
        {
            var s = Adult(); s.optionalVideoBonuses = true;
            Assert.IsFalse(PrivacyEligibility.AdvertisingAllowed(s, "current"));
            PrivacyEligibility.SetAdvertising(s, true, "old", () => true);
            Assert.IsFalse(PrivacyEligibility.AdvertisingAllowed(s, "current"));
            Assert.IsFalse(PrivacyEligibility.SetAdvertising(s, true, "current", () => false));
            Assert.IsFalse(PrivacyEligibility.AdvertisingAllowed(s, "current"));
        }
        [Test] public void RestoredBackupAndFailedWriteCannotReviveOldConsent()
        {
            string dir = Path.Combine(Path.GetTempPath(), "qc-receipt-" + Guid.NewGuid().ToString("N"));
            try
            {
                var s = Adult(); PrivacyInstallScope.Validate(s, dir);
                s.privacyAgeBand = 4; s.privacyAgeVersion = 1; s.analyticsConsent = true;
                Assert.IsTrue(PrivacyInstallScope.Commit(s, () => true, dir));
                var old = new SettingsSaveData { privacyAgeBand = 4, privacyAgeVersion = 1,
                    privacyInstallToken = s.privacyInstallToken, analyticsConsent = true };
                Assert.IsFalse(PrivacyInstallScope.Commit(s, () => false, dir));
                PrivacyInstallScope.Validate(old, dir);
                Assert.IsFalse(old.analyticsConsent); Assert.AreEqual(PrivacyAgeBand.Unknown, PrivacyEligibility.Band(old));
                File.Delete(Path.Combine(dir, "install-receipt"));
                old.privacyAgeBand = 4; old.privacyAgeVersion = 1; old.analyticsConsent = true;
                PrivacyInstallScope.Validate(old, dir);
                Assert.IsFalse(old.analyticsConsent);
            }
            finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
        }
        [Test] public async Task RevocationWinsOverPendingRewardAndDisposalDoesNotCreateProvider()
        {
            bool allowed = true; int factories = 0;
            var pending = new PendingAd();
            using (var unused = new PrivacyAdService(() => false, () => false, () => { factories++; return pending; })) { }
            Assert.Zero(factories);
            using var ads = new PrivacyAdService(() => allowed, () => true, () => pending);
            var result = ads.ShowRewarded("camp.life"); allowed = false; ads.CancelPending(); allowed = true;
            pending.Result.SetResult(AdResult.Completed);
            Assert.AreEqual(AdResult.Unavailable, await result, "Re-enabling cannot revive a revoked reward request.");
        }
        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)]
        public async Task RequiredPrivacyOptionsAfterRestartNeedEligibleExplicitAction(int age)
        {
            bool eligible = age >= 3; int factories = 0;
            var provider = new PendingAd();
            using var ads = new PrivacyAdService(() => false, () => eligible,
                () => { factories++; return provider; }, () => true);
            Assert.AreEqual(eligible, ads.PrivacyOptionsRequired);
            await ads.RefreshConsent(); Assert.Zero(factories, "The availability hint cannot start UMP.");
            await ads.ShowPrivacyOptions(); Assert.AreEqual(eligible ? 1 : 0, factories);
            Assert.AreEqual(eligible ? 1 : 0, provider.Options);
            Assert.IsFalse(ads.IsReady, "Managing Google choices does not grant the game's ad consent.");
        }
        [TestCase(true)] [TestCase(false)]
        public async Task PublishedSinglePurposeConsentDoesNotCreateOtherFirebaseTransport(bool analyticsChoice)
        {
            var s = Adult();
            s.analyticsConsent = analyticsChoice; s.analyticsPolicyVersion = ComfortAnalytics.PolicyVersion;
            s.analyticsPublicationRevision = "current";
            s.crashReportingConsent = !analyticsChoice; s.crashPolicyVersion = CrashDiagnostics.PolicyVersion;
            s.crashPublicationRevision = "current";
            var c = new GoogleServicesConfiguration { analyticsEnabled = true, crashlyticsEnabled = true };
            c.ResolveLegal(new LegalConfiguration { policyIsDraft = false, publisherName = "QA", supportEmail = "privacy@quietcamp.dev",
                privacyPolicyUrl = "https://quietcamp.dev/privacy", dataRequestUrl = "https://quietcamp.dev/data",
                revision = "current", publishedOn = "2026-10-21", teenPrivacyReviewed = true, sdkDisclosuresReviewed = true });
            c.BindPrivacy(s, "current");
            var oldAnalytics = OptionalGoogleServices.AnalyticsFactory; var oldCrash = OptionalGoogleServices.CrashFactory;
            int analyticsFactories = 0, crashFactories = 0;
            try
            {
                OptionalGoogleServices.AnalyticsFactory = () => { analyticsFactories++; return new OfflineComfortTransport(); };
                OptionalGoogleServices.CrashFactory = () => { crashFactories++; return new OfflineCrashTransport(); };
                using var analytics = new ComfortAnalytics(s, OptionalGoogleServices.Analytics(c), publicationRevision: "current");
                using var crash = new CrashDiagnostics(s, OptionalGoogleServices.Crashes(c), () => true, "current");
                await analytics.RestoreConsent(); await crash.RestoreConsent();
                Assert.AreEqual(analyticsChoice ? 1 : 0, analyticsFactories);
                Assert.AreEqual(analyticsChoice ? 0 : 1, crashFactories);
            }
            finally { OptionalGoogleServices.AnalyticsFactory = oldAnalytics; OptionalGoogleServices.CrashFactory = oldCrash; }
        }
        static SettingsSaveData Adult() => new SettingsSaveData { privacyAgeBand = 4, privacyAgeVersion = 1 };
        sealed class PendingAd : IAdService, IAdPrivacyOptions
        {
            public int Options;
            public bool PrivacyOptionsRequired => true;
            public Task ShowPrivacyOptions() { Options++; return Task.CompletedTask; }
            public TaskCompletionSource<AdResult> Result = new TaskCompletionSource<AdResult>();
            public bool IsReady => true;
            public Task<AdResult> ShowRewarded(string placementId) => Result.Task;
            public void ShowBanner(string placementId) { }
            public void HideBanner() { }
        }
    }
}
