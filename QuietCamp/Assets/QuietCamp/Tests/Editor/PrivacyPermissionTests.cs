using NUnit.Framework;
using QuietCamp.Application;

namespace QuietCamp.Tests.Editor
{
    public sealed class PrivacyPermissionTests
    {
        static PrivacyPermissionInputs Adult() => new PrivacyPermissionInputs {
            Context = "revision:adult", Adult = true, AdvertisingAvailable = true,
            AdvertisingChoice = true, AdultPersonalizationReviewed = true,
            AnalyticsAvailable = true, DiagnosticsAvailable = true, PurchasesAvailable = true };

        [TestCase(null)] [TestCase("")] [TestCase("111")] [TestCase("11111")]
        [TestCase("abcd")] [TestCase("4444")] [TestCase("0000")] [TestCase("2222")] [TestCase("3333")]
        public void MissingUnknownOrNonGrantedUmpDoesNotGrantPurposes(string mode)
        {
            var input = Adult(); var service = new PrivacyPermissionService(() => input);
            service.AcceptUmp(true, mode);
            Assert.IsTrue(service.Decisions.CanRequestAds);
            Assert.IsFalse(service.Decisions.AdStorage || service.Decisions.AdUserData || service.Decisions.AdPersonalization || service.Decisions.AnalyticsAllowed);
        }
        [Test] public void IndependentChoicesAndUmpCannotOverwriteEachOther()
        {
            var input = Adult(); input.AnalyticsChoice = true;
            var service = new PrivacyPermissionService(() => input);
            Assert.IsTrue(service.Decisions.AnalyticsAllowed);
            Assert.IsFalse(service.Decisions.CanRequestAds);
            service.AcceptUmp(true, "1114");
            Assert.IsTrue(service.Decisions.AdStorage && service.Decisions.AdUserData && service.Decisions.AdPersonalization);
            input.AnalyticsChoice = false; service.Refresh();
            Assert.IsFalse(service.Decisions.AnalyticsAllowed);
            Assert.IsTrue(service.Decisions.AdStorage && service.Decisions.PurchasesAllowed);
            input.AdvertisingChoice = false; input.AnalyticsChoice = true;
            Assert.IsFalse(service.Decisions.CanRequestAds || service.Decisions.AdStorage);
            Assert.IsTrue(service.Decisions.AnalyticsAllowed);
        }
        [Test] public void ContextChangesFailuresAndProRevokeAdvertising()
        {
            var input = Adult(); var service = new PrivacyPermissionService(() => input);
            service.AcceptUmp(true, "1114"); input.Context = "new-revision";
            Assert.IsFalse(service.Decisions.FreshUmp || service.Decisions.AdStorage);
            service.AcceptUmp(true, "1114"); input.Pro = true;
            Assert.IsFalse(service.Decisions.CanStartAdConsent || service.Decisions.CanRequestAds || service.Decisions.AdStorage);
            input.Pro = false; service.InvalidateAdvertising();
            Assert.IsFalse(service.Decisions.FreshUmp || service.Decisions.CanRequestAds);
            service.AcceptUmp(false, "1111");
            Assert.IsFalse(service.Decisions.AdPersonalization || service.Decisions.AnalyticsAllowed);
        }
        [Test] public void RestrictedAndTeenPathsNeverGrantAdvertisingPurposes()
        {
            var input = Adult(); input.Adult = false; input.AdvertisingChoice = false; input.RestrictedAdvertising = true;
            var service = new PrivacyPermissionService(() => input); service.AcceptUmp(true, "1111");
            Assert.IsTrue(service.Decisions.CanRequestAds);
            Assert.IsFalse(service.Decisions.AdStorage || service.Decisions.AdUserData || service.Decisions.AdPersonalization || service.Decisions.AnalyticsAllowed);
            input.RestrictedAdvertising = false;
            Assert.IsFalse(service.Decisions.CanStartAdConsent);
        }
        [Test] public void PersonalizationRequiresOwnerReviewAndAllRequiredGrants()
        {
            var input = Adult(); var service = new PrivacyPermissionService(() => input);
            service.AcceptUmp(true, "1214"); Assert.IsFalse(service.Decisions.AdPersonalization);
            service.AcceptUmp(true, "1114"); input.AdultPersonalizationReviewed = false;
            Assert.IsFalse(service.Decisions.AdPersonalization);
        }
    }
}
