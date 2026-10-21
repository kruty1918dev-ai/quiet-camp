// Copy only into an explicitly authorized QA Editor project, never a player asset.
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

[InitializeOnLoad]
static class PrivacyVerificationDriver
{
    static readonly string Control = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "../Temp/PrivacyVerification"));
    static TestRunnerApi _api;
    static bool _running;
    static PrivacyVerificationDriver() { EditorApplication.update += Tick; }
    static void Tick()
    {
        if (_running || EditorApplication.isCompiling || EditorApplication.isUpdating
            || EditorApplication.isPlayingOrWillChangePlaymode || BuildPipeline.isBuildingPlayer) return;
        var request = Path.Combine(Control, "request.json");
        if (!File.Exists(request)) return;
        File.Delete(request);
        if (PlayerSettings.productName != "Quiet Camp QA" || EditorSceneManager.GetActiveScene().isDirty)
        {
            File.WriteAllText(Path.Combine(Control, "refused.txt"), "Requires the authorized QA product and a saved scene.");
            return;
        }
        _api = ScriptableObject.CreateInstance<TestRunnerApi>();
        _api.RegisterCallbacks(new Results());
        _running = true;
        try
        {
            _api.Execute(new ExecutionSettings(new Filter { testMode = TestMode.EditMode,
                testNames = new[] {
                    "QuietCamp.Tests.Editor.AndroidPrivacyBuildTests",
                    "QuietCamp.Tests.Editor.OptionalPrivacyTests",
                    "QuietCamp.Tests.Editor.ComfortAndRewardsTests",
                    "QuietCamp.Tests.Editor.CrashDiagnosticsTests",
                    "QuietCamp.Tests.Editor.LegalConfigurationTests",
                    "QuietCamp.Tests.Editor.PrivacyAcknowledgementTests",
                    "QuietCamp.Tests.Editor.AdServiceStubTests" } }));
        }
        catch (Exception exception)
        { _running = false; File.WriteAllText(Path.Combine(Control, "failed.txt"), exception.ToString()); }
    }
    sealed class Results : ICallbacks
    {
        public void RunStarted(ITestAdaptor test) { }
        public void TestStarted(ITestAdaptor test) { }
        public void TestFinished(ITestResultAdaptor result) { }
        public void RunFinished(ITestResultAdaptor result)
        {
            TestRunnerApi.SaveResultToFile(result, Path.Combine(Control, "results.xml"));
            File.WriteAllText(Path.Combine(Control, "finished.txt"), result.ResultState + "\n" + result.Message);
            _running = false;
        }
    }
}
