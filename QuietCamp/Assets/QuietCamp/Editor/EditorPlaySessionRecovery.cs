#if UNITY_EDITOR
using System;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using QuietCamp.Infrastructure;
using QuietCamp.Presentation;
using QuietCamp.Presentation.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace QuietCamp.Editor
{
    /// <summary>Re-enter Boot after hot reload instead of leaving visible scenes with lost services.
    /// Production saves use their existing save pipeline; RAM previews never write a player slot.</summary>
    [InitializeOnLoad]
    public static class EditorPlaySessionRecovery
    {
        const string StateKey="QuietCamp.PlayReload.State", PhaseKey="QuietCamp.PlayReload.Phase";
        static double _checkAfter;
        public sealed class Snapshot
        {
            public bool memoryOnly,preview;
            public string scene,level,startScene,previewRequest,previewStartScene;
            public JObject data;
        }
        static EditorPlaySessionRecovery()
        {
            AssemblyReloadEvents.beforeAssemblyReload+=BeforeReload;
            EditorApplication.playModeStateChanged+=OnPlayState;
            EditorApplication.update+=Update;
            _checkAfter=EditorApplication.timeSinceStartup+1;
            if(SessionState.GetString(PhaseKey,"")=="starting")InstallMemoryFactory();
        }
        public static string Capture(SaveAdapter save,string scene,string level,bool preview,string startScene)
            =>JsonConvert.SerializeObject(new Snapshot{memoryOnly=save.MemoryOnly,scene=scene,level=level,preview=preview,startScene=startScene,
                data=JObject.FromObject(new{save.Session,save.Progress,save.Settings,save.Album,save.Economy,save.Entitlements,save.Purchases,save.Memories})});
        public static SaveAdapter RestoreMemory(string json)
        {
            var state=JsonConvert.DeserializeObject<Snapshot>(json);
            if(state?.memoryOnly!=true||state.data==null)throw new InvalidOperationException("Only a captured RAM save can be restored in memory.");
            var save=new SaveAdapter(memoryOnly:true);
            JsonConvert.PopulateObject(state.data.ToString(),save);return save;
        }
        static bool Controlled=>SessionState.GetBool("QuietCamp.NativeTestsRunning",false)
            ||LevelAuthoring.WorldOps.WorldOpsService.Busy||EditorAuthoringPreview.ControlledCapture;
        static void BeforeReload()
        {
            if(!EditorApplication.isPlaying||Controlled)return;
            var services=QuietCampBootstrap.ServicesRef;if(services==null)return;
            if(!services.Save.Save())
            {
                Debug.LogError("[QuietCamp] Could not preserve the session before script reload; automatic restart is disabled.");
                SessionState.SetString(PhaseKey,"save-failed");return;
            }
            SessionState.SetString(StateKey,Capture(services.Save,SceneManager.GetActiveScene().name,
                CampSceneHost.Current?.Session?.Level.id,EditorAuthoringPreview.Active,AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene)));
            SessionState.SetString(PhaseKey,"captured");
        }
        static void Update()
        {
            if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorUtility.scriptCompilationFailed||Controlled
                ||EditorApplication.timeSinceStartup<_checkAfter)return;
            var phase=SessionState.GetString(PhaseKey,"");
            if(phase=="stopping"&&!EditorApplication.isPlayingOrWillChangePlaymode){Restart();return;}
            if(phase=="starting")
            {
                var bootstrap=UnityEngine.Object.FindFirstObjectByType<QuietCampBootstrap>();
                var services=QuietCampBootstrap.ServicesRef;
                if(!EditorApplication.isPlaying||bootstrap?.StartupReady!=true||services==null)return;
                var state=JsonConvert.DeserializeObject<Snapshot>(SessionState.GetString(StateKey,""));
                EditorSceneManager.playModeStartScene=string.IsNullOrEmpty(state.startScene)?null:AssetDatabase.LoadAssetAtPath<SceneAsset>(state.startScene);
                SessionState.SetString(PhaseKey,"");SessionState.EraseString(StateKey);
                if(state.scene=="Camp"&&!string.IsNullOrEmpty(state.level)&&services.CanStart(state.level)
                    &&CampSceneHost.Current?.Session?.Level.id!=state.level)
                {services.PendingLevelId=state.level;SceneManager.LoadSceneAsync("Camp");}
                Debug.Log("[QuietCamp] Editor play session restored through Boot after script reload.");
                return;
            }
            if(phase=="save-failed"||phase=="stopping"||!EditorApplication.isPlaying||QuietCampBootstrap.ServicesRef!=null)return;
            var scene=SceneManager.GetActiveScene().name;
            if(scene!="Camp"&&scene!="MainMenu")return;
            if(UnityEngine.Object.FindFirstObjectByType<QuietCampBootstrap>()==null)return;
            if(phase!="captured")
            {
                // Recover an already orphaned session from before this hook was installed.
                SessionState.SetString(StateKey,JsonConvert.SerializeObject(new Snapshot{scene=scene,preview=EditorAuthoringPreview.Active,memoryOnly=EditorAuthoringPreview.Active,
                    startScene=AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene)}));
            }
            var pending=JsonConvert.DeserializeObject<Snapshot>(SessionState.GetString(StateKey,""));
            if(pending.preview&&File.Exists(EditorAuthoringPreview.RequestPath))
            {
                pending.previewRequest=File.ReadAllText(EditorAuthoringPreview.RequestPath);
                pending.previewStartScene=SessionState.GetString("QuietCamp.Authoring.PreviousStartScene","");
                SessionState.SetString(StateKey,JsonConvert.SerializeObject(pending));
            }
            SessionState.SetString(PhaseKey,"stopping");EditorApplication.ExitPlaymode();
        }
        static void OnPlayState(PlayModeStateChange state)
        {
            var phase=SessionState.GetString(PhaseKey,"");
            if(state==PlayModeStateChange.EnteredEditMode&&(phase=="starting"||phase=="captured"||phase=="save-failed"))
            {SessionState.SetString(PhaseKey,"");SessionState.EraseString(StateKey);}
        }
        static void InstallMemoryFactory()
        {
            string json=SessionState.GetString(StateKey,"");var snapshot=JsonConvert.DeserializeObject<Snapshot>(json);
            if(snapshot?.memoryOnly!=true||snapshot.data==null)return;
            QuietCampBootstrap.EditorSaveFactory=()=>{QuietCampBootstrap.EditorSaveFactory=null;return RestoreMemory(json);};
        }
        static void Restart()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode||EditorUtility.scriptCompilationFailed)return;
            var state=JsonConvert.DeserializeObject<Snapshot>(SessionState.GetString(StateKey,""));if(state==null)return;
            SessionState.SetString(PhaseKey,"starting");
            if(state.preview&&!string.IsNullOrEmpty(state.previewRequest))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(EditorAuthoringPreview.RequestPath));
                File.WriteAllText(EditorAuthoringPreview.RequestPath,state.previewRequest);
                EditorAuthoringPreview.Reset();
                SessionState.SetString("QuietCamp.Authoring.PreviousStartScene",state.previewStartScene??"");
            }
            EditorAuthoringPreview.Enable(state.preview);
            // Keep the existing preview owner without replaying its initial blank-session setup.
            if(state.preview)SessionState.SetString("QuietCamp.Authoring.PlayStage",state.scene=="Camp"?"camp":"menu");
            InstallMemoryFactory();
            EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/QuietCamp/Scenes/Boot.unity");
            EditorApplication.EnterPlaymode();
        }
    }
}
#endif
