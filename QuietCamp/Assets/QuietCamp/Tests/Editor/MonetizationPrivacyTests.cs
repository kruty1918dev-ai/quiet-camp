using System;
using System.Threading.Tasks;
using NUnit.Framework;
using QuietCamp.Application;
using QuietCamp.Infrastructure;

namespace QuietCamp.Tests.Editor
{
    public sealed class MonetizationPrivacyTests
    {
        static readonly DateTime Start = new DateTime(2026, 10, 21, 12, 0, 0, DateTimeKind.Utc);
        [Test] public void OldEmptySaveStartsARealTimerWithoutInstantFreeLife()
        {
            var data = new EconomySaveData { initialized = true, lives = 0 };
            var economy = new CampEconomy(data, new EconomyRules(), () => true, () => Start);
            Assert.AreEqual(EconomyResult.Applied, economy.RefreshLives()); Assert.Zero(economy.Lives);
            Assert.AreEqual(1800, economy.SecondsUntilNextLife); Assert.IsFalse(economy.CanCheck);
            Assert.AreEqual(EconomyResult.AlreadyApplied, economy.RefreshLives()); Assert.Zero(economy.Lives);
        }
        [Test] public void ThirtyMinuteRecoveryCapsAtFiveAndCannotBankTimeWhileFull()
        {
            var now = Start; var data = new EconomySaveData();
            var economy = new CampEconomy(data, new EconomyRules(), () => true, () => now);
            Assert.AreEqual(EconomyResult.Applied, economy.RecordCheck("level", false)); Assert.AreEqual(4, economy.Lives);
            now = Start.AddMinutes(29); Assert.AreEqual(EconomyResult.AlreadyApplied, economy.RefreshLives());
            now = Start.AddMinutes(30); Assert.AreEqual(EconomyResult.Applied, economy.RefreshLives()); Assert.AreEqual(5, economy.Lives);
            now = Start.AddDays(3); Assert.AreEqual(EconomyResult.Unavailable, economy.RefreshLives());
            economy.RecordCheck("level", false); economy.RefreshLives(); Assert.AreEqual(4, economy.Lives);
            Assert.AreEqual(1800, economy.SecondsUntilNextLife);
        }
        [Test] public void OfflineRecoveryPreservesRemainderAndDoesNotClampBoughtOrAdLives()
        {
            var now = Start.AddMinutes(95); var data = new EconomySaveData { initialized = true, lives = 0, lifeRecoveryAnchorUtc = Start.ToString("O") };
            var economy = new CampEconomy(data, new EconomyRules(), () => true, () => now);
            economy.RefreshLives(); Assert.AreEqual(3, economy.Lives); Assert.AreEqual(25 * 60, economy.SecondsUntilNextLife);
            now = now.AddDays(2); economy.RefreshLives(); Assert.AreEqual(5, economy.Lives);
            economy.GrantAdLife("earned-a"); economy.GrantAdLife("earned-b"); economy.RefreshLives();
            Assert.AreEqual(7, economy.Lives); Assert.Zero(economy.SecondsUntilNextLife);
        }
        [TestCase(false)] [TestCase(true)] public void RecoveryFailureRollsBackLifeAndAnchor(bool throws)
        {
            bool fail = true; var data = new EconomySaveData { initialized = true, lives = 0, lifeRecoveryAnchorUtc = Start.ToString("O") };
            var economy = new CampEconomy(data, new EconomyRules(), () => { if (fail && throws) throw new Exception("test"); return !fail; }, () => Start.AddMinutes(31));
            Assert.AreEqual(EconomyResult.SaveFailed, economy.RefreshLives()); Assert.Zero(economy.Lives);
            Assert.AreEqual(Start.ToString("O"), data.lifeRecoveryAnchorUtc);
            fail = false; economy.RefreshLives(); Assert.AreEqual(1, economy.Lives);
        }
        [Test] public void ClockRollbackRestartsWaitingWithoutGrantingALife()
        {
            var data = new EconomySaveData { initialized = true, lives = 0, lifeRecoveryAnchorUtc = Start.AddYears(10).ToString("O") };
            var economy = new CampEconomy(data, new EconomyRules(), () => true, () => Start);
            economy.RefreshLives(); Assert.Zero(economy.Lives); Assert.AreEqual(1800, economy.SecondsUntilNextLife);
        }
        [Test] public void HintRewardsHaveIndependentDailyQuotaAndIdempotentReceipts()
        {
            var now = Start; var data = new EconomySaveData(); var economy = new CampEconomy(data, new EconomyRules(), () => true, () => now);
            for (int i = 0; i < 5; i++) Assert.AreEqual(EconomyResult.Applied, economy.GrantAdHint("hint-" + i));
            Assert.AreEqual(8, economy.Hints); Assert.AreEqual(10, economy.AdLivesRemaining); Assert.Zero(economy.AdHintsRemaining);
            Assert.AreEqual(EconomyResult.AlreadyApplied, economy.GrantAdHint("hint-0"));
            Assert.AreEqual(EconomyResult.DailyLimit, economy.GrantAdHint("hint-next"));
            economy.GrantAdLife("life-0"); Assert.AreEqual(9, economy.AdLivesRemaining);
            now = now.AddDays(-1); Assert.Zero(economy.AdHintsRemaining);
            now = Start.AddDays(1); economy.GrantAdHint("next-day"); Assert.AreEqual(10, economy.AdLivesRemaining); Assert.AreEqual(4, economy.AdHintsRemaining);
        }
        [Test] public async Task CompletedHintRetriesFailedPersistenceWithoutAnotherAd()
        {
            bool save = false; var economy = new CampEconomy(new EconomySaveData(), new EconomyRules(), () => save);
            var ads = new Ad(); var service = new AdHintService(ads, economy, () => true);
            Assert.AreEqual(EconomyResult.SaveFailed, await service.Request()); Assert.AreEqual(1, ads.Requests); Assert.AreEqual(3, economy.Hints);
            save = true; Assert.AreEqual(EconomyResult.Applied, await service.Request()); Assert.AreEqual(1, ads.Requests); Assert.AreEqual(4, economy.Hints);
        }
        [Test] public async Task SkippedAndConcurrentHintRequestsNeverGrantDuplicateReward()
        {
            var economy = new CampEconomy(new EconomySaveData(), new EconomyRules(), () => true);
            var ads = new Ad { Result = AdResult.Skipped }; var service = new AdHintService(ads, economy, () => true);
            Assert.AreEqual(EconomyResult.Unavailable, await service.Request()); Assert.AreEqual(3, economy.Hints);
            ads.Pending = new TaskCompletionSource<AdResult>(); var request = service.Request();
            Assert.AreEqual(EconomyResult.Busy, await service.Request()); ads.Pending.SetResult(AdResult.Completed);
            Assert.AreEqual(EconomyResult.Applied, await request); Assert.AreEqual(4, economy.Hints);
        }
        static LegalConfiguration Legal() => new LegalConfiguration { policyIsDraft = false, minimumAge = 13, publisherName = "QA", supportEmail = "privacy@quietcamp.dev",
            privacyPolicyUrl = "https://quietcamp.dev/privacy", dataRequestUrl = "https://quietcamp.dev/data", revision = "r", publishedOn = "2026-10-21",
            teenPrivacyReviewed = true, sdkDisclosuresReviewed = true };
        [Test] public void PublishedFirstRunConsentMatchesCompositionHashRevision()
        {
            var flow = new PrivacySetup(true, true, true); var settings = new SettingsSaveData();
            flow.ReadPolicy(); flow.SelectAge(PrivacyAgeBand.From16To17); flow.ConfirmAge(); flow.Choose(true); flow.Choose(false); flow.Choose(false);
            Assert.IsTrue(flow.Commit(settings, "r", "hash", "uk", false, () => true, "r:hash"));
            var c = new GoogleServicesConfiguration { rewardedAdsEnabled = true, useTestAds = false };
            c.ResolveLegal(Legal()); c.BindPrivacy(settings, "r:hash");
            Assert.IsTrue(c.AdvertisingAllowed); Assert.IsFalse(c.AnalyticsAllowed || c.CrashesAllowed);
            Assert.AreEqual("r", settings.privacyAcknowledgementRevision);
            c.HasNoAdsEntitlement = () => true; Assert.IsFalse(c.AdvertisingAllowed, "Pro must suppress every ad placement, including cosmetic rewards.");
        }
        [Test] public void UnderConsentAgeLimitedAdsRequireSpecificApprovalAndCurrentNoticeWithoutFakeConsent()
        {
            var s = new SettingsSaveData { privacyAgeBand = 2, privacyAgeVersion = 1, privacySetupVersion = PrivacySetup.Version,
                privacyAcknowledgementRevision = "r", privacyAcknowledgementHash = "h", privacyAcknowledgementDraft = false };
            var legal = Legal(); var c = new GoogleServicesConfiguration { rewardedAdsEnabled = true, useTestAds = false, limitedAdsEnabled = true, underAgeLimitedAdsEnabled = true };
            c.ResolveLegal(legal); c.BindPrivacy(s, "r:h"); Assert.IsFalse(c.AdvertisingAllowed);
            legal.limitedAdsReviewed = true; Assert.IsFalse(c.AdvertisingAllowed);
            legal.underAgeAdvertisingReviewed = true; Assert.IsTrue(c.AdvertisingAllowed);
            Assert.IsFalse(s.advertisingConsent || s.analyticsConsent || s.crashReportingConsent);
            Assert.IsFalse(c.Eligible || c.AnalyticsAllowed || c.CrashesAllowed);
            s.optionalVideoBonuses = false; Assert.IsFalse(c.AdvertisingAllowed);
            s.optionalVideoBonuses = true; s.privacyAcknowledgementHash = "stale"; Assert.IsFalse(c.AdvertisingAllowed);
            s.privacyAcknowledgementHash = "h"; s.privacyAgeBand = 1; Assert.IsFalse(c.AdvertisingAllowed);
        }
        sealed class Ad : IAdService
        {
            public int Requests; public AdResult Result = AdResult.Completed; public TaskCompletionSource<AdResult> Pending;
            public bool IsReady => true;
            public Task<AdResult> ShowRewarded(string placement) { Assert.AreEqual("camp.hint", placement); Requests++; return Pending?.Task ?? Task.FromResult(Result); }
            public void ShowBanner(string placement) { } public void HideBanner() { }
        }
    }
}
