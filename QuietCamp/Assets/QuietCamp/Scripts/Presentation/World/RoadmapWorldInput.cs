using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace QuietCamp.Presentation.World
{
    public sealed class RoadmapWorldInput : MonoBehaviour,IPointerDownHandler,IPointerUpHandler,IBeginDragHandler,IDragHandler,IScrollHandler
    {
        RoadmapWorldPresenter _world;Vector2 _down;bool _dragged,_pinched;float _pinch;
        public void Configure(RoadmapWorldPresenter world)=>_world=world;
        public void OnPointerDown(PointerEventData e){_down=e.position;_dragged=false;_pinched=false;}
        public void OnBeginDrag(PointerEventData e)=>_dragged=true;
        public void OnDrag(PointerEventData e){_dragged=true;if(!_pinched)_world.Drag(e.delta.y);}
        public void OnPointerUp(PointerEventData e){if(!_dragged&&!_pinched&&Vector2.Distance(_down,e.position)<12)_world.Tap(e.position);}
        public void OnScroll(PointerEventData e)=>_world.Drag(e.scrollDelta.y*Screen.height*.07f);
        void Update()
        {
            var keys=Keyboard.current;
            if(keys!=null)
            {
                if(keys.upArrowKey.wasPressedThisFrame||keys.rightArrowKey.wasPressedThisFrame)_world.Step(1);
                if(keys.downArrowKey.wasPressedThisFrame||keys.leftArrowKey.wasPressedThisFrame)_world.Step(-1);
                if(keys.enterKey.wasPressedThisFrame)_world.ActivateFocused();
            }
            var touch=Touchscreen.current;if(touch==null)return;Vector2 a=default,b=default;int count=0;
            foreach(var t in touch.touches)if(t.press.isPressed){if(count++==0)a=t.position.ReadValue();else{b=t.position.ReadValue();break;}}
            if(count>=2){float distance=Vector2.Distance(a,b);if(_pinch>0)_world.Zoom(distance/_pinch);_pinch=distance;_pinched=true;_dragged=true;}else _pinch=0;
        }
    }
}
