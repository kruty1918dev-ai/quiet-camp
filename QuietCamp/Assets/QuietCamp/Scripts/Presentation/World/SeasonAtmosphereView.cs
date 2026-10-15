using System;
using System.Collections.Generic;
using System.Linq;
using QuietCamp.Infrastructure;
using Kruty1918.Audio;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace QuietCamp.Presentation.World
{
    // Ticked by CampAtmosphere in game, the workspace in Editor, or the capture boundary.
    public sealed class SeasonAtmosphereView : MonoBehaviour
    {
        public SeasonAtmospherePreset Preset {get;private set;}
        public float WeatherClock=>_clock;
        public Vector2 WindDirection=>new Vector2(Mathf.Cos(Preset.windHeading*Mathf.Deg2Rad),Mathf.Sin(Preset.windHeading*Mathf.Deg2Rad));
        public int LiveParticles=>_systems.Sum(s=>s.particleCount);
        public int OwnedVoices=>_voices.Count(v=>v.IsValid);
        public int PlayingVoices
        {
            get
            {
                var services=QuietCampBootstrap.ServicesRef;
                bool wind=services?.Audio==_audio&&services!=null&&services.AmbientWindHandle.IsValid&&services.AmbientWindHandle.Source.isPlaying;
                return _voices.Count(v=>v.IsValid&&v.Source.isPlaying)+(wind?1:0);
            }
        }
        readonly List<ParticleSystem> _systems=new List<ParticleSystem>();
        readonly List<AudioHandle> _voices=new List<AudioHandle>();
        readonly Dictionary<AudioHandle,float> _voiceGains=new Dictionary<AudioHandle,float>();
        float _audioVisibility=1;
        readonly List<Material> _materials=new List<Material>();
        readonly List<Mesh> _meshes=new List<Mesh>();
        readonly List<Texture2D> _textures=new List<Texture2D>();
        AudioService _audio;
        float _clock,_audioAt;
        Camera _camera;
        CampArtDefinition _art;
        Vector3[] _leafSources;
        Vector3[] _habitats;
        SeasonLightShafts _shafts;
        public int VisibleShafts=>_shafts?.VisibleCount??0;
        public bool ActiveFire=>_fireActive;
        bool _fireActive;Vector3 _firePosition;Light _fireLight;
        ParticleSystem _petals,_dust,_smoke,_embers;
        float _petalAmount,_dustAmount,_smokeAmount,_emberAmount;
        System.Random _random;
        ParticleSystem _rain,_snow,_leaves,_bugs,_splashes,_farSnow,_nearSnow,_drift,_farRain,_foam;
        Vector3[] _wheelContacts=Array.Empty<Vector3>();
        public void SetTents(IEnumerable<TentCloth> tents)=>_surfaceField.RefreshTents(tents,transform.parent);
        Rect _coverage;bool _coverageInitialized;
        public Rect Coverage=>_coverage;
        public float WindStrength{get;private set;}
        VolumeProfile _profile;
        SeasonVisibilityPass _visibility;
        WeatherObjectMotion[] _moving=Array.Empty<WeatherObjectMotion>();
        WeatherSurfaceField _surfaceField;
        readonly ParticleSystem.Particle[] _contacts=new ParticleSystem.Particle[1800];
        readonly Dictionary<MeshFilter,Mesh> _surfaceSources=new Dictionary<MeshFilter,Mesh>();
        readonly Dictionary<MeshRenderer,Material[]> _originalMaterials=new Dictionary<MeshRenderer,Material[]>();
        readonly List<Material> _weatherMaterials=new List<Material>();
        readonly Dictionary<Material,Material> _weatherCopies=new Dictionary<Material,Material>();
        readonly List<Material> _waterMaterials=new List<Material>();
        RiverSurfaceField _riverField;
        RiverEnvironmentProfile _riverProfile;
        ParticleSystem _riverRipples,_riverLeaves,_riverMist,_riverFish,_riverFoam;
        float _riverLeafAmount,_riverMistAmount,_riverFishAmount,_riverFoamAmount,_nextWaterImpact;
        bool _riverPrimed;
        public int RiverParticles=>(_riverRipples?.particleCount??0)+(_riverLeaves?.particleCount??0)+(_riverMist?.particleCount??0)+(_riverFish?.particleCount??0)+(_riverFoam?.particleCount??0);
        public int RiverRipples=>_riverRipples?.particleCount??0;
        public int FloatingLeaves=>_riverLeaves?.particleCount??0;
        public bool OwnsMaterial(Material material)=>_weatherMaterials.Contains(material);
        public void BindRenderer(MeshRenderer renderer)
        {
            var originals=renderer.sharedMaterials;var materials=originals.ToArray();bool changed=false;
            for(int i=0;i<materials.Length;i++)
            {
                bool water=materials[i]!=null&&materials[i].shader.name.IndexOf("Water",StringComparison.OrdinalIgnoreCase)>=0;
                if(materials[i]==null||(!materials[i].HasProperty("_QCWeather")&&!water)||OwnsMaterial(materials[i])||_waterMaterials.Contains(materials[i]))continue;
                if(!_weatherCopies.TryGetValue(materials[i],out var copy)){copy=new Material(materials[i]);_weatherCopies[materials[i]]=copy;_materials.Add(copy);if(water)_waterMaterials.Add(copy);else _weatherMaterials.Add(copy);}
                materials[i]=copy;PlantWeatherResponse.Set(copy,_clock,Preset.wind,Preset.rain);changed=true;
            }
            if(changed){_originalMaterials[renderer]=originals;renderer.sharedMaterials=materials;}
        }
        public void Configure(SeasonAtmospherePreset preset,CampArtDefinition art,Camera camera,Transform parent)
        {
            preset.Validate();Preset=SeasonAtmosphereCatalog.Copy(preset);_art=art;_camera=camera;_random=new System.Random(preset.seed^art.seed);
            transform.SetParent(parent,false);gameObject.name="Сезонні ефекти — "+preset.label;
            var modelMap=art.models.ToDictionary(m=>m.id);
            _leafSources=art.instances.Where(i=>i.modelId!=null&&modelMap.TryGetValue(i.modelId,out var m)&&m.seasonalRole=="deciduous"&&AuthoringHeight(m)*i.scale>3).Select(i=>
            {
                var m=SeasonModelAppearance.Resolve(modelMap[i.modelId],preset);float top=0;for(int j=1;j<m.vertices.Length;j+=3)top=Mathf.Max(top,m.vertices[j]);
                return new Vector3(i.x,i.y+top*(i.scaleXYZ==null?i.scale:i.scaleXYZ[1])*.75f,i.z);
            }).ToArray();
            _habitats=art.instances.Where(i=>i.modelId!=null&&modelMap.TryGetValue(i.modelId,out var m)&&(m.seasonalRole=="groundcover"||m.seasonalRole=="deciduous")&&(Mathf.Abs(i.x)>art.width*.5f+.8f||Mathf.Abs(i.z)>art.height*.5f+.8f)).Where((i,index)=>index%13==0).Select(i=>new Vector3(i.x,i.y,i.z)).ToArray();
            _profile=ScriptableObject.CreateInstance<VolumeProfile>();
            var color=_profile.Add<ColorAdjustments>(true);color.postExposure.Override(preset.exposure);color.contrast.Override(preset.contrast);color.saturation.Override(preset.saturation);
            var white=_profile.Add<WhiteBalance>(true);white.temperature.Override(preset.temperature);
            var bloom=_profile.Add<Bloom>(true);bloom.intensity.Override(preset.bloom);bloom.threshold.Override(1.05f);
            var tonemap=_profile.Add<Tonemapping>(true);tonemap.mode.Override(TonemappingMode.Neutral);
            var vignette=_profile.Add<Vignette>(true);vignette.intensity.Override(.06f);vignette.smoothness.Override(.7f);
            var volume=gameObject.AddComponent<Volume>();volume.isGlobal=true;volume.priority=40;volume.sharedProfile=_profile;
            camera.backgroundColor=CampArtDefinition.Color(preset.skyColor);camera.GetUniversalAdditionalCameraData().renderPostProcessing=true;
            _rain=Pool("Дощ",new Color(.78f,.88f,.94f,.42f),.045f,2,new Vector3(0,-10,0),1200);
            _farRain=Pool("Дальній дощ",new Color(.71f,.8f,.85f,.19f),.025f,2,new Vector3(0,-10,0),650);
            _splashes=Pool("Краплі на землі та воді",new Color(.73f,.84f,.89f,.28f),.13f,.55f,new Vector3(0,.12f,0),35);
            ShapeSplash(_splashes);
            _snow=Pool("Сніг",new Color(.92f,.96f,1,.62f),.11f,9,new Vector3(0,-.8f,0),1800);
            _farSnow=Pool("Дальній сніг",new Color(.82f,.88f,.94f,.38f),.055f,11,new Vector3(0,-.65f,0),650);
            _nearSnow=Pool("Ближні пластівці",new Color(.95f,.97f,1,.23f),.22f,2,_camera.transform.forward*.7f,36);
            _drift=Pool("Низький сніговий перенос",new Color(.83f,.89f,.92f,.13f),1.8f,5,new Vector3(1,.025f,0),100);
            _leaves=Pool("Листя з крон",new Color(.72f,.44f,.12f,.9f),.07f,7,new Vector3(1,-.65f,.4f),100);
            _bugs=Pool("Жучки та світлячки",preset.id.Contains("fireflies")?new Color(4.2f,3.5f,.65f,1):new Color(.83f,.78f,.44f,.8f),.11f,5,new Vector3(.13f,.035f,.06f),60);
            ShapeParticle(_leaves,false);if(!preset.id.Contains("fireflies"))ShapeParticle(_bugs,true);
            _petals=Pool("Весняні пелюстки",new Color(.98f,.86f,.8f,.8f),.13f,8,new Vector3(.1f,-.25f,0),80);ShapeParticle(_petals,false);
            _dust=Pool("Частинки у світлі",new Color(.96f,.88f,.65f,.4f),.075f,7,new Vector3(.06f,.02f,0),200);
            _smoke=Pool("Дим активного вогнища",new Color(.35f,.37f,.34f,.14f),.6f,4,new Vector3(0,.65f,0),64);
            _embers=Pool("Іскри активного вогнища",new Color(2.8f,.7f,.14f,.8f),.06f,1.8f,new Vector3(0,.9f,0),36);
            var fire=new GameObject("Тепле світло активного вогнища");fire.transform.SetParent(transform,false);_fireLight=fire.AddComponent<Light>();_fireLight.type=LightType.Point;_fireLight.color=new Color(1,.45f,.16f);_fireLight.range=3.5f;_fireLight.shadows=LightShadows.None;fire.SetActive(false);
            if(preset.rays>0)_shafts=new SeasonLightShafts(transform,camera,art,preset,_leafSources);
            _visibility=new SeasonVisibilityPass(camera,Preset,()=>gameObject.activeInHierarchy);
            _moving=parent.GetComponentsInChildren<WeatherObjectMotion>();
            _surfaceField=new WeatherSurfaceField(art);
            _wheelContacts=art.instances.Where(i=>i.motion=="waterwheel"&&i.motionPivot!=null).Select(i=>new Vector3(i.x,i.y,i.z)+Quaternion.Euler(0,i.yaw,0)*Vector3.Scale(CampArtDefinition.Vector(i.motionPivot),i.scaleXYZ==null?Vector3.one*i.scale:CampArtDefinition.Vector(i.scaleXYZ))).ToArray();
            _foam=Pool("Кола біля водяного колеса",new Color(.82f,.87f,.8f,.33f),.6f,1.5f,new Vector3(0,0,-.24f),40);ShapeSplash(_foam);var foamMain=_foam.main;foamMain.startSize=.65f;
            _waterY=art.landscape?.meshes.FirstOrDefault(m=>m.water)?.vertices[1]??-.075f;
            _riverProfile=RiverEnvironmentProfile.Load();_riverField=new RiverSurfaceField(art.landscape);
            if(_riverField.reaches.Length>0&&_riverProfile.maxParticles>0)
            {
                int budget=_riverProfile.maxParticles,ripples=budget*4/10,leaves=budget*2/10,mist=budget*2/10,fish=budget/20;
                _riverRipples=Pool("Дощові кола на річці",new Color(.7f,.8f,.76f,.44f),.35f,1.1f,Vector3.zero,ripples);ShapeSplash(_riverRipples);
                var size=_riverRipples.sizeOverLifetime;size.enabled=true;size.size=new ParticleSystem.MinMaxCurve(1,new AnimationCurve(new Keyframe(0,.35f),new Keyframe(1,2.6f)));
                _riverLeaves=Pool("Листя за течією",new Color(.48f,.35f,.17f,.85f),.13f,12,Vector3.zero,leaves);ShapeParticle(_riverLeaves,false);
                _riverMist=Pool("Низький серпанок над водою",new Color(.72f,.79f,.76f,.055f),1.6f,6,new Vector3(0,.012f,0),mist);
                _riverFish=Pool("Рідкісні силуети риб",new Color(.19f,.27f,.24f,.5f),.2f,7,Vector3.zero,fish);ShapeFish(_riverFish);
                _riverFoam=Pool("Локальні хвилі біля каміння й човнів",new Color(.76f,.83f,.76f,.24f),.35f,1.2f,Vector3.zero,budget-ripples-leaves-mist-fish);ShapeSplash(_riverFoam);
            }
            var accents=SeasonSurfaceAccents.Build(art,preset);
            if(accents!=null)
            {
                var node=new GameObject("Seasonal ground accents");node.transform.SetParent(transform,false);node.layer=BoardRenderer.DecorLayer;
                node.AddComponent<MeshFilter>().sharedMesh=accents;_meshes.Add(accents);
                var material=new Material(Resources.Load<Shader>("QuietCamp/CampArtLit"));material.SetColor("_ArtAmbient",CampArtDefinition.Color(preset.ambientColor)*.55f);material.SetFloat("_ArtGrass",1);_materials.Add(material);
                var renderer=node.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;
            }
            var driftMain=_drift.main;driftMain.startSize3D=true;driftMain.startSizeX=2.4f;driftMain.startSizeY=.32f;driftMain.startSizeZ=1;
            RefreshSurfaces();
        }
        // Called after a terrain rebuild, never from the frame loop.
        public void RefreshSurfaces()
        {
            var parent=transform.parent;if(parent==null||Preset==null)return;var preset=Preset;
            foreach(var renderer in parent.GetComponentsInChildren<MeshRenderer>())
                BindRenderer(renderer);
            if(preset.snowCover>0||preset.season=="spring"||preset.season=="autumn")
                foreach(var filter in parent.GetComponentsInChildren<MeshFilter>())
                {
                    var source=filter.sharedMesh;if(_surfaceSources.ContainsKey(filter)||source==null||source.colors.Length!=source.vertexCount)continue;
                    var renderer=filter.GetComponent<MeshRenderer>();if(renderer==null||renderer.sharedMaterial==null||renderer.sharedMaterial.shader.name.IndexOf("Water",StringComparison.OrdinalIgnoreCase)>=0)continue;
                    bool ground=filter.name=="ExteriorMeadow"||filter.name=="MeadowGround"||filter.name.IndexOf("земля",StringComparison.OrdinalIgnoreCase)>=0||filter.name.IndexOf("terrain",StringComparison.OrdinalIgnoreCase)>=0;
                    if(!ground)continue;
                    // CampArtLit owns spatial snow and traffic on these meshes.
                    // Recolouring their vertices here applies snow a second time.
                    if(renderer.sharedMaterials.Any(m=>m!=null&&m.HasProperty("_ArtGround")&&m.GetFloat("_ArtGround")>.5f))continue;
                    var mesh=UnityEngine.Object.Instantiate(source);var colors=mesh.colors;var normals=mesh.normals;
                    var snowColor=QualitySettings.activeColorSpace==ColorSpace.Linear?new Color(.79f,.84f,.85f).linear:new Color(.79f,.84f,.85f);
                    var seasonalColor=preset.season=="spring"?new Color(.39f,.51f,.27f):new Color(.49f,.42f,.27f);
                    if(QualitySettings.activeColorSpace==ColorSpace.Linear)seasonalColor=seasonalColor.linear;
                    for(int i=0;i<colors.Length;i++)
                    {
                        if(preset.snowCover<=0&&colors[i].g>colors[i].r*1.08f)colors[i]=Color.Lerp(colors[i],seasonalColor,preset.season=="spring"?.22f:.38f);
                        colors[i]=Color.Lerp(colors[i],snowColor,preset.snowCover*Mathf.Clamp01(normals[i].y));
                    }
                    mesh.colors=colors;_surfaceSources[filter]=source;filter.sharedMesh=mesh;_meshes.Add(mesh);
                }
        }
        ParticleSystem Pool(string name,Color color,float size,float life,Vector3 speed,int budget)
        {
            var node=new GameObject(name);node.transform.SetParent(transform,false);var s=node.AddComponent<ParticleSystem>();s.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=s.main;main.playOnAwake=false;main.loop=false;main.maxParticles=budget;main.startLifetime=life;main.startSize=size;main.startColor=color;main.startSpeed=0;main.simulationSpace=ParticleSystemSimulationSpace.World;main.useUnscaledTime=true;main.cullingMode=ParticleSystemCullingMode.AlwaysSimulate;
            var emission=s.emission;emission.enabled=false;var shape=s.shape;shape.enabled=false;
            var velocity=s.velocityOverLifetime;velocity.enabled=true;velocity.space=ParticleSystemSimulationSpace.World;velocity.x=speed.x;velocity.y=speed.y;velocity.z=speed.z;
            var renderer=s.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=ParticleMaterial();renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            if(name.Contains("дощ")||name=="Дощ"){renderer.renderMode=ParticleSystemRenderMode.Stretch;renderer.lengthScale=2;renderer.velocityScale=.025f;}
            var fade=s.colorOverLifetime;fade.enabled=true;var g=new Gradient();g.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(1,.15f),new GradientAlphaKey(1,.7f),new GradientAlphaKey(0,1)});fade.color=g;
            s.useAutoRandomSeed=false;s.randomSeed=(uint)_random.Next(1,int.MaxValue);s.Simulate(0,false,true,false);
            _systems.Add(s);return s;
        }
        Material ParticleMaterial()
        {
            var shader=Shader.Find("Universal Render Pipeline/Particles/Unlit");if(shader==null)throw new InvalidOperationException("URP particle shader missing.");
            var m=new Material(shader){renderQueue=3000};m.SetFloat("_Surface",1);m.SetFloat("_Blend",0);m.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);m.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);m.SetFloat("_ZWrite",0);m.SetFloat("_Cull",0);m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            var t=new Texture2D(32,32,TextureFormat.RGBA32,false);var colors=new Color[1024];
            for(int y=0;y<32;y++)for(int x=0;x<32;x++){float r=new Vector2((x-15.5f)/15.5f,(y-15.5f)/15.5f).magnitude;colors[y*32+x]=new Color(1,1,1,Mathf.Clamp01((1-r)*4));}
            t.SetPixels(colors);t.Apply();m.SetTexture("_BaseMap",t);m.SetColor("_BaseColor",Color.white);_textures.Add(t);_materials.Add(m);return m;
        }
        void ShapeParticle(ParticleSystem system,bool butterfly)
        {
            var mesh=new Mesh{name=butterfly?"Два крила":"Осінній листок"};
            mesh.vertices=butterfly?new[]{Vector3.zero,new Vector3(-.7f,.3f,0),new Vector3(-.4f,-.4f,0),Vector3.zero,new Vector3(.7f,.3f,0),new Vector3(.4f,-.4f,0)}:
                new[]{new Vector3(0,.6f,0),new Vector3(-.28f,0,0),new Vector3(0,-.5f,0),new Vector3(0,.6f,0),new Vector3(0,-.5f,0),new Vector3(.28f,0,0)};
            mesh.triangles=Enumerable.Range(0,6).ToArray();mesh.uv=Enumerable.Repeat(new Vector2(.5f,.5f),6).ToArray();mesh.RecalculateNormals();mesh.RecalculateBounds();_meshes.Add(mesh);
            var renderer=system.GetComponent<ParticleSystemRenderer>();renderer.renderMode=ParticleSystemRenderMode.Mesh;renderer.mesh=mesh;
            var rotation=system.rotationOverLifetime;rotation.enabled=true;rotation.separateAxes=true;rotation.x=.6f;rotation.y=1.2f;rotation.z=.4f;
            var main=system.main;main.startSize=butterfly?.12f:.15f;
        }
        void AddVeil(string name,Vector3[] vertices,Color color)
        {
            var go=new GameObject(name);go.transform.SetParent(transform,false);var mesh=new Mesh{name=name};mesh.vertices=vertices;mesh.triangles=new[]{0,1,2,0,2,3};mesh.colors=Enumerable.Repeat(color,4).ToArray();mesh.uv=new[]{Vector2.zero,Vector2.right,Vector2.one,Vector2.up};mesh.RecalculateNormals();mesh.RecalculateBounds();
            _meshes.Add(mesh);go.AddComponent<MeshFilter>().sharedMesh=mesh;var r=go.AddComponent<MeshRenderer>();r.sharedMaterial=ParticleMaterial();r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;
        }
        static float AuthoringHeight(CampArtDefinition.Model m)
        {float min=float.MaxValue,max=float.MinValue;for(int i=1;i<m.vertices.Length;i+=3){min=Mathf.Min(min,m.vertices[i]);max=Mathf.Max(max,m.vertices[i]);}return max-min;}
        public void SetFire(Vector3 position,bool active)
        {_fireActive=active;_firePosition=position;if(_fireLight!=null){_fireLight.transform.position=position+Vector3.up*.55f;_fireLight.gameObject.SetActive(active);}}
        Vector3 Habitat(float height)
        {
            if(_habitats.Length==0){float x=Mathf.Lerp(_coverage.xMin,_coverage.xMax,Next()),z=Mathf.Lerp(_coverage.yMin,_coverage.yMax,Next());return transform.parent.TransformPoint(new Vector3(x,Surface(x,z)+height,z));}
            var p=_habitats[_random.Next(_habitats.Length)];p.x+=(Next()-.5f)*1.5f;p.z+=(Next()-.5f)*1.5f;p.y=(_art.landscape?.SampleHeight(p.x,p.z)??_art.GroundHeight(p.x,p.z))+height;
            return transform.parent.TransformPoint(p);
        }
        float Next()=>(float)_random.NextDouble();
        float _rainAmount,_snowAmount,_leafAmount,_bugAmount,_splashAmount,_farSnowAmount,_nearSnowAmount,_driftAmount,_farRainAmount,_foamAmount;
        float Surface(float x,float z)
        {
            float ground=_art.landscape?.SampleHeight(x,z)??_art.GroundHeight(x,z);
            if(_riverField!=null&&_riverField.TrySample(x,z,out var water)&&water.Wet)ground=Mathf.Max(ground,water.centre.y);
            return _surfaceField?.Height(x,z,ground)??ground;
        }
        float _waterY=-.075f;
        void Emit(ParticleSystem system,ref float accumulated,float rate,float dt,Func<Vector3> position)
        {
            accumulated+=rate*dt;int count=Mathf.Min(64,Mathf.FloorToInt(accumulated));accumulated-=count;
            for(int i=0;i<count;i++)
            {
                var at=position();var local=transform.InverseTransformPoint(at);
                var emit=new ParticleSystem.EmitParams{position=at,randomSeed=(uint)_random.Next(1,int.MaxValue)};
                if(system==_rain||system==_farRain)emit.startLifetime=Mathf.Max(.05f,(local.y-Surface(local.x,local.z))/10);
                if(system==_snow||system==_farSnow){emit.startSize=(system==_snow?.06f:.035f)+Next()*(system==_snow?.16f:.045f);emit.rotation=Next()*360;}
                system.Emit(emit,1);
            }
        }
        public void Advance(float seconds,bool reduced=false,int tier=1)
        {
            if(Preset==null)return;float dt=Mathf.Min(.05f,Mathf.Max(0,seconds));_clock+=dt;float quality=tier==0?.45f:tier==1?1:1.3f;
            float gust=.72f+.28f*Mathf.Sin(_clock*.63f)+.13f*Mathf.Sin(_clock*1.37f);float w=Mathf.Clamp01(Preset.wind*gust);WindStrength=reduced?0:w;
            var direction=new Vector2(Mathf.Cos(Preset.windHeading*Mathf.Deg2Rad),Mathf.Sin(Preset.windHeading*Mathf.Deg2Rad));
            _visibility?.Update(_clock,direction*w,tier);
            _shafts?.Advance(_clock,direction*w,tier,reduced);
            foreach(var moving in _moving)if(moving!=null)moving.Advance(_clock,w,reduced,direction);
            var previousCoverage=_coverage;
            _coverage=WeatherCoverage.Ground(_camera,transform.parent,3+w*6);
            if(!_coverageInitialized||Vector2.Distance(previousCoverage.center,_coverage.center)>2||_coverage.width*_coverage.height>previousCoverage.width*previousCoverage.height*1.12f)PrimeCoverage(previousCoverage,quality);
            _coverageInitialized=true;
            SetFlow(_snow,direction*w*6);SetFlow(_farSnow,direction*w*6);SetFlow(_leaves,direction*w*6);SetFlow(_rain,direction*w*4);SetFlow(_farRain,direction*w*4);SetFlow(_drift,direction*w*6);SetFlow(_petals,direction*w*3);SetFlow(_dust,direction*w*.6f);SetFlow(_smoke,direction*w*1.5f);SetFlow(_embers,direction*w);
            var noise=_snow.noise;noise.enabled=true;noise.strength=.22f;noise.frequency=.18f;noise.scrollSpeed=.25f;
            foreach(var material in _weatherMaterials){PlantWeatherResponse.Set(material,_clock,reduced?0:w,Mathf.Max(Preset.rain,Preset.wetness));material.SetVector("_QCWind",new Vector4(direction.x,direction.y,0,0));}
            foreach(var material in _waterMaterials)
            {if(material.HasProperty("_WaveHeight"))material.SetFloat("_WaveHeight",.012f+w*.02f);if(material.HasProperty("_NormalStrength"))material.SetFloat("_NormalStrength",.25f+w*.15f+Preset.rain*.12f);if(material.HasProperty("_IntersectionRippleStrength"))material.SetFloat("_IntersectionRippleStrength",.08f+Preset.rain*.16f);}
            foreach(var material in _waterMaterials)
                if(material.HasProperty("_Speed"))material.SetFloat("_Speed",reduced?0:(_riverProfile?.flowSpeed??.22f)*(Preset.season=="winter"?.12f:1)*(1+Preset.rain*.15f));
            Vector3 Around(float y)=>transform.parent.TransformPoint(new Vector3(Mathf.Lerp(_coverage.xMin,_coverage.xMax,Next()),y,Mathf.Lerp(_coverage.yMin,_coverage.yMax,Next())));
            float area=Mathf.Clamp(_coverage.width*_coverage.height/600,.65f,3);
            Emit(_rain,ref _rainAmount,Preset.rain*440*quality*area,dt,()=>Around(8+Next()*3));
            Emit(_farRain,ref _farRainAmount,Preset.rain*210*quality*area,dt,()=>Around(7+Next()*4));
            Emit(_splashes,ref _splashAmount,Preset.rain*18*quality,dt,()=>{var p=transform.InverseTransformPoint(Around(0));p.y=Surface(p.x,p.z)+.025f;return transform.TransformPoint(p);});
            if(_wheelContacts.Length>0)Emit(_foam,ref _foamAmount,(3+Preset.wind*3)*quality,dt,()=>{var p=_wheelContacts[_random.Next(_wheelContacts.Length)];p.x+=(Next()-.5f)*.2f;p.y=_waterY+.085f;p.z-=(Next()*.5f);return transform.parent.TransformPoint(p);});
            Emit(_snow,ref _snowAmount,Preset.snow*(Preset.snow>.75f?290:110)*quality*area,dt,()=>Around(5+Next()*5));
            Emit(_farSnow,ref _farSnowAmount,Preset.snow*65*quality*area,dt,()=>Around(7+Next()*4));
            Emit(_nearSnow,ref _nearSnowAmount,Preset.snow*(reduced?0:3)*quality,dt,()=>_camera.ViewportToWorldPoint(new Vector3(Next()<.5f?.04f+Next()*.14f:.82f+Next()*.14f,Next(),.8f)));
            Emit(_drift,ref _driftAmount,Preset.snow*Preset.wind*19*quality,dt,()=>{var p=transform.parent.InverseTransformPoint(Around(0));p.y=Surface(p.x,p.z)+.15f;return transform.parent.TransformPoint(p);});
            if(_leafSources.Length>0)Emit(_leaves,ref _leafAmount,Preset.leaves*(reduced?0:10)*quality,dt,()=>transform.TransformPoint(_leafSources[_random.Next(_leafSources.Length)]+new Vector3((Next()-.5f)*2,Next(),(Next()-.5f)*2)));
            Emit(_bugs,ref _bugAmount,Preset.insects*(reduced?0:10)*quality,dt,()=>Habitat(.65f+Next()*1.5f));
            Emit(_petals,ref _petalAmount,Preset.pollen*(reduced?0:7)*quality,dt,()=>Habitat(2.5f+Next()*2));
            Emit(_dust,ref _dustAmount,Preset.dust*(reduced?0:16)*quality,dt,()=>Habitat(.6f+Next()*3));
            if(_fireActive)
            {
                Emit(_smoke,ref _smokeAmount,5*quality,dt,()=>_firePosition+new Vector3((Next()-.5f)*.12f,.16f,(Next()-.5f)*.12f));
                Emit(_embers,ref _emberAmount,4*quality,dt,()=>_firePosition+new Vector3((Next()-.5f)*.12f,.2f,(Next()-.5f)*.12f));
                _fireLight.intensity=.7f+.13f*Mathf.Sin(_clock*7)+w*.15f;
            }
            foreach(var system in _systems)system.Simulate(dt,false,false,false);
            AdvanceRiver(dt,quality,reduced);
            ClipToSurfaces(_rain);ClipToSurfaces(_farRain);ClipToSurfaces(_snow);ClipToSurfaces(_farSnow);ClearDriftFromBoard();GroundLeaves();
            if(_audio!=null)AdvanceAudio();
        }
        void ClearDriftFromBoard()
        {
            int count=_drift.GetParticles(_contacts);
            for(int i=0;i<count;i++)
            {
                var p=transform.parent.InverseTransformPoint(_contacts[i].position);
                if(Mathf.Abs(p.x)<_art.width*.5f+.5f&&Mathf.Abs(p.z)<_art.height*.5f+.5f)_contacts[i].remainingLifetime=0;
            }
            _drift.SetParticles(_contacts,count);
        }
        void ClipToSurfaces(ParticleSystem system)
        {
            int count=system.GetParticles(_contacts);
            for(int i=0;i<count;i++)
            {
                var local=transform.parent.InverseTransformPoint(_contacts[i].position);
                if(local.y<=Surface(local.x,local.z)+.01f)
                {
                    if((system==_rain||system==_farRain)&&_riverRipples!=null&&_clock>=_nextWaterImpact
                        &&_riverField.TrySample(local.x,local.z,out var water)&&water.Wet&&Surface(local.x,local.z)<=water.centre.y+.025f)
                    {
                        _riverRipples.Emit(new ParticleSystem.EmitParams{position=transform.parent.TransformPoint(new Vector3(local.x,water.centre.y+.04f,local.z))},1);
                        _nextWaterImpact=_clock+1/Mathf.Max(1,_riverProfile.rippleRate*Preset.rain);
                    }
                    _contacts[i].remainingLifetime=0;
                }
            }
            system.SetParticles(_contacts,count);
        }
        void PrimeCoverage(Rect previous,float quality)
        {
            foreach(var pair in new[]{(_rain,Preset.rain),(_farRain,Preset.rain),(_snow,Preset.snow),(_farSnow,Preset.snow)})
            {
                if(pair.Item2<=0)continue;
                var system=pair.Item1;int budget=system.main.maxParticles-system.particleCount;
                int wanted=Mathf.Min(budget,Mathf.RoundToInt(pair.Item2*quality*(system==_rain||system==_farRain?120:220)));
                for(int i=0;i<wanted;i++)
                {
                    float x=Mathf.Lerp(_coverage.xMin,_coverage.xMax,Next()),z=Mathf.Lerp(_coverage.yMin,_coverage.yMax,Next());
                    if(_coverageInitialized&&previous.Contains(new Vector2(x,z)))continue;
                    float y=Surface(x,z)+.3f+Next()*8;
                    var emit=new ParticleSystem.EmitParams{position=transform.parent.TransformPoint(new Vector3(x,y,z)),startLifetime=system==_rain||system==_farRain?(y-Surface(x,z))/10:8,randomSeed=(uint)_random.Next(1,int.MaxValue)};
                    if(system==_snow||system==_farSnow)emit.startSize=(system==_snow?.06f:.035f)+Next()*.08f;
                    system.Emit(emit,1);
                }
            }
        }
        void GroundLeaves()
        {
            int count=_leaves.GetParticles(_contacts);
            for(int i=0;i<count;i++)
            {
                var local=transform.parent.InverseTransformPoint(_contacts[i].position);
                float ground=Surface(local.x,local.z)+.025f;
                if(local.y<ground){local.y=ground;_contacts[i].position=transform.parent.TransformPoint(local);}
            }
            _leaves.SetParticles(_contacts,count);
        }
        void AdvanceRiver(float dt,float quality,bool reduced)
        {
            if(_riverRipples==null)return;
            SetFlow(_riverMist,reduced?Vector2.zero:WindDirection*WindStrength*.18f);
            var rotation=_riverLeaves.rotationOverLifetime;rotation.enabled=!reduced;
            if(!_riverPrimed&&_clock>.2f&&_coverage.width>0)
            {
                _riverPrimed=true;
                float initial=Preset.season=="winter"?0:1;
                EmitRiver(_riverLeaves,ref initial,0,0,.025f);
            }
            float cold=Preset.season=="winter"?.12f:1;
            EmitRiver(_riverLeaves,ref _riverLeafAmount,_riverProfile.leafRate*(Preset.leaves+Preset.pollen*.2f+.025f)*quality*cold,dt,.025f);
            EmitRiver(_riverMist,ref _riverMistAmount,_riverProfile.mistRate*Preset.lowFog*quality,dt,.13f);
            EmitRiver(_riverFish,ref _riverFishAmount,_riverProfile.fishRate*(Preset.season=="summer"?1:.3f)*quality*cold,dt,-.085f);
            FlowRiverParticles(_riverLeaves,.025f,reduced?0:_riverProfile.flowSpeed*cold);
            FlowRiverParticles(_riverFish,-.085f,reduced?0:_riverProfile.flowSpeed*.65f*cold);
            _riverFoamAmount+=dt*quality*cold*1.5f;
            if(_riverFoamAmount>=1)
            {
                _riverFoamAmount-=1;
                if(_riverField.contacts.Length>0)
                {
                    var p=_riverField.contacts[_random.Next(_riverField.contacts.Length)];
                    if(_coverage.Contains(new Vector2(p.x,p.z))&&_riverField.TrySample(p.x,p.z,out var sample))
                        _riverFoam.Emit(new ParticleSystem.EmitParams{position=transform.parent.TransformPoint(p+new Vector3(sample.direction.x*.16f,.025f,sample.direction.y*.16f)),velocity=transform.parent.TransformVector(new Vector3(sample.direction.x,0,sample.direction.y)*.08f)},1);
                }
                foreach(var boat in _moving)
                {
                    if(boat==null||boat.kind!="boat")continue;
                    var p=transform.parent.InverseTransformPoint(boat.transform.position);
                    if(_coverage.Contains(new Vector2(p.x,p.z))&&_riverField.TrySample(p.x,p.z,out var sample)&&sample.Wet)
                        _riverFoam.Emit(new ParticleSystem.EmitParams{position=transform.parent.TransformPoint(new Vector3(p.x,sample.centre.y+.025f,p.z))},1);
                }
            }
        }
        void EmitRiver(ParticleSystem system,ref float accumulator,float rate,float dt,float height)
        {
            accumulator+=rate*dt;
            while(accumulator>=1)
            {
                accumulator-=1;
                if(system.particleCount>=system.main.maxParticles)continue;
                for(int attempt=0;attempt<12;attempt++)
                {
                    var reach=_riverField.reaches[_random.Next(_riverField.reaches.Length)];var station=reach[_random.Next(reach.Length)];
                    float across=(Next()-.5f)*station.halfWidth*1.3f;
                    var point=station.centre+new Vector3(station.side.x,0,station.side.y)*across;
                    if(!_coverage.Contains(new Vector2(point.x,point.z))||Surface(point.x,point.z)>station.centre.y+.035f)continue;
                    point.y+=height;system.Emit(new ParticleSystem.EmitParams{position=transform.parent.TransformPoint(point)},1);break;
                }
            }
        }
        void FlowRiverParticles(ParticleSystem system,float height,float speed)
        {
            int count=system.GetParticles(_contacts);
            for(int i=0;i<count;i++)
            {
                var local=transform.parent.InverseTransformPoint(_contacts[i].position);
                if(!_riverField.TrySample(local.x,local.z,out var water)||!water.Wet){_contacts[i].remainingLifetime=0;continue;}
                // Slow edges hold leaves in sheltered bays; the centre follows the curved channel.
                var flow=_riverField.Velocity(local.x,local.z,speed);
                var velocity=new Vector3(flow.x,0,flow.y);
                local.y=water.centre.y+height;_contacts[i].position=transform.parent.TransformPoint(local);
                _contacts[i].velocity=transform.parent.TransformVector(velocity);
                _contacts[i].rotation3D=new Vector3(system==_riverFish?0:90,Mathf.Atan2(water.direction.x,water.direction.y)*Mathf.Rad2Deg,0);
            }
            system.SetParticles(_contacts,count);
        }
        void ShapeFish(ParticleSystem system)
        {
            var mesh=new Mesh{name="Low-poly river fish silhouette"};
            mesh.vertices=new[]{new Vector3(0,0,.6f),new Vector3(-.15f,0,0),new Vector3(0,0,-.35f),new Vector3(0,0,.6f),new Vector3(0,0,-.35f),new Vector3(.15f,0,0),new Vector3(0,0,-.25f),new Vector3(-.22f,0,-.6f),new Vector3(.22f,0,-.6f)};
            mesh.triangles=Enumerable.Range(0,9).ToArray();mesh.uv=Enumerable.Repeat(new Vector2(.5f,.5f),9).ToArray();mesh.RecalculateNormals();mesh.RecalculateBounds();_meshes.Add(mesh);
            var renderer=system.GetComponent<ParticleSystemRenderer>();renderer.renderMode=ParticleSystemRenderMode.Mesh;renderer.mesh=mesh;
        }
        void ShapeSplash(ParticleSystem system)
        {
            var vertices=new List<Vector3>();var indices=new List<int>();
            for(int i=0;i<12;i++)
            {
                float a=i*Mathf.PI/6,b=(i+1)*Mathf.PI/6;int offset=vertices.Count;
                vertices.Add(new Vector3(Mathf.Cos(a)*.5f,0,Mathf.Sin(a)*.5f));
                vertices.Add(new Vector3(Mathf.Cos(b)*.5f,0,Mathf.Sin(b)*.5f));
                vertices.Add(new Vector3(Mathf.Cos(a)*.37f,0,Mathf.Sin(a)*.37f));
                vertices.Add(new Vector3(Mathf.Cos(b)*.37f,0,Mathf.Sin(b)*.37f));
                indices.AddRange(new[]{offset,offset+1,offset+2,offset+2,offset+1,offset+3});
            }
            var mesh=new Mesh{name="Кільце краплі"};mesh.SetVertices(vertices);mesh.SetTriangles(indices,0);
            mesh.uv=Enumerable.Repeat(new Vector2(.5f,.5f),vertices.Count).ToArray();mesh.RecalculateNormals();mesh.RecalculateBounds();_meshes.Add(mesh);
            var renderer=system.GetComponent<ParticleSystemRenderer>();renderer.renderMode=ParticleSystemRenderMode.Mesh;renderer.mesh=mesh;
            var main=system.main;main.startSize=.3f;
            var grow=system.sizeOverLifetime;grow.enabled=true;grow.size=new ParticleSystem.MinMaxCurve(1,AnimationCurve.Linear(0,.2f,1,1));
        }
        static void SetFlow(ParticleSystem system,Vector2 direction){var velocity=system.velocityOverLifetime;velocity.x=direction.x;velocity.z=direction.y;}
        public void BindAudio(AudioService audio){StopAudio();_audio=audio;_audioAt=0;}
        public void StopAudio(){foreach(var voice in _voices)voice.Stop();_voices.Clear();_voiceGains.Clear();_audio=null;}
        public void SetAudioVisibility(float scale)
        {
            _audioVisibility=Mathf.Clamp01(scale);if(_audio==null)return;
            foreach(var voice in _voices)if(voice.IsValid)_audio.SetPlaybackScale(voice,_audioVisibility*_voiceGains[voice]);
            var services=QuietCampBootstrap.ServicesRef;
            if(services?.Audio==_audio&&services.AmbientWindHandle.IsValid)
            {int index=Array.IndexOf(Preset.audioKeys,"ambience.wind");if(index>=0)_audio.SetPlaybackScale(services.AmbientWindHandle,_audioVisibility*Preset.audioGains[index]);}
        }
        void AdvanceAudio()
        {
            if(_clock<_audioAt)return;_audioAt=_clock+8+Next()*5;
            var catalog=QuietCampAudioCatalog.Load();
            for(int i=0;i<Preset.audioKeys.Length;i++)
            {
                var services=QuietCampBootstrap.ServicesRef;
                if(Preset.audioKeys[i]=="ambience.wind"&&services?.Audio==_audio&&services.AmbientWindHandle.IsValid)
                {_audio.SetPlaybackScale(services.AmbientWindHandle,_audioVisibility*Preset.audioGains[i]);continue;}
                if(!catalog.TryGet(Preset.audioKeys[i],out var definition))continue;
                if(definition.Loop && _voices.Any(h=>h.IsValid&&h.Source.clip==definition.Clip))continue;
                // Ambience follows the game camera: an orthographic camera can be farther
                // from the origin than a bird clip's maximum spatial distance.
                var handle=_audio.Play(Preset.audioKeys[i],new AudioPlayOptions(initialPlaybackScale:_audioVisibility*Preset.audioGains[i],position:_camera.transform.position));
                if(handle.IsValid){_voices.Add(handle);_voiceGains[handle]=Preset.audioGains[i];}
            }
            foreach(var voice in _voices.Where(h=>!h.IsValid).ToArray()){_voices.Remove(voice);_voiceGains.Remove(voice);}
        }
        void OnDestroy()
        {
            _shafts?.Dispose();
            _visibility?.Dispose();_visibility=null;
            foreach(var pair in _originalMaterials)if(pair.Key!=null)pair.Key.sharedMaterials=pair.Value;
            foreach(var pair in _surfaceSources)if(pair.Key!=null)pair.Key.sharedMesh=pair.Value;
            StopAudio();foreach(var m in _materials)DestroyOwned(m);foreach(var m in _meshes)DestroyOwned(m);foreach(var t in _textures)DestroyOwned(t);DestroyOwned(_profile);
        }
        static void DestroyOwned(UnityEngine.Object value){if(value==null)return;if(UnityEngine.Application.isPlaying)Destroy(value);else DestroyImmediate(value);}
    }
}
