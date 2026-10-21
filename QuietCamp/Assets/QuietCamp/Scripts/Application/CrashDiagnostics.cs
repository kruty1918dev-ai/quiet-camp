using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace QuietCamp.Application
{
    public enum CrashArea { Startup, LevelCheck, Save, DevelopmentTest }
    public interface ICrashTransport : IDisposable
    {
        bool Available { get; }
        Task<bool> Initialize();
        void SetConsent(bool enabled, bool sendPreviousSession = false);
        void SetLevel(int level);
        void Report(CrashArea area);
    }
    public sealed class OfflineCrashTransport : ICrashTransport
    {
        public bool Available => false;
        public Task<bool> Initialize() => Task.FromResult(false);
        public void SetConsent(bool enabled, bool sendPreviousSession = false) { }
        public void SetLevel(int level) { }
        public void Report(CrashArea area) { }
        public void Dispose() { }
    }

    // Separate persisted choice from analytics and advertising. Reports contain
    // allow-listed diagnostic categories, never save contents or exception messages.
    public sealed class CrashDiagnostics : IDisposable
    {
        public const int PolicyVersion = 2;
        readonly SettingsSaveData _settings;
        readonly ICrashTransport _transport;
        readonly Func<bool> _save;
        readonly string _revision;
        readonly HashSet<CrashArea> _reported = new HashSet<CrashArea>();
        int _generation, _level;
        bool _ready, _disposed;
        public bool Available => !_disposed && PrivacyEligibility.Allowed(_settings) && _transport.Available;
        public bool Consented => PrivacyEligibility.Allowed(_settings) && _settings.crashReportingConsent && _settings.crashPolicyVersion == PolicyVersion
            && (_settings.crashPublicationRevision ?? "") == _revision;
        public bool Collecting => !_disposed && _ready && Consented && Available;
        public CrashDiagnostics(SettingsSaveData settings, ICrashTransport transport, Func<bool> save, string revision)
        { _settings = settings; _transport = transport; _save = save; _revision = revision ?? ""; }
        public Task SetConsent(bool enabled) => ApplyConsent(enabled, false);
        // Runtime revocation must not recursively invoke the save callback.
        public void Suspend()
        {
            _generation++; _ready = false;
            try { _transport.SetConsent(false); } catch { }
        }
        async Task ApplyConsent(bool enabled, bool sendPreviousSession)
        {
            if (_disposed) return;
            int generation = ++_generation;
            _ready = false;
            _settings.crashReportingConsent = enabled && Available;
            _settings.crashPolicyVersion = PolicyVersion;
            _settings.crashPublicationRevision = _revision;
            if (!enabled) _settings.crashSessionEligible = false;
            try
            {
                if (_save != null && !_save())
                { _settings.crashReportingConsent = false; _settings.crashSessionEligible = false; }
                if (!sendPreviousSession) _transport.SetConsent(false);
                if (!Consented) { _transport.SetConsent(false); return; }
                bool ready = await _transport.Initialize();
                if (_disposed || generation != _generation) return;
                _ready = ready;
                _transport.SetConsent(Collecting, sendPreviousSession && Collecting);
                if (Collecting) _transport.SetLevel(_level);
            }
            catch
            {
                _ready = false; _settings.crashReportingConsent = false; _settings.crashSessionEligible = false;
                try { _transport.SetConsent(false); } catch { }
            }
        }
        public Task RestoreConsent()
        {
            bool consent = Consented && Available;
            bool sendPreviousSession = consent && _settings.crashSessionEligible;
            // Reports from a launch that began without permission are discarded,
            // including when the player first opts in halfway through that launch.
            _settings.crashSessionEligible = consent;
            return ApplyConsent(consent, sendPreviousSession);
        }
        public void SetLevel(int level)
        {
            _level = Math.Max(0, Math.Min(level, 10000));
            if (Collecting) { try { _transport.SetLevel(_level); } catch { } }
        }
        public void Report(CrashArea area)
        {
            if (!Collecting || !Enum.IsDefined(typeof(CrashArea), area) || !_reported.Add(area)) return;
            try { _transport.Report(area); } catch { /* Diagnostics never interrupt gameplay. */ }
        }
        public void Dispose()
        { _disposed = true; _generation++; try { _transport.Dispose(); } catch { } }
    }
}
