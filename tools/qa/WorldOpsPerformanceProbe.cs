// Passive Editor fixture: copy into the existing authorized QA project's Editor folder.
// Observes WorldOps capture frames only; never starts Play Mode, builds, changes settings or SDKs.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using QuietCamp.Editor.LevelAuthoring.WorldOps;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
static class WorldOpsPerformanceProbe
{
    sealed class Counter {public string name,unit;public ProfilerRecorder recorder;}
    sealed class Frame {public int frame;public long[] values;}
    static readonly List<Counter> Counters=new List<Counter>();
    static readonly List<Frame> Frames=new List<Frame>();
    static string _folder,_runId;static int _lastFrame=-1,_width,_height;
    static WorldOpsPerformanceProbe()
    {
        EditorApplication.update+=Tick;AssemblyReloadEvents.beforeAssemblyReload+=Flush;EditorApplication.quitting+=Flush;
    }
    static void Tick()
    {
        var run=WorldOpsService.Current;
        string folder=EditorApplication.isPlaying&&run?.nativeState=="recording"?run.nativeFolder:null;
        if(folder!=_folder)
        {
            Flush();if(folder==null)return;
            string home=Path.GetFullPath(WorldOpsService.Home)+Path.DirectorySeparatorChar;
            if(!Path.GetFullPath(folder).StartsWith(home,StringComparison.OrdinalIgnoreCase))return;
            _folder=folder;_runId=run.id;_width=Screen.width;_height=Screen.height;
            var handles=new List<ProfilerRecorderHandle>();ProfilerRecorderHandle.GetAvailable(handles);
            foreach(var handle in handles)
            {
                var d=ProfilerRecorderHandle.GetDescription(handle);
                if(!new[]{"Main Thread","Render Thread","GPU Frame Time","Gfx Used Memory","Total Used Memory","Triangles Count","Draw Calls Count"}.Contains(d.Name)
                    ||Counters.Any(c=>c.name==d.Name))continue;
                var recorder=ProfilerRecorder.StartNew(d.Category,d.Name,1);
                if(recorder.Valid)Counters.Add(new Counter{name=d.Name,unit=d.UnitType.ToString(),recorder=recorder});else recorder.Dispose();
            }
        }
        if(_folder==null||_lastFrame==Time.frameCount||Frames.Count>=4096)return;
        _lastFrame=Time.frameCount;Frames.Add(new Frame{frame=Time.frameCount,values=Counters.Select(c=>c.recorder.LastValue).ToArray()});
    }
    static void Flush()
    {
        if(_folder!=null&&Frames.Count>0)
            File.WriteAllText(Path.Combine(_folder,"editor-performance.json"),JsonConvert.SerializeObject(new{
                scope="Editor capture frames including UI and occasional diagnostic renders; no Android FPS claim. Discard warm-up samples and compare matching profile/phase only.",
                runId=_runId,screen=new[]{_width,_height},counters=Counters.Select(c=>new{c.name,c.unit}).ToArray(),frames=Frames},Formatting.None));
        foreach(var counter in Counters)counter.recorder.Dispose();Counters.Clear();Frames.Clear();_folder=null;_lastFrame=-1;
    }
}
