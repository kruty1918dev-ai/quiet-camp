using System;
using System.Diagnostics;
using UnityEngine;

namespace QuietCamp.Presentation.UI
{
    /// <summary>Allocation-free CPU attribution, including Editors that compile out Profiler.BeginSample.
    /// CPU-only: excludes native Canvas batching/mesh upload and GPU work.</summary>
    public sealed class RoadmapWorkMetrics
    {
        public const int Frame=0,Activation=1,Glade=2,Background=3,Controls=4;
        readonly int[] _frames={-1,-1,-1,-1,-1};
        readonly double[] _nanoseconds=new double[5];
        readonly long[] _allocations=new long[5];
        static readonly double NanosecondsPerTick=1000000000d/Stopwatch.Frequency;
        public Scope Measure(int kind)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            return new Scope(this,kind);
#else
            return default;
#endif
        }
        public double Nanoseconds(int kind,int frame)=>_frames[kind]==frame?_nanoseconds[kind]:0;
        public long Allocated(int frame)
        {long bytes=0;for(int i=0;i<5;i++)if(_frames[i]==frame)bytes+=_allocations[i];return bytes;}
        void End(int kind,long start,long bytes)
        {
            int frame=Time.frameCount;if(_frames[kind]!=frame){_frames[kind]=frame;_nanoseconds[kind]=0;_allocations[kind]=0;}
            _nanoseconds[kind]+=(Stopwatch.GetTimestamp()-start)*NanosecondsPerTick;
            _allocations[kind]+=GC.GetAllocatedBytesForCurrentThread()-bytes;
        }
        public readonly struct Scope : IDisposable
        {
            readonly RoadmapWorkMetrics _owner;readonly int _kind;readonly long _start,_bytes;
            internal Scope(RoadmapWorkMetrics owner,int kind)
            {_owner=owner;_kind=kind;_bytes=GC.GetAllocatedBytesForCurrentThread();_start=Stopwatch.GetTimestamp();}
            public void Dispose()=>_owner?.End(_kind,_start,_bytes);
        }
    }
}
