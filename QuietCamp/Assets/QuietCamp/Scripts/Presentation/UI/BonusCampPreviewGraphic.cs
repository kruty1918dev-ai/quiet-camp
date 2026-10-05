using System.Collections.Generic;
using QuietCamp.Domain;
using UnityEngine;
using UnityEngine.UI;

namespace QuietCamp.Presentation.UI
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class BonusCampPreviewGraphic : MaskableGraphic
    {
        BonusCampDefinition _slot;
        public void Configure(BonusCampDefinition slot) { _slot=slot;raycastTarget=false;SetVerticesDirty(); }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();if(_slot==null)return;
            using(var artwork=new VertexHelper())
            {
                RoadmapGraphic.BonusClearing(artwork,Vector2.zero,_slot);
                var stream=new List<UIVertex>();artwork.GetUIVertexStream(stream);
                if(stream.Count==0)return;
                var bounds=new Bounds(stream[0].position,Vector3.zero);
                foreach(var vertex in stream)bounds.Encapsulate(vertex.position);
                var rect=rectTransform.rect;
                float scale=Mathf.Max(0,Mathf.Min((rect.width-24)/bounds.size.x,(rect.height-24)/bounds.size.y));
                for(int i=0;i<stream.Count;i++)
                {
                    var v=stream[i];v.position=(v.position-bounds.center)*scale+(Vector3)rect.center;stream[i]=v;
                }
                vh.AddUIVertexTriangleStream(stream);
            }
        }
    }
}
