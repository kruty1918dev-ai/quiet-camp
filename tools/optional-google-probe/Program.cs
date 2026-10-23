// Deterministic contract probe. SDK API doubles validate the guarded adapter's
// C# flow, not real SDK versions, network, Android forms or mediation ordering.
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GoogleMobileAds.Api;
using GoogleMobileAds.Ump.Api;
using QuietCamp.Application;
using Firebase.Analytics;
using QuietCamp.Infrastructure;

class Program
{
    static int _passed;
    static void Check(bool value, string name)
    { if (!value) throw new Exception(name); }
    static async Task Finishes(Task task)
    { Check(await Task.WhenAny(task, Task.Delay(1000)) == task, "Unresolved adapter task"); await task; }
    static GoogleRewardedAdapter NewAd()
    { ConsentInformation.Reset(); RewardedAd.Reset(); return new GoogleRewardedAdapter("test-only", authorized: () => true); }
    static void Pass(string name) { _passed++; Console.WriteLine("PASS " + name); }
    static async Task Main()
    {
        var config = new GoogleServicesConfiguration { rewardedAdsEnabled = true, useTestAds = true,
            androidRewardedUnitId = "publisher-android", iosRewardedUnitId = "publisher-ios" };
        Check(config.RewardedUnitId(false) == GoogleServicesConfiguration.AndroidTestRewardedUnitId
            && config.RewardedUnitId(true) == GoogleServicesConfiguration.IosTestRewardedUnitId, "Test mode must override publisher IDs");
        Check(config.CanUseRewardedAds(true) && !config.CanUseRewardedAds(false), "Test mode is development only");
        OptionalGoogleServices.AdsFactory = c => new GoogleRewardedAdapter(c.CurrentRewardedUnitId, c.TestAdsActive, () => c.AdvertisingAllowed);
        UnityEngine.Debug.isDebugBuild = false;
        Check(!OptionalGoogleServices.Ads(config).IsReady, "Release must not activate sample ads");
        UnityEngine.Debug.isDebugBuild = true;
        var sampleSettings = new SettingsSaveData { privacyAgeBand = 4, privacyAgeVersion = 1, advertisingConsent = true, advertisingPolicyVersion = 1, advertisingRevision = "review" };
        config.BindPrivacy(sampleSettings, "review");
        using (var testProvider = (PrivacyAdService)OptionalGoogleServices.Ads(config))
        {
            ConsentInformation.Reset(); RewardedAd.Reset();
            var task = testProvider.ShowRewarded("camp.life");
            Check(ConsentInformation.Updates == 0 && !testProvider.PrivacyOptionsRequired, "Sample inventory needs no publisher UMP message");
            RewardedAd.Last.Earn(); Check(!task.IsCompleted, "Sample reward must wait for close");
            RewardedAd.Last.Close(); Check(await task == AdResult.Completed, "Sample reward callback");
        }
        config.useTestAds = false;
        Check(!config.CanUseRewardedAds(true) && !OptionalGoogleServices.Ads(config).IsReady, "Live units require publication even in development");
        Check(config.RewardedUnitId(true) == "publisher-ios", "Live iOS unit selection");
        OptionalGoogleServices.AdsFactory = null;
        Pass("sample IDs, development gate, live publication gate and sample reward");
        using (var provider = new GoogleRewardedAdapter("publisher-unit", true, () => true))
        {
            ConsentInformation.Reset(); RewardedAd.Reset();
            var task = provider.ShowRewarded("camp.life");
            Check(ConsentInformation.Updates == 1, "A publisher unit cannot bypass consent via test flag");
            RewardedAd.Last.Close(); Check(await task == AdResult.Skipped, "No reward on dismissal");
            Pass("publisher unit cannot bypass UMP");
        }
        using (var provider = NewAd())
        {
            await provider.RefreshConsent();
            Check(ConsentInformation.Updates == 1 && RewardedAd.Last == null, "Launch refresh must not load an ad");
            Pass("publisher launch refresh requests consent information without loading an ad");
        }
        using (var provider = NewAd())
        {
            UnityEngine.Application.internetReachability = UnityEngine.NetworkReachability.NotReachable;
            Check(await provider.ShowRewarded("album.lantern") == AdResult.Unavailable && ConsentInformation.Updates == 0, "Offline must not request");
            UnityEngine.Application.internetReachability = UnityEngine.NetworkReachability.ReachableViaLocalAreaNetwork;
            Pass("offline makes no request");
        }
        using (var provider = NewAd())
        {
            var task = provider.ShowRewarded("album.lantern");
            Check(await provider.ShowRewarded("album.lantern") == AdResult.Unavailable && ConsentInformation.Updates == 1, "Duplicate request");
            RewardedAd.Last.Earn(); Check(!task.IsCompleted, "Reward before close must keep gate");
            RewardedAd.Last.Close(); Check(await task == AdResult.Completed, "Completed needs reward and close");
            Pass("one request, reward waits for dismissal");
        }
        using (var provider = NewAd())
        {
            var task = provider.ShowRewarded("album.lantern"); RewardedAd.Last.Close();
            Check(await task == AdResult.Skipped, "Close alone must not reward"); Pass("dismissal without reward");
        }
        using (var provider = NewAd())
        {
            var task = provider.ShowRewarded("album.lantern"); RewardedAd.Last.Fail();
            Check(await task == AdResult.Failed, "Show failure must not reward"); Pass("full-screen failure");
        }
        using (var provider = NewAd())
        {
            RewardedAd.HoldLoad = true;
            var task = provider.ShowRewarded("album.lantern"); Check(provider.CanCancelPending, "Loading is cancellable");
            provider.CancelPending(); await Finishes(task); Check(await task == AdResult.Unavailable, "Cancelled load");
            RewardedAd.CompleteLoad(); Check(RewardedAd.Last.Destroyed, "Late cancelled load leaks ad");
            Pass("cancelled load destroys late ad");
        }
        using (var provider = NewAd())
        {
            var task = provider.ShowRewarded("album.lantern"); var shown = RewardedAd.Last;
            Check(!provider.CanCancelPending, "Showing cannot release gate");
            provider.Dispose(); await Finishes(task);
            Check(await task == AdResult.Unavailable && shown.Destroyed, "Dispose while showing must release task without reward");
            Pass("dispose releases pending full-screen task");
        }
        using (var provider = NewAd())
        {
            ConsentInformation.HoldForm = true;
            var task = provider.ShowRewarded("album.lantern");
            Check(!task.IsCompleted && !provider.CanCancelPending, "Native consent form must retain gate");
            provider.CancelPending(); Check(!task.IsCompleted, "Cancel released a native form");
            provider.Dispose(); await Finishes(task); Check(await task == AdResult.Unavailable, "Dispose during consent");
            ConsentInformation.FormCallback(null); Check(RewardedAd.Last == null, "Stale form requested ad");
            Pass("native consent form keeps gate, dispose suppresses stale callback");
        }
        using (var provider = NewAd())
        {
            ConsentInformation.HoldPrivacy = true;
            var task = provider.ShowPrivacyOptions(); Check(!provider.IsReady, "Privacy and ad must not overlap");
            provider.Dispose(); await Finishes(task);
        }
        using (var provider = NewAd())
        {
            ConsentInformation.ThrowPrivacy = true;
            await Finishes(provider.ShowPrivacyOptions()); Check(provider.IsReady, "Privacy failure left provider busy");
            Pass("privacy options dispose and native error release task");
        }
        using (var provider = new FirebaseComfortTransport())
        {
            Check(await provider.Initialize(), "Dependencies available");
            var record = new ComfortRecord("qc_screen", new Dictionary<string, long> { ["screen"] = 1 });
            provider.Send(record); Check(FirebaseAnalytics.Events == 0, "No consent event");
            provider.SetConsent(true); provider.Send(record);
            Check(FirebaseAnalytics.Events == 1 && FirebaseAnalytics.Collection, "Consent enables one event");
            foreach (var pair in FirebaseAnalytics.Consent)
                Check(pair.Value == (pair.Key == ConsentType.AnalyticsStorage ? Firebase.Analytics.ConsentStatus.Granted : Firebase.Analytics.ConsentStatus.Denied), "Ad consent must be denied");
            provider.ResetLocalData(); provider.Send(record);
            Check(!FirebaseAnalytics.Collection && FirebaseAnalytics.Resets == 1 && FirebaseAnalytics.Events == 1, "Reset withdraws collection");
            Pass("Firebase consent, numeric event and local reset boundary");
        }
        await CheckConsentSaveFailures();
        await CheckAdvertisingPrivacy();
        await CheckPendingReports();
        await CheckCrashPrivacy();
        await CheckRevenueModes();
        await CheckProductionBoundaries();
        Console.WriteLine(_passed + " contract scenarios passed. SDK doubles only; no real Google/Android integration verified.");
    }
    static async Task CheckProductionBoundaries()
    {
        ConsentInformation.Reset(); RewardedAd.Reset();
        var stages = new List<int>(); int earned = 0;
        using (var provider = new GoogleRewardedAdapter("publisher-life", authorized: () => true,
            selectUnit: placement => placement == "camp.hint" ? "publisher-hint" : placement == "camp.interstitial" ? "publisher-interstitial" : "publisher-life",
            earned: _ => earned++, observed: (_, stage) => stages.Add(stage)))
        {
            var hint = provider.ShowRewarded("camp.hint");
            Check(RewardedAd.LastUnit == "publisher-hint", "Hint must select its own unit");
            RewardedAd.Last.Earn(); RewardedAd.Last.Earn();
            Check(earned == 1 && !hint.IsCompleted, "Earned journal runs once before close");
            RewardedAd.Last.Close(); Check(await hint == AdResult.Completed, "Hint completes after close");
            Check(string.Join(",", stages) == "0,1,2,3", "Lifecycle events follow native load/show/close");
            var interstitial = provider.ShowInterstitial("camp.interstitial");
            Check(!interstitial.IsCompleted, "Interstitial keeps the game gate until native close");
            InterstitialAd.Last.Close(); Check(await interstitial == AdResult.Completed && earned == 1, "Interstitial never grants a reward");
            Pass("placement routing, native lifecycle, duplicate earned callback and interstitial dismissal");
        }
        FirebaseConsentSignals.ResetDenied();
        FirebaseConsentSignals.SetAdvertising(true, true, false);
        using (var analytics = new FirebaseComfortTransport())
        {
            await analytics.Initialize(); analytics.SetConsent(true); analytics.ResetLocalData();
            Check(FirebaseAnalytics.Consent[ConsentType.AnalyticsStorage] == Firebase.Analytics.ConsentStatus.Denied
                && FirebaseAnalytics.Consent[ConsentType.AdStorage] == Firebase.Analytics.ConsentStatus.Granted
                && FirebaseAnalytics.Consent[ConsentType.AdUserData] == Firebase.Analytics.ConsentStatus.Granted
                && FirebaseAnalytics.Consent[ConsentType.AdPersonalization] == Firebase.Analytics.ConsentStatus.Denied,
                "Analytics withdrawal/reset must preserve independent advertising decisions");
        }
        FirebaseConsentSignals.ResetDenied();
        Pass("one Firebase consent owner preserves independent advertising purposes through Analytics withdrawal");
    }
    static async Task CheckRevenueModes()
    {
        ConsentInformation.Reset(); RewardedAd.Reset(); ConsentInformation.ConsentStatus = GoogleMobileAds.Ump.Api.ConsentStatus.NotRequired;
        int writes = 0;
        using (var provider = new GoogleRewardedAdapter("publisher-unit", authorized: () => true, underAge: () => true,
            limitedApproved: () => true, applyStorageMode: limited => { Check(limited, "Under-age path must force LTD before init"); writes++; return true; }))
        {
            await provider.RefreshConsent(); Check(ConsentInformation.Updates == 0, "No under-age startup UMP network request");
            var view = provider.ShowRewarded("camp.hint");
            Check(ConsentInformation.LastParameters.TagForUnderAgeOfConsent, "Under-age UMP tag must be explicit");
            Check(ConsentInformation.FormLoads == 0 && !provider.PrivacyOptionsRequired, "No adult consent form for under-age users");
            Check(MobileAds.Configuration.AgeRestrictedTreatment == AgeRestrictedTreatment.Child && MobileAds.Configuration.MaxAdContentRating == MaxAdContentRating.G,
                "Under digital-consent age needs Child treatment / G content");
            Check(writes == 1 && RewardedAd.Request.Extras["npa"] == "1", "Under-age request must retain restricted parameters");
            RewardedAd.Last.Earn(); RewardedAd.Last.Close(); Check(await view == AdResult.Completed, "Restricted path still grants the promised reward");
            Pass("13-15 approved LTD request is explicit, tagged, no adult form, correct reward lifecycle");
        }
        ConsentInformation.Reset(); RewardedAd.Reset();
        using (var provider = new GoogleRewardedAdapter("publisher-unit", authorized: () => true, underAge: () => true))
        {
            Check(await provider.ShowRewarded("camp.life") == AdResult.Unavailable && ConsentInformation.Updates == 0 && RewardedAd.Last == null,
                "A missing LTD approval must not become an under-age normal ad request");
            Pass("unapproved under-age mode cannot fall back to normal ads");
        }
        ConsentInformation.Reset(); RewardedAd.Reset(); ConsentInformation.ConsentStatus = GoogleMobileAds.Ump.Api.ConsentStatus.Required;
        using (var provider = new GoogleRewardedAdapter("publisher-unit", authorized: () => true, limitedApproved: () => true,
            applyStorageMode: limited => { Check(limited, "Denied NPA storage must use LTD override"); return true; }))
        {
            var view = provider.ShowRewarded("camp.life"); Check(ConsentInformation.FormLoads == 1, "Adult LTD must still use the required fresh UMP form");
            RewardedAd.Last.Close(); Check(await view == AdResult.Skipped, "No earned callback, no reward");
            Pass("approved adult LTD preserves UMP and does not turn tracking refusal into a forced NPA request");
        }
        ConsentInformation.Reset(); RewardedAd.Reset(); ConsentInformation.ConsentStatus = GoogleMobileAds.Ump.Api.ConsentStatus.Required;
        using (var provider = new GoogleRewardedAdapter("publisher-unit", authorized: () => true, limitedApproved: () => true, applyStorageMode: _ => false))
        {
            Check(await provider.ShowRewarded("camp.life") == AdResult.Unavailable && RewardedAd.Last == null, "Failed LTD storage control cannot load an ad");
            Pass("failed LTD preference write prevents loading");
        }
        foreach (bool underAge in new[] { false, true })
        {
            ConsentInformation.Reset(); RewardedAd.Reset(); ConsentInformation.RequestsAllowed = false;
            using var provider = new GoogleRewardedAdapter("publisher-unit", authorized: () => true, underAge: () => underAge,
                limitedApproved: () => true, applyStorageMode: _ => throw new Exception("Must stop before storage/initialization"));
            Check(await provider.ShowRewarded("camp.hint") == AdResult.Unavailable && RewardedAd.Last == null,
                "Limited Ads approval cannot override UMP CanRequestAds=false");
            Pass(underAge ? "under-age LTD honors UMP denial" : "adult LTD honors UMP denial");
        }
        foreach (bool teen in new[] { false, true })
        {
            ConsentInformation.Reset(); RewardedAd.Reset(); ConsentInformation.ConsentStatus = GoogleMobileAds.Ump.Api.ConsentStatus.NotRequired;
            using var provider = new GoogleRewardedAdapter("publisher-unit", authorized: () => true, teen: () => teen, personalizedEligible: () => true);
            var view = provider.ShowRewarded("camp.life");
            Check(RewardedAd.Request.Extras.ContainsKey("npa") == teen, "Only approved adults may omit the forced NPA extra; Google still enforces UMP");
            RewardedAd.Last.Close(); await view;
            Pass(teen ? "teen requests remain NPA despite adult-personalization feature flag" : "approved adult request lets Google apply the actual UMP advertising mode");
        }
        ConsentInformation.Reset(); RewardedAd.Reset(); ConsentInformation.ConsentStatus = GoogleMobileAds.Ump.Api.ConsentStatus.NotRequired;
    }
    static async Task CheckConsentSaveFailures()
    {
        foreach (var purpose in new[] { "advertising", "crash" })
        {
            var settings = new SettingsSaveData { privacyAgeBand = 4, privacyAgeVersion = 1 };
            bool writable = true; int writes = 0;
            var persistence = new PrivacyConsentPersistence(settings, () => { writes++; return writable; });
            using var analytics = new ComfortAnalytics(settings, new FirebaseComfortTransport(),
                () => { if (!persistence.Save()) throw new InvalidOperationException("Synthetic save failure"); }, "current");
            using var crashes = new CrashDiagnostics(settings, new Crashes(), persistence.Save, "current");
            persistence.Attach(analytics, crashes, new AdServiceStub());
            await analytics.SetConsent(true);
            Check(FirebaseAnalytics.Collection && analytics.Collecting, "Analytics must be active before regression trigger");
            int before = writes; writable = false;
            if (purpose == "advertising") PrivacyEligibility.SetAdvertising(settings, true, "current", persistence.Save);
            else await crashes.SetConsent(true);
            Check(!FirebaseAnalytics.Collection && !analytics.Collecting, purpose + " save failure must disable automatic SDK Analytics collection");
            Check(writes == before + 1, "Revocation cannot recursively save");
            foreach (var consent in FirebaseAnalytics.Consent.Values)
                Check(consent == Firebase.Analytics.ConsentStatus.Denied, "Every Firebase storage purpose must be denied after failed save");
            Pass("active SDK Analytics is disabled after failed " + purpose + " consent persistence");
        }
    }
    static async Task CheckAdvertisingPrivacy()
    {
        ConsentInformation.Reset(); RewardedAd.Reset();
        using (var denied = new GoogleRewardedAdapter("publisher-unit"))
        {
            await denied.RefreshConsent();
            Check(await denied.ShowRewarded("camp.life") == AdResult.Unavailable && ConsentInformation.Updates == 0,
                "Default-denied adapter must not contact UMP");
        }
        Pass("adapter defaults to no UMP and no ad requests");
        using (var privacy = new GoogleRewardedAdapter("publisher-unit", authorized: () => false, eligible: () => true))
        {
            await privacy.RefreshConsent(); Check(ConsentInformation.Updates == 0, "Withdrawal prevents automatic UMP");
            await privacy.ShowPrivacyOptions();
            Check(ConsentInformation.Updates == 1 && RewardedAd.Last == null, "Explicit privacy management must not initialize an ad");
        }
        Pass("eligible player can explicitly reopen Google choices after local ad withdrawal");
        foreach (bool teen in new[] { false, true })
        {
            ConsentInformation.Reset(); RewardedAd.Reset();
            using var ad = new GoogleRewardedAdapter("publisher-unit", authorized: () => true, teen: () => teen);
            var result = ad.ShowRewarded("camp.life");
            Check(RewardedAd.Request.Extras["npa"] == "1", "Every rewarded request must specify NPA");
            Check(MobileAds.Configuration.AgeRestrictedTreatment == (teen ? AgeRestrictedTreatment.Teen : AgeRestrictedTreatment.Unspecified), "Age treatment");
            ad.CancelPending(); RewardedAd.Last.Earn();
            Check(!result.IsCompleted, "Revocation cannot release a still-visible native ad");
            RewardedAd.Last.Close(); Check(await result == AdResult.Unavailable, "Revoked request must not grant after re-enable");
        }
        Pass("NPA, adult/teen signals and revoked visible reward waits for close");
        ConsentInformation.Reset(); RewardedAd.Reset();
        ConsentInformation.ConsentStatus = GoogleMobileAds.Ump.Api.ConsentStatus.Obtained;
        using (var ad = new GoogleRewardedAdapter("publisher-unit", authorized: () => true))
            Check(await ad.ShowRewarded("camp.life") == AdResult.Unavailable && RewardedAd.Last == null,
                "CanRequestAds alone cannot substitute for device-storage consent");
        ConsentInformation.ConsentStatus = GoogleMobileAds.Ump.Api.ConsentStatus.NotRequired;
        Pass("no Limited Ads fallback without additional device-access signal");
    }
    static async Task CheckPendingReports()
    {
        var first = new TaskCompletionSource<bool>();
        var operations = new List<string>();
        using (var crash = new FirebaseCrashTransport(method =>
        { operations.Add(method); return operations.Count == 1 ? first.Task : Task.CompletedTask; }))
        {
            Check(await crash.Initialize(), "Crash test initialization");
            int before = Firebase.Crashlytics.Crashlytics.Reports;
            crash.SetConsent(true, true); crash.SetConsent(false); crash.Report(CrashArea.Save);
            Check(operations.Count == 1, "Report operations must serialize");
            first.SetResult(true); await Task.Delay(20);
            crash.Report(CrashArea.Save);
            Check(operations.Count == 2 && operations[0] == "sendUnsentReports" && operations[1] == "deleteUnsentReports"
                && Firebase.Crashlytics.Crashlytics.Reports == before, "Late send completion cannot restore consent");
        }
        Pass("pending send/delete order and late completion after withdrawal");
        using (var crash = new FirebaseCrashTransport(_ => Task.FromException(new TimeoutException())))
        {
            await crash.Initialize(); int before = Firebase.Crashlytics.Crashlytics.Reports;
            crash.SetConsent(true); crash.SetConsent(true, true); crash.Report(CrashArea.Save);
            Check(Firebase.Crashlytics.Crashlytics.Reports == before, "A failed native pending operation stays disabled until restart");
        }
        Pass("failed pending report operation cannot be followed by enabled collection");
    }
    sealed class Crashes : ICrashTransport
    {
        public bool Available => true;
        public bool Enabled, SentPrevious;
        public int Reports;
        public TaskCompletionSource<bool> Pending;
        public Task<bool> Initialize() => Pending?.Task ?? Task.FromResult(true);
        public void SetConsent(bool enabled, bool sendPreviousSession = false)
        { Enabled = enabled; SentPrevious = enabled && sendPreviousSession; }
        public void SetLevel(int level) { }
        public void Report(CrashArea area) { Reports++; }
        public void Dispose() { Enabled = false; }
    }
    static async Task CheckCrashPrivacy()
    {
        var settings = new SettingsSaveData { privacyAgeBand = 4, privacyAgeVersion = 1 }; var transport = new Crashes();
        using (var service = new CrashDiagnostics(settings, transport, () => true, "review"))
        {
            await service.RestoreConsent(); service.Report(CrashArea.Save);
            Check(!transport.Enabled && transport.Reports == 0, "Default crash consent must be off");
            await service.SetConsent(true); service.Report(CrashArea.Save); service.Report(CrashArea.Save);
            Check(transport.Enabled && !transport.SentPrevious && transport.Reports == 1, "Fresh opt-in cannot send previous reports; duplicate boundary is suppressed");
            Check(!settings.analyticsConsent, "Crash consent must not enable analytics");
            await service.SetConsent(false); service.Report(CrashArea.DevelopmentTest);
            Check(!transport.Enabled && !settings.crashSessionEligible && transport.Reports == 1, "Withdrawal must stop diagnostics");
        }
        Pass("crash default-off, separate consent, first opt-in, deduplication and withdrawal");
        settings = new SettingsSaveData { privacyAgeBand = 4, privacyAgeVersion = 1 }; transport = new Crashes { Pending = new TaskCompletionSource<bool>() };
        using (var service = new CrashDiagnostics(settings, transport, () => true, "r1"))
        {
            var enabling = service.SetConsent(true); var revoking = service.SetConsent(false);
            transport.Pending.SetResult(true); await Task.WhenAll(enabling, revoking);
            Check(!service.Collecting && !transport.Enabled, "Pending initialization cannot override withdrawal");
        }
        Pass("crash consent withdrawal wins over pending dependency check");
        settings = new SettingsSaveData { privacyAgeBand = 4, privacyAgeVersion = 1, crashReportingConsent = true, crashPolicyVersion = CrashDiagnostics.PolicyVersion,
            crashPublicationRevision = "r1", crashSessionEligible = false };
        transport = new Crashes();
        using (var service = new CrashDiagnostics(settings, transport, () => true, "r1"))
        {
            await service.RestoreConsent();
            Check(transport.Enabled && !transport.SentPrevious && settings.crashSessionEligible, "A launch without prior consent is discarded");
        }
        using (var service = new CrashDiagnostics(settings, transport, () => true, "r1"))
        {
            await service.RestoreConsent(); Check(transport.SentPrevious, "Only an eligible previous session can be uploaded");
        }
        using (var service = new CrashDiagnostics(settings, transport, () => true, "r2"))
        {
            await service.RestoreConsent(); Check(!transport.Enabled && !transport.SentPrevious, "New publication invalidates crash consent");
        }
        Pass("crash restart receipt, previous-session eligibility and publication revision");
        using (var service = new CrashDiagnostics(new SettingsSaveData { privacyAgeBand = 4, privacyAgeVersion = 1 }, new Crashes(), () => false, "r1"))
        { await service.SetConsent(true); Check(!service.Collecting, "Failed consent save cannot enable diagnostics"); }
        Pass("crash consent save failure stays disabled");
        using (var sdk = new FirebaseCrashTransport())
        {
            Check(await sdk.Initialize(), "Shared Firebase initialization");
            sdk.Report(CrashArea.Save); Check(Firebase.Crashlytics.Crashlytics.Reports == 0, "No diagnostic before explicit consent");
            sdk.SetConsent(true); sdk.SetLevel(7); sdk.Report(CrashArea.DevelopmentTest);
            Check(Firebase.Crashlytics.Crashlytics.Reports == 1 && !Firebase.Crashlytics.Crashlytics.IsCrashlyticsCollectionEnabled,
                "Manual diagnostic keeps persistent auto-upload disabled");
            sdk.SetConsent(false); sdk.Report(CrashArea.Save); Check(Firebase.Crashlytics.Crashlytics.Reports == 1, "SDK transport withdrawal");
            Check(Firebase.FirebaseApp.Checks == 1, "Analytics and crashes share a single Firebase dependency check");
        }
        Pass("Firebase single initialization and crash SDK transport consent boundary");
        var config = new GoogleServicesConfiguration { analyticsEnabled = true, crashlyticsEnabled = true };
        Check(!await OptionalGoogleServices.InitializeFirebase(config) && !OptionalGoogleServices.Crashes(config).Available,
            "Review draft cannot start Firebase or crash uploads");
        Pass("policy review blocks Firebase startup and Crashlytics factory");
        typeof(FirebaseRuntime).GetMethod("Reset", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic).Invoke(null, null);
        Firebase.FirebaseApp.Result = Firebase.DependencyStatus.Unavailable;
        using (var analytics = new FirebaseComfortTransport())
        using (var crashes = new FirebaseCrashTransport())
        {
            Check(!await analytics.Initialize() && !await crashes.Initialize(), "Dependency failure must leave adapters unavailable");
            analytics.SetConsent(true); crashes.SetConsent(true);
            Check(!FirebaseRuntime.Ready && !Firebase.Analytics.FirebaseAnalytics.Collection, "Dependency failure must not enable analytics");
        }
        Pass("Firebase dependency failure leaves optional adapters disabled without blocking gameplay");
    }
}

namespace UnityEngine
{
    public enum RuntimeInitializeLoadType { BeforeSceneLoad, SubsystemRegistration }
    public class RuntimeInitializeOnLoadMethodAttribute : Attribute { public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType type) { } }
    public enum NetworkReachability { NotReachable, ReachableViaLocalAreaNetwork }
    public enum RuntimePlatform { Android, IPhonePlayer }
    public static class Application
    {
        public static string version = "probe";
        public static bool isEditor;
        public static RuntimePlatform platform = RuntimePlatform.Android;
        public static NetworkReachability internetReachability = NetworkReachability.ReachableViaLocalAreaNetwork;
    }
    public static class Debug { public static bool isDebugBuild; public static void Log(string message) { } public static void LogWarning(string message) { } }
    public class TextAsset { public string text; }
    public static class Resources { public static T Load<T>(string path) where T : class => null; }
    public static class JsonUtility { public static T FromJson<T>(string json) => System.Text.Json.JsonSerializer.Deserialize<T>(json); }
}
namespace QuietCamp.Application
{
    public sealed class SettingsSaveData
    {
        public bool analyticsConsent, crashReportingConsent, crashSessionEligible, advertisingConsent, googlePrivacyOptionsRequired, optionalVideoBonuses = true;
        public int privacySetupVersion;
        public string language, privacyAcknowledgementRevision, privacyAcknowledgementHash, privacyAcknowledgementLanguage, privacyAcknowledgedUtc;
        public bool privacyAcknowledgementDraft;
        public int privacyAgeBand, privacyAgeVersion, advertisingPolicyVersion, analyticsPolicyVersion;
        public string advertisingRevision, analyticsPublicationRevision;
        public int crashPolicyVersion;
        public string crashPublicationRevision;
    }
    public interface IAdPrivacyOptions { bool PrivacyOptionsRequired { get; } Task ShowPrivacyOptions(); }
    public interface IAdRequestCancellation { bool CanCancelPending { get; } void CancelPending(); }

}
namespace GoogleMobileAds.Ump.Api
{
    public enum PrivacyOptionsRequirementStatus { Required, NotRequired }
    public enum ConsentStatus { NotRequired, Required, Obtained }
    public class ConsentRequestParameters { public bool TagForUnderAgeOfConsent; }
    public class FormError { }
    public static class ConsentInformation
    {
        public static PrivacyOptionsRequirementStatus PrivacyOptionsRequirementStatus => PrivacyOptionsRequirementStatus.Required;
        public static ConsentStatus ConsentStatus = GoogleMobileAds.Ump.Api.ConsentStatus.NotRequired;
        public static ConsentRequestParameters LastParameters;
        public static int FormLoads;
        public static int Updates; public static bool HoldForm, HoldPrivacy, ThrowPrivacy, RequestsAllowed = true;
        public static Action<FormError> FormCallback;
        public static void Update(ConsentRequestParameters parameters, Action<FormError> callback) { Updates++; LastParameters = parameters; callback(null); }
        public static bool CanRequestAds() => RequestsAllowed;
        public static void Reset() { Updates = FormLoads = 0; HoldForm = HoldPrivacy = ThrowPrivacy = false; RequestsAllowed = true; FormCallback = null; }
    }
    public static class ConsentForm
    {
        public static void LoadAndShowConsentFormIfRequired(Action<FormError> callback)
        { ConsentInformation.FormLoads++; ConsentInformation.FormCallback = callback; if (!ConsentInformation.HoldForm) callback(null); }
        public static void ShowPrivacyOptionsForm(Action<FormError> callback)
        { if (ConsentInformation.ThrowPrivacy) throw new InvalidOperationException("Native form unavailable"); if (!ConsentInformation.HoldPrivacy) callback(null); }
    }
}
namespace GoogleMobileAds.Api
{
    public enum AgeRestrictedTreatment { Unspecified, Teen, Child }
    public enum MaxAdContentRating { T, G }
    public class RequestConfiguration { public AgeRestrictedTreatment AgeRestrictedTreatment; public MaxAdContentRating MaxAdContentRating; }
    public class AdRequest { public Dictionary<string,string> Extras = new Dictionary<string,string>(); }
    public class Reward { }
    public class AdError { }
    public class LoadAdError { }
    public class InitializationStatus { }
    public static class MobileAds
    {
        public static RequestConfiguration Configuration;
        public static void SetRequestConfiguration(RequestConfiguration value) { Configuration = value; }
        public static bool RaiseAdEventsOnUnityMainThread;
        public static void Initialize(Action<InitializationStatus> callback) => callback(new InitializationStatus());
    }
    public class InterstitialAd
    {
        public static InterstitialAd Last;
        public bool Destroyed;
        public event Action OnAdFullScreenContentClosed;
        public event Action<AdError> OnAdFullScreenContentFailed;
        public static void Load(string unit, AdRequest request, Action<InterstitialAd, LoadAdError> callback)
        { Last = new InterstitialAd(); callback(Last, null); }
        public bool CanShowAd() => true;
        public void Show() { }
        public void Close() => OnAdFullScreenContentClosed?.Invoke();
        public void Fail() => OnAdFullScreenContentFailed?.Invoke(new AdError());
        public void Destroy() => Destroyed = true;
    }
    public class RewardedAd
    {
        public static string LastUnit;
        public static AdRequest Request;
        public static RewardedAd Last; public static bool HoldLoad;
        static Action<RewardedAd, LoadAdError> _load;
        public bool Destroyed;
        public event Action OnAdFullScreenContentClosed;
        public event Action<AdError> OnAdFullScreenContentFailed;
        Action<Reward> _reward;
        public static void Reset() { Last = null; HoldLoad = false; _load = null; }
        public static void Load(string unit, AdRequest request, Action<RewardedAd, LoadAdError> callback)
        { LastUnit = unit; Request = request; _load = callback; if (!HoldLoad) CompleteLoad(); }
        public static void CompleteLoad() { Last = new RewardedAd(); _load(Last, null); }
        public bool CanShowAd() => !Destroyed;
        public void Show(Action<Reward> callback) => _reward = callback;
        public void Earn() => _reward(new Reward());
        public void Close() => OnAdFullScreenContentClosed();
        public void Fail() => OnAdFullScreenContentFailed(new AdError());
        public void Destroy() => Destroyed = true;
    }
}
namespace Firebase
{
    public enum DependencyStatus { Available, Unavailable }
    public class FirebaseApp
    {
        public static int Checks;
        public static DependencyStatus Result = DependencyStatus.Available;
        public static FirebaseApp DefaultInstance => new FirebaseApp();
        public static Task<DependencyStatus> CheckAndFixDependenciesAsync() { Checks++; return Task.FromResult(Result); }
    }
}
namespace Firebase.Crashlytics
{
    public static class Crashlytics
    {
        public static bool IsCrashlyticsCollectionEnabled, ReportUncaughtExceptionsAsFatal;
        public static int Reports;
        public static void SetCustomKey(string key, string value) { }
        public static void LogException(Exception error) { Reports++; }
    }
}
namespace Firebase.Analytics
{
    public enum ConsentType { AnalyticsStorage, AdStorage, AdUserData, AdPersonalization }
    public enum ConsentStatus { Granted, Denied }
    public class Parameter { public Parameter(string name, long value) { } }
    public static class FirebaseAnalytics
    {
        public static bool Collection; public static int Events, Resets;
        public static Dictionary<ConsentType, ConsentStatus> Consent;
        public static void SetConsent(Dictionary<ConsentType, ConsentStatus> value) => Consent = value;
        public static void SetAnalyticsCollectionEnabled(bool value) => Collection = value;
        public static void LogEvent(string name, Parameter[] parameters) { Events++; }
        public static void ResetAnalyticsData() { Resets++; }
    }
}
