using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
namespace QuietCamp.Presentation.UI
{
    /// <summary>Two-touch view transform. Never alters geography, progression, meshes or reveal.</summary>
    public sealed class RoadmapZoomController : MonoBehaviour
    {
        RoadmapGraphic _map;ScrollRect _scroll;bool _pinching,_scrollEnabled;float _startDistance,_startZoom;Vector2 _worldPoint,_viewportSize;int _firstId,_secondId;
        public void Configure(RoadmapGraphic map,ScrollRect scroll){_map=map;_scroll=scroll;}
        void Update()
        {
            if(_map==null||!_map.HasBakedComposition)return;
            var screen=Touchscreen.current;int count=0,firstId=0,secondId=0;Vector2 first=default,second=default;
            if(screen!=null)foreach(var touch in screen.touches)if(touch.press.isPressed){if(count++==0){first=touch.position.ReadValue();firstId=touch.touchId.ReadValue();}else{second=touch.position.ReadValue();secondId=touch.touchId.ReadValue();break;}}
            if(count<2){End();return;}if(_pinching&&(_firstId!=firstId||_secondId!=secondId||_viewportSize!=_scroll.viewport.rect.size)){End();return;}if(!_map.InputEnabled){End();return;}
            var camera=_map.canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:_map.canvas.worldCamera;
            if(!RectTransformUtility.RectangleContainsScreenPoint(_scroll.viewport,first,camera)||!RectTransformUtility.RectangleContainsScreenPoint(_scroll.viewport,second,camera)){End();return;}
            var centre=(first+second)*.5f;RectTransformUtility.ScreenPointToLocalPointInRectangle(_map.rectTransform,centre,camera,out var local);
            float distance=Vector2.Distance(first,second);
            if(!_pinching)
            {
                _pinching=true;_firstId=firstId;_secondId=secondId;_viewportSize=_scroll.viewport.rect.size;_scrollEnabled=_scroll.enabled;_scroll.StopMovement();_scroll.enabled=false;_map.GestureActive=true;
                _startDistance=Mathf.Max(1,distance);_startZoom=_map.Zoom;
                float logical=_map.rectTransform.rect.yMax-_map.VisibleArea.center.y;
                _worldPoint=new Vector2(_map.ZoomOffsetX+(local.x-_map.rectTransform.rect.center.x)/(28*_map.Zoom),logical-(local.y-_map.VisibleArea.center.y)/_map.Zoom);
            }
            _map.SetZoom(Mathf.Clamp(_startZoom*distance/_startDistance,1,1.8f),local,_worldPoint);
        }
        void End(){if(!_pinching)return;_pinching=false;_map.GestureActive=false;_map.SelectionAfter=Time.unscaledTime+.2f;_scroll.enabled=_scrollEnabled;_scroll.StopMovement();}
        void OnDisable()=>End();
        void OnApplicationFocus(bool focused){if(!focused)End();}
    }
}
