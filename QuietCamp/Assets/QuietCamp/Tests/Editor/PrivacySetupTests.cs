using System;
using NUnit.Framework;
using QuietCamp.Application;
using QuietCamp.Infrastructure;

namespace QuietCamp.Tests.Editor
{
    public sealed class PrivacySetupTests
    {
        static PrivacySetup Adult(bool analytics = true, bool crashes = true, bool ads = true)
        {
            var flow = new PrivacySetup(analytics, crashes, ads);
            flow.ReadPolicy(); flow.SelectAge(PrivacyAgeBand.Adult); flow.ConfirmAge(); return flow;
        }
        [Test] public void PolicyAndExplicitNeutralAgeChoicePrecedePurposeDecisions()
        {
            var flow = new PrivacySetup(true, true, true);
            flow.Choose(true); flow.SelectAge(PrivacyAgeBand.Adult); flow.ConfirmAge();
            Assert.AreEqual(PrivacySetupStep.Policy, flow.Step);
            flow.ReadPolicy(); flow.ConfirmAge();
            Assert.AreEqual(PrivacySetupStep.Age, flow.Step); Assert.IsFalse(flow.HasAgeChoice);
            flow.SelectAge(PrivacyAgeBand.Unknown); flow.ConfirmAge();
            Assert.AreEqual(PrivacySetupStep.Review, flow.Step);
        }
        [TestCase(0)] [TestCase(1)] [TestCase(2)]
        public void YoungerOrUnknownAgeDoesNotConsentToTracking(int age)
        {
            var flow = new PrivacySetup(true, true, true); var settings = new SettingsSaveData();
            flow.ReadPolicy(); flow.SelectAge((PrivacyAgeBand)age); flow.ConfirmAge();
            Assert.AreEqual(PrivacySetupStep.Review, flow.Step); Assert.IsTrue(flow.CanPlay);
            flow.Choose(true);
            Assert.IsTrue(flow.Commit(settings, "r", "hash", "uk", true, () => true));
            Assert.IsTrue(PrivacySetup.IsComplete(settings, "r", "hash", true));
            Assert.IsFalse(settings.analyticsConsent || settings.crashReportingConsent || settings.advertisingConsent);
            Assert.IsFalse(PrivacyEligibility.Allowed(settings));
            PrivacyEligibility.Reset(settings);
            Assert.IsFalse(PrivacySetup.IsComplete(settings, "r", "hash", true), "Restored backups must repeat decisions.");
        }
        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)]
        [TestCase(4)] [TestCase(5)] [TestCase(6)] [TestCase(7)]
        public void AdultPurposesAreIndependentAndRecordedOnlyAtFinalCommit(int mask)
        {
            var flow = Adult(); var settings = new SettingsSaveData(); int saves = 0;
            Assert.AreEqual(PrivacySetupStep.Advertising, flow.Step); flow.Choose((mask & 4) != 0);
            Assert.AreEqual(PrivacySetupStep.Analytics, flow.Step); flow.Choose((mask & 1) != 0);
            Assert.AreEqual(PrivacySetupStep.Crashes, flow.Step); flow.Choose((mask & 2) != 0);
            Assert.AreEqual(PrivacySetupStep.Review, flow.Step); Assert.IsFalse(settings.analyticsConsent);
            Assert.IsTrue(flow.Commit(settings, "r", "hash", "de", false, () => { saves++; return true; }));
            Assert.AreEqual(1, saves); Assert.AreEqual((mask & 1) != 0, settings.analyticsConsent);
            Assert.AreEqual((mask & 2) != 0, settings.crashReportingConsent);
            Assert.AreEqual((mask & 4) != 0, settings.advertisingConsent); Assert.IsFalse(settings.crashSessionEligible);
            Assert.IsFalse(PrivacySetup.IsComplete(settings, "changed", "hash", false));
            Assert.IsFalse(PrivacySetup.IsComplete(settings, "r", "changed", false));
        }
        [Test] public void ClosedReleaseServicesAreSkippedWithoutSeekingFictitiousConsent()
        {
            var flow = Adult(false, false, false);
            Assert.IsFalse(flow.HasOptionalServices); Assert.AreEqual(PrivacySetupStep.Review, flow.Step);
            var settings = new SettingsSaveData();
            Assert.IsTrue(flow.Commit(settings, "r", "hash", "en", true, () => true));
            Assert.IsFalse(settings.analyticsConsent || settings.crashReportingConsent || settings.advertisingConsent);
        }
        [TestCase(false)] [TestCase(true)]
        public void FailureCannotCompleteFlowOrRetainAnyPermission(bool throws)
        {
            var flow = Adult(); flow.Choose(true); flow.Choose(true); flow.Choose(true);
            var settings = new SettingsSaveData();
            Assert.IsFalse(flow.Commit(settings, "r", "hash", "en", true, () => { if (throws) throw new Exception("test"); return false; }));
            Assert.IsFalse(settings.analyticsConsent || settings.crashReportingConsent || settings.advertisingConsent);
            Assert.IsFalse(flow.Analytics || flow.Crashes || flow.Advertising);
            Assert.IsFalse(PrivacySetup.IsComplete(settings, "r", "hash", true));
        }
        [Test] public void BackAndSkipDiscardPendingGrants()
        {
            var flow = Adult(); flow.Choose(true); flow.Back();
            Assert.AreEqual(PrivacySetupStep.Age, flow.Step); Assert.IsFalse(flow.Analytics);
            flow.SelectAge(PrivacyAgeBand.Adult); flow.ConfirmAge(); flow.Choose(true); flow.ContinueWithoutOptional();
            Assert.AreEqual(PrivacySetupStep.Review, flow.Step); Assert.IsFalse(flow.Analytics || flow.Crashes || flow.Advertising);
        }
        [Test] public void Age16To17CanChooseAdvertisingWithoutAnalyticsOrCrashes()
        {
            var flow = new PrivacySetup(true, true, true); var settings = new SettingsSaveData();
            flow.ReadPolicy(); flow.SelectAge(PrivacyAgeBand.From16To17); flow.ConfirmAge();
            Assert.AreEqual(PrivacySetupStep.Advertising, flow.Step);
            flow.Choose(true); flow.Choose(false); flow.Choose(false);
            Assert.IsTrue(flow.Commit(settings, "r", "h", "en", true, () => true));
            Assert.IsTrue(PrivacyEligibility.AdvertisingAllowed(settings, "r"));
            Assert.IsFalse(settings.analyticsConsent || settings.crashReportingConsent);
        }
        [Test] public void OldAcknowledgementIsNotCompletedOnboarding()
        {
            var settings = new SettingsSaveData(); PrivacyAcknowledgement.Record(settings, "r", "h", "en", true);
            Assert.IsFalse(PrivacySetup.IsComplete(settings, "r", "h", true));
            Assert.IsFalse(new PrivacySetup(true, true, true).Commit(settings, "r", "h", "en", true, () => true));
        }
    }
}
