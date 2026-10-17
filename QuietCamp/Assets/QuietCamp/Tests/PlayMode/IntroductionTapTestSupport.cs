using System.Collections;
using NUnit.Framework;
using QuietCamp.Presentation.World;
using QuietCamp.Presentation;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace QuietCamp.Tests
{
    internal static class IntroductionTapTestSupport
    {
        public static void ResetRamBoot()
        {
            // Boot is persistent: loading its scene again intentionally retains
            // the live services. Independent fixtures must discard their own RAM session first.
            var boot=Object.FindFirstObjectByType<QuietCampBootstrap>();
            if(boot==null)return;
            var services=QuietCampBootstrap.ServicesRef;
            Assert.IsTrue(services==null||services.Save.MemoryOnly,"Never discard a normal player save in a fixture.");
            Object.DestroyImmediate(boot.gameObject);
        }
        public static IEnumerator Continue(CampSceneHost host)
        {
            var background=InputSystem.settings.backgroundBehavior;
            var editor=InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            var touch=InputSystem.AddDevice<Touchscreen>();
            try
            {
                Assert.IsTrue(host.CanContinueIntroduction);
                var at=new Vector2(Screen.width*.5f,Screen.height*.55f);
                InputSystem.QueueStateEvent(touch,new TouchState{touchId=96,phase=UnityEngine.InputSystem.TouchPhase.Began,position=at});yield return null;
                InputSystem.QueueStateEvent(touch,new TouchState{touchId=96,phase=UnityEngine.InputSystem.TouchPhase.Ended,position=at});yield return null;
                yield return null;
            }
            finally
            {
                InputSystem.RemoveDevice(touch);
                InputSystem.settings.backgroundBehavior=background;InputSystem.settings.editorInputBehaviorInPlayMode=editor;
            }
        }
    }
}
