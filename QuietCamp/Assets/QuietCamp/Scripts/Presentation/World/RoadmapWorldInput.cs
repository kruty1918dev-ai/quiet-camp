using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace QuietCamp.Presentation.World
{
    public sealed class RoadmapWorldInput : MonoBehaviour, IPointerDownHandler, IPointerUpHandler,
        IInitializePotentialDragHandler, IBeginDragHandler, IDragHandler, IScrollHandler
    {
        RoadmapWorldPresenter _world;
        Vector2 _down;
        int _pointer;
        bool _pressed, _dragged, _pinched;
        float _pinch;
        public void Configure(RoadmapWorldPresenter world) => _world = world;
        float Slop => 10 * Mathf.Max(1, Mathf.Min(Screen.width, Screen.height) / 360f);
        public void OnPointerDown(PointerEventData e)
        {
            if (e.button != PointerEventData.InputButton.Left) return;
            if (_pressed) { if (e.pointerId != _pointer) _pinched = true; return; }
            _pointer = e.pointerId; _down = e.position; _pressed = true;
            _dragged = false; _pinched = TouchCount() > 1;
            _world.BeginInteraction();
        }
        // Own the threshold in logical pixels, including on high-density phones.
        public void OnInitializePotentialDrag(PointerEventData e) => e.useDragThreshold = false;
        public void OnBeginDrag(PointerEventData e) { }
        public void OnDrag(PointerEventData e)
        {
            if (!_pressed || e.pointerId != _pointer || _pinched) return;
            Vector2 delta = e.delta;
            if (!_dragged)
            {
                delta = e.position - _down;
                if (delta.sqrMagnitude < Slop * Slop) return;
                _dragged = true;
            }
            _world.Drag(delta,e.position);
        }
        public void OnPointerUp(PointerEventData e)
        {
            if (e.button != PointerEventData.InputButton.Left || !_pressed || e.pointerId != _pointer) return;
            bool tap = !_dragged && !_pinched && !TouchCanceled()
                && (e.position - _down).sqrMagnitude < Slop * Slop;
            _pressed = false;
            if (tap) _world.Tap(e.position);
        }
        public void OnScroll(PointerEventData e)
        {
            if (_pressed || TouchCount() > 1) return;
            // Input System emits six units per mouse notch by default. Preserve
            // fractional trackpad events without multiplying that platform setting.
            var module = EventSystem.current?.currentInputModule as InputSystemUIInputModule;
            float perTick = module != null ? Mathf.Max(.01f, Mathf.Abs(module.scrollDeltaPerTick)) : 1;
            _world.Scroll(e.scrollDelta.y / perTick);
        }
        static int TouchCount()
        {
            int count = 0; var touch = Touchscreen.current;
            if (touch != null) foreach (var finger in touch.touches) if (finger.press.isPressed) count++;
            return count;
        }
        static bool TouchCanceled()
        {
            var touch = Touchscreen.current;
            if (touch != null) foreach (var finger in touch.touches)
                if (finger.phase.ReadValue() == UnityEngine.InputSystem.TouchPhase.Canceled) return true;
            return false;
        }
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
            if(count>=2)
            {
                if(!_pinched)_world.BeginInteraction();
                _pinched=true;_dragged=true;
                float distance=Vector2.Distance(a,b);
                if(_pinch>1)_world.Zoom(distance/_pinch);
                _pinch=distance;
            }
            else _pinch=0;
        }
        void ResetGesture(){_pressed=_dragged=_pinched=false;_pinch=0;}
        void OnApplicationFocus(bool focused){if(!focused){ResetGesture();_world?.BeginInteraction();}}
        void OnDisable()=>ResetGesture();
    }
}
