using System;

namespace QuietCamp.Application
{
    /// <summary>Reserve every cache hit before recycling misses. Fixed storage, no per-frame allocation.</summary>
    public sealed class RoadmapSlotPlan
    {
        readonly int[] _keys, _requested, _slots;
        readonly bool[] _reserved;
        public int Count { get; private set; }
        public RoadmapSlotPlan(int capacity)
        {
            if(capacity<1)throw new ArgumentOutOfRangeException(nameof(capacity));
            _keys=new int[capacity];_requested=new int[capacity];_slots=new int[capacity];_reserved=new bool[capacity];
            for(int i=0;i<capacity;i++)_keys[i]=int.MinValue;
        }
        public void Begin(){Count=0;Array.Clear(_reserved,0,_reserved.Length);}
        public void Request(int key)
        {
            if(key==int.MinValue)throw new ArgumentOutOfRangeException(nameof(key));
            for(int i=0;i<Count;i++)if(_requested[i]==key)return;
            if(Count==_requested.Length)throw new InvalidOperationException("Roadmap slot budget exhausted");
            _requested[Count++]=key;
        }
        public void Resolve()
        {
            for(int r=0;r<Count;r++)
            {
                _slots[r]=-1;
                for(int s=0;s<_keys.Length;s++)if(_keys[s]==_requested[r]){_slots[r]=s;_reserved[s]=true;break;}
            }
            for(int r=0;r<Count;r++)if(_slots[r]<0)
                for(int s=0;s<_keys.Length;s++)if(!_reserved[s])
                {_slots[r]=s;_keys[s]=_requested[r];_reserved[s]=true;break;}
        }
        public int KeyAt(int request)=>_requested[request];
        public int SlotAt(int request)=>_slots[request];
    }
}
