using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace QuietCamp.Infrastructure
{
    public enum SessionRequestKind { SignIn, Refresh }
    public enum SessionRequestResult { Success, ConnectionError, ProtocolError, DataProcessingError, Exception }
    public sealed class AccessRequestDiagnostic
    {
        public SessionRequestKind operation;
        public SessionRequestResult result;
        public long status;
        public double elapsedMs;
        public bool cloudflare1010;
    }
    [Serializable] public sealed class LocalSessionDevice
    {
        public string appVersion, buildGuid, unityVersion, platform, operatingSystem, model, graphicsDevice, graphicsApi;
        public int systemMemoryMb, graphicsMemoryMb, processors;
    }
    [Serializable] public sealed class LocalSessionEvent
    {
        public double seconds, durationMs;
        public string kind, code, levelId, signature;
        public string[] methods;
        public long number;
        public bool flag;
    }
    [Serializable] public sealed class LocalSessionSample
    {
        public double seconds, fps, maximumFrameMs;
        public int slow33Ms, slow100Ms, width, height, quality, completedLevels;
        public long managedBytes, allocatedBytes;
        public string levelId;
        public bool pro;
    }
    [Serializable] public sealed class LocalSessionDocument
    {
        public int schemaVersion = 1;
        public string sessionId, startedUtc, updatedUtc;
        public bool endedNormally;
        public int droppedEvents, droppedSamples, writeFailures;
        public LocalSessionDevice device;
        public LocalSessionEvent[] events;
        public LocalSessionSample[] samples;
    }

    // Local diagnostic-build recording. No save contents, account input, URLs,
    // request/response bodies, identifiers, raw exception messages or automatic upload.
    public sealed class LocalSessionJournal : IDisposable
    {
        public const int EventLimit = 2048, SampleLimit = 2048, RetainedSessions = 50;
        static readonly HashSet<string> UiActions = new HashSet<string>(StringComparer.Ordinal) {
            "qc.continue", "qc.play", "qc.check", "qc.hint", "qc.undo", "qc.redo", "qc.restart", "qc.reset",
            "qc.next", "qc.menu", "qc.back", "qc.settings", "qc.economy", "qc.album", "qc.select", "qc.tutorial.restart",
            "qc.pause", "qc.resume", "qc.rotate", "qc.remove", "qc.autoplace", "qc.transition.back"
        };
        static readonly string[] ErrorTypes = { "NullReferenceException", "IndexOutOfRangeException", "ArgumentException",
            "InvalidOperationException", "MissingReferenceException", "OutOfMemoryException", "IOException",
            "TypeInitializationException", "NotSupportedException", "UnityException" };
        readonly object _gate = new object();
        readonly List<LocalSessionEvent> _events = new List<LocalSessionEvent>();
        readonly List<LocalSessionSample> _samples = new List<LocalSessionSample>();
        readonly Stopwatch _clock = Stopwatch.StartNew();
        readonly string _id = Guid.NewGuid().ToString("N"), _started;
        readonly LocalSessionDevice _device;
        Task _pending = Task.CompletedTask;
        int _droppedEvents, _droppedSamples, _writeFailures;
        bool _ended, _disposed;
        public bool LastWriteSucceeded { get; private set; }
        double _lastError = -1;
        public string DirectoryPath { get; }
        public string FilePath { get; }
        public double ElapsedSeconds => _clock.Elapsed.TotalSeconds;

        public LocalSessionJournal(string directory, LocalSessionDevice device)
        {
            DirectoryPath = Path.GetFullPath(directory); _device = device;
            _started = DateTime.UtcNow.ToString("O");
            Directory.CreateDirectory(DirectoryPath);
            FilePath = Path.Combine(DirectoryPath, "session-" + DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ") + "-" + _id + ".json");
            RecoverAndPrune();
            Lifecycle("start");
            FlushBlocking();
        }
        static string SafeLevel(string value) => value != null && Regex.IsMatch(value, @"^(QC[0-9]{3}|gen:qc_camp:[0-9]{1,4}|QC_[A-Z]{2}[0-9]{3})$") ? value : null;
        void Add(LocalSessionEvent value)
        {
            lock (_gate) {
                if (_disposed) return;
                value.seconds = ElapsedSeconds;
                if (_events.Count == EventLimit) { _events.RemoveAt(32); _droppedEvents++; }
                _events.Add(value);
            }
        }
        public void Lifecycle(string code)
        {
            if (code != "start" && code != "pause" && code != "resume" && code != "focus" && code != "unfocus" && code != "quit" && code != "low-memory") return;
            Add(new LocalSessionEvent { kind = "lifecycle", code = code });
        }
        public void Scene(string name, string levelId)
            => Add(new LocalSessionEvent { kind = "scene", code = name == "Boot" || name == "MainMenu" || name == "Camp" ? name : "other", levelId = SafeLevel(levelId) });
        public void UiAction(string actionId, int status, bool consumed)
            => Add(new LocalSessionEvent { kind = "ui", code = UiActions.Contains(actionId ?? "") ? actionId : "other", number = status, flag = consumed });
        public void Access(AccessRequestDiagnostic value)
            => Add(new LocalSessionEvent { kind = "complimentary-http", code = value.operation + ":" + value.result, number = value.status,
                durationMs = value.elapsedMs, flag = value.cloudflare1010 });
        public void SaveFailed() => Add(new LocalSessionEvent { kind = "save", code = "failed" });
        public void UnityError(string condition, string stack, int logType)
        {
            lock (_gate) {
                if (_disposed) return;
                if (ElapsedSeconds - _lastError < .25) { _droppedEvents++; return; }
                _lastError = ElapsedSeconds;
            }
            string message = condition ?? "";
            if (message.Length > 8192) message = message.Substring(0, 8192);
            var methods = new List<string>();
            foreach (string line in (stack ?? "").Split('\n')) {
                int start = line.IndexOf("QuietCamp.", StringComparison.Ordinal);
                if (start < 0) continue;
                string method = line.Substring(start).Split('(')[0].Trim();
                if (method.Length <= 180 && Regex.IsMatch(method, @"^[A-Za-z0-9_.<>+`]+$")) methods.Add(method);
                if (methods.Count == 8) break;
            }
            string code = ErrorTypes.FirstOrDefault(message.Contains) ?? "RuntimeError";
            string signature;
            using (var sha = SHA256.Create()) signature = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(code + "|" + string.Join("|", methods)))).Replace("-", "").ToLowerInvariant();
            Add(new LocalSessionEvent { kind = "error", code = code, number = logType, signature = signature, methods = methods.ToArray() });
        }
        public void Sample(LocalSessionSample sample)
        {
            lock (_gate) {
                if (_disposed) return;
                sample.seconds = ElapsedSeconds; sample.levelId = SafeLevel(sample.levelId);
                if (_samples.Count == SampleLimit) { _samples.RemoveAt(0); _droppedSamples++; }
                _samples.Add(sample);
            }
        }
        public Task FlushAsync()
        {
            lock (_gate) {
                if (_disposed) return _pending;
                var snapshot = new LocalSessionDocument { sessionId = _id, startedUtc = _started, updatedUtc = DateTime.UtcNow.ToString("O"),
                    endedNormally = _ended, device = _device, events = _events.ToArray(), samples = _samples.ToArray(),
                    droppedEvents = _droppedEvents, droppedSamples = _droppedSamples, writeFailures = _writeFailures };
                _pending = _pending.ContinueWith(_ => Write(snapshot), TaskScheduler.Default);
                return _pending;
            }
        }
        void Write(LocalSessionDocument snapshot)
        {
            try {
                string temporary = FilePath + ".tmp", backup = FilePath + ".bak";
                byte[] bytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(snapshot));
                using (var file = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None)) { file.Write(bytes, 0, bytes.Length); file.Flush(true); }
                if (File.Exists(FilePath)) {
                    try { File.Replace(temporary, FilePath, backup); }
                    catch (PlatformNotSupportedException) { File.Copy(FilePath, backup, true); File.Delete(FilePath); File.Move(temporary, FilePath); }
                } else File.Move(temporary, FilePath);
                if (File.Exists(backup)) File.Delete(backup);
                lock (_gate) LastWriteSucceeded = true;
            } catch { lock (_gate) { _writeFailures++; LastWriteSucceeded = false; } } // Never recurse into Unity logging or expose paths/messages.
        }
        void RecoverAndPrune()
        {
            foreach (string backup in Directory.GetFiles(DirectoryPath, "session-*.json.bak")) {
                string original = backup.Substring(0, backup.Length - 4);
                if (!File.Exists(original)) File.Move(backup, original);
                else File.Delete(backup);
            }
            var previous = new DirectoryInfo(DirectoryPath).GetFiles("session-*.json").OrderByDescending(f => f.LastWriteTimeUtc).Skip(RetainedSessions - 1);
            foreach (var file in previous) file.Delete();
            foreach (string temporary in Directory.GetFiles(DirectoryPath, "session-*.json.tmp")) File.Delete(temporary);
        }
        public void FlushBlocking() => FlushAsync().GetAwaiter().GetResult();
        public void Dispose()
        {
            lock (_gate) { if (_disposed) return; _ended = true; }
            Lifecycle("quit"); FlushBlocking();
            lock (_gate) _disposed = true;
        }
    }
}
