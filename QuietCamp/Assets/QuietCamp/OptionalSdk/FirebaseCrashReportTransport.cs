#if QC_FIREBASE_CRASHLYTICS && UNITY_ANDROID
using System.Threading.Tasks;
using Firebase;
using Firebase.Crashlytics;
using QuietCamp.Application;
using QuietCamp.Infrastructure;
using UnityEngine;

// Never registered as an active provider unless the separate native/runtime
// verification flag AND publication gates pass. No user IDs, custom logs or keys.
sealed class FirebaseCrashReportTransport : ICrashReportTransport
{
    bool _ready, _disposed;
    public bool Available => true;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Register() => OptionalGoogleServices.CrashFactory = () => new FirebaseCrashReportTransport();
    public async Task<bool> Initialize()
    {
        if (_disposed) return false;
        if (_ready) return true;
        if (await FirebaseApp.CheckAndFixDependenciesAsync() != DependencyStatus.Available || _disposed) return false;
        _ = FirebaseApp.DefaultInstance;
        Crashlytics.IsCrashlyticsCollectionEnabled = false;
        _ready = true;
        DeletePendingReports();
        // Android deletion is queued by the SDK. Its ordering and old-release
        // cache migration must be verified on the installed SDK before enabling
        // crashlyticsRuntimeVerified; this source alone is not that evidence.
        return !_disposed;
    }
    public void SetCollection(bool enabled)
    { if (_ready) Crashlytics.IsCrashlyticsCollectionEnabled = enabled && !_disposed; }
    public void DeletePendingReports()
    {
        if (!_ready) return;
        using (var type = new AndroidJavaClass("com.google.firebase.crashlytics.FirebaseCrashlytics"))
        using (var instance = type.CallStatic<AndroidJavaObject>("getInstance"))
            instance.Call("deleteUnsentReports");
    }
    public void Dispose() { SetCollection(false); _disposed = true; }
}
#endif
