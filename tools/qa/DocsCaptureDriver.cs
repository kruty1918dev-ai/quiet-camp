// Temporary Editor fixture: copy into Assets/Editor only for an authorized documentation capture.
// Reuses the already open Editor. No player builds, process control, SDK/ADB or shared prefs changes.
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

[InitializeOnLoad]
static class DocsCaptureDriver
{
    const string Key = "QcDocsCapture.";
    static readonly string Control = Path.Combine(Path.GetTempPath(), "quietcamp-docs-capture-control");
    static TestRunnerApi api;
    [Serializable] sealed class Request { public string productName; public string context; }
    static DocsCaptureDriver()
    {
        EditorApplication.update += Tick;
        EditorApplication.playModeStateChanged += state => { if (state == PlayModeStateChange.ExitedPlayMode) Restore(); };
        EditorApplication.delayCall += Register;
    }
    static void Register()
    {
        api = ScriptableObject.CreateInstance<TestRunnerApi>();
        api.RegisterCallbacks(new Results());
    }
    static void Tick()
    {
        if (SessionState.GetBool(Key + "Finished", false) && !EditorApplication.isPlayingOrWillChangePlaymode) Restore();
        var path = Path.Combine(Control, "request.json");
        if (!File.Exists(path) || EditorApplication.isCompiling || EditorApplication.isUpdating || api == null) return;
        if (EditorApplication.isPlayingOrWillChangePlaymode || SessionState.GetBool(Key + "Active", false)) return;
        if (EditorSceneManager.GetActiveScene().isDirty)
        { File.Move(path, Path.Combine(Control, "refused-unsaved-scene.json")); Debug.LogError("[QC-DOCS] Preserve the unsaved scene before capture."); return; }
        var request = JsonUtility.FromJson<Request>(File.ReadAllText(path));
        if (request == null || string.IsNullOrEmpty(request.productName) || !request.productName.StartsWith("QuietCampDocsQA"))
            throw new InvalidOperationException("Documentation capture needs a distinct DocsQA product.");
        SessionState.SetString(Key + "OriginalProduct", PlayerSettings.productName);
        SessionState.SetString(Key + "Context", request.context ?? "{}");
        SessionState.SetBool(Key + "Active", true);
        SessionState.SetBool(Key + "Finished", false);
        File.Move(path, Path.Combine(Control, "started.json"));
        PlayerSettings.productName = request.productName;
        Debug.Log("[QC-DOCS] Starting rendered capture with isolated QA saves; existing Editor remains open.");
        api.Execute(new ExecutionSettings(new Filter { testMode = TestMode.PlayMode,
            testNames = new[] { "QuietCamp.Tests.GithubDocumentationCaptureTests.CaptureMenusPanelsAndSeasonalCamps" } }));
    }
    static void Restore()
    {
        // Never restore while a QA scene can still save during OnDestroy/OnApplicationQuit.
        if (!SessionState.GetBool(Key + "Active", false) || EditorApplication.isPlayingOrWillChangePlaymode) return;
        PlayerSettings.productName = SessionState.GetString(Key + "OriginalProduct", PlayerSettings.productName);
        SessionState.SetBool(Key + "Active", false);
        SessionState.SetBool(Key + "Finished", false);
        File.WriteAllText(Path.Combine(Control, "restored.txt"), "Original product identity restored after leaving Play Mode.");
        Debug.Log("[QC-DOCS] Original product identity restored; Editor remains open.");
    }
    sealed class Results : ICallbacks
    {
        public void RunStarted(ITestAdaptor test) { }
        public void TestStarted(ITestAdaptor test) { }
        public void TestFinished(ITestResultAdaptor result) { if (!result.Test.IsSuite) Debug.Log("[QC-DOCS] Test result: " + result.ResultState + " " + result.Message); }
        public void RunFinished(ITestResultAdaptor result)
        {
            TestRunnerApi.SaveResultToFile(result, Path.Combine(Control, "results.xml"));
            File.WriteAllText(Path.Combine(Control, "finished.txt"), result.ResultState + "\n" + result.Message);
            SessionState.SetBool(Key + "Finished", true);
            EditorApplication.delayCall += Restore;
        }
    }
}
