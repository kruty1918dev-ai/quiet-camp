// Temporary Editor fixture: copy into Assets/Editor only for an authorized performance check.
// Reuses the already open Editor. No player builds, process control, SDK/ADB or shared prefs changes.
using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

[InitializeOnLoad]
static class PerformanceAuditDriver
{
    const string Key = "QcPerf.";
    static readonly string Control = Path.Combine(Path.GetTempPath(), "quietcamp-performance-control");
    static TestRunnerApi api;
    [Serializable] sealed class Request { public string productName; public string context; public string suite; }
    static PerformanceAuditDriver()
    {
        EditorApplication.update += Tick;
        EditorApplication.delayCall += Register;
    }
    static void Register()
    {
        api = ScriptableObject.CreateInstance<TestRunnerApi>();
        api.RegisterCallbacks(new Results());
        Directory.CreateDirectory(Control);
        File.WriteAllText(Path.Combine(Control, "ready.txt"), "optimization driver v1; native assemblies imported");
    }
    static void Tick()
    {
        if (SessionState.GetBool(Key + "Finished", false) && !EditorApplication.isPlayingOrWillChangePlaymode
            && !EditorApplication.isCompiling && !EditorApplication.isUpdating
            && EditorApplication.timeSinceStartup > SessionState.GetFloat(Key + "RestoreAfter", float.MaxValue)) Restore();
        var path = Path.Combine(Control, "request.json");
        if (!File.Exists(path) || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        // PlayMode cleanup may destroy the transient Editor API object. Recreate it for a later request.
        if (api == null) Register();
        if (EditorApplication.isPlayingOrWillChangePlaymode || SessionState.GetBool(Key + "Active", false)) return;
        if (EditorSceneManager.GetActiveScene().isDirty)
        { File.Move(path, Path.Combine(Control, "refused-unsaved-scene.json")); Debug.LogError("[QC-PERF] Preserve the unsaved scene before the audit."); return; }
        var request = JsonUtility.FromJson<Request>(File.ReadAllText(path));
        if (request == null || string.IsNullOrEmpty(request.productName) || !request.productName.StartsWith("QuietCampPerfQA"))
            throw new InvalidOperationException("Performance checks need a distinct QuietCampPerfQA product.");
        if (!string.IsNullOrEmpty(request.suite) && request.suite != "performance" && request.suite != "foliage" && request.suite != "roadmap" && request.suite != "optimization-editor" && request.suite != "optimization-regressions")
            throw new InvalidOperationException("Only the performance matrix, roadmap audit and foliage regression suite are allowed.");
        SessionState.SetString(Key + "OriginalProduct", PlayerSettings.productName);
        var settingsPath = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "../ProjectSettings/ProjectSettings.asset"));
        SessionState.SetString(Key + "OriginalProductLine", File.ReadAllLines(settingsPath).First(line => line.StartsWith("  productName:")));
        SessionState.SetString(Key + "QaProduct", request.productName);
        SessionState.SetString(Key + "Context", request.context ?? "{}");
        SessionState.SetBool(Key + "Active", true);
        SessionState.SetBool(Key + "Finished", false);
        File.Move(path, Path.Combine(Control, "started.json"));
        PlayerSettings.productName = request.productName;
        Debug.Log("[QC-PERF] Starting native tests with isolated QA saves; existing Editor remains open.");
        api.Execute(new ExecutionSettings(new Filter { testMode = request.suite == "optimization-editor" ? TestMode.EditMode : TestMode.PlayMode,
            testNames = request.suite == "optimization-editor" ? new[] { "QuietCamp.Tests.PerformanceCacheTests", "QuietCamp.Tests.LeafCurtainGraphicTests" }
                : request.suite == "optimization-regressions" ? new[] { "QuietCamp.Tests.OptimizationPlayModeTests", "QuietCamp.Tests.FoliageDivePlayModeTests", "QuietCamp.Tests.ForestCoveragePlayModeTests" }
                : new[] { request.suite == "foliage" ? "QuietCamp.Tests.FoliageDivePlayModeTests"
                : request.suite == "roadmap" ? "QuietCamp.Tests.PerformanceAuditPlayModeTests.RunRoadmapPerformance"
                : "QuietCamp.Tests.PerformanceAuditPlayModeTests.RunPerformanceMap" } }));
    }
    static void Restore()
    {
        // Never restore while a QA scene can still save during OnDestroy/OnApplicationQuit.
        if (!SessionState.GetBool(Key + "Active", false) || EditorApplication.isPlayingOrWillChangePlaymode) return;
        PlayerSettings.productName = SessionState.GetString(Key + "OriginalProduct", PlayerSettings.productName);
        var settingsPath = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "../ProjectSettings/ProjectSettings.asset"));
        var originalLine = SessionState.GetString(Key + "OriginalProductLine", "");
        var qaName = SessionState.GetString(Key + "QaProduct", "");
        if (!string.IsNullOrEmpty(originalLine) && !string.IsNullOrEmpty(qaName))
        {
            // Unity can leave the serialized QA name on disk after restoring the in-memory property.
            // Restore only our own product-name line; preserve every other setting and pending asset.
            var content = File.ReadAllText(settingsPath);
            var updated = Regex.Replace(content, "(?m)^  productName: " + Regex.Escape(qaName) + "\\r?$",
                match => originalLine + (match.Value.EndsWith("\r") ? "\r" : ""));
            if (updated != content) File.WriteAllText(settingsPath, updated);
        }
        SessionState.SetBool(Key + "Active", false);
        SessionState.SetBool(Key + "Finished", false);
        File.WriteAllText(Path.Combine(Control, "restored.txt"), "Original product identity restored after leaving Play Mode.");
        Debug.Log("[QC-PERF] Original product identity restored; Editor remains open.");
    }
    sealed class Results : ICallbacks
    {
        public void RunStarted(ITestAdaptor test) { }
        public void TestStarted(ITestAdaptor test) { }
        public void TestFinished(ITestResultAdaptor result) { if (!result.Test.IsSuite) Debug.Log("[QC-PERF] Test result: " + result.ResultState + " " + result.Message); }
        public void RunFinished(ITestResultAdaptor result)
        {
            TestRunnerApi.SaveResultToFile(result, Path.Combine(Control, "results.xml"));
            File.WriteAllText(Path.Combine(Control, "finished.txt"), result.ResultState + "\n" + result.Message);
            SessionState.SetBool(Key + "Finished", true);
            SessionState.SetFloat(Key + "RestoreAfter", (float)EditorApplication.timeSinceStartup + 3f);
        }
    }
}
