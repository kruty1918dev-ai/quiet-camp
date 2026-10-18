using System.Collections.Generic;
using UnityEditor;
using UnityEditor.DeviceSimulation;
using UnityEngine;
using UnityEngine.InputSystem;

namespace QuietCamp.Editor
{
    /// <summary>Keep Simulator gestures in the player input stream while editor focus changes.</summary>
    public sealed class SimulatorTouchRouting : DeviceSimulatorPlugin
    {
        public override string title=>"QuietCamp input";
        static readonly HashSet<(int owner,int finger)> Active=new();
        static EditorPointerInputScope _scope;
        static int _nextOwner,_releaseFrame;
        static bool _hooks;
        int _owner;
        internal static string Diagnostics=>$"fingers={Active.Count}; lease={_scope!=null}; release={_releaseFrame}; frame={Time.frameCount}";

        public override void OnCreate()
        {
            _owner=++_nextOwner;
            if(deviceSimulator!=null)deviceSimulator.touchScreenInput+=Touch;
            if(_hooks)return;
            _hooks=true;
            EditorApplication.update+=Tick;
            EditorApplication.playModeStateChanged+=PlayState;
            AssemblyReloadEvents.beforeAssemblyReload+=Release;
            EditorApplication.quitting+=Release;
        }
        void Touch(TouchEvent input)
        {
            if(!EditorApplication.isPlaying||Infrastructure.EditorAuthoringPreview.ControlledCapture)return;
            SessionState.SetString("QuietCamp.Simulator.LastTouch",input.phase+" #"+input.touchId+" owner="+_owner+" "+input.position);
            var key=(_owner,input.touchId);
            if(input.phase==UnityEditor.DeviceSimulation.TouchPhase.Began)
            {
                if(_scope==null)_scope=new EditorPointerInputScope();
                Active.Add(key);
            }
            else if(input.phase==UnityEditor.DeviceSimulation.TouchPhase.Ended||input.phase==UnityEditor.DeviceSimulation.TouchPhase.Canceled)
            {
                Active.Remove(key);_releaseFrame=Time.frameCount+2;
            }
        }
        static void Tick()
        {
            if(_scope!=null&&Active.Count==0&&Time.frameCount>=_releaseFrame)Release();
        }
        static void PlayState(PlayModeStateChange state)
        {if(state==PlayModeStateChange.ExitingPlayMode||state==PlayModeStateChange.EnteredEditMode)Release();}
        static void Release()
        {
            Active.Clear();
            _scope?.Dispose();_scope=null;
        }
        public override void OnDestroy()
        {
            if(deviceSimulator!=null)deviceSimulator.touchScreenInput-=Touch;
            Active.RemoveWhere(key=>key.owner==_owner);
            if(Active.Count==0)Release();
        }
    }
}
