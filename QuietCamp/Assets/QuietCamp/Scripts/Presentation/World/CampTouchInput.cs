using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace QuietCamp.Presentation.World
{
    /// <summary>Resolve the live finger across hardware and Device Simulator screens.</summary>
    public static class CampTouchInput
    {
        public static TouchControl Find(int touchId=-1,int deviceId=0)
        {
            TouchControl released=null;
            foreach(var device in InputSystem.devices)
            {
                if(!(device is Touchscreen screen)||!screen.enabled||deviceId>0&&screen.deviceId!=deviceId)continue;
                foreach(var touch in screen.touches)
                {
                    if(touchId>0&&touch.touchId.ReadValue()!=touchId)continue;
                    if(touch.press.isPressed)return touch;
                    if(touch.press.wasReleasedThisFrame||touchId>0&&touch.phase.ReadValue()==TouchPhase.Canceled)released=touch;
                }
            }
            return released;
        }
    }
}
