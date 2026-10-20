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
#if QC_FIREBASE_CRASHLYTICS
            Firebase.Crashlytics.Crashlytics.IsCrashlyticsCollectionEnabled = false;
            Firebase.Crashlytics.Crashlytics.ReportUncaughtExceptionsAsFatal = false;
#endif
            Ready = true;
            return true;
        }
        catch (Exception e)
        { Debug.LogWarning("[QuietCamp Firebase] Initialization unavailable: " + e.GetType().Name); return false; }
    }
}
#endif
