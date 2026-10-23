#if QC_GOOGLE_REWARDED && (UNITY_ANDROID || UNITY_IOS || UNITY_EDITOR)
using System;
using System.Threading;
using System.Threading.Tasks;
using GoogleMobileAds.Api;
using GoogleMobileAds.Ump.Api;
using QuietCamp.Application;
using QuietCamp.Infrastructure;
using UnityEngine;

sealed class GoogleRewardedAdapter : IAdService, IInterstitialAdService, IAdPrivacyOptions, IAdRequestCancellation, IAdConsentInitialization, IDisposable
{
    readonly string _unit;
    readonly Func<string, string> _selectUnit;
    readonly Action<string> _earned;
    readonly Action<string, int> _observed;
    readonly Action<bool, string> _consentUpdated;
    readonly Action _invalidateConsent;
    readonly Func<long> _consentEpoch;
    long _requestEpoch;
    bool ConsentContextCurrent => _consentEpoch == null || _consentEpoch() == _requestEpoch;
    InterstitialAd _interstitial;
    readonly bool _testAds;
    readonly Func<bool> _authorized, _teen, _eligible;
    readonly Func<bool> _underAge, _limitedApproved;
    readonly Func<bool> _personalizedEligible;
    readonly Func<bool, bool> _applyStorageMode;
    readonly Action<bool> _rememberPrivacyOptions;
    readonly SynchronizationContext _main;
    bool _initialized, _disposed, _busy, _showing, _consentForm;
    TaskCompletionSource<bool> _cancel;
    TaskCompletionSource<bool> _privacyClosed;
    TaskCompletionSource<AdResult> _adClosed;
    public bool CanCancelPending => _busy && !_showing && !_consentForm;
    public void CancelPending() { _cancel?.TrySetResult(true); }
    async Task<bool> AwaitPending(Task task, int milliseconds, bool privacyManagement = false)
    { return await Task.WhenAny(task, Task.Delay(milliseconds), _cancel.Task) == task && !_disposed
        && (privacyManagement ? _eligible() : _authorized()) && !_cancel.Task.IsCompleted; }
    RewardedAd _ad;
    public bool IsReady => !_disposed && !_busy && _authorized() && !string.IsNullOrEmpty(_unit);
    public bool PrivacyOptionsRequired => !_disposed && !_underAge() && _eligible() && !_testAds && ConsentInformation.PrivacyOptionsRequirementStatus == PrivacyOptionsRequirementStatus.Required;
    public GoogleRewardedAdapter(string unit, bool testAds = false, Func<bool> authorized = null, Func<bool> teen = null, Func<bool> eligible = null, Action<bool> rememberPrivacyOptions = null,
        Func<bool> underAge = null, Func<bool> limitedApproved = null, Func<bool, bool> applyStorageMode = null, Func<bool> personalizedEligible = null, Func<string, string> selectUnit = null, Action<string> earned = null, Action<string, int> observed = null, Action<bool, string> consentUpdated = null, Action invalidateConsent = null, Func<long> consentEpoch = null)
    {
        // Never skip UMP for a publisher's unit, even if a caller passes testAds.
        _testAds = testAds && (unit == GoogleServicesConfiguration.AndroidTestRewardedUnitId
            || unit == GoogleServicesConfiguration.IosTestRewardedUnitId || unit == GoogleServicesConfiguration.AndroidTestInterstitialUnitId);
        _consentUpdated = consentUpdated; _invalidateConsent = invalidateConsent;
        _selectUnit = selectUnit ?? (_ => unit); _earned = earned; _observed = observed;
        _unit = unit; _main = SynchronizationContext.Current;
        _authorized = authorized ?? (() => false); _teen = teen ?? (() => false);
        _eligible = eligible ?? _authorized;
        _rememberPrivacyOptions = rememberPrivacyOptions;
        _underAge = underAge ?? (() => false); _limitedApproved = limitedApproved ?? (() => false);
        _applyStorageMode = applyStorageMode ?? ApplyStorageMode;
        _personalizedEligible = personalizedEligible ?? (() => false); _consentEpoch = consentEpoch;
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Register() => OptionalGoogleServices.AdsFactory = c => new GoogleRewardedAdapter(!string.IsNullOrEmpty(c.CurrentRewardedUnitId) ? c.CurrentRewardedUnitId : !string.IsNullOrEmpty(c.UnitFor("camp.hint")) ? c.UnitFor("camp.hint") : c.UnitFor("camp.interstitial"), c.TestAdsActive,
        () => c.AdRequestsAllowed, () => PrivacyEligibility.Band(c.PrivacySettings) == PrivacyAgeBand.From16To17, () => c.Eligible, c.RememberPrivacyOptions,
        () => PrivacyEligibility.Band(c.PrivacySettings) == PrivacyAgeBand.From13To15 || PrivacyEligibility.Band(c.PrivacySettings) == PrivacyAgeBand.Under13, () => c.LimitedAdsApproved, personalizedEligible: () => c.AdultPersonalizationApproved, selectUnit: c.UnitFor,
        earned: placement => c.RewardEarned?.Invoke(placement), observed: (placement, stage) => c.AdObserved?.Invoke(placement, stage), consentUpdated: c.Permissions.AcceptUmp, invalidateConsent: c.Permissions.InvalidateAdvertising, consentEpoch: () => c.Permissions.AdvertisingEpoch);
    void Main(Action action) { if (_main != null) _main.Post(_ => action(), null); else action(); }
    public void ShowBanner(string placementId) { }
    public void HideBanner() { }

    public async Task RefreshConsent()
    {
        // Publisher inventory only. Refresh information each launch without
        // initializing ads or opening a native form over unrelated core play.
        if (_testAds || !IsReady || UnityEngine.Application.internetReachability == NetworkReachability.NotReachable) return;
        _busy = true; _cancel = new TaskCompletionSource<bool>();
        try { await UpdateConsentInformation(); }
        catch (Exception error) { Debug.LogWarning("[QuietCamp Ads] Consent refresh unavailable: " + error.GetType().Name); }
        finally { _busy = false; }
    }
    async Task<bool> UpdateConsentInformation(bool privacyManagement = false)
    {
        _invalidateConsent?.Invoke();
        _requestEpoch = _consentEpoch?.Invoke() ?? 0;
        var requestCancellation = _cancel;
        var cancellation = _cancel.Task;
        var consent = new TaskCompletionSource<bool>();
        ConsentInformation.Update(new ConsentRequestParameters { TagForUnderAgeOfConsent = _underAge() }, error => Main(() =>
        {
            bool valid = error == null && !_disposed && !cancellation.IsCompleted && ConsentContextCurrent && (privacyManagement ? _eligible() : _authorized());
            if (valid) _rememberPrivacyOptions?.Invoke(ConsentInformation.PrivacyOptionsRequirementStatus == PrivacyOptionsRequirementStatus.Required);
            if (valid) PublishConsent(); else _invalidateConsent?.Invoke();
            consent.TrySetResult(valid);
        }));
        bool finished = await AwaitPending(consent.Task, 30000, privacyManagement);
        if (!finished) { requestCancellation.TrySetResult(true); _invalidateConsent?.Invoke(); }
        return finished && await consent.Task;
    }

    public Task<AdResult> ShowRewarded(string placementId) => ShowFullscreen(placementId, false);
    public Task<AdResult> ShowInterstitial(string placementId) => ShowFullscreen(placementId, true);
    void Observe(string placement, int stage) { try { _observed?.Invoke(placement, stage); } catch { } }
    async Task<AdResult> ShowFullscreen(string placementId, bool interstitial)
    {
        string unit = _selectUnit(placementId);
        if (string.IsNullOrEmpty(unit)) return AdResult.Unavailable;
        Observe(placementId, 0);
        if (!IsReady || UnityEngine.Application.internetReachability == NetworkReachability.NotReachable) return AdResult.Unavailable;
        _busy = true; _cancel = new TaskCompletionSource<bool>();
        var cancellation = _cancel.Task;
        try
        {
            bool underAge = _underAge();
            bool limited = underAge;
            if (underAge && (_testAds || !_limitedApproved())) return AdResult.Unavailable;
            if (!_testAds)
            {
                // Refresh again for the explicit request; launch-cached consent
                // alone never permits requesting ads.
                if (!await UpdateConsentInformation()) return AdResult.Unavailable;
                // This API loads and displays the UMP form in one operation. Never
                // release the game gate on a timer while a native form may be open.
                if (!underAge)
                {
                    _consentForm = true;
                    _privacyClosed = new TaskCompletionSource<bool>();
                    var form = _privacyClosed;
                    ConsentForm.LoadAndShowConsentFormIfRequired(error => Main(() =>
                    {
                        bool valid = !_disposed && error == null && !cancellation.IsCompleted && ConsentContextCurrent && _authorized();
                        if (valid) PublishConsent(); else _invalidateConsent?.Invoke();
                        form.TrySetResult(valid && ConsentInformation.CanRequestAds());
                    }));
                    if (!await form.Task) return AdResult.Unavailable;
                    _consentForm = false; _privacyClosed = null;
                    bool storageAllowed = DeviceStorageAllowed();
                    if (!storageAllowed) limited = _limitedApproved();
                    if (!storageAllowed && !limited) return AdResult.Unavailable;
                }
                if (!ConsentInformation.CanRequestAds() || _disposed || cancellation.IsCompleted || !_authorized()) return AdResult.Unavailable;
            }
            if (!_authorized()) return AdResult.Unavailable;
            if (!_applyStorageMode(limited)) return AdResult.Unavailable;
            MobileAds.SetRequestConfiguration(new RequestConfiguration
            {
                AgeRestrictedTreatment = underAge ? AgeRestrictedTreatment.Child : _teen() ? AgeRestrictedTreatment.Teen : AgeRestrictedTreatment.Unspecified,
                MaxAdContentRating = underAge ? MaxAdContentRating.G : MaxAdContentRating.T
            });
            // Google sample app IDs have no publisher UMP message configured.
            // This path is restricted to sample units in Editor/development players.
            if (!_initialized)
            {
                var init = new TaskCompletionSource<bool>();
                MobileAds.RaiseAdEventsOnUnityMainThread = true;
                MobileAds.Initialize(status => Main(() => init.TrySetResult(!_disposed && status != null)));
                if (!await AwaitPending(init.Task, 20000) || !await init.Task) return AdResult.Unavailable;
                _initialized = true;
            }
            var request = new AdRequest();
            // For approved adults the Google SDK must still honor the fresh UMP
            // choice; omission of npa is not consent. Never personalize minors/LTD.
            if (limited || underAge || _teen() || !_personalizedEligible()) request.Extras.Add("npa", "1");
            if (!_authorized()) return AdResult.Unavailable;
            if (interstitial) return await ShowInterstitialCore(unit, request, placementId);
            var loaded = new TaskCompletionSource<RewardedAd>();
            bool abandoned = false;
            RewardedAd.Load(unit, request, (ad, error) => Main(() =>
            {
                if (abandoned || _disposed || !_authorized() || cancellation.IsCompleted) { ad?.Destroy(); loaded.TrySetResult(null); }
                else
                {
                    if (error != null) { Debug.LogWarning("[QuietCamp Ads] Load failed."); Observe(placementId, 4); ad?.Destroy(); }
                    loaded.TrySetResult(error == null ? ad : null);
                }
            }));
            if (!await AwaitPending(loaded.Task, 30000))
            { abandoned = true; return AdResult.Unavailable; }
            _ad = await loaded.Task;
            if (_disposed || !_authorized() || _ad == null || !_ad.CanShowAd()) return AdResult.Unavailable;
            Observe(placementId, 1);
            var closed = _adClosed = new TaskCompletionSource<AdResult>();
            bool earned = false;
            _ad.OnAdFullScreenContentClosed += () => Main(() => closed.TrySetResult(earned ? AdResult.Completed : AdResult.Skipped));
            _ad.OnAdFullScreenContentFailed += error => Main(() =>
            {
                Debug.LogWarning("[QuietCamp Ads] Show failed."); Observe(placementId, 4);
                closed.TrySetResult(AdResult.Failed);
            });
            _showing = true;
            Observe(placementId, 2);
            _ad.Show(reward => Main(() => {
                if (_disposed || earned) return;
                earned = true;
                try { _earned?.Invoke(placementId); } catch { }
            }));
            // Do not time out while an ad is actually visible: input/audio must
            // stay gated until its close callback, including app backgrounding.
            var result = await closed.Task;
            if (result == AdResult.Completed || result == AdResult.Skipped) Observe(placementId, 3);
            return _authorized() && !cancellation.IsCompleted ? result : AdResult.Unavailable;
        }
        catch (Exception error) { Debug.LogWarning("[QuietCamp Ads] Request failed: " + error.GetType().Name); Observe(placementId, 4); return AdResult.Failed; }
        finally { _interstitial?.Destroy(); _interstitial = null; _ad?.Destroy(); _ad = null; _adClosed = null; _privacyClosed = null; _busy = false; _showing = false; _consentForm = false; }
    }
    async Task<AdResult> ShowInterstitialCore(string unit, AdRequest request, string placement)
    {
        var loaded = new TaskCompletionSource<InterstitialAd>();
        bool abandoned = false;
        InterstitialAd.Load(unit, request, (ad, error) => Main(() => {
            if (abandoned || _disposed || !_authorized() || _cancel.Task.IsCompleted || error != null)
            { if (error != null) Observe(placement, 4); ad?.Destroy(); loaded.TrySetResult(null); }
            else loaded.TrySetResult(ad);
        }));
        if (!await AwaitPending(loaded.Task, 30000)) { abandoned = true; return AdResult.Unavailable; }
        _interstitial = await loaded.Task;
        if (_interstitial == null || !_interstitial.CanShowAd()) return AdResult.Unavailable;
        Observe(placement, 1);
        var closed = _adClosed = new TaskCompletionSource<AdResult>();
        _interstitial.OnAdFullScreenContentClosed += () => Main(() => closed.TrySetResult(AdResult.Completed));
        _interstitial.OnAdFullScreenContentFailed += error => Main(() => { Observe(placement, 4); closed.TrySetResult(AdResult.Failed); });
        _showing = true; Observe(placement, 2); _interstitial.Show();
        var result = await closed.Task; if (result == AdResult.Completed) Observe(placement, 3);
        return _authorized() && !_cancel.Task.IsCompleted ? result : AdResult.Unavailable;
    }
    void PublishConsent()
    {
        if (!ConsentContextCurrent) return;
        string values = null;
#if UNITY_ANDROID && !UNITY_EDITOR
        try {
            using (var unity = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = unity.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var manager = new AndroidJavaClass("android.preference.PreferenceManager"))
            using (var preferences = manager.CallStatic<AndroidJavaObject>("getDefaultSharedPreferences", activity))
                values = preferences.Call<string>("getString", "UMP_consentModeValues", "");
        } catch { } // Missing/unsupported signal stays denied; never infer from CanRequestAds.
#endif
        _consentUpdated?.Invoke(ConsentInformation.CanRequestAds(), values);
    }
    static bool DeviceStorageAllowed()
    {
        // A fresh UMP result remains mandatory. These IAB signals are an
        // additional fail-closed check, never a replacement for SDK expiry checks.
        if (ConsentInformation.ConsentStatus == ConsentStatus.NotRequired) return true;
#if UNITY_ANDROID && !UNITY_EDITOR
        using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
        using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
        using (var preferences = new AndroidJavaClass("android.preference.PreferenceManager"))
        using (var store = preferences.CallStatic<AndroidJavaObject>("getDefaultSharedPreferences", activity))
        {
            string purposes = store.Call<string>("getString", "IABTCF_PurposeConsents", "");
            string vendors = store.Call<string>("getString", "IABTCF_VendorConsents", "");
            return store.Call<int>("getInt", "IABTCF_gdprApplies", -1) == 1
                && purposes.Length > 0 && purposes[0] == '1' && vendors.Length >= 755 && vendors[754] == '1';
        }
#else
        return false;
#endif
    }
    static bool ApplyStorageMode(bool limited)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        // Google's documented LTD control. Never manufacture a consent=true signal.
        try
        {
            using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var preferences = new AndroidJavaClass("android.preference.PreferenceManager"))
            using (var store = preferences.CallStatic<AndroidJavaObject>("getDefaultSharedPreferences", activity))
            using (var edit = store.Call<AndroidJavaObject>("edit"))
            {
                const string key = "gad_has_consent_for_cookies", owner = "qc_limited_ads_override";
                bool owned = store.Call<bool>("getBoolean", owner, false);
                if (limited && store.Call<int>("getInt", key, -1) != 0)
                {
                    using (edit.Call<AndroidJavaObject>("putInt", key, 0)) { }
                    using (edit.Call<AndroidJavaObject>("putBoolean", owner, true)) { }
                }
                else if (!limited && owned)
                {
                    using (edit.Call<AndroidJavaObject>("remove", key)) { }
                    using (edit.Call<AndroidJavaObject>("remove", owner)) { }
                }
                return edit.Call<bool>("commit");
            }
        }
        catch { return false; }
#else
        return !limited; // Native storage must be verified; contract tests inject a recording bridge.
#endif
    }
    public async Task ShowPrivacyOptions()
    {
        if (!_eligible() || _underAge() || _testAds || _busy || _disposed) return;
        _busy = true; _cancel = new TaskCompletionSource<bool>();
        var closed = _privacyClosed = new TaskCompletionSource<bool>();
        try
        {
            // Explicit management action after a prior required form is permitted
            // even when local ad opt-in was withdrawn; it never initializes ads.
            if (!await UpdateConsentInformation(true) || !PrivacyOptionsRequired) return;
            _consentForm = true;
            ConsentForm.ShowPrivacyOptionsForm(error => Main(() => {
                if (error == null && !_disposed && !_cancel.Task.IsCompleted) PublishConsent(); else _invalidateConsent?.Invoke();
                closed.TrySetResult(error == null && !_disposed);
            }));
            await closed.Task;
        }
        catch { /* An unavailable native form must leave the game usable. */ }
        finally { _privacyClosed = null; _consentForm = false; _busy = false; }
    }
    public void Dispose()
    {
        _disposed = true; _invalidateConsent?.Invoke(); _cancel?.TrySetResult(true);
        _privacyClosed?.TrySetResult(false); _adClosed?.TrySetResult(AdResult.Unavailable);
        _interstitial?.Destroy(); _interstitial = null;
        _ad?.Destroy(); _ad = null;
    }
}
#endif
