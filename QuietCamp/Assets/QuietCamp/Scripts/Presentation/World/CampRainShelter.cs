using Kruty1918.Atmos;
using System.Collections.Generic;
using UnityEngine;

namespace QuietCamp.Presentation.World
{
    /// <summary>Rain quenches scene-owned fire presentation, never the saved
    /// noise/occupancy rules. Interior warmth uses emission without extra lights.</summary>
    public sealed class CampRainShelter : MonoBehaviour
    {
        CampAtmosphere _atmosphere;Transform _world;BoardRenderer _board;FireVisual[] _fires;Vector3[] _scales;
        Light[] _fireLights;float[] _lightIntensity;FireFlicker[] _flickers;
        readonly List<TentCloth> _tents=new List<TentCloth>();Light _rigFire;
        bool _stoppedParticles;
        float _burn=1,_exposure,_warmth,_refresh;
        public bool Extinguished { get; private set; }
        public float FireStrength => _burn;
        public float InteriorWarmth => _warmth;
        public void Configure(CampAtmosphere atmosphere,Transform world,BoardRenderer board=null)
        {
            _atmosphere=atmosphere;_world=world;_board=board;
            _fires=world.GetComponentsInChildren<FireVisual>(true);_scales=new Vector3[_fires.Length];
            for(int i=0;i<_fires.Length;i++)_scales[i]=_fires[i].transform.localScale;
            var lights=new List<Light>();var flickers=new List<FireFlicker>();
            foreach(var fire in _fires){lights.AddRange(fire.GetComponentsInChildren<Light>(true));flickers.AddRange(fire.GetComponentsInChildren<FireFlicker>(true));}
            _fireLights=lights.ToArray();_flickers=flickers.ToArray();_lightIntensity=new float[_fireLights.Length];
            for(int i=0;i<_fireLights.Length;i++)_lightIntensity[i]=_fireLights[i].intensity;
            world.GetComponentsInChildren(true,_tents);
            var host=CampSceneHost.Current;
            if(host!=null&&host.Atmosphere==atmosphere)
                foreach(var light in host.gameObject.scene.GetRootGameObjects())
                    if(light.name=="LightingRoot")_rigFire=light.transform.Find("FireLight")?.GetComponent<Light>();
        }
        public void Advance(float seconds,float rain)
        {
            if(_world==null)return;
            float dt=Mathf.Max(0,seconds);
            if(rain>.15f&&!Extinguished&&_fires.Length>0){_exposure+=dt*rain;if(_exposure>=1.2f)Extinguished=true;}
            float target=Extinguished?0:1;
            _burn=Mathf.MoveTowards(_burn,target,dt/1.4f);
            float warmTarget=Extinguished?.95f:Mathf.Max(_atmosphere?.PhaseId=="night"?.45f:0,Mathf.Clamp01(rain*.8f));
            _warmth=Mathf.MoveTowards(_warmth,warmTarget,dt/2.5f);
            for(int i=0;i<_fires.Length;i++)
            {
                var fire=_fires[i];if(fire==null)continue;
                if(_burn<=.001f){fire.SetBurning(false);continue;}
                if(fire.gameObject.activeSelf)fire.transform.localScale=_scales[i]*Mathf.Lerp(.12f,1,_burn);
            }
            if(Extinguished&&_rigFire!=null)_rigFire.intensity=Mathf.MoveTowards(_rigFire.intensity,0,dt*2);
            if(Extinguished)
            {
                foreach(var flicker in _flickers)if(flicker!=null)flicker.enabled=false;
                for(int i=0;i<_fireLights.Length;i++)if(_fireLights[i]!=null)_fireLights[i].intensity=_lightIntensity[i]*_burn;
            }
            if(Extinguished&&!_stoppedParticles){_stoppedParticles=true;_atmosphere.SetFire(Vector3.zero,false);}
            _refresh-=dt;
            if(_refresh<=0)
            {
                _refresh=.5f;var board=_board;
                if(board==null)_world.GetComponentsInChildren(true,_tents);
                else{_tents.Clear();foreach(var presenter in board.Tents.Values){var cloth=presenter.Root.GetComponent<TentCloth>();if(cloth!=null)_tents.Add(cloth);}}
                _atmosphere?.SeasonalWeather?.SetTents(_tents);
            }
            foreach(var tent in _tents)if(tent!=null)
            {
                tent.SetShelterWarmth(_warmth);
                var seasonal=_atmosphere?.SeasonalWeather;
                if(seasonal!=null)tent.ApplyClimate(seasonal.Preset.season,seasonal.Preset.wetness,seasonal.Preset.snowCover);
                tent.AdvanceSurface(dt,rain,seasonal?.Preset.snow??_atmosphere?.Weather?.Snowfall??0,Shader.GetGlobalFloat("_AtmosWindStrength"));
            }
        }
    }
}
