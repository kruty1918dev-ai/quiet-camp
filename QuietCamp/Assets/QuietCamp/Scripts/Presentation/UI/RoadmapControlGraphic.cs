using UnityEngine;
using UnityEngine.UI;

namespace QuietCamp.Presentation.UI
{
    /// <summary>Shared, texture-free trail badges. Geometry changes only when a pooled identity/state changes.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class RoadmapControlGraphic : MaskableGraphic
    {
        bool _branch,_compact,_current;
        public void Configure(bool branch,bool compact=false,bool current=false)
        {if(_branch==branch&&_compact==compact&&_current==current)return;_branch=branch;_compact=compact;_current=current;SetVerticesDirty();}
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();var r=rectTransform.rect;
            var shadow=new Color(.12f,.20f,.16f,.18f);
            if(!_branch)
            {
                float radius=_compact?18:Mathf.Min(r.width,r.height)*.48f;
                RoadmapPainter.Ellipse(vh,r.center+Vector2.down*4,new Vector2(radius+5,radius+5),shadow,RoadmapPainter.Clear(shadow),40);
                Disc(vh,r.center,_current?radius+3:radius,_current?new Color(.79f,.61f,.32f):new Color(.35f,.42f,.31f,.75f));
                Disc(vh,r.center+Vector2.up*.5f,radius-2,color);
                return;
            }
            Rounded(vh,new Rect(r.xMin-2,r.yMin-5,r.width+4,r.height+4),20,shadow);
            Rounded(vh,r,18,new Color(.35f,.42f,.31f,.75f));
            Rounded(vh,new Rect(r.xMin+2,r.yMin+2,r.width-4,r.height-4),16,color);
        }
        static void Disc(VertexHelper vh,Vector2 centre,float radius,Color tint)
            =>RoadmapPainter.Ellipse(vh,centre,new Vector2(radius,radius),tint,tint,40);
        static void Rounded(VertexHelper vh,Rect r,float radius,Color tint)
        {
            int start=vh.currentVertCount;vh.AddVert(r.center,tint,Vector2.zero);
            for(int corner=0;corner<4;corner++)
            {
                var centre=new Vector2(corner==0||corner==3?r.xMax-radius:r.xMin+radius,corner<2?r.yMax-radius:r.yMin+radius);
                for(int arc=0;arc<=8;arc++)
                {
                    float angle=(corner*90+arc*90f/8)*Mathf.Deg2Rad;
                    vh.AddVert(centre+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*radius,tint,Vector2.zero);
                }
            }
            for(int i=0;i<36;i++)vh.AddTriangle(start,start+1+i,start+1+(i+1)%36);
        }
    }
}
