#if QC_FIREBASE_ANALYTICS && (UNITY_ANDROID || UNITY_EDITOR)
using System.Collections.Generic;
using Firebase.Analytics;

// Sole Firebase consent writer. Local Analytics opt-in never grants ad purposes.
static class FirebaseConsentSignals
{
    static bool _analytics, _adStorage, _adUserData, _personalization;
    public static void ResetDenied()
    { _analytics = _adStorage = _adUserData = _personalization = false; Apply(); }
    public static void SetAnalytics(bool enabled) { _analytics = enabled; Apply(); }
    // Call only with independently verified advertising decisions. The current
    // AdMob integration lets UMP own ad consent and does not export it to Firebase.
    public static void SetAdvertising(bool storage, bool userData, bool personalization)
    { _adStorage = storage; _adUserData = userData; _personalization = personalization && storage && userData; Apply(); }
    static ConsentStatus Status(bool allowed) => allowed ? ConsentStatus.Granted : ConsentStatus.Denied;
    public static void Reapply() => Apply();
    static void Apply() => FirebaseAnalytics.SetConsent(new Dictionary<ConsentType, ConsentStatus>
    {
        [ConsentType.AnalyticsStorage] = Status(_analytics),
        [ConsentType.AdStorage] = Status(_adStorage),
        [ConsentType.AdUserData] = Status(_adUserData),
        [ConsentType.AdPersonalization] = Status(_personalization)
    });
}
#endif
