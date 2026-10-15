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
        public static object Run(string action,string output=null,string levelId=null)
        {
            if(action=="start")
            {
                var level=LevelLoader.Load(levelId??"QC001");
                LevelAuthoringPlayDriver.Start(new LevelAuthoringDocument{level=level,art=CampArtDefinition.Load(level)},true,output);
                return new{status="opening-game-starting"};
            }
            if(EditorApplication.isPlayingOrWillChangePlaymode && EditorAuthoringPreview.CampaignPreview && QuietCampBootstrap.ServicesRef==null)
                return new{status="opening-game-waiting"};
            if(!EditorApplication.isPlaying || !EditorAuthoringPreview.CampaignPreview || QuietCampBootstrap.ServicesRef?.Save.MemoryOnly!=true)
                throw new InvalidOperationException("Only the isolated campaign preview can run this probe.");
            var host=CampSceneHost.Current;
            if(host?.UiReady!=true)return new{status="opening-game-waiting"};
            var services=QuietCampBootstrap.ServicesRef;var session=host.Session;string id=session.Level.id;
            if(action=="promo") return CampaignPromoRecorder.Begin(output);
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
            if(action=="frame")
            {
                if(string.IsNullOrEmpty(output)||!output.EndsWith(".png",StringComparison.OrdinalIgnoreCase))throw new ArgumentException("Expected a PNG path.");
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(output));ScreenCapture.CaptureScreenshot(output);return new{status="opening-game-frame-requested",id,output};
            }
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
            var transition=UnityEngine.Object.FindAnyObjectByType<FoliageDiveTransition>();
            return new{status="opening-game-ready",id,ready=host.UiReady,memoryOnly=services.Save.MemoryOnly,
                transitionIdle=transition==null||transition.Current==FoliageDiveTransition.State.Idle,
                cameraProof=CameraProof(session.Level,art?.Definition),
                frozenArtMatches=art!=null&&JsonConvert.SerializeObject(art.Definition)==JsonConvert.SerializeObject(CampArtDefinition.Load(session.Level)),
                story=OpeningCampaignStory.For(session.Level)?.title,surfaceSteps=host.SurfaceDetails?.Surface?.Steps,
                particles=art?.SeasonAtmosphere?.LiveParticles,voices=art?.SeasonAtmosphere?.OwnedVoices,
                playingVoices=art?.SeasonAtmosphere?.PlayingVoices,wind=art?.SeasonAtmosphere?.WindStrength,
                cameraEuler=Camera.main!=null?new[]{Camera.main.transform.eulerAngles.x,Camera.main.transform.eulerAngles.y,Camera.main.transform.eulerAngles.z}:new float[3],orthographicSize=Camera.main!=null?Camera.main.orthographicSize:0,
                completed=services.Save.Progress.completedIds,next=services.NextLevel(id)};
        }

        static object CameraProof(LevelData level,CampArtDefinition art)
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
            return new{gameplay=new[]{rect.x,rect.y,rect.width,rect.height},
                boardViewport=projected.Select(v=>new[]{v.x,v.y,v.z}),
                centreErrorPixels=new[]{(centre.x-rect.center.x)*Screen.width,(centre.y-rect.center.y)*Screen.height},
                fieldContained=projected.All(v=>v.z>0&&v.x>=rect.xMin-.001f&&v.x<=rect.xMax+.001f&&v.y>=rect.yMin-.001f&&v.y<=rect.yMax+.001f),
                groundCorners=corners.Select(v=>new[]{v.x,v.z}),
                groundContained=land!=null&&corners.All(v=>v.x>=land.originX+1&&v.x<=land.originX+land.size-1&&v.z>=land.originZ+1&&v.z<=land.originZ+land.size-1),
                boardFraming=art.frameGameplayBoard};
        }
    }
}
