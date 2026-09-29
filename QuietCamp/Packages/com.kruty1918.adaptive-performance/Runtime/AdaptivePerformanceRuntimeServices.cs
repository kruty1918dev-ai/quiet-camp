using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.U2D;

namespace Kruty1918.Performance
{
    public struct FrameTimeSnapshot
    {
        public float P50Ms;
        public float P95Ms;
        public float P99Ms;
        public float CpuBudgetMs;
        public float GpuBudgetMs;
        public string LastDegradationReason;
    }

    public interface IFrameBudgetMonitorService
    {
        FrameTimeSnapshot GetSnapshot();
        void SetDegradationReason(string reason);
    }

    public sealed class FrameBudgetMonitorService : IFrameBudgetMonitorService
    {
        private readonly FrameBudgetSettings _budget;
        private readonly Queue<float> _frameTimes;
        private readonly float[] _sortedBuffer;
        private string _lastDegradationReason;

        public FrameBudgetMonitorService(FrameBudgetSettings budget)
        {
            _budget = budget.Normalize();
            _frameTimes = new Queue<float>(_budget.PercentileWindow);
            _sortedBuffer = new float[_budget.PercentileWindow];
        }

        public void Tick()
        {
            float frameMs = Mathf.Max(0.1f, Time.unscaledDeltaTime * 1000f);
            if (_frameTimes.Count >= _budget.PercentileWindow)
                _frameTimes.Dequeue();

            _frameTimes.Enqueue(frameMs);
        }

        public void SetDegradationReason(string reason)
        {
            _lastDegradationReason = reason ?? string.Empty;
        }

        public FrameTimeSnapshot GetSnapshot()
        {
            int count = _frameTimes.Count;
            if (count == 0)
            {
                return new FrameTimeSnapshot
                {
                    CpuBudgetMs = _budget.CpuFrameBudgetMs,
                    GpuBudgetMs = _budget.GpuFrameBudgetMs,
                    LastDegradationReason = _lastDegradationReason,
                };
            }

            int index = 0;
            foreach (var item in _frameTimes)
                _sortedBuffer[index++] = item;

            Array.Sort(_sortedBuffer, 0, count);

            return new FrameTimeSnapshot
            {
                P50Ms = Percentile(_sortedBuffer, count, 0.50f),
                P95Ms = Percentile(_sortedBuffer, count, 0.95f),
                P99Ms = Percentile(_sortedBuffer, count, 0.99f),
                CpuBudgetMs = _budget.CpuFrameBudgetMs,
                GpuBudgetMs = _budget.GpuFrameBudgetMs,
                LastDegradationReason = _lastDegradationReason,
            };
        }

        private static float Percentile(float[] values, int count, float percentile)
        {
            if (count <= 0)
                return 0f;

            int rank = Mathf.Clamp(Mathf.CeilToInt(count * percentile) - 1, 0, count - 1);
            return values[rank];
        }
    }

    public interface IStartupPrewarmService
    {
        Task PrewarmAsync(CancellationToken ct = default);
    }

    public interface IScenePreActivationInitializer
    {
        Task InitializeBeforeActivationAsync(
            CancellationToken ct = default);
    }

    public sealed class StartupPrewarmService : IStartupPrewarmService
    {
        private readonly PrewarmSettings _settings;

        public StartupPrewarmService(PrewarmSettings settings)
        {
            _settings = settings;
        }

        public async Task PrewarmAsync(CancellationToken ct = default)
        {
            await LoadResourcesAsync<Shader>(_settings.ShaderResourcePaths, ct);
            await LoadResourcesAsync<Material>(_settings.MaterialResourcePaths, ct);
            await LoadResourcesAsync<SpriteAtlas>(_settings.CriticalSpriteAtlasResourcePaths, ct);

            if (_settings.WarmupAllShaders)
                Shader.WarmupAllShaders();
        }

        private static async Task LoadResourcesAsync<T>(string[] paths, CancellationToken ct) where T : UnityEngine.Object
        {
            if (paths == null || paths.Length == 0)
                return;

            for (int i = 0; i < paths.Length; i++)
            {
                ct.ThrowIfCancellationRequested();
                string path = paths[i];
                if (string.IsNullOrWhiteSpace(path))
                    continue;

                ResourceRequest request = Resources.LoadAsync<T>(path);
                while (!request.isDone)
                {
                    ct.ThrowIfCancellationRequested();
                    await Task.Yield();
                }
            }
        }
    }

}
