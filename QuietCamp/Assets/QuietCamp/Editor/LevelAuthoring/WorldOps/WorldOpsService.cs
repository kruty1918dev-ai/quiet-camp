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
        static LevelAuthoringWorkspace _terrainWorkspace;
        static LevelAuthoringWindow.VerificationSession _terrainSession;
        static double _terrainDeadline;
        public static event Action Changed;
        public static string EditorProject=>Path.GetDirectoryName(UnityEngine.Application.dataPath);
        public static string Home=>Path.Combine(EditorProject,"UserSettings/WorldOps");
        public static string LevelFolder(WorldRun run,string id)=>WorldOpsStorage.SafeChild(run.directory,id.Replace(':','_'));
        public static WorldRun Current=>_run;
        public static bool Busy=>_run!=null&&new[]{"preparing","running","capturing","applying","cancelling"}.Contains(_run.status);
        static WorldOpsService(){EditorApplication.update+=Tick;Restore();}
        static void Restore()
        {
            var id=SessionState.GetString(ActiveKey,"");if(string.IsNullOrEmpty(id))return;
            var path=WorldOpsStorage.SafeChild(Home,id+"/run.json");
            if(File.Exists(path))_run=WorldOpsStorage.Read<WorldRun>(path);
            RestoreWorkspaceSnapshot();
        }
        static void RestoreWorkspaceSnapshot()
        {
            if(_run!=null)
            {
                string snapshot=Path.Combine(_run.directory,"suspended-workspace.json");
                if(File.Exists(snapshot))
                {
                    var saved=Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(snapshot));
                    _terrainSession=new LevelAuthoringWindow.VerificationSession{document=saved["document"].ToObject<LevelAuthoringDocument>(),path=(string)saved["path"],dirty=(bool)saved["dirty"]};
                }
            }
        }
        static string LevelFile(string root,string id)=>Path.Combine(root,Content,id.StartsWith("gen:",StringComparison.Ordinal)?"GeneratedLevels":"Levels",id.Replace(':','_')+".json");
        static string ArtFile(string root,string id)=>Path.Combine(root,Content,"ArtCompositions",id.Replace(':','_')+".json");
        static string DraftFile(string root,string id)=>Path.Combine(root,Drafts,id.Replace(':','_')+".draft.json");
        public static WorldRun Start(WorldRecipe recipe,string repositoryProject,bool agentDelivery=false,string[] preexistingDirtyPaths=null)
        {
            if(EditorUtility.scriptCompilationFailed||EditorApplication.isCompiling||EditorApplication.isUpdating)throw new InvalidOperationException("Wait for successful Unity compilation before starting WorldOps.");
            if(Busy||EditorApplication.isPlayingOrWillChangePlaymode||SessionState.GetBool("QuietCamp.NativeTestsRunning",false))throw new InvalidOperationException("Wait for the current Editor operation; a second Unity instance is not used.");
            WorldOpsOperations.Validate(recipe);repositoryProject=Path.GetFullPath(repositoryProject);
            if(!File.Exists(Path.Combine(repositoryProject,Content,"campaign.json")))throw new DirectoryNotFoundException("QuietCamp campaign not found in "+repositoryProject);
            var cards=LevelAuthoringCampaign.ReadCards(Path.Combine(repositoryProject,Content));
            var selected=cards.Where(c=>(recipe.levelIds.Length==0&&recipe.orders.Length==0)||recipe.levelIds.Contains(c.id)||recipe.orders.Contains(c.number)).ToArray();
            if(selected.Length==0||recipe.levelIds.Except(cards.Select(c=>c.id)).Any()||recipe.orders.Any(n=>n<1||n>cards.Length))throw new ArgumentException("Unknown campaign level selection.");
            var id=DateTime.UtcNow.ToString("yyyyMMddTHHmmssfff")+"-"+Guid.NewGuid().ToString("N").Substring(0,6);
            _run=new WorldRun{id=id,directory=WorldOpsStorage.SafeChild(Home,id),recipe=LevelAuthoringDocument.Copy(recipe),repositoryProject=repositoryProject,editorProject=EditorProject,createdUtc=DateTime.UtcNow.ToString("O"),dependencies=Dependencies(repositoryProject)};
            _run.sourceManifestHash=WorldOpsStorage.Digest(_run.dependencies);
            _run.agentDelivery=agentDelivery;_run.preexistingDirtyPaths=preexistingDirtyPaths??Array.Empty<string>();
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
        public static Dictionary<string,string> Dependencies(string repositoryProject=null)
        {
            var result=new Dictionary<string,string>{{"@engine",UnityEngine.Application.unityVersion},{"@worldops",Version},{"@target",EditorUserBuildSettings.activeBuildTarget.ToString()}};
            var levelFiles=new HashSet<string>(Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(Path.Combine(EditorProject,Content,"campaign.json")))["campaignLevelIds"].Values<string>().Select(id=>id.Replace(':','_')+".json"));
            // Archived roadmap shaders and catalogue-only thumbnails cannot affect a campaign capture.
            // Their QA-local versions are deliberately not synchronized by this tool.
            foreach(var path in AssetDatabase.GetAllAssetPaths().Where(p=>p.StartsWith("Assets/QuietCamp/",StringComparison.Ordinal)&&!AssetDatabase.IsValidFolder(p)&&!p.Contains("/Editor/RoadmapArchive/Legacy/")&&p!="Assets/QuietCamp/Editor/TentThumbnail.shader"&&!p.Contains("/LevelAuthoring/Palette/")&&!p.Contains("/LevelAuthoring/Workspaces/")&&
                !(Path.GetExtension(p)==".json"&&(new[]{"/Levels/","/GeneratedLevels/","/ArtRevisions/","/ContentRevisions/","/CampaignLevels/"}.Any(p.Contains)||p.Contains("/ArtCompositions/")&&levelFiles.Contains(Path.GetFileName(p))))).OrderBy(p=>p,StringComparer.Ordinal))
            {
                result[path]=AssetDatabase.GetAssetDependencyHash(path).ToString();
                string local=Path.Combine(EditorProject,path);
                result["editor/"+path]=WorldOpsStorage.FileHash(local);
                result["editor/"+path+".meta"]=WorldOpsStorage.FileHash(local+".meta");
                if(repositoryProject!=null)
                {
                    string canonical=Path.Combine(repositoryProject,path);
                    result["repository/"+path]=WorldOpsStorage.FileHash(canonical);
                    result["repository/"+path+".meta"]=WorldOpsStorage.FileHash(canonical+".meta");
                    // QA may retain editor-only legacy files absent from the repository. They
                    // remain hashed inputs; shared source files must still match byte for byte.
                    if(File.Exists(canonical)&&new[]{".cs",".shader",".hlsl",".asmdef"}.Contains(Path.GetExtension(path))&&WorldOpsStorage.FileHash(local)!=result["repository/"+path])throw new IOException("Repository and Editor sources differ; synchronize the owned source before running: "+path);
                }
            }
            foreach(var file in new[]{"Packages/manifest.json","Packages/packages-lock.json","ProjectSettings/GraphicsSettings.asset","ProjectSettings/QualitySettings.asset"})result[file]=WorldOpsStorage.FileHash(Path.Combine(EditorProject,file));
            foreach(var file in Directory.GetFiles(Path.Combine(EditorProject,"Library/ScriptAssemblies"),"*.dll").OrderBy(p=>p,StringComparer.Ordinal))result["assembly/"+Path.GetFileName(file)]=WorldOpsStorage.FileHash(file);
            return result;
        }
        static void Fresh(WorldLevelRun item)
        {
            WorldOpsStorage.Check(item.sources);
            var current=Dependencies(_run.repositoryProject);
            if(WorldOpsStorage.Digest(current)!=_run.sourceManifestHash)
            {
                var changed=current.Keys.Concat(_run.dependencies.Keys).Distinct().Where(k=>
                    !current.TryGetValue(k,out var a)||!_run.dependencies.TryGetValue(k,out var b)||a!=b).Take(8);
                throw new IOException("Unity/assets/assemblies changed; prepare fresh evidence: "+string.Join(", ",changed));
            }
            if(WorldOpsStorage.FileHash(item.stagePath)!=item.documentHash)throw new IOException("Staged document changed outside WorldOps.");
            if(WorldOpsStorage.FileHash(item.beforePath)!=item.beforeFileHash)throw new IOException("Source snapshot changed outside WorldOps.");
        }
        public static void Resume(string id=null)
        {
            if(SessionState.GetBool("QuietCamp.NativeTestsRunning",false)||File.Exists(Path.Combine(EditorProject,"Temp/ai/qc007-art-request.json")))throw new InvalidOperationException("Wait for the active native verification job.");
            if(Busy)throw new InvalidOperationException("WorldOps is already working.");
            if(id!=null){var file=WorldOpsStorage.SafeChild(Home,id+"/run.json");_run=WorldOpsStorage.Read<WorldRun>(file);SessionState.SetString(ActiveKey,id);RestoreWorkspaceSnapshot();}
            if(_run==null)throw new InvalidOperationException("No run to resume.");
            if(!EditorApplication.isPlayingOrWillChangePlaymode&&_run.nativeState!=null&&_run.nativeState!="stopping")
            {_run.nativeState=null;_run.nativeLevel=null;}
            _run.cancelled=false;_run.error=null;_run.nativeIoRetries=0;_run.nativeRetryAt=0;_run.status="running";Save();
        }
        public static void Cancel(){if(_run==null)return;_run.cancelled=true;_run.status="cancelling";Save();}
        public static void Rollback(string levelId)
        {
            if(Busy)throw new InvalidOperationException("Cancel the active run first.");
            var item=_run.levels.Single(x=>x.levelId==levelId);
            if(LevelAuthoringWindow.WorldOpsConflict(item.levelId))throw new IOException("The open draft has unsaved changes.");
            WorldOpsTransaction.Rollback(Path.Combine(LevelFolder(_run,item.levelId),"transaction"));item.status="rolled-back";
            UpdateCampaignSources(item);
            LevelAuthoringCampaign.InvalidateCards();AssetDatabase.Refresh();LevelAuthoringWindow.WorldOpsApplied(item.levelId,DraftFile(_run.editorProject,item.levelId));Save();
        }
        static void Save()
        {
            WorldOpsStorage.Json(Path.Combine(_run.directory,"run.json"),_run);
            WorldOpsStorage.Json(Path.Combine(Home,"latest.json"),new{id=_run.id,path=_run.directory,status=_run.status});
            WorldOpsReport.Write(_run);
            Changed?.Invoke();
        }
        static void Tick()
        {
            if(_run==null||!Busy||_ticking||EditorApplication.isCompiling||EditorApplication.isUpdating)return;
            _ticking=true;
            try
            {
                if(EditorUtility.scriptCompilationFailed&&!_run.cancelled)throw new InvalidOperationException("Unity compilation failed during WorldOps; the current sources have no valid loaded assembly.");
                if(_run.cancelled)
                {
                    DisposeTerrain();
                    if(EditorApplication.isPlaying){EditorApplication.ExitPlaymode();return;}
                    if(EditorApplication.isPlayingOrWillChangePlaymode)return;
                    WorldOpsNativeRunner.RestoreCaptureSettings(_run);
                    _run.nativeState=null;_run.nativeLevel=null;
                    _run.status="cancelled";Save();return;
                }
                if(_run.nativeState!=null){if(WorldOpsNativeRunner.Tick(_run))Save();return;}
                if(_run.campaignValidationHash==null){ValidateCampaign();Save();return;}
                var baking=_run.levels.FirstOrDefault(x=>x.status=="baking");
                if(baking!=null){if(BakeTerrain(baking))Save();return;}
                var item=_run.levels.FirstOrDefault(x=>x.status=="queued");
                if(item!=null){Prepare(item);Save();return;}
                item=_run.levels.FirstOrDefault(x=>x.status=="ready");
                if(item!=null&&_run.recipe.autoApply){Apply(item);Save();return;}
                item=_run.levels.FirstOrDefault(x=>x.status=="capture");
                if(item!=null){WorldOpsNativeRunner.Begin(_run,item);Save();return;}
                _run.status=_run.levels.Any(x=>x.status=="review")?"awaiting-review":_run.levels.Any(x=>x.status=="ready")?"ready-to-apply":_run.levels.Any(x=>x.status=="exception"||x.status=="conflict")?"exceptions":_run.levels.All(x=>x.status=="audited")?(_run.levels.Any(x=>x.findings.Count>0)?"audited-with-findings":"audited"):"completed";Save();
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
                string folder=LevelFolder(_run,item.levelId);Directory.CreateDirectory(folder);
                if(RestoreCacheEarly(item,folder))return;
                var doc=LevelAuthoringCampaign.Open(item.levelId,Path.Combine(_run.repositoryProject,Content),Path.Combine(_run.repositoryProject,Drafts),false);
                item.beforePath=Path.Combine(folder,"before.draft.json");WorldOpsStorage.Json(item.beforePath,doc);
                item.beforeFileHash=WorldOpsStorage.FileHash(item.beforePath);
                string key=WorldOpsStorage.Digest(_run.recipe);
                bool already=doc.worldOps?.appliedRecipes.TryGetValue(key,out var applied)==true&&applied==RecipeState(doc);
                if(!already)foreach(var op in _run.recipe.operations)WorldOpsOperations.Apply(doc,op,_run.recipe.seed);
                item.stagePath=Path.Combine(folder,"after.draft.json");Stage(item,doc);
                if(!already&&_run.recipe.operations.Any(o=>o.kind=="river.edit"||o.kind=="terrain.stamp")){item.status="baking";return;}
                item.findings=WorldOpsGeometry.Validate(doc,_run.recipe.criteria);
                Repair(item,doc);
                PlaceChanged(item,doc);
                item.changes=Changes(WorldOpsStorage.Read<LevelAuthoringDocument>(item.beforePath),doc);
                item.status=item.findings.Any(f=>f.code=="document.invalid")?"exception":!_run.recipe.capture?"audited":"capture";
                if(item.status=="capture")RestoreCache(item);
            }
            catch(Exception e){item.error=e.Message;item.status=e is IOException?"conflict":"exception";}
        }
        static void ValidateCampaign()
        {
            var sources=new SortedDictionary<string,string>();
            if(_run.recipe.previewOnly)
            {
                // Selected native preview is useful while an unrelated puzzle is broken.
                // This is never a successful campaign preflight and cannot be published.
                foreach(var item in _run.levels)
                {
                    string path=LevelFile(_run.repositoryProject,item.levelId);
                    var level=WorldOpsStorage.Read<LevelData>(path);
                    if(LevelContentValidator.Validate(level).Count>0||!RuleEvaluator.Evaluate(level,level.witness).IsSolved)
                        throw new InvalidDataException("Preview puzzle/witness failed: "+item.levelId);
                    sources[path]=WorldOpsStorage.FileHash(path);
                }
                _run.campaignSources=sources.ToDictionary(p=>p.Key,p=>p.Value);
                _run.campaignValidationHash="preview-only:"+WorldOpsStorage.Digest(sources);
                WorldOpsStorage.Json(Path.Combine(_run.directory,"campaign-validation.json"),new{scope="selected-preview-only",publicationAllowed=false,sources});
                return;
            }
            foreach(var root in new[]{_run.repositoryProject,_run.editorProject}.Distinct(StringComparer.OrdinalIgnoreCase))
                foreach(var card in LevelAuthoringCampaign.ReadCards(Path.Combine(root,Content)))
                {
                    string path=LevelFile(root,card.id);var level=WorldOpsStorage.Read<LevelData>(path);
                    var issues=LevelContentValidator.Validate(level);
                    if(issues.Count>0||!RuleEvaluator.Evaluate(level,level.witness).IsSolved)throw new InvalidDataException("Campaign puzzle/witness failed: "+card.id);
                    sources[path]=WorldOpsStorage.FileHash(path);
                }
            CampaignLandmarkCatalog.Read();
            _run.campaignSources=sources.ToDictionary(p=>p.Key,p=>p.Value);_run.campaignValidationHash=WorldOpsStorage.Digest(sources);WorldOpsStorage.Json(Path.Combine(_run.directory,"campaign-validation.json"),new{puzzles=sources.Count,primaryReservations=100,hash=_run.campaignValidationHash,sources});
        }
        static void RestoreCache(WorldLevelRun item)
        {
            string file=Path.Combine(Home,"cache",item.cacheKey+".json");if(!File.Exists(file))return;
            try
            {
                var cached=WorldOpsStorage.Read<WorldLevelRun>(file);
                if(cached.documentHash!=item.documentHash||cached.review==null||cached.reviewHash!=WorldOpsStorage.Digest(cached.review)||cached.inputHash!=item.inputHash)return;
                VerifyEvidence(cached);
                Fresh(item);item.cached=true;item.review=new ReviewPacket{runId=_run.id,levelId=item.levelId,round=item.round,inputHash=item.inputHash,documentHash=item.documentHash,frames=cached.review.frames,findings=cached.review.findings};
                item.findings=item.review.findings;item.reviewHash=WorldOpsStorage.Digest(item.review);item.status="review";
                WorldOpsStorage.Json(Path.Combine(LevelFolder(_run,item.levelId),"review-"+item.round+".json"),item.review);
            }
            catch(IOException){/* An invalid cache is a miss, never evidence. */}
            catch(JsonException){/* A corrupt cache is rebuilt. */}
        }
        static bool RestoreCacheEarly(WorldLevelRun item,string folder)
        {
            string path=Path.Combine(Home,"cache",item.cacheKey+".json");if(!File.Exists(path))return false;
            try
            {
                var cached=WorldOpsStorage.Read<WorldLevelRun>(path);
                if(cached.cacheKey!=item.cacheKey||cached.beforeFileHash==null||WorldOpsStorage.FileHash(cached.beforePath)!=cached.beforeFileHash||WorldOpsStorage.FileHash(cached.stagePath)!=cached.documentHash)return false;
                VerifyEvidence(cached);WorldOpsStorage.Check(item.sources);
                item.beforePath=Path.Combine(folder,"before.draft.json");item.stagePath=Path.Combine(folder,"after.draft.json");
                File.Copy(cached.beforePath,item.beforePath,true);File.Copy(cached.stagePath,item.stagePath,true);
                item.beforeFileHash=cached.beforeFileHash;item.documentHash=cached.documentHash;item.round=cached.round;item.changes=cached.changes;
                RestoreCache(item);return item.status=="review";
            }
            catch(IOException){return false;}
            catch(JsonException){return false;}
        }
        static bool BakeTerrain(WorldLevelRun item)
        {
            try
            {
                if(_terrainWorkspace==null)
                {
                    var window=Resources.FindObjectsOfTypeAll<LevelAuthoringWindow>().FirstOrDefault();
                    _terrainSession=window?.PrepareVerification()??_terrainSession;
                    if(_terrainSession!=null)WorldOpsStorage.Json(Path.Combine(_run.directory,"suspended-workspace.json"),new{document=_terrainSession.document,path=_terrainSession.path,dirty=_terrainSession.dirty});
                    if(!string.IsNullOrEmpty(_run.terrainScenePath))
                    {
                        var orphan=UnityEngine.SceneManagement.SceneManager.GetSceneByPath(_run.terrainScenePath);
                        if(orphan.IsValid()&&orphan.isLoaded&&orphan.path.StartsWith("Assets/QuietCamp/Editor/LevelAuthoring/Workspaces/",StringComparison.Ordinal))UnityEditor.SceneManagement.EditorSceneManager.CloseScene(orphan,true);
                    }
                    _terrainWorkspace=new LevelAuthoringWorkspace(WorldOpsStorage.Read<LevelAuthoringDocument>(item.stagePath));_run.terrainScenePath=_terrainWorkspace.Scene.path;_terrainDeadline=EditorApplication.timeSinceStartup+180;return true;
                }
                if(_terrainWorkspace.River.IsUpdating)
                {if(EditorApplication.timeSinceStartup>_terrainDeadline)throw new TimeoutException("MicroVerse/River Modeler bake did not settle.");return false;}
                var doc=_terrainWorkspace.Capture(true);DisposeTerrain();
                Stage(item,doc);
                item.findings=WorldOpsGeometry.Validate(doc,_run.recipe.criteria);Repair(item,doc);
                PlaceChanged(item,doc);
                item.changes=Changes(WorldOpsStorage.Read<LevelAuthoringDocument>(item.beforePath),doc);
                item.status=item.findings.Any(f=>f.code=="document.invalid")?"exception":"capture";return true;
            }
            catch(Exception e){DisposeTerrain();item.status="exception";item.error=e.Message;return true;}
        }
        static void DisposeTerrain()
        {
            _terrainWorkspace?.Dispose();_terrainWorkspace=null;
            if(_run!=null)_run.terrainScenePath=null;
            if(_terrainSession!=null){LevelAuthoringWindow.OpenWindow().RestoreVerification(_terrainSession);_terrainSession=null;}
            if(_run!=null){var file=Path.Combine(_run.directory,"suspended-workspace.json");if(File.Exists(file))File.Delete(file);}
        }
        static void PlaceChanged(WorldLevelRun item,LevelAuthoringDocument doc)
        {
            var poses=NativePlacements(WorldOpsStorage.Read<LevelAuthoringDocument>(item.beforePath),doc);
            if(poses.Length==0)
            {
                WorldOpsStorage.Json(Path.Combine(LevelFolder(_run,item.levelId),"placement-receipt.json"),new{adapter="Prefab World Builder",placed=0,reason="Existing IDs: dimensions and vertical contact only; native Game View still required."});
                return;
            }
            var staging=doc.EditingCopy();staging.terrain=null;staging.art.instances=Array.Empty<CampArtDefinition.Instance>();staging.art.cover=Array.Empty<CampArtDefinition.CoverPoint>();
            using(var workspace=new LevelAuthoringWorkspace(staging))
            {
                int count=OpeningCampaignAuthoring.Paint(workspace,poses);workspace.RefreshPlantBatches();var captured=workspace.Capture(false);
                var native=captured.art.instances.ToDictionary(p=>p.id);
                foreach(var p in poses)
                {
                    if(!native.TryGetValue(p.id,out var actual))throw new InvalidDataException("PWB lost a stable object ID.");
                    if(Vector3.Distance(new Vector3(p.x,p.y,p.z),new Vector3(actual.x,actual.y,actual.z))>.002f)throw new InvalidDataException("PWB changed an explicit object pose.");
                    var a=actual.rotationEuler==null?Quaternion.Euler(0,actual.yaw,0):Quaternion.Euler(CampArtDefinition.Vector(actual.rotationEuler));
                    var b=p.rotationEuler==null?Quaternion.Euler(0,p.yaw,0):Quaternion.Euler(CampArtDefinition.Vector(p.rotationEuler));
                    var aScale=actual.scaleXYZ==null?Vector3.one*actual.scale:CampArtDefinition.Vector(actual.scaleXYZ);
                    var bScale=p.scaleXYZ==null?Vector3.one*p.scale:CampArtDefinition.Vector(p.scaleXYZ);
                    if(Quaternion.Angle(a,b)>.01f||Vector3.Distance(aScale,bScale)>.002f||actual.modelId!=p.modelId)throw new InvalidDataException("PWB changed an explicit rotation, scale or model ID.");
                }
                WorldOpsStorage.Json(Path.Combine(LevelFolder(_run,item.levelId),"placement-receipt.json"),new{adapter="Prefab World Builder",placed=count,objects=poses.Select(p=>p.id),nativeHash=WorldOpsStorage.Digest(captured.art.instances)});
            }
            WorldOpsStorage.Json(item.stagePath,doc);item.documentHash=WorldOpsStorage.FileHash(item.stagePath);
        }
        public static CampArtDefinition.Instance[] NativePlacements(LevelAuthoringDocument before,LevelAuthoringDocument after)
        {
            var existing=before.art.instances.ToDictionary(p=>p.id);
            return after.art.instances.Where(p=>
            {
                if(p.id.StartsWith("worldops/path/",StringComparison.Ordinal))return false;
                if(!existing.TryGetValue(p.id,out var old))return true;
                if(Mathf.Abs(p.x-old.x)>.00001f||Mathf.Abs(p.z-old.z)>.00001f)return true;
                var a=old.rotationEuler==null?Quaternion.Euler(0,old.yaw,0):Quaternion.Euler(CampArtDefinition.Vector(old.rotationEuler));
                var b=p.rotationEuler==null?Quaternion.Euler(0,p.yaw,0):Quaternion.Euler(CampArtDefinition.Vector(p.rotationEuler));
                return Quaternion.Angle(a,b)>.01f;
            }).ToArray();
        }
        static WorldChangeSet Changes(LevelAuthoringDocument before,LevelAuthoringDocument after)
        {
            var a=before.art.instances.ToDictionary(p=>p.id,p=>WorldOpsStorage.Digest(p));var b=after.art.instances.ToDictionary(p=>p.id,p=>WorldOpsStorage.Digest(p));
            var am=before.art.models.ToDictionary(p=>p.id,p=>WorldOpsStorage.Digest(p));var bm=after.art.models.ToDictionary(p=>p.id,p=>WorldOpsStorage.Digest(p));
            var objects=a.Keys.Union(b.Keys).Where(k=>!a.TryGetValue(k,out var x)||!b.TryGetValue(k,out var y)||x!=y).ToArray();
            var models=am.Keys.Union(bm.Keys).Where(k=>!am.TryGetValue(k,out var x)||!bm.TryGetValue(k,out var y)||x!=y).ToArray();
            string beforeHash=WorldOpsStorage.Digest(before),afterHash=WorldOpsStorage.Digest(after);
            return new WorldChangeSet{levelId=after.sourceLevelId,beforeHash=beforeHash,afterHash=afterHash,changed=beforeHash!=afterHash,changedObjects=objects,changedModels=models,invalidated=beforeHash!=afterHash?new[]{"document","geometry","camera","game-view","review"}:Array.Empty<string>()};
        }
        static void Repair(WorldLevelRun item,LevelAuthoringDocument doc)
        {
            var progress=new WorldLevelRun();
            while(_run.recipe.criteria.autoRepair&&item.round<_run.recipe.maxRepairRounds&&item.findings.Any(f=>f.repair!=null))
            {
                progress.previousReviewFindings=item.findings;item.round++;
                foreach(var finding in item.findings.Where(f=>f.repair!=null))
                    WorldOpsOperations.Apply(doc,new WorldOperation{id="repair/"+finding.code,kind=finding.repair,selector=new WorldSelector{ids=new[]{finding.objectId}},parameters=new Newtonsoft.Json.Linq.JObject()},_run.recipe.seed+item.round);
                item.findings=WorldOpsGeometry.Validate(doc,_run.recipe.criteria);bool stalled=RecordReviewProgress(progress,item.findings);
                item.stalled=progress.visualStalls.Values.DefaultIfEmpty(0).Max();item.lastFailure=WorldOpsStorage.Digest(item.findings);
                if(stalled)break;
            }
            Stage(item,doc);
        }
        public static string RecipeState(LevelAuthoringDocument doc)=>WorldOpsStorage.Digest(new{doc.art,objects=doc.worldOps?.objects,paths=doc.worldOps?.paths,doc.level.width,doc.level.height});
        static void Stage(WorldLevelRun item,LevelAuthoringDocument doc)
        {
            if(_run.recipe.operations.Any(o=>o.kind!="audit"))
            {if(doc.worldOps==null)doc.worldOps=new WorldAuthoringMetadata();doc.worldOps.appliedRecipes[WorldOpsStorage.Digest(_run.recipe)]=RecipeState(doc);}
            WorldOpsStorage.Json(item.stagePath,doc);item.documentHash=WorldOpsStorage.FileHash(item.stagePath);
        }
        public static void Submit(ReviewVerdict verdict)
        {
            if(_run==null||verdict.runId!=_run.id)throw new InvalidDataException("Verdict belongs to another run.");
            var item=_run.levels.Single(x=>x.levelId==verdict.levelId);
            if(item.status!="review"||item.review==null||verdict.round!=item.round||verdict.inputHash!=item.inputHash||verdict.documentHash!=item.documentHash||string.IsNullOrWhiteSpace(verdict.reviewer))throw new InvalidDataException("Stale or incomplete review verdict.");
            Fresh(item);
            VerifyEvidence(item);
            foreach(var frame in item.review.frames)
                if(WorldOpsStorage.FileHash(frame.path)!=frame.sha256||!verdict.inspectedFrameHashes.Contains(frame.sha256))throw new InvalidDataException("Every current frame must be inspected and hash-bound.");
            if(verdict.accepted&&(verdict.corrections.Length>0||verdict.residuals.Length>0||item.findings.Any(f=>f.severity=="error")))throw new InvalidDataException("Unresolved defects cannot be accepted.");
            WorldOpsStorage.Json(Path.Combine(LevelFolder(_run,item.levelId),"verdict-"+item.round+".json"),verdict);
            item.verdictHash=WorldOpsStorage.FileHash(Path.Combine(LevelFolder(_run,item.levelId),"verdict-"+item.round+".json"));
            if(verdict.accepted){item.visualAccepted=true;item.status="ready";}
            else if(verdict.corrections.Length==0||item.round>=_run.recipe.maxRepairRounds){item.status="exception";item.error=verdict.notes;}
            else
            {
                var recipe=new WorldRecipe{id="review",operations=verdict.corrections};WorldOpsOperations.Validate(recipe);
                item.previousReviewFindings=LevelAuthoringDocument.Copy(item.review.findings);
                item.beforeFrames=item.review.frames.Where(f=>f.phase=="before").ToList();
                var doc=WorldOpsStorage.Read<LevelAuthoringDocument>(item.stagePath);foreach(var op in verdict.corrections)WorldOpsOperations.Apply(doc,op,_run.recipe.seed+item.round);
                string previous=item.documentHash;Stage(item,doc);
                item.stalled=previous==item.documentHash?item.stalled+1:0;item.round++;item.review=null;item.visualAccepted=false;item.findings=WorldOpsGeometry.Validate(doc,_run.recipe.criteria);
                if(item.stalled>=2||item.findings.Any(f=>f.code=="document.invalid"))item.status="exception";
                else if(verdict.corrections.Any(o=>o.kind=="river.edit"||o.kind=="terrain.stamp"))item.status="baking";
                else{Repair(item,doc);PlaceChanged(item,doc);item.changes=Changes(WorldOpsStorage.Read<LevelAuthoringDocument>(item.beforePath),doc);item.status="capture";}
            }
            _run.status="running";Save();
        }
        public static void Apply(WorldLevelRun item)
        {
            try
            {
                if(_run.recipe.previewOnly)throw new InvalidOperationException("Preview-only run cannot publish. A normal run with full campaign validation is required.");
                if(!item.visualAccepted||item.status!="ready")throw new InvalidOperationException("Current native frames need artistic acceptance first.");
                VerifyEvidence(item);
                if(WorldOpsStorage.FileHash(Path.Combine(LevelFolder(_run,item.levelId),"verdict-"+item.round+".json"))!=item.verdictHash)throw new InvalidDataException("Damaged artistic verdict.");
                if(LevelAuthoringWindow.WorldOpsConflict(item.levelId))throw new IOException("The open draft has unsaved changes.");
                string journal=Path.Combine(LevelFolder(_run,item.levelId),"transaction");
                if(File.Exists(Path.Combine(journal,"journal.json")))
                {
                    if(WorldOpsStorage.Digest(Dependencies(_run.repositoryProject))!=_run.sourceManifestHash||WorldOpsStorage.FileHash(item.stagePath)!=item.documentHash)throw new IOException("Recovery inputs changed.");
                    WorldOpsTransaction.Commit(journal);item.status="applied";UpdateCampaignSources(item);LevelAuthoringCampaign.InvalidateCards();AssetDatabase.Refresh();LevelAuthoringWindow.WorldOpsApplied(item.levelId,DraftFile(_run.editorProject,item.levelId));return;
                }
                Fresh(item);
                if(_run.campaignSources.Any(p=>WorldOpsStorage.FileHash(p.Key)!=p.Value))ValidateCampaign();
                var doc=WorldOpsStorage.Read<LevelAuthoringDocument>(item.stagePath);var writes=new Dictionary<string,string>();
                foreach(var root in new[]{_run.repositoryProject,_run.editorProject}.Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    foreach(var pair in LevelAuthoringCampaign.PrepareApply(doc,Path.Combine(root,Content),false))writes.Add(pair.Key,pair.Value);
                    var saved=LevelAuthoringDocument.Copy(doc);
                    saved.sourceContentHash=WorldOpsStorage.Hash(writes[LevelFile(root,item.levelId)]);saved.sourceArtHash=WorldOpsStorage.Hash(writes[ArtFile(root,item.levelId)]);
                    writes[DraftFile(root,item.levelId)]=JsonConvert.SerializeObject(saved,Formatting.Indented);
                }
                AddMissingMetadata(writes);
                WorldOpsTransaction.Prepare(journal,writes,item.sources,new[]{_run.repositoryProject,_run.editorProject});WorldOpsTransaction.Commit(journal);
                item.status="applied";WorldOpsStorage.Json(Path.Combine(LevelFolder(_run,item.levelId),"owned-files.json"),writes.Keys.ToArray());
                UpdateCampaignSources(item);
                LevelAuthoringCampaign.InvalidateCards();AssetDatabase.Refresh();LevelAuthoringWindow.WorldOpsApplied(item.levelId,DraftFile(_run.editorProject,item.levelId));
            }
            catch(Exception e){item.status=e is IOException?"conflict":"exception";item.error=e.Message;}
            finally{Save();}
        }
        static void AddMissingMetadata(Dictionary<string,string> writes)
        {
            foreach(var pair in writes.ToArray())
            {
                if(Path.GetExtension(pair.Key)!=".json"||File.Exists(pair.Key+".meta"))continue;
                string root=pair.Key.StartsWith(_run.repositoryProject,StringComparison.OrdinalIgnoreCase)?_run.repositoryProject:_run.editorProject;
                string relative=Path.GetRelativePath(root,pair.Key);string peer=Path.Combine(root==_run.repositoryProject?_run.editorProject:_run.repositoryProject,relative)+".meta";
                string meta=File.Exists(peer)?File.ReadAllText(peer):"fileFormatVersion: 2\nguid: "+WorldOpsStorage.Hash("QuietCamp.WorldOps/"+relative.Replace('\\','/')).Substring(0,32)+"\nTextScriptImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n";
                writes[pair.Key+".meta"]=meta;
            }
        }
        public static string PrepareDelivery(bool requireAll=false)
        {
            if(_run==null||Busy)throw new InvalidOperationException("Wait for WorldOps to finish its active step.");
            if(requireAll&&(_run.levels.Count!=100||_run.levels.Any(l=>l.status!="applied")))throw new InvalidOperationException("All 100 levels require accepted, applied current evidence.");
            if(WorldOpsStorage.Digest(Dependencies(_run.repositoryProject))!=_run.sourceManifestHash)throw new IOException("Delivery dependencies changed.");
            var groups=new Dictionary<string,Dictionary<string,string>>();
            foreach(var item in _run.levels.Where(l=>l.status=="applied"))
            {
                VerifyEvidence(item);
                if(WorldOpsStorage.FileHash(Path.Combine(LevelFolder(_run,item.levelId),"verdict-"+item.round+".json"))!=item.verdictHash)throw new InvalidDataException("Damaged verdict.");
                var files=WorldOpsTransaction.VerifyCommitted(Path.Combine(LevelFolder(_run,item.levelId),"transaction"));
                string prefix=_run.repositoryProject.TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
                groups[item.groupId]=files.Where(p=>p.Key.StartsWith(prefix,StringComparison.OrdinalIgnoreCase)).ToDictionary(p=>p.Key,p=>p.Value);
            }
            string output=Path.Combine(_run.directory,"delivery.json");WorldOpsStorage.Json(output,new{runId=_run.id,repository=Path.GetDirectoryName(_run.repositoryProject),groups,agentDelivery=_run.agentDelivery,preexistingDirtyPaths=_run.preexistingDirtyPaths});return output;
        }
        static void UpdateCampaignSources(WorldLevelRun item)
        {
            foreach(var root in new[]{_run.repositoryProject,_run.editorProject}.Distinct(StringComparer.OrdinalIgnoreCase))
            {string path=LevelFile(root,item.levelId);_run.campaignSources[path]=WorldOpsStorage.FileHash(path);}
            _run.campaignValidationHash=WorldOpsStorage.Digest(_run.campaignSources.OrderBy(p=>p.Key).ToArray());
        }
        public static void VerifyEvidence(WorldLevelRun item)
        {
            if(item.review==null||WorldOpsStorage.Digest(item.review)!=item.reviewHash)throw new InvalidDataException("Damaged review packet.");
            if(item.review.frames.Count==0)throw new InvalidDataException("No native frames.");
            VerifyFrames(item.review.frames);
        }
        public static void VerifyFrames(IEnumerable<WorldFrame> frames)
        {
            var evidence=new Dictionary<string,string>();
            foreach(var frame in frames)
            {
                if(WorldOpsStorage.FileHash(frame.path)!=frame.sha256)throw new InvalidDataException("Stale or damaged gameplay frame.");
                foreach(var pair in frame.evidence)
                {if(evidence.TryGetValue(pair.Key,out var previous)&&previous!=pair.Value)throw new InvalidDataException("Inconsistent capture receipt.");evidence[pair.Key]=pair.Value;}
            }
            WorldOpsStorage.Check(evidence);
        }
        internal static void NativeFinished(WorldLevelRun item,List<WorldFrame> frames,List<ValidationFinding> findings)
        {
            Fresh(item);item.findings.AddRange(findings);item.findings=item.findings.GroupBy(f=>f.code+"/"+f.objectId+"/"+f.profile).Select(g=>g.First()).ToList();
            bool stalled=RecordReviewProgress(item,item.findings)||item.stalled>=2;
            item.review=new ReviewPacket{runId=_run.id,levelId=item.levelId,inputHash=item.inputHash,documentHash=item.documentHash,round=item.round,frames=frames,findings=item.findings};
            item.reviewHash=WorldOpsStorage.Digest(item.review);WorldOpsStorage.Json(Path.Combine(LevelFolder(_run,item.levelId),"review-"+item.round+".json"),item.review);
            item.status=stalled?"exception":"review";if(stalled)item.error="The same visual defect did not improve in two correction rounds.";_run.nativeState=null;_run.nativeLevel=null;
            WorldOpsStorage.Json(Path.Combine(Home,"cache",item.cacheKey+".json"),item);
        }
        public static bool RecordReviewProgress(WorldLevelRun item,List<ValidationFinding> current)
        {
            string Key(ValidationFinding f)=>f.code+"/"+f.objectId+"/"+f.profile;
            var previous=item.previousReviewFindings.GroupBy(Key).ToDictionary(g=>g.Key,g=>g.First());
            var active=new HashSet<string>();
            foreach(var finding in current.Where(f=>f.severity=="error"))
            {
                string key=Key(finding);active.Add(key);
                if(!previous.TryGetValue(key,out var before)){item.visualStalls[key]=0;continue;}
                bool improved=finding.code=="landmark.occluded"?finding.measured>before.measured+.001f:
                    finding.measured<before.measured-.001f;
                item.visualStalls.TryGetValue(key,out int attempts);item.visualStalls[key]=improved?0:attempts+1;
            }
            foreach(var key in item.visualStalls.Keys.Where(k=>!active.Contains(k)).ToArray())item.visualStalls.Remove(key);
            return item.visualStalls.Values.Any(n=>n>=2);
        }
    }
}
