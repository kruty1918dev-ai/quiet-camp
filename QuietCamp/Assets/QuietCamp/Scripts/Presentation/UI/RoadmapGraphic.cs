using System;
using System.Collections.Generic;
using QuietCamp.Domain;
using QuietCamp.Infrastructure;
using UnityEngine;
using UnityEngine.UI;

namespace QuietCamp.Presentation.UI
{
    /// <summary>Viewport-culling front end to the procedural map scene system.
    /// The campaign remains ordinary scrollable HTML with native touch targets.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class RoadmapGraphic : MaskableGraphic
    {
        public const int Step=420, CentreY=210;
        public static float NodeX(float index)=>.5f+Mathf.Sin(index*.85f)*.21f;
        RoadmapSceneGenerator.Scene[] _scenes=Array.Empty<RoadmapSceneGenerator.Scene>();
        readonly List<int> _active=new List<int>(30);
        readonly Queue<RoadmapSceneGenerator.Scene> _scenePool=new Queue<RoadmapSceneGenerator.Scene>(30);
        readonly Vector3[] _corners=new Vector3[4];
        readonly List<RoadmapGladeGraphic> _pool=new List<RoadmapGladeGraphic>();
        readonly QuietCamp.Application.RoadmapWindow _window=new QuietCamp.Application.RoadmapWindow();
        RoadmapPainter _painter;
        RoadmapWeatherGraphic _weather;
        RoadmapNodeLayer _nodes;
        RoadmapWorldRenderer _worldRenderer;
        public RoadmapWorldRenderer WorldRenderer=>_worldRenderer;
        public QuietCamp.Application.ProgressionService Progress=>_services.Progression;
        public bool HasBakedComposition=>!string.IsNullOrEmpty(_data?.Definition.compositionManifest);
        public LevelSummary Summary(int index)=>_summary(_data.Nodes[index].levelId);
        public bool IsWorldDiorama=>_data?.Definition.presentation=="world3d";
        Material _ownedMaterial;
        ScrollRect _scroll;
        GameServices _services;
        AtmosphereCatalog _catalog;
        QuietCamp.Application.RoadmapCatalog _data;
        Func<string,LevelSummary> _summary;
        Coroutine _preparation,_geometryPreparation;
        readonly VertexHelper _geometryBuffer=new VertexHelper();
        Rect _visible;
        float _lastWidth;
        int _frontier,_lastTier=-1;
        QuietCamp.Application.RoadmapRevealState _reveal;
        float _revealFrom,_revealTo,_revealElapsed=2;
        bool _clamping;
        Color _revealFogFrom,_revealFogTo;
        public Color RevealFog=>Color.Lerp(_revealFogFrom,_revealFogTo,Mathf.SmoothStep(0,1,_revealElapsed/2));
        public bool RevealAnimating=>_revealElapsed<2;
        public float RevealDistance=>Mathf.Lerp(_revealFrom,_revealTo,Mathf.SmoothStep(0,1,_revealElapsed/2));
        public float KnownDistance=>_data.Y(_reveal.LastKnown);
        public QuietCamp.Application.RoadmapRevealState RevealState=>_reveal;
        public QuietCamp.Application.RoadmapEnvironmentSampler Environment {get;private set;}
        public RoadmapVisualProfile VisualAt(float distance)=>new RoadmapVisualProfile(Environment.Sample(Mathf.Min(distance,_data.Y(_reveal.LastKnown))));
        public float MaxScrollDistance=>_reveal.MaxScrollDistance(_scroll.viewport.rect.height);
        bool _manualWeather,_initialized;
        int _backgroundVertices;
        float _lowMemoryUntil;
        bool _wasLowMemory;
        public bool InputEnabled=true;
        public bool GestureActive;
        public float SelectionAfter;
        public float Zoom {get;private set;}=1;
        public float ZoomOffsetX {get;private set;}
        public void SetZoom(float zoom,Vector2 local,Vector2 worldPoint)
        {
            if(!HasBakedComposition)return;Zoom=Mathf.Clamp(zoom,1,1.8f);
            ZoomOffsetX=Mathf.Clamp(worldPoint.x-(local.x-rectTransform.rect.center.x)/(28*Zoom),-12,12);
            float distance=worldPoint.y+(local.y-_visible.center.y)/Zoom;
            float viewport=_scroll.viewport.rect.height,range=Mathf.Max(1,_data.Height-viewport);
            _scroll.verticalNormalizedPosition=1-Mathf.Clamp01(Mathf.Clamp(distance-viewport*.5f,0,MaxScrollDistance)/range);
            _nodes?.Invalidate();_weather?.SetVerticesDirty();
        }
        public readonly RoadmapWorkMetrics Work=new RoadmapWorkMetrics();
        public int ActiveControls=>_nodes?.ActiveControls??0;
        public IReadOnlyList<RoadmapSceneGenerator.Scene> Scenes=>_scenes;
        public IReadOnlyList<int> ActiveIndices=>_active;
        public QuietCamp.Application.RoadmapCatalog Data=>_data;
        public int ActiveChunks=>_window.Count;
        public int FirstActiveChunk=>_window.First;
        public int LastActiveChunk=>_window.Last;
        public int AvailableSceneLeases=>_scenePool.Count;
        public int Frontier=>_frontier;
        public Rect VisibleArea=>_visible;
        public bool IsReady=>_initialized&&_scroll!=null&&_scroll.viewport!=null&&_visible.height>0&&_preparation==null&&(_nodes?.Prepared??false)&&(IsWorldDiorama?(_worldRenderer?.Ready??false):_geometryPreparation==null&&GeometryReady&&(_branchLayer?.Ready??true)&&_backgroundVertices>0);
        bool GeometryReady {get {foreach(var g in _pool)if(g.gameObject.activeSelf&&g.NeedsGeometry)return false;return true;}}
        public int VisibleGlades {get;private set;}
        public IReadOnlyList<RoadmapGladeGraphic> GladePool=>_pool;
        public int PaintedModels {get {int n=0;foreach(var g in _pool)if(g!=null&&g.gameObject.activeSelf)n+=g.Models;return n;}}
        public int ShadowCasters {get {int n=0;foreach(var g in _pool)if(g!=null&&g.gameObject.activeSelf)n+=g.Shadows;return n;}}
        public int TruncatedModels {get {int n=0;foreach(var g in _pool)if(g!=null&&g.gameObject.activeSelf)n+=g.Truncated;return n;}}
        public int VisibleVertices {get {if(IsWorldDiorama)return _worldRenderer?.Vertices??0;int n=_backgroundVertices;foreach(var g in _pool)if(g!=null&&g.gameObject.activeSelf)n+=g.Vertices;return n;}}
        public int QualityTier
        {
            get
            {
                int tier=_services?.EffectiveQuality??1;
                if(_data==null||_window.Count==0)return tier;
                var region=_data.Definition.regions[_data.RegionAt(rectTransform.rect.yMax-_visible.center.y)];
                return Mathf.Min(tier,region.performanceTier=="low"?0:region.performanceTier=="high"?2:1);
            }
        }
        public bool Reduced=>_services?.ReducedMotion??false;
        public bool HasCulturalLandscape=>_data?.Definition.regions[0].culturalLandscape!=null;
        public float ProjectionScale=>HasCulturalLandscape?28*Zoom:28*Mathf.Clamp(rectTransform.rect.width/850,1,1.28f);
        RoadmapBranchLayer _branchLayer;
        public event Action<string> LevelSelected;
        public event Action<RoadmapBranchData> BranchSelected;
        public void Configure(GameServices services,QuietCamp.Application.RoadmapCatalog data=null,Func<string,LevelSummary> summary=null)
        {
            if(_initialized){RefreshProgress();return;}
            // Stage all fallible resources before committing initialization. Retry cannot keep a partial map.
            var definition=data??RoadmapRepository.Main;
            var profiles=AtmosphereCatalog.Load();var library=string.IsNullOrEmpty(definition.Definition.compositionManifest)?RoadmapModelLibrary.Load():null;library?.PrepareSeasons();
            if(definition.Definition.regions[0].culturalLandscape!=null)library?.PrepareCulture();
            var painter=library!=null?new RoadmapPainter(library):null;var scenes=new RoadmapSceneGenerator.Scene[definition.Nodes.Length];
            var shader=definition.Definition.presentation=="world3d"?null:Resources.Load<Shader>("QuietCamp/RoadmapCanopy");
            _services=services;_data=definition;_catalog=profiles;_painter=painter;_scenes=scenes;_summary=summary??RoadmapRepository.Summary;
            Environment=new QuietCamp.Application.RoadmapEnvironmentSampler(definition);
            _scroll=GetComponentInParent<ScrollRect>();_reveal=new QuietCamp.Application.RoadmapRevealState(definition,services.Progression);_frontier=_reveal.Frontier;
            _revealTo=definition.Y(_frontier);_revealFrom=_revealTo;_revealFogFrom=_revealFogTo=FogFor(_frontier);
            if(services.RoadmapSeenFrontiers.TryGetValue(definition.Definition.journeyId,out var seen)&&seen<_frontier)
            {_revealFrom=definition.Y(Mathf.Clamp(seen,0,definition.Nodes.Length-1));_revealFogFrom=FogFor(Mathf.Clamp(seen,0,definition.Nodes.Length-1));_revealElapsed=0;}
            services.RoadmapSeenFrontiers[definition.Definition.journeyId]=_frontier;
            if(_scroll!=null)_scroll.onValueChanged.AddListener(ClampScroll);
            if(shader!=null){_ownedMaterial=new Material(shader){name="Roadmap shared canopy"};material=_ownedMaterial;canvas.additionalShaderChannels|=AdditionalCanvasShaderChannels.TexCoord1|AdditionalCanvasShaderChannels.TexCoord2;}
            try
            {
            // Fixed native and managed pools: scene/prop instances are reused when leases move.
            for(int i=0;i<30;i++)_scenePool.Enqueue(RoadmapSceneGenerator.Scene.Reusable());
            // No GameObject creation or scene generation in the scroll callback.
            for(int i=0;i<(IsWorldDiorama?0:16);i++)
            {var r=QcUi.Stretch(rectTransform,"RoadmapGladeSlot"+i);var g=r.gameObject.AddComponent<RoadmapGladeGraphic>();g.raycastTarget=false;g.material=material;g.gameObject.SetActive(false);_pool.Add(g);}
            if(!IsWorldDiorama){_branchLayer=QcUi.Stretch(rectTransform,"RoadmapBranches").gameObject.AddComponent<RoadmapBranchLayer>();_branchLayer.Configure(this);}
            _weather=QcUi.Stretch(rectTransform,"RoadmapWeather").gameObject.AddComponent<RoadmapWeatherGraphic>();_weather.Configure(this);
            _nodes=QcUi.Stretch(rectTransform,"RoadmapNodes").gameObject.AddComponent<RoadmapNodeLayer>();_nodes.Configure(this,services);
            if(IsWorldDiorama)
            {
                if(_scroll?.viewport==null)throw new InvalidOperationException("World roadmap requires a measured scroll viewport");
                _worldRenderer=gameObject.AddComponent<RoadmapWorldRenderer>();_worldRenderer.Configure(this,_scroll.viewport);
                if(HasBakedComposition)gameObject.AddComponent<RoadmapZoomController>().Configure(this,_scroll);
            }
            gameObject.AddComponent<RoadmapAmbientBlend>().Configure(this,services.Audio);
            _initialized=true;SetVerticesDirty();
            }
            catch
            {
                foreach(var g in _pool)if(g!=null)Destroy(g.gameObject);_pool.Clear();
                if(_worldRenderer!=null){Destroy(_worldRenderer);_worldRenderer=null;}
                if(_branchLayer!=null){Destroy(_branchLayer.gameObject);_branchLayer=null;}
                if(_weather!=null)Destroy(_weather.gameObject);if(_nodes!=null)Destroy(_nodes.gameObject);
                if(_ownedMaterial!=null)Destroy(_ownedMaterial);_ownedMaterial=null;material=null;
                _scenePool.Clear();_weather=null;_nodes=null;_data=null;_scenes=Array.Empty<RoadmapSceneGenerator.Scene>();throw;
            }
        }
        public void RefreshProgress()
        {
            if(_data==null||!_reveal.Refresh())return;
            int frontier=_reveal.Frontier;
            if(frontier!=_frontier){_revealFogFrom=RevealFog;_revealFogTo=FogFor(frontier);_revealFrom=RevealDistance;_revealTo=_data.Y(frontier);_revealElapsed=frontier>_frontier?0:2;_frontier=frontier;}
            _services.RoadmapSeenFrontiers[_data.Definition.journeyId]=_frontier;
            for(int k=_active.Count-1;k>=0;k--)
            {int i=_active[k];if(_scenes[i].StoryCompleted!=_services.Progression.IsCompleted(_data.Nodes[i].levelId)&&Array.Exists(_data.Nodes[i].world.props,p=>p.storyId!=null))
                {_worldRenderer?.InvalidateStory(i);_scenePool.Enqueue(_scenes[i]);_scenes[i]=null;_active.RemoveAt(k);}}
            RestartPreparation();RefreshPresentation();
        }
        Color FogFor(int index)
        {
            var summary=Summary(index);var palette=VisualAt(_data.Y(index)).Palette;
            var light=_catalog.Resolve(summary.id,summary.lighting);
            return Color.Lerp(Color.Lerp(palette.Fog,palette.GrassLight,.25f),palette.Ambient(light.Ambient),light.Id=="night"?.65f:.12f);
        }
        public void RefreshPresentation(){_branchLayer?.Invalidate();_nodes?.Invalidate();SetVerticesDirty();}
        public RoadmapReveal Reveal(int index)=>_reveal.Main(index);
        public bool BranchVisible(RoadmapBranchData branch)=>_reveal.BranchVisible(branch);
        public float RevealOpacity(int index)=>Mathf.SmoothStep(0,1,Mathf.InverseLerp(_data.Y(index)-200,_data.Y(index),RevealDistance));
        void ClampScroll(Vector2 unused)
        {
            if(_clamping||_data==null||_scroll?.viewport==null||_scroll.content==null||_scroll.viewport.rect.height<=0)return;
            float range=Mathf.Max(1,_data.Height-_scroll.viewport.rect.height);
            float minimum=1-Mathf.Clamp01(MaxScrollDistance/range);
            if(_scroll.verticalNormalizedPosition>=minimum)return;
            _clamping=true;_scroll.StopMovement();_scroll.verticalNormalizedPosition=minimum;_clamping=false;
        }
        public void SelectLevel(string id){int index=_data.LevelIndex(id);if(InputEnabled&&!GestureActive&&Time.unscaledTime>=SelectionAfter&&index>=0&&Reveal(index)!=RoadmapReveal.Hidden)LevelSelected?.Invoke(id);}
        public void SelectBranch(RoadmapBranchData branch){if(InputEnabled&&!GestureActive&&Time.unscaledTime>=SelectionAfter&&BranchVisible(branch))BranchSelected?.Invoke(branch);}
        public void SetWeatherMoment(float seconds)
        { _manualWeather=seconds>=0;foreach(var i in _active)_scenes[i].SetMoment(seconds);SetVerticesDirty();_weather?.SetVerticesDirty();foreach(var g in _pool)g.InvalidateGeometry(); }
        void LateUpdate()
        {
            if(!_initialized)return;
            using var timing=Work.Measure(RoadmapWorkMetrics.Frame);
            UnityEngine.Profiling.Profiler.BeginSample("Roadmap.Frame");
            try
            {
                RefreshProgress();ClampScroll(Vector2.zero);
                if(RevealAnimating&&_preparation==null&&(IsWorldDiorama?(_worldRenderer?.Ready??false):_geometryPreparation==null&&GeometryReady))
                {_revealElapsed=Mathf.Min(2,_revealElapsed+Time.unscaledDeltaTime*(Reduced?1.7f:1));_nodes.Invalidate();_branchLayer?.Invalidate();_weather.SetVerticesDirty();}
                var visible=FindVisibleArea();if(visible.height<=0)return;
                int region=_data.ChunkAt(rectTransform.rect.yMax-visible.center.y);
                bool lowMemory=Time.unscaledTime<_lowMemoryUntil;
                int neighbors=lowMemory&&ViewportFitsRegion(region)?0:1;
                if(_window.Move(region,_data.Chunks.Length,neighbors)||lowMemory!=_wasLowMemory){_wasLowMemory=lowMemory;RestartPreparation();}
                bool resized=Mathf.Abs(_lastWidth-rectTransform.rect.width)>.1f,quality=_lastTier!=QualityTier;
                if(visible!=_visible||resized||quality)
                { _visible=visible;_lastWidth=rectTransform.rect.width;_lastTier=QualityTier;SetVerticesDirty();_weather.SetVerticesDirty();_nodes.Invalidate();_branchLayer?.Invalidate();if(resized||quality)foreach(var g in _pool)g.InvalidateGeometry(); }
                if(!_manualWeather&&!Reduced)foreach(var i in _active)_scenes[i].Advance(Time.unscaledDeltaTime);
                if(!IsWorldDiorama)
                {
                    UpdatePool();
                    if(_geometryPreparation==null&&!GeometryReady)_geometryPreparation=StartCoroutine(PrepareGeometry());
                }
                if(_ownedMaterial!=null)
                { _ownedMaterial.SetFloat("_RoadmapTime",Time.unscaledTime);_ownedMaterial.SetFloat("_Motion",Reduced?0:1);UpdateMaskedMaterial(this);foreach(var g in _pool)if(g.gameObject.activeSelf)UpdateMaskedMaterial(g); }
            }
            finally {UnityEngine.Profiling.Profiler.EndSample();}
        }
        void RestartPreparation()
        {
            if(_geometryPreparation!=null){StopCoroutine(_geometryPreparation);_geometryPreparation=null;}
            if(_preparation!=null){StopCoroutine(_preparation);_preparation=null;}
            for(int k=_active.Count-1;k>=0;k--){int i=_active[k];if(!_window.Contains(_data.ChunkForNode(i))||Reveal(i)!=RoadmapReveal.Revealed||Time.unscaledTime<_lowMemoryUntil&&!InView(Centre(i).y,_data.Nodes[i].world.extent)){_scenePool.Enqueue(_scenes[i]);_scenes[i]=null;_active.RemoveAt(k);}}
            if(_window.Count>0&&isActiveAndEnabled)_preparation=StartCoroutine(PrepareWindow());
        }
        System.Collections.IEnumerator PrepareWindow()
        {
            // One static-data activation per frame, separate from scrolling/layout callbacks.
            yield return null;
            int closest=_data.NearestNode(rectTransform.rect.yMax-_visible.center.y);
            // Prepare the glades nearest the viewport before offscreen neighbors. Distant jumps
            // never wait for a region's first nine invisible nodes to appear before the target.
            for(int offset=0;offset<61;offset++)
            {
                int i=closest+(offset%2==1?(offset+1)/2:-offset/2);
                if(i<0||i>=_scenes.Length||!_window.Contains(_data.ChunkForNode(i))||_scenes[i]!=null||Reveal(i)!=RoadmapReveal.Revealed||Time.unscaledTime<_lowMemoryUntil&&!InView(Centre(i).y,_data.Nodes[i].world.extent))continue;
                var node=_data.Nodes[i];var summary=_summary(node.levelId);
                if(summary==null){Debug.LogError("Missing roadmap summary: "+node.levelId);continue;}
                var lease=_scenePool.Dequeue();
                var timing=Work.Measure(RoadmapWorkMetrics.Activation);
                UnityEngine.Profiling.Profiler.BeginSample("Roadmap.ActivateBaked");
                try
                {
                    var region=_data.Definition.regions[_data.RegionForNode(i)];
                    _scenes[i]=RoadmapSceneGenerator.FromBaked(summary,node.world,_catalog,node==region.nodePositions[0]?region.storyProps:null,lease,Environment,_data.Y(i),ProjectionScale*(IsWorldDiorama?Mathf.Sin(55*Mathf.Deg2Rad):.37f),!IsWorldDiorama,_services.Progression.IsCompleted(node.levelId),!HasBakedComposition);_active.Add(i);
                }
                catch{_scenePool.Enqueue(lease);throw;}
                finally{UnityEngine.Profiling.Profiler.EndSample();timing.Dispose();}
                yield return null;
            }
            _preparation=null;SetVerticesDirty();_weather.SetVerticesDirty();
        }
        void UpdateMaskedMaterial(MaskableGraphic graphic)
        { var masked=graphic.materialForRendering;if(masked!=null&&masked!=_ownedMaterial){masked.SetFloat("_RoadmapTime",Time.unscaledTime);masked.SetFloat("_Motion",Reduced?0:1);} }
        void UpdatePool()
        {
            // Retain scene identities. An entering glade never rebinds all surviving slots.
            foreach(var g in _pool)if(g.gameObject.activeSelf&&(!_active.Contains(g.SceneIndex)||!InView(Centre(g.SceneIndex).y,SceneExtent(g.SceneIndex))))g.gameObject.SetActive(false);
            int visible=0;
            foreach(int i in _active)
            {
                if(!InView(Centre(i).y,SceneExtent(i)))continue;visible++;RoadmapGladeGraphic slot=null;
                foreach(var g in _pool)if(g.gameObject.activeSelf&&g.SceneIndex==i){slot=g;break;}
                if(slot!=null)continue;
                foreach(var g in _pool)if(!g.gameObject.activeSelf){slot=g;break;}
                if(slot==null){Debug.LogError("Roadmap viewport exceeds the bounded glade pool");break;}
                slot.Configure(this,i);slot.gameObject.SetActive(true);
            }
            VisibleGlades=Mathf.Min(visible,_pool.Count);
        }
        System.Collections.IEnumerator PrepareGeometry()
        {
            yield return null;
            const double sliceSeconds=.00125;
            var library=RoadmapModelLibrary.Load();
            while(true)
            {
                RoadmapGladeGraphic slot=null;float nearest=float.PositiveInfinity;
                foreach(var candidate in _pool)
                {
                    if(!candidate.gameObject.activeSelf||!candidate.NeedsGeometry)continue;
                    float distance=Mathf.Abs(Centre(candidate.SceneIndex).y-_visible.center.y);
                    if(distance<nearest){nearest=distance;slot=candidate;}
                }
                if(slot==null)break;
                int index=slot.SceneIndex,version=slot.Version;var scene=_scenes[index];var centre=Centre(index);float scale=ProjectionScale;
                bool Valid()=>slot.gameObject.activeSelf&&slot.SceneIndex==index&&slot.Version==version&&_scenes[index]==scene;
                _geometryBuffer.Clear();_painter.TruncatedModels=0;int models=0,shadows=0;
                using(var work=Work.Measure(RoadmapWorkMetrics.Glade))RoadmapPainter.Glade(_geometryBuffer,scene,centre,scale);
                yield return null;
                if(!Valid())continue;
                for(int pass=0;pass<2&&Valid();pass++)
                {
                    int propIndex=0,offset=0;
                    while(propIndex<scene.Props.Count&&Valid())
                    {
                        long started=System.Diagnostics.Stopwatch.GetTimestamp();
                        using(var work=Work.Measure(RoadmapWorkMetrics.Glade))
                        {
                            do
                            {
                                var prop=scene.Props[propIndex];var model=prop.Geometry??library.Get(prop.Asset);
                                int length=model?.Positions.Length??0;
                                bool bare=prop.Geometry!=null&&prop.Geometry.id.EndsWith(":bare");
                                int end=pass==0&&!bare?length:Mathf.Min(length,offset+192);
                                if(pass==0)_painter.Shadow(_geometryBuffer,scene,prop,centre,scale,offset,end);
                                else _painter.Model(_geometryBuffer,scene,prop,centre,scale,offset,end);
                                offset=end;
                                if(offset>=length)
                                {
                                    if(pass==0){if(prop.Height>=.45f)shadows++;}
                                    else
                                    {
                                        models++;
                                        if(prop.Tent&&(scene.Night||scene.Weather.Rain>.2f))Glow(_geometryBuffer,centre+RoadmapPainter.Project(prop.Position,scale),14,.3f);
                                        if(prop.Fire&&scene.Weather.Rain<.2f)Flame(_geometryBuffer,centre+RoadmapPainter.Project(prop.Position,scale),scene.Night);
                                    }
                                    propIndex++;offset=0;
                                }
                            }while(propIndex<scene.Props.Count&&(System.Diagnostics.Stopwatch.GetTimestamp()-started)/(double)System.Diagnostics.Stopwatch.Frequency<sliceSeconds);
                        }
                        yield return null;
                    }
                }
                if(Valid())
                {
                    using(var work=Work.Measure(RoadmapWorkMetrics.Glade))slot.Commit(_geometryBuffer,models,shadows,_painter.TruncatedModels);
                    yield return null;
                }
            }
            _geometryPreparation=null;
        }
        internal void PaintGlade(VertexHelper vh,int index,RoadmapGladeGraphic output)
        {
            using var timing=Work.Measure(RoadmapWorkMetrics.Glade);
            vh.Clear();if(index<0||index>=_scenes.Length||_scenes[index]==null)return;
            UnityEngine.Profiling.Profiler.BeginSample("Roadmap.Mesh.Glade");
            try
            {
                var scene=_scenes[index];var centre=Centre(index);_painter.TruncatedModels=0;int models=0,shadows=0;
                RoadmapPainter.Glade(vh,scene,centre,ProjectionScale);
                foreach(var prop in scene.Props){_painter.Shadow(vh,scene,prop,centre,ProjectionScale);if(prop.Height>=.45f)shadows++;}
                foreach(var prop in scene.Props)
                {_painter.Model(vh,scene,prop,centre,ProjectionScale);models++;if(prop.Tent&&(scene.Night||scene.Weather.Rain>.2f))Glow(vh,centre+RoadmapPainter.Project(prop.Position,ProjectionScale),14,.3f);if(prop.Fire&&scene.Weather.Rain<.2f)Flame(vh,centre+RoadmapPainter.Project(prop.Position,ProjectionScale),scene.Night);}
                output.Models=models;output.Shadows=shadows;output.Truncated=_painter.TruncatedModels;
            }
            finally{UnityEngine.Profiling.Profiler.EndSample();}
        }
        Rect FindVisibleArea()
        {
            var r=rectTransform.rect;if(_scroll==null||_scroll.viewport==null)return new Rect();
            _scroll.viewport.GetWorldCorners(_corners);var a=rectTransform.InverseTransformPoint(_corners[0]);var b=rectTransform.InverseTransformPoint(_corners[2]);
            return Rect.MinMaxRect(r.xMin,Mathf.Max(r.yMin,Mathf.Floor((Mathf.Min(a.y,b.y)-16)/64)*64),r.xMax,Mathf.Min(r.yMax,Mathf.Ceil((Mathf.Max(a.y,b.y)+16)/64)*64));
        }
        public Vector2 Position(float x,float distance)=>new Vector2(HasCulturalLandscape?rectTransform.rect.center.x+((x-.5f)*QuietCamp.Application.RoadmapRuralLayout.PathWidth-ZoomOffsetX)*ProjectionScale:rectTransform.rect.xMin+x*rectTransform.rect.width,HasBakedComposition&&_visible.height>0?_visible.center.y+(rectTransform.rect.yMax-_visible.center.y-distance)*Zoom:rectTransform.rect.yMax-distance);
        public Vector2 Centre(int i)=>Position(_data.Nodes[i].x,_data.Y(i));
        public Vector2 ControlCentre(int i)=>IsWorldDiorama?Centre(i)+new Vector2(0,-65):Centre(i);
        public float SceneExtent(int i)=>_scenes[i]!=null?Mathf.Max(230,_scenes[i].VerticalExtent*ProjectionScale+12):_data.Nodes[i].world.extent;
        public bool InView(float y,float extent=0)=>y+extent>=_visible.yMin&&y-extent<=_visible.yMax;
        Vector2 Node(float index)
        {int i=Mathf.Clamp(Mathf.FloorToInt(index),0,_data.Nodes.Length-1);var a=Centre(i);var b=Centre(Mathf.Min(i+1,_data.Nodes.Length-1));float t=index-i;return new Vector2(Mathf.Lerp(a.x,b.x,Mathf.SmoothStep(0,1,t)),Mathf.Lerp(a.y,b.y,t));}
        Color Terrain(float y)
        {
            var visual=VisualAt(rectTransform.rect.yMax-y);return visual.Lit(Color.Lerp(visual.Palette.GrassDark,visual.Palette.GrassLight,.65f),Vector3.up);
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            using var timing=Work.Measure(RoadmapWorkMetrics.Background);
            vh.Clear();if(!_initialized||_visible.height<=0||IsWorldDiorama)return;
            UnityEngine.Profiling.Profiler.BeginSample("Roadmap.Mesh.Background");
            try
            {
                var r=rectTransform.rect;
                for(float y=_visible.yMax;y>_visible.yMin;y-=72)
                {float bottom=Mathf.Max(_visible.yMin,y-72);var a=Terrain(y);var b=Terrain(bottom);RoadmapPainter.Quad(vh,new Vector2(r.xMin,y),new Vector2(r.xMax,y),new Vector2(r.xMax,bottom),new Vector2(r.xMin,bottom),a,a,b,b);}
                // An inexpensive lit veil at the periphery, stretched to the measured viewport.
                // It never requires extra cameras or offscreen content and follows the local season.
                float edge=Mathf.Min(r.width*.16f,145);
                for(float y=_visible.yMax;y>_visible.yMin;y-=144)
                {
                    float bottom=Mathf.Max(_visible.yMin,y-144);var haze=Color.Lerp(Terrain(y),new Color(.70f,.76f,.68f),.23f);haze.a=.25f;
                    RoadmapPainter.Quad(vh,new Vector2(r.xMin,y),new Vector2(r.xMin+edge,y),new Vector2(r.xMin+edge,bottom),new Vector2(r.xMin,bottom),haze,RoadmapPainter.Clear(haze),RoadmapPainter.Clear(haze),haze);
                    RoadmapPainter.Quad(vh,new Vector2(r.xMax-edge,y),new Vector2(r.xMax,y),new Vector2(r.xMax,bottom),new Vector2(r.xMax-edge,bottom),RoadmapPainter.Clear(haze),haze,haze,RoadmapPainter.Clear(haze));
                }
                int first=Mathf.Max(0,_data.NearestNode(r.yMax-_visible.yMax)-2),last=Mathf.Min(_data.Nodes.Length-1,_data.NearestNode(r.yMax-_visible.yMin)+2);
                for(int k=first;k<last;k++)for(int part=0;part<18;part++)
                {
                    var a=Node(k+part/18f);var b=Node(k+(part+1)/18f);if(!InView(a.y,35)&&!InView(b.y,35))continue;
                    var soil=Color.Lerp(Terrain(a.y),new Color(.69f,.59f,.40f),.48f);soil.a=Reveal(k)==RoadmapReveal.Hidden?.10f:.66f;
                    SoftTrail(vh,a,b,27,soil);
                }
                for(int i=first;i<=last;i++)if(Reveal(i)!=RoadmapReveal.Revealed&&InView(Centre(i).y,230))
                {var tint=Terrain(Centre(i).y);tint.a=Reveal(i)==RoadmapReveal.Hidden?.25f:.65f;RoadmapPainter.Ellipse(vh,Centre(i),new Vector2(150,72),tint,RoadmapPainter.Clear(tint),18);}
                if(_window.Count>0)for(int reg=_window.First;reg<=_window.Last;reg++)
                {
                    var region=_data.Definition.regions[reg];
                    foreach(var branch in region.branches)
                    {
                        int anchor=_data.NodeIndex(branch.anchorNodeId);if(anchor<0||!BranchVisible(branch))continue;
                        var at=new Vector2(r.xMin+branch.x*r.width,r.yMax-_data.RegionStarts[reg]-branch.y);if(!InView(at.y,210))continue;
                        var start=Centre(anchor);var soil=Terrain(at.y);soil.a=.55f;
                        for(int part=0;part<12;part++)SoftTrail(vh,Vector2.Lerp(start,at,part/12f),Vector2.Lerp(start,at,(part+1)/12f),22,soil);
                        RoadmapPainter.Ellipse(vh,at,new Vector2(175,92),soil,RoadmapPainter.Clear(soil),24);
                    }
                }
            }
            finally{_backgroundVertices=vh.currentVertCount;UnityEngine.Profiling.Profiler.EndSample();}
        }
        static void SoftTrail(VertexHelper vh,Vector2 a,Vector2 b,float width,Color soil)
        {
            var d=(b-a).normalized;var n=new Vector2(-d.y,d.x);float core=width*.30f,outer=width*.65f;
            RoadmapPainter.Ribbon(vh,a,b,core*2,soil);
            var clear=RoadmapPainter.Clear(soil);
            RoadmapPainter.Quad(vh,a+n*core,b+n*core,b+n*outer,a+n*outer,soil,soil,clear,clear);
            RoadmapPainter.Quad(vh,a-n*outer,b-n*outer,b-n*core,a-n*core,clear,clear,soil,soil);
        }
        /// <summary>Drop offscreen leases for ten seconds, retaining chunks that still contribute visible geometry.</summary>
        public void TrimForLowMemory()
        {
            if(!_initialized)return;_lowMemoryUntil=Time.unscaledTime+10;
            int region=_data.ChunkAt(rectTransform.rect.yMax-_visible.center.y);
            _wasLowMemory=true;_window.Move(region,_data.Chunks.Length,ViewportFitsRegion(region)?0:1);RestartPreparation();if(!IsWorldDiorama)UpdatePool();
            foreach(var g in _pool)if(!g.gameObject.activeSelf){g.ClearCache();g.InvalidateGeometry();}
        }
        bool ViewportFitsRegion(int region)
        {
            // Never shed an adjacent chunk that contributes visible geometry or long tree shadows.
            float top=rectTransform.rect.yMax-_visible.yMax-600,bottom=rectTransform.rect.yMax-_visible.yMin+600;
            return _data.ChunkAt(top)==region&&_data.ChunkAt(bottom)==region;
        }
        protected override void OnDisable(){UnityEngine.Application.lowMemory-=TrimForLowMemory;base.OnDisable();if(_preparation!=null){StopCoroutine(_preparation);_preparation=null;}if(_geometryPreparation!=null){StopCoroutine(_geometryPreparation);_geometryPreparation=null;}}
        protected override void OnEnable(){base.OnEnable();UnityEngine.Application.lowMemory+=TrimForLowMemory;if(_initialized)RestartPreparation();}
        internal static void Glow(VertexHelper vh,Vector2 p,float radius,float alpha)
        {
            var color=new Color(1,.74f,.32f,alpha);
            RoadmapPainter.Ellipse(vh,p,new Vector2(radius,radius*.7f),color,RoadmapPainter.Clear(color));
        }
        static void Flame(VertexHelper vh,Vector2 p,bool night)
        {
            Glow(vh,p,night?29:18,night?.30f:.12f);
            RoadmapPainter.Triangle(vh,p+Vector2.left*4,p+Vector2.up*15,p+Vector2.right*5,new Color(.94f,.46f,.13f));
            RoadmapPainter.Triangle(vh,p+Vector2.left*2,p+Vector2.up*9,p+Vector2.right*3,new Color(1,.82f,.32f));
        }
        static readonly Dictionary<string,RoadmapSceneGenerator.Scene> BonusScenes=new Dictionary<string,RoadmapSceneGenerator.Scene>();
        internal static int BonusClearing(VertexHelper vh,Vector2 at,BonusCampDefinition slot)
        {
            if(!BonusScenes.TryGetValue(slot.id,out var scene))
            {
                var level=new LevelSummary { id=slot.id,number=slot.afterLevel,width=4,height=4,decorSeed=slot.afterLevel*7919,
                    environmentPreset=slot.theme=="fireflies"?"pines":"meadow",lighting=slot.theme=="fireflies"?"night":slot.theme=="amber"?"evening":"morning" };
                scene=RoadmapSceneGenerator.Generate(level,AtmosphereCatalog.Load());
                scene.Weather=new CampWeatherTimeline.State(0,0);BonusScenes.Add(slot.id,scene);
            }
            const float scale=22;var painter=new RoadmapPainter(RoadmapModelLibrary.Load());
            var moss=scene.Ground;moss.a=.9f;RoadmapPainter.Ellipse(vh,at,new Vector2(178,94),moss,RoadmapPainter.Clear(moss),24);
            var water=scene.Tint(new Color(.39f,.62f,.63f),Vector3.up);
            RoadmapPainter.Ellipse(vh,at+new Vector2(30,12),new Vector2(63,30),water*1.16f,water*.84f,24);
            foreach(var prop in scene.Props)painter.Shadow(vh,scene,prop,at,scale);
            foreach(var prop in scene.Props)
            { painter.Model(vh,scene,prop,at,scale);if(prop.Tent)Glow(vh,at+RoadmapPainter.Project(prop.Position,scale),19,.34f); }
            if(scene.Night)for(int i=0;i<8;i++)Glow(vh,at+new Vector2(Mathf.Sin(i*2.4f)*135,Mathf.Cos(i*1.5f)*65),5,.60f);
            return painter.TruncatedModels;
        }
        protected override void OnDestroy()
        {if(_scroll!=null)_scroll.onValueChanged.RemoveListener(ClampScroll); base.OnDestroy();_geometryBuffer.Dispose();if(_ownedMaterial!=null)Destroy(_ownedMaterial); }
    }
}
