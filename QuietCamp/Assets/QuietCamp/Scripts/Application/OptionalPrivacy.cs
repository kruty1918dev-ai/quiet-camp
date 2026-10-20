using System;
using System.Threading.Tasks;

namespace QuietCamp.Application
{
    // A local age band, not a date of birth or verified identity.
    public enum PrivacyAgeBand { Unknown, Under13, From13To15, From16To17, Adult }
    public static class PrivacyEligibility
    {
        public const int AgePolicyVersion = 1;
        public static PrivacyAgeBand Band(SettingsSaveData settings)
            => settings != null && settings.privacyAgePolicyVersion == AgePolicyVersion
                && Enum.IsDefined(typeof(PrivacyAgeBand), settings.privacyAgeBand)
                ? (PrivacyAgeBand)settings.privacyAgeBand : PrivacyAgeBand.Unknown;
        public static bool OptionalServicesAllowed(SettingsSaveData settings)
            => Band(settings) == PrivacyAgeBand.From16To17 || Band(settings) == PrivacyAgeBand.Adult;
        public static bool CanContinue(SettingsSaveData settings)
            => Band(settings) != PrivacyAgeBand.Unknown && Band(settings) != PrivacyAgeBand.Under13;
        public static void RecordAge(SettingsSaveData settings, PrivacyAgeBand band)
        {
            if (settings == null || !Enum.IsDefined(typeof(PrivacyAgeBand), band)) throw new ArgumentException("Invalid age band.");
            if (Band(settings) == band && settings.privacyAgePolicyVersion == AgePolicyVersion) return;
            settings.privacyAgeBand = (int)band;
            settings.privacyAgePolicyVersion = AgePolicyVersion;
            // Moving between bands never grants or carries an optional permission.
            settings.analyticsConsent = settings.crashReportingConsent = settings.advertisingConsent = false;
            settings.analyticsPolicyVersion = settings.crashReportingPolicyVersion = settings.advertisingPolicyVersion = 0;
            settings.analyticsPublicationRevision = settings.crashReportingPublicationRevision = settings.advertisingPublicationRevision = "";
        }
    }

    /// <summary>SDK factories are invoked only after eligible opt-in, not while
    /// composing services or rendering the availability of a disabled switch.</summary>
    public sealed class DeferredComfortTransport : IComfortTransport
    {
        readonly Func<bool> _available;
        readonly Func<IComfortTransport> _factory;
        IComfortTransport _inner;
        bool _disposed;
        public DeferredComfortTransport(Func<bool> available, Func<IComfortTransport> factory) { _available = available; _factory = factory; }
        public bool Available => !_disposed && _available() && (_inner?.Available ?? true);
        public Task<bool> Initialize() => Available ? (_inner ?? (_inner = _factory())).Initialize() : Task.FromResult(false);
        public void SetConsent(bool enabled) => _inner?.SetConsent(enabled && Available);
        public void ResetLocalData() => _inner?.ResetLocalData();
        public void Send(ComfortRecord record) { if (Available) _inner?.Send(record); }
        public void Dispose() { _disposed = true; _inner?.Dispose(); }
    }
    public sealed class DeferredCrashReportTransport : ICrashReportTransport
    {
        readonly Func<bool> _available;
        readonly Func<ICrashReportTransport> _factory;
        ICrashReportTransport _inner;
        bool _disposed;
        public DeferredCrashReportTransport(Func<bool> available, Func<ICrashReportTransport> factory) { _available = available; _factory = factory; }
        public bool Available => !_disposed && _available() && (_inner?.Available ?? true);
        public Task<bool> Initialize() => Available ? (_inner ?? (_inner = _factory())).Initialize() : Task.FromResult(false);
        public void SetCollection(bool enabled) => _inner?.SetCollection(enabled && Available);
        public void DeletePendingReports() => _inner?.DeletePendingReports();
        public void Dispose() { _disposed = true; _inner?.Dispose(); }
    }

    public interface ICrashReportTransport : IDisposable
    {
        bool Available { get; }
        Task<bool> Initialize();
        void SetCollection(bool enabled);
        // Does not delete already transmitted reports; do not initialize merely to erase.
        void DeletePendingReports();
    }
    public sealed class UnavailableCrashReportTransport : ICrashReportTransport
    {
        public bool Available => false;
        public Task<bool> Initialize() => Task.FromResult(false);
        public void SetCollection(bool enabled) { }
        public void DeletePendingReports() { }
        public void Dispose() { }
    }

    /// <summary>Independent crash-report opt-in; installation and native startup
    /// require separate verification before a production transport is supplied.</summary>
    public sealed class CrashReportConsent : IDisposable
    {
        public const int PolicyVersion = 1;
        readonly SettingsSaveData _settings;
        readonly ICrashReportTransport _transport;
        readonly Action _save;
        readonly Func<bool> _persist;
        public bool ChoicePersisted { get; private set; } = true;
        readonly string _revision;
        int _generation;
        bool _ready, _disposed;
        public bool Available => _transport.Available && PrivacyEligibility.OptionalServicesAllowed(_settings);
        public bool ChoiceCurrent => _settings.crashReportingPolicyVersion == PolicyVersion
            && (_settings.crashReportingPublicationRevision ?? "") == _revision;
        public bool Consented => Available && ChoiceCurrent && _settings.crashReportingConsent;
        public bool Collecting => !_disposed && _ready && Consented;
        public CrashReportConsent(SettingsSaveData settings, ICrashReportTransport transport, Action save = null, string publicationRevision = "", Func<bool> persist = null)
        { _settings = settings; _transport = transport ?? new UnavailableCrashReportTransport(); _save = save; _persist = persist; _revision = publicationRevision ?? ""; }
        public async Task SetConsent(bool enabled)
        {
            if (_disposed) return;
            int generation = ++_generation;
            enabled = enabled && Available;
            _settings.crashReportingConsent = enabled;
            _settings.crashReportingPolicyVersion = PolicyVersion;
            _settings.crashReportingPublicationRevision = _revision;
            _ready = false;
            try { _save?.Invoke(); ChoicePersisted = _persist?.Invoke() ?? true; } catch { ChoicePersisted = false; }
            if (!ChoicePersisted) { enabled = false; _settings.crashReportingConsent = false; }
            if (!enabled)
            {
                StopAndDelete();
                return;
            }
            try
            {
                bool ready = await _transport.Initialize();
                if (_disposed || generation != _generation || !Consented)
                { if (_disposed || !Consented) StopAndDelete(); return; }
                // A verified transport must purge pre-consent reports before returning ready.
                _transport.SetCollection(ready);
                _ready = ready;
            }
            catch { _ready = false; StopAndDelete(); }
        }
        void StopAndDelete()
        {
            try { _transport.SetCollection(false); } catch { }
            try { _transport.DeletePendingReports(); } catch { }
        }
        public Task RestoreConsent() => Consented ? SetConsent(true) : SetConsent(false);
        public void Dispose() { _disposed = true; _generation++; _ready = false; try { _transport.SetCollection(false); } finally { _transport.Dispose(); } }
    }

    public interface IAdLaunchPrivacy { Task RefreshPrivacyAtLaunch(); }
    public interface IAdPrivacyOptions { bool PrivacyOptionsRequired { get; } Task ShowPrivacyOptions(); }
    public interface IAdRequestCancellation { bool CanCancelPending { get; } void CancelPending(); }

    /// <summary>Wraps every ad provider, including development sample inventory.
    /// The factory is not invoked for unknown or under-16 users.</summary>
    public sealed class AgeRestrictedAdService : IAdService, IAdLaunchPrivacy, IAdPrivacyOptions, IAdRequestCancellation, IDisposable
    {
        readonly SettingsSaveData _settings;
        readonly Func<IAdService> _factory;
        IAdService _inner;
        bool _disposed;
        int _generation;
        bool Allowed => !_disposed && PrivacyEligibility.OptionalServicesAllowed(_settings);
        IAdService Inner => Allowed ? (_inner ?? (_inner = _factory())) : null;
        public AgeRestrictedAdService(SettingsSaveData settings, Func<IAdService> factory) { _settings = settings; _factory = factory; }
        public bool IsReady => Inner?.IsReady ?? false;
        public bool PrivacyOptionsRequired => Allowed && _inner is IAdPrivacyOptions p && p.PrivacyOptionsRequired;
        public bool CanCancelPending => _inner is IAdRequestCancellation c && c.CanCancelPending;
        public void CancelPending() { if (_inner is IAdRequestCancellation c) c.CancelPending(); }
        public void ShowBanner(string placementId) { /* No banner integration. */ }
        public void HideBanner() => _inner?.HideBanner();
        public async Task RefreshPrivacyAtLaunch() { if (Inner is IAdLaunchPrivacy p) await p.RefreshPrivacyAtLaunch(); }
        public async Task ShowPrivacyOptions() { if (Allowed && _inner is IAdPrivacyOptions p) await p.ShowPrivacyOptions(); }
        public async Task<AdResult> ShowRewarded(string placementId)
        {
            var provider = Inner;
            if (provider == null) return AdResult.Unavailable;
            int generation = _generation;
            var result = await provider.ShowRewarded(placementId);
            return Allowed && generation == _generation ? result : AdResult.Unavailable;
        }
        public void Invalidate() { _generation++; CancelPending(); (_inner as IDisposable)?.Dispose(); _inner = null; }
        public void Dispose() { _disposed = true; Invalidate(); }
    }
}
