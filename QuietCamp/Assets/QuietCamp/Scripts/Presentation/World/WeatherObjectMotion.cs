using QuietCamp.Infrastructure;
using UnityEngine;

namespace QuietCamp.Presentation.World
{
    public sealed class WeatherObjectMotion:MonoBehaviour
    {
        public string kind;
        public Vector3 pivot;
        Vector3 _position;
        Quaternion _rotation;
        bool _ready;float _lastClock,_angle;
        public void Configure(CampArtDefinition.Instance item)
        {kind=item.motion;pivot=item.motionPivot==null?Vector3.zero:CampArtDefinition.Vector(item.motionPivot);_position=transform.localPosition;_rotation=transform.localRotation;_ready=true;}
        public void Advance(float clock,float wind,bool reduced,Vector2 direction=default)
        {
            if(!_ready)return;float strength=reduced?0:1;
            if(kind=="waterwheel")
            {
                float delta=Mathf.Max(0,clock-_lastClock);if(clock<_lastClock)_angle=0;
                _angle=Mathf.Repeat(_angle+delta*(7+wind*5)*strength,360);_lastClock=clock;
                var rotation=Quaternion.AngleAxis(_angle*strength,Vector3.right);
                transform.localRotation=_rotation*rotation;
                var scaledPivot=Vector3.Scale(pivot,transform.localScale);
                transform.localPosition=_position+_rotation*(scaledPivot-rotation*scaledPivot);
            }
            else if(kind=="cloth")
            {
                if(direction.sqrMagnitude<.01f)direction=Vector2.right;
                var localWind=Quaternion.Inverse(_rotation)*new Vector3(direction.x,0,direction.y);
                var axis=Vector3.Cross(Vector3.up,localWind).normalized;
                var rotation=Quaternion.AngleAxis((.55f+.45f*Mathf.Sin(clock*1.7f+pivot.x*2))*wind*14*strength,axis);
                var scaledPivot=Vector3.Scale(pivot,transform.localScale);
                transform.localRotation=_rotation*rotation;transform.localPosition=_position+_rotation*(scaledPivot-rotation*scaledPivot);
            }
            else if(kind=="boat")
            {
                transform.localPosition=_position+Vector3.up*Mathf.Sin(clock*1.1f)*(.012f+wind*.035f)*strength;
                transform.localRotation=_rotation*Quaternion.Euler(Mathf.Sin(clock*.8f)*(.6f+wind*2)*strength,0,Mathf.Sin(clock*1.3f)*(.5f+wind*1.8f)*strength);
            }
        }
        void OnDisable(){if(_ready){transform.localPosition=_position;transform.localRotation=_rotation;}}
    }
}
