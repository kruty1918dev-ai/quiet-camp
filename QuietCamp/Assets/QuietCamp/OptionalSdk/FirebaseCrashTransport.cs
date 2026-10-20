#if QC_FIREBASE_CRASHLYTICS && (UNITY_ANDROID || UNITY_EDITOR)
using System;
using System.Threading.Tasks;
using Firebase.Crashlytics;
using QuietCamp.Application;
using QuietCamp.Infrastructure;
using UnityEngine;

sealed class FirebaseCrashTransport : ICrashTransport
{
    bool _ready, _enabled, _disposed;
    public bool Available => true;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Register() => OptionalGoogleServices.CrashFactory = () => new FirebaseCrashTransport();
    public async Task<bool> Initialize()
    {
        if (!await FirebaseRuntime.Initialize() || _disposed) return false;
        _ready = true;
        Crashlytics.IsCrashlyticsCollectionEnabled = false;
        Crashlytics.SetCustomKey("game_version", UnityEngine.Application.version);
        Crashlytics.SetCustomKey("game_mode", "campaign");
        return true;
    }
    public void SetConsent(bool enabled, bool sendPreviousSession = false)
    {
        _enabled = enabled && _ready && !_disposed;
        if (!_ready) return;
        // Never persist a true automatic-upload override. Cached reports are sent
        // only after this launch has validated the game's current consent receipt.
        Crashlytics.IsCrashlyticsCollectionEnabled = false;
        PendingReports(_enabled && sendPreviousSession ? "sendUnsentReports" : "deleteUnsentReports");
    }
    static void PendingReports(string method)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        using (var type = new AndroidJavaClass("com.google.firebase.crashlytics.FirebaseCrashlytics"))
        using (var instance = type.CallStatic<AndroidJavaObject>("getInstance"))
        using (var task = instance.Call<AndroidJavaObject>(method)) { }
#endif
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
    public void Dispose() { _enabled = false; _disposed = true; }
}
#endif
