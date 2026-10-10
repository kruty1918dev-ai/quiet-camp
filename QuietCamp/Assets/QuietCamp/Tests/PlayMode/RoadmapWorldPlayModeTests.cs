#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using NUnit.Framework;
using QuietCamp.Application;
using QuietCamp.Infrastructure;
using QuietCamp.Presentation;
using QuietCamp.Presentation.UI;
using QuietCamp.Presentation.World;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace QuietCamp.Tests
{
    // Native script imports must be repaired before entering Play Mode. Shared immutable
    // import caches can retain an old class binding when the QA project recompiles scripts.
    public sealed class RoadmapWorldScriptPreflight : IPrebuildSetup
    {
        public void Setup()
        {
            if(UnityEngine.Application.productName!="QuietCampRoadmapQA")
                throw new InvalidOperationException("Roadmap script preflight requires the private QA project");
            foreach(string path in new[]{
                "Assets/QuietCamp/Scripts/Presentation/QuietCampBootstrap.cs",
                "Assets/QuietCamp/Scripts/Presentation/World/RoadmapWorldAsset.cs",
                "Assets/QuietCamp/Scripts/Presentation/World/RoadmapWorldChunk.cs"})
            {
                var script=UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEditor.MonoScript>(path);
                if(script==null||script.GetClass()==null)
                    UnityEditor.AssetDatabase.ImportAsset(path,UnityEditor.ImportAssetOptions.ForceUpdate|UnityEditor.ImportAssetOptions.ForceSynchronousImport);
                // Import may request compilation. The strict GetClass guard runs after the
                // Test Framework enters Play Mode and completes that assembly reload.
            }
        }
    }
    [PrebuildSetup(typeof(RoadmapWorldScriptPreflight))]
    public sealed class RoadmapWorldPlayModeTests
    {
        static string Output=>Environment.GetEnvironmentVariable("QC_CINEMATIC_EVIDENCE_ROOT")
            ?? Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath,"../../Design/Roadmap/CinematicPilot/2026-10-10"));
        static IEnumerator Frames(int count=15){for(int i=0;i<count;i++)yield return null;}
        static Vector2Int ExpectedSize;
        static void Size(int width,int height)
        {
            ExpectedSize=new Vector2Int(width,height);typeof(ScreenshotPlayModeTest)
                .GetMethod("SetGameViewSize",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{width,height});
        }
        static void Tap(string id)=>PrivacyBootTestSupport.Tap(PrivacyBootTestSupport.Find(id));
        static RoadmapWorldPresenter World()=>Object.FindAnyObjectByType<RoadmapWorldPresenter>();
        static HtmlSurface Overlay()=>Object.FindObjectsByType<HtmlSurface>().Single(s=>s.name=="MenuOverlay");
        static IEnumerator Ready(string scene)
        {
            float until=Time.realtimeSinceStartup+90;
            while(Time.realtimeSinceStartup<until)
            {
                if(SceneManager.GetActiveScene().name==scene
                    && (scene=="Camp"?CampSceneHost.Current?.UiReady==true:MenuSceneHost.Current?.UiReady==true))yield break;
                yield return null;
            }
            Assert.Fail("Scene readiness timed out: "+scene);
        }
        static IEnumerator Shot(string name)
        {
            yield return new WaitForEndOfFrame();var pixels=ScreenCapture.CaptureScreenshotAsTexture();
            try{Assert.AreEqual(ExpectedSize.x,pixels.width);Assert.AreEqual(ExpectedSize.y,pixels.height);Directory.CreateDirectory(Output);File.WriteAllBytes(Path.Combine(Output,name+".png"),pixels.EncodeToPNG());}
            finally{Object.Destroy(pixels);}
        }
        static Vector2 Marker(RoadmapWorldPresenter world,int index)
        {
            var marker=world.WorldRoot.Find("Waystone "+RoadmapPilotPolicy.LevelIds[index]);Assert.NotNull(marker);
            return world.WorldCamera.WorldToScreenPoint(marker.position);
        }
        static void Pointer(RoadmapWorldPresenter world,int index,bool drag,bool jitter=false)
        {
            var input=Object.FindAnyObjectByType<RoadmapWorldInput>();Assert.NotNull(input);
            var pointer=new PointerEventData(EventSystem.current){position=Marker(world,index)};
            var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(pointer,hits);
            Assert.IsTrue(hits.Count>0&&hits[0].gameObject==input.gameObject,"World input is covered by the HTML overlay");
            ExecuteEvents.Execute(input.gameObject,pointer,ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(input.gameObject,pointer,ExecuteEvents.initializePotentialDrag);
            if(drag)
            {
                pointer.delta=new Vector2(0,90);pointer.position+=pointer.delta;
                ExecuteEvents.Execute(input.gameObject,pointer,ExecuteEvents.beginDragHandler);
                ExecuteEvents.Execute(input.gameObject,pointer,ExecuteEvents.dragHandler);
            }
            else if(jitter)
            {
                pointer.delta=new Vector2(2,2);pointer.position+=pointer.delta;
                ExecuteEvents.Execute(input.gameObject,pointer,ExecuteEvents.beginDragHandler);
                ExecuteEvents.Execute(input.gameObject,pointer,ExecuteEvents.dragHandler);
            }
            ExecuteEvents.Execute(input.gameObject,pointer,ExecuteEvents.pointerUpHandler);
        }
        [UnityTest,Timeout(900000)] public IEnumerator FivePlacesProgressInputReturnLifecycleAndNativeEvidence()
        {
            Assert.IsTrue(UnityEngine.Application.productName.StartsWith("QuietCampRoadmapQA"),"Run only with isolated synthetic QA saves");
            Assert.IsFalse(UnityEngine.Application.isBatchMode,"Native Game View and end-of-frame captures required");
            UnityEditor.ShaderUtil.allowAsyncCompilation=false;
            // The QA copy keeps mutable import databases private; its private script mapper must resolve after domain reload.
            foreach(var path in new[]{"Assets/QuietCamp/Scripts/Presentation/QuietCampBootstrap.cs",
                "Assets/QuietCamp/Scripts/Presentation/World/RoadmapWorldAsset.cs",
                "Assets/QuietCamp/Scripts/Presentation/World/RoadmapWorldChunk.cs"})
            {
                var script=UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEditor.MonoScript>(path);
                Assert.NotNull(script);Assert.NotNull(script.GetClass(),"Native script mapping: "+path);
            }
            Directory.CreateDirectory(Output);Size(720,1600);
            float menuShadowDistance=((UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset)UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline).shadowDistance;
            var savedShadowRanges=new[]{"Low","Balanced","High"}.Select(name=>UnityEditor.AssetDatabase
                .LoadAssetAtPath<UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset>("Assets/QuietCamp/Settings/URP/QC_"+name+".asset"))
                .ToDictionary(asset=>asset,asset=>asset.shadowDistance);
            var save=new SaveAdapter();save.Load(out _);save.Progress=new ProgressSaveData();save.Session=new SessionSaveData();save.Album=new AlbumSaveData();
            // Fresh campaign progress after onboarding; the existing introductory tutorial has separate QA.
            var guide=new TutorialDirector(new TutorialSaveData(),false,new ProgressionService(),()=>true);guide.Skip();save.Progress.tutorial=guide.Save;
            save.Settings.reducedMotion=true;save.Settings.language="uk";Assert.IsTrue(save.Save());
            yield return SceneManager.LoadSceneAsync("Boot");yield return PrivacyBootTestSupport.EnterGame();yield return Ready("MainMenu");yield return Frames(30);
            var services=QuietCampBootstrap.ServicesRef;services.Tutorial.Skip();services.ReducedMotion=true;
            var adaptive=Object.FindAnyObjectByType<AdaptiveCampQuality>();if(adaptive!=null)adaptive.enabled=false;
            Tap("continue");yield return Ready("MainMenu");yield return Frames(25);
            Assert.IsTrue(World().IsOpen);Assert.AreEqual(0,World().Frontier);Assert.IsFalse(services.CanStart("QC006"));
            Assert.AreEqual(1,Overlay().GetComponentsInChildren<Button>().Count(b=>b.gameObject.activeInHierarchy));
            Pointer(World(),0,true);yield return Frames();Assert.AreEqual("MainMenu",SceneManager.GetActiveScene().name,"Dragging started a level");
            World().Seek(0);yield return Frames(2);float reveal=Shader.GetGlobalFloat("_RoadmapRevealZ");
            World().Drag(-Screen.height*.03f);yield return Frames(2);
            Assert.Greater(World().RouteCoordinate,0,"Fresh profile cannot inspect its surroundings");
            World().Drag(-Screen.height*99);yield return Frames(2);Assert.AreEqual(World().MaxRoute,World().RouteCoordinate);
            Assert.IsFalse(services.CanStart("QC002"));Assert.AreEqual(reveal,Shader.GetGlobalFloat("_RoadmapRevealZ"),"Camera movement grants reveal");
            World().Drag(Screen.height*99);yield return Frames(2);Assert.AreEqual(World().MinRoute,World().RouteCoordinate);
            World().Seek(0);yield return Frames();yield return Shot("gameview-fresh-profile");
            for(int i=0;i<5;i++)
            {
                World().Seek(i);yield return Frames(10);Pointer(World(),i,false,i==0);yield return Ready("Camp");yield return Frames(15);
                Assert.AreEqual(RoadmapPilotPolicy.LevelIds[i],CampSceneHost.Current.Session.Level.id);
                CampSceneHost.Current.Session.DebugApplyWitness();yield return Frames(5);Tap("check");
                float until=Time.realtimeSinceStartup+20;
                while(PrivacyBootTestSupport.Find("next")==null&&Time.realtimeSinceStartup<until)yield return null;
                Assert.IsTrue(CampSceneHost.Current.Session.IsCompleted);Tap("next");
                yield return Ready("MainMenu");yield return Frames(25);
                Assert.IsTrue(World().IsOpen);Assert.AreEqual(Mathf.Min(4,i+1),World().Frontier);
                Assert.AreEqual(World().Frontier,World().RouteCoordinate,"Reduced motion return did not focus the newly available place");
                Assert.LessOrEqual(World().LoadedChunks,3);Assert.IsTrue(services.Progression.IsCompleted(RoadmapPilotPolicy.LevelIds[i]));
                yield return Shot("gameview-after-"+(i+1));
            }
            Assert.IsTrue(RoadmapPilotPolicy.Finished(services.Progression.IsCompleted));
            Assert.IsFalse(services.CanStart("QC006"));
            // Older campaign and album records stay present; the presentation only limits launch.
            var oldIds=Enumerable.Range(1,12).Select(i=>"QC"+i.ToString("000")).ToArray();
            services.Progression.Restore(oldIds,"QC012",0);
            Tap("back");yield return Frames(20);Tap("continue");yield return Ready("MainMenu");yield return Frames(20);
            Assert.AreEqual(4,World().Frontier);Assert.AreEqual(12,services.Progression.CompletedCount);yield return Shot("gameview-old-progress");
            services.Progression.Restore(new[]{"QC012"},"QC012",0);
            Tap("back");yield return Frames();Tap("continue");yield return Frames(25);
            Assert.AreEqual(4,World().Frontier);Assert.AreEqual(1,services.Progression.CompletedCount);
            foreach(var id in RoadmapPilotPolicy.LevelIds){Assert.IsTrue(services.PilotCompleted(id));Assert.IsTrue(services.CanStart(id));}
            var navigation=new List<object>();
            // Check screen-space tracking at both sides of stops, at all pinch scales and aspects.
            foreach(bool landscape in new[]{false,true})
            {
                Size(landscape?1600:720,landscape?720:1600);yield return Frames(3);
                foreach(float zoom in new[]{.85f,1f,1.15f})
                {
                    World().Zoom(zoom/World().ZoomFactor);yield return Frames(2);
                    foreach(float route in new[]{.05f,.5f,.97f,1.03f,2.5f,3.95f})
                    {
                        World().Seek(route);yield return Frames(6);
                        int marker=Mathf.Clamp(Mathf.RoundToInt(route),0,4);Vector2 beforeMarker=Marker(World(),marker);
                        float requested=Screen.height*.035f;World().Drag(new Vector2(0,requested),beforeMarker+Vector2.up*requested);yield return Frames(2);
                        Vector2 moved=Marker(World(),marker)-beforeMarker;
                        Assert.Less(World().RouteCoordinate,route,"Map moved against the finger");
                        Assert.That(moved.y,Is.InRange(requested*.85f,requested*1.15f),"Visible world does not follow the drag at "+route+" / "+zoom);
                        navigation.Add(new{orientation=landscape?"landscape":"portrait",zoom,route,requestedPixels=requested,worldMotionPixels=moved.y});
                    }
                }
            }
            Size(720,1600);World().Zoom(1/World().ZoomFactor);World().Seek(2);yield return Frames(8);
            var worldInput=Object.FindAnyObjectByType<RoadmapWorldInput>();
            var module=EventSystem.current.currentInputModule as InputSystemUIInputModule;
            float tick=module!=null?module.scrollDeltaPerTick:1;
            var wheel=new PointerEventData(EventSystem.current){scrollDelta=new Vector2(0,-tick)};
            worldInput.OnScroll(wheel);yield return Frames(3);float fullNotch=World().RouteCoordinate-2;
            Assert.Less(fullNotch,0,"Wheel down should move content upward toward the start");
            World().Seek(2);yield return Frames(3);wheel.scrollDelta=new Vector2(0,-tick*.25f);
            worldInput.OnScroll(wheel);yield return Frames(3);
            Assert.That(World().RouteCoordinate-2,Is.EqualTo(fullNotch*.25f).Within(.002f),"Fractional trackpad scroll lost precision");
            World().Seek(2);yield return Frames(3);wheel.scrollDelta=new Vector2(0,tick);
            Vector2 wheelMarker=Marker(World(),2);
            worldInput.OnScroll(wheel);yield return Frames(3);Assert.Greater(World().RouteCoordinate,2,"Wheel up should travel onward");
            Assert.Less(Marker(World(),2).y,wheelMarker.y,"Wheel direction differs from standard ScrollRect content movement");
            World().Seek(2);yield return Frames(3);
            var primary=new PointerEventData(EventSystem.current){pointerId=71,position=Marker(World(),2)};
            var secondary=new PointerEventData(EventSystem.current){pointerId=72,position=primary.position+Vector2.right*100};
            worldInput.OnPointerDown(primary);worldInput.OnPointerDown(secondary);
            primary.position+=Vector2.up*90;primary.delta=Vector2.up*90;worldInput.OnDrag(primary);
            worldInput.OnPointerUp(secondary);worldInput.OnPointerUp(primary);yield return Frames(3);
            Assert.AreEqual(2,World().RouteCoordinate,"Second pointer stole or moved the primary gesture");
            Assert.AreEqual("MainMenu",SceneManager.GetActiveScene().name,"Multitouch gesture launched a level");
            // Real Input System events exercise pinch, release and OS cancellation.
            var touch=InputSystem.AddDevice<Touchscreen>();
            try
            {
                Vector2 a=Marker(World(),2),b=a+Vector2.right*120;
                InputSystem.QueueStateEvent(touch,new TouchState{touchId=17,position=a,phase=UnityEngine.InputSystem.TouchPhase.Began});
                InputSystem.QueueStateEvent(touch,new TouchState{touchId=18,position=b,phase=UnityEngine.InputSystem.TouchPhase.Began});yield return Frames(3);
                b+=Vector2.right*80;
                InputSystem.QueueStateEvent(touch,new TouchState{touchId=18,position=b,phase=UnityEngine.InputSystem.TouchPhase.Moved});yield return Frames(3);
                Assert.AreEqual(1.15f,World().ZoomFactor,"Pinch did not obey its upper bound");
                Assert.AreEqual(2,World().RouteCoordinate,"Pinch also scrolled the map");
                InputSystem.QueueStateEvent(touch,new TouchState{touchId=18,position=b,phase=UnityEngine.InputSystem.TouchPhase.Ended});yield return Frames(2);
                InputSystem.QueueStateEvent(touch,new TouchState{touchId=17,position=a,phase=UnityEngine.InputSystem.TouchPhase.Ended});yield return Frames(3);
                Assert.AreEqual("MainMenu",SceneManager.GetActiveScene().name,"Pinch release launched a level");
                a=Marker(World(),2);
                InputSystem.QueueStateEvent(touch,new TouchState{touchId=19,position=a,phase=UnityEngine.InputSystem.TouchPhase.Began});yield return Frames(2);
                InputSystem.QueueStateEvent(touch,new TouchState{touchId=19,position=a,phase=UnityEngine.InputSystem.TouchPhase.Canceled});yield return Frames(3);
                Assert.AreEqual("MainMenu",SceneManager.GetActiveScene().name,"Canceled touch launched a level");
                a=Marker(World(),2);
                InputSystem.QueueStateEvent(touch,new TouchState{touchId=20,position=a,phase=UnityEngine.InputSystem.TouchPhase.Began});yield return Frames(2);
                a+=Vector2.down*70;
                InputSystem.QueueStateEvent(touch,new TouchState{touchId=20,position=a,phase=UnityEngine.InputSystem.TouchPhase.Moved});yield return Frames(2);
                Assert.Greater(World().RouteCoordinate,2,"One-finger drag did not recover after pinch/cancel");
                InputSystem.QueueStateEvent(touch,new TouchState{touchId=20,position=a,phase=UnityEngine.InputSystem.TouchPhase.Ended});yield return Frames(3);
                Assert.AreEqual("MainMenu",SceneManager.GetActiveScene().name,"Touch drag release launched a level");
            }
            finally{InputSystem.RemoveDevice(touch);}
            World().Zoom(1/World().ZoomFactor);World().Seek(2);yield return Frames(2);
            // Package test provider exercises a portrait notch without touching shared Simulator settings.
            var environment=typeof(UnityHTML.Runtime.UnityHtmlHost).Assembly.GetType("UnityHTML.Runtime.UnityHtmlEnvironment");
            var safeField=environment.GetField("SafeAreaProvider",BindingFlags.Static|BindingFlags.NonPublic);
            var originalSafe=safeField.GetValue(null);
            try
            {
                safeField.SetValue(null,(Func<Rect>)(()=>new Rect(0,48,Screen.width,Screen.height-138)));yield return Frames(30);
                var corners=new Vector3[4];((RectTransform)PrivacyBootTestSupport.Find("back").transform).GetWorldCorners(corners);
                foreach(var corner in corners)Assert.IsTrue(new Rect(0,48,Screen.width,Screen.height-138).Contains(RectTransformUtility.WorldToScreenPoint(null,corner)),"Exit enters synthetic notch area");
                yield return Shot("gameview-notched-portrait");
            }
            finally{safeField.SetValue(null,originalSafe);}
            yield return Frames();
            var measurements=new List<object>();
            foreach(int quality in new[]{0,1})
            {
                services.Settings.quality=quality+1;services.EffectiveQuality=quality;
                QualitySettings.SetQualityLevel(quality,true);World().Seek(3);yield return Frames(40);Assert.IsTrue(World().Ready);
                Assert.AreEqual(quality==0?"Low":"Balanced",QualitySettings.names[QualitySettings.GetQualityLevel()],"Incorrect native render profile");
                var opaque=World().WorldRoot.GetComponentsInChildren<MeshRenderer>().Where(r=>r.sharedMaterial.shader.name!="QuietCamp/RoadmapMotes"&&!r.sharedMaterial.shader.name.Contains("Stylized Water"));
                Assert.IsTrue(opaque.All(r=>r.shadowCastingMode==UnityEngine.Rendering.ShadowCastingMode.On),"An opaque world object lost its shadow caster");
                var shadowPipeline=(UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset)UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
                Assert.IsTrue(shadowPipeline.supportsMainLightShadows);Assert.IsFalse(shadowPipeline.supportsAdditionalLightShadows);
                Assert.AreEqual(1,shadowPipeline.shadowCascadeCount);Assert.AreEqual(quality==0?512:1024,shadowPipeline.mainLightShadowmapResolution);
                foreach(var mesh in opaque.Select(r=>r.GetComponent<MeshFilter>().sharedMesh))
                    Assert.LessOrEqual(mesh.vertexCount,(long)mesh.GetIndexCount(0),"Indexed shadow geometry exceeds its triangle stream");
                for(int i=0;i<9;i++)
                {
                    World().Seek(i*.5f);yield return Frames(12);Assert.IsTrue(World().Ready,World().Fault);Assert.LessOrEqual(World().LoadedChunks,3);
                    for(int chunk=0;chunk<5;chunk++)if(World().WorldRoot.Find("Valley chunk "+chunk)!=null)
                        Assert.IsFalse(World().WorldRoot.Find("Far terrain "+chunk).GetComponent<MeshRenderer>().enabled,"Coarse terrain covers the detailed road");
                    yield return Shot((quality==0?"gameview-low-composition-":"gameview-composition-")+i.ToString("00"));
                    measurements.Add(new{quality=quality==0?"Low":"Balanced",renderProfile=QualitySettings.names[QualitySettings.GetQualityLevel()],route=i*.5f,orientation="portrait",editorDrawCalls=UnityEditor.UnityStats.drawCalls,editorTriangles=UnityEditor.UnityStats.triangles,worldChunks=World().LoadedChunks});
                    Assert.LessOrEqual(UnityEditor.UnityStats.drawCalls,quality==0?80:120,"Editor draw-call budget");
                }
                Size(1600,720);yield return Frames(20);yield return Shot("gameview-"+(quality==0?"low":"balanced")+"-landscape");
                measurements.Add(new{quality=quality==0?"Low":"Balanced",renderProfile=QualitySettings.names[QualitySettings.GetQualityLevel()],route=4f,orientation="landscape",editorDrawCalls=UnityEditor.UnityStats.drawCalls,editorTriangles=UnityEditor.UnityStats.triangles,worldChunks=World().LoadedChunks});
                Size(720,1600);yield return Frames();
            }
            var waterMotion=new List<object>();
            for(int quality=0;quality<=1;quality++)
            {
                services.Settings.quality=quality;services.EffectiveQuality=quality;
                QualitySettings.SetQualityLevel(quality,true);services.ReducedMotion=false;
                World().Seek(4);yield return Frames(30);
                var river=World().WorldRoot.Find("Continuous river");Assert.NotNull(river);
                var material=river.GetComponent<MeshRenderer>().sharedMaterial;
                Assert.IsTrue(material.IsKeywordEnabled("_WAVES")&&material.IsKeywordEnabled("_INTERSECTION_FOAM"));
                Assert.AreEqual(4,material.GetInt("_WaveMaxLayers"));
                var point=World().WorldCamera.WorldToScreenPoint(World().WorldRoot.TransformPoint(new Vector3(RoadmapLandscape.RiverX(150),RoadmapLandscape.WaterHeight,150)));
                const int radius=20;
                Assert.IsTrue(point.x>radius&&point.x<Screen.width-radius&&point.y>radius&&point.y<Screen.height-radius,"Stationary water patch must be visible");
                yield return new WaitForEndOfFrame();
                var waterBefore=ScreenCapture.CaptureScreenshotAsTexture();
                Color[] patch=waterBefore.GetPixels((int)point.x-radius,(int)point.y-radius,radius*2,radius*2);
                File.WriteAllBytes(Path.Combine(Output,"water-"+(quality==0?"low":"balanced")+"-0.png"),waterBefore.EncodeToPNG());Object.Destroy(waterBefore);
                var cameraPosition=World().WorldCamera.transform.position;var cameraRotation=World().WorldCamera.transform.rotation;
                float beganWater=Time.realtimeSinceStartup;
                yield return new WaitForSecondsRealtime(2);yield return new WaitForEndOfFrame();
                var waterAfter=ScreenCapture.CaptureScreenshotAsTexture();
                var changed=waterAfter.GetPixels((int)point.x-radius,(int)point.y-radius,radius*2,radius*2);
                File.WriteAllBytes(Path.Combine(Output,"water-"+(quality==0?"low":"balanced")+"-1.png"),waterAfter.EncodeToPNG());Object.Destroy(waterAfter);
                float difference=0;for(int p=0;p<patch.Length;p++)difference+=Mathf.Abs(patch[p].r-changed[p].r)+Mathf.Abs(patch[p].g-changed[p].g)+Mathf.Abs(patch[p].b-changed[p].b);
                difference=difference/patch.Length/3*255;
                Assert.Greater(difference,.15f,"Water must visibly animate with a stationary camera");
                Assert.AreEqual(cameraPosition,World().WorldCamera.transform.position);Assert.AreEqual(cameraRotation,World().WorldCamera.transform.rotation);
                waterMotion.Add(new{quality=quality==0?"Low":"Balanced",renderProfile=QualitySettings.names[QualitySettings.GetQualityLevel()],seconds=Time.realtimeSinceStartup-beganWater,meanRgbByteDifference=difference,patchX=(int)point.x-radius,patchY=(int)point.y-radius,patchSize=radius*2,cameraStationary=true,shader=material.shader.name});
            }
            var airborneMotion=new List<object>();
            for(int quality=0;quality<=1;quality++)
            {
                services.Settings.quality=quality+1;services.EffectiveQuality=quality;QualitySettings.SetQualityLevel(quality,true);
                services.ReducedMotion=false;World().Seek(0);yield return Frames(35);
                var camera=World().WorldCamera;var position=camera.transform.position;var rotation=camera.transform.rotation;
                var renderers=World().WorldRoot.GetComponentsInChildren<MeshRenderer>();
                var states=renderers.ToDictionary(r=>r,r=>r.enabled);var flags=camera.clearFlags;var background=camera.backgroundColor;
                try
                {
                    foreach(var r in renderers)if(r.sharedMaterial.shader.name!="QuietCamp/RoadmapMotes")r.enabled=false;
                    camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;
                    Color32[] previous=null;int lit=0,changed=0,hidden=0;
                    for(int phase=0;phase<3;phase++)
                    {
                        if(phase==1)yield return new WaitForSecondsRealtime(2);
                        if(phase==2)services.ReducedMotion=true;
                        yield return Frames(3);yield return new WaitForEndOfFrame();
                        var texture=ScreenCapture.CaptureScreenshotAsTexture();var pixels=texture.GetPixels32();
                        File.WriteAllBytes(Path.Combine(Output,"airborne-"+(quality==0?"low":"balanced")+"-"+phase+".png"),texture.EncodeToPNG());Object.Destroy(texture);
                        // Exclude the exit control and safe-area edges. The empty
                        // black world isolates actual shader pixels from moving foliage.
                        for(int y=80;y<Screen.height-180;y++)for(int x=60;x<Screen.width-60;x++)
                        {
                            int p=y*Screen.width+x;var c=pixels[p];int brightness=c.r+c.g+c.b;
                            if(phase==0&&brightness>6)lit++;
                            if(phase==1&&Math.Abs(c.r-previous[p].r)+Math.Abs(c.g-previous[p].g)+Math.Abs(c.b-previous[p].b)>6)changed++;
                            if(phase==2&&brightness>6)hidden++;
                        }
                        if(phase==0)previous=pixels;
                    }
                    Assert.Greater(lit,5,"Airborne detail is invisible from the authored overview");
                    Assert.Greater(changed,5,"Airborne detail does not move with a stationary camera");
                    Assert.AreEqual(0,hidden,"Reduced motion leaves airborne particles visible");
                    Assert.AreEqual(position,camera.transform.position);Assert.AreEqual(rotation,camera.transform.rotation);
                    airborneMotion.Add(new{quality=quality==0?"Low":"Balanced",cameraStationary=true,litPixels=lit,changedPixels=changed,reducedMotionLitPixels=hidden,isolatedRendererProbe=true});
                }
                finally
                {
                    foreach(var state in states)state.Key.enabled=state.Value;
                    camera.clearFlags=flags;camera.backgroundColor=background;services.ReducedMotion=false;
                }
            }
            services.Settings.quality=2;services.EffectiveQuality=1;services.ReducedMotion=false;World().Seek(0);yield return Frames(60);
            var movie=Path.Combine(Output,"MovieFrames~");Directory.CreateDirectory(movie);
            float began=Time.realtimeSinceStartup;
            for(int frame=0;frame<=192;frame++)
            {
                World().Seek(frame/48f);yield return Frames(2);yield return new WaitForEndOfFrame();
                var pixels=ScreenCapture.CaptureScreenshotAsTexture();try{File.WriteAllBytes(Path.Combine(movie,"frame-"+frame.ToString("0000")+".png"),pixels.EncodeToPNG());}finally{Object.Destroy(pixels);}
            }
            float capturedSeconds=Time.realtimeSinceStartup-began;
            services.ReducedMotion=true;World().Seek(2);yield return Frames(10);
            // Warm all chunks once before leak comparison. No new map cameras, meshes or materials per visit.
            Assert.AreEqual(World().WorldRoot.Find("Valley afternoon sun").GetComponent<Light>(),RenderSettings.sun,"World sunlight authority changed");
            var pipeline=UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline as UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset;
            float mapShadowDistance=pipeline.shadowDistance;
            Assert.Greater(mapShadowDistance,32,"Aerial camera lost ground shadows");
            var counts=new List<int[]>();
            var materialVisits=new List<Dictionary<string,string>>();
            for(int visit=0;visit<4;visit++)
            {
                Tap("back");yield return Frames(20);
                Assert.IsFalse(World().IsOpen);Assert.AreEqual(menuShadowDistance,pipeline.shadowDistance,"Roadmap shadow range leaked into the menu");Assert.IsNull(Object.FindObjectsByType<Camera>().FirstOrDefault(c=>c.name=="Roadmap perspective camera"));
                foreach(var saved in savedShadowRanges)Assert.AreEqual(saved.Value,saved.Key.shadowDistance,"Shadow range leaked into "+saved.Key.name);
                Tap("continue");yield return Ready("MainMenu");World().Seek(2);yield return Frames(25);
                counts.Add(new[]{Object.FindObjectsByType<Camera>().Length,Resources.FindObjectsOfTypeAll<Material>().Length,Resources.FindObjectsOfTypeAll<Mesh>().Length});
                materialVisits.Add(Resources.FindObjectsOfTypeAll<Material>().ToDictionary(m=>m.GetEntityId().ToString(),m=>m.name+" / "+m.shader.name+" / "+m.hideFlags));
            }
            File.WriteAllText(Path.Combine(Output,"lifecycle-diagnostics.json"),JsonConvert.SerializeObject(new{counts,
                addedMaterials=materialVisits.Skip(1).Select(v=>v.Where(kv=>!materialVisits[0].ContainsKey(kv.Key)).Select(kv=>kv.Value).ToArray())},Formatting.Indented));
            for(int i=1;i<counts.Count;i++)CollectionAssert.AreEqual(counts[0],counts[i],"World entry grows native objects");
            services.RoadmapAdvanceFrom=0;services.ReducedMotion=false;Tap("back");yield return Frames();Tap("continue");yield return Ready("MainMenu");
            float before=World().RouteCoordinate;World().Drag(0);yield return Frames(3);Assert.Less(Mathf.Abs(World().RouteCoordinate-before),.2f,"Interrupted flight jumped to the endpoint");
            services.ReducedMotion=true;Tap("back");yield return Frames();PointerReplay(services);
            yield return Ready("MainMenu");yield return Frames();
            World().Seek(0);yield return Frames();Pointer(World(),0,false);yield return Ready("Camp");Assert.AreEqual("QC001",CampSceneHost.Current.Session.Level.id);
            File.WriteAllText(Path.Combine(Output,"gameview-integration-receipt.json"),JsonConvert.SerializeObject(new{capturedUtc=DateTime.UtcNow.ToString("o"),unity=UnityEngine.Application.unityVersion,
                product=UnityEngine.Application.productName,sourceHash=Resources.Load<RoadmapWorldAsset>("QuietCamp/CinematicRoadmap/World").sourceHash,playerBuild=false,mobileFpsMeasured=false,audioSuppressionRequested=true,audioOutputVerified=false,measurements,navigation,waterMotion,airborneMotion,reentryCounts=counts,movieFrameCount=193,capturedSeconds,
                checks="Fresh campaign progress after onboarding, completion 1–5, old progress including gaps, replay, jitter-tolerant tap, drag rejection, 36 screen-space tracking samples, normalized wheel and fractional trackpad, multitouch ownership, real Input System pinch/release/cancel, fresh-profile inspection margins without reveal, quality change, synthetic notch, portrait/landscape, reduced motion, interrupted reveal, repeated entry/exit"},Formatting.Indented)+"\n");
        }
        static void PointerReplay(GameServices services){services.PendingMenuScreen="Levels";PrivacyBootTestSupport.Tap(PrivacyBootTestSupport.Find("continue"));}
    }
}
#endif
