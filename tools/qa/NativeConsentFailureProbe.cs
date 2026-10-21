// Explicit QA command only. Never copy into the production project.
// Exercises actual native adapters using isolated settings, without changing publication gates.
#if UNITY_ANDROID && !UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using QuietCamp.Application;
using QuietCamp.Infrastructure;
using QuietCamp.Presentation;
using UnityEngine;

public sealed class NativeConsentFailureProbe : MonoBehaviour
{
    bool _running;
    string Control => Path.Combine(UnityEngine.Application.persistentDataPath, "qa-native-consent");
    [Serializable] sealed class Result
    {
        public string purpose, error;
        public bool writeThrows, passed, nativeBefore, nativeAfter;
        public bool analyticsCollecting, crashesCollecting, allPermissionsOff;
        public int failedWrites;
    }
    [Serializable] sealed class Report
    {
        public string scope = "Explicit Android QA; synthetic isolated settings and simulated write failures; actual Firebase adapters and persisted native measurement flag. No release clearance.";
        public List<Result> results = new List<Result>();
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Create()
    {
        if (!Debug.isDebugBuild || UnityEngine.Application.productName != "Quiet Camp QA") return;
        var go = new GameObject("NativeConsentFailureProbe"); DontDestroyOnLoad(go);
        go.AddComponent<NativeConsentFailureProbe>();
    }
    void Update()
    {
        if (_running || !File.Exists(Path.Combine(Control, "run"))) return;
        File.Delete(Path.Combine(Control, "run"));
        if (!PrivacyEligibility.Allowed(QuietCampBootstrap.ServicesRef?.Settings))
        { Directory.CreateDirectory(Control); File.WriteAllText(Path.Combine(Control, "refused.txt"), "Choose an eligible age in the real QA UI before this explicit test."); return; }
        _running = true; _ = Run();
    }
    static bool NativeCollection()
    {
        using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
        using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
        using (var preferences = activity.Call<AndroidJavaObject>("getSharedPreferences", "com.google.android.gms.measurement.prefs", 0))
            return preferences.Call<bool>("getBoolean", "measurement_enabled", false);
    }
    static async Task<bool> WaitNative(bool value)
    {
        for (int i = 0; i < 50; i++)
        { if (NativeCollection() == value) return true; await Task.Delay(100); }
        return false;
    }
    async Task Run()
    {
        var report = new Report();
        try
        {
            foreach (string purpose in new[] { "advertising", "crash" })
            foreach (bool writeThrows in new[] { false, true })
            {
                var result = new Result { purpose = purpose, writeThrows = writeThrows };
                report.results.Add(result);
                var settings = new SettingsSaveData { privacyAgeBand = 4, privacyAgeVersion = PrivacyEligibility.AgeVersion };
                bool writable = true;
                var persistence = new PrivacyConsentPersistence(settings, () =>
                { if (writable) return true; result.failedWrites++; if (writeThrows) throw new IOException("Synthetic QA consent-write failure"); return false; });
                var analytics = new ComfortAnalytics(settings, OptionalGoogleServices.AnalyticsFactory(),
                    () => { if (!persistence.Save()) throw new IOException("Synthetic QA consent-write failure"); }, "explicit-native-qa");
                var crashes = new CrashDiagnostics(settings, OptionalGoogleServices.CrashFactory(), persistence.Save, "explicit-native-qa");
                persistence.Attach(analytics, crashes, null);
                try
                {
                    await analytics.SetConsent(true);
                    if (!analytics.Collecting || !await WaitNative(true)) throw new InvalidOperationException("Actual native Analytics did not enable");
                    result.nativeBefore = NativeCollection();
                    writable = false;
                    if (purpose == "advertising") { settings.advertisingConsent = true; persistence.Save(); }
                    else await crashes.SetConsent(true);
                    if (!await WaitNative(false)) throw new InvalidOperationException("Actual native Analytics did not disable");
                    result.nativeAfter = NativeCollection();
                    result.analyticsCollecting = analytics.Collecting;
                    result.crashesCollecting = crashes.Collecting;
                    result.allPermissionsOff = !settings.analyticsConsent && !settings.crashReportingConsent && !settings.advertisingConsent && settings.privacyAgeBand == 0;
                    result.passed = result.nativeBefore && !result.nativeAfter && !result.analyticsCollecting && !result.crashesCollecting && result.allPermissionsOff && result.failedWrites == 1;
                }
                catch (Exception exception) { result.error = exception.GetType().Name + ": " + exception.Message; }
                finally { analytics.Suspend(); crashes.Suspend(); analytics.Dispose(); crashes.Dispose(); }
                File.WriteAllText(Path.Combine(Control, "results.json"), JsonUtility.ToJson(report, true));
            }
        }
        catch (Exception exception)
        { report.results.Add(new Result { error = exception.GetType().Name + ": " + exception.Message }); }
        finally
        {
            File.WriteAllText(Path.Combine(Control, "results.json"), JsonUtility.ToJson(report, true));
            File.WriteAllText(Path.Combine(Control, "finished"), "complete"); _running = false;
        }
    }
}
#endif
