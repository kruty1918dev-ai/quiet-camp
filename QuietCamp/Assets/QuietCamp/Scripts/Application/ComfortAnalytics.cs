using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace QuietCamp.Application
{
    public enum ComfortAction { Place, InvalidDrop, Move, CancelDrag, Rotate, Undo, Redo, Remove, Check, Hint, TutorialStep }
    public enum ComfortScreen { Main, Levels, Album, Settings, Guests, Pause }
    public enum ComfortSetting { Sound, Motion, Calm, Contrast, Haptics, Quality, Language, Text, Scroll }
    public enum ComfortOutcome { Completed, Left, Backgrounded }

    /// <summary>A closed numeric schema. No text, identifiers or raw input are accepted.</summary>
    public sealed class ComfortRecord
    {
        public string Name { get; }
        public IReadOnlyDictionary<string, long> Values { get; }
        internal ComfortRecord(string name, Dictionary<string, long> values)
        { Name = name; Values = new System.Collections.ObjectModel.ReadOnlyDictionary<string, long>(values); }
    }

    public interface IComfortTransport : IDisposable
    {
        bool Available { get; }
        Task<bool> Initialize();
        void SetConsent(bool enabled);
        void Send(ComfortRecord record);
        /// <summary>Deletes local SDK data/instance ID, not already uploaded server data.</summary>
        void ResetLocalData();
    }

    public sealed class OfflineComfortTransport : IComfortTransport
    {
        public bool Available => false;
        public Task<bool> Initialize() => Task.FromResult(false);
        public void SetConsent(bool enabled) { }
        public void Send(ComfortRecord record) { }
        public void ResetLocalData() { }
        public void Dispose() { }
    }

    /// <summary>Opt-in aggregate diagnostics. No disk queue and no retroactive
    /// collection. A level has counters, not a trace of individual gestures.</summary>
    public sealed class ComfortAnalytics : IDisposable
    {
        public const int PolicyVersion = 1;
        public const int SessionEventLimit = 120;
        readonly SettingsSaveData _settings;
        readonly IComfortTransport _transport;
        readonly Action _save;
        readonly string _publicationRevision;
        readonly long[] _counts = new long[Enum.GetValues(typeof(ComfortAction)).Length];
        static readonly string[] RuleKeys = { "bounds", "overlap", "path", "shade", "quiet", "friends", "missing" };
        readonly long[] _ruleCounts = new long[7];
        readonly Dictionary<ComfortSetting, int> _settingBands = new Dictionary<ComfortSetting, int>();
        bool _ready, _disposed, _feedbackSent;
        int _generation, _sent, _level, _width, _height, _preset;
        float _activeSeconds, _performanceSeconds, _frames, _frameTotal;
        ComfortScreen? _lastScreen;
        public bool Available => _transport.Available;
        public bool ChoiceCurrent => _settings.analyticsPolicyVersion == PolicyVersion
            && (_settings.analyticsPublicationRevision ?? "") == _publicationRevision;
        public bool Consented => _settings.analyticsConsent && ChoiceCurrent;
        public bool Collecting => !_disposed && Consented && _ready && Available;
        public bool HasActiveAttempt => _level > 0;

        public ComfortAnalytics(SettingsSaveData settings, IComfortTransport transport, Action save = null, string publicationRevision = "")
        { _settings = settings; _transport = transport ?? new OfflineComfortTransport(); _save = save;
            _publicationRevision = publicationRevision ?? ""; }

        public async Task SetConsent(bool enabled)
        {
            if (_disposed) return;
            var generation = ++_generation;
            _settings.analyticsConsent = enabled;
            _settings.analyticsPolicyVersion = PolicyVersion;
            _settings.analyticsPublicationRevision = _publicationRevision;
            _ready = false;
            ClearAttempt(); _lastScreen = null;
            _settingBands.Clear();
            _performanceSeconds = _frames = _frameTotal = 0;
            _save?.Invoke();
            if (!enabled)
            {
                try { _transport.SetConsent(false); _transport.ResetLocalData(); } catch { }
                return;
            }
            if (!Available) return;
            try
            {
                var ready = await _transport.Initialize();
                // Withdrawal while dependencies are being resolved must win.
                if (_disposed || generation != _generation || !Consented)
                {
                    if (!Consented || _disposed) { _transport.SetConsent(false); _transport.ResetLocalData(); }
                    return;
                }
                _transport.SetConsent(ready);
                _ready = ready;
            }
            catch { _ready = false; try { _transport.SetConsent(false); } catch { } }
        }

        public Task RestoreConsent()
        {
            if (Consented) return SetConsent(true);
            _settings.analyticsConsent = false;
            try { _transport.SetConsent(false); } catch { }
            return Task.CompletedTask;
        }
        public Task ClearLocalDataAndWithdraw() => SetConsent(false);

        public void BeginLevel(int number, int width, int height, int preset)
        {
            ClearAttempt();
            if (!Collecting || number < 1 || number > 30) return;
            _level = number; _width = Clamp(width, 4, 8); _height = Clamp(height, 4, 8); _preset = Clamp(preset, 0, 3);
            Send("qc_level_begin", LevelValues());
        }
        public void Action(ComfortAction action)
        {
            int index = (int)action;
            if (Collecting && _level > 0 && index >= 0 && index < _counts.Length)
                _counts[index] = Math.Min(250, _counts[index] + 1);
        }
        public void EndLevel(ComfortOutcome outcome, bool keepAttempt = false)
        {
            if (!Collecting || _level == 0) { ClearAttempt(); return; }
            var values = LevelValues();
            values["outcome"] = Clamp((int)outcome, 0, 2);
            // Duration buckets: <30s, <1m, <2m, <5m, <10m, longer.
            values["active_time_band"] = _activeSeconds < 30 ? 0 : _activeSeconds < 60 ? 1 : _activeSeconds < 120 ? 2 : _activeSeconds < 300 ? 3 : _activeSeconds < 600 ? 4 : 5;
            for (int i = 0; i < _counts.Length; i++) values["n_" + ((ComfortAction)i).ToString().ToLowerInvariant()] = _counts[i];
            for (int i = 0; i < RuleKeys.Length; i++) values["rule_" + RuleKeys[i]] = _ruleCounts[i];
            Send("qc_level_summary", values);
            if (keepAttempt) { Array.Clear(_counts, 0, _counts.Length); Array.Clear(_ruleCounts, 0, _ruleCounts.Length); _activeSeconds = 0; }
            else ClearAttempt();
        }
        public void Screen(ComfortScreen screen)
        {
            if (!Collecting || _lastScreen == screen || !Enum.IsDefined(typeof(ComfortScreen), screen)) return;
            _lastScreen = screen;
            Send("qc_screen", new Dictionary<string, long> { ["screen"] = (int)screen });
        }
        public void RuleReport(QuietCamp.Domain.RuleReport report)
        {
            if (!Collecting || _level == 0 || report == null) return;
            // Once per rule per explicit check. Never copy guest IDs/cells.
            for (int i = 0; i < RuleKeys.Length; i++)
                if (report.Issues.Exists(issue => issue.Code == RuleKeys[i]
                    || (RuleKeys[i] == "friends" && issue.Code == "friends-missing")))
                    _ruleCounts[i] = Math.Min(250, _ruleCounts[i] + 1);
        }
        public void Setting(ComfortSetting setting, int bucket)
        {
            if (!Collecting || !Enum.IsDefined(typeof(ComfortSetting), setting)) return;
            int band=Clamp(bucket,0,10);
            if(_settingBands.TryGetValue(setting,out var previous)&&previous==band)return;
            _settingBands[setting]=band;
            Send("qc_comfort_setting", new Dictionary<string, long> { ["setting"] = (int)setting, ["value_band"] = band });
        }
        public void Feedback(int choice)
        {
            if (!Collecting || choice < 0 || choice > 2 || _feedbackSent) return;
            _feedbackSent = true;
            Send("qc_comfort_feedback", new Dictionary<string, long> { ["choice"] = choice });
        }
        public void Tick(float deltaTime, bool gameplayActive, int quality)
        {
            if (!Collecting || deltaTime <= 0 || deltaTime > 1) return;
            if (gameplayActive && _level > 0) _activeSeconds += deltaTime;
            _performanceSeconds += deltaTime; _frames++; _frameTotal += deltaTime;
            if (_performanceSeconds < 60) return;
            float ms = _frameTotal * 1000 / Math.Max(1, _frames);
            Send("qc_frame_budget", new Dictionary<string, long> { ["frame_band"] = ms <= 18 ? 0 : ms <= 35 ? 1 : ms <= 50 ? 2 : 3, ["quality"] = Clamp(quality, 0, 2) });
            _performanceSeconds = _frames = _frameTotal = 0;
        }
        Dictionary<string, long> LevelValues() => new Dictionary<string, long>
        { ["level"] = _level, ["width"] = _width, ["height"] = _height, ["preset"] = _preset, ["schema"] = 1 };
        void Send(string name, Dictionary<string, long> values)
        {
            if (!Collecting || _sent >= SessionEventLimit) return;
            try { _transport.Send(new ComfortRecord(name, values)); _sent++; }
            catch { _ready = false; try { _transport.SetConsent(false); } catch { } }
        }
        void ClearAttempt() { _level = 0; _activeSeconds = 0; Array.Clear(_counts, 0, _counts.Length); Array.Clear(_ruleCounts, 0, _ruleCounts.Length); }
        static int Clamp(int value, int min, int max) => Math.Max(min, Math.Min(max, value));
        public void Dispose() { _disposed = true; _generation++; ClearAttempt(); _transport.Dispose(); }
    }
}
