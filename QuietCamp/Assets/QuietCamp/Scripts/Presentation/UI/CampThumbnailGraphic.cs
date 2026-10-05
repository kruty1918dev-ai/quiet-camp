using QuietCamp.Domain;
using UnityEngine;
using UnityEngine.UI;

namespace QuietCamp.Presentation.UI
{
    // Snapshot miniatures share no camera, render texture or living world.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class CampThumbnailGraphic : MaskableGraphic
    {
        LevelData _level;Placement[] _tents;
        public void Configure(LevelData level,Placement[] tents){_level=level;_tents=tents;SetVerticesDirty();}
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();if(_level==null)return;
            var rect=rectTransform.rect;float unit=Mathf.Min(rect.width/(_level.width+1),rect.height/(_level.height+1));
            Vector2 At(float x,float z)=>new Vector2(rect.center.x+(x-_level.width*.5f)*unit,rect.center.y+(z-_level.height*.5f)*unit);
            Quad(vh,At(0,0),new Vector2(_level.width*unit,_level.height*unit),new Color(.45f,.58f,.39f));
            foreach(var b in _level.blocked)Quad(vh,At(b[0]+.13f,b[1]+.13f),Vector2.one*unit*.74f,new Color(.32f,.34f,.27f));
            foreach(var p in _tents??System.Array.Empty<Placement>())
            {
                var at=At(p.x+.16f,p.z+.16f);var size=Vector2.one*unit*1.68f;
                Quad(vh,at,size,new Color(.74f,.32f,.19f));
                int n=vh.currentVertCount;vh.AddVert(at,new Color(.89f,.48f,.25f),Vector2.zero);vh.AddVert(at+new Vector2(size.x,0),new Color(.89f,.48f,.25f),Vector2.zero);vh.AddVert(at+size*.5f,new Color(.97f,.67f,.38f),Vector2.zero);vh.AddTriangle(n,n+1,n+2);
            }
        }
        static void Quad(VertexHelper vh,Vector2 p,Vector2 size,Color c){int n=vh.currentVertCount;vh.AddVert(p,c,Vector2.zero);vh.AddVert(p+new Vector2(size.x,0),c,Vector2.zero);vh.AddVert(p+size,c,Vector2.zero);vh.AddVert(p+new Vector2(0,size.y),c,Vector2.zero);vh.AddTriangle(n,n+1,n+2);vh.AddTriangle(n,n+2,n+3);}
    }
}
