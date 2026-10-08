using System;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using Unity.Profiling;
using Stopwatch = System.Diagnostics.Stopwatch;
#endif
namespace QuietCamp.Infrastructure
{
    /// <summary>Opt-in Editor audit spans. No recording, allocations or timing calls in a player.</summary>
    public static class PerformanceAudit
    {
#if UNITY_EDITOR
        public struct Span
        {
            public int frame;
            public string stage, name;
            public double startMs, durationMs;
            public long allocatedBytes;
        }
        public static bool Enabled;
        public static string Stage = "unassigned";
        public static readonly List<Span> Spans = new List<Span>(65536);
        static readonly Dictionary<string, ProfilerMarker> Markers = new Dictionary<string, ProfilerMarker>();
        static long _origin;
        public static void Reset()
        {
            Spans.Clear(); _origin = Stopwatch.GetTimestamp(); Enabled = true;
        }
        public static double ElapsedMs => (Stopwatch.GetTimestamp() - _origin) * 1000.0 / Stopwatch.Frequency;
        public readonly struct Scope : IDisposable
        {
            readonly string _name, _stage;
            readonly ProfilerMarker _marker;
            readonly long _start, _allocated;
            readonly int _frame;
            public Scope(string name)
            {
                _name = Enabled ? name : null; _stage = Stage; _frame = Time.frameCount;
                _marker = default; _start = _allocated = 0;
                if (_name == null) return;
                if (!Markers.TryGetValue(name, out _marker)) { _marker = new ProfilerMarker(name); Markers.Add(name, _marker); }
                _marker.Begin(); _allocated = GC.GetAllocatedBytesForCurrentThread(); _start = Stopwatch.GetTimestamp();
            }
            public void Dispose()
            {
                if (_name == null) return;
                long end = Stopwatch.GetTimestamp(), allocated = GC.GetAllocatedBytesForCurrentThread() - _allocated;
                _marker.End();
                if (Spans.Count < 250000) Spans.Add(new Span { frame = _frame, stage = _stage, name = _name,
                    startMs = (_start - _origin) * 1000.0 / Stopwatch.Frequency,
                    durationMs = (end - _start) * 1000.0 / Stopwatch.Frequency, allocatedBytes = allocated });
            }
        }
        public static Scope Measure(string name) => new Scope(name);
#else
        public readonly struct Scope : IDisposable { public void Dispose() { } }
        public static Scope Measure(string name) => default;
#endif
    }
}
