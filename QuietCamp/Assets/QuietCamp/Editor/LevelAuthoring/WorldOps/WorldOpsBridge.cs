using System;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;

namespace QuietCamp.Editor.LevelAuthoring.WorldOps
{
    [InitializeOnLoad] public static class WorldOpsBridge
    {
        // Optional Editor-only capture extension; no dependency on marketing in the core assembly.
        public static Func<JObject,object> TeaserCommand;
        public static Func<bool> TeaserBusy;
        static string Inbox=>Path.Combine(WorldOpsService.EditorProject,"Temp/ai/qc-world-request.json");
        static string Outbox=>Path.Combine(WorldOpsService.EditorProject,"Temp/ai/qc-world-result.json");
        static WorldOpsBridge(){EditorApplication.update+=Poll;}
        public static void GuardLegacyMutation(string mode)
        {
            if(TeaserBusy?.Invoke()==true&&mode!="input-status"&&mode!="authoring-status")throw new InvalidOperationException("Wait for the WorldOps teaser capture to finish.");
            switch(mode)
            {
                case "forest-regrowth":case "object-overgrowth":case "opening-details":
                case "opening-compose":case "opening-apply":case "crash-compose":
                case "campaign-regenerate":case "campaign-weather-upgrade":case "season-variant":
                    throw new InvalidOperationException("Legacy world mutation '"+mode+"' is disabled. Use tools/qc_world.py run <recipe.json> and its hash-bound review. Greenery recipe: Assets/QuietCamp/Authoring/WorldOps/greenery-all.json. Extend a WorldOps operation if needed; do not write generated drafts directly.");
            }
        }
        static void Poll()
        {
            if(EditorApplication.isCompiling||EditorApplication.isUpdating||!File.Exists(Inbox))return;
            string requestId=null;
            try
            {
                var request=JObject.Parse(File.ReadAllText(Inbox));requestId=(string)request["requestId"];
                if(string.IsNullOrWhiteSpace(requestId))throw new ArgumentException("A requestId is required.");
                var command=(string)request["command"];
                if(TeaserBusy?.Invoke()==true&&command!="teaser"&&command!="status")throw new InvalidOperationException("Wait for the WorldOps teaser capture to finish.");
                if(command=="teaser")
                {
                    if(WorldOpsService.Busy)throw new InvalidOperationException("Wait for the current WorldOps run.");
                    if(TeaserCommand==null)throw new InvalidOperationException("The marketing capture extension is not loaded.");
                    var result=TeaserCommand(request);
                    WorldOpsStorage.Json(Outbox,new{requestId,status="ok",result});return;
                }
                if(EditorUtility.scriptCompilationFailed&&command!="status"&&command!="cancel")throw new InvalidOperationException("Unity compilation failed; loaded assemblies cannot certify the current sources.");
                if(command=="run")
                {
                    if(SessionState.GetBool("QuietCamp.NativeTestsRunning",false)||File.Exists(Path.Combine(WorldOpsService.EditorProject,"Temp/ai/qc007-art-request.json")))throw new InvalidOperationException("Another native verification job is active.");
                    WorldOpsService.Start(WorldOpsStorage.Read<WorldRecipe>((string)request["recipe"]),(string)request["repositoryProject"],request["agentDelivery"]?.Value<bool>()??false,request["preexistingDirtyPaths"]?.ToObject<string[]>());
                }
                else if(command=="resume")WorldOpsService.Resume((string)request["runId"]);
                else if(command=="cancel")WorldOpsService.Cancel();
                else if(command=="review")WorldOpsService.Submit(WorldOpsStorage.Read<ReviewVerdict>((string)request["verdict"]));
                else if(command=="rollback")WorldOpsService.Rollback((string)request["levelId"]);
                else if(command=="delivery"||command=="verify-publication")WorldOpsService.PrepareDelivery(command=="verify-publication");
                else if(command=="apply")
                {
                    if(WorldOpsService.Busy||EditorApplication.isPlayingOrWillChangePlaymode||SessionState.GetBool("QuietCamp.NativeTestsRunning",false))throw new InvalidOperationException("Wait for the current native operation before applying.");
                    var run=WorldOpsService.Current??throw new InvalidOperationException("No run.");foreach(var item in run.levels.FindAll(l=>l.status=="ready"))WorldOpsService.Apply(item);
                }
                else if(command!="status")throw new ArgumentException("Unknown WorldOps command.");
                var current=WorldOpsService.Current;
                WorldOpsStorage.Json(Outbox,new{requestId,status="ok",runId=current?.id,path=current?.directory,runStatus=current?.status,busy=WorldOpsService.Busy});
            }
            catch(Exception e){WorldOpsStorage.Json(Outbox,new{requestId,status="error",error=e.Message});}
            finally{File.Delete(Inbox);}
        }
    }
}
