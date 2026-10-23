using System;
using System.Threading.Tasks;

namespace QuietCamp.Application
{
    // Separate from Firebase. No user/session/install IDs, exact level or time.
    // Memory-only aggregates are discarded on withdrawal and app termination.
    public sealed class OperationalMetrics
    {
        readonly Func<bool> _permitted;
        readonly Func<string, int, int, Task<bool>> _send;
        readonly int[,] _counts = new int[3, 6];
        bool _busy;
        int _generation;
        static readonly string[] Names = { "level_result", "technical_failure", "frame_time" };
        public OperationalMetrics(Func<bool> permitted, Func<string, int, int, Task<bool>> send)
        { _permitted = permitted; _send = send; }
        public void Clear() { _generation++; Array.Clear(_counts, 0, _counts.Length); }
        public void Record(int metric, int bucket)
        {
            if (!_permitted()) { Clear(); return; }
            if (metric < 0 || metric >= 3 || bucket < 0 || bucket >= 6) return;
            _counts[metric, bucket] = Math.Min(100, _counts[metric, bucket] + 1);
        }
        public async Task Flush()
        {
            if (_busy) return;
            if (!_permitted()) { Clear(); return; }
            int total = 0; foreach (int count in _counts) total += count;
            if (total < 10) return;
            int generation = _generation;
            _busy = true;
            try
            {
                for (int metric = 0; metric < 3; metric++) for (int bucket = 0; bucket < 6; bucket++)
                {
                    if (generation != _generation) return;
                    if (!_permitted()) { Clear(); return; }
                    int count = _counts[metric, bucket];
                    if (count > 0 && await _send(Names[metric], bucket, count)) {
                        if (generation != _generation) return;
                        _counts[metric, bucket] = Math.Max(0, _counts[metric, bucket] - count);
                    }
                }
            }
            catch { }
            finally { _busy = false; }
        }
    }
}
