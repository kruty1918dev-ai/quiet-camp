// Compile/contract doubles only. These do not verify a real SDK or Android runtime.
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
namespace QuietCamp.Domain {
 public struct Placement { }
 public class LevelData { }
 public class RuleIssue { public string Code; }
 public class RuleReport { public List<RuleIssue> Issues = new(); }
}
namespace QuietCamp.Application { public class TutorialSaveData { } }
namespace UnityEngine {
 public enum RuntimeInitializeLoadType { BeforeSceneLoad, SubsystemRegistration }
 public class RuntimeInitializeOnLoadMethodAttribute : Attribute { public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType t) { } }
 public enum RuntimePlatform { Android, IPhonePlayer }
 public enum NetworkReachability { NotReachable, ReachableViaLocalAreaNetwork }
 public static class Application { public static bool isEditor; public static RuntimePlatform platform = RuntimePlatform.Android; public static NetworkReachability internetReachability = NetworkReachability.ReachableViaLocalAreaNetwork; }
 public static class Debug { public static bool isDebugBuild; public static void Log(object t) { } public static void LogWarning(object t) { } }
 public class TextAsset { public string text; }
 public static class Resources { public static T Load<T>(string p) where T:class => null; }
 public static class JsonUtility { public static T FromJson<T>(string p) => default; }
 public class AndroidJavaObject : IDisposable {
  public static string Purposes = "1", Vendors = new string('1',755); public static int Deletes;
  public T Call<T>(string method, params object[] args) { if (method=="getString") return (T)(object)((string)args[0]=="IABTCF_PurposeConsents"?Purposes:Vendors); return default; }
  public void Call(string method, params object[] args) { if(method=="deleteUnsentReports")Deletes++; }
  public void Dispose() { }
 }
 public class AndroidJavaClass : IDisposable {
  public AndroidJavaClass(string n) { }
  public T CallStatic<T>(string m, params object[] args) => (T)(object)new AndroidJavaObject();
  public T GetStatic<T>(string n) => (T)(object)new AndroidJavaObject(); public void Dispose() { }
 }
}
namespace GoogleMobileAds.Ump.Api {
 public enum ConsentStatus { Unknown, Required, NotRequired, Obtained }
 public enum PrivacyOptionsRequirementStatus { Unknown, Required, NotRequired }
 public class ConsentRequestParameters { public bool TagForUnderAgeOfConsent; }
 public class FormError { }
 public static class ConsentInformation {
  public static ConsentStatus ConsentStatus = ConsentStatus.Obtained;
  public static PrivacyOptionsRequirementStatus PrivacyOptionsRequirementStatus = PrivacyOptionsRequirementStatus.Required;
  public static int Updates; public static bool Allowed = true;
  public static void Update(ConsentRequestParameters p, Action<FormError> cb) { Updates++; cb(null); }
  public static bool CanRequestAds() => Allowed;
 }
 public static class ConsentForm {
  public static void LoadAndShowConsentFormIfRequired(Action<FormError> cb) => cb(null);
  public static void ShowPrivacyOptionsForm(Action<FormError> cb) => cb(null);
 }
}
namespace GoogleMobileAds.Api {
 public enum AgeRestrictedTreatment { Unspecified, Child, Teen }
 public enum MaxAdContentRating { PG }
 public class RequestConfiguration { public AgeRestrictedTreatment? AgeRestrictedTreatment; public MaxAdContentRating MaxAdContentRating; }
 public class AdRequest { public Dictionary<string,string> Extras = new(); }
 public static class MobileAds {
  public static bool RaiseAdEventsOnUnityMainThread; public static int Initializations;
  public static RequestConfiguration Configuration;
  public static void SetRequestConfiguration(RequestConfiguration c) => Configuration=c;
  public static void Initialize(Action<object> cb) { Initializations++; cb(new object()); }
 }
 public class RewardedAd {
  public static int Loads; public static AdRequest Request;
  public event Action OnAdFullScreenContentClosed; public event Action<object> OnAdFullScreenContentFailed;
  public static void Load(string id, AdRequest request, Action<RewardedAd,object> cb) { Loads++; Request=request; cb(new RewardedAd(),null); }
  public bool CanShowAd() => true;
  public void Show(Action<object> reward) { reward(new object()); OnAdFullScreenContentClosed?.Invoke(); }
  public void Destroy() { }
 }
}
namespace Firebase {
 public enum DependencyStatus { Available }
 public class FirebaseApp {
  public static int Initializations; public static FirebaseApp DefaultInstance { get { Initializations++;return new FirebaseApp(); } }
  public static Task<DependencyStatus> CheckAndFixDependenciesAsync() => Task.FromResult(DependencyStatus.Available);
 }
}
namespace Firebase.Analytics {
 public enum ConsentType { AnalyticsStorage, AdStorage, AdUserData, AdPersonalization }
 public enum ConsentStatus { Granted, Denied }
 public class Parameter { public Parameter(string k,long v) { } }
 public static class FirebaseAnalytics {
  public static bool Enabled; public static int Events;
  public static void SetConsent(Dictionary<ConsentType,ConsentStatus> c) { }
  public static void SetAnalyticsCollectionEnabled(bool v) => Enabled=v;
  public static void LogEvent(string n, Parameter[] p) { Events++; }
  public static void ResetAnalyticsData() { }
 }
}
namespace Firebase.Crashlytics { public static class Crashlytics { public static bool IsCrashlyticsCollectionEnabled; } }
