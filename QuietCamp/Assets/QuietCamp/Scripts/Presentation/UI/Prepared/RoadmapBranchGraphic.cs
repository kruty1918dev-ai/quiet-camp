using System.Collections;
using QuietCamp.Domain;
using UnityEngine;
using UnityEngine.UI;
namespace QuietCamp.Presentation.UI.Prepared
{
    /// <summary>One mesh per selected/pooled branch. Prep is yielded, never performed during Canvas rebuild.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class RoadmapBranchGraphic : MaskableGraphic
    {
        RoadmapBranchData _branch;RoadmapSceneGenerator.Scene _scene;Mesh _mesh;Coroutine _build;CanvasGroup _fade;
        RoadmapGraphic _map;int _anchor;Vector2 _centre;float _scale;
        public bool Ready {get;private set;}
        public int VertexCount=>_mesh?.vertexCount??0;
        public void Configure(RoadmapBranchData branch,RoadmapGraphic map=null,int anchor=0,Vector2 centre=default,float scale=24)
        {
            if(_branch==branch&&_map==map&&_centre==centre&&Mathf.Approximately(scale,_scale))return;
            if(_build!=null)StopCoroutine(_build);_branch=branch;_map=map;_anchor=anchor;_centre=centre;_scale=scale;Ready=false;raycastTarget=false;
            if(_fade==null)_fade=gameObject.AddComponent<CanvasGroup>();_fade.blocksRaycasts=false;
            if(material==null)material=defaultGraphicMaterial;SetMaterialDirty();
            _build=StartCoroutine(Build());
        }
        IEnumerator Build()
        {
            yield return null;
            while(rectTransform.rect.width<1||rectTransform.rect.height<1)yield return null;
            _scene=RoadmapBranchArt.Create(_branch,_scene);
            var painter=new RoadmapPainter(RoadmapModelLibrary.Load());
            using(var geometry=new VertexHelper())
            {
                float scale=_map!=null?_scale:Mathf.Min(rectTransform.rect.width/16,rectTransform.rect.height/8);
                var centre=_map!=null?_centre:rectTransform.rect.center;
                var ground=_scene.Ground;RoadmapPainter.Ellipse(geometry,centre,new Vector2(6*scale,3*scale),ground,RoadmapPainter.Clear(ground),32);
                foreach(var prop in _scene.Props){painter.Shadow(geometry,_scene,prop,centre,scale);yield return null;}
                foreach(var prop in _scene.Props)
                {
                    var model=prop.Geometry??RoadmapModelLibrary.Load().Get(prop.Asset);
                    for(int i=0;i<(model?.Positions.Length??0);i+=900)
                    {painter.Model(geometry,_scene,prop,centre,scale,i,Mathf.Min(i+900,model.Positions.Length));yield return null;}
                }
                if(_map==null&&_branch.visualIdentity=="fireflies")
                {
                    for(int light=0;light<6;light++)
                    {var at=centre+new Vector2(Mathf.Cos(light*2.3f)*scale*1.9f,Mathf.Sin(light*1.7f)*scale*.9f);var tint=new Color(.94f,.83f,.45f,.65f);RoadmapPainter.Ellipse(geometry,at,new Vector2(4,3),tint,RoadmapPainter.Clear(tint),10);}
                }
                if(_mesh==null){_mesh=new Mesh{name="Pooled branch presentation"};_mesh.MarkDynamic();}
                geometry.FillMesh(_mesh);Ready=true;SetVerticesDirty();SetMaterialDirty();
            }
            _build=null;
        }
        void LateUpdate(){if(_fade!=null)_fade.alpha=_map==null?1:_map.RevealOpacity(_anchor);}
        protected override void OnRectTransformDimensionsChange(){base.OnRectTransformDimensionsChange();if(_branch!=null&&_map==null&&isActiveAndEnabled){var branch=_branch;_branch=null;Configure(branch);}}
        protected override void UpdateGeometry(){if(_mesh!=null&&_mesh.vertexCount>0)canvasRenderer.SetMesh(_mesh);else canvasRenderer.Clear();}
        protected override void OnDisable(){if(_build!=null){StopCoroutine(_build);_build=null;_branch=null;Ready=false;}base.OnDisable();}
        protected override void OnDestroy(){if(_mesh!=null)Destroy(_mesh);base.OnDestroy();}
    }
}
