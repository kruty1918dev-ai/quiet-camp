#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using NUnit.Framework;
using QuietCamp.Application;
using QuietCamp.Domain;
using QuietCamp.Infrastructure;
using QuietCamp.Presentation;
using QuietCamp.Presentation.UI;
using QuietCamp.Presentation.World;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace QuietCamp.Tests
{
    public sealed class DeferredRoadmapPlayModeTests
    {
        static string Output => Environment.GetEnvironmentVariable("QC_DEFERRED_EVIDENCE_ROOT")
            ?? Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath,"../../Design/Roadmap/Paused/2026-10-11"));
        static IEnumerator Ready(string scene)
        {
            var until=Time.realtimeSinceStartup+90;
            while (Time.realtimeSinceStartup<until)
            {
                var dive=Object.FindAnyObjectByType<FoliageDiveTransition>();
                if ((dive==null||dive.Current==FoliageDiveTransition.State.Idle)
                    && SceneManager.GetActiveScene().name==scene
                    && (scene=="Camp" ? CampSceneHost.Current?.UiReady==true : MenuSceneHost.Current?.UiReady==true)) yield break;
                yield return null;
            }
            Assert.Fail("Scene readiness timed out: "+scene);
        }
        static void Tap(string id) => PrivacyBootTestSupport.Tap(PrivacyBootTestSupport.Find(id));
        static IEnumerator Frames(int count=20) { for(int i=0;i<count;i++)yield return null; }
        static void Size(int width,int height) => typeof(ScreenshotPlayModeTest)
            .GetMethod("SetGameViewSize",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic)
            .Invoke(null,new object[]{width,height});
        static IEnumerator Complete()
        {
            CampSceneHost.Current.Session.DebugApplyWitness();yield return Frames();Tap("check");
            var until=Time.realtimeSinceStartup+30;
            while(PrivacyBootTestSupport.Find("next")==null&&Time.realtimeSinceStartup<until)yield return null;
            Assert.IsTrue(CampSceneHost.Current.Session.IsCompleted);yield return Frames();
        }
        static IEnumerator CampReady(string id)
        {
            var until=Time.realtimeSinceStartup+90;
            while(CampSceneHost.Current?.Session?.Level?.id!=id&&Time.realtimeSinceStartup<until)yield return null;
            yield return Ready("Camp");Assert.AreEqual(id,CampSceneHost.Current.Session.Level.id);NoMap();
        }
        static void Act(string id) => QuietCampBootstrap.ServicesRef.Actions.Execute(
            new Kruty1918.UIActions.API.UiActionRequest(new Kruty1918.UIActions.API.UiActionId(id),
                Kruty1918.UIActions.API.UiActionSource.Programmatic,"Gameplay"));
        static IEnumerator Capture(string file)
        {
            yield return new WaitForEndOfFrame();
            var texture=ScreenCapture.CaptureScreenshotAsTexture();
            try { File.WriteAllBytes(Path.Combine(Output,file),texture.EncodeToPNG()); }
            finally { Object.Destroy(texture); }
        }
        static void NoMap()
        {
            Assert.IsNull(Object.FindAnyObjectByType<RoadmapWorldPresenter>());
            Assert.IsNull(Object.FindAnyObjectByType<RoadmapWorldInput>());
            Assert.IsNull(PrivacyBootTestSupport.Find("levels"));
            Assert.IsFalse(QuietCampBootstrap.ServicesRef.EditorRoadmapPreview);
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator DirectMenuPlayNextReturnAndArchiveExclusion()
        {
            Assert.IsTrue(UnityEngine.Application.productName.StartsWith("QuietCampRoadmapQA"),"Private synthetic QA saves required");
            Assert.IsFalse(UnityEngine.Application.isBatchMode,"Native Game View capture required");
            Size(720,1600);
            Directory.CreateDirectory(Output);
            // Inspect the actual imported asset graph without a BuildPipeline call.
            var roots=UnityEditor.AssetDatabase.GetAllAssetPaths().Where(p=>!p.Contains("/Editor/")
                && p.Contains("/Resources/") && !UnityEditor.AssetDatabase.IsValidFolder(p))
                .Concat(UnityEditor.EditorBuildSettings.scenes.Where(s=>s.enabled).Select(s=>s.path)).ToArray();
            var dependencies=UnityEditor.AssetDatabase.GetDependencies(roots,true);
            Assert.IsFalse(dependencies.Any(p=>p.StartsWith(RoadmapEditorArchive.Root+"/",StringComparison.Ordinal)),"Archive reachable from player roots");
            Assert.IsNull(Resources.Load<RoadmapWorldAsset>("QuietCamp/CinematicRoadmap/World"));
            Assert.NotNull(RoadmapEditorArchive.Load<RoadmapWorldAsset>("QuietCamp/CinematicRoadmap/World"),"Archived native world lost its import mapping");
            var save=new SaveAdapter();save.Load(out _);save.Progress=new ProgressSaveData();save.Session=new SessionSaveData();save.Album=new AlbumSaveData();
            var guide=new TutorialDirector(new TutorialSaveData(),false,new ProgressionService(),()=>true);guide.Skip();save.Progress.tutorial=guide.Save;
            save.Settings.reducedMotion=true;save.Settings.language="uk";Assert.IsTrue(save.Save());
            yield return SceneManager.LoadSceneAsync("Boot");yield return PrivacyBootTestSupport.EnterGame();yield return Ready("MainMenu");
            var services=QuietCampBootstrap.ServicesRef;services.Tutorial.Skip();services.ReducedMotion=true;
            var adaptive=Object.FindAnyObjectByType<AdaptiveCampQuality>();if(adaptive!=null)adaptive.enabled=false;
            NoMap();Assert.AreEqual("QC001",services.ContinueLevel());
            // A stale pending map destination is normalized to Main, with usable controls.
            var screens=(MenuScreens)typeof(MenuSceneHost).GetField("_screens",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(MenuSceneHost.Current);
            screens.Show("Levels");Assert.AreEqual("Main",screens.Current);
            for(int i=0;i<20;i++)yield return null;
            yield return Capture("main-menu-no-roadmap.png");
            Tap("continue");yield return Ready("Camp");Assert.AreEqual("QC001",CampSceneHost.Current.Session.Level.id);NoMap();
            yield return Capture("camp-001-portrait.png");
            yield return Complete();
            // React can publish the Button before Yoga has laid out its rectangle.
            // Wait for the rendered panel; Tap still requires a real unobstructed raycast.
            for(int i=0;i<20;i++)yield return null;
            Tap("next");yield return Ready("Camp");
            // Camp is reloaded: wait for the new level rather than an old host's readiness.
            var until=Time.realtimeSinceStartup+90;
            while(CampSceneHost.Current?.Session.Level.id!="QC002"&&Time.realtimeSinceStartup<until)yield return null;
            yield return Ready("Camp");Assert.AreEqual("QC002",CampSceneHost.Current.Session.Level.id);NoMap();
            Act("qc.menu");yield return Ready("MainMenu");NoMap();
            var ids=LevelLoader.MvpLevelIds();services.Progression.Restore(ids.Take(5),"QC005",0);services.Save.Session=new SessionSaveData();
            Assert.AreEqual("QC006",services.ContinueLevel());Assert.AreEqual("QC006",services.NextLevel("QC005"));
            Tap("continue");yield return Ready("Camp");Assert.AreEqual("QC006",CampSceneHost.Current.Session.Level.id);NoMap();
            Act("qc.menu");yield return Ready("MainMenu");NoMap();
            var backdrop=MenuSceneHost.Current.BackgroundLevel.id;
            services.Progression.Restore(ids.Take(45),ids[44],0);services.Save.Session=new SessionSaveData();
            Assert.AreEqual(ids[45],services.ContinueLevel());
            Assert.AreEqual(100,ids.Count);
            services.Progression.Restore(ids.Take(4),ids[3],0);services.Save.Session=new SessionSaveData();
            Tap("continue");yield return CampReady("QC005");yield return Complete();
            Tap("next");yield return CampReady("QC006");
            Act("qc.menu");yield return Ready("MainMenu");
            // Last camp must complete the base story and return through the real UI.
            services.Progression.Restore(ids.Take(99),ids[98],0);services.Save.Session=new SessionSaveData();
            Assert.AreEqual(ids[99],services.ContinueLevel());
            Tap("continue");yield return CampReady(ids[99]);yield return Capture("camp-100-portrait.png");
            Size(1600,720);yield return Frames();yield return Capture("camp-100-landscape.png");
            yield return Complete();Assert.IsNull(services.NextLevel(ids[99]));
            Tap("next");yield return Ready("MainMenu");NoMap();
            // Retired legacy content remains replayable as an album snapshot,
            // but cannot silently grow the active 100-level campaign.
            var retired=CampContent.Summaries.FirstOrDefault(s=>s.id=="gen:qc_camp:9");
            Assert.IsNull(retired);Assert.IsFalse(services.CanStart("gen:qc_camp:9"));
            services.AlbumIndex=Array.FindIndex(services.Save.Album.entries,e=>e.levelId==ids[99]);
            Assert.GreaterOrEqual(services.AlbumIndex,0);
            Tap("album");yield return Frames();Tap("album-replay");yield return CampReady(ids[99]);
            Act("qc.menu");yield return Ready("MainMenu");
            // Save/load retains all completed IDs and the exact old puzzle snapshot.
            Assert.IsTrue(services.Save.Save());var reopened=new SaveAdapter();Assert.IsTrue(reopened.Load(out _));
            Assert.AreEqual(100,reopened.Progress.completedIds.Intersect(ids).Count());
            var legacy=CampContent.Legacy("QC001");Assert.NotNull(legacy);
            Assert.AreNotEqual(LevelLoader.Load("QC001").contentHash,legacy.contentHash);
            File.WriteAllText(Path.Combine(Output,"native-flow-receipt.json"),JsonConvert.SerializeObject(new {
                capturedUtc=DateTime.UtcNow.ToString("o"),result="Passed",playerBuild=false,mobileFpsMeasured=false,
                playerDependencyCount=dependencies.Length,archiveReachable=false,worldAvailableInEditor=true,
                freshStart="QC001",next="QC002",afterPilot="QC006",legacyContinue=ids[45],backdrop,
                noMapComponents=true,noMapMenuControl=true,reducedMotion=true,rendererUntouchedByTest=true,
                baseCount=ids.Count,fifthToSixth=true,lastLevel=ids[99],afterLast="MainMenu",albumReplay=true,
                savedCompletions=100,retiredLaunchBlocked=true,legacySnapshotRetained=true
            },Formatting.Indented)+"\n");
        }
    }
}
#endif
