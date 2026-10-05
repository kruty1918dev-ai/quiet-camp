using NUnit.Framework;
using QuietCamp.Application;

namespace QuietCamp.Tests.Editor
{
    public sealed class PrivacyAcknowledgementTests
    {
        [Test] public void OldSaveAndRefusalHaveNoAcknowledgement()
        { Assert.IsFalse(PrivacyAcknowledgement.IsCurrent(new SettingsSaveData(), "r1", "hash", true)); }
        [Test] public void ReceiptIsSpecificToPublicationContentAndDraftMode()
        {
            var settings = new SettingsSaveData(); PrivacyAcknowledgement.Record(settings, "r1", "hash", "uk", true);
            Assert.IsTrue(PrivacyAcknowledgement.IsCurrent(settings, "r1", "hash", true));
            Assert.IsFalse(PrivacyAcknowledgement.IsCurrent(settings, "r2", "hash", true));
            Assert.IsFalse(PrivacyAcknowledgement.IsCurrent(settings, "r1", "edited", true));
            Assert.IsFalse(PrivacyAcknowledgement.IsCurrent(settings, "r1", "hash", false));
            Assert.IsNotEmpty(settings.privacyAcknowledgedUtc); Assert.AreEqual("uk", settings.privacyAcknowledgementLanguage);
        }
        [Test] public void AcknowledgementCannotEnableAnalyticsOrChangeRewards()
        {
            var settings = new SettingsSaveData { analyticsConsent = false, optionalVideoBonuses = false };
            PrivacyAcknowledgement.Record(settings, "r1", "hash", "en", false);
            Assert.IsFalse(settings.analyticsConsent); Assert.AreEqual(0, settings.analyticsPolicyVersion);
            Assert.IsNull(settings.analyticsPublicationRevision); Assert.IsFalse(settings.optionalVideoBonuses);
        }
    }
}
