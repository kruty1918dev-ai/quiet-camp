using QuietCamp.Infrastructure;
using UnityEngine;
using UnityEngine.UI;

namespace QuietCamp.Presentation.UI
{
    /// <summary>A reusable visible scene slot. Each glade has its own bounded UI mesh;
    /// scrolling reassigns the slot instead of allocating 30 miniature worlds.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class RoadmapGladeGraphic : MaskableGraphic
    {
        RoadmapGraphic _map;
        int _index=-1;
        public int SceneIndex=>_index;
        public int Models { get; internal set; }
        public int Shadows { get; internal set; }
        public int Truncated { get; internal set; }
        public void Configure(RoadmapGraphic map,int index)
        { if(_map==map&&_index==index)return;_map=map;_index=index;SetVerticesDirty(); }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            using var audit = PerformanceAudit.Measure("QC.RoadmapGladeGraphic.OnPopulateMesh"); vh.Clear();if(_map!=null&&_index>=0)_map.PaintGlade(vh,_index,this); }
    }
}
