#if QC_GOOGLE_REWARDED && UNITY_ANDROID
using System;
using System.Threading;
using System.Threading.Tasks;
using GoogleMobileAds.Api;
using GoogleMobileAds.Ump.Api;
using QuietCamp.Application;
using QuietCamp.Infrastructure;
using UnityEngine;

sealed class GoogleRewardedAdapter : IAdService, IAdPrivacyOptions, IAdRequestCancellation, IDisposable
{
    readonly string _unit;
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
    public bool IsReady => !_disposed && !_busy && !string.IsNullOrEmpty(_unit);
    public bool PrivacyOptionsRequired => ConsentInformation.PrivacyOptionsRequirementStatus == PrivacyOptionsRequirementStatus.Required;
    public GoogleRewardedAdapter(string unit) { _unit = unit; _main = SynchronizationContext.Current; }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Register() => OptionalGoogleServices.AdsFactory = c => new GoogleRewardedAdapter(c.androidRewardedUnitId);
    void Main(Action action) { if (_main != null) _main.Post(_ => action(), null); else action(); }
    public void ShowBanner(string placementId) { }
    public void HideBanner() { }

    public async Task<AdResult> ShowRewarded(string placementId)
    {
        if (!IsReady || UnityEngine.Application.internetReachability == NetworkReachability.NotReachable) return AdResult.Unavailable;
        _busy = true; _cancel = new TaskCompletionSource<bool>();
        var cancellation = _cancel.Task;
        try
        {
            // No UMP / ad traffic at startup. Update at each explicit request;
            // cached app consent is never used as permission to request ads.
            var consent = new TaskCompletionSource<bool>();
            ConsentInformation.Update(new ConsentRequestParameters(), error => Main(() =>
                consent.TrySetResult(error == null && !_disposed && !cancellation.IsCompleted)));
            if (!await AwaitPending(consent.Task, 30000) || !await consent.Task) return AdResult.Unavailable;
            // This API loads and displays the UMP form in one operation. Never
            // release the game gate on a timer while a native form may be open.
            _consentForm = true;
            _privacyClosed = new TaskCompletionSource<bool>();
            var form = _privacyClosed;
            ConsentForm.LoadAndShowConsentFormIfRequired(error => Main(() =>
                form.TrySetResult(!_disposed && error == null && ConsentInformation.CanRequestAds())));
            if (!await form.Task || _disposed) return AdResult.Unavailable;
            _consentForm = false; _privacyClosed = null;
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
            RewardedAd.Load(_unit, new AdRequest(), (ad, error) => Main(() =>
            {
                if (abandoned || _disposed || cancellation.IsCompleted) { ad?.Destroy(); loaded.TrySetResult(null); }
                else loaded.TrySetResult(error == null ? ad : null);
            }));
            if (!await AwaitPending(loaded.Task, 30000))
            { abandoned = true; return AdResult.Unavailable; }
            _ad = await loaded.Task;
            if (_disposed || _ad == null || !_ad.CanShowAd()) return AdResult.Unavailable;
            var closed = _adClosed = new TaskCompletionSource<AdResult>();
            bool earned = false;
            _ad.OnAdFullScreenContentClosed += () => Main(() => closed.TrySetResult(earned ? AdResult.Completed : AdResult.Skipped));
            _ad.OnAdFullScreenContentFailed += error => Main(() => closed.TrySetResult(AdResult.Failed));
            _showing = true;
            _ad.Show(reward => Main(() => { if (!_disposed) earned = true; }));
            // Do not time out while an ad is actually visible: input/audio must
            // stay gated until its close callback, including app backgrounding.
            return await closed.Task;
        }
        catch { return AdResult.Failed; }
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
