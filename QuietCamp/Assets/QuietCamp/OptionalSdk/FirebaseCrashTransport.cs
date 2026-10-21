#if QC_FIREBASE_CRASHLYTICS && (UNITY_ANDROID || UNITY_EDITOR)
using System;
using System.Threading.Tasks;
using Firebase.Crashlytics;
using QuietCamp.Application;
using QuietCamp.Infrastructure;
using UnityEngine;

sealed class FirebaseCrashTransport : ICrashTransport
{
    bool _ready, _enabled, _disposed, _pendingFailed;
    int _generation;
    Task _pending = Task.CompletedTask;
    readonly Func<string, Task> _reports;
    public FirebaseCrashTransport(Func<string, Task> reports = null) { _reports = reports ?? PendingReports; }
    public bool Available => true;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Register() => OptionalGoogleServices.CrashFactory = () => new FirebaseCrashTransport();
    public async Task<bool> Initialize()
    {
        if (!await FirebaseRuntime.Initialize() || _disposed) return false;
        _ready = true;
        Crashlytics.IsCrashlyticsCollectionEnabled = false;
        Crashlytics.ReportUncaughtExceptionsAsFatal = false;
        Crashlytics.SetCustomKey("game_version", UnityEngine.Application.version);
        Crashlytics.SetCustomKey("game_mode", "campaign");
        return true;
    }
    public void SetConsent(bool enabled, bool sendPreviousSession = false)
    {
        _enabled = false;
        int generation = ++_generation;
        if (!_ready) return;
        // Never persist a true automatic-upload override. Cached reports are sent
        // only after this launch has validated the game's current consent receipt.
        Crashlytics.IsCrashlyticsCollectionEnabled = false;
        _pending = ApplyReports(_pending, generation, enabled, sendPreviousSession);
    }
    async Task ApplyReports(Task previous, int generation, bool enabled, bool sendPreviousSession)
    {
        try
        {
            await previous;
            if (_disposed || _pendingFailed || generation != _generation) return;
            await _reports(enabled && sendPreviousSession ? "sendUnsentReports" : "deleteUnsentReports");
            if (!_disposed && generation == _generation) _enabled = enabled;
        }
        catch { _pendingFailed = true; _enabled = false; Debug.LogWarning("[QuietCamp Firebase] Pending report operation failed; diagnostics remain disabled until restart."); }
    }
    static Task PendingReports(string method)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        using (var type = new AndroidJavaClass("com.google.firebase.crashlytics.FirebaseCrashlytics"))
        using (var instance = type.CallStatic<AndroidJavaObject>("getInstance"))
        {
            // Crashlytics Android 21.0.0 exposes void APIs here, not Google Tasks.
            // Serialize issuance only; native completion is not observable through
            // these public APIs and must not be inferred from this completed Task.
            instance.Call(method);
        }
#endif
        return Task.CompletedTask;
    }
    public void SetLevel(int level)
    { if (_ready && _enabled && !_disposed) Crashlytics.SetCustomKey("level_index", level.ToString(System.Globalization.CultureInfo.InvariantCulture)); }
    public void Report(CrashArea area)
    {
        if (!_ready || !_enabled || _disposed) return;
        Crashlytics.SetCustomKey("failure_area", area.ToString());
        // A fixed message avoids forwarding paths, save payloads or user input.
        Crashlytics.LogException(new InvalidOperationException("QuietCamp diagnostic: " + area));
    }
    public void Dispose() { _enabled = false; _disposed = true; _generation++; }
}
#endif
