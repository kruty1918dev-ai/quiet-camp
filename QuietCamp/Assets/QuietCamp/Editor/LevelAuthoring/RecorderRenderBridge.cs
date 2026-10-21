using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEditor.Recorder;
using UnityEngine;
using UnityEngine.Rendering;

namespace QuietCamp.Editor.LevelAuthoring
{
    /// <summary>
    /// Recorder 5.1.7's EOF coroutine stalls in this 6000.6 Editor after Simulator.
    /// Drive the unchanged Recorder session at the real URP end-context boundary.
    /// No encoder, audio mixing or input implementation is duplicated here.
    /// Private API is deliberately version-pinned and fails closed if unavailable.
    /// </summary>
    public sealed class RecorderRenderBridge : IDisposable
    {
        readonly RecordingSession _session;
        readonly Camera _camera;
        readonly Action<RecordingSession> _prepare, _record;
        bool _armed;
        public int Frames {get;private set;}
        public Exception Error {get;private set;}
        public RecorderRenderBridge(RecorderController controller,Camera camera)
        {
            var package=UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(RecorderController).Assembly);
            if(package?.version!="5.1.7")throw new NotSupportedException("Revalidate the Recorder bridge before changing package version.");
            var flags=BindingFlags.Instance|BindingFlags.NonPublic;
            var sessions=(IEnumerable)typeof(RecorderController).GetField("m_RecordingSessions",flags)?.GetValue(controller);
            _session=sessions?.Cast<RecordingSession>().Single()??throw new MissingMemberException("Recorder sessions API changed.");
            var component=(MonoBehaviour)typeof(RecordingSession).GetField("recorderComponent",flags)?.GetValue(_session);
            if(component==null)throw new MissingMemberException("Recorder component API changed.");
            _prepare=(Action<RecordingSession>)typeof(RecordingSession).GetMethod("PrepareNewFrame",flags).CreateDelegate(typeof(Action<RecordingSession>));
            _record=(Action<RecordingSession>)typeof(RecordingSession).GetMethod("RecordFrame",flags).CreateDelegate(typeof(Action<RecordingSession>));
            component.StopAllCoroutines();component.enabled=false;
            var restore=component.GetType().BaseType.GetMethod("RestoreProjectTimeScale",flags);
            if(restore==null)throw new MissingMemberException("Recorder time restoration API changed.");
            restore.Invoke(component,null);
            _camera=camera;
            RenderPipelineManager.endContextRendering+=Rendered;
        }
        public void Arm(){if(_armed)throw new InvalidOperationException("Previous frame still pending.");_armed=true;}
        void Rendered(ScriptableRenderContext context,System.Collections.Generic.List<Camera> cameras)
        {
            if(!_armed||!cameras.Contains(_camera))return;
            _armed=false;
            try{_prepare(_session);_record(_session);Frames++;}
            catch(Exception e){Error=e;}
        }
        public void Dispose(){RenderPipelineManager.endContextRendering-=Rendered;_armed=false;}
    }
}
