using UnityEngine;
using UnityEngine.UI;

namespace QuietCamp.Presentation.UI
{
    /// <summary>One bounded native mesh slot. Geometry is prepared in budgeted slices, never in Canvas rebuild.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class RoadmapGladeGraphic : MaskableGraphic
    {
        RoadmapGraphic _map;Mesh _cached;CanvasGroup _revealGroup;
        int _index=-1;
        public int SceneIndex=>_index;
        public int Version {get;private set;}
        public bool NeedsGeometry {get;private set;}=true;
        public int Models {get;internal set;}
        public int Shadows {get;internal set;}
        public int Truncated {get;internal set;}
        public int Vertices {get;internal set;}
        public void Configure(RoadmapGraphic map,int index)
        {
            if(_map==map&&_index==index)return;
            _map=map;_index=index;
            if(_revealGroup==null)_revealGroup=gameObject.GetComponent<CanvasGroup>();
            if(_revealGroup==null)_revealGroup=gameObject.AddComponent<CanvasGroup>();
            _revealGroup.alpha=_map.RevealOpacity(_index);
            ClearCache();InvalidateGeometry();
        }
        void LateUpdate()
        {
            if(_map!=null&&_index>=0&&_revealGroup!=null)
                _revealGroup.alpha=_map.RevealOpacity(_index);
        }
        public void InvalidateGeometry(){NeedsGeometry=true;Version++;}
        public void Commit(VertexHelper geometry,int models,int shadows,int truncated)
        {
            if(_cached==null){_cached=new Mesh{name="Roadmap pooled glade mesh"};_cached.MarkDynamic();}
            geometry.FillMesh(_cached);Vertices=geometry.currentVertCount;Models=models;Shadows=shadows;Truncated=truncated;
            NeedsGeometry=false;SetVerticesDirty();
        }
        public void ClearCache()
        {canvasRenderer.Clear();_cached?.Clear();Vertices=Models=Shadows=Truncated=0;}
        protected override void UpdateGeometry()
        {if(_cached!=null&&Vertices>0)canvasRenderer.SetMesh(_cached);else canvasRenderer.Clear();}
        protected override void OnDestroy(){base.OnDestroy();if(_cached!=null)Destroy(_cached);}
    }
}
