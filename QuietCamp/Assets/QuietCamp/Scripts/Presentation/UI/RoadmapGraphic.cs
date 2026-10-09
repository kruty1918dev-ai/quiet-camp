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
        readonly List<RoadmapSceneGenerator.Scene> _scenes=new List<RoadmapSceneGenerator.Scene>();
        LevelSummary[] _summaries;
        AtmosphereCatalog _catalog;
        readonly Vector3[] _corners=new Vector3[4];
        readonly List<RoadmapGladeGraphic> _pool=new List<RoadmapGladeGraphic>();
        int _drawingModels,_drawingShadows,_bonusTruncated,_lastTier=-1;
        RoadmapPainter _painter;
        RoadmapWeatherGraphic _weather;
        Material _ownedMaterial;
        ScrollRect _scroll;
        GameServices _services;
        Rect _visible;
        float _lastRefresh;
        bool _manualWeather;
        public IReadOnlyList<RoadmapSceneGenerator.Scene> Scenes=>_scenes;
        public Rect VisibleArea=>_visible;
        public int VisibleGlades { get; private set; }
        public IReadOnlyList<RoadmapGladeGraphic> GladePool=>_pool;
        public int PaintedModels { get { int n=0;foreach(var glade in _pool)if(glade.gameObject.activeSelf)n+=glade.Models;return n; } }
        public int ShadowCasters { get { int n=0;foreach(var glade in _pool)if(glade.gameObject.activeSelf)n+=glade.Shadows;return n; } }
        public int TruncatedModels { get { int n=_bonusTruncated;foreach(var glade in _pool)if(glade.gameObject.activeSelf)n+=glade.Truncated;return n; } }
        public int VisibleVertices { get { int n=canvasRenderer.GetMesh()?.vertexCount??0;foreach(var glade in _pool)if(glade.gameObject.activeSelf)n+=glade.canvasRenderer.GetMesh()?.vertexCount??0;return n; } }
        public int QualityTier=>_services?.EffectiveQuality??1;
        public int WeatherRevision { get; private set; }
        public bool Reduced=>_services?.ReducedMotion??false;
        public float ProjectionScale=>28*Mathf.Clamp(rectTransform.rect.width/850,1,1.28f);
        public void Configure(GameServices services)
        {
            _services=services;EnsureData();
            if(_weather==null)
            {
                var child=QcUi.Stretch(rectTransform,"RoadmapWeather");
                _weather=child.gameObject.AddComponent<RoadmapWeatherGraphic>();_weather.Configure(this);
            }
            SetVerticesDirty();
        }
        void EnsureData()
        {
            using var audit = PerformanceAudit.Measure("QC.RoadmapGraphic.EnsureData");
            if(_painter!=null)return;
            _catalog=AtmosphereCatalog.Load();_painter=new RoadmapPainter(RoadmapModelLibrary.Load());
            // Metadata may include extra frozen/QA levels or a different order
            // on a fresh checkout. Match the same campaign IDs as the buttons.
            // Dioramas are still materialized only when their area is painted.
            var ids=LevelLoader.MvpLevelIds();
            _summaries=new LevelSummary[ids.Count];
            for(int i=0;i<_summaries.Length;i++)
                _summaries[i]=CampContent.Summary(ids[i])??new LevelSummary
                    {id=ids[i],number=i+1,width=8,height=8,decorSeed=11000+i};
            for(int i=0;i<_summaries.Length;i++)_scenes.Add(null);
            _scroll=GetComponentInParent<ScrollRect>();
            var shader=Resources.Load<Shader>("QuietCamp/RoadmapCanopy");
            if(shader!=null)
            {
                _ownedMaterial=new Material(shader){name="Roadmap living canopy"};material=_ownedMaterial;
                canvas.additionalShaderChannels|=AdditionalCanvasShaderChannels.TexCoord1|AdditionalCanvasShaderChannels.TexCoord2;
            }
        }
        /// <summary>Materialize the scene for index on first use; callers only
        /// reach scenes whose area is on screen, so generation stays bounded.</summary>
        public RoadmapSceneGenerator.Scene SceneAt(int i)
        {
            var scene=_scenes[i];
            if(scene==null)
            {
                scene=RoadmapSceneGenerator.Generate(_summaries[i],_catalog);
                if(_manualWeather)scene.SetMoment(_weatherMoment);
                _scenes[i]=scene;
            }
            return scene;
        }
        float _weatherMoment;
        public void SetWeatherMoment(float seconds)
        {
            WeatherRevision++;
            EnsureData();_manualWeather=seconds>=0;_weatherMoment=seconds;
            foreach(var scene in _scenes)
                scene?.SetMoment(seconds);
            SetVerticesDirty();_weather?.SetVerticesDirty();foreach(var glade in _pool)glade.SetVerticesDirty();
        }
        void LateUpdate()
        {
            using var audit = PerformanceAudit.Measure("QC.RoadmapGraphic.LateUpdate");
            if(_painter==null)return;
            if(!_manualWeather&&!Reduced)for(int i=0;i<_scenes.Count;i++)
                if(_scenes[i]!=null&&InView(Centre(i).y,Extent(i)))_scenes[i].Advance(Time.unscaledDeltaTime);
            var visible=FindVisibleArea();
            bool refresh=Time.unscaledTime-_lastRefresh>3||_lastTier!=QualityTier;
            if(visible!=_visible||refresh)
            { _visible=visible;SetVerticesDirty();_weather?.SetVerticesDirty(); }
            if(refresh) { _lastRefresh=Time.unscaledTime;_lastTier=QualityTier; }
            UpdatePool();
            if (!_manualWeather && !Reduced && _pool.Count > 0)
            {
                int slot = Time.frameCount % _pool.Count;
                var glade = _pool[slot];
                if (glade.gameObject.activeSelf && Time.unscaledTime >= _nextGladeRefresh)
                { glade.InvalidateWeather(); glade.SetVerticesDirty(); _nextGladeRefresh = Time.unscaledTime + 3f / _pool.Count; }
            }
            if(_ownedMaterial!=null)
            {
                _ownedMaterial.SetFloat("_RoadmapTime",Time.unscaledTime);_ownedMaterial.SetFloat("_Motion",Reduced?0:1);
                UpdateMaskedMaterial(this);
                foreach(var glade in _pool)if(glade.gameObject.activeSelf)UpdateMaskedMaterial(glade);
            }
        }
        void UpdateMaskedMaterial(MaskableGraphic graphic)
        {
            var masked=graphic.materialForRendering;
            if(masked!=null&&masked!=_ownedMaterial)
            { masked.SetFloat("_RoadmapTime",Time.unscaledTime);masked.SetFloat("_Motion",Reduced?0:1); }
        }
        float _nextGladeRefresh;
        readonly List<int> _visibleIndices = new List<int>(12);
        void UpdatePool()
        {
            using var audit = PerformanceAudit.Measure("QC.RoadmapGraphic.UpdatePool");
            _visibleIndices.Clear();
            for (int i = 0; i < _scenes.Count; i++)
                if (InView(Centre(i).y, Extent(i))) _visibleIndices.Add(i);
            // Preserve slot ownership while the scene stays visible. A newly
            // entering glade must not rebind/rebuild all the other visible ones.
            foreach (var glade in _pool)
                if (!_visibleIndices.Contains(glade.SceneIndex)) glade.gameObject.SetActive(false);
            foreach (int index in _visibleIndices)
            {
                RoadmapGladeGraphic selected = null;
                foreach (var glade in _pool)
                    if (glade.SceneIndex == index) { selected = glade; break; }
                if (selected == null)
                    foreach (var glade in _pool)
                        if (!glade.gameObject.activeSelf) { selected = glade; break; }
                if (selected == null)
                {
                    var rect = QcUi.Stretch(rectTransform, "RoadmapGladeSlot" + _pool.Count);
                    selected = rect.gameObject.AddComponent<RoadmapGladeGraphic>();
                    selected.raycastTarget = false; selected.material = material; _pool.Add(selected);
                }
                selected.Configure(this, index); selected.gameObject.SetActive(true);
            }
            VisibleGlades = _visibleIndices.Count; _weather?.transform.SetAsLastSibling();
        }
        internal void PaintGlade(VertexHelper vh,int index,RoadmapGladeGraphic output)
        {
            using var audit = PerformanceAudit.Measure("QC.RoadmapGraphic.PaintGlade");
            vh.Clear();_drawingModels=_drawingShadows=0;_painter.TruncatedModels=0;
            var scene=SceneAt(index);var centre=Centre(index);
            DrawSurroundings(vh,scene,centre,rectTransform.rect);
            RoadmapPainter.Glade(vh,scene,centre,ProjectionScale);
            foreach(var prop in scene.Props)
            { _painter.Shadow(vh,scene,prop,centre,ProjectionScale);if(prop.Height>=.45f)_drawingShadows++; }
            foreach(var prop in scene.Props)
            {
                _painter.Model(vh,scene,prop,centre,ProjectionScale);_drawingModels++;
                if(prop.Tent&&(scene.Night||scene.Weather.Rain>.2f))Glow(vh,centre+RoadmapPainter.Project(prop.Position,ProjectionScale),14,.30f);
                if(prop.Fire&&scene.Weather.Rain<.2f)Flame(vh,centre+RoadmapPainter.Project(prop.Position,ProjectionScale),scene.Night);
            }
            output.Models=_drawingModels;output.Shadows=_drawingShadows;output.Truncated=_painter.TruncatedModels;
        }
        Rect FindVisibleArea()
        {
            var r=rectTransform.rect;
            if(_scroll==null||_scroll.viewport==null)return r;
            _scroll.viewport.GetWorldCorners(_corners);var a=rectTransform.InverseTransformPoint(_corners[0]);var b=rectTransform.InverseTransformPoint(_corners[2]);
            return Rect.MinMaxRect(r.xMin,Mathf.Max(r.yMin,Mathf.Floor((a.y-16)/64)*64),r.xMax,Mathf.Min(r.yMax,Mathf.Ceil((b.y+16)/64)*64));
        }
        public Vector2 Centre(int i)=>Node(i,rectTransform.rect);
        /// <summary>Vertical extent for culling; unmaterialized scenes use the
        /// conservative estimate so culling never needs generation.</summary>
        public float Extent(int i)=>Mathf.Max(230,(_scenes[i]?.VerticalExtent??3f)*ProjectionScale+12);
        public float SceneExtent(int i)=>Mathf.Max(230,SceneAt(i).VerticalExtent*ProjectionScale+12);
        static Vector2 Node(float index,Rect r)
        {
            int lower=Mathf.FloorToInt(index);
            float y=Mathf.Lerp(RoadmapLayout.MainY(lower),RoadmapLayout.MainY(lower+1),index-lower);
            return new Vector2(r.xMin+NodeX(index)*r.width,r.yMax-y);
        }
        public bool InView(float y,float extent=0)=>y+extent>=_visible.yMin&&y-extent<=_visible.yMax;
        Color Terrain(float y)
        {
            var r=rectTransform.rect;float distance=r.yMax-y;
            int a=0;while(a+1<_scenes.Count&&RoadmapLayout.MainY(a+1)<distance)a++;
            int b=Mathf.Min(a+1,_scenes.Count-1);
            float t=Mathf.InverseLerp(RoadmapLayout.MainY(a),RoadmapLayout.MainY(b),distance);
            return Color.Lerp(SceneAt(a).Ground*.91f,SceneAt(b).Ground*.91f,Mathf.SmoothStep(0,1,t));
        }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            using var audit = PerformanceAudit.Measure("QC.RoadmapGraphic.OnPopulateMesh");
            vh.Clear();if(rectTransform.rect.width<=0)return;EnsureData();if(_scenes.Count==0)return;
            _visible=FindVisibleArea();var r=rectTransform.rect;
            _bonusTruncated=0;
            for(float y=_visible.yMax;y>_visible.yMin;y-=72)
            {
                float bottom=Mathf.Max(_visible.yMin,y-72);var a=Terrain(y);var b=Terrain(bottom);
                RoadmapPainter.Quad(vh,new Vector2(r.xMin,y),new Vector2(r.center.x,y),new Vector2(r.center.x,bottom),new Vector2(r.xMin,bottom),a*.88f,a,b,b*.88f);
                RoadmapPainter.Quad(vh,new Vector2(r.center.x,y),new Vector2(r.xMax,y),new Vector2(r.xMax,bottom),new Vector2(r.center.x,bottom),a,a*.88f,b*.88f,b);
            }
            // Trampled soil with mossy verges; muted by the local weather/light.
            int first = 0, last = _scenes.Count - 1;
            while (first < last && Centre(first + 1).y > _visible.yMax + 35) first++;
            while (last > first && Centre(last - 1).y < _visible.yMin - 35) last--;
            for(int i=first*18;i<last*18;i++)
            {
                var a=Node(i/18f,r);var b=Node((i+1)/18f,r);if(!InView(a.y,35)&&!InView(b.y,35))continue;
                var moss=Terrain(a.y)*.78f;moss.a=.22f;
                var soil=Color.Lerp(Terrain(a.y),new Color(.53f,.49f,.32f),.20f);soil.a=.52f;
                RoadmapPainter.Ribbon(vh,a,b,44,moss);RoadmapPainter.Ribbon(vh,a,b,28,soil);
                soil.a=.44f;RoadmapPainter.Ribbon(vh,a,b,16+Mathf.Sin(i*.7f)*3,soil);
            }
            foreach(var slot in BonusCampCatalog.Slots)
            {
                if(slot.afterLevel>_scenes.Count)continue;
                var at=new Vector2(r.xMin+RoadmapLayout.BonusX(slot)*r.width,r.yMax-RoadmapLayout.BonusY(slot));
                if(!InView(at.y,250))continue;
                var start=Centre(slot.afterLevel-1)+Vector2.down*125;
                for(int i=0;i<14;i++)
                {
                    Vector2 Point(float t)=>Vector2.Lerp(start,at,t)+Vector2.up*Mathf.Sin(t*Mathf.PI)*45;
                    var soil=Terrain(at.y)*1.1f;soil.a=.52f;
                    RoadmapPainter.Ribbon(vh,Point(i/14f),Point((i+1)/14f),23,soil);
                }
                _bonusTruncated+=BonusClearing(vh,at,slot);
            }
        }
        void DrawSurroundings(VertexHelper vh,RoadmapSceneGenerator.Scene scene,Vector2 centre,Rect rect)
        {
            var rng=new System.Random(scene.Level.decorSeed^1918);
            int pockets=Mathf.Clamp(Mathf.CeilToInt(rect.width/160),5,QualityTier==0?8:12);
            for(int i=0;i<pockets;i++)
            {
                float x=Mathf.Lerp(rect.xMin+30,rect.xMax-30,(i+.45f)/pockets);
                float y=centre.y+((float)rng.NextDouble()-.5f)*Step;
                var origin=new Vector2(x,y);
                if(Mathf.Abs(x-centre.x)<250)continue;
                var moss=scene.Ground*.84f;moss.a=.22f;
                RoadmapPainter.Ellipse(vh,origin,new Vector2(120,60),moss,RoadmapPainter.Clear(moss),18);
                for(int tree=0;tree<1;tree++)
                {
                    bool pine=scene.Winter||scene.Level.environmentPreset=="pines"||rng.Next(4)==0;
                    var prop=new RoadmapSceneGenerator.Prop { Asset=pine?"tree_pineRoundA":"tree_default",
                        Position=new Vector3(tree*.9f,0,tree*-.65f),Height=1.9f+(float)rng.NextDouble()*1.1f,Yaw=rng.Next(360),Sway=true };
                    if(vh.currentVertCount>46000)return;
                    _painter.Shadow(vh,scene,prop,origin,ProjectionScale);_painter.Model(vh,scene,prop,origin,ProjectionScale);_drawingModels++;_drawingShadows++;
                }
                var bush=new RoadmapSceneGenerator.Prop { Asset="plant_bushSmall",Position=Vector3.forward,Height=.65f,Yaw=rng.Next(360),Sway=true };
                _painter.Model(vh,scene,bush,origin,ProjectionScale);_drawingModels++;
                bush=new RoadmapSceneGenerator.Prop { Asset=i%4==0?"flower_yellowA":"plant_bushSmall",Position=new Vector3(-.8f,0,-.3f),Height=.28f,Yaw=rng.Next(360),Sway=true };
                _painter.Model(vh,scene,bush,origin,ProjectionScale);_drawingModels++;
            }
        }
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
        sealed class BonusGeometry { public UIVertex[] Vertices; public int[] Indices; public int Truncated; }
        static readonly Dictionary<string,BonusGeometry> BonusMeshes = new Dictionary<string,BonusGeometry>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetBonusCaches() { BonusScenes.Clear(); BonusMeshes.Clear(); }
        internal static int BonusClearing(VertexHelper vh,Vector2 at,BonusCampDefinition slot)
        {
            using var audit = PerformanceAudit.Measure("QC.RoadmapGraphic.BonusClearing");
            string key = slot.id + ":" + slot.afterLevel + ":" + slot.theme;
            if (!BonusMeshes.TryGetValue(key, out var geometry))
            {
                using var source = new VertexHelper();
                int truncated = PaintBonusClearing(source, Vector2.zero, slot);
                var vertices = new UIVertex[source.currentVertCount];
                for (int i=0;i<vertices.Length;i++) source.PopulateUIVertex(ref vertices[i],i);
                var mesh = new Mesh();
                source.FillMesh(mesh);
                var indices = mesh.triangles;
                if (UnityEngine.Application.isPlaying) Destroy(mesh); else DestroyImmediate(mesh);
                geometry = new BonusGeometry { Vertices=vertices, Indices=indices, Truncated=truncated }; BonusMeshes.Add(key,geometry);
            }
            if (vh.currentVertCount + geometry.Vertices.Length >= 65000) return geometry.Truncated + 1;
            int start = vh.currentVertCount;
            foreach (var source in geometry.Vertices)
            {
                var vertex = source; vertex.position += new Vector3(at.x,at.y,0);
                vertex.uv1 += new Vector4(at.x,at.y,0,0); vh.AddVert(vertex);
            }
            for (int i=0;i<geometry.Indices.Length;i+=3)
                vh.AddTriangle(start+geometry.Indices[i],start+geometry.Indices[i+1],start+geometry.Indices[i+2]);
            return geometry.Truncated;
        }
        internal static int PaintBonusClearing(VertexHelper vh,Vector2 at,BonusCampDefinition slot)
        {
            string key = slot.id + ":" + slot.afterLevel + ":" + slot.theme;
            if(!BonusScenes.TryGetValue(key,out var scene))
            {
                var level=new LevelSummary { id=slot.id,number=slot.afterLevel,width=4,height=4,decorSeed=slot.afterLevel*7919,
                    entry=new[]{2,3},environmentPreset=BonusPreset(slot.theme),lighting=BonusLighting(slot.theme),
                    environment=new EnvironmentCompositionData { seasonId=BonusSeason(slot.theme) } };
                scene=RoadmapSceneGenerator.Generate(level,AtmosphereCatalog.Load());
                scene.Weather=new CampWeatherTimeline.State(0,0);BonusScenes.Add(key,scene);
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
        static string BonusPreset(string theme)
        {
            switch(theme)
            {
                case "fireflies":case "pinewood":case "quarry":case "highmoor":case "marshal":case "summit":return "pines";
                default:return "meadow";
            }
        }
        static string BonusLighting(string theme)
        {
            switch(theme)
            {
                case "fireflies":case "lantern":case "lagoon":return "night";
                case "amber":case "creek":case "overlook":case "cliff":case "autumn":return "evening";
                default:return "morning";
            }
        }
        static string BonusSeason(string theme)
        {
            switch(theme)
            {
                case "frost":case "summit":return "winter";
                case "spring":case "birch":case "meadow":return "spring";
                case "summer":case "lagoon":case "creek":return "summer";
                case "autumn":case "amber":case "orchard":return "autumn";
                default:return "";
            }
        }
        protected override void OnDestroy()
        { base.OnDestroy();if(_ownedMaterial!=null)Destroy(_ownedMaterial); }
    }
}
