using System;
using QuietCamp.Domain;
using QuietCamp.Infrastructure;
using UnityEngine;
using UnityEngine.Rendering;

namespace QuietCamp.Presentation.World
{
    /// <summary>Slow visual weather, advanced by the scene's single atmosphere owner.
    /// Lighting direction, puzzle masks and saved placements never change.</summary>
    public sealed class CampWeather : MonoBehaviour
    {
        public enum Mood { Sunshine, Cloudy, Drizzle }
        ParticleSystem _rain,_pollen,_snow;
        RainImpacts _impacts;
        const int MaximumDrops = 96;
        struct Drop { public uint id; public Vector3 previous; public bool alive, seen; }
        readonly Drop[] _drops = new Drop[MaximumDrops];
        readonly ParticleSystem.Particle[] _particles = new ParticleSystem.Particle[MaximumDrops];
        uint _generation;
        int _dropCursor;
        public int ContactCount { get; private set; }

        Material _material;Texture2D _texture;
        LevelData _level;Camera _camera;float _clock,_emitRain,_emitPollen,_emitSnow;
        public Transform World { get; set; }
        public RainImpacts Impacts => _impacts;
        float _cloud,_wet;bool _night;int _seed;
        string _weatherId;bool _winter;
        public float Snowfall {get;private set;}
        public Mood Current { get; private set; }
        public float Cloudiness=>_cloud;
        public float RainAmount=>_wet;
        public float SunMultiplier=>Mathf.Max(.18f,1-.65f*_cloud-.15f*_wet);
        public int ParticleBudget { get; private set; }
        public void SetNight(bool night)=>_night=night;

        public void Configure(Camera camera,LevelData level,bool night)
        {
            _camera=camera;_level=level;_seed=level.decorSeed;_night=night;
            _weatherId=level.environment?.weatherId;_winter=EnvironmentCompositionData.For(level).seasonId=="winter";
            var initial=CampWeatherTimeline.Initial(_weatherId);_cloud=initial.Cloud;_wet=_winter?0:initial.Rain;
            _texture=CozyParticleAtlas.Create(rainStreak: true);
            _material=CozyParticleMaterial.Create(_texture,0);
            if(_material!=null&&_material.HasProperty("_SoftDistance"))_material.SetFloat("_SoftDistance",.06f);
            _rain=Pool("GentleRain",96,.75f,new Color(.84f,.93f,.93f,.28f));
            // Stretch along world velocity, rather than screen-up: a billboard
            // streak otherwise appears to slide sideways in the isometric view.
            var rainSize=_rain.main;rainSize.loop=true;
            var rainRenderer=_rain.GetComponent<ParticleSystemRenderer>();
            rainRenderer.renderMode=ParticleSystemRenderMode.Stretch;
            rainRenderer.cameraVelocityScale=0;rainRenderer.velocityScale=.025f;rainRenderer.lengthScale=1;
            rainRenderer.freeformStretching=true;rainRenderer.rotateWithStretchDirection=true;
            var rainFade=_rain.colorOverLifetime;var gradient=new Gradient();
            gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},
                new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(1,.08f),new GradientAlphaKey(1,.92f),new GradientAlphaKey(0,1)});
            rainFade.color=gradient;
            var contactObject=new GameObject("RainSurfaceContacts");contactObject.transform.SetParent(transform,false);
            _impacts=contactObject.AddComponent<RainImpacts>();_impacts.Configure();
            _pollen=Pool("SunlitPollen",24,5,new Color(1,.89f,.54f,.45f));
            CozyParticleAtlas.Tile(_rain, 2); CozyParticleAtlas.Tile(_pollen, 1);
            if(_winter)
            {
                _snow=Pool("SlowSnow",36,12,new Color(.91f,.96f,.94f,.56f));
                CozyParticleAtlas.Tile(_snow,1);
            }
        }
        ParticleSystem Pool(string name,int count,float lifetime,Color color)
        {
            var go=new GameObject(name);go.transform.SetParent(transform,false);
            var ps=go.AddComponent<ParticleSystem>();ps.Stop();var main=ps.main;
            main.loop=false;main.playOnAwake=false;main.simulationSpace=ParticleSystemSimulationSpace.World;
            main.startLifetime=lifetime;main.startSpeed=0;main.startSize=.045f;main.startColor=color;main.maxParticles=count;
            var emission=ps.emission;emission.enabled=false;var shape=ps.shape;shape.enabled=false;
            var fade=ps.colorOverLifetime;fade.enabled=true;var gradient=new Gradient();
            gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},
                new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(1,.15f),new GradientAlphaKey(0,1)});fade.color=gradient;
            var renderer=go.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=_material;
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            return ps;
        }
        // Public advance makes the cycle deterministic and independently checkable.
        public void Advance(float seconds,int tier,bool reduced,Vector2 wind)
        {
            using var audit = PerformanceAudit.Measure("QC.CampWeather.Advance");
            if(_level==null)return;
            CozyParticleMaterial.ApplyTier(_material,tier);
            _clock+=Mathf.Max(0,seconds);var target=CampWeatherTimeline.Target(_clock,_seed,_weatherId);
            float ease=1-Mathf.Exp(-Mathf.Max(0,seconds)/5);
            _cloud=Mathf.Lerp(_cloud,target.Cloud,ease);_wet=_winter?0:Mathf.Lerp(_wet,target.Rain,ease);
            Snowfall=_winter?Mathf.Lerp(.16f,.72f,Mathf.Max(_cloud,target.Rain)):0;
            Current=_wet>.2f?Mood.Drizzle:_cloud>.3f?Mood.Cloudy:Mood.Sunshine;
            ParticleBudget=reduced?0:tier==0?12:tier==1?60:96;
            var main=_rain.main;main.maxParticles=Mathf.Max(1,ParticleBudget);
            _impacts.SetQuality(tier,reduced);
            main=_pollen.main;main.maxParticles=tier==0?4:tier==1?12:24;
            Shader.SetGlobalFloat("_CampCloudiness",_cloud);Shader.SetGlobalFloat("_CampWetness",_wet);
            RainSurface.CaptureWind();
            _impacts.Advance(seconds);
            if(reduced)
            {
                _rain.Clear();_pollen.Clear();_snow?.Clear();System.Array.Clear(_drops,0,_drops.Length);
                _emitRain=_emitPollen=_emitSnow=0;return;
            }
            CheckDropContacts();
            _emitRain+=Mathf.Clamp(seconds,0,.05f)*_wet*(tier==0?8:tier==1?40:64);
            _emitPollen+=Mathf.Min(seconds,.05f)*(_night||_winter?0:1-_wet)*(tier==0?.3f:1.7f)*SeasonProfile.For(_level).PollenWeight;
            if(_snow!=null)
            {
                var snowMain=_snow.main;snowMain.maxParticles=tier==0?10:tier==1?24:36;
                _emitSnow+=Mathf.Min(seconds,.05f)*Snowfall*(tier==0?3:tier==1?8:12);
                while(_emitSnow>=1)
                {
                    _emitSnow--;if(_snow.particleCount>=snowMain.maxParticles)continue;
                    if(!_snow.isPlaying)_snow.Play(false);
                    float x=Hash(_clock+_emitSnow+21),z=Hash(_clock*1.23f+_emitSnow+31);
                    var at=new Vector3((x-.5f)*(_level.width+9),4.2f+z,(z-.5f)*(_level.height+9));
                    float fall=.4f+x*.16f;
                    _snow.Emit(new ParticleSystem.EmitParams{position=at,velocity=new Vector3(wind.x*.10f,-fall,wind.y*.10f),
                        startSize=.025f+x*.02f,startLifetime=(at.y-.025f)/fall},1);
                }
            }
            while(_emitRain>=1)
            {
                _emitRain--;
                if(_rain.particleCount>=ParticleBudget)continue;
                float x=Hash(_clock+_emitRain+1),z=Hash(_clock*1.17f+_emitRain+4);
                var position=new Vector3((x-.5f)*(_level.width+6),4.6f+z*.5f,(z-.5f)*(_level.height+6));
                EmitDrop(position,DropVelocity(wind));
            }
            while(_emitPollen>=1)
            {
                if(!_pollen.isPlaying)_pollen.Play(false);
                _emitPollen--;var at=Ground(Hash(_clock),Hash(_clock+13))+Vector3.up*.55f;
                _pollen.Emit(new ParticleSystem.EmitParams{position=at,velocity=new Vector3(wind.x*.08f,.03f,wind.y*.08f),startSize=.025f},1);
            }
        }
        /// <summary>Wind is a bounded visual breeze, not a lateral rain jet.</summary>
        public static Vector3 DropVelocity(Vector2 wind)
        {
            wind=Vector2.ClampMagnitude(wind,1);
            return new Vector3(wind.x*.28f,-4.8f,wind.y*.28f);
        }
        void EmitDrop(Vector3 position,Vector3 velocity)
        {
            int slot=-1;
            for(int n=0;n<MaximumDrops;n++)
            {
                int candidate=(_dropCursor+n)%MaximumDrops;
                if(!_drops[candidate].alive){slot=candidate;break;}
            }
            if(slot<0)return;
            _dropCursor=(slot+1)%MaximumDrops;
            // Encode a slot and generation; Unity keeps randomSeed with the
            // actual particle even when its native pool reorders or discards it.
            _generation=(_generation+1)%40000000;
            uint id=1+(uint)slot+_generation*MaximumDrops;
            _drops[slot]=new Drop{id=id,previous=position,alive=true};
            if(!_rain.isPlaying)_rain.Play(false);
            _rain.Emit(new ParticleSystem.EmitParams{position=position,velocity=velocity,randomSeed=id,
                startSize=.055f,startLifetime=1.5f},1);
        }
        void CheckDropContacts()
        {
            for(int i=0;i<MaximumDrops;i++)_drops[i].seen=false;
            int count=_rain.GetParticles(_particles);bool changed=false;
            for(int i=0;i<count;i++)
            {
                var particle=_particles[i];if(particle.randomSeed==0)continue;
                int slot=(int)((particle.randomSeed-1)%MaximumDrops);
                var drop=_drops[slot];
                if(!drop.alive||drop.id!=particle.randomSeed)continue;
                drop.seen=true;
                float ground=Mathf.Abs(particle.position.x)<_level.width*.5f&&Mathf.Abs(particle.position.z)<_level.height*.5f?.014f:0;
                if(RainSurface.TryCastSegment(drop.previous,particle.position,ground,World,out var contact))
                {
                    // Exactly one mark for this visible drop's first hit. Never
                    // use a timer or emit a second, invisible rain stream.
                    _impacts.Emit(contact,.085f);ContactCount++;
                    particle.remainingLifetime=-1;_particles[i]=particle;changed=true;drop.alive=false;
                }
                drop.previous=particle.position;_drops[slot]=drop;
            }
            if(changed)_rain.SetParticles(_particles,count);
            for(int i=0;i<MaximumDrops;i++)if(!_drops[i].seen)_drops[i].alive=false;
        }
        Vector3 Ground(float x,float z)=>new Vector3((x-.5f)*(_level.width+5),.035f,(z-.5f)*(_level.height+5));
        float Hash(float t)=>Mathf.Repeat(Mathf.Sin(t*12.9898f+_seed)*43758.54f,1);
        void OnDestroy(){if(_material!=null)Destroy(_material);if(_texture!=null)Destroy(_texture);}
    }
}
