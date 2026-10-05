using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using QuietCamp.Application;
using QuietCamp.Infrastructure;

namespace QuietCamp.Tests.Editor
{
    public class ComfortAndRewardsTests
    {
        sealed class Transport : IComfortTransport
        {
            public bool Available => true;
            public int Initializations, Resets;
            public bool Enabled, FailSend;
            public TaskCompletionSource<bool> Pending;
            public List<ComfortRecord> Records = new List<ComfortRecord>();
            public Task<bool> Initialize() { Initializations++; return Pending?.Task ?? Task.FromResult(true); }
            public void SetConsent(bool enabled) => Enabled = enabled;
            public void Send(ComfortRecord record) { if (FailSend) throw new Exception("offline"); Records.Add(record); }
            public void ResetLocalData() => Resets++;
            public void Dispose() => Enabled = false;
        }
        sealed class Ads : IAdService
        {
            public bool IsReady { get; set; } = true;
            public int Requests;
            public AdResult Result = AdResult.Completed;
            public TaskCompletionSource<AdResult> Pending;
            public void ShowBanner(string placementId) => Assert.Fail("No banners");
            public void HideBanner() { }
            public Task<AdResult> ShowRewarded(string placementId) { Requests++; return Pending?.Task ?? Task.FromResult(Result); }
        }
        [Test]
        public async Task DefaultOffDoesNotInitializeOrQueueAnyActions()
        {
            var transport = new Transport(); int saves = 0;
            using var analytics = new ComfortAnalytics(new SettingsSaveData(), transport, () => saves++);
            await analytics.RestoreConsent();
            analytics.BeginLevel(1, 4, 4, 0); analytics.Action(ComfortAction.Place);
            analytics.Tick(1, true, 1); analytics.EndLevel(ComfortOutcome.Left); analytics.Feedback(0);
            Assert.AreEqual(0, transport.Initializations); Assert.AreEqual(0, saves); Assert.IsEmpty(transport.Records);
            await analytics.SetConsent(true);
            analytics.BeginLevel(1, 4, 4, 0); analytics.EndLevel(ComfortOutcome.Left);
            Assert.AreEqual(0, transport.Records.Last().Values["n_place"], "Pre-consent gestures must not be replayed.");
        }
        [Test]
        public async Task WithdrawalDuringDependencyLoadCannotReenableCollection()
        {
            var transport = new Transport { Pending = new TaskCompletionSource<bool>() };
            using var analytics = new ComfortAnalytics(new SettingsSaveData(), transport);
            var starting = analytics.SetConsent(true); await analytics.SetConsent(false);
            transport.Pending.SetResult(true); await starting;
            analytics.BeginLevel(2, 5, 6, 1);
            Assert.IsFalse(transport.Enabled); Assert.IsFalse(analytics.Collecting); Assert.IsEmpty(transport.Records); Assert.Greater(transport.Resets, 0);
        }
        [Test]
        public async Task SummariesContainNumericBoundedCountersAndActiveTimeOnly()
        {
            var transport = new Transport(); using var analytics = new ComfortAnalytics(new SettingsSaveData(), transport);
            await analytics.SetConsent(true); analytics.BeginLevel(12, 9, 1, 9);
            for (int i = 0; i < 350; i++) analytics.Action(ComfortAction.InvalidDrop);
            for (int i = 0; i < 120; i++) analytics.Tick(.5f, false, 1);
            analytics.EndLevel(ComfortOutcome.Completed);
            var summary = transport.Records.Last(r => r.Name == "qc_level_summary");
            Assert.AreEqual(250, summary.Values["n_invaliddrop"]); Assert.AreEqual(0, summary.Values["active_time_band"]);
            Assert.AreEqual(8, summary.Values["width"]); Assert.AreEqual(4, summary.Values["height"]);
            Assert.IsFalse(summary.Values.Keys.Any(k => k.Contains("guest") || k.Contains("user") || k.Contains("touch")));
        }
        [Test]
        public async Task SessionCapNavigationDedupAndFeedbackLimitPreventSpam()
        {
            var transport = new Transport(); using var analytics = new ComfortAnalytics(new SettingsSaveData(), transport);
            await analytics.SetConsent(true);
            for (int i = 0; i < 20; i++) analytics.Screen(ComfortScreen.Album);
            Assert.AreEqual(1, transport.Records.Count);
            analytics.Feedback(0); analytics.Feedback(1);
            Assert.AreEqual(1, transport.Records.Count(r => r.Name == "qc_comfort_feedback"));
            for (int i = 0; i < 300; i++) analytics.Setting(ComfortSetting.Sound, i%2);
            Assert.AreEqual(ComfortAnalytics.SessionEventLimit, transport.Records.Count);
            Assert.IsTrue(transport.Records.SelectMany(r => r.Values).All(p => p.Value <= 10));
        }
        [Test]
        public async Task SliderValuesAreCoarseDeduplicatedAndResetWithConsent()
        {
            var transport=new Transport();using var analytics=new ComfortAnalytics(new SettingsSaveData(),transport);
            await analytics.SetConsent(true);
            for(int i=0;i<100;i++)analytics.Setting(ComfortSetting.Sound,5);
            analytics.Setting(ComfortSetting.Text,10);
            Assert.AreEqual(2,transport.Records.Count);
            await analytics.SetConsent(false);analytics.Setting(ComfortSetting.Sound,4);
            await analytics.SetConsent(true);analytics.Setting(ComfortSetting.Sound,5);
            Assert.AreEqual(3,transport.Records.Count);
        }
        [Test]
        public async Task RuleDiagnosticsAreDeduplicatedAndFitFirebaseParameterLimit()
        {
            var transport = new Transport(); using var analytics = new ComfortAnalytics(new SettingsSaveData(), transport);
            await analytics.SetConsent(true); analytics.BeginLevel(2, 5, 5, 1);
            var level = LevelLoader.Load("QC_TEST");
            foreach (var guest in level.guests) guest.shade = true;
            level.shade = Array.Empty<int[]>();
            var report = QuietCamp.Domain.RuleEvaluator.Evaluate(level, level.witness);
            Assert.Greater(report.Issues.Count(i => i.Code == "shade"), 1);
            report.Issues.Add(new QuietCamp.Domain.RuleIssue("anything-unapproved", "another-id", false));
            analytics.RuleReport(report); analytics.EndLevel(ComfortOutcome.Left);
            var record = transport.Records.Last(); Assert.AreEqual(1, record.Values["rule_shade"]);
            Assert.LessOrEqual(record.Values.Count, 25); Assert.IsFalse(record.Values.Keys.Any(k => k.Contains("private") || k.Contains("unapproved")));
        }
        [Test]
        public async Task StalePolicyAndMissingProviderRemainOff()
        {
            var transport = new Transport();
            var settings = new SettingsSaveData { analyticsConsent = true, analyticsPolicyVersion = ComfortAnalytics.PolicyVersion - 1 };
            using var analytics = new ComfortAnalytics(settings, transport); await analytics.RestoreConsent();
            Assert.IsFalse(analytics.Consented); Assert.AreEqual(0, transport.Initializations);
            using var offline = new ComfortAnalytics(new SettingsSaveData(), new OfflineComfortTransport()); await offline.SetConsent(true);
            Assert.IsFalse(offline.Collecting); Assert.IsFalse(offline.Available);
        }
        [Test]
        public async Task TransportFailureDoesNotEscapeOrBlockGameplay()
        {
            var transport = new Transport { FailSend = true }; using var analytics = new ComfortAnalytics(new SettingsSaveData(), transport);
            await analytics.SetConsent(true); Assert.DoesNotThrow(() => analytics.Screen(ComfortScreen.Main));
            Assert.IsFalse(analytics.Collecting); Assert.IsFalse(transport.Enabled);
        }
        [Test]
        public async Task LocalDeletionWithdrawsConsentAndLeavesGameSettingsIntact()
        {
            var settings = new SettingsSaveData { language = "de", reducedMotion = true, fireflyLantern = true };
            var transport = new Transport(); using var analytics = new ComfortAnalytics(settings, transport);
            await analytics.SetConsent(true); await analytics.ClearLocalDataAndWithdraw();
            Assert.IsFalse(settings.analyticsConsent); Assert.IsTrue(settings.reducedMotion); Assert.IsTrue(settings.fireflyLantern); Assert.AreEqual("de", settings.language);
        }
        [TestCase(AdResult.Skipped, CozyRewardResult.Skipped)]
        [TestCase(AdResult.Failed, CozyRewardResult.Failed)]
        [TestCase(AdResult.Unavailable, CozyRewardResult.Unavailable)]
        public async Task OnlyAnEarnedVideoGrantsACosmetic(AdResult adResult, CozyRewardResult expected)
        {
            var ads = new Ads { Result = adResult }; var progress = new ProgressionService();
            var rewards = new CozyRewardService(ads, new SettingsSaveData(), progress, () => true);
            Assert.AreEqual(expected, await rewards.Request("album.lantern")); Assert.IsFalse(rewards.Owned);
            Assert.AreEqual(0, progress.CompletedCount);
        }
        [Test]
        public async Task ConcurrentClicksAreOneRequestAndOnePersistedGrant()
        {
            var ads = new Ads { Pending = new TaskCompletionSource<AdResult>() }; var progress = new ProgressionService(); int saves = 0;
            var rewards = new CozyRewardService(ads, new SettingsSaveData(), progress, () => { saves++; return true; });
            var first = rewards.Request("album.lantern");
            Assert.AreEqual(CozyRewardResult.Busy, await rewards.Request("camp.complete.lantern"));
            ads.Pending.SetResult(AdResult.Completed); Assert.AreEqual(CozyRewardResult.Granted, await first);
            Assert.AreEqual(1, ads.Requests); Assert.AreEqual(1, saves); Assert.IsTrue(rewards.Owned);
            Assert.AreEqual(CozyRewardResult.AlreadyOwned, await rewards.Request("album.lantern"));
        }
        [Test]
        public async Task DisabledBonusesAndUnknownPlacementsNeverRequestAds()
        {
            var ads = new Ads(); var settings = new SettingsSaveData { optionalVideoBonuses = false };
            var rewards = new CozyRewardService(ads, settings, new ProgressionService(), () => true);
            Assert.AreEqual(CozyRewardResult.Unavailable, await rewards.Request("album.lantern"));
            settings.optionalVideoBonuses = true;
            Assert.AreEqual(CozyRewardResult.Unavailable, await rewards.Request("hint")); Assert.AreEqual(0, ads.Requests);
        }
        [Test]
        public void NormalPlayEarnsTheSameLanternWithoutAnyAds()
        {
            var ads = new Ads(); var progress = new ProgressionService(); var settings = new SettingsSaveData();
            var rewards = new CozyRewardService(ads, settings, progress, () => true);
            for (int i = 1; i <= 3; i++) progress.MarkCompleted("QC00" + i);
            rewards.EarnFromPlay(); Assert.IsTrue(rewards.Owned); Assert.IsTrue(settings.fireflyLantern); Assert.AreEqual(0, ads.Requests);
        }
        [Test]
        public async Task SaveFailureDoesNotClaimTheRewardWasGranted()
        {
            var settings = new SettingsSaveData(); var rewards = new CozyRewardService(new Ads(), settings, new ProgressionService(), () => false);
            Assert.AreEqual(CozyRewardResult.Failed, await rewards.Request("album.lantern"));
            Assert.IsFalse(rewards.Owned); Assert.IsFalse(settings.fireflyLantern);
        }
        [Test]
        public void ReleaseTemplateIsInertWithoutSdkAndPublishedPrivacyEndpoints()
        {
            var config = GoogleServicesConfiguration.Load();
            Assert.IsFalse(config.analyticsEnabled); Assert.IsFalse(config.rewardedAdsEnabled);
            Assert.IsFalse(OptionalGoogleServices.Analytics(config).Available); Assert.IsFalse(OptionalGoogleServices.Ads(config).IsReady);
        }
    }
}
