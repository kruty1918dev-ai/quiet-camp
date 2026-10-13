#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using QuietCamp.Editor.LevelAuthoring;
using Unity.Profiling;
using UnityEditor;

namespace QuietCamp.Editor
{
    public static partial class QC007ArtCapture
    {
        sealed class AuthoringTiming
        {
            public string name;
            public ProfilerRecorder recorder;
            public long total, peak;
            public int samples;
            public void Read()
            {
                long value = recorder.Valid ? recorder.LastValue : 0;
                if (value <= 0) return;
                total += value; peak = Math.Max(peak, value); samples++;
            }
        }
        static AuthoringTiming[] _authoringTimings;
        static int _profileFrames;
        static void BeginAuthoringProfile()
        {
            if (_authoringTimings != null) throw new InvalidOperationException("A profile is already running.");
            _authoringTimings = new[] { "Panel", "SceneView", "Thumbnail", "Rules", "Landscape.Bake" }
                .Select(name => new AuthoringTiming { name = name, recorder = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "QuietCamp.Authoring." + name, 1) }).ToArray();
            _profileFrames = 0; EditorApplication.update += PollAuthoringProfile;
            AssemblyReloadEvents.beforeAssemblyReload += EndAuthoringProfile;
            File.WriteAllText(ResultPath, "{\"status\":\"profiling\"}");
        }
        static void PollAuthoringProfile()
        {
            if (EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode) { EndAuthoringProfile(); return; }
            foreach (var sample in _authoringTimings) sample.Read();
            var window = LevelAuthoringWindow.OpenWindow(); window.Repaint(); SceneView.RepaintAll();
            if (++_profileFrames < 60) return;
            string report = JsonConvert.SerializeObject(new { status = "authoring-profile", frames = _profileFrames,
                unity = UnityEngine.Application.unityVersion, timings = _authoringTimings.Select(t => new { t.name, available = t.recorder.Valid,
                    t.samples, averageMs = t.samples == 0 ? 0 : t.total / (double)t.samples / 1000000, peakMs = t.peak / 1000000d }) }, Formatting.Indented);
            File.WriteAllText(Path.Combine(UnityEngine.Application.dataPath, "../Temp/ai/authoring-ui-profile.json"), report);
            File.WriteAllText(ResultPath, report); EndAuthoringProfile();
        }
        static void EndAuthoringProfile()
        {
            EditorApplication.update -= PollAuthoringProfile; AssemblyReloadEvents.beforeAssemblyReload -= EndAuthoringProfile;
            if (_authoringTimings != null) foreach (var timing in _authoringTimings) timing.recorder.Dispose();
            _authoringTimings = null;
        }
    }
}
#endif
