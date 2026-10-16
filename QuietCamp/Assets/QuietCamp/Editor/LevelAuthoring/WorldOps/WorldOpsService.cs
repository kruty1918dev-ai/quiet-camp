using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using QuietCamp.Domain;
using QuietCamp.Infrastructure;
using UnityEditor;
using UnityEngine;

namespace QuietCamp.Editor.LevelAuthoring.WorldOps
{
    [InitializeOnLoad]
    public static class WorldOpsService
    {
        public const string Version="1";
        const string ActiveKey="QuietCamp.WorldOps.Active";
        const string Content="Assets/QuietCamp/Resources/QuietCamp";
        const string Drafts="Assets/QuietCamp/Authoring/CampaignLevels";
        static WorldRun _run;
        static bool _ticking;
        public static event Action Changed;
        public static string EditorProject=>Path.GetDirectoryName(UnityEngine.Application.dataPath);
        public static string Home=>Path.Combine(EditorProject,"UserSettings/WorldOps");
        public static WorldRun Current=>_run;
        public static bool Busy=>_run!=null&&new[]{"preparing","running","capturing","applying","cancelling"}.Contains(_run.status);
        static WorldOpsService(){EditorApplication.update+=Tick;EditorApplication.delayCall+=Restore;}
        static void Restore()
        {
            var id=SessionState.GetString(ActiveKey,"");if(string.IsNullOrEmpty(id))return;
            var path=WorldOpsStorage.SafeChild(Home,id+"/run.json");
            if(File.Exists(path))_run=WorldOpsStorage.Read<WorldRun>(path);
        }
        static string LevelFile(string root,string id)=>Path.Combine(root,Content,id.StartsWith("gen:",StringComparison.Ordinal)?"GeneratedLevels":"Levels",id.Replace(':','_')+".json");
        static string ArtFile(string root,string id)=>Path.Combine(root,Content,"ArtCompositions",id.Replace(':','_')+".json");
        static string DraftFile(string root,string id)=>Path.Combine(root,Drafts,id.Replace(':','_')+".draft.json");
        public static WorldRun Start(WorldRecipe recipe,string repositoryProject)
        {
            if(Busy||EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Wait for the current Editor operation; a second Unity instance is not used.");
            WorldOpsOperations.Validate(recipe);repositoryProject=Path.GetFullPath(repositoryProject);
            if(!File.Exists(Path.Combine(repositoryProject,Content,"campaign.json")))throw new DirectoryNotFoundException("QuietCamp campaign not found in "+repositoryProject);
            var cards=LevelAuthoringCampaign.ReadCards(Path.Combine(repositoryProject,Content));
            var selected=cards.Where(c=>(recipe.levelIds.Length==0&&recipe.orders.Length==0)||recipe.levelIds.Contains(c.id)||recipe.orders.Contains(c.number)).ToArray();
            if(selected.Length==0||recipe.levelIds.Except(cards.Select(c=>c.id)).Any()||recipe.orders.Any(n=>n<1||n>cards.Length))throw new ArgumentException("Unknown campaign level selection.");
            var id=DateTime.UtcNow.ToString("yyyyMMddTHHmmssfff")+"-"+Guid.NewGuid().ToString("N").Substring(0,6);
            _run=new WorldRun{id=id,directory=WorldOpsStorage.SafeChild(Home,id),recipe=LevelAuthoringDocument.Copy(recipe),repositoryProject=repositoryProject,editorProject=EditorProject,createdUtc=DateTime.UtcNow.ToString("O"),dependencies=Dependencies()};
            _run.sourceManifestHash=WorldOpsStorage.Digest(_run.dependencies);
            foreach(var card in selected)
            {
                var item=new WorldLevelRun{levelId=card.id,groupId=card.id};
                foreach(var root in new[]{repositoryProject,EditorProject}.Distinct(StringComparer.OrdinalIgnoreCase))
                    foreach(var path in new[]{LevelFile(root,card.id),ArtFile(root,card.id),DraftFile(root,card.id),Path.Combine(root,Content,"campaign.json")})item.sources[path]=WorldOpsStorage.FileHash(path);
                item.inputHash=WorldOpsStorage.Digest(item.sources.OrderBy(p=>p.Key).ToArray());
                item.cacheKey=WorldOpsStorage.Digest(new{Version,recipe,item.inputHash,_run.sourceManifestHash});
                _run.levels.Add(item);
            }
            Directory.CreateDirectory(_run.directory);SessionState.SetString(ActiveKey,id);Save();return _run;
        }
        public static Dictionary<string,string> Dependencies()
        {
            var result=new Dictionary<string,string>{{"@engine",UnityEngine.Application.unityVersion},{"@worldops",Version},{"@target",EditorUserBuildSettings.activeBuildTarget.ToString()}};
            foreach(var path in AssetDatabase.GetAllAssetPaths().Where(p=>p.StartsWith("Assets/QuietCamp/",StringComparison.Ordinal)&&new[]{".cs",".shader",".hlsl",".mat",".prefab",".asmdef",".compute"}.Contains(Path.GetExtension(p))))
                result[path]=AssetDatabase.GetAssetDependencyHash(path).ToString();
            foreach(var file in new[]{"Packages/manifest.json","Packages/packages-lock.json","ProjectSettings/GraphicsSettings.asset","ProjectSettings/QualitySettings.asset"})result[file]=WorldOpsStorage.FileHash(Path.Combine(EditorProject,file));
            foreach(var file in Directory.GetFiles(Path.Combine(EditorProject,"Library/ScriptAssemblies"),"*.dll"))result["assembly/"+Path.GetFileName(file)]=WorldOpsStorage.FileHash(file);
            return result;
        }
        static void Fresh(WorldLevelRun item)
        {
            WorldOpsStorage.Check(item.sources);
            if(WorldOpsStorage.Digest(Dependencies())!=_run.sourceManifestHash)throw new IOException("Unity/assets/assemblies changed; prepare fresh evidence.");
            if(WorldOpsStorage.FileHash(item.stagePath)!=item.documentHash)throw new IOException("Staged document changed outside WorldOps.");
        }
        public static void Resume(string id=null)
        {
            if(Busy)throw new InvalidOperationException("WorldOps is already working.");
            if(id!=null){var file=WorldOpsStorage.SafeChild(Home,id+"/run.json");_run=WorldOpsStorage.Read<WorldRun>(file);SessionState.SetString(ActiveKey,id);}
            if(_run==null)throw new InvalidOperationException("No run to resume.");
            _run.cancelled=false;_run.status="running";Save();
        }
        public static void Cancel(){if(_run==null)return;_run.cancelled=true;_run.status="cancelling";Save();}
        public static void Rollback(string levelId)
        {
            if(Busy)throw new InvalidOperationException("Cancel the active run first.");
            var item=_run.levels.Single(x=>x.levelId==levelId);
            WorldOpsTransaction.Rollback(Path.Combine(_run.directory,item.levelId,"transaction"));item.status="rolled-back";
            LevelAuthoringCampaign.InvalidateCards();AssetDatabase.Refresh();Save();
        }
        static void Save()
        {
            WorldOpsStorage.Json(Path.Combine(_run.directory,"run.json"),_run);
            WorldOpsStorage.Json(Path.Combine(Home,"latest.json"),new{id=_run.id,path=_run.directory,status=_run.status});
            Changed?.Invoke();
        }
        static void Tick()
        {
            if(_run==null||!Busy||_ticking||EditorApplication.isCompiling||EditorApplication.isUpdating)return;
            _ticking=true;
            try
            {
                if(_run.cancelled)
                {
                    if(EditorApplication.isPlaying){EditorApplication.ExitPlaymode();return;}
                    if(EditorApplication.isPlayingOrWillChangePlaymode)return;
                    _run.status="cancelled";Save();return;
                }
                if(_run.nativeState!=null){if(WorldOpsNativeRunner.Tick(_run))Save();return;}
                var item=_run.levels.FirstOrDefault(x=>x.status=="queued");
                if(item!=null){Prepare(item);Save();return;}
                item=_run.levels.FirstOrDefault(x=>x.status=="capture");
                if(item!=null){WorldOpsNativeRunner.Begin(_run,item);Save();return;}
                item=_run.levels.FirstOrDefault(x=>x.status=="ready");
                if(item!=null&&_run.recipe.autoApply){Apply(item);Save();return;}
                _run.status=_run.levels.Any(x=>x.status=="review")?"awaiting-review":_run.levels.Any(x=>x.status=="exception"||x.status=="conflict")?"exceptions":"completed";Save();
            }
            catch(Exception e){_run.error=e.ToString();_run.status="error";Save();Debug.LogError("[WorldOps] "+e.Message);}
            finally{_ticking=false;}
        }
        static void Prepare(WorldLevelRun item)
        {
            try
            {
                WorldOpsStorage.Check(item.sources);
                if(LevelAuthoringWindow.WorldOpsConflict(item.levelId))throw new IOException("The open level has unsaved edits.");
                string folder=Path.Combine(_run.directory,item.levelId);Directory.CreateDirectory(folder);
                var doc=LevelAuthoringCampaign.Open(item.levelId,Path.Combine(_run.repositoryProject,Content),Path.Combine(_run.repositoryProject,Drafts),false);
                item.beforePath=Path.Combine(folder,"before.draft.json");WorldOpsStorage.Json(item.beforePath,doc);
                string key=WorldOpsStorage.Digest(_run.recipe);string artBefore=WorldOpsStorage.Digest(doc.art);
                bool already=doc.worldOps?.appliedRecipes.TryGetValue(key,out var applied)==true&&applied==artBefore;
                if(!already)foreach(var op in _run.recipe.operations)WorldOpsOperations.Apply(doc,op,_run.recipe.seed);
                // Vendor terrain rebuilds need a real, settled workspace. Never publish old baked geometry.
                if(_run.recipe.operations.Any(o=>o.kind=="river.edit"||o.kind=="terrain.stamp"))throw new InvalidOperationException("Terrain geometry requires a settled native bake; this change remains staged, not published.");
                if(doc.worldOps==null&&!already&&WorldOpsStorage.Digest(doc.art)!=artBefore)doc.worldOps=new WorldAuthoringMetadata();
                if(doc.worldOps!=null)doc.worldOps.appliedRecipes[key]=WorldOpsStorage.Digest(doc.art);
                item.stagePath=Path.Combine(folder,"after.draft.json");WorldOpsStorage.Json(item.stagePath,doc);item.documentHash=WorldOpsStorage.FileHash(item.stagePath);
                item.changes=Changes(WorldOpsStorage.Read<LevelAuthoringDocument>(item.beforePath),doc);item.findings=WorldOpsGeometry.Validate(doc,_run.recipe.criteria);
                Repair(item,doc);
                item.status=item.findings.Any(f=>f.severity=="error")?"exception":"capture";
            }
            catch(Exception e){item.error=e.Message;item.status=e is IOException?"conflict":"exception";}
        }
        static WorldChangeSet Changes(LevelAuthoringDocument before,LevelAuthoringDocument after)
        {
            var a=before.art.instances.ToDictionary(p=>p.id,p=>WorldOpsStorage.Digest(p));var b=after.art.instances.ToDictionary(p=>p.id,p=>WorldOpsStorage.Digest(p));
            var am=before.art.models.ToDictionary(p=>p.id,p=>WorldOpsStorage.Digest(p));var bm=after.art.models.ToDictionary(p=>p.id,p=>WorldOpsStorage.Digest(p));
            var objects=a.Keys.Union(b.Keys).Where(k=>!a.TryGetValue(k,out var x)||!b.TryGetValue(k,out var y)||x!=y).ToArray();
            var models=am.Keys.Union(bm.Keys).Where(k=>!am.TryGetValue(k,out var x)||!bm.TryGetValue(k,out var y)||x!=y).ToArray();
            return new WorldChangeSet{levelId=after.sourceLevelId,beforeHash=WorldOpsStorage.Digest(before),afterHash=WorldOpsStorage.Digest(after),changed=WorldOpsStorage.Digest(before)!=WorldOpsStorage.Digest(after),changedObjects=objects,changedModels=models,invalidated=objects.Length+models.Length>0?new[]{"geometry","camera","game-view","review"}:Array.Empty<string>()};
        }
        static void Repair(WorldLevelRun item,LevelAuthoringDocument doc)
        {
            while(_run.recipe.criteria.autoRepair&&item.round<_run.recipe.maxRepairRounds&&item.findings.Any(f=>f.repair!=null))
            {
                string signature=WorldOpsStorage.Digest(item.findings);item.round++;
                foreach(var finding in item.findings.Where(f=>f.repair!=null))
                    WorldOpsOperations.Apply(doc,new WorldOperation{id="repair/"+finding.code,kind=finding.repair,selector=new WorldSelector{ids=new[]{finding.objectId}},parameters=new Newtonsoft.Json.Linq.JObject()},_run.recipe.seed+item.round);
                item.findings=WorldOpsGeometry.Validate(doc,_run.recipe.criteria);string after=WorldOpsStorage.Digest(item.findings);
                item.stalled=after==signature?item.stalled+1:0;item.lastFailure=after;
                if(item.stalled>=2)break;
            }
            WorldOpsStorage.Json(item.stagePath,doc);item.documentHash=WorldOpsStorage.FileHash(item.stagePath);
        }
        public static void Submit(ReviewVerdict verdict)
        {
            if(_run==null||verdict.runId!=_run.id)throw new InvalidDataException("Verdict belongs to another run.");
            var item=_run.levels.Single(x=>x.levelId==verdict.levelId);
            if(item.status!="review"||item.review==null||verdict.round!=item.round||verdict.inputHash!=item.inputHash||verdict.documentHash!=item.documentHash||string.IsNullOrWhiteSpace(verdict.reviewer))throw new InvalidDataException("Stale or incomplete review verdict.");
            Fresh(item);
            if(WorldOpsStorage.Digest(item.review)!=item.reviewHash)throw new InvalidDataException("Damaged review packet.");
            foreach(var frame in item.review.frames)
                if(WorldOpsStorage.FileHash(frame.path)!=frame.sha256||!verdict.inspectedFrameHashes.Contains(frame.sha256))throw new InvalidDataException("Every current frame must be inspected and hash-bound.");
            if(verdict.accepted&&(verdict.corrections.Length>0||verdict.residuals.Length>0||item.findings.Any(f=>f.severity=="error")))throw new InvalidDataException("Unresolved defects cannot be accepted.");
            WorldOpsStorage.Json(Path.Combine(_run.directory,item.levelId,"verdict-"+item.round+".json"),verdict);
            if(verdict.accepted){item.visualAccepted=true;item.status="ready";}
            else if(verdict.corrections.Length==0||item.round>=_run.recipe.maxRepairRounds){item.status="exception";item.error=verdict.notes;}
            else
            {
                var recipe=new WorldRecipe{id="review",operations=verdict.corrections};WorldOpsOperations.Validate(recipe);
                var doc=WorldOpsStorage.Read<LevelAuthoringDocument>(item.stagePath);foreach(var op in verdict.corrections)WorldOpsOperations.Apply(doc,op,_run.recipe.seed+item.round);
                string previous=item.documentHash;WorldOpsStorage.Json(item.stagePath,doc);item.documentHash=WorldOpsStorage.FileHash(item.stagePath);
                item.stalled=previous==item.documentHash?item.stalled+1:0;item.round++;item.review=null;item.visualAccepted=false;item.findings=WorldOpsGeometry.Validate(doc,_run.recipe.criteria);
                item.status=item.stalled>=2||item.findings.Any(f=>f.severity=="error")?"exception":"capture";
            }
            _run.status="running";Save();
        }
        public static void Apply(WorldLevelRun item)
        {
            try
            {
                if(!item.visualAccepted||item.status!="ready")throw new InvalidOperationException("Current native frames need artistic acceptance first.");
                Fresh(item);if(LevelAuthoringWindow.WorldOpsConflict(item.levelId))throw new IOException("The open draft has unsaved changes.");
                var doc=WorldOpsStorage.Read<LevelAuthoringDocument>(item.stagePath);var writes=new Dictionary<string,string>();
                foreach(var root in new[]{_run.repositoryProject,_run.editorProject}.Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    foreach(var pair in LevelAuthoringCampaign.PrepareApply(doc,Path.Combine(root,Content),false))writes.Add(pair.Key,pair.Value);
                    var saved=LevelAuthoringDocument.Copy(doc);
                    saved.sourceContentHash=WorldOpsStorage.Hash(writes[LevelFile(root,item.levelId)]);saved.sourceArtHash=WorldOpsStorage.Hash(writes[ArtFile(root,item.levelId)]);
                    writes[DraftFile(root,item.levelId)]=JsonConvert.SerializeObject(saved,Formatting.Indented);
                }
                string journal=Path.Combine(_run.directory,item.levelId,"transaction");
                WorldOpsTransaction.Prepare(journal,writes,item.sources,new[]{_run.repositoryProject,_run.editorProject});WorldOpsTransaction.Commit(journal);
                item.status="applied";WorldOpsStorage.Json(Path.Combine(_run.directory,item.levelId,"owned-files.json"),writes.Keys.ToArray());
                LevelAuthoringCampaign.InvalidateCards();AssetDatabase.Refresh();LevelAuthoringWindow.WorldOpsApplied(item.levelId,DraftFile(_run.editorProject,item.levelId));
            }
            catch(Exception e){item.status=e is IOException?"conflict":"exception";item.error=e.Message;}
        }
        internal static void NativeFinished(WorldLevelRun item,List<WorldFrame> frames,List<ValidationFinding> findings)
        {
            item.findings.AddRange(findings);item.review=new ReviewPacket{runId=_run.id,levelId=item.levelId,inputHash=item.inputHash,documentHash=item.documentHash,round=item.round,frames=frames,findings=item.findings};
            item.reviewHash=WorldOpsStorage.Digest(item.review);WorldOpsStorage.Json(Path.Combine(_run.directory,item.levelId,"review-"+item.round+".json"),item.review);
            item.status="review";_run.nativeState=null;_run.nativeLevel=null;
        }
    }
}
