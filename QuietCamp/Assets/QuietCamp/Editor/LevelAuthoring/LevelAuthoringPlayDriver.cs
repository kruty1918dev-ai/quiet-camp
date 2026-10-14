using System;
using System.IO;
using Newtonsoft.Json;
using QuietCamp.Infrastructure;
using QuietCamp.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace QuietCamp.Editor.LevelAuthoring
{
    [InitializeOnLoad]
    public static class LevelAuthoringPlayDriver
    {
        const string PreviousScene = "QuietCamp.Authoring.PreviousStartScene";
        const string Stage = "QuietCamp.Authoring.PlayStage";
        static LevelAuthoringPlayDriver()
        {
            EditorApplication.playModeStateChanged += Changed;
            EditorApplication.update += Tick;
        }
        public static void Start(LevelAuthoringDocument document, bool campaign = false)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Play Mode is already active.");
            var errors = document.Validate(true); if (errors.Count > 0) throw new InvalidOperationException(string.Join("\n", errors));
            var bootPath = AssetDatabase.FindAssets("Boot t:Scene").SelectPath("Boot");
            if (bootPath == null) throw new FileNotFoundException("Boot scene not found.");
            // The workspace is reconstructed from the JSON snapshot after domain reload.
            var window = LevelAuthoringWindow.OpenWindow(); window.PreparePlay();
            Directory.CreateDirectory(Path.GetDirectoryName(EditorAuthoringPreview.RequestPath));
            File.WriteAllText(EditorAuthoringPreview.RequestPath, JsonConvert.SerializeObject(new EditorAuthoringPreview.Request { level = document.level, art = document.art, campaign = campaign }));
            EditorAuthoringPreview.Reset();
            EditorAuthoringPreview.Enable(true);
            SessionState.SetString(PreviousScene, AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(bootPath);
            SessionState.SetString(Stage, "boot"); EditorApplication.EnterPlaymode();
        }
        static string SelectPath(this string[] guids, string name)
        {
            foreach (var guid in guids) { string path = AssetDatabase.GUIDToAssetPath(guid); if (Path.GetFileNameWithoutExtension(path) == name) return path; } return null;
        }
        static void Tick()
        {
            if (!EditorApplication.isPlaying || !EditorAuthoringPreview.Active || SessionState.GetString(Stage, "") != "boot") return;
            var bootstrap = UnityEngine.Object.FindAnyObjectByType<QuietCampBootstrap>();
            if (bootstrap == null || !bootstrap.StartupReady) return;
            var services = QuietCampBootstrap.ServicesRef;
            if (services == null || !services.Save.MemoryOnly) throw new InvalidOperationException("Authoring preview requires a memory-only save.");
            services.Save.Session = new QuietCamp.Application.SessionSaveData();
            services.PendingLevelId = EditorAuthoringPreview.Current.level.id;
            SessionState.SetString(Stage, "camp"); SceneManager.LoadSceneAsync("Camp");
        }
        static void Changed(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode || string.IsNullOrEmpty(SessionState.GetString(Stage, ""))) return;
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(SessionState.GetString(PreviousScene, ""));
            SessionState.EraseString(Stage); SessionState.EraseString(PreviousScene);
            if (File.Exists(EditorAuthoringPreview.RequestPath)) File.Delete(EditorAuthoringPreview.RequestPath);
            EditorAuthoringPreview.Reset();
            EditorAuthoringPreview.Enable(false);
            LevelAuthoringWindow.OpenWindow().RestoreAfterPlay();
        }
    }
}
