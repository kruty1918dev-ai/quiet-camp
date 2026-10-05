using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Kruty1918.SaveSystem;
using NUnit.Framework;
using QuietCamp.Application;
using QuietCamp.Domain;
using QuietCamp.Infrastructure;

namespace QuietCamp.Tests
{
    public sealed class MonetizationTests
    {
        sealed class Writer : ISaveWriteService
        {
            public bool Success;
            public bool TrySave(int slot, IReadOnlyList<ISaveModule> modules, string required, out string error)
            { error = Success ? null : "Synthetic IO failure"; return Success; }
        }
        [Test]
        public void InitialResourcesAndFailedWritesAreStable()
        {
            var write = true; var economy = new CampEconomy(new EconomySaveData(), new EconomyRules(), () => write);
            Assert.AreEqual(3, economy.Hints); Assert.AreEqual(5, economy.Lives);
            Assert.AreEqual(EconomyResult.Applied, economy.UseHint());
            write = false;
            Assert.AreEqual(EconomyResult.SaveFailed, economy.UseHint()); Assert.AreEqual(2, economy.Hints);
            Assert.AreEqual(EconomyResult.SaveFailed, economy.RecordCheck("a", false));
            Assert.AreEqual(5, economy.Lives); Assert.AreEqual(0, economy.Attempts("a"));
        }
        [Test]
        public void IncorrectCheckClearsTentsAndHistoryOnlyAfterSave()
        {
            var writer = new Writer(); var save = new SaveAdapter(writer);
            var economy = new CampEconomy(save.Economy, new EconomyRules(), save.Save);
            var level = LevelLoader.Load("QC001"); using var session = new CampSession(level);
            var catalog = new JourneyCatalog(new[] { new JourneyDefinition { id = "main", published = true, levelIds = new[] { level.id } } });
            var completion = new CampCompletionService(save, new ProgressionService(), new CampMemoryService(save.Memories), catalog);
            var attempts = new CampAttemptService(save, economy, completion);
            session.Restore(new[] { level.witness[0] }, "g1");
            UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Error, "[QuietCamp] Save failed: Synthetic IO failure");
            Assert.AreEqual(CampAttemptResult.SaveFailed, attempts.Check(session, "day"));
            Assert.AreEqual(1, session.State.Count); Assert.AreEqual(5, economy.Lives);
            writer.Success = true;
            Assert.AreEqual(CampAttemptResult.FailedAttempt, attempts.Check(session, "day"));
            Assert.AreEqual(0, session.State.Count); Assert.IsFalse(session.CanUndo); Assert.AreEqual(4, economy.Lives);
            Assert.AreEqual(0, save.Session.placements.Length);
        }
        [Test]
        public void CompletionIsPublishedOnlyAfterAtomicCommit()
        {
            var writer = new Writer(); var save = new SaveAdapter(writer); var progress = new ProgressionService();
            var memory = new CampMemoryService(save.Memories); var level = LevelLoader.Load("QC001");
            var catalog = new JourneyCatalog(new[] { new JourneyDefinition { id = "main", published = true, levelIds = new[] { level.id } } });
            var completion = new CampCompletionService(save, progress, memory, catalog);
            using var session = new CampSession(level); session.Restore(level.witness, "g1");
            int events = 0; session.Evented += e => { if (e.Kind == CampEventKind.LevelCompleted) events++; };
            UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Error, "[QuietCamp] Save failed: Synthetic IO failure");
            session.Check(() => completion.Complete(session, "night"));
            Assert.IsFalse(session.IsCompleted); Assert.AreEqual(0, events); Assert.AreEqual(0, progress.CompletedCount);
            Assert.AreEqual(0, save.Album.entries.Length); Assert.AreEqual(0, memory.Data.rewardIds.Length);
            writer.Success = true; session.Check(() => completion.Complete(session, "night"));
            Assert.IsTrue(session.IsCompleted); Assert.AreEqual(1, events); Assert.AreEqual("night", save.Album.entries.Single().lighting);
            Assert.IsTrue(save.Album.entries.Single().cared);
            session.Restore(level.witness, "g1"); Assert.IsTrue(completion.Complete(session, "night"));
            Assert.AreEqual(1, memory.Data.rewardIds.Length); Assert.AreEqual(1, save.Album.entries.Length);
        }
        [Test]
        public void AdDayQuotaCannotBeRefilledByClockRollback()
        {
            var now = new DateTime(2026, 10, 5, 12, 0, 0, DateTimeKind.Utc);
            var economy = new CampEconomy(new EconomySaveData(), new EconomyRules(), () => true, () => now);
            for (int i = 0; i < 10; i++) Assert.AreEqual(EconomyResult.Applied, economy.GrantAdLife("ad-" + i));
            now = now.AddDays(-1); Assert.AreEqual(EconomyResult.DailyLimit, economy.GrantAdLife("rollback"));
            now = now.AddDays(2); Assert.AreEqual(EconomyResult.Applied, economy.GrantAdLife("tomorrow"));
            Assert.AreEqual(EconomyResult.AlreadyApplied, economy.GrantAdLife("tomorrow")); Assert.AreEqual(9, economy.AdLivesRemaining);
        }
        [Test]
        public void ProLeavesSupplyAndAttemptCountersUntouched()
        {
            var economy = new CampEconomy(new EconomySaveData(), new EconomyRules(), () => true);
            economy.GrantPro("pro");
            for (int i = 0; i < 20; i++) { Assert.AreEqual(EconomyResult.Applied, economy.UseHint()); Assert.AreEqual(EconomyResult.Applied, economy.RecordCheck("a", false)); }
            Assert.AreEqual(3, economy.Hints); Assert.AreEqual(5, economy.Lives); Assert.AreEqual(0, economy.Attempts("a"));
        }
        [Test]
        public void OwnDlcCanStartWithoutMainAndMissingContentCannot()
        {
            var catalog = new JourneyCatalog(new[]
            {
                new JourneyDefinition { id = "main", published = true, levelIds = new[] { "a", "b" } },
                new JourneyDefinition { id = "dlc", published = true, entitlementId = "dlc", levelIds = new[] { "c", "d" } },
                new JourneyDefinition { id = "missing", entitlementId = "missing", levelIds = new[] { "e" } }
            });
            var access = new JourneyAccessService(catalog, new ProgressionService(), new EntitlementSaveData { ownedIds = new[] { "dlc", "missing" } }, () => false);
            Assert.IsTrue(access.Evaluate("c").CanStart); Assert.IsFalse(access.Evaluate("d").CanStart);
            Assert.IsFalse(access.Evaluate("e").CanStart); Assert.IsFalse(access.Evaluate("unknown").CanStart);
        }
        [Test]
        public async Task PendingAndDuplicatePurchaseDoNotDuplicateCurrency()
        {
            var economy = new CampEconomy(new EconomySaveData(), new EconomyRules(), () => true);
            var provider = new FakePurchaseProvider { NextState = PurchaseState.Pending };
            var product = new PurchaseProduct { id = "qa", currencyAmount = 100, published = true };
            var service = new PurchaseService(provider, new[] { product }, new PurchaseSaveData(), (id, p) => economy.CreditVerified(id, p.currencyAmount), () => true);
            Assert.AreEqual(PurchaseState.Pending, await service.Buy("qa")); Assert.AreEqual(0, economy.Currency);
            Assert.AreEqual(PurchaseState.Pending, await service.Buy("qa"));
            provider.NextState = PurchaseState.Owned;
            Assert.AreEqual(PurchaseState.Owned, await service.Restore()); Assert.AreEqual(100, economy.Currency);
            Assert.AreEqual(PurchaseState.Owned, await service.Restore()); Assert.AreEqual(100, economy.Currency);
        }
        [Test]
        public async Task InvalidVerificationNeverGrantsOrLocksFutureAttempts()
        {
            var provider = new FakePurchaseProvider { Verified = false }; int grants = 0;
            var product = new PurchaseProduct { id = "qa", currencyAmount = 100, published = true };
            var service = new PurchaseService(provider, new[] { product }, new PurchaseSaveData(), (id, p) => { grants++; return EconomyResult.Applied; }, () => true);
            Assert.AreEqual(PurchaseState.Failed, await service.Buy("qa")); Assert.AreEqual(0, grants);
            provider.Verified = true;
            Assert.AreEqual(PurchaseState.Owned, await service.Buy("qa")); Assert.AreEqual(1, grants);
        }
    }
}
