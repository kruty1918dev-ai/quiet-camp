using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Kruty1918.SaveSystem;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using QuietCamp.Application;
using QuietCamp.Domain;
using QuietCamp.Infrastructure;

// Counterexamples supplement the existing probe. No runtime code is replaced;
// only storage failures, ad delivery and store responses use platform doubles.
// Run from the repo root after rebuilding tools/MonetizationProbe.csproj.
internal static class MonetizationAudit
{
    private static int _failed;

    private static async Task<int> Main()
    {
        await Case("AUD-01 current offline configuration stays playable at zero lives", OfflineExhaustion);
        await Case("AUD-02 duplicate check cannot charge the reset board", DuplicateCheck);
        await Case("AUD-03 completed ad reward can retry after restart without another ad", AdRestart);
        await Case("AUD-04 Owned is never reported for an ungranted Pro receipt", ReceiptBinding);
        Console.WriteLine($"{_failed} failed acceptance checks. These are isolated reproductions, not Unity scene/device tests.");
        return _failed == 0 ? 0 : 1;
    }

    private static async Task Case(string name, Func<Task> check)
    {
        try { await check(); Console.WriteLine("PASS " + name); }
        catch (Exception ex) { _failed++; Console.WriteLine("FAIL " + name + ": " + ex.Message); }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static Task OfflineExhaustion()
    {
        using var fixture = new Fixture();
        var manifest = JObject.Parse(File.ReadAllText(Path.Combine(UnityEngine.Resources.Root, "QuietCamp/monetization.json")));
        var rules = manifest["economy"].ToObject<EconomyRules>();
        var products = manifest["products"].ToObject<PurchaseProduct[]>();
        var google = JObject.Parse(File.ReadAllText(Path.Combine(UnityEngine.Resources.Root, "QuietCamp/google_services.json")));
        Require(google["rewardedAdsEnabled"].Value<bool>() == false && products.All(p => !p.published), "The inspected offline configuration changed; update this reproduction.");
        var economy = new CampEconomy(fixture.Save.Economy, rules, fixture.Save.Save);
        var attempts = new CampAttemptService(fixture.Save, economy, fixture.Completion);
        var bad = BadLayout(fixture.Level);
        for (int i = 0; i < rules.initialLives; i++)
        {
            fixture.Session.Restore(bad, bad[0].guestId);
            Require(attempts.Check(fixture.Session, "day") == CampAttemptResult.FailedAttempt, "The deliberately incorrect complete layout must cost a life.");
        }
        fixture.Session.Restore(fixture.Level.witness, fixture.Level.witness[0].guestId);
        Require(RuleEvaluator.Evaluate(fixture.Level, fixture.Session.State.Placements).IsSolved, "Witness must remain valid.");
        var adLives = new AdLifeService(new AdServiceStub(), economy, () => true);
        var result = attempts.Check(fixture.Session, "day");
        Require(result == CampAttemptResult.Completed, $"valid witness -> {result}; lives={economy.Lives}, currency={economy.Currency}, adAvailable={adLives.Available}, publishedProducts={products.Count(p => p.published)}. No configured recovery route.");
        return Task.CompletedTask;
    }

    private static Task DuplicateCheck()
    {
        using var fixture = new Fixture();
        var economy = new CampEconomy(fixture.Save.Economy, new EconomyRules(), fixture.Save.Save);
        var attempts = new CampAttemptService(fixture.Save, economy, fixture.Completion);
        var bad = BadLayout(fixture.Level);
        fixture.Session.Restore(bad, bad[0].guestId);
        var original = RuleEvaluator.Evaluate(fixture.Level, fixture.Session.State.Placements);
        RuleReport lastPublishedReport = null;
        fixture.Session.Evented += e => { if (e.Kind == CampEventKind.RulesChanged) lastPublishedReport = e.Report; };
        var first = attempts.Check(fixture.Session, "day");
        var afterFirst = economy.Lives;
        var second = attempts.Check(fixture.Session, "day");
        Console.WriteLine("  feedback before reset=" + string.Join(",", original.Issues.Select(i => i.Code)) + "; after reset=" + string.Join(",", lastPublishedReport.Issues.Select(i => i.Code)));
        Require(first == CampAttemptResult.FailedAttempt && fixture.Session.State.Count == 0, "The first failed attempt must reset the board.");
        Require(economy.Lives == afterFirst, $"consecutive calls -> {first}, {second}; lives 5 -> {afterFirst} -> {economy.Lives}, attempts={economy.Attempts(fixture.Level.id)}, boardCount={fixture.Session.State.Count}. The second call had no new placement.");
        return Task.CompletedTask;
    }

    private static async Task AdRestart()
    {
        using var fixture = new Fixture();
        var economy = new CampEconomy(fixture.Save.Economy, new EconomyRules(), fixture.Save.Save);
        Require(fixture.Save.Save(), "Initial state must persist before the simulated failure.");
        var ads = new ProbeAds();
        var rewards = new AdLifeService(ads, economy, () => true);
        fixture.Writer.Fail = true;
        Require(await rewards.Request() == EconomyResult.SaveFailed && ads.Requests == 1, "One completed ad and one failed save are required.");
        fixture.Writer.Fail = false;
        var reloaded = new SaveAdapter();
        Require(reloaded.Load(out _), "Restart must read the original valid slot.");
        var reloadedEconomy = new CampEconomy(reloaded.Economy, new EconomyRules(), reloaded.Save);
        var restartedAds = new ProbeAds { Ready = false };
        var restartedRewards = new AdLifeService(restartedAds, reloadedEconomy, () => true);
        var retry = await restartedRewards.Request();
        Require(retry == EconomyResult.Applied && reloadedEconomy.Lives == economy.Lives + 1 && restartedAds.Requests == 0,
            $"completedAds={ads.Requests}; after restart retry={retry}, lives={reloadedEconomy.Lives}, adRequestsAfterRestart={restartedAds.Requests}. Pending reward existed only in the previous service instance.");
    }

    private static async Task ReceiptBinding()
    {
        // Defensive adapter contract: a provider returning a contradictory but
        // 'verified' token must not let the client journal claim a grant it lacks.
        var economy = new CampEconomy(new EconomySaveData(), new EconomyRules(), () => true);
        var coins = new PurchaseProduct { id = "audit.coins", currencyAmount = 100, published = true };
        var pro = new PurchaseProduct { id = "audit.pro", pro = true, published = true };
        var purchases = new PurchaseService(new ReusedReceiptProvider(), new[] { coins, pro }, new PurchaseSaveData(),
            (token, product) => product.pro ? economy.GrantPro(token) : economy.CreditVerified(token, product.currencyAmount), () => true);
        Require(await purchases.Buy(coins.id) == PurchaseState.Owned && economy.Currency == 100, "The first receipt must credit only currency.");
        var result = await purchases.Buy(pro.id);
        Require(result != PurchaseState.Owned || economy.IsPro, $"currency={economy.Currency}, Pro purchase result={result}, IsPro={economy.IsPro}. A globally applied token is treated as a grant for another SKU.");
    }

    private static Placement[] BadLayout(LevelData level)
    {
        foreach (var guest in level.guests)
        for (int x = 0; x < level.width - 1; x++)
        for (int z = 0; z < level.height - 1; z++)
        for (int rotation = 0; rotation < 4; rotation++)
        {
            var layout = level.witness.Select(p => p.Copy()).ToArray();
            var moved = layout.Single(p => p.guestId == guest.id);
            moved.x = x; moved.z = z; moved.rotation = rotation;
            var report = RuleEvaluator.Evaluate(level, layout);
            if (report.CanCommit && !report.IsSolved)
            {
                Console.WriteLine("  bad complete layout=" + JsonConvert.SerializeObject(layout));
                return layout;
            }
        }
        throw new InvalidOperationException("No physically valid, logically incorrect complete layout found for the fixture.");
    }

    private sealed class Fixture : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "quietcamp-monetization-audit-slot-" + Guid.NewGuid().ToString("N"));
        private readonly string _previousRoot;
        public readonly SwitchWriter Writer = new SwitchWriter();
        public readonly SaveAdapter Save;
        public readonly LevelData Level;
        public readonly CampSession Session;
        public readonly CampCompletionService Completion;
        public Fixture()
        {
            _previousRoot = UnityEngine.Application.persistentDataPath;
            Directory.CreateDirectory(_root);
            UnityEngine.Application.persistentDataPath = _root;
            Save = new SaveAdapter(Writer);
            Require(Save.Load(out _), "Fresh synthetic slot must load.");
            Level = LevelLoader.Load("QC001");
            Session = new CampSession(Level);
            var catalog = new JourneyCatalog(new[] { new JourneyDefinition { id = "main", levelIds = new[] { Level.id }, published = true } });
            Completion = new CampCompletionService(Save, new ProgressionService(), new CampMemoryService(Save.Memories), catalog);
        }
        public void Dispose()
        {
            Session.Dispose();
            UnityEngine.Application.persistentDataPath = _previousRoot;
            Directory.Delete(_root, true);
        }
    }

    private sealed class SwitchWriter : ISaveWriteService
    {
        public bool Fail;
        private readonly SaveWriteService _real = new SaveWriteService();
        public bool TrySave(int slot, IReadOnlyList<ISaveModule> modules, string required, out string error)
        {
            if (Fail) { error = "Synthetic audit IO failure"; return false; }
            return _real.TrySave(slot, modules, required, out error);
        }
    }

    private sealed class ProbeAds : IAdService
    {
        public bool Ready = true;
        public int Requests;
        public bool IsReady => Ready;
        public void ShowBanner(string placementId) { }
        public void HideBanner() { }
        public Task<AdResult> ShowRewarded(string placementId) { Requests++; return Task.FromResult(AdResult.Completed); }
    }

    private sealed class ReusedReceiptProvider : IPurchaseProvider
    {
        public Task<PurchaseDetails> Details(string productId) => Task.FromResult(new PurchaseDetails { productId = productId, formattedPrice = "TEST ONLY", available = true });
        public Task<PurchaseReceipt> Buy(string productId) => Task.FromResult(new PurchaseReceipt { productId = productId, receiptId = "audit:same-token", verified = true, state = PurchaseState.Owned });
        public Task<IReadOnlyList<PurchaseReceipt>> Query() => Task.FromResult<IReadOnlyList<PurchaseReceipt>>(Array.Empty<PurchaseReceipt>());
    }
}
