using System;
using System.Linq;
using Newtonsoft.Json;
using QuietCamp.Domain;
using QuietCamp.Infrastructure;
using QuietCamp.Presentation;
using QuietCamp.Presentation.World;
using QuietCamp.Presentation.UI;
using Kruty1918.UIActions.API;
using UnityEditor;
using UnityEngine;

namespace QuietCamp.Editor.LevelAuthoring
{
    public static class OpeningCampaignGameplayProbe
    {
        static uint _previousWidth, _previousHeight;
        static bool _reviewResolution;

        static void RestoreReviewResolution(PlayModeStateChange state)
        {
            if(state!=PlayModeStateChange.ExitingPlayMode||!_reviewResolution)return;
            PlayModeWindow.SetCustomRenderingResolution(_previousWidth,_previousHeight,"Restored after camera review");
            _reviewResolution=false;
            EditorApplication.playModeStateChanged-=RestoreReviewResolution;
        }

        public static object Run(string action,string output=null,string levelId=null)
        {
            if(action=="start"||action=="start-menu")
            {
                var level=LevelLoader.Load(levelId??"QC001");
                LevelAuthoringPlayDriver.Start(new LevelAuthoringDocument{level=level,art=CampArtDefinition.Load(level)},true,output,action=="start-menu");
                return new{status="opening-game-starting"};
            }
            if(EditorApplication.isPlayingOrWillChangePlaymode && EditorAuthoringPreview.CampaignPreview && QuietCampBootstrap.ServicesRef==null)
                return new{status="opening-game-waiting"};
            if(!EditorApplication.isPlaying || !EditorAuthoringPreview.CampaignPreview || QuietCampBootstrap.ServicesRef?.Save.MemoryOnly!=true)
                throw new InvalidOperationException("Only the isolated campaign preview can run this probe.");
            if(action=="viewport")
            {
                var parts=(levelId??"").Split('x');
                if(parts.Length!=2||!uint.TryParse(parts[0],out var width)||!uint.TryParse(parts[1],out var height)
                    ||width<320||height<320||width>4096||height>4096)throw new ArgumentException("Expected a review size such as 750x1334.");
                var game=Resources.FindObjectsOfTypeAll<EditorWindow>().FirstOrDefault(w=>w.GetType().FullName=="UnityEditor.GameView");
                if(game==null)throw new InvalidOperationException("An existing Game View is required; no new window was opened.");
                if(!_reviewResolution)
                {
                    PlayModeWindow.GetRenderingResolution(out _previousWidth,out _previousHeight);
                    _reviewResolution=true;
                    EditorApplication.playModeStateChanged+=RestoreReviewResolution;
                }
                game.Focus();game.Repaint();
                PlayModeWindow.SetCustomRenderingResolution(width,height,"QuietCamp camera review");
                return new{status="review-viewport-requested",width,height,memoryOnly=true};
            }
            if(action=="frame")
            {
                if(string.IsNullOrEmpty(output)||!System.IO.Path.IsPathRooted(output)||!output.EndsWith(".png",StringComparison.OrdinalIgnoreCase))throw new ArgumentException("Expected an absolute PNG path.");
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(output));ScreenCapture.CaptureScreenshot(output);
                return new{status="opening-game-frame-requested",id=CampSceneHost.Current?.Session?.Level?.id,output};
            }
            if(action=="quality")
            {
                if(!int.TryParse(levelId,out int quality)||quality<0||quality>3)throw new ArgumentException("Expected quality preference 0 through 3.");
                QuietCampBootstrap.ServicesRef.Settings.quality=quality;
                return new{status="memory-quality-requested",quality,memoryOnly=true};
            }
            if(action=="simulator")
            {
                var window=Resources.FindObjectsOfTypeAll<EditorWindow>().FirstOrDefault(w=>w.GetType().FullName=="UnityEditor.DeviceSimulation.SimulatorWindow");
                if(window==null)throw new InvalidOperationException("No existing Device Simulator window; no new window was opened.");
                window.Focus();window.Repaint();return new{status="existing-simulator-focused",memoryOnly=true};
            }
            if(action=="additive-camp")
            {
                var menu=MenuSceneHost.Current;
                if(menu?.UiReady!=true)throw new InvalidOperationException("The menu must be ready before the additive-scene regression check.");
                var previewServices=QuietCampBootstrap.ServicesRef;
                if(!previewServices.CanStart(levelId))throw new InvalidOperationException("The requested level is not unlocked in this RAM-only preview.");
                foreach(var root in menu.gameObject.scene.GetRootGameObjects())root.SetActive(false);
                var auxiliary=UnityEngine.SceneManagement.SceneManager.CreateScene("QA unrelated active scene");
                UnityEngine.SceneManagement.SceneManager.SetActiveScene(auxiliary);
                previewServices.PendingLevelId=levelId;
                UnityEngine.SceneManagement.SceneManager.LoadSceneAsync("Camp",UnityEngine.SceneManagement.LoadSceneMode.Additive);
                return new{status="additive-camp-starting",memoryOnly=true};
            }
            if(action=="resume-session")
            {
                if(string.IsNullOrEmpty(output)||!System.IO.Path.IsPathRooted(output))throw new ArgumentException("Expected an absolute copied session JSON path.");
                var saved=JsonConvert.DeserializeObject<QuietCamp.Application.SessionSaveData>(System.IO.File.ReadAllText(output));
                var resumed=CampContent.SessionLevel(saved,saved.levelId);
                var savedServices=QuietCampBootstrap.ServicesRef;
                if(!savedServices.CanStart(resumed.id))throw new InvalidOperationException("The isolated preview has not unlocked this session.");
                savedServices.Save.Session=saved;
                return new{status="session-copied-to-memory",id=resumed.id,hash=resumed.contentHash,placements=saved.placements?.Length,memoryOnly=true};
            }
            if(action=="menu-continue"||action=="menu-album"||action=="menu-back")
            {
                if(MenuSceneHost.Current?.UiReady!=true)throw new InvalidOperationException("The menu is not ready.");
                string command=action=="menu-continue"?"qc.continue":action=="menu-album"?"qc.album":"qc.back";
                QuietCampBootstrap.ServicesRef.Actions.Execute(new UiActionRequest(new UiActionId(command),UiActionSource.Programmatic,"Menu"));
                return new{status="menu-action-requested",command,memoryOnly=true};
            }
            if(action=="menu-status"||action=="menu-frame")
            {
                var menu=MenuSceneHost.Current;var view=Camera.main?.GetComponent<CampArtView>();
                var menuTransition=UnityEngine.Object.FindAnyObjectByType<FoliageDiveTransition>();
                bool ready=menu!=null&&menu.UiReady&&(menuTransition==null||menuTransition.IsIdle);
                if(action=="menu-frame"&&ready)
                {
                    if(string.IsNullOrEmpty(output)||!System.IO.Path.IsPathRooted(output)||!output.EndsWith(".png",StringComparison.OrdinalIgnoreCase))throw new ArgumentException("Expected an absolute PNG path.");
                    System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(output));ScreenCapture.CaptureScreenshot(output);
                }
                return new{status="menu-review",ready,memoryOnly=true,id=menu?.BackgroundLevel?.id,authored=view!=null,
                    size=new[]{Screen.width,Screen.height},residents=UnityEngine.Object.FindObjectsByType<CampGroundDetails>(FindObjectsSortMode.None).Sum(s=>s.Residents),
                    cameraPosition=Camera.main==null?null:new[]{Camera.main.transform.position.x,Camera.main.transform.position.y,Camera.main.transform.position.z},
                    cameraEuler=Camera.main==null?null:new[]{Camera.main.transform.eulerAngles.x,Camera.main.transform.eulerAngles.y,Camera.main.transform.eulerAngles.z},
                    cameraSize=Camera.main?.orthographicSize,mask=Camera.main?.cullingMask,far=Camera.main?.farClipPlane,
                    ground=UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None).Where(r=>r.sharedMaterial!=null&&r.sharedMaterial.HasProperty("_ArtGround")&&r.sharedMaterial.GetFloat("_ArtGround")>.5f).Select(r=>new{name=r.name,active=r.gameObject.activeInHierarchy,centre=new[]{r.bounds.center.x,r.bounds.center.y,r.bounds.center.z},size=new[]{r.bounds.size.x,r.bounds.size.y,r.bounds.size.z}}).ToArray(),
                    opaqueCells=UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None).Count(r=>r.name.StartsWith("Cell_",StringComparison.Ordinal)&&r.sharedMaterial?.shader.name!="QuietCamp/PlacementGrid")};
            }
            var host=CampSceneHost.Current;
            if(host?.UiReady!=true)return new{status="opening-game-waiting",scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,host=host!=null,mask=Camera.main?.cullingMask,transition=UnityEngine.Object.FindAnyObjectByType<FoliageDiveTransition>()?.Current.ToString()};
            var services=QuietCampBootstrap.ServicesRef;var session=host.Session;string id=session.Level.id;
            if(action=="close-auxiliary")
            {
                var auxiliary=UnityEngine.SceneManagement.SceneManager.GetSceneByName("QA unrelated active scene");
                if(!auxiliary.IsValid())throw new InvalidOperationException("No owned auxiliary regression scene is loaded.");
                UnityEngine.SceneManagement.SceneManager.SetActiveScene(host.gameObject.scene);
                UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync(auxiliary);
                return new{status="owned-auxiliary-scene-closing",memoryOnly=true};
            }
            if(action=="menu")
            {
                typeof(GameServices).GetProperty(nameof(GameServices.MenuBackdrop)).SetValue(services,LevelLoader.Load(levelId??id));
                services.Actions.Execute(new UiActionRequest(new UiActionId("qc.menu"),UiActionSource.Programmatic,"Gameplay"));
                return new{status="menu-requested",memoryOnly=true};
            }
            if(action=="promo") return CampaignPromoRecorder.Begin(output);
            if(action=="composition-stills") return CampaignPromoRecorder.Begin(output, true);
            if(action=="hud")
            {
                var surface=UnityEngine.Object.FindObjectsByType<HtmlSurface>(FindObjectsSortMode.None).SingleOrDefault(s=>s.name=="CampHtml");
                var nodes=new[]{"camp","header","pause","opening-finding","bottom","tutorial","guide-portrait"}.Select(key=>
                {
                    var rect=surface?.Element(key);var corners=new Vector3[4];if(rect!=null)rect.GetWorldCorners(corners);
                    return new{id=key,exists=rect!=null,active=rect?.gameObject.activeInHierarchy??false,
                        corners=rect==null?null:corners.Select(p=>new[]{p.x,p.y}).ToArray(),
                        alpha=rect==null?0:rect.GetComponentsInParent<CanvasGroup>().Aggregate(1f,(value,group)=>value*group.alpha)};
                }).ToArray();
                var safe=Screen.safeArea;
                var parents=surface?.GetComponentsInParent<RectTransform>().Select(rect=>new{name=rect.name,
                    size=new[]{rect.rect.width,rect.rect.height},anchors=new[]{rect.anchorMin.x,rect.anchorMin.y,rect.anchorMax.x,rect.anchorMax.y},scale=new[]{rect.lossyScale.x,rect.lossyScale.y}}).ToArray();
                return new{status="opening-game-hud",id,screen=new[]{Screen.width,Screen.height},safeArea=new[]{safe.x,safe.y,safe.width,safe.height},parents,nodes};
            }
            if(action=="finding")
            {
                var surface=UnityEngine.Object.FindObjectsByType<HtmlSurface>(FindObjectsSortMode.None).Single(s=>s.name=="CampHtml");
                surface.Callbacks.Click("opening-finding");return new{status="opening-finding-requested",id};
            }
            if(action=="pro")
            {var result=services.Economy.GrantPro("qa-campaign-memory-only");return new{status="opening-game-pro",pro=services.Economy.IsPro,memoryOnly=services.Save.MemoryOnly,result=result.ToString()};}
            if(action=="check")
            {
                session.DebugApplyWitness();
                services.Actions.Execute(new UiActionRequest(new UiActionId("qc.check"),UiActionSource.Programmatic,"Gameplay"));
                return new{status="opening-game-checked",id,solved=session.IsCompleted,recorded=services.Progression.IsCompleted(id),next=services.NextLevel(id),completed=services.Save.Progress.completedIds,memoryOnly=services.Save.MemoryOnly};
            }
            if(action=="next")
            {
                if(!session.IsCompleted)throw new InvalidOperationException("Complete the current camp before Next.");
                string next=services.NextLevel(id);
                services.Actions.Execute(new UiActionRequest(new UiActionId("qc.next"),UiActionSource.Programmatic,"Gameplay"));
                return new{status="opening-game-next",from=id,to=next};
            }
            var art=UnityEngine.Object.FindAnyObjectByType<CampArtView>();
            if(action=="river-focus")
            {
                var field=new RiverSurfaceField(art.PresentedLandscape);
                var point=field.reaches.SelectMany(r=>r).OrderBy(s=>Mathf.Abs(s.centre.x-8)+Mathf.Abs(s.centre.z)).First().centre;
                var surface=UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None).First(r=>r.sharedMaterial!=null&&r.sharedMaterial.shader.name=="Stylized Water 3/Standard");
                var camera=Camera.main;camera.orthographicSize=3.4f;
                camera.transform.position=surface.transform.TransformPoint(point)-camera.transform.forward*20+camera.transform.right*1.5f;
                return new{status="river-focus",id,point=new[]{point.x,point.y,point.z}};
            }
            if(action=="river-debug")
            {
                var target=art?.RiverReflections?.ReflectionTexture;
                if(target==null)throw new InvalidOperationException("No rendered river reflection texture.");
                if(string.IsNullOrEmpty(output)||!System.IO.Path.IsPathRooted(output))throw new ArgumentException("Expected an absolute diagnostics directory.");
                System.IO.Directory.CreateDirectory(output);var previous=RenderTexture.active;
                var image=new Texture2D(target.width,target.height,TextureFormat.RGBA32,false);
                try{RenderTexture.active=target;image.ReadPixels(new Rect(0,0,target.width,target.height),0,0);image.Apply();System.IO.File.WriteAllBytes(System.IO.Path.Combine(output,"reflection.png"),image.EncodeToPNG());}
                finally{RenderTexture.active=previous;UnityEngine.Object.DestroyImmediate(image);}
                var surfaces=UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None)
                    .Where(r=>r.sharedMaterial!=null&&r.sharedMaterial.shader.name=="Stylized Water 3/Standard")
                    .Select(r=>{var m=r.sharedMaterial;var block=new MaterialPropertyBlock();r.GetPropertyBlock(block);return new{name=r.name,
                        river=m.IsKeywordEnabled("_RIVER"),worldUV=m.GetFloat("_WorldSpaceUV"),strength=m.GetFloat("_ReflectionStrength"),fresnel=m.GetFloat("_ReflectionFresnel"),normal=m.GetFloat("_NormalStrength"),speed=m.GetFloat("_Speed"),
                        normalTexture=m.GetTexture("_BumpMap")?.name,planar=block.GetFloat("_PlanarReflectionsEnabled"),reflectionTexture=block.GetTexture("_PlanarReflection")?.name};}).ToArray();
                return new{status="river-debug",id,width=target.width,height=target.height,surfaces};
            }
            var transition=UnityEngine.Object.FindAnyObjectByType<FoliageDiveTransition>();
            var scenery=GameObject.Find("AuthoredCampSurround")?.GetComponentsInChildren<MeshRenderer>()??Array.Empty<MeshRenderer>();
            var planes=Camera.main==null?null:GeometryUtility.CalculateFrustumPlanes(Camera.main);
            return new{status="opening-game-ready",id,ready=host.UiReady,memoryOnly=services.Save.MemoryOnly,activeScene=UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,hostScene=host.gameObject.scene.name,
                transitionIdle=transition==null||transition.Current==FoliageDiveTransition.State.Idle,
                cameraProof=CameraProof(session.Level,art?.Definition),
                frozenArtMatches=art!=null&&JsonConvert.SerializeObject(art.Definition)==JsonConvert.SerializeObject(CampArtDefinition.Load(session.Level)),
                artTriangles=art?.TriangleCount,artModels=art?.Definition.models.Length,artInstances=art?.Definition.instances.Length,previewArtOverride=EditorAuthoringPreview.MatchesArt(id),
                cameraMask=Camera.main?.cullingMask,
                sceneryActive=art?.PresentationActive,sceneryRenderers=scenery.Length,
                placementScene=UnityEngine.Object.FindAnyObjectByType<PlacementController>()?.gameObject.scene.name,
                listenerScenes=UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Where(l=>l.enabled).Select(l=>l.gameObject.scene.name).ToArray(),
                inFrustumSceneryRenderers=planes==null?0:scenery.Count(r=>r.enabled&&((1<<r.gameObject.layer)&Camera.main.cullingMask)!=0&&GeometryUtility.TestPlanesAABB(planes,r.bounds)),
                visibleGridCells=UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None).Count(r=>r.name.StartsWith("Cell_",StringComparison.Ordinal)&&r.sharedMaterial!=null&&r.sharedMaterial.HasProperty("_GridVisible")&&r.sharedMaterial.GetFloat("_GridVisible")>.5f),
                story=OpeningCampaignStory.For(session.Level)?.title,surfaceSteps=host.SurfaceDetails?.Surface?.Steps,residents=host.SurfaceDetails?.Residents,
                particles=art?.SeasonAtmosphere?.LiveParticles,voices=art?.SeasonAtmosphere?.OwnedVoices,
                riverReflectionRendered=art?.RiverReflections?.HasRenderedReflection,
                playingVoices=art?.SeasonAtmosphere?.PlayingVoices,wind=art?.SeasonAtmosphere?.WindStrength,
                rainContacts=art?.SeasonAtmosphere?.RainContactCount,groundRainContacts=art?.SeasonAtmosphere?.GroundRainContacts,fabricRainContacts=art?.SeasonAtmosphere?.FabricRainContacts,
                activeRainContacts=art?.SeasonAtmosphere?.RainContacts?.ActiveCount,rainRunoff=art?.SeasonAtmosphere?.RainContacts?.RunoffCount,
                cameraEuler=Camera.main!=null?new[]{Camera.main.transform.eulerAngles.x,Camera.main.transform.eulerAngles.y,Camera.main.transform.eulerAngles.z}:new float[3],orthographicSize=Camera.main!=null?Camera.main.orthographicSize:0,
                completed=services.Save.Progress.completedIds,next=services.NextLevel(id)};
        }

        internal static object CameraProof(LevelData level,CampArtDefinition art)
        {
            var camera=Camera.main;
            if(camera==null||art==null)return null;
            var adaptation=camera.GetComponent<Kruty1918.GameplayViewport.GameplayViewport>();
            var rect=adaptation!=null?adaptation.Frame.NormalizedGameplay:new Rect(0,0,1,1);
            var bounds=CameraFitter.GameplayBounds(level);
            var projected=Enumerable.Range(0,8).Select(i=>camera.WorldToViewportPoint(bounds.center+new Vector3(
                (i&1)==0?-bounds.extents.x:bounds.extents.x,(i&2)==0?-bounds.extents.y:bounds.extents.y,(i&4)==0?-bounds.extents.z:bounds.extents.z))).ToArray();
            var centre=camera.WorldToViewportPoint(bounds.center);
            var ground=new Plane(Vector3.up,Vector3.zero);
            var corners=new[]{new Vector2(0,0),new Vector2(1,0),new Vector2(1,1),new Vector2(0,1)}.Select(v=>
            {
                var ray=camera.ViewportPointToRay(v);
                return ground.Raycast(ray,out var distance)?ray.GetPoint(distance):new Vector3(float.NaN,0,float.NaN);
            }).ToArray();
            var land=art.landscape;
            var playable=CameraFitter.ContentBounds(level);
            var boardCentre=camera.WorldToViewportPoint(playable.center);
            var groundField=Enumerable.Range(0,8).Select(i=>camera.WorldToViewportPoint(playable.center+new Vector3(
                (i&1)==0?-playable.extents.x:playable.extents.x,(i&2)==0?-playable.extents.y:playable.extents.y,(i&4)==0?-playable.extents.z:playable.extents.z))).ToArray();
            return new{screen=new[]{Screen.width,Screen.height},gameplay=new[]{rect.x,rect.y,rect.width,rect.height},
                playableFieldContained=groundField.All(v=>v.z>0&&v.x>=rect.xMin-.001f&&v.x<=rect.xMax+.001f&&v.y>=rect.yMin-.001f&&v.y<=rect.yMax+.001f),
                playableCentreErrorPixels=new[]{(boardCentre.x-rect.center.x)*Screen.width,(boardCentre.y-rect.center.y)*Screen.height},
                frameCentreErrorPixels=new[]{(boardCentre.x-.5f)*Screen.width,(boardCentre.y-.5f)*Screen.height},
                boardViewport=projected.Select(v=>new[]{v.x,v.y,v.z}),
                centreErrorPixels=new[]{(centre.x-rect.center.x)*Screen.width,(centre.y-rect.center.y)*Screen.height},
                fieldContained=projected.All(v=>v.z>0&&v.x>=rect.xMin-.001f&&v.x<=rect.xMax+.001f&&v.y>=rect.yMin-.001f&&v.y<=rect.yMax+.001f),
                groundCorners=corners.Select(v=>new[]{v.x,v.z}),
                groundContained=land!=null&&corners.All(v=>v.x>=land.originX+1&&v.x<=land.originX+land.size-1&&v.z>=land.originZ+1&&v.z<=land.originZ+land.size-1),
                boardFraming=art.frameGameplayBoard};
        }
    }
}
