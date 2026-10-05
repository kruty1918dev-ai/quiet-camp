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
            if(_painter!=null)return;
            var catalog=AtmosphereCatalog.Load();_painter=new RoadmapPainter(RoadmapModelLibrary.Load());
            foreach(var level in CampContent.Summaries)_scenes.Add(RoadmapSceneGenerator.Generate(level,catalog));
            _scroll=GetComponentInParent<ScrollRect>();
            var shader=Resources.Load<Shader>("QuietCamp/RoadmapCanopy");
            if(shader!=null)
            {
                _ownedMaterial=new Material(shader){name="Roadmap living canopy"};material=_ownedMaterial;
                canvas.additionalShaderChannels|=AdditionalCanvasShaderChannels.TexCoord1|AdditionalCanvasShaderChannels.TexCoord2;
            }
        }
        public void SetWeatherMoment(float seconds)
        {
            EnsureData();_manualWeather=seconds>=0;
            foreach(var scene in _scenes)
            {
                scene.SetMoment(seconds);
            }
            SetVerticesDirty();_weather?.SetVerticesDirty();foreach(var glade in _pool)glade.SetVerticesDirty();
        }
        void LateUpdate()
        {
            if(_painter==null)return;
            if(!_manualWeather&&!Reduced)foreach(var scene in _scenes)scene.Advance(Time.unscaledDeltaTime);
            var visible=FindVisibleArea();
            bool refresh=Time.unscaledTime-_lastRefresh>1||_lastTier!=QualityTier;
            if(visible!=_visible||refresh)
            { _visible=visible;SetVerticesDirty();_weather?.SetVerticesDirty(); }
            if(refresh) { _lastRefresh=Time.unscaledTime;_lastTier=QualityTier;foreach(var glade in _pool)glade.SetVerticesDirty(); }
            UpdatePool();
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
        void UpdatePool()
        {
            int slot=0;
            for(int i=0;i<_scenes.Count;i++)
            {
                if(!InView(Centre(i).y,SceneExtent(i)))continue;
                if(slot==_pool.Count)
                {
                    var rect=QcUi.Stretch(rectTransform,"RoadmapGladeSlot"+slot);
                    var graphic=rect.gameObject.AddComponent<RoadmapGladeGraphic>();graphic.raycastTarget=false;
                    graphic.material=material;_pool.Add(graphic);
                }
                var glade=_pool[slot++];glade.gameObject.SetActive(true);glade.Configure(this,i);
            }
            for(int i=slot;i<_pool.Count;i++)_pool[i].gameObject.SetActive(false);
            VisibleGlades=slot;_weather?.transform.SetAsLastSibling();
        }
        internal void PaintGlade(VertexHelper vh,int index,RoadmapGladeGraphic output)
        {
            vh.Clear();_drawingModels=_drawingShadows=0;_painter.TruncatedModels=0;
            var scene=_scenes[index];var centre=Centre(index);
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
        public float SceneExtent(int i)=>Mathf.Max(230,_scenes[i].VerticalExtent*ProjectionScale+12);
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
            return Color.Lerp(_scenes[a].Ground*.91f,_scenes[b].Ground*.91f,Mathf.SmoothStep(0,1,t));
        }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
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
            for(int i=0;i<(_scenes.Count-1)*18;i++)
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
        { base.OnDestroy();if(_ownedMaterial!=null)Destroy(_ownedMaterial); }
    }
}
