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
        float _lastPos;
        int _stable;
        Func<bool> _canRemember;
        Action<float,float> _windowChanged;
        public void Configure(GameServices services,Func<bool> canRemember=null,Action<float,float> windowChanged=null)
        {
            _windowChanged = windowChanged;
            if (_services != null) return;
            _services=services;_canRemember=canRemember; _scroll=GetComponent<ScrollRect>(); _frames=3;
        }
        void LateUpdate()
        {
            if (_scroll==null) return;
            if (_armed && Scrollable())
            {
                float height = _scroll.viewport != null ? _scroll.viewport.rect.height : ((RectTransform)transform).rect.height;
                float top = (1-_scroll.verticalNormalizedPosition)*Mathf.Max(0,_scroll.content.rect.height-height);
                _windowChanged?.Invoke(top, height);
            }
            if (_frames>0)
            {
                if (--_frames>0) return;
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
                _lastPos=target;_stable=2;
                return;
            }
            if (!_armed||!Scrollable()) return;
            // Persist only positions that survived a couple of frames: during
            // layout passes the normalized value oscillates and sampling it at
            // teardown would store a transient clamp.
            var pos=_scroll.verticalNormalizedPosition;
            if (Mathf.Abs(pos-_lastPos)<.001f)
            {
                if (++_stable==2&&(_canRemember?.Invoke()??true)) _services.LevelMapScroll=pos;
            }
            else {_stable=0;_lastPos=pos;}
        }
        bool Scrollable()
        {
            // During DOM teardown the content rect collapses and the scroll
            // snaps to the top — persisting that clobbers the remembered
            // position. Only trust values while the list is actually scrollable.
            var contentH=_scroll.content!=null?_scroll.content.rect.height:0f;
            var viewH=_scroll.viewport!=null?_scroll.viewport.rect.height:((RectTransform)transform).rect.height;
            return contentH>viewH&&viewH>1f;
        }
        public void Freeze()
        {
            if(_armed&&_scroll!=null&&Scrollable())
                _services.LevelMapScroll=_stable>=2?_lastPos:_scroll.verticalNormalizedPosition;
            _armed=false;
        }
        void OnDisable(){_armed=false;}
    }
}
