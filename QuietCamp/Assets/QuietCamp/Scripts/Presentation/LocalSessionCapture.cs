using System;
using System.IO;
using Kruty1918.UIActions.API;
using QuietCamp.Infrastructure;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.SceneManagement;

namespace QuietCamp.Presentation
{
    // Owned by the persistent bootstrap; enabled only by QC_SESSION_LOGS builds.
    public sealed class LocalSessionCapture : MonoBehaviour
    {
        public LocalSessionJournal Journal { get; private set; }
        GameServices _services;
        double _seconds, _maximum;
        int _frames, _slow33, _slow100;
        bool _paused;
        void Awake()
        {
            try {
                Journal = new LocalSessionJournal(Path.Combine(UnityEngine.Application.persistentDataPath, "session-logs"), new LocalSessionDevice {
                    appVersion = UnityEngine.Application.version, buildGuid = UnityEngine.Application.buildGUID,
                    unityVersion = UnityEngine.Application.unityVersion, platform = UnityEngine.Application.platform.ToString(),
                    operatingSystem = SystemInfo.operatingSystem, model = SystemInfo.deviceModel,
                    graphicsDevice = SystemInfo.graphicsDeviceName, graphicsApi = SystemInfo.graphicsDeviceType.ToString(),
                    systemMemoryMb = SystemInfo.systemMemorySize, graphicsMemoryMb = SystemInfo.graphicsMemorySize, processors = SystemInfo.processorCount
                });
                UnityEngine.Application.logMessageReceivedThreaded += OnLog;
                UnityEngine.Application.lowMemory += OnLowMemory;
                SceneManager.sceneLoaded += OnScene;
                Journal.Scene(SceneManager.GetActiveScene().name, null);
            } catch { Journal = null; } // Diagnostics must never prevent game startup.
        }
        public void Configure(GameServices services) { _services = services; }
        void OnScene(Scene scene, LoadSceneMode _) => Journal?.Scene(scene.name, scene.name == "Camp" ? _services?.PendingLevelId : null);
        void OnLog(string message, string stack, LogType type)
        {
            if (type == LogType.Log) return;
            try { Journal?.UnityError(message, stack, (int)type); } catch { /* A logging callback must never fail gameplay. */ }
        }
        void OnLowMemory() { Journal?.Lifecycle("low-memory"); if (Journal != null) _ = Journal.FlushAsync(); }
        void Update()
        {
            if (Journal == null || _paused) return;
            double frame = Time.unscaledDeltaTime;
            _seconds += frame; _frames++; _maximum = Math.Max(_maximum, frame);
            if (frame > .033333) _slow33++; if (frame > .1) _slow100++;
            if (_seconds < 5) return;
            Journal.Sample(new LocalSessionSample { fps = _frames / _seconds, maximumFrameMs = _maximum * 1000,
                slow33Ms = _slow33, slow100Ms = _slow100, width = Screen.width, height = Screen.height,
                quality = _services?.EffectiveQuality ?? QualitySettings.GetQualityLevel(),
                managedBytes = GC.GetTotalMemory(false), allocatedBytes = Profiler.GetTotalAllocatedMemoryLong(),
                levelId = SceneManager.GetActiveScene().name == "Camp" ? _services?.PendingLevelId : null,
                pro = _services?.Economy.IsPro ?? false, completedLevels = _services?.Progression.CompletedCount ?? 0 });
            _seconds = _maximum = 0; _frames = _slow33 = _slow100 = 0;
            _ = Journal.FlushAsync();
        }
        void OnApplicationPause(bool paused)
        {
            _paused = paused; _seconds = _maximum = 0; _frames = _slow33 = _slow100 = 0;
            Journal?.Lifecycle(paused ? "pause" : "resume");
            if (paused) Journal?.FlushBlocking();
        }
        void OnApplicationFocus(bool focused) => Journal?.Lifecycle(focused ? "focus" : "unfocus");
        void OnApplicationQuit() { Journal?.Dispose(); }
        void OnDestroy()
        {
            UnityEngine.Application.logMessageReceivedThreaded -= OnLog;
            UnityEngine.Application.lowMemory -= OnLowMemory;
            SceneManager.sceneLoaded -= OnScene;
            Journal?.Dispose();
        }
    }
    // Decorates the canonical action journal. Payload, target, details and text input are never copied.
    sealed class SessionActionJournal : IUiActionJournal
    {
        readonly IUiActionJournal _inner;
        readonly LocalSessionJournal _local;
        public SessionActionJournal(IUiActionJournal inner, LocalSessionJournal local) { _inner = inner; _local = local; }
        public int Capacity => _inner.Capacity;
        public int Count => _inner.Count;
        public void Record(in UiActionRequest request, in UiActionResult result)
        { _inner.Record(in request, in result); _local.UiAction(request.ActionId.Value, (int)result.Status, result.Consumed); }
        public System.Collections.Generic.IReadOnlyList<UiActionJournalEntry> GetRecent(int count) => _inner.GetRecent(count);
    }
}
