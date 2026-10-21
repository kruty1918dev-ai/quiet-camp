using System;
using System.Threading.Tasks;

namespace QuietCamp.Application
{
    public sealed class PrivacyComfortTransport : IComfortTransport
    {
        readonly Func<bool> _available, _allowed;
        readonly Func<IComfortTransport> _factory;
        IComfortTransport _inner;
        public PrivacyComfortTransport(Func<bool> available, Func<bool> allowed, Func<IComfortTransport> factory)
        { _available = available; _allowed = allowed; _factory = factory; }
        public bool Available => _available();
        public Task<bool> Initialize() => _allowed() ? (_inner ?? (_inner = _factory())).Initialize() : Task.FromResult(false);
        public void SetConsent(bool enabled) => _inner?.SetConsent(enabled && _allowed());
        public void Send(ComfortRecord record) { if (_allowed()) _inner?.Send(record); }
        public void ResetLocalData() => _inner?.ResetLocalData();
        public void Dispose() => _inner?.Dispose();
    }
    public sealed class PrivacyCrashTransport : ICrashTransport
    {
        readonly Func<bool> _available, _allowed;
        readonly Func<ICrashTransport> _factory;
        ICrashTransport _inner;
        public PrivacyCrashTransport(Func<bool> available, Func<bool> allowed, Func<ICrashTransport> factory)
        { _available = available; _allowed = allowed; _factory = factory; }
        public bool Available => _available();
        public Task<bool> Initialize() => _allowed() ? (_inner ?? (_inner = _factory())).Initialize() : Task.FromResult(false);
        public void SetConsent(bool enabled, bool sendPreviousSession = false) => _inner?.SetConsent(enabled && _allowed(), sendPreviousSession && _allowed());
        public void SetLevel(int level) { if (_allowed()) _inner?.SetLevel(level); }
        public void Report(CrashArea area) { if (_allowed()) _inner?.Report(area); }
        public void Dispose() => _inner?.Dispose();
    }
    public sealed class PrivacyAdService : IAdService, IAdPrivacyOptions, IAdConsentInitialization, IAdRequestCancellation, IDisposable
    {
        readonly Func<bool> _allowed, _eligible;
        readonly Func<IAdService> _factory;
        readonly Func<bool> _restorePrivacyOptions;
        IAdService _inner;
        bool _disposed;
        int _generation;
        public PrivacyAdService(Func<bool> allowed, Func<bool> eligible, Func<IAdService> factory, Func<bool> restorePrivacyOptions = null)
        { _allowed = allowed; _eligible = eligible; _factory = factory; _restorePrivacyOptions = restorePrivacyOptions; }
        public bool IsReady => !_disposed && _allowed() && (_inner?.IsReady ?? true);
        public bool PrivacyOptionsRequired => !_disposed && _eligible() && ((_restorePrivacyOptions?.Invoke() ?? false)
            || (_inner is IAdPrivacyOptions privacy && privacy.PrivacyOptionsRequired));
        public bool CanCancelPending => _inner is IAdRequestCancellation request && request.CanCancelPending;
        public void CancelPending() { _generation++; (_inner as IAdRequestCancellation)?.CancelPending(); }
        IAdService Provider() => _inner ?? (_inner = _factory());
        public async Task<AdResult> ShowRewarded(string placementId)
        {
            if (!IsReady) return AdResult.Unavailable;
            int generation = _generation;
            var result = await Provider().ShowRewarded(placementId);
            return !_disposed && generation == _generation && _allowed() ? result : AdResult.Unavailable;
        }
        public Task RefreshConsent() => IsReady && Provider() is IAdConsentInitialization consent ? consent.RefreshConsent() : Task.CompletedTask;
        public Task ShowPrivacyOptions() => PrivacyOptionsRequired && Provider() is IAdPrivacyOptions privacy ? privacy.ShowPrivacyOptions() : Task.CompletedTask;
        public void ShowBanner(string placementId) { }
        public void HideBanner() { }
        public void Dispose() { _disposed = true; _generation++; (_inner as IDisposable)?.Dispose(); }
    }
}
