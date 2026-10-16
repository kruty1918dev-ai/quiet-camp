using System;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;

namespace QuietCamp.Editor.LevelAuthoring.WorldOps
{
    [InitializeOnLoad] public static class WorldOpsBridge
    {
        static string Inbox=>Path.Combine(WorldOpsService.EditorProject,"Temp/ai/qc-world-request.json");
        static string Outbox=>Path.Combine(WorldOpsService.EditorProject,"Temp/ai/qc-world-result.json");
        static WorldOpsBridge(){EditorApplication.update+=Poll;}
        static void Poll()
        {
            if(EditorApplication.isCompiling||EditorApplication.isUpdating||!File.Exists(Inbox))return;
            string requestId=null;
            try
            {
                var request=JObject.Parse(File.ReadAllText(Inbox));requestId=(string)request["requestId"];
                if(string.IsNullOrWhiteSpace(requestId))throw new ArgumentException("A requestId is required.");
                var command=(string)request["command"];
                if(command=="run")
                {
                    if(SessionState.GetBool("QuietCamp.NativeTestsRunning",false)||File.Exists(Path.Combine(WorldOpsService.EditorProject,"Temp/ai/qc007-art-request.json")))throw new InvalidOperationException("Another native verification job is active.");
                    WorldOpsService.Start(WorldOpsStorage.Read<WorldRecipe>((string)request["recipe"]),(string)request["repositoryProject"]);
                }
                else if(command=="resume")WorldOpsService.Resume((string)request["runId"]);
                else if(command=="cancel")WorldOpsService.Cancel();
                else if(command=="review")WorldOpsService.Submit(WorldOpsStorage.Read<ReviewVerdict>((string)request["verdict"]));
                else if(command=="rollback")WorldOpsService.Rollback((string)request["levelId"]);
                else if(command=="apply")
                {var run=WorldOpsService.Current??throw new InvalidOperationException("No run.");foreach(var item in run.levels.FindAll(l=>l.status=="ready"))WorldOpsService.Apply(item);}
                else if(command!="status")throw new ArgumentException("Unknown WorldOps command.");
                var current=WorldOpsService.Current;
                WorldOpsStorage.Json(Outbox,new{requestId,status="ok",runId=current?.id,path=current?.directory,runStatus=current?.status,busy=WorldOpsService.Busy});
            }
            catch(Exception e){WorldOpsStorage.Json(Outbox,new{requestId,status="error",error=e.Message});}
            finally{File.Delete(Inbox);}
        }
    }
}
