#if QC_FIREBASE_ANALYTICS
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Firebase;
using Firebase.Analytics;
using QuietCamp.Application;
using QuietCamp.Infrastructure;
using UnityEngine;

// Predefined Assembly-CSharp intentionally sees the optional SDK assemblies.
// Core asmdefs do not acquire missing references when SDKs are removed.
sealed class FirebaseComfortTransport : IComfortTransport
{
    bool _ready, _enabled, _disposed;
    Task<bool> _initialization;
    public bool Available => true;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Register() => OptionalGoogleServices.AnalyticsFactory = () => new FirebaseComfortTransport();
    public Task<bool> Initialize() => _initialization ?? (_initialization = InitializeCore());
    async Task<bool> InitializeCore()
    {
        var dependencies = await FirebaseApp.CheckAndFixDependenciesAsync();
        if (_disposed || dependencies != DependencyStatus.Available) return false;
        _ = FirebaseApp.DefaultInstance;
        _ready = true;
        SetConsent(false);
        return true;
    }
    public void SetConsent(bool enabled)
    {
        _enabled = enabled && !_disposed && _ready;
        if (!_ready) return;
        FirebaseAnalytics.SetConsent(new Dictionary<ConsentType, ConsentStatus>
        {
            [ConsentType.AnalyticsStorage] = _enabled ? ConsentStatus.Granted : ConsentStatus.Denied,
            [ConsentType.AdStorage] = ConsentStatus.Denied,
            [ConsentType.AdUserData] = ConsentStatus.Denied,
            [ConsentType.AdPersonalization] = ConsentStatus.Denied
        });
        FirebaseAnalytics.SetAnalyticsCollectionEnabled(_enabled);
    }
    public void Send(ComfortRecord record)
    {
        if (!_enabled || !_ready || _disposed) return;
        var parameters = new Parameter[record.Values.Count];
        int i = 0;
        foreach (var value in record.Values) parameters[i++] = new Parameter(value.Key, value.Value);
        FirebaseAnalytics.LogEvent(record.Name, parameters);
    }
    public void ResetLocalData()
    { if (_ready) { SetConsent(false); FirebaseAnalytics.ResetAnalyticsData(); } }
    public void Dispose() { SetConsent(false); _disposed = true; }
}
#endif
