#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using QuietCamp.Application;
using QuietCamp.Domain;
using QuietCamp.Infrastructure;
using TMPro;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.UI;

namespace QuietCamp.Presentation.UI.Prepared
{
    /// <summary>Editor-only reproducible workload. Synthetic progress and RAM-only saves, no live profile.</summary>
    public sealed class RoadmapBenchmarkHarness : MonoBehaviour
    {
        public bool autoRun=true;
        public bool ukrainianRural;
        [NonSerialized] public RoadmapDefinition definitionOverride;
        [NonSerialized] public LevelSummary[] summariesOverride;
        public float durationSeconds=180;
        public int completedLevels=340;
        public string fixtureName="benchmark-360",summaryName="benchmark-summaries";
        public RoadmapGraphic Map {get;private set;}
        public ScrollRect Scroll {get;private set;}
        public GameServices Services {get;private set;}
        public string outputDirectoryOverride;
        public string OutputDirectory=>outputDirectoryOverride??Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath,"../../TestResults/roadmap-production-2026-10-07"));
        readonly Dictionary<string,LevelSummary> _summaries=new Dictionary<string,LevelSummary>();
        readonly List<Transform> _worldTransforms=new List<Transform>(64);
        readonly List<MeshRenderer> _worldRenderers=new List<MeshRenderer>(32);
        readonly List<Transform> _transforms=new List<Transform>(512);
        readonly List<CanvasRenderer> _renderers=new List<CanvasRenderer>(256);
        readonly HashSet<Material> _materials=new HashSet<Material>();
        readonly List<ProfilerRecorderHandle> _handles=new List<ProfilerRecorderHandle>();
        ProfilerRecorder _main,_gc,_draw,_memory,_canvas;
        Sample[] _samples;
        int _count,_objects,_rendererCount,_materialCount,_worldRendererCount;
        long _baseline;
        float _buildStarted,_buildMilliseconds,_readyMilliseconds;
        bool _assetCacheWarm,_coldReady,_settled,_scopedGcSupported;
        float _nextCensus;
        [Serializable] public struct Sample
        {
            public double frameMs,cpuMs,gcBytes,drawCalls,canvasNs,memoryDelta,roadmapNs,activateNs,gladeNs,backgroundNs,controlsNs,ownedGcBytes;
            public int objects,renderers,materials,chunks,scenes,controls,vertices,worldRenderers;
        }
        IEnumerator Start()
        {
            if(!autoRun)yield break;
            Build();yield return Run();
        }
        public void Build()
        {
            _buildStarted=Time.realtimeSinceStartup;_assetCacheWarm=RoadmapModelLibrary.IsLoaded;
            string fixture=Path.Combine(UnityEngine.Application.dataPath,"QuietCamp/Tests/Fixtures/Roadmap/");
            var definition=definitionOverride??JsonConvert.DeserializeObject<RoadmapDefinition>(File.ReadAllText(fixture+fixtureName+".json"));
            if(ukrainianRural)
            {
                if(definition.presentation!="world3d")throw new InvalidOperationException("Cultural benchmark requires a world3d fixture.");
                foreach(var region in definition.regions)region.culturalLandscape=new RoadmapCulturalLandscapeData();
            }
            var summaries=summariesOverride??JsonConvert.DeserializeObject<LevelSummary[]>(File.ReadAllText(fixture+summaryName+".json"));
            foreach(var s in summaries)_summaries.Add(s.id,s);
            var progress=new ProgressionService();for(int i=0;i<Math.Min(completedLevels,summaries.Length);i++)progress.MarkCompleted(summaries[i].id);
            var save=new SaveAdapter(memoryOnly:true);save.Settings.haptics=false;
            Services=new GameServices(save,QuietCampLocalization.Create(Path.Combine(Path.GetTempPath(),"quietcamp-roadmap-benchmark-locale.txt")),null,null,null,null,null,null,null,null,null,null,null,null,null,progress);
            Services.ReducedMotion=false;
            var root=new GameObject("RoadmapBenchmarkCanvas",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));root.transform.SetParent(transform,false);
            var canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;
            var scaler=root.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(850,1700);scaler.matchWidthOrHeight=.5f;
            var area=QcUi.Stretch(root.GetComponent<RectTransform>(),"RoadmapScroll");Scroll=area.gameObject.AddComponent<ScrollRect>();Scroll.horizontal=false;
            var viewport=QcUi.Stretch(area,"Viewport");viewport.gameObject.AddComponent<Image>().color=new Color(.23f,.33f,.27f);viewport.gameObject.AddComponent<RectMask2D>();Scroll.viewport=viewport;
            var content=QcUi.Stretch(viewport,"Content");content.anchorMin=new Vector2(0,1);content.anchorMax=Vector2.one;content.pivot=new Vector2(.5f,1);content.sizeDelta=new Vector2(0,new RoadmapCatalog(definition).Height);Scroll.content=content;
            Map=QcUi.Stretch(content,"RoadmapMesh").gameObject.AddComponent<RoadmapGraphic>();Map.raycastTarget=false;
            Map.Configure(Services,new RoadmapCatalog(definition),id=>_summaries.TryGetValue(id,out var s)?s:null);
            Scroll.verticalNormalizedPosition=1;
            _buildMilliseconds=(Time.realtimeSinceStartup-_buildStarted)*1000;
        }
        public void RecordColdReady()
        {
            if(_coldReady||Map==null||!Map.IsReady)return;
            _coldReady=true;_readyMilliseconds=(Time.realtimeSinceStartup-_buildStarted)*1000;
            Directory.CreateDirectory(OutputDirectory);
            var opening=new{fixture=fixtureName,ukrainianRural,levels=Map.Data.Nodes.Length,assetCacheWarm=_assetCacheWarm,buildMs=_buildMilliseconds,readyMs=_readyMilliseconds,editorOnly=true};
            File.AppendAllText(Path.Combine(OutputDirectory,"editor-openings-"+System.Diagnostics.Process.GetCurrentProcess().Id+".jsonl"),JsonConvert.SerializeObject(opening)+"\n");
        }
        ProfilerRecorder Record(string name)
        {
            foreach(var handle in _handles){var description=ProfilerRecorderHandle.GetDescription(handle);if(description.Name==name)return ProfilerRecorder.StartNew(description.Category,name,1);}
            return default;
        }
        public IEnumerator Run()
        {
            long allocatedBefore=GC.GetAllocatedBytesForCurrentThread();var allocationProbe=new byte[8192];GC.KeepAlive(allocationProbe);
            _scopedGcSupported=GC.GetAllocatedBytesForCurrentThread()>allocatedBefore;
            _samples=new Sample[Mathf.CeilToInt(Mathf.Max(1,durationSeconds)*240)+240];
            ProfilerRecorderHandle.GetAvailable(_handles);_main=Record("Main Thread");_gc=Record("GC Allocated In Frame");_draw=Record("Draw Calls Count");_memory=Record("Total Used Memory");_canvas=Record("Canvas.BuildBatch");
            var timeout=Time.realtimeSinceStartup+15;
            while(!Map.IsReady&&Time.realtimeSinceStartup<timeout)yield return null;
            RecordColdReady();
            if(!Map.IsReady)throw new InvalidOperationException("Roadmap benchmark did not reach readiness before sampling");
            for(int i=0;i<60;i++)yield return null;
            _baseline=_memory.Valid?_memory.LastValue:UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong();
            float start=Time.realtimeSinceStartup;
            while(Time.realtimeSinceStartup-start<durationSeconds)
            {
                float elapsed=Time.realtimeSinceStartup-start;
                // Traverses >20 nodes, reversals and distant jumps. Entry/branch/menu tests are separate.
                Scroll.verticalNormalizedPosition=1-Mathf.PingPong(elapsed/35,1);Scroll.velocity=Vector2.zero;
                if(Time.realtimeSinceStartup>=_nextCensus){Census();_nextCensus=Time.realtimeSinceStartup+1;}
                if(_count<_samples.Length)_samples[_count++]=new Sample{frameMs=Time.unscaledDeltaTime*1000,cpuMs=_main.Valid?_main.LastValue/1000000d:-1,gcBytes=_gc.Valid?_gc.LastValue:-1,drawCalls=_draw.Valid?_draw.LastValue:-1,canvasNs=_canvas.Valid?_canvas.LastValue:-1,
                    roadmapNs=Map.Work.Nanoseconds(RoadmapWorkMetrics.Frame,Time.frameCount-1),activateNs=Map.Work.Nanoseconds(RoadmapWorkMetrics.Activation,Time.frameCount-1),gladeNs=Map.Work.Nanoseconds(RoadmapWorkMetrics.Glade,Time.frameCount-1),backgroundNs=Map.Work.Nanoseconds(RoadmapWorkMetrics.Background,Time.frameCount-1),controlsNs=Map.Work.Nanoseconds(RoadmapWorkMetrics.Controls,Time.frameCount-1),ownedGcBytes=_scopedGcSupported?Map.Work.Allocated(Time.frameCount-1):-1,
                    memoryDelta=(_memory.Valid?_memory.LastValue:UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong())-_baseline,objects=_objects,renderers=_rendererCount,materials=_materialCount,chunks=Map.ActiveChunks,scenes=Map.ActiveIndices.Count,controls=Map.ActiveControls,vertices=Map.VisibleVertices,worldRenderers=_worldRendererCount};
                yield return null;
            }
            // A streaming traversal may end while the last neighbor is still preparing. Measure
            // cold readiness once, and settle separately instead of mislabeling this as a cold failure.
            Scroll.StopMovement();timeout=Time.realtimeSinceStartup+30;
            while(!Map.IsReady&&Time.realtimeSinceStartup<timeout)yield return null;
            _settled=Map.IsReady;
            Export();DisposeRecorders();
            if(!_settled)throw new InvalidOperationException("Roadmap did not settle after traversal");
        }
        void LateUpdate()
        {
            // Short, one-node fixtures still fill the viewport. The catalog retains its true height;
            // padding is presentation only and must not expose a differently colored empty rect.
            if(Scroll?.viewport==null||Map?.Data==null)return;
            RecordColdReady();
            float height=Mathf.Max(Map.Data.Height,Scroll.viewport.rect.height);
            if(Mathf.Abs(Scroll.content.rect.height-height)>.1f)Scroll.content.sizeDelta=new Vector2(0,height);
        }
        void Census()
        {
            _objects=_rendererCount=_worldRendererCount=0;_materials.Clear();
            GetComponentsInChildren(true,_transforms);foreach(var t in _transforms)if(t.gameObject.activeInHierarchy)_objects++;
            GetComponentsInChildren(true,_renderers);foreach(var renderer in _renderers)if(renderer.gameObject.activeInHierarchy){_rendererCount++;var material=renderer.GetMaterial();if(material!=null)_materials.Add(material);}
            if(Map.WorldRenderer!=null)
            {
                var world=Map.WorldRenderer.WorldRoot;world.GetComponentsInChildren(true,_worldTransforms);
                foreach(var t in _worldTransforms)if(t.gameObject.activeInHierarchy)_objects++;
                world.GetComponentsInChildren(true,_worldRenderers);
                foreach(var renderer in _worldRenderers)if(renderer.gameObject.activeInHierarchy){_worldRendererCount++;if(renderer.sharedMaterial!=null)_materials.Add(renderer.sharedMaterial);}
            }
            _materialCount=_materials.Count;
        }
        void Export()
        {
            Directory.CreateDirectory(OutputDirectory);var csv=new StringBuilder("frameMs,cpuMs,gcBytes,drawCalls,canvasNs,memoryDelta,activeGameObjects,canvasRenderers,materials,chunks,scenes,controls,vertices,roadmapNs,activateNs,gladeNs,backgroundNs,controlsNs,ownedGcBytes,worldRenderers\n");
            var ci=CultureInfo.InvariantCulture;
            for(int i=0;i<_count;i++){var s=_samples[i];csv.Append(s.frameMs.ToString(ci)).Append(',').Append(s.cpuMs.ToString(ci)).Append(',').Append(s.gcBytes.ToString(ci)).Append(',').Append(s.drawCalls.ToString(ci)).Append(',').Append(s.canvasNs.ToString(ci)).Append(',').Append(s.memoryDelta.ToString(ci)).Append(',').Append(s.objects).Append(',').Append(s.renderers).Append(',').Append(s.materials).Append(',').Append(s.chunks).Append(',').Append(s.scenes).Append(',').Append(s.controls).Append(',').Append(s.vertices).Append(',').Append(s.roadmapNs.ToString(ci)).Append(',').Append(s.activateNs.ToString(ci)).Append(',').Append(s.gladeNs.ToString(ci)).Append(',').Append(s.backgroundNs.ToString(ci)).Append(',').Append(s.controlsNs.ToString(ci)).Append(',').Append(s.ownedGcBytes.ToString(ci)).Append(',').Append(s.worldRenderers).Append('\n');}
            File.WriteAllText(Path.Combine(OutputDirectory,Map.IsWorldDiorama?"world3d-benchmark.csv":"editor-benchmark.csv"),csv.ToString());
            bool anyDraw=false;var frames=new double[_count];for(int i=0;i<_count;i++){frames[i]=_samples[i].frameMs;anyDraw|=_samples[i].drawCalls>0;}Array.Sort(frames);
            File.WriteAllText(Path.Combine(OutputDirectory,Map.IsWorldDiorama?"world3d-benchmark-context.json":"editor-benchmark-context.json"),JsonConvert.SerializeObject(new{editorOnly=true,deviceFPS=false,levels=Map.Data.Nodes.Length,frames=_count,durationSeconds,assetCacheWarm=_assetCacheWarm,buildMs=_buildMilliseconds,readyMs=_readyMilliseconds,readyPassed=_coldReady,settledPassed=_settled,p95FrameMs=_count>0?frames[(int)((_count-1)*.95)]:-1,p99FrameMs=_count>0?frames[(int)((_count-1)*.99)]:-1,worstFrameMs=_count>0?frames[_count-1]:-1,cpuSupported=_main.Valid,gcSupported=_gc.Valid,drawCounterPresent=_draw.Valid,drawSamplesNonzero=anyDraw,canvasSupported=_canvas.Valid,memorySupported=_memory.Valid,scopedGcSupported=_scopedGcSupported,roadmapWorkTiming="Stopwatch scopes, CPU only, excludes native Canvas batching/mesh upload and GPU",censusHz=1,note="-1 means unsupported; a present draw counter returning only zero is inconclusive. Census and global counters include harness/UI costs. Menu navigation and branch lifecycle require separate capture."},Formatting.Indented));
        }
        void DisposeRecorders()
        {if(_main.Valid)_main.Dispose();if(_gc.Valid)_gc.Dispose();if(_draw.Valid)_draw.Dispose();if(_memory.Valid)_memory.Dispose();if(_canvas.Valid)_canvas.Dispose();}
        void OnDestroy(){DisposeRecorders();Services?.Dispose();}
    }
}
#endif
