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
        static void Pointer(RoadmapWorldPresenter world,int index,bool drag)
        {
            var input=Object.FindAnyObjectByType<RoadmapWorldInput>();Assert.NotNull(input);
            var pointer=new PointerEventData(EventSystem.current){position=Marker(world,index)};
            var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(pointer,hits);
            Assert.IsTrue(hits.Count>0&&hits[0].gameObject==input.gameObject,"World input is covered by the HTML overlay");
            ExecuteEvents.Execute(input.gameObject,pointer,ExecuteEvents.pointerDownHandler);
            if(drag)
            {
                pointer.delta=new Vector2(0,90);pointer.position+=pointer.delta;
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
            // The QA copy shares imported artifacts; its private script mapper must resolve after domain reload.
            foreach(var path in new[]{"Assets/QuietCamp/Scripts/Presentation/QuietCampBootstrap.cs",
                "Assets/QuietCamp/Scripts/Presentation/World/RoadmapWorldAsset.cs",
                "Assets/QuietCamp/Scripts/Presentation/World/RoadmapWorldChunk.cs"})
            {
                var script=UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEditor.MonoScript>(path);
                Assert.NotNull(script);Assert.NotNull(script.GetClass(),"Native script mapping: "+path);
            }
            Directory.CreateDirectory(Output);Size(720,1600);
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
            World().Seek(0);yield return Frames();yield return Shot("gameview-fresh-profile");
            for(int i=0;i<5;i++)
            {
                World().Seek(i);yield return Frames(10);Pointer(World(),i,false);yield return Ready("Camp");yield return Frames(15);
                Assert.AreEqual(RoadmapPilotPolicy.LevelIds[i],CampSceneHost.Current.Session.Level.id);
                CampSceneHost.Current.Session.DebugApplyWitness();yield return Frames(5);Tap("check");
                float until=Time.realtimeSinceStartup+20;
                while(PrivacyBootTestSupport.Find("next")==null&&Time.realtimeSinceStartup<until)yield return null;
                Assert.IsTrue(CampSceneHost.Current.Session.IsCompleted);Tap("next");
                yield return Ready("MainMenu");yield return Frames(25);
                Assert.IsTrue(World().IsOpen);Assert.AreEqual(Mathf.Min(4,i+1),World().Frontier);
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
                services.Settings.quality=quality+1;services.EffectiveQuality=quality;World().Seek(3);yield return Frames(40);Assert.IsTrue(World().Ready);
                for(int i=0;i<9;i++)
                {
                    World().Seek(i*.5f);yield return Frames(12);Assert.IsTrue(World().Ready,World().Fault);Assert.LessOrEqual(World().LoadedChunks,3);
                    yield return Shot((quality==0?"gameview-low-composition-":"gameview-composition-")+i.ToString("00"));
                    measurements.Add(new{quality=quality==0?"Low":"Balanced",route=i*.5f,orientation="portrait",editorDrawCalls=UnityEditor.UnityStats.drawCalls,editorTriangles=UnityEditor.UnityStats.triangles,worldChunks=World().LoadedChunks});
                    Assert.LessOrEqual(UnityEditor.UnityStats.drawCalls,quality==0?80:120,"Editor draw-call budget");
                }
                Size(1600,720);yield return Frames(20);yield return Shot("gameview-"+(quality==0?"low":"balanced")+"-landscape");
                measurements.Add(new{quality=quality==0?"Low":"Balanced",route=4f,orientation="landscape",editorDrawCalls=UnityEditor.UnityStats.drawCalls,editorTriangles=UnityEditor.UnityStats.triangles,worldChunks=World().LoadedChunks});
                Size(720,1600);yield return Frames();
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
            var counts=new List<int[]>();
            var materialVisits=new List<Dictionary<string,string>>();
            for(int visit=0;visit<4;visit++)
            {
                Tap("back");yield return Frames(20);
                Assert.IsFalse(World().IsOpen);Assert.IsNull(Object.FindObjectsByType<Camera>().FirstOrDefault(c=>c.name=="Roadmap perspective camera"));
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
                product=UnityEngine.Application.productName,sourceHash=Resources.Load<RoadmapWorldAsset>("QuietCamp/CinematicRoadmap/World").sourceHash,playerBuild=false,mobileFpsMeasured=false,audioSuppressionRequested=true,audioOutputVerified=false,measurements,reentryCounts=counts,movieFrameCount=193,capturedSeconds,
                checks="Fresh campaign progress after onboarding, completion 1–5, old progress including gaps, replay, drag rejection, quality change, synthetic notch, portrait/landscape, reduced motion, interrupted reveal, repeated entry/exit"},Formatting.Indented)+"\n");
        }
        static void PointerReplay(GameServices services){services.PendingMenuScreen="Levels";PrivacyBootTestSupport.Tap(PrivacyBootTestSupport.Find("continue"));}
    }
}
#endif
