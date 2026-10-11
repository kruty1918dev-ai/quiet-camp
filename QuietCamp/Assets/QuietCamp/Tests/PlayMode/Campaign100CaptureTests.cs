#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using QuietCamp.Domain;
using QuietCamp.Infrastructure;
using QuietCamp.Application;
using QuietCamp.Presentation;
using QuietCamp.Presentation.UI;
using QuietCamp.Presentation.World;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

namespace QuietCamp.Tests
{
    public sealed class Campaign100CaptureTests
    {
        static string Output=>Environment.GetEnvironmentVariable("QC_CAMPAIGN100_ROOT")??Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath,"../../Design/Campaign100/2026-10-11"));
        static bool Offscreen=>Environment.GetEnvironmentVariable("QC_CAMPAIGN_OFFSCREEN")=="1";
        static string CaptureRoot=>Environment.GetEnvironmentVariable("QC_FIRST_TEN_EVIDENCE")=="1"?Path.Combine(Output,"FirstTenRefinement/After"):Output;
        static string SourceRevision;
        static string Hash(byte[] bytes)=>BitConverter.ToString(SHA256.Create().ComputeHash(bytes)).Replace("-","").ToLowerInvariant();
        static void VerifyCaptureSources()
        {
            var manifest=JObject.Parse(File.ReadAllText(Path.Combine(Output,"capture-source-manifest.json")));
            SourceRevision=(string)manifest["revision"];
            foreach(var entry in manifest["files"].OfType<JObject>())
            {
                string relative=(string)entry["path"];
                Assert.IsTrue(relative.StartsWith("Assets/",StringComparison.Ordinal));
                var path=Path.Combine(UnityEngine.Application.dataPath,relative.Substring(7));
                Assert.AreEqual((string)entry["sha256"],Hash(File.ReadAllBytes(path)),"Capture source changed: "+relative);
            }
        }
        static int Width=1280,Height=800;
        static void Size(int width,int height)
        {Width=width;Height=height;if(!Offscreen)typeof(ScreenshotPlayModeTest).GetMethod("SetGameViewSize",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{width,height});}
        static IEnumerator Settle(int frames){for(int i=0;i<frames;i++)yield return null;}
        static IEnumerator Ready(string id)
        {
            float deadline=Time.realtimeSinceStartup+90;
            while(Time.realtimeSinceStartup<deadline)
            {
                if(CampSceneHost.Current?.Session?.Level?.id==id&&CampSceneHost.Current.UiReady)yield break;
                yield return null;
            }
            Assert.Fail("Native camp timed out "+id);
        }
        static float[] Viewport(Camera camera,EnvironmentalStoryView view)
        {var p=camera.WorldToViewportPoint(new Vector3(view.x,view.height*.5f,view.z));return new[]{p.x,p.y,p.z};}
        static IEnumerator Shot(LevelData level,string orientation)
        {
            if(Offscreen)yield return null;else yield return new WaitForEndOfFrame();
            string file="level-"+level.order.ToString("D3")+"-"+orientation;
            var camera=Object.FindAnyObjectByType<CampAtmosphere>().WorldCamera;
            if(Offscreen)
            {
                var adapter=camera.GetComponent<Kruty1918.GameplayViewport.GameplayViewport>();if(adapter!=null)adapter.enabled=false;
                camera.aspect=(float)Width/Height;
                Kruty1918.GameplayViewport.GameplayCameraMath.TryOrthographic(CameraFitter.ContentBounds(level),camera.transform.rotation,
                    camera.aspect,new Rect(.04f,.04f,.92f,.92f),CameraFitter.Distance,CameraFitter.FitMargin,out var pose,camera.nearClipPlane);
                camera.orthographicSize=pose.OrthographicSize;camera.transform.position=pose.Position;
                camera.farClipPlane=Mathf.Max(camera.farClipPlane,pose.RequiredFarClip);
                yield return Settle(8);
                var target=new RenderTexture(Width,Height,24,RenderTextureFormat.ARGB32);target.Create();var previous=RenderTexture.active;
                var texture=new Texture2D(Width,Height,TextureFormat.RGB24,false);
                try
                {
                    camera.targetTexture=target;
                    var request=new UniversalRenderPipeline.SingleCameraRequest{destination=target};
                    RenderPipeline.SubmitRenderRequest(camera,request);RenderPipeline.SubmitRenderRequest(camera,request);
                    RenderTexture.active=target;texture.ReadPixels(new Rect(0,0,Width,Height),0,0);texture.Apply();
                    File.WriteAllBytes(Path.Combine(CaptureRoot,"Renders",file+".png"),texture.EncodeToPNG());
                }
                finally{camera.targetTexture=null;RenderTexture.active=previous;target.Release();Object.Destroy(target);Object.Destroy(texture);}
            }
            else
            {
                var texture=ScreenCapture.CaptureScreenshotAsTexture();
                try{File.WriteAllBytes(Path.Combine(CaptureRoot,"Renders",file+".png"),texture.EncodeToPNG());}
                finally{Object.Destroy(texture);}
            }
            var story=Object.FindAnyObjectByType<EnvironmentalStoryVisual>();
            var mesh=story?.GetComponent<MeshFilter>()?.sharedMesh;
            var baked=Object.FindAnyObjectByType<BakedCampEnvironment>();
            Assert.IsTrue(mesh!=null||baked!=null,"Story geometry absent: "+level.id);
            File.WriteAllText(Path.Combine(CaptureRoot,"Renders",file+".json"),JsonConvert.SerializeObject(new{
                kind=Offscreen?"native-unity-world-camera":"native-unity-game-view",capturedUtc=DateTime.UtcNow.ToString("o"),unityVersion=UnityEngine.Application.unityVersion,
                levelId=level.id,number=level.order,contentHash=level.contentHash,sourceRevision=SourceRevision,orientation,
                imageSha256=BitConverter.ToString(SHA256.Create().ComputeHash(File.ReadAllBytes(Path.Combine(CaptureRoot,"Renders",file+".png")))).Replace("-","").ToLowerInvariant(),
                width=Width,height=Height,quality=QualitySettings.names[QualitySettings.GetQualityLevel()],
                graphics=SystemInfo.graphicsDeviceType.ToString(),qaProduct=UnityEngine.Application.productName,
                cameraPosition=new[]{camera.transform.position.x,camera.transform.position.y,camera.transform.position.z},
                cameraEuler=new[]{camera.transform.eulerAngles.x,camera.transform.eulerAngles.y,camera.transform.eulerAngles.z},orthographicSize=camera.orthographicSize,
                storyTriangles=mesh!=null?mesh.triangles.Length/3:baked.TriangleCount,bakedEnvironment=level.bakedEnvironment,bakedSourceRevision=baked?.Source.sourceRevision,
                bakedParts=baked?.Source.parts.Select(p=>new{p.kind,p.triangles,p.vertices,objectCount=p.objects.Length}),seasonTimeline=level.environment.seasonTimeline,snow=SeasonProfile.For(level).SnowCoverage,
                props=level.environmentalStory.beats.SelectMany(b=>b.props).Select(p=>new{id=p.id,asset=p.level.assetId,
                    viewport=Viewport(camera,p.level)}),
                syntheticWitnessLayout=true,reducedMotion=true,playerBuild=false,mobileFpsMeasured=false,audioMeasured=false
            },Formatting.Indented)+"\n");
        }
        static IEnumerator CaptureWorldComponents()
        {
            QualitySettings.SetQualityLevel(1,true);
            var ids=LevelLoader.MvpLevelIds();Assert.AreEqual(100,ids.Count);
            int first=int.TryParse(Environment.GetEnvironmentVariable("QC_CAMPAIGN_FIRST"),out int a)?Math.Max(1,a):1;
            int last=int.TryParse(Environment.GetEnvironmentVariable("QC_CAMPAIGN_LAST"),out int b)?Math.Min(100,b):100;
            // Same board, tents, decor, atmosphere and shaders as CampSceneHost.BuildWorld.
            // No Bootstrap/menu/HUD allocation; this evidence covers rendered world only.
            foreach(var id in ids.Skip(first-1).Take(last-first+1))
            {
                var level=LevelLoader.Load(id);
                if(Environment.GetEnvironmentVariable("QC_FIRST_TEN_EVIDENCE")=="1")
                {
                    Assert.IsTrue(BakedCampEnvironment.Uses(level),"Actual LevelKit loader must preserve the baked world reference "+id);
                    Assert.AreEqual(level.contentHash,CampContent.CalculateHash(level),"Actual loader must preserve canonical content "+id);
                }
                var root=new GameObject("Actual Camp world capture");
                Transform Child(string name){var go=new GameObject(name);go.transform.SetParent(root.transform);return go.transform;}
                var camera=Child("World Camera").gameObject.AddComponent<Camera>();CameraFitter.Configure(camera);
                var sun=Child("Shared Sun").gameObject.AddComponent<Light>();sun.type=LightType.Directional;sun.shadows=LightShadows.Soft;RenderSettings.sun=sun;
                var assets=AssetCatalog.Load();Assert.NotNull(assets);
                var board=new BoardRenderer(level,assets,Child("Base"),Child("Grid"),Child("Obstacle"),Child("Tent"),Child("Overlay"));
                board.SyncPlacements(level.witness,level,1,true);
                var decor=Child("DecorRoot");DecorSpawner.Spawn(level,assets,decor);
                var atmosphere=root.AddComponent<CampAtmosphere>();
                atmosphere.Configure(camera,level,null,AtmosphereCatalog.Load().Get(level.lighting),()=>true,1,decor);
                board.BuildCanopies();
                yield return Settle(35);
                VerifyGeometry(root,level);
                Size(1280,800);yield return Shot(level,"landscape");
                Size(720,1600);yield return Shot(level,"portrait");
                Debug.Log("[Campaign100] captured "+level.order+" "+id);
                board.ClearAll();Object.Destroy(root);yield return Settle(3);yield return Resources.UnloadUnusedAssets();GC.Collect();
            }
            File.WriteAllText(Path.Combine(CaptureRoot,"native-batch-"+first+"-"+last+".json"),JsonConvert.SerializeObject(new{
                result="Passed",capturedUtc=DateTime.UtcNow.ToString("o"),first,last,captureCount=(last-first+1)*2,
                nativeGameView=false,nativeWorldCamera=true,allAuthoredPropsPresent=true,playerBuild=false,mobileFpsMeasured=false},Formatting.Indented)+"\n");
        }
        static void VerifyGeometry(GameObject root,LevelData level)
        {
            if(BakedCampEnvironment.Uses(level))
            {
                var baked=root.GetComponentInChildren<BakedCampEnvironment>();
                if(baked==null)baked=root.scene.GetRootGameObjects().SelectMany(go=>go.GetComponentsInChildren<BakedCampEnvironment>()).SingleOrDefault(b=>b.Source.id==level.id);
                Assert.NotNull(baked,level.id);
                Assert.AreEqual(level.id,baked.Source.id);Assert.AreEqual(5,baked.Source.parts.Length);
                foreach(var part in baked.Source.parts)
                {
                    var filter=baked.transform.Find("Baked "+part.kind).GetComponent<MeshFilter>();
                    Assert.AreEqual(part.triangles,filter.sharedMesh.triangles.Length/3,part.kind);
                    Assert.AreEqual(part.vertices,filter.sharedMesh.vertexCount,part.kind);
                    Assert.AreEqual(UnityEngine.Rendering.ShadowCastingMode.On,filter.GetComponent<MeshRenderer>().shadowCastingMode,part.kind);
                }
                var identifiers=baked.Source.parts.SelectMany(p=>p.objects).Select(p=>p.id).ToArray();
                Assert.AreEqual(identifiers.Length,identifiers.Distinct().Count(),"Stable unique baked IDs");
                foreach(var prop in level.environmentalStory.beats.SelectMany(b=>b.props))Assert.Contains(prop.id,identifiers,"Every narrative anchor present");
                return;
            }
            var visual=root.GetComponentInChildren<EnvironmentalStoryVisual>();Assert.NotNull(visual,level.id);
            int expected=level.environmentalStory.beats.SelectMany(beat=>beat.props).Sum(p=>RoadmapModelLibrary.Load().Get(p.level.assetId).Positions.Length/3);
            Assert.AreEqual(expected,visual.GetComponent<MeshFilter>().sharedMesh.triangles.Length/3,"Every authored prop must be present "+level.id);
        }
        [UnityTest,Timeout(1800000)]
        public IEnumerator CaptureActualHundredLevelScenes()
        {
            if(!Offscreen)Assert.IsFalse(UnityEngine.Application.isBatchMode,"Requires actual Game View");
            Assert.AreNotEqual(GraphicsDeviceType.Null,SystemInfo.graphicsDeviceType);
            Assert.IsTrue(UnityEngine.Application.productName.Contains("RoadmapQA"),"Private synthetic QA identity required");
            Assert.IsTrue(LevelLoader.Campaign100Enabled());
            VerifyCaptureSources();
            Directory.CreateDirectory(Path.Combine(CaptureRoot,"Renders"));
            if(Offscreen){yield return CaptureWorldComponents();yield break;}
            Size(1280,800);
            yield return SceneManager.LoadSceneAsync("Boot");yield return PrivacyBootTestSupport.EnterGame();
            var services=QuietCampBootstrap.ServicesRef;services.Tutorial.Skip();services.ReducedMotion=true;
            services.Settings.quality=2;services.EffectiveQuality=1;QualitySettings.SetQualityLevel(1,true);
            services.Localization.TrySetLanguage("uk");services.Settings.language="uk";
            var adaptive=Object.FindAnyObjectByType<AdaptiveCampQuality>();if(adaptive!=null)adaptive.enabled=false;
            var ids=LevelLoader.MvpLevelIds();Assert.AreEqual(100,ids.Count);
            services.Progression.Restore(ids,ids.Last(),0);services.Save.Session=new SessionSaveData();
            int first=int.TryParse(Environment.GetEnvironmentVariable("QC_CAMPAIGN_FIRST"),out int a)?Math.Max(1,a):1;
            int last=int.TryParse(Environment.GetEnvironmentVariable("QC_CAMPAIGN_LAST"),out int b)?Math.Min(100,b):100;
            foreach(var id in ids.Skip(first-1).Take(last-first+1))
            {
                services.Save.Session=new SessionSaveData();services.PendingLevelId=id;
                yield return SceneManager.LoadSceneAsync("Camp");yield return Ready(id);
                var host=CampSceneHost.Current;host.Session.DebugApplyWitness();yield return Settle(30);
                VerifyGeometry(host.gameObject,host.Session.Level);
                Size(1280,800);yield return Settle(25);yield return Shot(host.Session.Level,"landscape");
                Size(720,1600);yield return Settle(25);yield return Shot(host.Session.Level,"portrait");
                Debug.Log("[Campaign100] captured "+host.Session.Level.order+" "+id);
                // Scene-owned meshes/materials must be released before the next capture.
                int number=host.Session.Level.order;
                yield return SceneManager.LoadSceneAsync("MainMenu");yield return Settle(3);
                if(number%10==0){yield return Resources.UnloadUnusedAssets();GC.Collect();}
            }
            File.WriteAllText(Path.Combine(CaptureRoot,"native-batch-"+first+"-"+last+".json"),JsonConvert.SerializeObject(new{
                result="Passed",capturedUtc=DateTime.UtcNow.ToString("o"),first,last,captureCount=(last-first+1)*2,
                nativeGameView=!Offscreen,nativeWorldCamera=Offscreen,allAuthoredPropsPresent=true,playerBuild=false,mobileFpsMeasured=false},Formatting.Indented)+"\n");
        }
    }
}
#endif
