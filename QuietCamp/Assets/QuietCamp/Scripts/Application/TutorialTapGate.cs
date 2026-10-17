namespace QuietCamp.Application
{
    /// <summary>A new, short tap after the reading delay; old presses, swipes and holds cannot dismiss a lesson.</summary>
    public sealed class TutorialTapGate
    {
        readonly double _readyAt;
        readonly float _movement;
        int? _pointer;
        float _x,_y;
        double _pressedAt;
        public TutorialTapGate(double shownAt,float movement,float delay=1f)
        { _readyAt=shownAt+delay;_movement=movement; }
        public bool Ready(double now)=>now>=_readyAt;
        public void Press(double now,int pointer,float x,float y)
        {
            _pointer=null;
            if(!Ready(now))return;
            _pointer=pointer;_x=x;_y=y;_pressedAt=now;
        }
        public void Move(int pointer,float x,float y)
        {
            if(_pointer!=pointer)return;
            if((x-_x)*(x-_x)+(y-_y)*(y-_y)>_movement*_movement)_pointer=null;
        }
        public bool Release(double now,int pointer,float x,float y)
        {
            Move(pointer,x,y);
            bool accepted=_pointer==pointer&&now-_pressedAt<=.6&&Ready(now);
            _pointer=null;return accepted;
        }
        public void Cancel()=>_pointer=null;
    }
}
