// Deterministic contract probe. SDK API doubles validate the guarded adapter's
// C# flow, not real SDK versions, network, Android forms or mediation ordering.
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GoogleMobileAds.Api;
using GoogleMobileAds.Ump.Api;
using QuietCamp.Application;
using Firebase.Analytics;

class Program
{
    static int _passed;
    static void Check(bool value, string name)
    { if (!value) throw new Exception(name); }
    static async Task Finishes(Task task)
    { Check(await Task.WhenAny(task, Task.Delay(1000)) == task, "Unresolved adapter task"); await task; }
    static GoogleRewardedAdapter NewAd()
    { ConsentInformation.Reset(); RewardedAd.Reset(); return new GoogleRewardedAdapter("test-only"); }
    static void Pass(string name) { _passed++; Console.WriteLine("PASS " + name); }
    static async Task Main()
    {
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
                Check(pair.Value == (pair.Key == ConsentType.AnalyticsStorage ? ConsentStatus.Granted : ConsentStatus.Denied), "Ad consent must be denied");
            provider.ResetLocalData(); provider.Send(record);
            Check(!FirebaseAnalytics.Collection && FirebaseAnalytics.Resets == 1 && FirebaseAnalytics.Events == 1, "Reset withdraws collection");
            Pass("Firebase consent, numeric event and local reset boundary");
        }
        Console.WriteLine(_passed + "/9 contract scenarios passed. SDK doubles only; no real Google/Android integration verified.");
    }
}

namespace UnityEngine
{
    public enum RuntimeInitializeLoadType { BeforeSceneLoad }
    public class RuntimeInitializeOnLoadMethodAttribute : Attribute { public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType type) { } }
    public enum NetworkReachability { NotReachable, ReachableViaLocalAreaNetwork }
    public static class Application { public static NetworkReachability internetReachability = NetworkReachability.ReachableViaLocalAreaNetwork; }
    public static class Debug { public static void Log(string message) { } }
}
namespace QuietCamp.Application
{
    public interface IAdPrivacyOptions { bool PrivacyOptionsRequired { get; } Task ShowPrivacyOptions(); }
    public interface IAdRequestCancellation { bool CanCancelPending { get; } void CancelPending(); }
    public interface IComfortTransport : IDisposable { bool Available { get; } Task<bool> Initialize(); void SetConsent(bool value); void Send(ComfortRecord record); void ResetLocalData(); }
    public class ComfortRecord
    {
        public string Name; public IReadOnlyDictionary<string, long> Values;
        public ComfortRecord(string name, Dictionary<string, long> values) { Name = name; Values = values; }
    }
}
namespace QuietCamp.Infrastructure
{
    public class GoogleServicesConfiguration { public string androidRewardedUnitId; }
    public static class OptionalGoogleServices
    {
        public static Func<IComfortTransport> AnalyticsFactory;
        public static Func<GoogleServicesConfiguration, IAdService> AdsFactory;
    }
}
namespace GoogleMobileAds.Ump.Api
{
    public enum PrivacyOptionsRequirementStatus { Required, NotRequired }
    public class ConsentRequestParameters { }
    public class FormError { }
    public static class ConsentInformation
    {
        public static PrivacyOptionsRequirementStatus PrivacyOptionsRequirementStatus => PrivacyOptionsRequirementStatus.Required;
        public static int Updates; public static bool HoldForm, HoldPrivacy, ThrowPrivacy;
        public static Action<FormError> FormCallback;
        public static void Update(ConsentRequestParameters parameters, Action<FormError> callback) { Updates++; callback(null); }
        public static bool CanRequestAds() => true;
        public static void Reset() { Updates = 0; HoldForm = HoldPrivacy = ThrowPrivacy = false; FormCallback = null; }
    }
    public static class ConsentForm
    {
        public static void LoadAndShowConsentFormIfRequired(Action<FormError> callback)
        { ConsentInformation.FormCallback = callback; if (!ConsentInformation.HoldForm) callback(null); }
        public static void ShowPrivacyOptionsForm(Action<FormError> callback)
        { if (ConsentInformation.ThrowPrivacy) throw new InvalidOperationException("Native form unavailable"); if (!ConsentInformation.HoldPrivacy) callback(null); }
    }
}
namespace GoogleMobileAds.Api
{
    public class AdRequest { }
    public class Reward { }
    public class AdError { }
    public class LoadAdError { }
    public class InitializationStatus { }
    public static class MobileAds
    {
        public static bool RaiseAdEventsOnUnityMainThread;
        public static void Initialize(Action<InitializationStatus> callback) => callback(new InitializationStatus());
    }
    public class RewardedAd
    {
        public static RewardedAd Last; public static bool HoldLoad;
        static Action<RewardedAd, LoadAdError> _load;
        public bool Destroyed;
        public event Action OnAdFullScreenContentClosed;
        public event Action<AdError> OnAdFullScreenContentFailed;
        Action<Reward> _reward;
        public static void Reset() { Last = null; HoldLoad = false; _load = null; }
        public static void Load(string unit, AdRequest request, Action<RewardedAd, LoadAdError> callback)
        { _load = callback; if (!HoldLoad) CompleteLoad(); }
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
    public enum DependencyStatus { Available }
    public class FirebaseApp
    {
        public static FirebaseApp DefaultInstance => new FirebaseApp();
        public static Task<DependencyStatus> CheckAndFixDependenciesAsync() => Task.FromResult(DependencyStatus.Available);
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
