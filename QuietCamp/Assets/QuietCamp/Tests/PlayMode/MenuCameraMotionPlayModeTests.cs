using System.Collections;
using System.Linq;
using Kruty1918.InputRouting.Runtime;
using NUnit.Framework;
using QuietCamp.Infrastructure;
using QuietCamp.Presentation.World;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace QuietCamp.Tests
{
    public sealed class MenuCameraMotionPlayModeTests
    {
        GameObject _root,_uiRoot;
        Camera _camera;
        MenuCameraDrift _motion;
        Mouse _mouse;Mouse[] _otherMice;
        bool _reduced,_suppressed;
        InputSettings.BackgroundBehavior _background;
        InputSettings.EditorInputBehaviorInPlayMode _editorInput;
        Vector2 Centre=>new Vector2(Screen.width*.5f,Screen.height*.5f);

        [SetUp] public void SetUp()
        {
            _reduced=false;_suppressed=false;
            _background=InputSystem.settings.backgroundBehavior;_editorInput=InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            _otherMice=InputSystem.devices.OfType<Mouse>().Where(m=>m.enabled).ToArray();
            foreach(var mouse in _otherMice)InputSystem.DisableDevice(mouse);
            _mouse=InputSystem.AddDevice<Mouse>();
            _root=new GameObject("menu-camera-motion-test");_camera=_root.AddComponent<Camera>();
            _camera.orthographic=true;_camera.transform.rotation=Quaternion.Euler(52,225,0);
            _motion=_root.AddComponent<MenuCameraDrift>();Configure();
        }
        void Configure(GameplayInputPolicy input=null)=>_motion.Configure(_camera,LevelLoader.Load("QC001"),null,()=>_reduced,()=>_suppressed,input);
        void MouseAt(Vector2 position,bool down)
        {_mouse.MakeCurrent();InputSystem.QueueStateEvent(_mouse,new MouseState{position=position}.WithButton(MouseButton.Left,down));}
        [TearDown] public void TearDown()
        {
            if(_uiRoot!=null)Object.DestroyImmediate(_uiRoot);
            Object.DestroyImmediate(_root);InputSystem.RemoveDevice(_mouse);
            foreach(var mouse in _otherMice)InputSystem.EnableDevice(mouse);
            InputSystem.settings.backgroundBehavior=_background;InputSystem.settings.editorInputBehaviorInPlayMode=_editorInput;
        }

        [UnityTest] public IEnumerator HoverAndTapsDoNotSteerOrRotateTheMenuCamera()
        {
            yield return null;var rotation=_camera.transform.rotation;
            MouseAt(Vector2.one,false);yield return null;
            MouseAt(new Vector2(Screen.width-1,Screen.height-1),false);yield return null;
            MouseAt(Centre,true);yield return null;
            MouseAt(Centre+Vector2.one,false);yield return null;
            Assert.AreEqual(Vector2.zero,_motion.SwipeOffset);
            Assert.Less(_motion.VisualOffset.magnitude,.021f,"Passive movement is at most a tiny translation.");
            Assert.Less(Quaternion.Angle(rotation,_camera.transform.rotation),.001f,"No rotational rocking.");
        }

        [UnityTest] public IEnumerator MouseSwipeIsBoundedAndSmoothlyReturnsAfterRelease()
        {
            yield return null;var rotation=_camera.transform.rotation;
            MouseAt(Centre,true);yield return null;
            MouseAt(Centre+Vector2.right*Mathf.Min(Screen.width,Screen.height)*.3f,true);yield return null;
            float until=Time.realtimeSinceStartup+.65f;
            while(Time.realtimeSinceStartup<until)
            {
                var before=_camera.transform.position;yield return null;
                Assert.LessOrEqual(Vector3.Distance(before,_camera.transform.position),.002f+.2f*Time.unscaledDeltaTime,"The swipe must not snap the camera.");
                Assert.LessOrEqual(_motion.VisualOffset.magnitude,.096f);
            }
            float peak=_motion.SwipeOffset.magnitude;Assert.Greater(peak,.004f);
            Assert.Less(Quaternion.Angle(rotation,_camera.transform.rotation),.001f);
            MouseAt(Centre,false);yield return null;
            Assert.Greater(_motion.SwipeOffset.magnitude,peak*.5f,"Releasing must ease home, not snap.");
            yield return new WaitForSecondsRealtime(2.4f);
            Assert.Less(_motion.SwipeOffset.magnitude,peak*.1f);
        }

        [UnityTest] public IEnumerator TouchSwipeMovesOnlySlightlyAndASecondFingerCancelsIt()
        {
            var touch=InputSystem.AddDevice<Touchscreen>();
            try
            {
                yield return null;
                InputSystem.QueueStateEvent(touch,new TouchState{touchId=61,phase=UnityEngine.InputSystem.TouchPhase.Began,position=Centre});yield return null;
                var destination=Centre+Vector2.up*Mathf.Min(Screen.width,Screen.height)*.25f;
                InputSystem.QueueStateEvent(touch,new TouchState{touchId=61,phase=UnityEngine.InputSystem.TouchPhase.Moved,position=destination});
                yield return new WaitForSecondsRealtime(.6f);
                float peak=_motion.SwipeOffset.magnitude;Assert.Greater(peak,.004f);Assert.Less(peak,.076f);
                InputSystem.QueueStateEvent(touch,new TouchState{touchId=62,phase=UnityEngine.InputSystem.TouchPhase.Began,position=Centre});yield return null;
                yield return new WaitForSecondsRealtime(2.4f);Assert.Less(_motion.SwipeOffset.magnitude,peak*.1f);
            }
            finally{InputSystem.RemoveDevice(touch);}
        }

        [UnityTest] public IEnumerator SwipingARealUiButtonNeverMovesTheScenery()
        {
            var system=new GameObject("isolated menu event system",typeof(EventSystem));system.transform.SetParent(_root.transform);
            var canvas=new GameObject("menu controls",typeof(RectTransform),typeof(Canvas),typeof(GraphicRaycaster));_uiRoot=canvas;
            canvas.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
            var button=new GameObject("menu button",typeof(RectTransform),typeof(CanvasRenderer),typeof(Image),typeof(Button));button.transform.SetParent(canvas.transform,false);
            ((RectTransform)button.transform).sizeDelta=new Vector2(Screen.width*.25f,Screen.height*.25f);
            var input=new GameplayInputPolicy(system.GetComponent<EventSystem>());Configure(input);
            yield return null;Canvas.ForceUpdateCanvases();
            Assert.IsFalse(input.CanProcess(Kruty1918.InputRouting.API.GameplayInputKind.PointerPan,Centre,0));
            MouseAt(Centre,true);yield return null;
            MouseAt(new Vector2(Screen.width-1,Screen.height-1),true);yield return new WaitForSecondsRealtime(.6f);
            Assert.AreEqual(Vector2.zero,_motion.SwipeOffset,"A gesture starting on UI stays excluded after leaving the button.");
            MouseAt(Centre,false);yield return null;
        }

        [UnityTest] public IEnumerator ReducedMotionAndTransitionsKeepTheirOwnCameraPose()
        {
            yield return null;_reduced=true;yield return null;
            Assert.AreEqual(Vector3.zero,_motion.VisualOffset);
            MouseAt(Centre,true);yield return null;MouseAt(Centre+Vector2.right*150,true);yield return null;
            Assert.AreEqual(Vector2.zero,_motion.SwipeOffset);
            _suppressed=true;var externalPosition=new Vector3(13,17,19);var externalRotation=Quaternion.Euler(33,44,5);
            _camera.transform.SetPositionAndRotation(externalPosition,externalRotation);yield return null;yield return null;
            Assert.AreEqual(externalPosition,_camera.transform.position);Assert.Less(Quaternion.Angle(externalRotation,_camera.transform.rotation),.001f);
            _motion.enabled=false;Assert.AreEqual(externalPosition,_camera.transform.position,"Disabling must not overwrite a transition's work.");
        }
    }
}
