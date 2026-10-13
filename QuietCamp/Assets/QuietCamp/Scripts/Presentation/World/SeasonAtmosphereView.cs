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
        System.Random _random;
        ParticleSystem _rain,_snow,_leaves,_bugs,_splashes;
        VolumeProfile _profile;
        readonly Dictionary<MeshFilter,Mesh> _surfaceSources=new Dictionary<MeshFilter,Mesh>();
        readonly Dictionary<MeshRenderer,Material[]> _originalMaterials=new Dictionary<MeshRenderer,Material[]>();
        readonly List<Material> _weatherMaterials=new List<Material>();
        readonly Dictionary<Material,Material> _weatherCopies=new Dictionary<Material,Material>();
        readonly List<Material> _waterMaterials=new List<Material>();
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
            _leafSources=art.instances.Where(i=>i.modelId!=null&&modelMap.TryGetValue(i.modelId,out var m)&&m.seasonalRole=="deciduous").Select(i=>
            {
                var m=modelMap[i.modelId];float top=0;for(int j=1;j<m.vertices.Length;j+=3)top=Mathf.Max(top,m.vertices[j]);
                return new Vector3(i.x,i.y+top*(i.scaleXYZ==null?i.scale:i.scaleXYZ[1])*.75f,i.z);
            }).ToArray();
            _profile=ScriptableObject.CreateInstance<VolumeProfile>();
            var color=_profile.Add<ColorAdjustments>(true);color.postExposure.Override(preset.exposure);color.contrast.Override(preset.contrast);color.saturation.Override(preset.saturation);
            var white=_profile.Add<WhiteBalance>(true);white.temperature.Override(preset.temperature);
            var bloom=_profile.Add<Bloom>(true);bloom.intensity.Override(preset.bloom);bloom.threshold.Override(1.05f);
            var tonemap=_profile.Add<Tonemapping>(true);tonemap.mode.Override(TonemappingMode.Neutral);
            var vignette=_profile.Add<Vignette>(true);vignette.intensity.Override(.06f);vignette.smoothness.Override(.7f);
            var volume=gameObject.AddComponent<Volume>();volume.isGlobal=true;volume.priority=40;volume.sharedProfile=_profile;
            camera.backgroundColor=CampArtDefinition.Color(preset.skyColor);camera.GetUniversalAdditionalCameraData().renderPostProcessing=true;
            _rain=Pool("Дощ",new Color(.78f,.88f,.94f,.35f),.028f,1.6f,new Vector3(0,-10,0),350);
            _splashes=Pool("Краплі на землі та воді",new Color(.73f,.84f,.89f,.28f),.13f,.55f,new Vector3(0,.12f,0),35);
            _snow=Pool("Сніг",new Color(.92f,.96f,1,preset.snow>.75f?.48f:.7f),preset.snow>.75f?.105f:.075f,6,new Vector3(1,-1.25f,0),260);
            if(preset.snow>.75f){var renderer=_snow.GetComponent<ParticleSystemRenderer>();renderer.renderMode=ParticleSystemRenderMode.Stretch;renderer.lengthScale=.8f;renderer.velocityScale=.06f;}
            _leaves=Pool("Листя з крон",new Color(.72f,.44f,.12f,.9f),.07f,7,new Vector3(1,-.65f,.4f),100);
            _bugs=Pool("Жучки та світлячки",preset.id.Contains("fireflies")?new Color(4.2f,3.5f,.65f,1):new Color(.83f,.78f,.44f,.8f),.11f,5,new Vector3(.13f,.035f,.06f),60);
            ShapeParticle(_leaves,false);if(!preset.id.Contains("fireflies"))ShapeParticle(_bugs,true);
            if(preset.rays>0)CreateRays(art,preset);
            if(preset.fog>0)CreateHaze(art,preset);
            RefreshSurfaces();
        }
        // Called after a terrain rebuild, never from the frame loop.
        public void RefreshSurfaces()
        {
            var parent=transform.parent;if(parent==null||Preset==null)return;var preset=Preset;
            foreach(var renderer in parent.GetComponentsInChildren<MeshRenderer>())
                BindRenderer(renderer);
            if(preset.snowCover>0)
                foreach(var filter in parent.GetComponentsInChildren<MeshFilter>())
                {
                    var source=filter.sharedMesh;if(_surfaceSources.ContainsKey(filter)||source==null||source.colors.Length!=source.vertexCount)continue;
                    var renderer=filter.GetComponent<MeshRenderer>();if(renderer==null||renderer.sharedMaterial==null||renderer.sharedMaterial.shader.name.IndexOf("Water",StringComparison.OrdinalIgnoreCase)>=0)continue;
                    bool ground=filter.name=="ExteriorMeadow"||filter.name.IndexOf("земля",StringComparison.OrdinalIgnoreCase)>=0||filter.name.IndexOf("terrain",StringComparison.OrdinalIgnoreCase)>=0;
                    if(!ground)continue;
                    var mesh=UnityEngine.Object.Instantiate(source);var colors=mesh.colors;var normals=mesh.normals;
                    var snowColor=QualitySettings.activeColorSpace==ColorSpace.Linear?new Color(.79f,.84f,.85f).linear:new Color(.79f,.84f,.85f);
                    for(int i=0;i<colors.Length;i++)colors[i]=Color.Lerp(colors[i],snowColor,preset.snowCover*Mathf.Clamp01(normals[i].y));
                    mesh.colors=colors;_surfaceSources[filter]=source;filter.sharedMesh=mesh;_meshes.Add(mesh);
                }
        }
        ParticleSystem Pool(string name,Color color,float size,float life,Vector3 speed,int budget)
        {
            var node=new GameObject(name);node.transform.SetParent(transform,false);var s=node.AddComponent<ParticleSystem>();s.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=s.main;main.playOnAwake=false;main.loop=false;main.maxParticles=budget;main.startLifetime=life;main.startSize=size;main.startColor=color;main.startSpeed=0;main.simulationSpace=ParticleSystemSimulationSpace.World;main.useUnscaledTime=true;
            var emission=s.emission;emission.enabled=false;var shape=s.shape;shape.enabled=false;
            var velocity=s.velocityOverLifetime;velocity.enabled=true;velocity.space=ParticleSystemSimulationSpace.World;velocity.x=speed.x;velocity.y=speed.y;velocity.z=speed.z;
            var renderer=s.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=ParticleMaterial();renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            if(name=="Дощ"){renderer.renderMode=ParticleSystemRenderMode.Stretch;renderer.lengthScale=2;renderer.velocityScale=.025f;}
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
        void CreateRays(CampArtDefinition art,SeasonAtmospherePreset preset)
        {
            var direction=Quaternion.Euler(CampArtDefinition.Vector(preset.sunEuler))*Vector3.forward;
            foreach(var centre in _leafSources.Where(p=>Mathf.Abs(p.x)>art.width*.5f+1).Take(5))
            {
                var upper=centre+Vector3.up*2;var lower=upper+direction*8;var side=Vector3.Cross(_camera.transform.forward,direction).normalized;
                var quad=new[]{upper-side*.05f,upper+side*.05f,lower+side*.35f,lower-side*.35f};
                var inverse=Quaternion.Inverse(_camera.transform.rotation);var points=quad.Select(p=>inverse*p).ToArray();
                bool covers=false;for(float x=-art.width*.5f;x<=art.width*.5f;x+=1)for(float z=-art.height*.5f;z<=art.height*.5f;z+=1)
                {var cell=inverse*new Vector3(x,.5f,z);if(cell.x>=points.Min(p=>p.x)&&cell.x<=points.Max(p=>p.x)&&cell.y>=points.Min(p=>p.y)&&cell.y<=points.Max(p=>p.y))covers=true;}
                if(!covers)AddVeil("Промінь між деревами",quad,new Color(1,.89f,.61f,preset.rays*.08f));
            }
        }
        void CreateHaze(CampArtDefinition art,SeasonAtmospherePreset preset)
        {
            // Veils sit behind the puzzle, never between the camera and its cells.
            var away=Vector3.ProjectOnPlane(_camera.transform.forward,Vector3.up).normalized;var right=_camera.transform.right;
            for(int i=0;i<3;i++)
            {
                var centre=away*(Mathf.Max(art.width,art.height)*.5f+5+i*5);
                var c=CampArtDefinition.Color(preset.skyColor);c.a=preset.fog*.12f;
                AddVeil("Далека імла",new[]{centre-right*20,centre+right*20,centre+right*20+Vector3.up*8,centre-right*20+Vector3.up*8},c);
            }
        }
        float Next()=>(float)_random.NextDouble();
        float _rainAmount,_snowAmount,_leafAmount,_bugAmount,_splashAmount;
        float Surface(float x,float z)=>Mathf.Max(_art.landscape?.SampleHeight(x,z)??_art.GroundHeight(x,z),_art.landscape?.meshes.FirstOrDefault(m=>m.water)?.vertices[1]??-.02f);
        void Emit(ParticleSystem system,ref float accumulated,float rate,float dt,Func<Vector3> position)
        {
            accumulated+=rate*dt;int count=Mathf.Min(12,Mathf.FloorToInt(accumulated));accumulated-=count;
            for(int i=0;i<count;i++)
            {
                var at=position();var local=transform.InverseTransformPoint(at);
                var emit=new ParticleSystem.EmitParams{position=at,randomSeed=(uint)_random.Next(1,int.MaxValue)};
                if(system==_rain)emit.startLifetime=Mathf.Max(.05f,(local.y-Surface(local.x,local.z))/10);
                system.Emit(emit,1);
            }
        }
        public void Advance(float seconds,bool reduced=false,int tier=1)
        {
            if(Preset==null)return;float dt=Mathf.Min(.05f,Mathf.Max(0,seconds));_clock+=dt;float quality=tier==0?.45f:1;
            float w=Preset.wind;var v=_snow.velocityOverLifetime;v.x=(1+w*3);v.z=w;
            foreach(var material in _weatherMaterials)PlantWeatherResponse.Set(material,_clock,reduced?0:w,Preset.rain);
            foreach(var material in _waterMaterials)
            {if(material.HasProperty("_WaveHeight"))material.SetFloat("_WaveHeight",.02f+w*.035f);if(material.HasProperty("_NormalStrength"))material.SetFloat("_NormalStrength",.35f+w*.15f+Preset.rain*.14f);}
            Vector3 Around(float y)=>transform.TransformPoint(new Vector3((Next()-.5f)*18,y,(Next()-.5f)*18));
            Emit(_rain,ref _rainAmount,Preset.rain*90*quality,dt,()=>Around(8));
            Emit(_splashes,ref _splashAmount,Preset.rain*18*quality,dt,()=>{var p=transform.InverseTransformPoint(Around(0));p.y=Surface(p.x,p.z)+.025f;return transform.TransformPoint(p);});
            Emit(_snow,ref _snowAmount,Preset.snow*(Preset.snow>.75f?65:32)*quality,dt,()=>Around(7));
            if(_leafSources.Length>0)Emit(_leaves,ref _leafAmount,Preset.leaves*(reduced?0:4)*quality,dt,()=>transform.TransformPoint(_leafSources[_random.Next(_leafSources.Length)]+new Vector3((Next()-.5f)*2,Next(),(Next()-.5f)*2)));
            Emit(_bugs,ref _bugAmount,Preset.insects*(reduced?0:6)*quality,dt,()=>Around(.5f+Next()*1.3f));
            foreach(var system in _systems)system.Simulate(dt,false,false,false);
            if(_audio!=null)AdvanceAudio();
        }
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
            foreach(var pair in _originalMaterials)if(pair.Key!=null)pair.Key.sharedMaterials=pair.Value;
            foreach(var pair in _surfaceSources)if(pair.Key!=null)pair.Key.sharedMesh=pair.Value;
            StopAudio();foreach(var m in _materials)DestroyOwned(m);foreach(var m in _meshes)DestroyOwned(m);foreach(var t in _textures)DestroyOwned(t);DestroyOwned(_profile);
        }
        static void DestroyOwned(UnityEngine.Object value){if(value==null)return;if(UnityEngine.Application.isPlaying)Destroy(value);else DestroyImmediate(value);}
    }
}
