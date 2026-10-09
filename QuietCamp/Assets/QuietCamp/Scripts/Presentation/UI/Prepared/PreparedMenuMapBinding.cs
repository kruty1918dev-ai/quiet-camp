using System;
using QuietCamp.Infrastructure;
using QuietCamp.Domain;
using UnityEngine;
using UnityEngine.UI;

namespace QuietCamp.Presentation.UI.Prepared
{
    /// <summary>Restore by stable node/offset after measured layout, never a fixed frame count.</summary>
    public sealed class PreparedMenuMapBinding : MonoBehaviour
    {
        GameServices _services;ScrollRect _scroll;Func<bool> _canRemember;
        bool _armed,_restore=true,_subscribed;
        Vector2 _viewportSize;
        public void Configure(GameServices services,Func<bool> canRemember=null)
        {
            _services=services;_canRemember=canRemember;
            if(!_armed&&(_canRemember?.Invoke()??true))_restore=true;
            if(_scroll==null)_scroll=GetComponent<ScrollRect>();
            if(_scroll!=null&&!_subscribed){_scroll.onValueChanged.AddListener(Remember);_subscribed=true;}
        }
        void LateUpdate()
        {
            if(_scroll==null||_services==null||_scroll.viewport==null||_scroll.content==null)return;
            var size=_scroll.viewport.rect.size;
            if(_armed&&size!=_viewportSize){_armed=false;_restore=true;}
            _viewportSize=size;
            if(!_restore)return;
            var map=_scroll.GetComponentInChildren<RoadmapGraphic>();
            if(map?.Data==null||_scroll.viewport.rect.height<=0||Mathf.Abs(_scroll.content.rect.height-map.Data.Height)>2)return;
            _services.LevelMapAnchors.TryGetValue(map.Data.Definition.journeyId,out var anchor);
            if(anchor==null)anchor=_services.LevelMapAnchor;int index=anchor!=null&&anchor.journeyId==map.Data.Definition.journeyId?map.Data.NodeIndex(anchor.nodeId):-1;
            float offset=index>=0&&anchor.revision==map.Data.Definition.revision?anchor.offset:0;
            if(index>=0&&map.Reveal(index)==RoadmapReveal.Hidden){index=-1;offset=0;}
            if(index<0){index=map.Data.LevelIndex(_services.JourneyAccess.ContinueTarget(map.Data.Definition.journeyId));if(index<0)index=map.Frontier;}
            Apply(map,index,offset);_restore=false;_armed=true;Capture(map);
        }
        void Apply(RoadmapGraphic map,int index,float offset)
        {
            float viewport=_scroll.viewport.rect.height,range=Mathf.Max(1,map.Data.Height-viewport);
            _scroll.StopMovement();_scroll.verticalNormalizedPosition=1-Mathf.Clamp01(Mathf.Min(map.MaxScrollDistance,map.Data.Y(index)+offset-viewport*.45f)/range);
            _services.LevelMapScroll=_scroll.verticalNormalizedPosition;
        }
        public void ScrollTo(string nodeId)
        {
            var map=_scroll?.GetComponentInChildren<RoadmapGraphic>();int index=map?.Data?.NodeIndex(nodeId)??-1;
            if(index<0)return;Apply(map,index,0);Capture(map);
        }
        void Remember(Vector2 position)
        {if(_armed&&_scroll.viewport.rect.size==_viewportSize&&(_canRemember?.Invoke()??true)&&_scroll.isActiveAndEnabled){var map=_scroll.GetComponentInChildren<RoadmapGraphic>();if(map?.Data!=null)Capture(map);}}
        void Capture(RoadmapGraphic map)
        {
            float viewport=_scroll.viewport.rect.height;
            float distance=(1-_scroll.verticalNormalizedPosition)*Mathf.Max(0,map.Data.Height-viewport)+viewport*.45f;
            int index=map.Data.NearestNode(distance);
            if(!_services.LevelMapAnchors.TryGetValue(map.Data.Definition.journeyId,out var anchor))
            {anchor=new RoadmapAnchor();_services.LevelMapAnchors.Add(map.Data.Definition.journeyId,anchor);}
            _services.LevelMapAnchor=anchor;
            anchor.journeyId=map.Data.Definition.journeyId;anchor.nodeId=map.Data.Nodes[index].id;anchor.offset=distance-map.Data.Y(index);anchor.revision=map.Data.Definition.revision;
            _services.LevelMapScroll=_scroll.verticalNormalizedPosition;
        }
        public void Freeze(){if(_armed&&_scroll!=null&&_scroll.viewport!=null&&_scroll.viewport.rect.size==_viewportSize){var map=_scroll.GetComponentInChildren<RoadmapGraphic>();if(map?.Data!=null)Capture(map);}_armed=false;}
        void OnEnable(){_restore=true;_armed=false;}
        void OnDisable(){Freeze();_restore=true;}
        void OnDestroy(){if(_scroll!=null&&_subscribed)_scroll.onValueChanged.RemoveListener(Remember);}
    }
}
