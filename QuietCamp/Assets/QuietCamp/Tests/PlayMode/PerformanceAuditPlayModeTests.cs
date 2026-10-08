#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using QuietCamp.Application;
using QuietCamp.Infrastructure;
using QuietCamp.Presentation;
using QuietCamp.Presentation.UI;
using QuietCamp.Presentation.World;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using Stopwatch = System.Diagnostics.Stopwatch;
namespace QuietCamp.Tests
{
    [DefaultExecutionOrder(-30000)]
    public sealed class PerformanceFrameCollector : MonoBehaviour
    {
        public readonly List<Frame> Frames = new List<Frame>(16384);
        public readonly List<Counter> Counters = new List<Counter>();
        public readonly List<object> Inventory = new List<object>();
        long _tick;
        string _stage = "unassigned", _state = "Idle";
        int _sampleFrame;
        FoliageDiveTransition _dive;
        public struct Frame { public int frame; public double timestampMs, wallMs; public string stage, transition; public long[] values; public int tier, gc0; }
        public sealed class Counter { public string name, category, unit; public ProfilerRecorder recorder; }
        readonly string[] _wanted = { "Main Thread", "Render Thread", "PlayerLoop", "EditorLoop", "GC Allocated In Frame",
            "GC Used Memory", "Total Used Memory", "Draw Calls Count", "SetPass Calls Count", "Batches Count", "Triangles Count", "Vertices Count", "GPU Frame Time", "Gfx.WaitForPresentOnGfxThread", "WaitForTargetFPS", "GC.Collect", "Canvas.BuildBatch", "Canvas.SendWillRenderCanvases", "Update.ScriptRunBehaviourUpdate", "Update.ScriptRunBehaviourLateUpdate" };
        public void Begin()
        {
            var handles = new List<ProfilerRecorderHandle>(); ProfilerRecorderHandle.GetAvailable(handles);
            foreach (var handle in handles)
            {
                var d = ProfilerRecorderHandle.GetDescription(handle);
                Inventory.Add(new { name = d.Name, category = d.Category.Name, unit = d.UnitType.ToString() });
                if (!_wanted.Contains(d.Name) || Counters.Any(c => c.name == d.Name)) continue;
                var recorder = ProfilerRecorder.StartNew(d.Category, d.Name, 1);
                if (!recorder.Valid) { recorder.Dispose(); continue; }
                Counters.Add(new Counter { name = d.Name, category = d.Category.Name, unit = d.UnitType.ToString(), recorder = recorder });
            }
            _tick = Stopwatch.GetTimestamp(); _sampleFrame = Time.frameCount;
        }
        public void RefreshRenderCounters()
        {
            // Several render counters appear only after the first rendered scene.
            // Prefer scene rendering to the identically named UI Toolkit counters.
            foreach (string name in new[] { "Batches Count", "Draw Calls Count", "Vertices Count" })
            {
                var recorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, name, 1);
                if (!recorder.Valid) { recorder.Dispose(); continue; }
                var prior = Counters.FirstOrDefault(c => c.name == name);
                if (prior != null) { prior.recorder.Dispose(); prior.recorder = recorder; prior.category = "Render"; }
                else Counters.Add(new Counter { name = name, category = "Render", unit = "Count", recorder = recorder });
            }
            var handles = new List<ProfilerRecorderHandle>(); ProfilerRecorderHandle.GetAvailable(handles);
            Inventory.Clear();
            foreach (var handle in handles)
            {
                var d = ProfilerRecorderHandle.GetDescription(handle);
                Inventory.Add(new { name = d.Name, category = d.Category.Name, unit = d.UnitType.ToString() });
            }
        }
        void Update()
        {
            if (_tick == 0 || !PerformanceAudit.Enabled) return;
            long now = Stopwatch.GetTimestamp();
            if (Frames.Count < 30000) Frames.Add(new Frame { frame = _sampleFrame, stage = _stage, transition = _state,
                timestampMs = PerformanceAudit.ElapsedMs, wallMs = (now - _tick) * 1000.0 / Stopwatch.Frequency,
                values = Values(), tier = QuietCampBootstrap.ServicesRef?.EffectiveQuality ?? -1, gc0 = GC.CollectionCount(0) });
            _tick = now;
        }
        long[] Values() { var values = new long[Counters.Count]; for (int i=0;i<values.Length;i++) values[i]=Counters[i].recorder.LastValue; return values; }
        void LateUpdate()
        {
            _stage = PerformanceAudit.Stage; _sampleFrame = Time.frameCount;
            if (_dive == null) _dive = Object.FindFirstObjectByType<FoliageDiveTransition>();
            _state = _dive != null ? _dive.Current.ToString() : "Idle";
        }
        void OnDestroy() { foreach (var counter in Counters) counter.recorder.Dispose(); }
    }

    public sealed class PerformanceAuditPlayModeTests
    {
        readonly ScreenshotPlayModeTest _input = new ScreenshotPlayModeTest();
        PerformanceFrameCollector _collector;
        readonly List<object> _operations = new List<object>();
        readonly List<object> _snapshots = new List<object>();
        int _workers, _vsync, _target;
        string _reportName = "editor-audit.json";
        static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        static MenuScreens Menu => (MenuScreens)typeof(MenuSceneHost).GetField("_screens", Private).GetValue(MenuSceneHost.Current);
        static ScreenRouter Router => (ScreenRouter)typeof(QuietCampBootstrap).GetField("_router", Private).GetValue(Object.FindAnyObjectByType<QuietCampBootstrap>());
        [SetUp] public void SetUp()
        {
            _input.SyntheticInput(); _workers = Unity.Jobs.LowLevel.Unsafe.JobsUtility.JobWorkerCount;
            _vsync = QualitySettings.vSyncCount; _target = UnityEngine.Application.targetFrameRate;
            Unity.Jobs.LowLevel.Unsafe.JobsUtility.JobWorkerCount = 1;
        }
        [TearDown] public void Cleanup()
        {
            if (_collector != null) WriteReport();
            PerformanceAudit.Enabled = false;
            if (_collector != null) Object.Destroy(_collector.gameObject);
            _input.RestoreInput(); Unity.Jobs.LowLevel.Unsafe.JobsUtility.JobWorkerCount = _workers;
            QualitySettings.vSyncCount = _vsync; UnityEngine.Application.targetFrameRate = _target;
        }
        static void Stage(string name) { PerformanceAudit.Stage = name; Debug.Log("[QC-PERF] " + name); }
        static IEnumerator Frames(int count) { for (int i = 0; i < count; i++) yield return null; }
        IEnumerator Sample(string label, int count = 150)
        {
            Stage(label); yield return Frames(count); Snapshot(label);
        }
        void Snapshot(string stage)
        {
            PerformanceAudit.Stage = "audit.snapshot." + stage;
            double start = PerformanceAudit.ElapsedMs;
            var renderers = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
            var filters = Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None);
            var leaf = Object.FindFirstObjectByType<LeafCurtainGraphic>();
            var mesh = leaf != null ? leaf.canvasRenderer.GetMesh() : null;
            _snapshots.Add(new { stage, frame = Time.frameCount, startMs = start, scene = SceneManager.GetActiveScene().name, levelId = CampSceneHost.Current?.Session?.Level?.id,
                renderers = renderers.Length, meshFilters = filters.Length, uniqueMeshVertices = filters.Where(f => f.sharedMesh != null).Select(f => f.sharedMesh).Distinct().Sum(m => (long)m.vertexCount),
                uniqueMaterials = renderers.SelectMany(r => r.sharedMaterials).Where(m => m != null).Distinct().Count(),
                transforms = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Length,
                canvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Length,
                graphics = Object.FindObjectsByType<Graphic>(FindObjectsSortMode.None).Length,
                activeLeavesVertices = mesh != null ? mesh.vertexCount : 0, managedHeapBytes = GC.GetTotalMemory(false),
                unityAllocatedBytes = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong(), endMs = PerformanceAudit.ElapsedMs });
        }
        IEnumerator WaitTask(Task task, double deadline)
        {
            while (!task.IsCompleted && PerformanceAudit.ElapsedMs < deadline) yield return null;
            Assert.IsTrue(task.IsCompleted, "Transition task exceeded the audit wall-clock bound.");
            Assert.IsFalse(task.IsFaulted, task.Exception?.ToString());
        }
        IEnumerator PureLeaves(GameServices services, int tier, bool reduced, int repetition)
        {
            services.ReducedMotion = reduced;
            string label = $"leaves.tier{tier}.{(reduced ? "reduced" : "normal")}.r{repetition}"; Stage(label);
            var dive = FoliageDiveTransition.Ensure(services);
            double start = PerformanceAudit.ElapsedMs;
            yield return WaitTask(dive.CoverAsync(), start + 20000);
            Snapshot(label + ".covered"); yield return Frames(1);
            Stage(label + ".hold"); yield return Frames(19);
            Stage(label + ".reveal");
            dive.BeginReveal(); yield return WaitTask(dive.RevealAsync(), PerformanceAudit.ElapsedMs + 20000);
            _operations.Add(new { label, startMs = start, endMs = PerformanceAudit.ElapsedMs, elapsedMs = PerformanceAudit.ElapsedMs - start, kind = "leaves-only", tier, reduced, repetition });
            Stage(label + ".settle"); yield return Frames(15);
        }
        IEnumerator Route(GameServices services, string label, string levelId = null)
        {
            Stage(label); double start = PerformanceAudit.ElapsedMs;
            var router = Router;
            if (levelId == null) router.GoToMenu(); else router.GoToCamp(levelId);
            while ((router.IsBusy || !router.Dive.IsIdle) && PerformanceAudit.ElapsedMs < start + 45000) yield return null;
            Assert.IsFalse(router.IsBusy, "Router exceeded audit bound: " + label);
            Assert.IsTrue(router.Dive.IsIdle, "Transition gate remained active: " + label);
            if (levelId != null) { Assert.NotNull(CampSceneHost.Current); Assert.AreEqual(levelId, CampSceneHost.Current.Session.Level.id); }
            else Assert.IsTrue(MenuSceneHost.Current != null && MenuSceneHost.Current.IsReady);
            _operations.Add(new { label, startMs = start, endMs = PerformanceAudit.ElapsedMs, elapsedMs = PerformanceAudit.ElapsedMs - start, kind = "route", tier = services.EffectiveQuality, reduced = services.ReducedMotion, levelId });
            Snapshot(label + ".ready");
        }
        static void Tier(GameServices services, int tier)
        {
            services.Settings.quality = tier + 1; services.EffectiveQuality = tier;
            QualitySettings.SetQualityLevel(tier, true); QualitySettings.vSyncCount = 0;
            UnityEngine.Application.targetFrameRate = 60;
        }
        [UnityTest]
        [Timeout(600000)]
        public IEnumerator RunPerformanceMap()
        {
            if (UnityEngine.Application.isBatchMode || !UnityEngine.Application.productName.StartsWith("QuietCampPerfQA"))
                Assert.Ignore("Requires rendered Game View and distinct performance QA storage.");
            typeof(ScreenshotPlayModeTest).GetMethod("SetGameViewSize", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { 720, 1600 });
            PerformanceAudit.Reset(); Stage("boot.first-measured");
            var root = new GameObject("QuietCampPerformanceCollector"); Object.DontDestroyOnLoad(root);
            _collector = root.AddComponent<PerformanceFrameCollector>(); _collector.Begin();
            yield return SceneManager.LoadSceneAsync("Boot"); yield return PrivacyBootTestSupport.EnterGame();
            var services = QuietCampBootstrap.ServicesRef;
            services.Tutorial.Skip(); services.Tutorial.MarkMenuIntroSeen();
            foreach (var sign in new[] { "shade", "quiet", "friends" }) services.Tutorial.ExplainSign(sign);
            services.Progression.Restore(LevelLoader.MvpLevelIds().ToArray(), "QC007", services.Progression.CosmeticFlags);
            services.Localization.TrySetLanguage("uk"); services.Settings.language = "uk";
            services.Settings.master = 0; services.Audio?.SetBusVolume(Kruty1918.Audio.AudioBus.Master, 0);
            var adaptive = Object.FindAnyObjectByType<AdaptiveCampQuality>(); if (adaptive != null) adaptive.enabled = false;
            Tier(services,2); services.ReducedMotion = false;
            Stage("menu.high.initial-load"); yield return SceneManager.LoadSceneAsync("MainMenu"); yield return Frames(45);
            _collector.RefreshRenderCounters();
            yield return Sample("menu.high.steady");
            for (int tier = 0; tier < 3; tier++)
            {
                Tier(services,tier); yield return Frames(30);
                yield return Sample($"menu.tier{tier}.steady",120);
                for (int rep=1;rep<=2;rep++)
                { yield return PureLeaves(services,tier,false,rep); yield return PureLeaves(services,tier,true,rep); }
                services.ReducedMotion=false;
                yield return Route(services,$"route.tier{tier}.menu-camp","QC007"); yield return Frames(30);
                yield return Sample($"camp.summer.tier{tier}.empty",120);
                CampSceneHost.Current.Session.DebugApplyWitness(); CampSceneHost.Current.Session.Select(null); yield return Frames(30);
                yield return Sample($"camp.summer.tier{tier}.solved",120);
                yield return Route(services,$"route.tier{tier}.camp-menu"); yield return Frames(30);
            }
            Tier(services,2);
            for (int rep=1;rep<=3;rep++)
                foreach (bool reduced in new[] { false,true })
                {
                    services.ReducedMotion=reduced;
                    string mode=reduced?"reduced":"normal";
                    yield return Route(services,$"route.high.{mode}.r{rep}.menu-camp","QC007"); yield return Frames(20);
                    yield return Route(services,$"route.high.{mode}.r{rep}.camp-menu"); yield return Frames(20);
                }
            services.ReducedMotion=false;
            foreach (var item in new[] { new[]{"spring","QC001"}, new[]{"autumn","gen:qc_camp:9"}, new[]{"winter","gen:qc_camp:14"},new[]{"late-haven","gen:qc_camp:122"} })
            {
                yield return Route(services,"route.high.to-"+item[0],item[1]);
                CampSceneHost.Current.Session.DebugApplyWitness(); CampSceneHost.Current.Session.Select(null); yield return Frames(45);
                yield return Sample("camp."+item[0]+".high.solved",180);
            }
            yield return Route(services,"route.high.final-camp-menu"); yield return Frames(45);
            foreach(string screen in new[]{"Levels","Journeys","Settings"})
            {
                Stage("ui."+screen+".open"); Menu.Show(screen); yield return Frames(30);
                yield return Sample("ui."+screen+".steady",120);
                if(screen=="Levels")
                {
                    Stage("ui.map.scroll");
                    var scroll=Object.FindObjectsByType<ScrollRect>(FindObjectsSortMode.None).First(s=>s.gameObject.activeInHierarchy&&s.content!=null&&s.vertical);
                    for(int i=0;i<90;i++){scroll.verticalNormalizedPosition=1-i/89f;yield return null;}
                    Snapshot("ui.map.scroll");
                }
            }
            services.Save.Album.entries = new[]{"QC001","QC007","gen:qc_camp:9","gen:qc_camp:14"}.Select((id,index)=>{
                var level=LevelLoader.Load(id);var session=new CampSession(level);session.DebugApplyWitness();
                return new AlbumSaveData.Entry { levelId=id,order=index,levelSnapshot=level,placements=session.State.Placements.Select(p=>p.Copy()).ToArray(),lighting=level.lighting,cared=true };
            }).ToArray();
            services.AlbumIndex=1;Stage("ui.album.open");Menu.Show("Album");yield return Frames(45);
            yield return Sample("ui.album.high.steady",150);
            var album=Object.FindFirstObjectByType<AlbumDiorama>();Assert.NotNull(album);
            Stage("ui.album.rotate");for(int i=0;i<120;i++){album.Rotate(3);yield return null;}Snapshot("ui.album.rotate");
            Menu.Show("AlbumQuiet");yield return Frames(45);yield return Sample("ui.album.quiet.high",150);
            Stage("finished");Assert.Greater(_collector.Frames.Count,1000);Assert.Greater(PerformanceAudit.Spans.Count,1000);
        }
        [UnityTest]
        [Timeout(180000)]
        public IEnumerator RunRoadmapPerformance()
        {
            if (UnityEngine.Application.isBatchMode || !UnityEngine.Application.productName.StartsWith("QuietCampPerfQA"))
                Assert.Ignore("Requires rendered Game View and distinct performance QA storage.");
            _reportName = "editor-roadmap-audit.json";
            typeof(ScreenshotPlayModeTest).GetMethod("SetGameViewSize", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { 720, 1600 });
            PerformanceAudit.Reset(); Stage("roadmap.boot");
            var root = new GameObject("QuietCampPerformanceCollector"); Object.DontDestroyOnLoad(root);
            _collector = root.AddComponent<PerformanceFrameCollector>(); _collector.Begin();
            yield return SceneManager.LoadSceneAsync("Boot"); yield return PrivacyBootTestSupport.EnterGame();
            var services = QuietCampBootstrap.ServicesRef; services.Tutorial.Skip(); services.Tutorial.MarkMenuIntroSeen();
            services.Progression.Restore(LevelLoader.MvpLevelIds().ToArray(), "QC007", services.Progression.CosmeticFlags);
            services.Localization.TrySetLanguage("uk"); services.Settings.language = "uk";
            services.Settings.master = 0; services.Audio?.SetBusVolume(Kruty1918.Audio.AudioBus.Master, 0);
            var adaptive = Object.FindAnyObjectByType<AdaptiveCampQuality>(); if (adaptive != null) adaptive.enabled = false;
            Tier(services, 2); services.ReducedMotion = false;
            yield return SceneManager.LoadSceneAsync("MainMenu"); yield return Frames(45); _collector.RefreshRenderCounters();
            Stage("ui.map.open"); Menu.Show("Levels"); yield return Frames(30);
            yield return Sample("ui.map.steady", 120);
            var scroll = Object.FindObjectsByType<ScrollRect>(FindObjectsSortMode.None).First(s => s.gameObject.activeInHierarchy && s.content != null && s.vertical);
            Stage("ui.map.sweep");
            for (int i = 0; i < 90; i++) { scroll.verticalNormalizedPosition = 1 - i / 89f; yield return null; }
            Snapshot("ui.map.sweep"); yield return Frames(1);
            Stage("ui.map.reposition"); scroll.verticalNormalizedPosition = .9f; yield return Frames(30);
            Stage("ui.map.drag");
            for (int i = 0; i < 120; i++) { scroll.verticalNormalizedPosition = .9f - .05f * i / 119f; yield return null; }
            Snapshot("ui.map.drag"); yield return Frames(1);
            Stage("ui.map.final-steady"); yield return Frames(120);
            Stage("finished"); Assert.Greater(_collector.Frames.Count, 300);
        }
        void WriteReport()
        {
            var output=Path.Combine(Directory.GetCurrentDirectory(),"TestResults","Performance2026-10-08");Directory.CreateDirectory(output);
            var context=JObject.Parse(UnityEditor.SessionState.GetString("QcPerf.Context","{}"));
            var report=new { schemaVersion=1,capturedUtc=DateTime.UtcNow.ToString("o"),unityVersion=UnityEngine.Application.unityVersion,
                product=UnityEngine.Application.productName,screen=new[]{Screen.width,Screen.height},source=context,
                host=new{cpu=SystemInfo.processorType,cores=SystemInfo.processorCount,ramMb=SystemInfo.systemMemorySize,gpu=SystemInfo.graphicsDeviceName,graphicsApi=SystemInfo.graphicsDeviceType.ToString()},
                methodology=new{renderedEditor=true,playerBuild=false,deepProfiling=false,workerCount=1,targetFps=60,vsync=0,adaptiveQualityDisabled=true,audioMasterMuted=true,
                    profilerCountersArePreviousCompletedFrame=true,wallIntervalIncludesEditorAndOS=true,scopesInclusive=true,syntheticQaProgress=true},
                counters=_collector.Counters.Select(c=>new{name=c.name,category=c.category,unit=c.unit}).ToArray(),availableCounters=_collector.Inventory,
                frames=_collector.Frames,spans=PerformanceAudit.Spans,operations=_operations,snapshots=_snapshots,
                instrumentationOverhead=new{frameValueArrayBytesApprox=_collector.Counters.Count*8,frameCount=_collector.Frames.Count,spanCount=PerformanceAudit.Spans.Count},
                terminalStage=PerformanceAudit.Stage };
            File.WriteAllText(Path.Combine(output,_reportName),Newtonsoft.Json.JsonConvert.SerializeObject(report,Newtonsoft.Json.Formatting.None));
            Debug.Log("[QC-PERF] Report written: frames="+_collector.Frames.Count+" spans="+PerformanceAudit.Spans.Count);
        }
    }
}
#endif
