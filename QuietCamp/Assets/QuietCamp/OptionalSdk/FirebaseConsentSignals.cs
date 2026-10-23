#if QC_FIREBASE_ANALYTICS && (UNITY_ANDROID || UNITY_EDITOR)
using System.Collections.Generic;
using Firebase.Analytics;
using QuietCamp.Application;

// Sole Firebase consent writer. Local Analytics opt-in never grants ad purposes.
static class FirebaseConsentSignals
{
    static PrivacyPermissionService _permissions;
    static bool _analyticsRuntimeEnabled;
    public static bool AnalyticsEnabled => _analyticsRuntimeEnabled && (_permissions?.Decisions.AnalyticsAllowed ?? false);
    public static void Bind(PrivacyPermissionService permissions)
    {
        if (_permissions != null) _permissions.Changed -= Apply;
        _permissions = permissions;
        if (_permissions != null) _permissions.Changed += Apply;
        Apply();
    }
    public static void ResetDenied()
    {
        _analyticsRuntimeEnabled = false;
        FirebaseAnalytics.SetAnalyticsCollectionEnabled(false);
        FirebaseAnalytics.SetConsent(new Dictionary<ConsentType, ConsentStatus> {
            [ConsentType.AnalyticsStorage] = ConsentStatus.Denied, [ConsentType.AdStorage] = ConsentStatus.Denied,
            [ConsentType.AdUserData] = ConsentStatus.Denied, [ConsentType.AdPersonalization] = ConsentStatus.Denied });
    }
    public static void SetAnalytics(bool enabled) { _analyticsRuntimeEnabled = enabled; Apply(); }
    static ConsentStatus Status(bool allowed) => allowed ? ConsentStatus.Granted : ConsentStatus.Denied;
    public static void Reapply() => Apply();
    static void Apply()
    {
        if (!FirebaseRuntime.Ready) return; // Binding a decision must not initialize Firebase.
        var decision = _permissions?.Decisions ?? default;
        FirebaseAnalytics.SetConsent(new Dictionary<ConsentType, ConsentStatus> {
            [ConsentType.AnalyticsStorage] = Status(AnalyticsEnabled),
            [ConsentType.AdStorage] = Status(decision.AdStorage),
            [ConsentType.AdUserData] = Status(decision.AdUserData),
            [ConsentType.AdPersonalization] = Status(decision.AdPersonalization) });
        FirebaseAnalytics.SetAnalyticsCollectionEnabled(AnalyticsEnabled);
    }
}
#endif
