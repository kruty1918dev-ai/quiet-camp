using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using QuietCamp.Application;
using QuietCamp.Infrastructure;

namespace QuietCamp.Tests.Editor
{
    public sealed class ProductionMonetizationTests
    {
        sealed class Ads : IAdService, IInterstitialAdService
        {
            public bool IsReady => true;
            public int Requests;
            public void ShowBanner(string id) { }
            public void HideBanner() { }
            public Task<AdResult> ShowRewarded(string id) { Requests++; return Task.FromResult(AdResult.Completed); }
            public Task<AdResult> ShowInterstitial(string id) { Requests++; return Task.FromResult(AdResult.Completed); }
        }
        [TestCase(0, false)] [TestCase(1, false)] [TestCase(2, true)] [TestCase(3, true)] [TestCase(4, true)]
        public void AgeCapabilitiesDoNotTreatUnknownAsAdult(int band, bool purchases)
        {
            var settings = new SettingsSaveData { privacyAgeBand = band, privacyAgeVersion = 1 };
            Assert.AreEqual(purchases, PrivacyEligibility.PurchasesAllowed(settings));
            Assert.AreEqual(band == 4, PrivacyEligibility.PersonalizationAllowed(settings));
            Assert.IsFalse(settings.analyticsConsent || settings.crashReportingConsent || settings.advertisingConsent);
        }
        [Test] public void TeenMeasurementsNeedTheirOwnReviewWhileAdultChoicesRemainIndependent()
        {
            var flow = new PrivacySetup(true, true, true, false, false);
            flow.ReadPolicy(); flow.SelectAge(PrivacyAgeBand.From13To15); flow.ConfirmAge();
            Assert.AreEqual(PrivacySetupStep.Review, flow.Step);
            flow = new PrivacySetup(true, true, true, false, true);
            flow.ReadPolicy(); flow.SelectAge(PrivacyAgeBand.From13To15); flow.ConfirmAge();
            Assert.AreEqual(PrivacySetupStep.Analytics, flow.Step);
            flow.Choose(false); flow.Choose(true);
            Assert.IsFalse(flow.Analytics); Assert.IsTrue(flow.Crashes); Assert.IsFalse(flow.Advertising);
        }
        [Test] public async Task EarnedReceiptRecoversAcrossServiceRestartAndSaveFailure()
        {
            var data = new EconomySaveData(); bool writable = true;
            var economy = new CampEconomy(data, new EconomyRules(), () => writable);
            string receipt = economy.RememberEarnedAdReward(false);
            var restored = new CampEconomy(data.Copy(), new EconomyRules(), () => writable);
            var ads = new Ads(); int before = restored.Lives;
            var service = new AdLifeService(ads, restored, () => false);
            writable = false; Assert.AreEqual(EconomyResult.SaveFailed, await service.Request());
            Assert.AreEqual(receipt, restored.PendingAdReward(false)); Assert.AreEqual(before, restored.Lives);
            writable = true; Assert.AreEqual(EconomyResult.Applied, await service.Request());
            Assert.AreEqual(before + 1, restored.Lives); Assert.Zero(ads.Requests);
            Assert.AreEqual(EconomyResult.AlreadyApplied, restored.GrantAdLife(receipt));
            Assert.IsNull(restored.PendingAdReward(false));
        }
        [Test] public async Task InterstitialLimitsAndRewardCooldownAreIndependent()
        {
            var ads = new Ads(); double now = 0; bool pro = false;
            var service = new InterstitialService(ads, () => !pro, 0, seconds: () => now);
            Assert.AreEqual(AdResult.Unavailable, await service.AtLevelTransition(4));
            Assert.AreEqual(AdResult.Completed, await service.AtLevelTransition(5));
            now = 200; Assert.AreEqual(AdResult.Unavailable, await service.AtLevelTransition(8));
            service.Rewarded(); Assert.AreEqual(AdResult.Unavailable, await service.AtLevelTransition(9));
            now = 381; Assert.AreEqual(AdResult.Completed, await service.AtLevelTransition(10));
            pro = true; now = 600; Assert.AreEqual(AdResult.Unavailable, await service.AtLevelTransition(14));
            pro = false; Assert.AreEqual(AdResult.Completed, await service.AtLevelTransition(15));
            now = 1000; Assert.AreEqual(AdResult.Unavailable, await service.AtLevelTransition(20)); Assert.AreEqual(3, ads.Requests);
        }
        sealed class Provider : IPurchaseProvider
        {
            public PurchaseReceipt Receipt;
            public Task<PurchaseDetails> Details(string id) => Task.FromResult(new PurchaseDetails { productId = id, available = true, formattedPrice = "store-price" });
            public Task<PurchaseReceipt> Buy(string id) => Task.FromResult(Receipt);
            public Task<IReadOnlyList<PurchaseReceipt>> Query() => Task.FromResult<IReadOnlyList<PurchaseReceipt>>(new[] { Receipt });
        }
        [Test] public async Task PendingRestoreRefundAndDuplicatePurchaseUseServerDecisions()
        {
            var product = new PurchaseProduct { id = "quietcamp_pro", published = true, pro = true };
            var provider = new Provider { Receipt = new PurchaseReceipt { productId = product.id, receiptId = "server-receipt", state = PurchaseState.Pending } };
            var data = new PurchaseSaveData(); var economy = new CampEconomy(new EconomySaveData(), new EconomyRules(), () => true);
            var service = new PurchaseService(provider, new[] { product }, data, (_, __) => throw new Exception("Pro must use server grant"), () => true,
                _ => economy.SetProEntitlement(false), receipt => economy.GrantVerifiedPro(receipt.receiptId, receipt.validUntilUtc));
            Assert.AreEqual(PurchaseState.Pending, await service.Buy(product.id)); Assert.IsFalse(economy.IsPro);
            provider.Receipt.state = PurchaseState.Owned; provider.Receipt.verified = true;
            provider.Receipt.validUntilUtc = DateTime.UtcNow.AddHours(24).ToString("O");
            Assert.AreEqual(PurchaseState.Owned, await service.Restore()); Assert.IsTrue(economy.IsPro);
            Assert.AreEqual(PurchaseState.Owned, await service.Restore()); Assert.AreEqual(1, data.fulfilledProductIds.Length);
            provider.Receipt.state = PurchaseState.Revoked;
            Assert.AreEqual(PurchaseState.Revoked, await service.Restore()); Assert.IsFalse(economy.IsPro);
            Assert.IsEmpty(data.fulfilledProductIds);
            Assert.AreEqual(PurchaseState.Revoked, await service.Restore());
            provider.Receipt.receiptId = "cancelled-pending"; provider.Receipt.state = PurchaseState.Pending; provider.Receipt.verified = false;
            Assert.AreEqual(PurchaseState.Pending, await service.Buy(product.id));
            provider.Receipt.state = PurchaseState.Revoked; provider.Receipt.verified = true;
            Assert.AreEqual(PurchaseState.Revoked, await service.Restore()); Assert.IsNull(data.pendingProductId);
            provider.Receipt.receiptId = "new-purchase"; provider.Receipt.state = PurchaseState.Owned;
            Assert.AreEqual(PurchaseState.Owned, await service.Buy(product.id)); Assert.IsTrue(economy.IsPro);
        }
        [Test] public void ExpiredServerLeaseDisablesProWithoutChangingResourceBalances()
        {
            DateTime now = DateTime.UtcNow;
            var data = new EconomySaveData(); var economy = new CampEconomy(data, new EconomyRules(), () => true, () => now);
            Assert.AreEqual(EconomyResult.Applied, economy.GrantVerifiedPro("receipt", now.AddHours(24).ToString("O")));
            int lives = economy.Lives, hints = economy.Hints;
            now = now.AddHours(25); Assert.IsFalse(economy.IsPro);
            Assert.AreEqual(lives, economy.Lives); Assert.AreEqual(hints, economy.Hints);
        }
        [Test] public void FutureJourneyIsNotAutomaticallyIncludedInPro()
        {
            var journey = new JourneyDefinition { id = "future", published = true, entitlementId = "dlc.future", levelIds = new[] { "future.1" } };
            var access = new JourneyAccessService(new JourneyCatalog(new[] { journey }), new ProgressionService(), new EntitlementSaveData(), () => true);
            Assert.AreEqual(JourneyAccessState.PurchaseRequired, access.Evaluate("future.1").State);
            journey.includedInPro = true; Assert.IsTrue(access.Evaluate("future.1").CanStart);
        }
        [Test] public async Task OperationalStatisticsRequireSeparatePermissionAndDropWithdrawnQueue()
        {
            bool permitted = false; int requests = 0;
            var metrics = new OperationalMetrics(() => permitted, (name, bucket, count) => { requests++; return Task.FromResult(true); });
            for (int i = 0; i < 20; i++) metrics.Record(0, 0);
            await metrics.Flush(); Assert.Zero(requests);
            permitted = true;
            for (int i = 0; i < 9; i++) metrics.Record(0, 0);
            await metrics.Flush(); Assert.Zero(requests);
            metrics.Record(0, 0); await metrics.Flush(); Assert.AreEqual(1, requests);
            for (int i = 0; i < 10; i++) metrics.Record(0, 1);
            permitted = false; await metrics.Flush(); permitted = true; await metrics.Flush(); Assert.AreEqual(1, requests);
        }
    }
}
