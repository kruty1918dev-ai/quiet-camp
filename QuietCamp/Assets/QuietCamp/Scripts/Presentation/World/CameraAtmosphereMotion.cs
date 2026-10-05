using System;
using QuietCamp.Infrastructure;
using UnityEngine;

namespace QuietCamp.Presentation.World
{
    /// <summary>Small camera response layered over the fitted pose. Placement freezes the exact rendered pose;
    /// the owner never reframes the board or feeds its visual offsets into gameplay data.</summary>
    [DefaultExecutionOrder(450)]
    public sealed class CameraAtmosphereMotion : MonoBehaviour
    {
        Camera _camera;Func<WindSim.Snapshot> _wind;Func<bool> _reduced,_suppressed;
        Vector3 _basePosition,_lastPosition,_offset,_velocity,_impulse;
        Quaternion _baseRotation,_lastRotation;
        Vector2 _angles,_angleVelocity;
        bool _hasWritten;
        public Vector3 VisualOffset=>_offset;
        public void Configure(Camera camera,Func<WindSim.Snapshot> wind,Func<bool> reduced,Func<bool> suppressed)
        {
            _camera=camera;_wind=wind;_reduced=reduced;_suppressed=suppressed;
            _basePosition=camera.transform.position;_baseRotation=camera.transform.rotation;_hasWritten=false;
        }
        public void Impulse(float strength,Vector3 direction)
        {
            if(_reduced?.Invoke()==true||_suppressed?.Invoke()==true)return;
            _impulse+=Vector3.ClampMagnitude(direction,1)*Mathf.Clamp(strength,0,.035f);
            _impulse=Vector3.ClampMagnitude(_impulse,.045f);
        }
        void LateUpdate()
        {
            if(_camera==null)return;
            if(_hasWritten&&(_camera.transform.position!=_lastPosition||_camera.transform.rotation!=_lastRotation))
            { _basePosition=_camera.transform.position;_baseRotation=_camera.transform.rotation;_offset=Vector3.zero;_angles=Vector2.zero; }
            if(_suppressed?.Invoke()==true)
            { _lastPosition=_camera.transform.position;_lastRotation=_camera.transform.rotation;_hasWritten=true;return; }
            bool reduced=_reduced?.Invoke()==true;var wind=_wind?.Invoke()??default;
            float dt=Mathf.Min(.05f,Time.unscaledDeltaTime),t=wind.PhaseSeconds;
            float gust=wind.Strength*(.6f+.4f*wind.GustEnvelope);
            var horizontal=new Vector3(wind.DirectionXZ.x,0,wind.DirectionXZ.y);
            var right=_baseRotation*Vector3.right;float lean=Vector3.Dot(horizontal,right);
            var windOffset=Vector3.ClampMagnitude(horizontal*(Mathf.Sin(t*.67f)*.025f*gust)+right*(Mathf.Sin(t*.29f+1.3f)*.013f*gust),.02f);
            var target=reduced?Vector3.zero:windOffset+_impulse;
            var targetAngles=reduced?Vector2.zero:new Vector2(Mathf.Sin(t*.45f)*.07f*gust,Mathf.Sin(t*.67f)*.10f*gust*lean);
            _impulse=Vector3.Lerp(_impulse,Vector3.zero,1-Mathf.Exp(-dt*4));
            _offset=Vector3.SmoothDamp(_offset,target,ref _velocity,.45f,.4f,dt);
            _angles=Vector2.SmoothDamp(_angles,targetAngles,ref _angleVelocity,.55f,1,dt);
            _camera.transform.SetPositionAndRotation(_basePosition+_offset,_baseRotation*Quaternion.Euler(_angles.x,0,_angles.y));
            _lastPosition=_camera.transform.position;_lastRotation=_camera.transform.rotation;_hasWritten=true;
        }
        void OnDisable()
        {
            if(_camera!=null&&_hasWritten&&_camera.transform.position==_lastPosition)
                _camera.transform.SetPositionAndRotation(_basePosition,_baseRotation);
            _hasWritten=false;_offset=Vector3.zero;_angles=Vector2.zero;
        }
    }
}
