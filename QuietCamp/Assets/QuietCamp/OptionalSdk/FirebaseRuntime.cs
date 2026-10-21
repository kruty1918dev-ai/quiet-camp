#if (QC_FIREBASE_ANALYTICS || QC_FIREBASE_CRASHLYTICS) && (UNITY_ANDROID || UNITY_EDITOR)
using System;
using System.Threading.Tasks;
using Firebase;
using QuietCamp.Infrastructure;
using UnityEngine;

// One dependency check shared by both optional adapters, owned by the app lifetime.
static class FirebaseRuntime
{
    static Task<bool> _initialization;
    public static bool Ready { get; private set; }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() { _initialization = null; Ready = false; }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Register() => OptionalGoogleServices.FirebaseInitialization = Initialize;
    public static Task<bool> Initialize() => _initialization ?? (_initialization = InitializeCore());
    static async Task<bool> InitializeCore()
    {
        try
        {
            var check = FirebaseApp.CheckAndFixDependenciesAsync();
            if (await Task.WhenAny(check, Task.Delay(20000)) != check)
            { Debug.LogWarning("[QuietCamp Firebase] Dependency check timed out; gameplay continues."); return false; }
            if (await check != DependencyStatus.Available) return false;
            _ = FirebaseApp.DefaultInstance;
            // Shared Core can create native components for both installed products.
            // Deny their collection before either purpose-specific adapter enables its own work.
            // This does not prove the absence of native files/traffic during DefaultInstance creation.
#if QC_FIREBASE_ANALYTICS
            Firebase.Analytics.FirebaseAnalytics.SetAnalyticsCollectionEnabled(false);
            Firebase.Analytics.FirebaseAnalytics.SetConsent(new System.Collections.Generic.Dictionary<Firebase.Analytics.ConsentType, Firebase.Analytics.ConsentStatus>
            {
                [Firebase.Analytics.ConsentType.AnalyticsStorage] = Firebase.Analytics.ConsentStatus.Denied,
                [Firebase.Analytics.ConsentType.AdStorage] = Firebase.Analytics.ConsentStatus.Denied,
                [Firebase.Analytics.ConsentType.AdUserData] = Firebase.Analytics.ConsentStatus.Denied,
                [Firebase.Analytics.ConsentType.AdPersonalization] = Firebase.Analytics.ConsentStatus.Denied
            });
#endif
#if QC_FIREBASE_CRASHLYTICS && UNITY_ANDROID && !UNITY_EDITOR
            // Use the native collection API without installing Unity Crashlytics handlers for Analytics-only.
            using (var type = new AndroidJavaClass("com.google.firebase.crashlytics.FirebaseCrashlytics"))
            using (var crash = type.CallStatic<AndroidJavaObject>("getInstance"))
                crash.Call("setCrashlyticsCollectionEnabled", false);
#endif
            Ready = true;
            return true;
        }
        catch (Exception e)
        { Debug.LogWarning("[QuietCamp Firebase] Initialization unavailable: " + e.GetType().Name); return false; }
    }
}
#endif
