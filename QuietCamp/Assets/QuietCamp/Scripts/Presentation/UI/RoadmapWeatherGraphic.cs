using QuietCamp.Infrastructure;
using UnityEngine;
using UnityEngine.UI;

namespace QuietCamp.Presentation.UI
{
    /// <summary>Bounded local weather and atmospheric depth over visible miniatures.
    /// Never paints over native controls and never captures an input pointer.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class RoadmapWeatherGraphic : MaskableGraphic
    {
        RoadmapGraphic _map;
        float _nextUpdate;
        public int RainGlades { get; private set; }
        public int SunlitGlades { get; private set; }
        public int MistGlades { get; private set; }
        public int RainDrops { get; private set; }
        public int SnowGlades {get;private set;}
        public int Snowflakes {get;private set;}
        public void Configure(RoadmapGraphic map) { _map=map;raycastTarget=false;SetVerticesDirty(); }
        void LateUpdate()
        {
            if(_map==null||_map.Reduced)return;
            if(Time.unscaledTime<_nextUpdate)return;
            _nextUpdate=Time.unscaledTime+(_map.QualityTier==0?.18f:.08f);SetVerticesDirty();
        }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            using var audit = PerformanceAudit.Measure("QC.RoadmapWeatherGraphic.OnPopulateMesh");
            vh.Clear();RainGlades=SunlitGlades=MistGlades=RainDrops=SnowGlades=Snowflakes=0;if(_map==null)return;
            float time=_map.Reduced?0:Time.unscaledTime;
            for(int i=0;i<_map.Scenes.Count;i++)
            {
                var centre=_map.Centre(i);
                if(!_map.InView(centre.y,_map.Extent(i)))continue;
                var scene=_map.SceneAt(i);
                if(scene.Light.Mist||scene.Weather.Cloud>.35f||scene.Level.environment?.moisture>.6f)
                {
                    MistGlades++;
                    var mist=Color.Lerp(Color.Lerp(scene.Light.Ambient,scene.Light.Sun,scene.Night?.04f:.22f),scene.Palette.Fog,.30f);
                    mist.a=scene.Night?.065f:Mathf.Lerp(.07f,.14f,scene.Weather.Cloud);
                    for(int layer=0;layer<3;layer++)
                    {
                        float move=Mathf.Sin(time*.10f+i+layer)*8;
                        RoadmapPainter.Ellipse(vh,centre+new Vector2((layer-1)*125+move,45+layer*32),
                            new Vector2(132,48+layer*8),mist,RoadmapPainter.Clear(mist),18);
                    }
                }
                if(!scene.Night&&scene.Weather.Cloud<.50f&&scene.Weather.Rain<.08f&&_map.QualityTier>0)
                {
                    SunlitGlades++;int count=0;
                    foreach(var tree in scene.Props)
                    {
                        if(!tree.Asset.StartsWith("tree"))continue;
                        // Deterministic canopy openings, rather than a screen-wide bloom.
                        if(tree.Position.x>0||tree.Position.z>0)continue;
                        var top=tree.Position+Vector3.up*tree.Height*.87f;
                        var bottom=top-scene.Sun*(top.y/Mathf.Max(.20f,scene.Sun.y));
                        var a=centre+RoadmapPainter.Project(top,_map.ProjectionScale);
                        var b=centre+RoadmapPainter.Project(bottom,_map.ProjectionScale);
                        var tint=scene.Light.Sun;tint.a=(1-scene.Weather.Cloud)*(.065f+.015f*Mathf.Sin(time*.14f+i));
                        Beam(vh,a,b,6,24,tint);
                        var glow=tint;glow.a*=.75f;
                        RoadmapPainter.Ellipse(vh,b,new Vector2(30,14),glow,RoadmapPainter.Clear(glow),18);
                        if(++count>=(_map.QualityTier==2?3:2))break;
                    }
                }
                if(scene.Winter)
                {
                    SnowGlades++;if(_map.Reduced)continue;
                    int count=_map.QualityTier==0?4:_map.QualityTier==1?12:18;
                    for(int flake=0;flake<count;flake++)
                    {
                        Snowflakes++;
                        float phase=Mathf.Repeat(time*.095f+Hash(flake*91+i*31),1);
                        float x=(Hash(scene.Level.decorSeed+flake*37)-.5f)*390+Mathf.Sin(time*.35f+flake)*4;
                        var p=centre+new Vector2(x,Mathf.Lerp(170,-135,phase));
                        var snow=new Color(.91f,.95f,.90f,Mathf.Sin(phase*Mathf.PI)*scene.Snowfall*.82f);
                        float radius=1.4f+Hash(flake*19)*1.1f;
                        RoadmapPainter.Ellipse(vh,p,new Vector2(radius,radius),snow,RoadmapPainter.Clear(snow),6);
                    }
                }
                else if(scene.Weather.Rain>.12f)
                {
                    RainGlades++;
                    if(_map.Reduced)continue;
                    int count=_map.QualityTier==0?6:_map.QualityTier==1?22:30;
                    var rain=Color.Lerp(scene.Light.Ambient,Color.white,.35f);rain.a=scene.Weather.Rain*.42f;
                    for(int drop=0;drop<count;drop++)
                    {
                        RainDrops++;
                        float x=Hash(scene.Level.decorSeed+drop*37),phase=Mathf.Repeat(time*.75f+Hash(drop*91+i*31),1);
                        var p=centre+new Vector2((x-.5f)*390,Mathf.Lerp(170,-135,phase));
                        var color=rain;color.a*=Mathf.Sin(phase*Mathf.PI);
                        RoadmapPainter.Ribbon(vh,p,p+new Vector2(1.4f,-9-Hash(drop*83)*7),.8f,color);
                        if(phase>.93f)
                        {
                            color.a*=.5f;var radius=(phase-.93f)*38;
                            RoadmapPainter.Ellipse(vh,p,new Vector2(radius,radius*.36f),RoadmapPainter.Clear(color),color,8);
                        }
                    }
                }
                else if(scene.Night)
                {
                    int count=_map.QualityTier==0?3:9;
                    for(int dot=0;dot<count;dot++)
                    {
                        float offset=Mathf.Sin(time*.25f+dot)*7;
                        var p=centre+new Vector2((Hash(dot*31+i*73)-.5f)*380,Hash(dot*93+i)*135+offset);
                        RoadmapGraphic.Glow(vh,p,3,.30f+.13f*Mathf.Sin(time*.7f+dot));
                    }
                }
            }
        }
        static float Hash(int seed)
        { uint x=(uint)seed;x^=x>>16;x*=0x7feb352du;x^=x>>15;x*=0x846ca68bu;x^=x>>16;return (x&65535)/65535f; }
        static void Beam(VertexHelper vh,Vector2 a,Vector2 b,float narrow,float wide,Color tint)
        {
            var direction=(b-a).normalized;var normal=new Vector2(-direction.y,direction.x);
            for(int i=0;i<8;i++)
            {
                float t=i/8f,u=(i+1)/8f;var p=Vector2.Lerp(a,b,t);var q=Vector2.Lerp(a,b,u);
                float w=Mathf.Lerp(narrow,wide,t),v=Mathf.Lerp(narrow,wide,u);
                var c=tint;c.a*=Mathf.Sin(t*Mathf.PI);var d=tint;d.a*=Mathf.Sin(u*Mathf.PI);
                RoadmapPainter.Quad(vh,p-normal*w,q-normal*v,q,p,RoadmapPainter.Clear(c),RoadmapPainter.Clear(d),d,c);
                RoadmapPainter.Quad(vh,p,q,q+normal*v,p+normal*w,c,d,RoadmapPainter.Clear(d),RoadmapPainter.Clear(c));
            }
        }
    }
}
