#if QC_GOOGLE_REWARDED && (UNITY_ANDROID || UNITY_IOS || UNITY_EDITOR)
using System;
using System.Threading;
using System.Threading.Tasks;
using GoogleMobileAds.Api;
using GoogleMobileAds.Ump.Api;
using QuietCamp.Application;
using QuietCamp.Infrastructure;
using UnityEngine;

sealed class GoogleRewardedAdapter : IAdService, IAdPrivacyOptions, IAdRequestCancellation, IAdConsentInitialization, IDisposable
{
    readonly string _unit;
    readonly bool _testAds;
    readonly Func<bool> _authorized, _teen, _eligible;
    readonly SynchronizationContext _main;
    bool _initialized, _disposed, _busy, _showing, _consentForm;
    TaskCompletionSource<bool> _cancel;
    TaskCompletionSource<bool> _privacyClosed;
    TaskCompletionSource<AdResult> _adClosed;
    public bool CanCancelPending => _busy && !_showing && !_consentForm;
    public void CancelPending() { _cancel?.TrySetResult(true); }
    async Task<bool> AwaitPending(Task task, int milliseconds)
    { return await Task.WhenAny(task, Task.Delay(milliseconds), _cancel.Task) == task && !_disposed && _authorized() && !_cancel.Task.IsCompleted; }
    RewardedAd _ad;
    public bool IsReady => !_disposed && !_busy && _authorized() && !string.IsNullOrEmpty(_unit);
    public bool PrivacyOptionsRequired => !_disposed && _eligible() && !_testAds && ConsentInformation.PrivacyOptionsRequirementStatus == PrivacyOptionsRequirementStatus.Required;
    public GoogleRewardedAdapter(string unit, bool testAds = false, Func<bool> authorized = null, Func<bool> teen = null, Func<bool> eligible = null)
    {
        // Never skip UMP for a publisher's unit, even if a caller passes testAds.
        _testAds = testAds && (unit == GoogleServicesConfiguration.AndroidTestRewardedUnitId
            || unit == GoogleServicesConfiguration.IosTestRewardedUnitId);
        _unit = unit; _main = SynchronizationContext.Current;
        _authorized = authorized ?? (() => false); _teen = teen ?? (() => false);
        _eligible = eligible ?? _authorized;
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Register() => OptionalGoogleServices.AdsFactory = c => new GoogleRewardedAdapter(c.CurrentRewardedUnitId, c.TestAdsActive,
        () => c.AdvertisingAllowed, () => PrivacyEligibility.Band(c.PrivacySettings) == PrivacyAgeBand.From16To17, () => c.Eligible);
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
    async Task<bool> UpdateConsentInformation()
    {
        var cancellation = _cancel.Task;
        var consent = new TaskCompletionSource<bool>();
        ConsentInformation.Update(new ConsentRequestParameters { TagForUnderAgeOfConsent = false }, error => Main(() =>
            consent.TrySetResult(error == null && !_disposed && !cancellation.IsCompleted)));
        return await AwaitPending(consent.Task, 30000) && await consent.Task;
    }

    public async Task<AdResult> ShowRewarded(string placementId)
    {
        if (!IsReady || UnityEngine.Application.internetReachability == NetworkReachability.NotReachable) return AdResult.Unavailable;
        _busy = true; _cancel = new TaskCompletionSource<bool>();
        var cancellation = _cancel.Task;
        try
        {
            if (!_testAds)
            {
                // Refresh again for the explicit request; launch-cached consent
                // alone never permits requesting ads.
                if (!await UpdateConsentInformation()) return AdResult.Unavailable;
                // This API loads and displays the UMP form in one operation. Never
                // release the game gate on a timer while a native form may be open.
                _consentForm = true;
                _privacyClosed = new TaskCompletionSource<bool>();
                var form = _privacyClosed;
                ConsentForm.LoadAndShowConsentFormIfRequired(error => Main(() =>
                    form.TrySetResult(!_disposed && error == null && ConsentInformation.CanRequestAds())));
                if (!await form.Task || _disposed || cancellation.IsCompleted || !_authorized() || !DeviceStorageAllowed()) return AdResult.Unavailable;
                _consentForm = false; _privacyClosed = null;
            }
            if (!_authorized()) return AdResult.Unavailable;
            MobileAds.SetRequestConfiguration(new RequestConfiguration
            {
                AgeRestrictedTreatment = _teen() ? AgeRestrictedTreatment.Teen : AgeRestrictedTreatment.Unspecified,
                MaxAdContentRating = MaxAdContentRating.T
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
            var loaded = new TaskCompletionSource<RewardedAd>();
            bool abandoned = false;
            var request = new AdRequest(); request.Extras.Add("npa", "1");
            if (!_authorized()) return AdResult.Unavailable;
            RewardedAd.Load(_unit, request, (ad, error) => Main(() =>
            {
                if (abandoned || _disposed || !_authorized() || cancellation.IsCompleted) { ad?.Destroy(); loaded.TrySetResult(null); }
                else
                {
                    if (error != null) { Debug.LogWarning("[QuietCamp Ads] Load failed: " + error); ad?.Destroy(); }
                    loaded.TrySetResult(error == null ? ad : null);
                }
            }));
            if (!await AwaitPending(loaded.Task, 30000))
            { abandoned = true; return AdResult.Unavailable; }
            _ad = await loaded.Task;
            if (_disposed || !_authorized() || _ad == null || !_ad.CanShowAd()) return AdResult.Unavailable;
            var closed = _adClosed = new TaskCompletionSource<AdResult>();
            bool earned = false;
            _ad.OnAdFullScreenContentClosed += () => Main(() => closed.TrySetResult(earned ? AdResult.Completed : AdResult.Skipped));
            _ad.OnAdFullScreenContentFailed += error => Main(() =>
            {
                Debug.LogWarning("[QuietCamp Ads] Show failed: " + error);
                closed.TrySetResult(AdResult.Failed);
            });
            _showing = true;
            _ad.Show(reward => Main(() => { if (!_disposed) earned = true; }));
            // Do not time out while an ad is actually visible: input/audio must
            // stay gated until its close callback, including app backgrounding.
            var result = await closed.Task;
            return _authorized() && !cancellation.IsCompleted ? result : AdResult.Unavailable;
        }
        catch (Exception error) { Debug.LogWarning("[QuietCamp Ads] " + error.Message); return AdResult.Failed; }
        finally { _ad?.Destroy(); _ad = null; _adClosed = null; _privacyClosed = null; _busy = false; _showing = false; _consentForm = false; }
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
    public async Task ShowPrivacyOptions()
    {
        if (!PrivacyOptionsRequired || _busy || _disposed) return;
        _busy = true; _consentForm = true;
        var closed = _privacyClosed = new TaskCompletionSource<bool>();
        try
        {
            ConsentForm.ShowPrivacyOptionsForm(error => Main(() => closed.TrySetResult(error == null && !_disposed)));
            await closed.Task;
        }
        catch { /* An unavailable native form must leave the game usable. */ }
        finally { _privacyClosed = null; _consentForm = false; _busy = false; }
    }
    public void Dispose()
    {
        _disposed = true; _cancel?.TrySetResult(true);
        _privacyClosed?.TrySetResult(false); _adClosed?.TrySetResult(AdResult.Unavailable);
        _ad?.Destroy(); _ad = null;
    }
}
#endif
