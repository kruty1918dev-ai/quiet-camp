#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Newtonsoft.Json;
using QuietCamp.Domain;
using QuietCamp.Infrastructure;
using QuietCamp.Presentation.World;
using QuietCamp.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace QuietCamp.Editor
{
    [InitializeOnLoad]
    public static partial class QC007ArtCapture
    {
        sealed class Request { public string mode, output, name, levelId, profile, draftPath; public int width = 1536, height = 1024, seed = 12007; }
        static readonly string RequestPath = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "../Temp/ai/qc007-art-request.json"));
        static readonly string ResultPath = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "../Temp/ai/qc007-art-result.json"));
        static readonly string GamePath = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "../Temp/ai/qc007-game-session.json"));
        static QC007ArtCapture() => EditorApplication.update += Poll;
        [MenuItem("Quiet Camp/Майстерня/Стан перевірки редактора")]
        static void AuthoringProbe()
        {
            File.WriteAllText(ResultPath,JsonConvert.SerializeObject(new{status="editor-probe",compiling=EditorApplication.isCompiling,updating=EditorApplication.isUpdating,requestPath=RequestPath,pending=File.Exists(RequestPath)}));
            Poll();
        }
        [MenuItem("Quiet Camp/Майстерня/Перевірити інтеграцію")]
        static void AuthoringTestsFromMenu()
        {
            if(EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode || EditorUtility.scriptCompilationFailed)return;
            RunTests(true);
        }
        static LevelAuthoring.LevelAuthoringWorkspace _worldWorkspace;
        static Request _worldRequest;
        static LevelAuthoring.OpeningCampaignAuthoring _openingBuild;
        static LevelAuthoring.CampaignWorkshopRegeneration _campaignBatch;
        static void Poll()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if(_campaignBatch!=null)
            {
                try
                {
                    if(_campaignBatch.Completed<_campaignBatch.Total){_campaignBatch.Step();File.WriteAllText(ResultPath,JsonConvert.SerializeObject(new{status="campaign-generating",completed=_campaignBatch.Completed,total=_campaignBatch.Total}));}
                    else{_campaignBatch.Publish();File.WriteAllText(ResultPath,File.ReadAllText(Path.Combine(_campaignBatch.Output,"published.json")));_campaignBatch=null;}
                }
                catch(Exception e){File.WriteAllText(ResultPath,JsonConvert.SerializeObject(new{status="error",error=e.ToString()}));_campaignBatch=null;}
                return;
            }
            if(_worldWorkspace!=null)
            {
                if(_worldWorkspace.River!=null && _worldWorkspace.River.IsUpdating)return;
                try
                {
                    var d=_openingBuild!=null?_openingBuild.Finish(_worldWorkspace):_worldWorkspace.Capture(true);d.Save(_worldRequest.draftPath);
                    File.WriteAllText(ResultPath,JsonConvert.SerializeObject(new{status="world-demo",draftPath=_worldRequest.draftPath,errors=d.Validate(true),workspaceObjects=d.art.instances.Length,baked=d.art.landscape!=null,painted=_openingBuild?.Painted,backend=_openingBuild!=null?LevelAuthoring.PlacementToolIntegration.Controlled.Backend:null}));
                }
                catch(Exception e){File.WriteAllText(ResultPath,JsonConvert.SerializeObject(new{status="error",error=e.ToString()}));}
                finally{_worldWorkspace.Dispose();_worldWorkspace=null;_worldRequest=null;_openingBuild=null;}
                return;
            }
            if (File.Exists(GamePath)) PollGame();
            if (!File.Exists(RequestPath)) return;
            try
            {
                var request = JsonConvert.DeserializeObject<Request>(File.ReadAllText(RequestPath));
                File.Delete(RequestPath);
                if (UnityEditor.EditorUtility.scriptCompilationFailed) throw new InvalidOperationException("Capture refused: Unity script compilation failed; loaded assemblies may be stale.");
                if (request.mode == "capture" || request.mode == "draft-capture") Capture(request);
                else if (request.mode == "props-demo")
                {
                    var doc = LevelAuthoring.PropDemonstrations.Create(request.profile);
                    doc.Save(request.draftPath);
                    File.WriteAllText(ResultPath, JsonConvert.SerializeObject(new { status = "props-demo", draftPath = request.draftPath, errors = doc.Validate(true) }));
                }
                else if(request.mode=="aging-ui")
                {
                    var window=LevelAuthoring.LevelAuthoringWindow.OpenWindow();var session=window.PrepareVerification();
                    File.WriteAllText(Path.Combine("Temp/ai","aging-ui-original.json"),JsonConvert.SerializeObject(new{session.document,session.path,session.dirty}));
                    window.SetDocument(LevelAuthoring.LevelAuthoringDocument.Open(request.draftPath));window.ShowPage(6);
                    Selection.activeGameObject=window.Workspace.Scenery.GetComponentsInChildren<LevelAuthoring.LevelAuthoringAsset>().First(m=>m.instanceId=="crash/airframe/tail").gameObject;
                    window.position=new Rect(50,70,1120,1000);
                    File.WriteAllText(ResultPath,JsonConvert.SerializeObject(new{status="aging-ui-ready"}));
                }
                else if(request.mode=="aging-ui-restore")
                {
                    var original=Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText("Temp/ai/aging-ui-original.json"));
                    LevelAuthoring.LevelAuthoringWindow.OpenWindow().RestoreVerification(new LevelAuthoring.LevelAuthoringWindow.VerificationSession{document=original["document"].ToObject<LevelAuthoring.LevelAuthoringDocument>(),path=(string)original["path"],dirty=(bool)original["dirty"]});
                    File.WriteAllText(ResultPath,JsonConvert.SerializeObject(new{status="aging-ui-restored"}));
                }
                else if(request.mode=="object-overgrowth")
                {
                    var d=LevelAuthoring.LevelAuthoringDocument.Open(request.draftPath);
                    var settings=string.IsNullOrEmpty(request.profile)?LevelAuthoring.ObjectOvergrowthSettings.Defaults():JsonConvert.DeserializeObject<LevelAuthoring.ObjectOvergrowthSettings>(File.ReadAllText(request.profile));
                    var ids=d.art.instances.Where(i=>i.id=="crash/airframe/nose"||i.id=="crash/airframe/tail"||i.id=="crash/wing").Select(i=>i.id).ToArray();
                    int plants=LevelAuthoring.ObjectOvergrowthAuthoring.Apply(d,ids,settings);d.Save(request.output);
                    File.WriteAllText(ResultPath,JsonConvert.SerializeObject(new{status="object-overgrowth",addedPlants=plants,objects=d.art.instances.Length,errors=d.Validate(true)}));
                }
                else if(request.mode=="forest-regrowth")
                {
                    var d=LevelAuthoring.LevelAuthoringDocument.Open(request.draftPath);
                    var settings=string.IsNullOrEmpty(request.profile)?LevelAuthoring.ForestRegrowthAuthoring.Defaults():JsonConvert.DeserializeObject<LevelAuthoring.ForestRegrowthSettings>(File.ReadAllText(request.profile));
                    int plants=LevelAuthoring.ForestRegrowthAuthoring.Apply(d,settings);d.art.blurPixels=3f;d.Save(request.output);
                    File.WriteAllText(ResultPath,JsonConvert.SerializeObject(new{status="forest-regrowth",addedPlants=plants,objects=d.art.instances.Length,errors=d.Validate(true)}));
                }
                else if(request.mode=="crash-compose")
                {
                    var proof=LevelAuthoring.AircraftCrashAuthoring.BuildThroughPwb(request.draftPath,request.profile,request.output);
                    File.WriteAllText(ResultPath,JsonConvert.SerializeObject(proof));
                }
                else if(request.mode=="opening-details")File.WriteAllText(ResultPath,JsonConvert.SerializeObject(LevelAuthoring.OpeningCampaignAuthoring.AddDetails(request.draftPath,request.profile,request.output)));
                else if(request.mode=="opening-gameplay")File.WriteAllText(ResultPath,JsonConvert.SerializeObject(LevelAuthoring.OpeningCampaignGameplayProbe.Run(request.name,request.output)));
                else if(request.mode=="opening-water-tests")RunTests(true,"QuietCamp.Tests.CampArtCompositionTests");
                else if(request.mode=="opening-tests") RunTests(true,"QuietCamp.Tests.OpeningCampaignTests");
                else if(request.mode=="opening-compose")
                {
                    _openingBuild=new LevelAuthoring.OpeningCampaignAuthoring(request.profile);
                    _worldWorkspace=new LevelAuthoring.LevelAuthoringWorkspace(_openingBuild.Document);_worldRequest=request;
                }
                else if(request.mode=="authoring-trim")
                {
                    long before=UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong();
                    System.GC.Collect();System.GC.WaitForPendingFinalizers();System.GC.Collect();
                    EditorUtility.UnloadUnusedAssetsImmediate();
                    File.WriteAllText(ResultPath,JsonConvert.SerializeObject(new{status="trimmed",allocatedBefore=before,allocatedAfter=UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong()}));
                }
                else if(request.mode=="opening-apply")
                {
                    var doc=LevelAuthoring.LevelAuthoringDocument.Open(request.draftPath);
                    LevelAuthoring.LevelAuthoringCampaign.Apply(doc);
                    doc.Save(Path.Combine("Assets/QuietCamp/Authoring/CampaignLevels",doc.sourceLevelId.Replace(':','_')+".draft.json"));
                    File.WriteAllText(ResultPath,JsonConvert.SerializeObject(new{status="opening-applied",id=doc.sourceLevelId,errors=doc.Validate(true)}));
                }
                else if(request.mode=="world-demo")
                {
                    var doc=LevelAuthoring.WorldStudyFactory.Create(request.profile);
                    LevelAuthoring.WorldStudyFactory.EnableTerrain(doc,request.profile);
                    _worldWorkspace=new LevelAuthoring.LevelAuthoringWorkspace(doc);_worldRequest=request;
                }
                else if(request.mode=="world-export")
                {
                    var doc=LevelAuthoring.LevelAuthoringDocument.Open(request.draftPath);
                    doc.Export(request.output);
                    File.WriteAllText(ResultPath,JsonConvert.SerializeObject(new {status="world-export",output=request.output,errors=doc.Validate(true)}));
                }
                else if(request.mode=="mood-demo" || request.mode=="season-variant")
                {
                    var doc=LevelAuthoring.LevelAuthoringDocument.Open(request.draftPath);
                    if(request.mode=="mood-demo")LevelAuthoring.AtmosphereDemoComposition.Prepare(doc);
                    LevelAuthoring.LevelAuthoringPalette.EnsureDocumentModels(doc.art.models);SeasonAtmosphereCatalog.Reset();
                    LevelAuthoring.SeasonAtmosphereAuthoring.Apply(doc,SeasonAtmosphereCatalog.Get(request.profile));doc.art.cameraAspect=1;if(request.mode=="mood-demo")doc.art.orthographicSize=11.5f;
                    string path=Path.Combine(request.output,request.name+".draft.json");doc.Save(path);
                    File.WriteAllText(ResultPath,JsonConvert.SerializeObject(new{status="mood-demo",draftPath=path,errors=doc.Validate(true)}));
                }
                else if (request.mode == "props-play")
                {
                    LevelAuthoring.LevelAuthoringPlayDriver.Start(LevelAuthoring.LevelAuthoringDocument.Open(request.draftPath));
                    File.WriteAllText(ResultPath, "{\"status\":\"props-play-started\"}");
                }
                else if(request.mode=="authoring-focus-game")
                {
                    if(EditorApplication.isPlaying)EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView")).Focus();
                    // Exercise the focused-app audio path in our memory-only test.
                    // Other desktop sessions can move OS focus between native requests.
                    if(EditorAuthoringPreview.Active&&QuietCampBootstrap.ServicesRef?.Save.MemoryOnly==true)
                    {
                        var bootstrap=UnityEngine.Object.FindAnyObjectByType<QuietCampBootstrap>();
                        bootstrap?.SendMessage("OnApplicationPause",false,SendMessageOptions.RequireReceiver);
                        bootstrap?.SendMessage("OnApplicationFocus",true,SendMessageOptions.RequireReceiver);
                    }
                    File.WriteAllText(ResultPath,JsonConvert.SerializeObject(new{status="game-focus",playing=EditorApplication.isPlaying,audioPaused=AudioListener.pause,focusSimulated=EditorAuthoringPreview.Active}));
                }
                else if(request.mode=="forest-motion")new LevelAuthoring.WeatherMotionCapture(request.output,ResultPath,true);
                else if(request.mode=="weather-motion")new LevelAuthoring.WeatherMotionCapture(request.output,ResultPath);
                else if (request.mode == "props-game-status")
                {
                    var draft = LevelAuthoring.LevelAuthoringDocument.Open(request.draftPath);
                    var view = UnityEngine.Object.FindObjectsByType<CampArtView>(FindObjectsSortMode.None).FirstOrDefault(v => v.Definition?.levelId == draft.level.id);
                    var foreign = SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<Component>(true))
                        .Where(c => c != null && new[] { "Sirenix", "PluginMaster", "JBooth.MicroVerse", "Staggart.RiverModeler", "QuietCamp.Editor.LevelAuthoring" }.Any(n => (c.GetType().Namespace ?? "").StartsWith(n,StringComparison.Ordinal)))
                        .Select(c => c.GetType().FullName).ToArray();
                    File.WriteAllText(ResultPath,JsonConvert.SerializeObject(new { status="props-game-status",playing=EditorApplication.isPlaying,
                        ready=CampSceneHost.Current?.UiReady,memoryOnly=QuietCampBootstrap.ServicesRef?.Save.MemoryOnly,
                        identical=view!=null && JsonConvert.SerializeObject(view.Definition)==JsonConvert.SerializeObject(draft.art),
                        tents=UnityEngine.Object.FindObjectsByType<TentCloth>(FindObjectsSortMode.None).Select(t=>new{name=t.name,t.SnowCover,t.Wetness,materials=t.GetComponentsInChildren<MeshRenderer>().SelectMany(r=>r.sharedMaterials).Where(m=>m!=null).Distinct().Select(m=>new{name=m.name,shader=m.shader.name,snow=m.HasProperty("_SnowCover")?m.GetFloat("_SnowCover"):-1}).ToArray()}).ToArray(),
                        shelter=CampSceneHost.Current?.Atmosphere?.RainShelter!=null,
                        objects=view?.Definition.instances.Length, atmosphere=view?.SeasonAtmosphere?.Preset.id, particles=view?.SeasonAtmosphere?.LiveParticles, voices=view?.SeasonAtmosphere?.OwnedVoices, playingVoices=view?.SeasonAtmosphere?.PlayingVoices, audioPaused=AudioListener.pause, dsp=AudioSettings.dspTime, forbiddenComponents=foreign, persistentDataPath=UnityEngine.Application.persistentDataPath }));
                }
                else if (request.mode == "tests") RunTests();
                else if (request.mode == "authoring-tests") RunTests(true);
                else if(request.mode=="surface-tests")RunTests(true,"QuietCamp.Tests.CampSurfaceUseTests");
                else if(request.mode=="forest-tests")RunTests(true,"QuietCamp.Tests.ForestAtmosphereTests");
                else if(request.mode=="props-tests")RunTests(true,"QuietCamp.Tests.AuthoringPropTests");
                else if (request.mode == "world-tests") RunTests(true,"QuietCamp.Tests.ModelWorkshopTests");
                else if(request.mode=="mood-tests")RunTests(true,"QuietCamp.Tests.SeasonAtmosphereTests");
                else if(request.mode=="campaign-regenerate"||request.mode=="campaign-weather-upgrade")_campaignBatch=new LevelAuthoring.CampaignWorkshopRegeneration(request.output,request.mode=="campaign-weather-upgrade");
                else if (request.mode == "terrain-tests") RunTests(true, "QuietCamp.Tests.LevelAuthoringV2Tests.RiverAndTerrainBakeAndRestoreWithoutVendorRuntimeComponents");
                else if (request.mode == "gallery-tests") RunTests(true, "QuietCamp.Tests.LevelAuthoringV2Tests.CampaignGalleryOpensAllHundredPublishedPuzzles");
                else if (request.mode == "authoring-profile") BeginAuthoringProfile();
                else if (request.mode == "authoring-workspace-status")
                {
                    var window = LevelAuthoring.LevelAuthoringWindow.OpenWindow();
                    if(window.Workspace==null)
                    {
                        File.WriteAllText(ResultPath,"{\"status\":\"workspace-empty\"}");return;
                    }
                    var snapshot = window.Workspace.Capture(false);
                    File.WriteAllText(ResultPath, JsonConvert.SerializeObject(new { status = "workspace", busy = window.Workspace.River?.IsUpdating ?? false,
                        objects = snapshot.art.instances.Length, baked = snapshot.art.landscape != null, errors = snapshot.Validate(true),
                        artSha256=Hash(System.Text.Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(snapshot.art))),
                        legacyArtSha256=Hash(System.Text.Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(snapshot.art).Replace(",\"primaryLandmarkId\":null","").Replace(",\"primaryLandmarkInstanceId\":null",""))) }));
                }
                else if (request.mode == "authoring-open") LevelAuthoring.LevelAuthoringWindow.OpenWindow().NewDocument(false);
                else if(request.mode=="authoring-load-draft")
                {
                    var restored=LevelAuthoring.LevelAuthoringDocument.Open(request.draftPath);
                    if(request.profile=="legacy-annotations")LevelAuthoring.SeasonAtmosphereAuthoring.Annotate(restored);
                    LevelAuthoring.LevelAuthoringWindow.OpenWindow().SetDocument(restored);
                    File.WriteAllText(ResultPath,"{\"status\":\"draft-restored\"}");
                }
                else if (request.mode == "authoring-recover") LevelAuthoring.LevelAuthoringWindow.OpenWindow().RestoreAfterPlay();
                else if (request.mode == "authoring-load-smoke")
                    LevelAuthoring.LevelAuthoringWindow.OpenWindow().SetDocument(LevelAuthoring.LevelAuthoringDocument.Open(Path.Combine(UnityEngine.Application.dataPath, "../Temp/ai/authoring-smoke-draft.json")));
                else if (request.mode == "authoring-page")
                {
                    var window = LevelAuthoring.LevelAuthoringWindow.OpenWindow();
                    if (window.Workspace == null) window.SetDocument(LevelAuthoring.LevelAuthoringCampaign.Open("QC007"));
                    window.ShowPage(request.seed); window.position = new Rect(80, 100, 950, 760);
                    if (request.profile == "grass") window.ShowPaletteCategory(2);
                    if (request.profile == "fence") window.ShowPaletteCategory(5);
                    if (request.profile == "props" || request.profile == "landmarks" || request.profile == "parts") window.ShowPropPalette(request.profile != "props",request.profile == "parts");
                    if (request.profile == "props-camp") window.ShowPropPack(1);
                    if (request.profile == "props-village") window.ShowPropPack(2);
                    if (request.profile == "props-river") window.ShowPropPack(3);
                    if (request.profile == "props-ruins") window.ShowPropPack(4);
                    File.WriteAllText(ResultPath, JsonConvert.SerializeObject(new { status = "authoring-page", page = request.seed }));
                }
                else if (request.mode == "authoring-terrain")
                {
                    var document = LevelAuthoring.LevelAuthoringCampaign.Open("QC007");
                    document.terrain = LevelAuthoring.RiverTerrainAuthoringData.FromLegacy(document.art);
                    document.terrain.stamps = new[] { new LevelAuthoring.LandscapeStamp { x = -8, z = -8, radius = 3, height = 1 } };
                    var window = LevelAuthoring.LevelAuthoringWindow.OpenWindow(); window.SetDocument(document); window.ShowPage(3);
                    File.WriteAllText(ResultPath, "{\"status\":\"terrain-started\"}");
                }
                else if (request.mode == "authoring-play" || request.mode == "authoring-current-play")
                {
                    var window=LevelAuthoring.LevelAuthoringWindow.OpenWindow();
                    if (request.mode == "authoring-play") window.SetDocument(LevelAuthoring.LevelAuthoringDocument.Create(true));
                    var snapshot=window.Workspace.Capture();
                    File.WriteAllText(Path.Combine(UnityEngine.Application.dataPath,"../Temp/ai/authoring-smoke-draft.json"),JsonConvert.SerializeObject(snapshot));
                    LevelAuthoring.LevelAuthoringPlayDriver.Start(snapshot);
                }
                else if (request.mode == "authoring-status") File.WriteAllText(ResultPath, JsonConvert.SerializeObject(new { status = "authoring-status", playing = EditorApplication.isPlaying, memoryOnly = QuietCampBootstrap.ServicesRef?.Save.MemoryOnly, level = CampSceneHost.Current?.Session?.Level?.id, ready = CampSceneHost.Current?.UiReady, scene = SceneManager.GetActiveScene().name }));
                else if (request.mode == "authoring-stop") EditorApplication.ExitPlaymode();
                else if(request.mode=="authoring-place-witness")
                {
                    if(!EditorApplication.isPlaying||!EditorAuthoringPreview.Active||QuietCampBootstrap.ServicesRef?.Save.MemoryOnly!=true)throw new InvalidOperationException("Requires a memory-only authoring preview.");
                    var session=CampSceneHost.Current?.Session??throw new InvalidOperationException("Camp is not ready.");session.DebugApplyWitness();
                    File.WriteAllText(ResultPath,JsonConvert.SerializeObject(new{status="witness-placed"}));
                }
                else if (request.mode == "authoring-complete")
                {
                    if(!EditorApplication.isPlaying||!EditorAuthoringPreview.Active||QuietCampBootstrap.ServicesRef?.Save.MemoryOnly!=true)
                        throw new InvalidOperationException("Only an isolated authoring preview can run this smoke check.");
                    var session=CampSceneHost.Current?.Session??throw new InvalidOperationException("Camp is not ready.");
                    var services=QuietCampBootstrap.ServicesRef;
                    var progress=JsonConvert.SerializeObject(services.Save.Progress);var album=JsonConvert.SerializeObject(services.Save.Album);
                    if(request.name!="placed")session.DebugApplyWitness();var report=session.Check();
                    var view=UnityEngine.Object.FindAnyObjectByType<CampArtView>();
                    File.WriteAllText(ResultPath,JsonConvert.SerializeObject(new {status="authoring-completion",solved=report.IsSolved,completed=session.IsCompleted,
                        memoryOnly=services.Save.MemoryOnly,progressUnchanged=progress==JsonConvert.SerializeObject(services.Save.Progress),albumUnchanged=album==JsonConvert.SerializeObject(services.Save.Album),
                        frozenArtMatches=view!=null&&JsonConvert.SerializeObject(view.Definition)==JsonConvert.SerializeObject(EditorAuthoringPreview.Current.art)}));
                }
                else if (request.mode == "authoring-restored")
                {
                    var window=LevelAuthoring.LevelAuthoringWindow.OpenWindow();
                    var before=LevelAuthoring.LevelAuthoringDocument.Open(Path.Combine(UnityEngine.Application.dataPath,"../Temp/ai/authoring-smoke-draft.json"));
                    if(window.Workspace!=null)window.Workspace.Capture().Save(Path.Combine(UnityEngine.Application.dataPath,"../Temp/ai/authoring-smoke-restored.json"));
                    File.WriteAllText(ResultPath,JsonConvert.SerializeObject(new {status="authoring-restored",sceneObjects=window.Workspace?.SceneryCount,
                        identical=window.Workspace!=null&&JsonConvert.SerializeObject(before.art)==JsonConvert.SerializeObject(window.Workspace.Capture().art)}));
                }
                else if (request.mode == "game-start") StartGame();
                else if (request.mode == "game-shot") GameShot(request);
                else if (request.mode == "game-stop") { SetGameStage("stopping"); EditorApplication.ExitPlaymode(); }
                else File.WriteAllText(ResultPath, JsonConvert.SerializeObject(new { status = "ready", playing = EditorApplication.isPlaying, unity = UnityEngine.Application.unityVersion }, Formatting.Indented));
            }
            catch (IOException) when (File.Exists(RequestPath)) { /* Retry a producer/scanner sharing lock on the next Editor update. */ }
            catch (Exception error) { SessionState.SetBool("QuietCamp.Authoring.Verifying", false); File.WriteAllText(ResultPath, JsonConvert.SerializeObject(new { status = "error", error = error.ToString() })); }
        }
        sealed class GameState { public string stage, saveRoot, backupRoot; public string[] files; }
        static void StartGame()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("The existing Play session must be left intact.");
            string saves = Path.Combine(UnityEngine.Application.persistentDataPath, "saves");
            string backup = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "../Temp/ai/qc007-save-backup"));
            Directory.CreateDirectory(backup);
            var files = Directory.Exists(saves) ? Directory.GetFiles(saves).Select(Path.GetFileName).ToArray() : Array.Empty<string>();
            foreach (var file in files) File.Copy(Path.Combine(saves, file), Path.Combine(backup, file), true);
            File.WriteAllText(GamePath, JsonConvert.SerializeObject(new GameState { stage = "boot", saveRoot = saves, backupRoot = backup, files = files }));
            File.WriteAllText(ResultPath, "{\"status\":\"starting-game\"}");
            EditorApplication.EnterPlaymode();
        }
        static void SetGameStage(string stage)
        {
            var state = JsonConvert.DeserializeObject<GameState>(File.ReadAllText(GamePath)); state.stage = stage;
            File.WriteAllText(GamePath, JsonConvert.SerializeObject(state));
        }
        static void PollGame()
        {
            var state = JsonConvert.DeserializeObject<GameState>(File.ReadAllText(GamePath));
            if (!EditorApplication.isPlaying)
            {
                if (state.stage != "stopping") return;
                Directory.CreateDirectory(state.saveRoot);
                foreach (var file in Directory.GetFiles(state.saveRoot))
                    if (!state.files.Contains(Path.GetFileName(file))) File.Delete(file);
                foreach (var file in state.files) File.Copy(Path.Combine(state.backupRoot, file), Path.Combine(state.saveRoot, file), true);
                File.Delete(GamePath); File.WriteAllText(ResultPath, "{\"status\":\"game-stopped-saves-restored\"}"); return;
            }
            if (state.stage == "boot")
            {
                var services = QuietCampBootstrap.ServicesRef;
                if (services == null) return;
                if (MenuSceneHost.Current == null || !MenuSceneHost.Current.UiReady) return;
                services.Progression.Restore(LevelLoader.MvpLevelIds().Take(6), "QC006", services.Progression.CosmeticFlags);
                services.Save.Session = new QuietCamp.Application.SessionSaveData();
                services.PendingLevelId = "QC007"; services.PendingMenuScreen = null;
                SetGameStage("camp"); SceneManager.LoadSceneAsync("Camp");
            }
            else if (state.stage == "camp" && CampSceneHost.Current != null && CampSceneHost.Current.UiReady)
            {
                SetGameStage("ready");
                File.WriteAllText(ResultPath, JsonConvert.SerializeObject(new { status = "game-ready", level = CampSceneHost.Current.Session.Level.id,
                    art = UnityEngine.Object.FindAnyObjectByType<CampArtView>() != null, scene = SceneManager.GetActiveScene().name }, Formatting.Indented));
            }
        }
        static void GameShot(Request request)
        {
            if (!EditorApplication.isPlaying || CampSceneHost.Current?.Session?.Level?.id != "QC007") throw new InvalidOperationException("QC007 Gameplay is not ready.");
            var host = CampSceneHost.Current; var camera = UnityEngine.Object.FindAnyObjectByType<CampAtmosphere>().WorldCamera;
            var view = camera.GetComponent<CampArtView>();
            var board = typeof(CampSceneHost).GetField("_renderer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(host) as BoardRenderer;
            board.SyncPlacements(host.Session.Level.witness, host.Session.Level, 0, true);
            board.ShowShade(false); board.HidePath();
            var overlays = typeof(BoardRenderer).GetField("_overlayRoot", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(board) as Transform;
            bool overlayVisible = overlays.gameObject.activeSelf; overlays.gameObject.SetActive(false);
            var previousTarget = camera.targetTexture; float previousAspect = camera.aspect; var previousActive = RenderTexture.active;
            var texture = new RenderTexture(request.width, request.height, 24, RenderTextureFormat.ARGB32); texture.Create();
            var image = new Texture2D(request.width, request.height, TextureFormat.RGB24, false);
            try
            {
                camera.aspect = (float)request.width / request.height; view.FitCamera(new Rect(0, 0, 1, 1));
                camera.targetTexture = texture;
                var render = new UniversalRenderPipeline.SingleCameraRequest { destination = texture };
                RenderPipeline.SubmitRenderRequest(camera, render); RenderPipeline.SubmitRenderRequest(camera, render);
                RenderTexture.active = texture; image.ReadPixels(new Rect(0, 0, request.width, request.height), 0, 0); image.Apply();
                var bytes = image.EncodeToPNG(); Directory.CreateDirectory(request.output);
                string output = Path.Combine(request.output, request.name + ".png"); File.WriteAllBytes(output, bytes);
                var result = new { status = "game-captured", kind = "native-live-gameplay-camera", output, imageSha256 = Hash(bytes),
                    level = host.Session.Level.id, contentHash = host.Session.Level.contentHash, scene = SceneManager.GetActiveScene().name,
                    width = request.width, height = request.height, artSha256 = Hash(File.ReadAllBytes("Assets/QuietCamp/Resources/QuietCamp/ArtCompositions/QC007.json")),
                    profile = view.Definition.generation, seed = view.Definition.seed, effectiveSeed = view.Definition.generationSeed,
                    review = view.Definition.review, attempts = view.Definition.attempts, sourceReceipt = SourceReceipt(view.Definition),
                    cameraEuler = view.Definition.cameraEuler, camera.orthographicSize, capturedUtc = DateTime.UtcNow.ToString("o"), playerBuild = false };
                string serialized = JsonConvert.SerializeObject(result, Formatting.Indented);
                File.WriteAllText(Path.Combine(request.output, request.name + ".json"), serialized); File.WriteAllText(ResultPath, serialized);
            }
            finally
            {
                camera.targetTexture = previousTarget; camera.aspect = previousAspect; RenderTexture.active = previousActive;
                overlays.gameObject.SetActive(overlayVisible);
                texture.Release(); UnityEngine.Object.DestroyImmediate(texture); UnityEngine.Object.DestroyImmediate(image);
            }
        }
        static void RunTests(bool authoring = false, string focused = null)
        {
            if (authoring) SessionState.SetBool("QuietCamp.Authoring.Verifying", true);
            var window = authoring ? LevelAuthoring.LevelAuthoringWindow.OpenWindow() : null;
            var session = window?.PrepareVerification();
            if(session!=null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ResultPath));
                File.WriteAllText(Path.Combine(Path.GetDirectoryName(ResultPath),"authoring-before-tests.json"),JsonConvert.SerializeObject(new{document=session.document,path=session.path,dirty=session.dirty}));
            }
            if (authoring) LevelAuthoring.LevelAuthoringWorkspace.CloseOrphanedScenes();
            if (authoring)
                for (int i = 0; i < SceneManager.sceneCount; i++)
                {
                    var scene = SceneManager.GetSceneAt(i);
                    if (!scene.isLoaded || !scene.isDirty || !string.IsNullOrEmpty(scene.path)) continue;
                    // Preserve an unsaved scene as its own local asset instead of
                    // asking TestRunner to discard it or restore an absent path.
                    const string backups = "Assets/QuietCamp/Editor/LevelAuthoring/SceneBackups";
                    Directory.CreateDirectory(backups);
                    if (!EditorSceneManager.SaveScene(scene, backups + "/PreservedUntitled-" + Guid.NewGuid().ToString("N") + ".unity"))
                        throw new IOException("Could not preserve the unsaved Editor scene before verification.");
                }
            File.WriteAllText(ResultPath,JsonConvert.SerializeObject(new{status="tests-running",authoring,started=DateTime.UtcNow}));
            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            api.RegisterCallbacks(new TestReport(api, session));
            api.Execute(new ExecutionSettings(new Filter { testMode = TestMode.EditMode,
                testNames = focused != null ? new[] { focused } : authoring ? new[] { "QuietCamp.Tests.TutorialDirectorTests", "QuietCamp.Tests.OpeningCampaignTests", "QuietCamp.Tests.SeasonPresentationTests", "QuietCamp.Tests.BiomeSurfaceTests", "QuietCamp.Tests.CampSurfaceUseTests", "QuietCamp.Tests.SeasonAtmosphereTests", "QuietCamp.Tests.SurfaceWeatheringTests", "QuietCamp.Tests.ForestAtmosphereTests", "QuietCamp.Tests.ModelWorkshopTests", "QuietCamp.Tests.FreeWorldAssetsTests", "QuietCamp.Tests.AuthoringPropTests", "QuietCamp.Tests.LevelAuthoringTests", "QuietCamp.Tests.LevelAuthoringV2Tests", "QuietCamp.Tests.CampArtCompositionTests", "QuietCamp.Tests.RulesTests", "QuietCamp.Tests.LiveCampContentTests" } : new[] { "QuietCamp.Tests.CampArtCompositionTests", "QuietCamp.Tests.RulesTests", "QuietCamp.Tests.LiveCampContentTests" } }));
        }
        sealed class TestReport : ICallbacks
        {
            readonly TestRunnerApi _api;
            readonly LevelAuthoring.LevelAuthoringWindow.VerificationSession _session;
            readonly System.Collections.Generic.List<object> _failures = new System.Collections.Generic.List<object>();
            public TestReport(TestRunnerApi api, LevelAuthoring.LevelAuthoringWindow.VerificationSession session) { _api = api; _session = session; }
            public void RunStarted(ITestAdaptor testsToRun) { }
            public void TestStarted(ITestAdaptor test) { }
            public void TestFinished(ITestResultAdaptor result)
            {
                if (result.TestStatus == TestStatus.Failed) _failures.Add(new { name = result.Test.FullName, result.Message, result.StackTrace });
            }
            public void RunFinished(ITestResultAdaptor result)
            {
                string report=JsonConvert.SerializeObject(new { status = "tests", outcome = result.TestStatus.ToString(),
                    passed = result.PassCount, failed = result.FailCount, skipped = result.SkipCount, duration = result.Duration, failures = _failures }, Formatting.Indented);
                _api.UnregisterCallbacks(this); UnityEngine.Object.DestroyImmediate(_api);
                if(_session==null){SessionState.SetBool("QuietCamp.Authoring.Verifying",false);File.WriteAllText(ResultPath,report);return;}
                // RunFinished precedes TestRunner's scene/window restoration. Wait
                // until it settles before restoring the user's preserved session.
                double restoreAt=EditorApplication.timeSinceStartup+1;
                EditorApplication.CallbackFunction restore=null;
                restore=()=>
                {
                    if(EditorApplication.isCompiling||EditorApplication.timeSinceStartup<restoreAt)return;
                    EditorApplication.update-=restore;
                    SessionState.SetBool("QuietCamp.Authoring.Verifying",false);
                    try{LevelAuthoring.LevelAuthoringWindow.OpenWindow().RestoreVerification(_session);File.WriteAllText(ResultPath,report);}
                    catch(Exception e){File.WriteAllText(ResultPath,JsonConvert.SerializeObject(new{status="error",error="Workspace restoration: "+e}));}
                };
                EditorApplication.update+=restore;
            }
        }
        static string Hash(byte[] bytes) { using var hash = SHA256.Create(); return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant(); }
        static object SourceReceipt(CampArtDefinition art)
        {
            var catalog = AssetCatalog.Load();
            var paths = new[] { "Assets/QuietCamp/Scripts/Infrastructure/CampArtDefinition.cs", "Assets/QuietCamp/Scripts/Infrastructure/CampCompositionGenerator.cs",
                "Assets/QuietCamp/Scripts/Presentation/World/CampArtView.cs", "Assets/QuietCamp/Scripts/Presentation/World/BoardRenderer.cs",
                "Assets/QuietCamp/Scripts/Presentation/World/CampArtEdgeBlur.cs", "Assets/QuietCamp/Scripts/Presentation/World/CampAtmosphere.cs",
                "Assets/QuietCamp/Resources/QuietCamp/CampArtLit.shader", "Assets/QuietCamp/Resources/QuietCamp/CampArtEdgeBlur.shader",
                "Assets/QuietCamp/Resources/QuietCamp/ArtCompositions/Profiles/" + art.generation + ".json",
                "Assets/QuietCamp/Settings/URP/QC_Renderer.asset", "Assets/QuietCamp/Resources/QuietCamp/ArtCompositions/CampModelLibrary.json",
                "Assets/QuietCamp/Scripts/Presentation/QuietCamp.Presentation.asmdef", "Assets/QuietCamp/Editor/QC007ArtCapture.cs",
                LevelAuthoring.AuthoringPropCatalog.ModelPath, LevelAuthoring.AuthoringPropCatalog.CatalogPath }
                .Concat(new[]{"Assets/QuietCamp/Scripts/Presentation/World/SeasonAtmosphereView.cs","Assets/QuietCamp/Scripts/Presentation/World/RiverWeatherMaterial.cs","Assets/QuietCamp/Scripts/Presentation/World/SeasonVisibilityPass.cs","Assets/QuietCamp/Scripts/Presentation/World/WeatherCoverage.cs","Assets/QuietCamp/Scripts/Presentation/World/WeatherSurfaceField.cs","Assets/QuietCamp/Scripts/Presentation/World/WeatherObjectMotion.cs","Assets/QuietCamp/Scripts/Presentation/World/SeasonSurfaceAccents.cs","Assets/QuietCamp/Resources/QuietCamp/SeasonVisibility.shader","Assets/QuietCamp/Resources/QuietCamp/SeasonLightShaft.shader","Assets/QuietCamp/Scripts/Presentation/World/SeasonLightShafts.cs","Assets/QuietCamp/Scripts/Presentation/World/SeasonModelAppearance.cs","Assets/QuietCamp/Editor/LevelAuthoring/ForestRegrowthAuthoring.cs","Assets/QuietCamp/Resources/QuietCamp/forest_regrowth.json","Assets/QuietCamp/Resources/QuietCamp/PlacementGrid.shader","Assets/QuietCamp/Resources/QuietCamp/TentCloth.shader","Assets/QuietCamp/Resources/QuietCamp/season_atmosphere.json"})
                .Concat(new[] { "scene-placement", "vegetation-placement", "rock-placement" }.SelectMany(id => Directory.GetFiles("Packages/com.kruty1918." + id + "/Runtime/Core", "*.cs")))
                .Concat(new[]{"Assets/QuietCamp/Scripts/Presentation/World/CampSurfaceUseField.cs","Assets/QuietCamp/Scripts/Presentation/World/CampGroundDetails.cs","Assets/QuietCamp/Scripts/Presentation/World/SeasonProfile.cs","Assets/QuietCamp/Resources/QuietCamp/CampSurfaceUse.hlsl","Assets/QuietCamp/Resources/QuietCamp/Meadow.shader","Assets/QuietCamp/Resources/QuietCamp/camp_surface_use.json","Assets/QuietCamp/Editor/LevelAuthoring/ObjectOvergrowthAuthoring.cs","Assets/QuietCamp/Editor/LevelAuthoring/SurfaceWeathering.cs","Assets/QuietCamp/Resources/QuietCamp/object_overgrowth.json"})
                .Concat(AssetDatabase.GetDependencies(AssetDatabase.GetAssetPath(Resources.Load<Material>(art.waterMaterial)), true))
                .Concat(AssetDatabase.GetDependencies("Assets/QuietCamp/Resources/QuietCamp/CozyVegetationLibrary.asset", true))
                .Concat(art.instances.Where(i => !string.IsNullOrEmpty(i.assetId)).Select(i => i.assetId).Distinct().SelectMany(id => AssetDatabase.GetDependencies(AssetDatabase.GetAssetPath(catalog.Prefab(id)), true)))
                .Select(path => path.Replace('\\', '/')).Where(File.Exists).Distinct().OrderBy(p => p, StringComparer.Ordinal).ToArray();
            return new { resolvedArtSha256 = Hash(System.Text.Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(art))),
                assemblies = new[] { typeof(CampArtView).Assembly, typeof(CampCompositionGenerator).Assembly }.Select(a => new { name = a.GetName().Name, moduleVersion = a.ManifestModule.ModuleVersionId.ToString(), sha256 = Hash(File.ReadAllBytes(a.Location)) }).ToArray(),
                sources = paths.Select(path => new { path, sha256 = Hash(File.ReadAllBytes(path)) }).ToArray() };
        }
        static object WaterEvidence(GameObject root)
        {
            var river = root.GetComponentsInChildren<MeshRenderer>().FirstOrDefault(r => r.name == "ExteriorStream"
                || r.sharedMaterial?.shader != null && r.sharedMaterial.shader.name.IndexOf("Water",StringComparison.OrdinalIgnoreCase)>=0);
            if(river==null)return new{present=false};
            var m = river.sharedMaterial; var mesh = river.GetComponent<MeshFilter>().sharedMesh;
            return new { shader = m.shader.name, keywords = m.shaderKeywords,
                properties = new[] { "_FogSource", "_IntersectionSource", "_FoamBaseAmount", "_FoamStrength", "_VertexColorFoam", "_SlopeFoam", "_IntersectionSharp", "_BaseColor" }.ToDictionary(k => k, k => k == "_BaseColor" ? (object)new[] { m.GetColor(k).r, m.GetColor(k).g, m.GetColor(k).b } : m.GetFloat(k)),
                normals = new[] { mesh.normals[0].y, mesh.normals[mesh.vertexCount / 2].y },
                colorMinimum = new[] { mesh.colors.Min(c => c.r), mesh.colors.Min(c => c.g), mesh.colors.Min(c => c.b), mesh.colors.Min(c => c.a) },
                colorMaximum = new[] { mesh.colors.Max(c => c.r), mesh.colors.Max(c => c.g), mesh.colors.Max(c => c.b), mesh.colors.Max(c => c.a) } };
        }
        static void Capture(Request request)
        {
            if(request.mode=="draft-capture" && LevelAuthoring.LevelAuthoringDocument.Open(request.draftPath).art.atmosphere!=null)request.width=request.height=1024;
            if (request.width < 640 || request.width > 2560 || request.height < 480 || request.height > 2560)
                throw new InvalidDataException("Capture dimensions out of range.");
            if (string.IsNullOrEmpty(request.output) || string.IsNullOrEmpty(request.name)
                || request.name.Any(c => !(char.IsLetterOrDigit(c) || c == '-' || c == '_')))
                throw new InvalidDataException("Invalid capture destination.");
            Directory.CreateDirectory(request.output);
            var draft = request.mode == "draft-capture" ? LevelAuthoring.LevelAuthoringDocument.Open(request.draftPath) : null;
            if (draft != null && draft.Validate(true).Count != 0) throw new InvalidDataException(string.Join("\n", draft.Validate(true)));
            var level = draft?.level ?? LevelLoader.Load(request.levelId ?? "QC007");
            var art = draft?.art ?? (string.IsNullOrEmpty(request.profile) ? CampArtDefinition.Load(level) : CampCompositionGenerator.Generate(level, request.profile, request.seed, out _));
            var catalog = AssetCatalog.Load();
            var oldAsync = ShaderUtil.allowAsyncCompilation;
            var oldSun = RenderSettings.sun; var oldAmbient = RenderSettings.ambientLight;
            var oldMode = RenderSettings.ambientMode; var oldFog = RenderSettings.fog;
            var oldSunDir = Shader.GetGlobalVector("_CampSunDirW");
            var oldSunColor = Shader.GetGlobalColor("_CampSunColor");
            var oldCampAmbient = Shader.GetGlobalColor("_CampAmbient");
            var preview = new PreviewRenderUtility(true);
            GameObject root = null; CampArtView view = null;
            try
            {
                ShaderUtil.allowAsyncCompilation = false;
                root = new GameObject("QC007_NativeComposition");
                Transform Child(string name) { var go = new GameObject(name); go.transform.SetParent(root.transform, false); return go.transform; }
                var board = new BoardRenderer(level, catalog, Child("Base"), Child("Grid"), Child("Obstacles"), Child("Tents"), Child("Overlay"), art);
                board.SyncPlacements(request.profile=="empty"?Array.Empty<Placement>():level.witness, level, 0, true);
                // Preview the same cloth and snow response used by the actual game.
                if(art.atmosphere!=null)foreach(var tent in board.Tents.Values)
                {
                    TentCloth.Apply(tent.Root);
                    tent.Root.GetComponent<TentCloth>()?.ApplyClimate(art.atmosphere.season,art.atmosphere.wetness,art.atmosphere.snowCover);
                }
                var decor = Child("Decor");
                var camera = preview.camera;
                camera.cameraType = CameraType.Game; camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = CampArtDefinition.Color(art.groundLow); camera.aspect = (float)request.width / request.height;
                camera.allowHDR = true; camera.allowMSAA = true;
                view = camera.gameObject.AddComponent<CampArtView>(); view.Configure(art, level, decor, catalog);
                board.SurfaceDetails.Bind(decor,previewWeather:view.SeasonAtmosphere);
                if(request.profile=="empty")board.SetPlacementGridVisible(true);
                view.FitCamera(new Rect(0, 0, 1, 1));
                preview.lights[0].type = LightType.Directional; view.ApplyLighting(preview.lights[0]);
                preview.lights[1].intensity = 0;
                preview.ambientColor = CampArtDefinition.Color(art.atmosphere?.ambientColor??art.ambientColor) * .60f;
                RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = preview.ambientColor; RenderSettings.sun = preview.lights[0]; RenderSettings.fog = false;
                Shader.SetGlobalVector("_CampSunDirW", -preview.lights[0].transform.forward);
                Shader.SetGlobalColor("_CampSunColor", preview.lights[0].color * preview.lights[0].intensity);
                Shader.SetGlobalColor("_CampAmbient", preview.ambientColor);
                foreach (var transform in root.GetComponentsInChildren<Transform>(true)) transform.gameObject.layer = 31;
                camera.GetUniversalAdditionalCameraData().volumeLayerMask = 1 << 31;
                preview.AddSingleGO(root);
                if(view.SeasonAtmosphere!=null)for(int i=0;i<180;i++)view.SeasonAtmosphere.Advance(.05f);
                if(request.profile=="used")for(int i=0;i<600;i++)board.SurfaceDetails.Advance(1,art.atmosphere?.wetness??0,art.atmosphere?.snow??0);
                else board.SurfaceDetails.Advance(.25f,art.atmosphere?.wetness??0,art.atmosphere?.snow??0,true);
                File.WriteAllText(Path.Combine(request.output,request.name+".surface.json"),JsonConvert.SerializeObject(new{mode=request.profile??"placed",acceleratedPreview=request.profile=="used",seconds=request.profile=="used"?600:0,steps=board.SurfaceDetails.Surface.Steps,residents=board.SurfaceDetails.Residents,prints=board.SurfaceDetails.PrintCount,moisture=board.SurfaceDetails.Surface.Moisture}));
                if(view.SeasonAtmosphere!=null)File.WriteAllText(Path.Combine(request.output,request.name+".effects.json"),JsonConvert.SerializeObject(new{preset=art.atmosphere.id,particles=view.SeasonAtmosphere.LiveParticles,systems=view.SeasonAtmosphere.GetComponentsInChildren<ParticleSystem>().Select(s=>new{name=s.name,count=s.particleCount}),rays=view.SeasonAtmosphere.VisibleShafts}));
                for (int frame = 0; frame < 3; frame++)
                { preview.BeginStaticPreview(new Rect(0, 0, request.width, request.height)); preview.Render(true); UnityEngine.Object.DestroyImmediate(preview.EndStaticPreview()); }
                preview.BeginStaticPreview(new Rect(0, 0, request.width, request.height)); preview.Render(true);
                var image = preview.EndStaticPreview(); var bytes = image.EncodeToPNG(); UnityEngine.Object.DestroyImmediate(image);
                string output = Path.Combine(request.output, request.name + ".png"); File.WriteAllBytes(output, bytes);
                var errors = new[] { Resources.Load<Shader>("QuietCamp/CampArtLit"), Resources.Load<Shader>("QuietCamp/CampArtEdgeBlur"), Resources.Load<Shader>("QuietCamp/SeasonVisibility"),Resources.Load<Shader>("QuietCamp/SeasonLightShaft"),Resources.Load<Shader>("QuietCamp/PlacementGrid"), Resources.Load<Shader>("QuietCamp/TentCloth"), Resources.Load<Material>(art.waterMaterial).shader }
                    .SelectMany(ShaderUtil.GetShaderMessages).Where(message => message.severity.ToString() == "Error").Select(message => message.message).ToArray();
                if (errors.Length != 0) throw new InvalidOperationException(string.Join("\n", errors));
                var receipt = new { status = "captured", kind = "native-unity-production-world-components", output,
                    unity = UnityEngine.Application.unityVersion, graphics = SystemInfo.graphicsDeviceType.ToString(), device = SystemInfo.graphicsDeviceName,
                    width = request.width, height = request.height, levelId = level.id, contentHash = level.contentHash,
                    imageSha256 = Hash(bytes), artSha256 = Hash(System.Text.Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(art))), draftPath = request.draftPath,
                    cameraEuler = art.cameraEuler, cameraPosition = new[] { camera.transform.position.x, camera.transform.position.y, camera.transform.position.z }, camera.orthographicSize,
                    waterEvidence = WaterEvidence(root), sourceMeshes = art.models.Select(model => model.id).ToArray(), instances = art.instances.Length, triangles = view.TriangleCount,
                    profile = art.generation, generationAttempt = art.generationAttempt, seed = art.seed, effectiveSeed = art.generationSeed,
                    review = art.review, attempts = art.attempts, sourceReceipt = SourceReceipt(art),
                    sunEuler = art.sunEuler, art.sunIntensity, art.blurPixels, warmupFrames = 3, capturedUtc = DateTime.UtcNow.ToString("o"), playerBuild = false, shaderErrors = errors };
                string serialized = JsonConvert.SerializeObject(receipt, Formatting.Indented);
                File.WriteAllText(Path.Combine(request.output, request.name + ".json"), serialized);
                File.WriteAllText(ResultPath, serialized);
            }
            finally
            {
                if (view != null) { view.Release(); UnityEngine.Object.DestroyImmediate(view); }
                preview.Cleanup();
                RenderSettings.sun = oldSun; RenderSettings.ambientLight = oldAmbient; RenderSettings.ambientMode = oldMode; RenderSettings.fog = oldFog;
                Shader.SetGlobalVector("_CampSunDirW", oldSunDir); Shader.SetGlobalColor("_CampSunColor", oldSunColor); Shader.SetGlobalColor("_CampAmbient", oldCampAmbient);
                ShaderUtil.allowAsyncCompilation = oldAsync;
            }
        }
    }
}
#endif

