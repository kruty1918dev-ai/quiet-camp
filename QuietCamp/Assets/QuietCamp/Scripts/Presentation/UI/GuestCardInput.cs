using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using QuietCamp.Presentation.World;

namespace QuietCamp.Presentation.UI
{
    // The UI hands ownership to PlacementController once a deliberate hold/drag starts.
    public sealed class GuestCardInput : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler
    {
        PlacementController _placement;
        string _guest;
        Vector2 _press;
        float _time;
        int _pointer = -1;
        int _eventPointer;
        bool _held, _started;
        public void Configure(PlacementController placement, string guest) { _placement = placement; _guest = guest; }
        public void OnPointerDown(PointerEventData e)
        {
            if (_held || _placement == null) return;
            // InputSystem UI IDs combine device and touch IDs. Gameplay reads
            // TouchControl.touchId; mouse ownership is always zero.
            _pointer = e is ExtendedPointerEventData input
                ? input.pointerType == UIPointerType.Touch ? input.touchId : 0
                : e.pointerId < 0 ? 0 : e.pointerId;
            _eventPointer = e.pointerId;
            _press = e.position; _time = Time.unscaledTime; _held = true; _started = false;
        }
        public void OnDrag(PointerEventData e)
        {
            if (_held && e.pointerId == _eventPointer && !_started && Vector2.Distance(e.position,_press) >= BoardMath.DragThresholdDp*PlacementTargetProjector.ScreenDpScale()) StartDrag(e.position);
        }
        void Update()
        {
            if (_held && !_started && Time.unscaledTime-_time >= .2f) StartDrag(_press);
        }
        void StartDrag(Vector2 position)
        {
            _started = _placement.BeginCardDrag(_guest,_pointer,position);
            if (_started)
            {
                // Prevent the package button's release click from re-selecting
                // a different guest after this single drop.
                var button = GetComponent<Button>(); if (button != null) button.interactable = false;
            }
        }
        public void OnPointerUp(PointerEventData e) { if(e.pointerId == _eventPointer) _held = false; }
        void OnDisable() { _held = false; }
    }
}
