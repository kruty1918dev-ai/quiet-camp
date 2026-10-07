using System;
using QuietCamp.Infrastructure;
using UnityEngine;
using UnityEngine.UI;

namespace QuietCamp.Presentation.UI
{
    public sealed class MenuMapBinding : MonoBehaviour
    {
        GameServices _services;
        ScrollRect _scroll;
        int _frames;
        int _settle;
        bool _armed;
        Func<bool> _canRemember;
        public void Configure(GameServices services,Func<bool> canRemember=null)
        {
            if (_services != null) return;
            _services=services;_canRemember=canRemember; _scroll=GetComponent<ScrollRect>(); _frames=3;
            if (_scroll!=null) _scroll.onValueChanged.AddListener(Remember);
        }
        void LateUpdate()
        {
            if (_scroll==null || _frames<=0 || --_frames>0) return;
            // A freshly mounted list still reports zero content height; applying
            // a normalized position now would clamp it to the top forever.
            var contentH=_scroll.content!=null?_scroll.content.rect.height:0f;
            var viewH=_scroll.viewport!=null?_scroll.viewport.rect.height:((RectTransform)transform).rect.height;
            if (contentH<=viewH&&_settle++<240){_frames=1;return;}
            float target=_services.LevelMapScroll;
            if (target<0)
            {
                var ids=LevelLoader.MvpLevelIds();
                var id=_services.Progression.ContinueTarget(ids); int index=0;
                for (int i=0;i<ids.Count;i++) if (ids[i]==id) { index=i; break; }
                target=1-Mathf.Clamp01((RoadmapLayout.MainY(index)-viewH*.45f)/Mathf.Max(1,RoadmapLayout.Height(ids.Count)-viewH));
            }
            _scroll.verticalNormalizedPosition=target;
            _services.LevelMapScroll=target; _armed=true;
        }
        void Remember(Vector2 position)
        { if (_armed&&(_canRemember?.Invoke()??true)&&_scroll.isActiveAndEnabled) _services.LevelMapScroll=_scroll.verticalNormalizedPosition; }
        public void Freeze()
        {
            if(_armed&&_scroll!=null)_services.LevelMapScroll=_scroll.verticalNormalizedPosition;
            _armed=false;
        }
        void OnDisable(){_armed=false;}
        void OnDestroy() { if (_scroll!=null) _scroll.onValueChanged.RemoveListener(Remember); }
    }
}
