using NUnit.Framework;
using QuietCamp.Application;
using UnityEngine;
using Newtonsoft.Json.Linq;

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
        [TestCase("uk")]
        [TestCase("en")]
        [TestCase("de")]
        public void BundledPolicyAndPrivacyLabelsAreReadableAndVersioned(string language)
        {
            var publication = JObject.Parse(Resources.Load<TextAsset>("QuietCamp/privacy_content").text);
            var variants = (JArray)publication["document"]["variants"];
            var variant = System.Linq.Enumerable.Single(variants, item => (string)item["language"] == language);
            string body = (string)variant["bundledBody"];
            Assert.Greater(body.Length, 10000);
            Assert.IsNotEmpty((string)variant["revision"]);
            Assert.IsTrue(QuietCamp.Infrastructure.LegalConfiguration.PublishedUrl((string)variant["sourceUrl"]));
            var locale = JObject.Parse(Resources.Load<TextAsset>("QuietCampLocales/" + language).text);
            foreach (string key in new[] { "legal.section.crashes", "privacy.crashes.consent", "privacy.crashes.explanation", "privacy.contact" })
            {
                string label = (string)locale["entries"][key];
                Assert.IsNotEmpty(label, key); Assert.IsFalse(label.Contains("??"), key);
                Assert.IsFalse(label.Contains("\uFFFD"), key);
            }
            foreach (var summary in publication["summaries"])
            {
                if ((string)summary["language"] != language) continue;
                Assert.AreEqual((string)variant["revision"], (string)summary["revision"]);
                Assert.IsFalse(((string)summary["text"]).Contains("??"));
            }
        }
    }
}
