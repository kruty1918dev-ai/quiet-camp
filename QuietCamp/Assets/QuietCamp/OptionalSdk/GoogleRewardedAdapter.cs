#if QC_GOOGLE_REWARDED && (UNITY_ANDROID || UNITY_IOS || UNITY_EDITOR)
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using GoogleMobileAds.Api;
using GoogleMobileAds.Ump.Api;
using QuietCamp.Application;
using QuietCamp.Infrastructure;
using UnityEngine;

sealed class GoogleRewardedAdapter : IAdService, IAdPrivacyOptions, IAdRequestCancellation, IAdLaunchPrivacy, IDisposable
{
    readonly string _unit;
    readonly bool _testAds;
    readonly SettingsSaveData _privacySettings;
    readonly string _revision;
    Task<bool> _consentUpdate;
    readonly SynchronizationContext _main;
    bool _initialized, _disposed, _busy, _showing, _consentForm;
    TaskCompletionSource<bool> _cancel;
    TaskCompletionSource<bool> _privacyClosed;
    TaskCompletionSource<AdResult> _adClosed;
    public bool CanCancelPending => _busy && !_showing && !_consentForm;
    public void CancelPending() { if (CanCancelPending) _cancel?.TrySetResult(true); }
    async Task<bool> AwaitPending(Task task, int milliseconds)
    { return await Task.WhenAny(task, Task.Delay(milliseconds), _cancel.Task) == task && !_disposed && !_cancel.Task.IsCompleted; }
    RewardedAd _ad;
    bool AgeAllowed => PrivacyEligibility.OptionalServicesAllowed(_privacySettings);
    bool AdsAllowed => (_privacySettings != null && _privacySettings.advertisingConsent
        && _privacySettings.advertisingPolicyVersion == 1 && _privacySettings.advertisingPublicationRevision == _revision);
    public bool IsReady => AdsAllowed && AgeAllowed && !_disposed && !_busy && !string.IsNullOrEmpty(_unit);
    public bool PrivacyOptionsRequired => !_testAds && ConsentInformation.PrivacyOptionsRequirementStatus == PrivacyOptionsRequirementStatus.Required;
    public GoogleRewardedAdapter(string unit, bool testAds = false, SettingsSaveData privacySettings = null, string revision = "")
    {
        // Never skip UMP for a publisher's unit, even if a caller passes testAds.
        _testAds = testAds && (unit == GoogleServicesConfiguration.AndroidTestRewardedUnitId
            || unit == GoogleServicesConfiguration.IosTestRewardedUnitId);
        _unit = unit; _revision = revision; _privacySettings = privacySettings; _main = SynchronizationContext.Current;
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Register() => OptionalGoogleServices.AdsFactory = c => new GoogleRewardedAdapter(c.CurrentRewardedUnitId, c.TestAdsActive, c.PrivacySettings, c.PrivacyRevision);
    void Main(Action action) { if (_main != null) _main.Post(_ => action(), null); else action(); }
    public void ShowBanner(string placementId) { }
    public void HideBanner() { }

    public async Task RefreshPrivacyAtLaunch()
    {
        if (!_testAds && AdsAllowed && AgeAllowed && !_disposed) await UpdateConsentInfo();
    }
    async Task<bool> UpdateConsentInfo()
    {
        if (!AgeAllowed || !AdsAllowed || _disposed) return false;
        var completion = new TaskCompletionSource<bool>();
        ConsentInformation.Update(new ConsentRequestParameters { TagForUnderAgeOfConsent = false }, error => Main(() =>
            completion.TrySetResult(error == null && !_disposed && AgeAllowed)));
        _consentUpdate = completion.Task;
        return await Task.WhenAny(completion.Task, Task.Delay(30000)) == completion.Task && await completion.Task;
    }
    bool StorageConsentAllowsNpa()
    {
        if (ConsentInformation.ConsentStatus == ConsentStatus.NotRequired) return true;
#if UNITY_ANDROID && !UNITY_EDITOR
        // Conservative additional checks for NPA device access. CanRequestAds
        // alone can also allow Limited Ads after refusal; this release does not
        // intentionally serve that mode. Google still evaluates remaining TCF purposes.
        using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
        using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
        using (var preferences = new AndroidJavaClass("android.preference.PreferenceManager"))
        using (var store = preferences.CallStatic<AndroidJavaObject>("getDefaultSharedPreferences", activity))
        {
            var purposes = store.Call<string>("getString", "IABTCF_PurposeConsents", "");
            var vendors = store.Call<string>("getString", "IABTCF_VendorConsents", "");
            // Google Advertising Products is IAB GVL vendor 755. Recheck this
            // mapping and the published CMP vendor configuration at release audit.
            return purposes.Length > 0 && purposes[0] == '1' && vendors.Length >= 755 && vendors[754] == '1';
        }
#else
        return false; // Publisher-mode consent is verified on Android, never faked in Editor.
#endif
    }
    void ConfigureRequest()
    {
        MobileAds.SetRequestConfiguration(new RequestConfiguration
        {
            AgeRestrictedTreatment = PrivacyEligibility.Band(_privacySettings) == PrivacyAgeBand.From16To17
                ? AgeRestrictedTreatment.Teen : AgeRestrictedTreatment.Unspecified,
            MaxAdContentRating = MaxAdContentRating.PG
        });
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
                // Refresh at launch; retry on an explicit request if that update failed.
                if (_consentUpdate == null || _consentUpdate.Status != TaskStatus.RanToCompletion || !_consentUpdate.Result)
                {
                    if (!await UpdateConsentInfo()) return AdResult.Unavailable;
                }
                if (!AgeAllowed) return AdResult.Unavailable;
                // This API loads and displays the UMP form in one operation. Never
                // release the game gate on a timer while a native form may be open.
                _consentForm = true;
                _privacyClosed = new TaskCompletionSource<bool>();
                var form = _privacyClosed;
                ConsentForm.LoadAndShowConsentFormIfRequired(error => Main(() =>
                    form.TrySetResult(!_disposed && error == null && ConsentInformation.CanRequestAds())));
                if (!await form.Task || _disposed || !AdsAllowed || !AgeAllowed || !StorageConsentAllowsNpa()) return AdResult.Unavailable;
                _consentForm = false; _privacyClosed = null;
            }
            // Google sample app IDs have no publisher UMP message configured.
            // This path is restricted to sample units in Editor/development players.
            ConfigureRequest();
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
            RewardedAd.Load(_unit, new AdRequest { Extras = new Dictionary<string, string> { ["npa"] = "1" } }, (ad, error) => Main(() =>
            {
                if (abandoned || _disposed || cancellation.IsCompleted) { ad?.Destroy(); loaded.TrySetResult(null); }
                else
                {
                    if (error != null) { Debug.LogWarning("[QuietCamp Ads] Load failed: " + error); ad?.Destroy(); }
                    loaded.TrySetResult(error == null ? ad : null);
                }
            }));
            if (!await AwaitPending(loaded.Task, 30000))
            { abandoned = true; return AdResult.Unavailable; }
            _ad = await loaded.Task;
            if (_disposed || _ad == null || !_ad.CanShowAd()) return AdResult.Unavailable;
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
            return await closed.Task;
        }
        catch (Exception error) { Debug.LogWarning("[QuietCamp Ads] " + error.Message); return AdResult.Failed; }
        finally { _ad?.Destroy(); _ad = null; _adClosed = null; _privacyClosed = null; _busy = false; _showing = false; _consentForm = false; }
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
