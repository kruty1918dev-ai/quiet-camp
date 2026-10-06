using System;
using System.Linq;
using System.IO;
using System.Text;
using System.Collections.Generic;
using Kruty1918.SaveSystem;
using Newtonsoft.Json.Linq;
using QuietCamp.Infrastructure;
using QuietCamp.Domain;
using System.Threading.Tasks;
using QuietCamp.Application;

class MonetizationProbe
{
    static int _passed;
    static void Expect(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }
    static void Pass(string name) { Console.WriteLine("PASS " + name); _passed++; }
    static async Task Main(string[] args)
    {
        if (args.Length == 2 && args[0] == "--author-lighthouse") { AuthorLighthouse(args[1]); return; }
        var now = new DateTime(2026, 10, 5, 23, 59, 0, DateTimeKind.Utc);
        var state = new EconomySaveData();
        var persist = true;
        var economy = new CampEconomy(state, new EconomyRules(), () => persist, () => now);
        Expect(economy.Lives == 5 && economy.Hints == 3 && economy.Currency == 0, "Initial allowance");
        Expect(economy.UseHint() == EconomyResult.Applied && economy.Hints == 2, "One hint consumed");
        persist = false;
        Expect(economy.UseHint() == EconomyResult.SaveFailed && economy.Hints == 2, "Failed save restores hint");
        Expect(economy.RecordCheck("QC001", false) == EconomyResult.SaveFailed && economy.Lives == 5 && economy.Attempts("QC001") == 0, "Failed check save restores life and attempt");
        persist = true;
        Expect(economy.RecordCheck("QC001", true) == EconomyResult.Applied && economy.Lives == 5 && economy.Attempts("QC001") == 1, "Success counts attempt, not life");
        Expect(economy.RecordCheck("QC001", false) == EconomyResult.Applied && economy.Lives == 4 && economy.Attempts("QC001") == 2, "Failure costs exactly one life");
        Pass("initial allowances, hint and attempt persistence");
        for (int i = 0; i < 4; i++) Expect(economy.RecordCheck("QC001", false) == EconomyResult.Applied, "Failure applies");
        Expect(economy.RecordCheck("QC001", false) == EconomyResult.NoLives && economy.Attempts("QC001") == 6, "No extra attempt at zero lives");
        Expect(economy.BuyLives(1) == EconomyResult.InsufficientCurrency && economy.Lives == 0, "No free purchase");
        Expect(economy.CreditVerified("receipt-1", 100) == EconomyResult.Applied && economy.Currency == 100, "Credit purchase");
        Expect(economy.CreditVerified("receipt-1", 100) == EconomyResult.AlreadyApplied && economy.Currency == 100, "Receipt idempotency");
        Expect(economy.BuyLives(2) == EconomyResult.Applied && economy.Currency == 80 && economy.Lives == 2, "Atomic currency debit and lives");
        persist = false;
        Expect(economy.BuyHints(1) == EconomyResult.SaveFailed && economy.Currency == 80 && economy.Hints == 2, "Atomic rollback of currency exchange");
        persist = true;
        Expect(economy.BuyLives(int.MaxValue) == EconomyResult.Invalid, "Overflow rejected");
        Expect(economy.BuyHints(-1) == EconomyResult.Invalid, "Negative quantity rejected");
        Pass("currency, no-lives, duplicate receipt and exchange rollback");
        for (int i = 0; i < 10; i++) Expect(economy.GrantAdLife("ad-" + i) == EconomyResult.Applied, "Ad reward");
        Expect(economy.GrantAdLife("ad-10") == EconomyResult.DailyLimit && economy.AdLivesRemaining == 0, "Ten rewards per day");
        now = now.AddDays(-1);
        Expect(economy.GrantAdLife("clock-back") == EconomyResult.DailyLimit, "Clock rollback does not refill daily cap");
        now = new DateTime(2026, 10, 6, 0, 0, 0, DateTimeKind.Utc);
        Expect(economy.GrantAdLife("next-day") == EconomyResult.Applied && economy.AdLivesRemaining == 9, "UTC day rollover");
        Expect(economy.GrantAdLife("next-day") == EconomyResult.AlreadyApplied && economy.AdLivesRemaining == 9, "Duplicate reward does not consume quota");
        Pass("ad daily quota, UTC reset, clock rollback and duplicate rewards");
        var restored = new CampEconomy(state, new EconomyRules(), () => true, () => now);
        Expect(restored.Hints == 2 && restored.Currency == 80 && restored.AdLivesRemaining == 9, "Restart retains balances");
        Expect(restored.GrantPro("pro-receipt") == EconomyResult.Applied, "Pro grant");
        var lives = restored.Lives; var hints = restored.Hints;
        for (int i = 0; i < 100; i++) { Expect(restored.UseHint() == EconomyResult.Applied, "Unlimited hints"); Expect(restored.RecordCheck("QC001", false) == EconomyResult.Applied, "Unlimited attempts"); }
        Expect(restored.IsPro && restored.Lives == lives && restored.Hints == hints && restored.Attempts("QC001") == 6, "Pro consumes nothing and records no counters");
        Pass("restart and Pro unlimited without counters");
        var main = new JourneyDefinition { id = "main", levelIds = new[] { "a", "b" }, published = true };
        var dlc = new JourneyDefinition { id = "lighthouse", levelIds = new[] { "c", "d" }, entitlementId = "dlc.lighthouse", published = true };
        var pending = new JourneyDefinition { id = "garden", levelIds = new[] { "e" }, entitlementId = "dlc.garden", published = false };
        var catalog = new JourneyCatalog(new[] { main, dlc, pending });
        var grants = new EntitlementSaveData();
        var access = new JourneyAccessService(catalog, new ProgressionService(), grants, () => false);
        Expect(access.Evaluate("a").CanStart && access.Evaluate("b").State == JourneyAccessState.Predecessor, "Main order");
        Expect(access.Evaluate("c").State == JourneyAccessState.PurchaseRequired && !access.Evaluate("missing").CanStart, "Locked DLC and unknown ID");
        grants.ownedIds = new[] { "dlc.lighthouse" };
        Expect(access.Evaluate("c").CanStart && !access.Evaluate("d").CanStart && !access.Evaluate("e").CanStart, "Independent DLC start, unpublished blocked");
        var proAccess = new JourneyAccessService(catalog, new ProgressionService(), grants, () => true);
        Expect(proAccess.Evaluate("c").CanStart && proAccess.Evaluate("d").CanStart && !proAccess.Evaluate("e").CanStart, "Pro opens all published levels but cannot create missing content");
        Expect(JourneyCatalog.Validate(new[] { main, new JourneyDefinition { id = "other", levelIds = new[] { "a" }, published = true } }).Count > 0, "Duplicate level rejected");
        Pass("catalog, main progression, standalone DLC and missing content");
        var fake = new FakePurchaseProvider();
        var product = new PurchaseProduct { id = "coins.100", currencyAmount = 100, published = true };
        var storeState = new PurchaseSaveData();
        var bought = new PurchaseService(fake, new[] { product }, storeState, (receipt, p) => restored.CreditVerified(receipt, p.currencyAmount), () => true);
        Expect(await bought.Buy(product.id) == PurchaseState.Owned, "Fake verified purchase");
        fake.NextState = PurchaseState.Pending;
        Expect(await bought.Buy(product.id) == PurchaseState.Pending, "Pending purchase");
        var previous = restored.Currency;
        Expect(await bought.Buy(product.id) == PurchaseState.Pending && restored.Currency == previous, "Pending not bought twice");
        fake.NextState = PurchaseState.Owned;
        Expect(await bought.Restore() == PurchaseState.Owned && restored.Currency == previous + 100, "Pending resolved through restore");
        Expect(await bought.Restore() == PurchaseState.Owned && restored.Currency == previous + 100, "Restore never duplicates credits");
        var unavailable = new PurchaseService(new UnavailablePurchaseProvider(), new[] { product }, new PurchaseSaveData(), (receipt, p) => throw new Exception("Must not grant"), () => true);
        Expect(await unavailable.Buy(product.id) == PurchaseState.Unavailable, "No SDK cannot succeed");
        Pass("fake store, pending, restore, duplicate credits and unavailable provider");
        var failures = new FakePurchaseProvider { Verified = false };
        var failureState = new PurchaseSaveData(); var storePersist = true;
        var recoverable = new PurchaseService(failures, new[] { product }, failureState,
            (receipt, p) => restored.CreditVerified(receipt, p.currencyAmount), () => storePersist);
        previous = restored.Currency;
        Expect(await recoverable.Buy(product.id) == PurchaseState.Failed && restored.Currency == previous && failureState.pendingProductId == null, "Invalid receipt never grants or blocks future purchase");
        failures.Verified = true; storePersist = false;
        Expect(await recoverable.Buy(product.id) == PurchaseState.Retry && failureState.pendingProductId == product.id, "Final journal failure keeps recovery pending");
        previous = restored.Currency;
        Expect(await recoverable.Buy(product.id) == PurchaseState.Pending && restored.Currency == previous, "Retry state cannot trigger another charge");
        storePersist = true;
        Expect(await recoverable.Restore() == PurchaseState.Failed && restored.Currency == previous, "Invalid old receipt is rejected while valid grant remains idempotent");
        Expect(failureState.pendingProductId == null, "Valid recovered receipt clears its own pending state");
        Pass("invalid verification and final-journal recovery never grant twice or re-charge");
        await NativeContracts();
        ContentContracts();
        Console.WriteLine(_passed + " contract groups passed; no Unity player, real store, ads, device FPS or human pilot verified.");
    }
    sealed class SwitchWriter : ISaveWriteService
    {
        public bool Fail;
        readonly SaveWriteService _real = new SaveWriteService();
        public bool TrySave(int slot, IReadOnlyList<ISaveModule> modules, string required, out string error)
        {
            if (Fail) { error = "Synthetic IO failure"; return false; }
            return _real.TrySave(slot, modules, required, out error);
        }
    }
    sealed class ProbeAds : IAdService
    {
        public bool IsReady => true;
        public AdResult Result = AdResult.Completed;
        public int Requests;
        public TaskCompletionSource<AdResult> Pending;
        public void ShowBanner(string id) { }
        public void HideBanner() { }
        public Task<AdResult> ShowRewarded(string id) { Requests++; return Pending?.Task ?? Task.FromResult(Result); }
    }
    static async Task NativeContracts()
    {
        var root = Path.Combine(Path.GetTempPath(), "quietcamp-monetization-fixture-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root); UnityEngine.Application.persistentDataPath = root;
        try
        {
            var writer = new SwitchWriter(); var save = new SaveAdapter(writer);
            Expect(save.Load(out _), "Fresh isolated slot");
            var progress = new ProgressionService(); var memory = new CampMemoryService(save.Memories);
            var level = LevelLoader.Load("QC001");
            var catalog = new JourneyCatalog(new[] { new JourneyDefinition { id = "main", levelIds = new[] { level.id }, published = true } });
            var completion = new CampCompletionService(save, progress, memory, catalog);
            var economy = new CampEconomy(save.Economy, new EconomyRules(), save.Save);
            var attempts = new CampAttemptService(save, economy, completion);
            using var session = new CampSession(level);
            session.Restore(new[] { level.witness[0] }, level.witness[0].guestId);
            Expect(attempts.Check(session, "day") == CampAttemptResult.FailedAttempt && session.State.Count == 0 && !session.CanUndo && economy.Lives == 4, "Failure resets all tents and undo atomically");
            var reload = new SaveAdapter(); Expect(reload.Load(out _), "Reload failed attempt");
            Expect(reload.Economy.lives == 4 && reload.Session.placements.Length == 0 && reload.Economy.attempts.Single().checks == 1, "Restart cannot restore failed layout or life");
            session.Restore(new[] { level.witness[0] }, level.witness[0].guestId); writer.Fail = true;
            Expect(attempts.Check(session, "day") == CampAttemptResult.SaveFailed && session.State.Count == 1 && economy.Lives == 4, "IO failure keeps current tents and life");
            session.Restore(level.witness, "g1");
            Expect(session.Check(() => completion.Complete(session, "night")).IsSolved && !session.IsCompleted && progress.CompletedCount == 0 && save.Album.entries.Length == 0 && memory.Data.rewardIds.Length == 0, "No completion publication after failed save");
            writer.Fail = false;
            Expect(attempts.Check(session, "night") == CampAttemptResult.Completed && session.IsCompleted, "Completion saved before event");
            Expect(save.Album.entries.Single().cared && save.Album.entries.Single().lighting == "night" && memory.Data.rewardIds.Length == 1 && save.Session.levelId == null, "Memory and own arrangement saved");
            session.Restore(level.witness, "g1"); Expect(completion.Complete(session, "night"), "Replay commits");
            Expect(save.Album.entries.Length == 1 && memory.Data.rewardIds.Length == 1 && progress.CompletedCount == 1, "Replay rewards are idempotent");
            session.Restore(level.witness, "g1"); session.CompletionPersistence = () => completion.Complete(session, "night");
            writer.Fail = true; session.Check(); Expect(!session.IsCompleted, "Scene-bound direct check cannot bypass save failure");
            writer.Fail = false; session.Check(); Expect(session.IsCompleted && memory.Data.rewardIds.Length == 1, "Scene-bound direct check preserves existing integration behavior");
            reload = new SaveAdapter(); Expect(reload.Load(out _) && reload.Album.entries.Single().cared && reload.Memories.rewardIds.Length == 1, "Memory survives restart");
            Pass("actual session, reset/undo, completion transaction, IO rollback, replay and saved memory");
            var ads = new ProbeAds(); var adService = new AdLifeService(ads, economy, () => true);
            ads.Result = AdResult.Skipped; int lives = economy.Lives;
            Expect(await adService.Request() == EconomyResult.Unavailable && economy.Lives == lives, "Skipped ad gives nothing");
            ads.Result = AdResult.Completed; writer.Fail = true;
            Expect(await adService.Request() == EconomyResult.SaveFailed && economy.Lives == lives, "Ad grant rollback");
            int requests = ads.Requests; writer.Fail = false;
            Expect(await adService.Request() == EconomyResult.Applied && ads.Requests == requests && economy.Lives == lives + 1, "Failed reward retries without watching another ad");
            ads.Pending = new TaskCompletionSource<AdResult>(); var pendingAd = adService.Request();
            Expect(await adService.Request() == EconomyResult.Busy, "Ad requests are serialized");
            ads.Pending.SetResult(AdResult.Completed); Expect(await pendingAd == EconomyResult.Applied && !adService.Busy, "Full-screen completion releases busy");
            Pass("rewarded ad skip, save rollback, retry without second video and concurrent requests");
            var path = Path.Combine(root, "saves/slot00.mvs");
            var fixture = new Dictionary<string, string>
            {
                ["qc.session.v1"] = "{}", ["qc.progress.v1"] = "{}", ["qc.settings.v1"] = "{}",
                ["qc.album.v1"] = "{\"futureAlbumField\":17,\"entries\":[{\"levelId\":\"QC001\",\"placements\":[],\"futureEntryField\":42}]}"
            };
            WriteFixture(path, fixture); reload = new SaveAdapter(); Expect(reload.Load(out _), "Legacy save without new modules");
            var migrated = new CampEconomy(reload.Economy, new EconomyRules(), reload.Save);
            Expect(migrated.Hints == 3 && migrated.Lives == 5 && reload.Save(), "Legacy receives initial allowances");
            var blocks = Decode(path); var album = JObject.Parse(Encoding.UTF8.GetString(blocks[SaveFileCodec.ComputeBlockId("qc.album.v1")].Skip(4).ToArray()));
            Expect(album["futureAlbumField"].Value<int>() == 17 && album["entries"][0]["futureEntryField"].Value<int>() == 42, "Unknown optional fields survive round trip");
            Pass("native save module migration and unknown nested JSON preservation");
            fixture["qc.economy.v1"] = "{\"version\":99}"; WriteFixture(path, fixture);
            var before = File.ReadAllBytes(path); reload = new SaveAdapter();
            Expect(!reload.Load(out _) && !reload.Save() && before.SequenceEqual(File.ReadAllBytes(path)), "Future mandatory version never rewrites primary, even with valid backup");
            File.WriteAllText(path, "synthetic corruption"); var corruptBefore = File.ReadAllBytes(path);
            File.WriteAllText(path + ".bak", "synthetic corruption"); reload = new SaveAdapter();
            Expect(!reload.Load(out _) && !reload.Save() && corruptBefore.SequenceEqual(File.ReadAllBytes(path)), "Corrupt slots are not replaced by new defaults");
            Pass("future schema and corrupt primary/backup are write-protected");
            var settings = reload.Settings; reload.ResetAfterErase();
            Expect(ReferenceEquals(settings, reload.Settings) && reload.Save(), "Explicit reset clears read-only and keeps settings references");
            Pass("explicit local reset clears staged extension data and stale write protection");
        }
        finally { Directory.Delete(root, true); }
    }
    static void ContentContracts()
    {
        var resources = UnityEngine.Resources.Root;
        var campaign = JObject.Parse(File.ReadAllText(Path.Combine(resources, "QuietCamp/campaign.json")));
        var main = campaign["mvpLevelIds"].Values<string>().Concat(campaign["generatedLevelIds"].Values<string>()).ToArray();
        Expect(main.Length == 42 && main.Distinct().Count() == 42, "42 campaign IDs across two acts");
        var manifest = JObject.Parse(File.ReadAllText(Path.Combine(resources, "QuietCamp/monetization.json")));
        var journeys = manifest["journeys"].ToObject<JourneyDefinition[]>();
        Expect(JourneyCatalog.Validate(journeys).Count == 0, "Journey manifest valid");
        var dlc = journeys.Single(j => j.id == "lighthouse");
        Expect(dlc.levelIds.Length == 8 && dlc.storyKeys.Length == 8 && !dlc.published, "Lighthouse staging, narrative and sales gate");
        var memories = journeys.Single(j => j.id == "memories");
        Expect(memories.published && memories.requiredCompletions == 15
            && string.IsNullOrEmpty(memories.entitlementId) && memories.currencyCost == 0,
            "Memories is a free, progress-gated side story");
        var bonus = JArray.Parse(File.ReadAllText(Path.Combine(resources, "QuietCamp/bonus_camps.json")))
            .ToObject<BonusCampDefinition[]>();
        var districts = campaign["districts"].ToObject<DistrictDefinition[]>();
        Expect(districts.Length == 6 && districts.All(d => d.from >= 1 && d.to <= main.Length && d.from <= d.to)
            && districts.Sum(d => d.to - d.from + 1) == main.Length && districts.Count(d => d.act == 2) == 3,
            "Six districts tile the campaign; act 2 starts after level 30");
        var sideContent = bonus.Where(b => !string.IsNullOrEmpty(b.levelId)).Select(b => b.levelId)
            .Concat(memories.levelIds).ToArray();
        foreach (var id in main.Concat(dlc.levelIds).Concat(sideContent))
        {
            var level = LevelLoader.Load(id);
            Expect(LevelContentValidator.Validate(level).Count == 0 && RuleEvaluator.Evaluate(level, level.witness).IsSolved, "Frozen content and witness " + id);
            var solver = new CampSolver(level, Array.Empty<Placement>(), 20);
            var status = solver.Step(); while (status == CampSolver.Status.Searching) status = solver.Step(.05);
            Expect(status == CampSolver.Status.Solved && RuleEvaluator.Evaluate(level, solver.Solution).IsSolved, "Independent solver " + id);
            if (dlc.levelIds.Contains(id) || id.StartsWith("gen:", StringComparison.Ordinal))
                Expect(level.contentHash == CampContent.CalculateHash(level), "Frozen hash " + id);
        }
        Pass("42 main, 4 bonus, 3 memories and 8 authored DLC puzzles: validator, saved witness and independent solver");
        var localization = new Dictionary<string, JObject>();
        foreach (var language in new[] { "uk", "en", "de" })
            localization[language] = (JObject)JObject.Parse(File.ReadAllText(Path.Combine(resources, "QuietCampLocales/" + language + ".json")))["entries"];
        var keys = localization["uk"].Properties().Select(p => p.Name).Where(k => k.StartsWith("economy.") || k.StartsWith("purchase.") || k.StartsWith("journey.") || k.StartsWith("memory.") || k.StartsWith("district.")).ToArray();
        foreach (var language in localization)
        {
            foreach (var key in keys) Expect(!string.IsNullOrWhiteSpace(language.Value[key]?.Value<string>()), "Missing translation: " + language.Key + ":" + key);
            foreach (var key in dlc.storyKeys) Expect(language.Value[key] != null, "Missing story text");
        }
        Pass("all new economy, memory, journey and purchase keys localized in uk/en/de");
    }
    static void AuthorLighthouse(string folder)
    {
        if (!Directory.Exists(folder)) throw new InvalidOperationException("Missing target content directory");
        var levels = Enumerable.Range(1, 8).Select(LighthouseJourneyAuthoring.Create).ToArray();
        foreach (var level in levels)
        {
            level.contentHash = CampContent.CalculateHash(level);
            var solver = new CampSolver(level, Array.Empty<Placement>(), 20);
            var status = solver.Step(); while (status == CampSolver.Status.Searching) status = solver.Step(.05);
            Expect(status == CampSolver.Status.Solved && RuleEvaluator.Evaluate(level, solver.Solution).IsSolved, "Independent lighthouse solver");
            Expect(!File.Exists(Path.Combine(folder, level.id + ".json")), "Authoring must not overwrite existing content");
        }
        foreach (var level in levels)
        {
            var path = Path.Combine(folder, level.id + ".json");
            File.WriteAllText(path, Newtonsoft.Json.JsonConvert.SerializeObject(level, Newtonsoft.Json.Formatting.Indented) + "\n");
            File.WriteAllText(path + ".meta", "fileFormatVersion: 2\nguid: " + Guid.NewGuid().ToString("N") + "\nTextScriptImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n");
            Console.WriteLine("AUTHORED " + level.id + " " + level.width + "x" + level.height + " " + level.environment.seasonId + "/" + level.environment.weatherId);
        }
    }
    static void WriteFixture(string path, Dictionary<string, string> values)
    {
        var blocks = new List<(uint, byte[])>();
        foreach (var pair in values)
        {
            var json = Encoding.UTF8.GetBytes(pair.Value); using var buffer = new MemoryStream(); using var binary = new BinaryWriter(buffer);
            binary.Write(json.Length); binary.Write(json); binary.Flush(); blocks.Add((SaveFileCodec.ComputeBlockId(pair.Key), buffer.ToArray()));
        }
        File.WriteAllBytes(path, SaveFileCodec.Encode(blocks));
    }
    static Dictionary<uint, byte[]> Decode(string path)
    {
        var result = SaveFileCodec.TryDecode(File.ReadAllBytes(path), out _, out var blocks, out var error);
        Expect(result == SaveFileCodec.DecodeError.None, "Fixture decode: " + error); return blocks.ToDictionary(b => b.blockId, b => b.payload);
    }
}
