using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using QuietCamp.Editor.LevelAuthoring.WorldOps;
using UnityEditor;
using UnityEngine;

namespace QuietCamp.Editor.LevelAuthoring
{
    public sealed partial class LevelAuthoringWindow
    {
        bool _worldOpsOpen;
        TextAsset _worldRecipe;
        string _worldRepository;
        public static bool WorldOpsConflict(string id)=>Resources.FindObjectsOfTypeAll<LevelAuthoringWindow>().Any(w=>w._dirty&&w.Document?.sourceLevelId==id);
        public static void WorldOpsApplied(string id,string path)
        {
            foreach(var window in Resources.FindObjectsOfTypeAll<LevelAuthoringWindow>().Where(w=>w.Document?.sourceLevelId==id))
            {
                if(window._dirty)continue;
                window.SetDocument(LevelAuthoringDocument.Open(path));window._path=path;window._dirty=false;window._gallery=null;window.Repaint();
            }
        }
        void DrawWorldOps()
        {
            _worldOpsOpen=EditorGUILayout.Foldout(_worldOpsOpen,"WorldOps — пакетні правки",true);
            if(!_worldOpsOpen)return;
            using(new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                var run=WorldOpsService.Current;
                _worldRecipe=(TextAsset)EditorGUILayout.ObjectField("Рецепт",_worldRecipe,typeof(TextAsset),false);
                if(string.IsNullOrEmpty(_worldRepository))_worldRepository=EditorPrefs.GetString("QuietCamp.WorldOps.Repository",WorldOpsService.EditorProject);
                using(new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("Проєкт у репозиторії",_worldRepository);
                    if(GUILayout.Button("…",GUILayout.Width(25)))
                    {var path=EditorUtility.OpenFolderPanel("QuietCamp у репозиторії",_worldRepository,"");if(!string.IsNullOrEmpty(path)){_worldRepository=path;EditorPrefs.SetString("QuietCamp.WorldOps.Repository",path);}}
                }
                using(new EditorGUI.DisabledScope(_worldRecipe==null||WorldOpsService.Busy||EditorApplication.isPlayingOrWillChangePlaymode))
                    if(GUILayout.Button("Запустити"))Try(()=>WorldOpsService.Start(JsonConvert.DeserializeObject<WorldRecipe>(_worldRecipe.text),_worldRepository));
                if(run==null)return;
                int done=run.levels.Count(x=>x.status=="applied"||x.status=="completed");
                EditorGUILayout.LabelField($"{run.status}: {done}/{run.levels.Count} · {_worldRecipe?.name}");
                if(WorldOpsService.Busy&&GUILayout.Button("Скасувати"))Try(WorldOpsService.Cancel);
                if(!WorldOpsService.Busy&&GUILayout.Button("Продовжити"))Try(()=>WorldOpsService.Resume());
                if(GUILayout.Button("Відкрити кадри й звіт"))EditorUtility.RevealInFinder(run.directory);
                foreach(var item in run.levels.Where(x=>x.status=="exception"||x.status=="conflict"||x.status=="review"))
                    EditorGUILayout.HelpBox(item.levelId+" · "+item.status+"\n"+(item.error??string.Join("\n",item.findings.Take(3).Select(f=>f.code+": "+f.message))),item.status=="review"?MessageType.Info:MessageType.Warning);
            }
        }
    }
}
