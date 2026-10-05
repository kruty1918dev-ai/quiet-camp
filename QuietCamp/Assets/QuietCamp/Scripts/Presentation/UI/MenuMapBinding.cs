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
            float target=_services.LevelMapScroll;
            if (target<0)
            {
                var ids=LevelLoader.MvpLevelIds();
                var id=_services.Progression.ContinueTarget(ids); int index=0;
                for (int i=0;i<ids.Count;i++) if (ids[i]==id) { index=i; break; }
                var height=_scroll.viewport!=null?_scroll.viewport.rect.height:((RectTransform)transform).rect.height;
                target=1-Mathf.Clamp01((RoadmapLayout.MainY(index)-height*.45f)/Mathf.Max(1,RoadmapLayout.Height(ids.Count)-height));
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
