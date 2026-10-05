using System.Linq;
using NUnit.Framework;
using QuietCamp.Infrastructure;

namespace QuietCamp.Tests.Editor
{
    public sealed class LegalConfigurationTests
    {
        static LegalConfiguration Ready() => new LegalConfiguration {
            publisherName = "QA Publisher", supportEmail = "privacy@quietcamp.dev",
            privacyPolicyUrl = "https://quietcamp.dev/privacy", dataRequestUrl = "https://quietcamp.dev/data",
            revision = "1", publishedOn = "2026-10-04", teenPrivacyReviewed = true, sdkDisclosuresReviewed = true };
        [TestCase("http://quietcamp.dev/privacy")]
        [TestCase("https://example.com/privacy")]
        [TestCase("https://www.example.org/privacy")]
        [TestCase("https://localhost/privacy")]
        [TestCase("https://127.0.0.1/privacy")]
        [TestCase("https://10.1.2.3/privacy")]
        [TestCase("https://[2001:db8::1]/privacy")]
        [TestCase("https://quietcamp.dev/Policy.PDF")]
        [TestCase("https://user:secret@quietcamp.dev/privacy")]
        [TestCase("https://quietcamp.dev/%7B%7Bpolicy%7D%7D")]
        [TestCase("")]
        public void DraftAndUnsafeEndpointsAreNotPublished(string url) => Assert.IsFalse(LegalConfiguration.PublishedUrl(url));
        [Test] public void DraftCannotActivateFactoriesEvenIfSdkIsRegistered()
        {
            var analytics = OptionalGoogleServices.AnalyticsFactory; var ads = OptionalGoogleServices.AdsFactory;
            try {
                OptionalGoogleServices.AnalyticsFactory = () => { Assert.Fail("Draft initialized analytics"); return null; };
                OptionalGoogleServices.AdsFactory = _ => { Assert.Fail("Draft initialized ads"); return null; };
                var config = new GoogleServicesConfiguration { analyticsEnabled = true, rewardedAdsEnabled = true,
                    androidRewardedUnitId = "QA", privacyPolicyUrl = "https://quietcamp.dev/privacy", deletionRequestUrl = "https://quietcamp.dev/data" };
                config.ResolveLegal(new LegalConfiguration());
                Assert.IsFalse(config.OnlineServicesApproved);
                Assert.IsFalse(OptionalGoogleServices.Analytics(config).Available);
                Assert.IsFalse(OptionalGoogleServices.Ads(config).IsReady);
            } finally { OptionalGoogleServices.AnalyticsFactory = analytics; OptionalGoogleServices.AdsFactory = ads; }
        }
        [Test] public void MetadataApprovalDoesNotReplaceConsentAndCanBeRevoked()
        {
            var legal = Ready(); var config = new GoogleServicesConfiguration(); config.ResolveLegal(legal);
            Assert.IsTrue(config.OnlineServicesApproved); Assert.IsFalse(config.analyticsEnabled);
            Assert.AreEqual(legal.privacyPolicyUrl, config.privacyPolicyUrl);
            legal.teenPrivacyReviewed = false; Assert.IsFalse(config.OnlineServicesApproved);
            legal.teenPrivacyReviewed = true; legal.sdkDisclosuresReviewed = false; Assert.IsFalse(config.OnlineServicesApproved);
            legal.sdkDisclosuresReviewed = true; legal.minimumAge = 0; Assert.IsFalse(config.OnlineServicesApproved);
        }
        [Test] public void AccountsNeedAnExternalDeletionRouteButOfflineGameDoesNot()
        {
            var legal = Ready(); var google = new GoogleServicesConfiguration();
            Assert.IsEmpty(legal.PublicationIssues(google));
            legal.accountsEnabled = true;
            Assert.IsTrue(legal.PublicationIssues(google).Any(v => v.Contains("public web deletion")));
            legal.accountDeletionUrl = "https://quietcamp.dev/delete-account";
            Assert.IsFalse(legal.PublicationIssues(google).Any(v => v.Contains("public web deletion")));
        }
        [Test] public void OptionalFeaturesNeedTheirDocumentsBeforeAnyOnlineActivation()
        {
            var legal = Ready(); legal.accountsEnabled = true; Assert.IsFalse(legal.OnlineServicesApproved);
            legal.accountDeletionUrl = "https://quietcamp.dev/delete-account"; Assert.IsTrue(legal.OnlineServicesApproved);
            legal.purchasesEnabled = true; Assert.IsFalse(legal.OnlineServicesApproved);
            legal.termsUrl = "https://quietcamp.dev/terms"; Assert.IsTrue(legal.OnlineServicesApproved);
        }
        [Test] public void ContactDoesNotAcceptPlaceholdersHeadersOrDisplayNames()
        {
            Assert.IsFalse(LegalConfiguration.Email("privacy@example.com"));
            Assert.IsFalse(LegalConfiguration.Email("Name <privacy@quietcamp.dev>"));
            Assert.IsFalse(LegalConfiguration.Email("privacy@quietcamp.dev\nBcc:other@quietcamp.dev"));
            Assert.IsTrue(LegalConfiguration.Email("privacy@quietcamp.dev"));
        }
        [Test] public void SourceReleaseTemplateTargetsTeensAndRemainsOffline()
        {
            var legal = LegalConfiguration.Load(); var google = GoogleServicesConfiguration.Load();
            Assert.AreEqual(13, legal.minimumAge); Assert.IsFalse(legal.accountsEnabled);
            Assert.IsFalse(legal.cloudSavesEnabled); Assert.IsFalse(legal.purchasesEnabled);
            Assert.IsFalse(legal.Published); Assert.IsFalse(google.OnlineServicesApproved);
            Assert.IsNotEmpty(legal.PublicationIssues(google));
        }
    }
}
