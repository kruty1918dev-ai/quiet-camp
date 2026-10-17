using System;
using Kruty1918.InputRouting.API;
using QuietCamp.Domain;
using UnityEngine;
using UnityEngine.InputSystem;

namespace QuietCamp.Presentation.World
{
    /// <summary>
    /// Fitted menu pose with a tiny, slow translation and a bounded swipe
    /// response. Hovering and tapping UI never steer the camera. Its angle
    /// stays fixed; reduced motion restores the fitted pose.
    /// </summary>
    public sealed class MenuCameraDrift : MonoBehaviour
    {
        Camera _camera;
        LevelData _level;
        RectTransform _viewport;
        Func<bool> _reducedMotion;
        Func<bool> _suppressed;
        IGameplayInputPolicy _input;
        Vector3 _basePos;
        Quaternion _baseRot;
        Vector2 _parallax;
        Vector2 _parallaxTarget, _parallaxVelocity, _pressPosition;
        Vector3 _offset, _lastPosition;
        Quaternion _lastRotation;
        int _pointerId = -1;
        bool _gesture, _swiping, _hasWritten;
        float _elapsed;
        Vector2Int _screen;
        Rect _viewportRect;
        Vector3 _viewportPosition;
        bool _dirty = true;
        public Vector3 VisualOffset => _offset;
        public Vector2 SwipeOffset => _parallax * SwipeRange;
        float SwipeRange => _camera == null ? 0 : Mathf.Min(.075f,_camera.orthographicSize*.008f);

        public void Configure(Camera camera, LevelData level, RectTransform viewport,
            Func<bool> reducedMotion, Func<bool> suppressed = null, IGameplayInputPolicy input = null)
        {
            _camera = camera;
            _level = level;
            _viewport = viewport;
            _reducedMotion = reducedMotion;
            _suppressed = suppressed;
            _input = input;
            _baseRot = camera.transform.rotation;
            _dirty = true;
        }

        void LateUpdate()
        {
            if (_camera == null || _level == null) return;
            // A transition owns the actual transform. Never snap it back to
            // the fitted pose while the other owner is moving it.
            if (_suppressed?.Invoke()==true)
            {
                ClearGesture();_dirty=true;_hasWritten=false;return;
            }
            var size = new Vector2Int(Screen.width, Screen.height);
            var rect = _viewport != null ? _viewport.rect : default;
            var position = _viewport != null ? _viewport.position : Vector3.zero;
            if (size != _screen || _dirty || rect != _viewportRect || position != _viewportPosition)
            {
                _screen = size; _viewportRect = rect; _viewportPosition = position;
                _camera.transform.rotation = _baseRot;
                CameraFitter.Fit(_camera, _level, _viewport);
                _basePos = _camera.transform.position;
                ClearGesture();_parallax=Vector2.zero;_parallaxVelocity=Vector2.zero;_offset=Vector3.zero;
                _dirty = false;
            }
            if (_reducedMotion?.Invoke()==true)
            {
                ClearGesture();_parallax=Vector2.zero;_parallaxVelocity=Vector2.zero;_offset=Vector3.zero;
                WritePose();
                return;
            }
            ReadSwipe();
            float dt=Mathf.Min(.05f,Time.unscaledDeltaTime);
            _parallax=Vector2.SmoothDamp(_parallax,_parallaxTarget,ref _parallaxVelocity,.6f,2f,dt);
            _elapsed+=dt;
            var right = _baseRot * Vector3.right;
            var up = _baseRot * Vector3.up;
            // Scenery follows the finger by a few pixels, then eases home.
            var swipe=SwipeOffset;
            _offset=right*(Mathf.Sin(_elapsed*.15f)*.012f-swipe.x)
                +up*(Mathf.Sin(_elapsed*.12f)*.008f-swipe.y);
            WritePose();
        }

        void ReadSwipe()
        {
            var touch=Touchscreen.current?.primaryTouch;
            bool touching=touch!=null&&(touch.press.isPressed||touch.press.wasReleasedThisFrame);
            var mouse=Mouse.current;
            if(!touching&&mouse==null){ClearGesture();return;}
            int id=touching?touch.touchId.ReadValue():0;
            var position=touching?touch.position.ReadValue():mouse.position.ReadValue();
            bool began=touching?touch.press.wasPressedThisFrame:mouse.leftButton.wasPressedThisFrame;
            bool held=touching?touch.press.isPressed:mouse.leftButton.isPressed;
            if(touching)
            {
                int count=0;foreach(var finger in Touchscreen.current.touches)if(finger.press.isPressed)count++;
                if(count>1){ClearGesture();return;}
            }
            if(began)
            {
                _pointerId=id;_pressPosition=position;_swiping=false;_parallaxTarget=Vector2.zero;
                _gesture=_camera.pixelRect.Contains(position)&&(_input?.CanProcess(GameplayInputKind.PointerPan,position,id)??true);
            }
            if(!held||id!=_pointerId){ClearGesture();return;}
            if(!_gesture)return;
            if(!(_input?.CanProcess(GameplayInputKind.PointerPan,position,id)??true)){ClearGesture();return;}
            var delta=position-_pressPosition;
            float threshold=12*PlacementTargetProjector.ScreenDpScale();
            if(!_swiping&&delta.sqrMagnitude<threshold*threshold)return;
            _swiping=true;
            _parallaxTarget=Vector2.ClampMagnitude(delta*(3f/Mathf.Max(1,Mathf.Min(Screen.width,Screen.height))),1);
        }

        void ClearGesture(){_gesture=false;_swiping=false;_pointerId=-1;_parallaxTarget=Vector2.zero;}
        void WritePose()
        {
            _camera.transform.SetPositionAndRotation(_basePos+_offset,_baseRot);
            _lastPosition=_camera.transform.position;_lastRotation=_camera.transform.rotation;_hasWritten=true;
        }
        void OnApplicationFocus(bool focused){if(!focused)ClearGesture();}
        void OnDisable()
        {
            if(_hasWritten&&_camera!=null&&_camera.transform.position==_lastPosition&&_camera.transform.rotation==_lastRotation)
                _camera.transform.SetPositionAndRotation(_basePos,_baseRot);
            ClearGesture();_parallax=Vector2.zero;_parallaxVelocity=Vector2.zero;_offset=Vector3.zero;_hasWritten=false;_dirty=true;
        }
    }
}
