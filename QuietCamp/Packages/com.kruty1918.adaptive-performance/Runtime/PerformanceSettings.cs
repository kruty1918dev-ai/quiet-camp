using UnityEngine;

namespace Kruty1918.Performance
{
    [System.Serializable]
    public struct FrameBudgetSettings
    {
        public float TargetFps;
        public float CpuFrameBudgetMs;
        public float GpuFrameBudgetMs;
        public float AlertCpuFrameMs;
        public float AlertGpuFrameMs;
        public int PercentileWindow;

        public static FrameBudgetSettings CreateDefault()
        {
            return new FrameBudgetSettings
            {
                TargetFps = 60f,
                CpuFrameBudgetMs = 16.6f,
                GpuFrameBudgetMs = 16.6f,
                AlertCpuFrameMs = 19f,
                AlertGpuFrameMs = 19f,
                PercentileWindow = 180,
            };
        }

        public FrameBudgetSettings Normalize()
        {
            return new FrameBudgetSettings
            {
                TargetFps = Mathf.Clamp(TargetFps, 30f, 120f),
                CpuFrameBudgetMs = Mathf.Clamp(CpuFrameBudgetMs, 6f, 45f),
                GpuFrameBudgetMs = Mathf.Clamp(GpuFrameBudgetMs, 6f, 45f),
                AlertCpuFrameMs = Mathf.Clamp(AlertCpuFrameMs, CpuFrameBudgetMs, 60f),
                AlertGpuFrameMs = Mathf.Clamp(AlertGpuFrameMs, GpuFrameBudgetMs, 60f),
                PercentileWindow = Mathf.Clamp(PercentileWindow, 30, 600),
            };
        }
    }

    [System.Serializable]
    public struct PrewarmSettings
    {
        public bool WarmupAllShaders;
        public string[] ShaderResourcePaths;
        public string[] MaterialResourcePaths;
        public string[] CriticalSpriteAtlasResourcePaths;

        public static PrewarmSettings CreateDefault()
        {
            return new PrewarmSettings
            {
                WarmupAllShaders = false,
                ShaderResourcePaths = new string[0],
                MaterialResourcePaths = new string[0],
                CriticalSpriteAtlasResourcePaths = new string[0],
            };
        }
    }
}
