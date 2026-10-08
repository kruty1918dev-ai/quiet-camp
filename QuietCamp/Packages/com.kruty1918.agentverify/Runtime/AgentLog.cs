using System;
using System.Collections.Generic;
using UnityEngine;

namespace Kruty1918.AgentVerify
{
    /// <summary>
    /// Console-log capture for verification runs. Hooks
    /// Application.logMessageReceived once per session and keeps a bounded
    /// buffer so an agent can ask "did anything error since my mark?".
    /// Works in edit mode too when installed via AgentVerifyInstall (editor asmdef).
    /// </summary>
    public static class AgentLog
    {
        [Serializable]
        public class Entry
        {
            public string type;     // Log | Warning | Error | Exception | Assert
            public string message;
            public string stack;
            public double time;     // Time.realtimeSinceStartup at capture
        }

        const int MaxEntries = 2000;
        static readonly List<Entry> _entries = new List<Entry>(256);
        static bool _installed;

        /// <summary>Idempotent. Called automatically on play via RuntimeInitializeOnLoadMethod.</summary>
        public static void Install()
        {
            if (_installed) return;
            Application.logMessageReceived += OnLog;
            _installed = true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void AutoInstall()
        {
            _entries.Clear();
            Install();
        }

        /// <summary>Snapshot index; pair with Since(mark) for incremental reads.</summary>
        public static int Mark() => _entries.Count;

        public static void Clear() => _entries.Clear();

        /// <summary>Entries captured since <paramref name="mark"/> (default: all).</summary>
        public static List<Entry> Since(int mark = 0)
        {
            var result = new List<Entry>();
            for (var i = Mathf.Clamp(mark, 0, _entries.Count); i < _entries.Count; i++)
                result.Add(_entries[i]);
            return result;
        }

        /// <summary>Error + Exception + Assert count since <paramref name="mark"/>.</summary>
        public static int ErrorCount(int mark = 0)
        {
            var n = 0;
            foreach (var e in Since(mark))
                if (e.type == "Error" || e.type == "Exception" || e.type == "Assert") n++;
            return n;
        }

        /// <summary>True when no Error/Exception/Assert was captured since <paramref name="mark"/>.</summary>
        public static bool IsClean(int mark = 0) => ErrorCount(mark) == 0;

        static void OnLog(string message, string stack, LogType type)
        {
            if (_entries.Count >= MaxEntries) _entries.RemoveAt(0);
            _entries.Add(new Entry
            {
                type = type.ToString(),
                message = message ?? "",
                stack = stack ?? "",
                time = Time.realtimeSinceStartupAsDouble
            });
        }
    }
}
