using System;
using System.Collections.Generic;
using QuietCamp.Domain;
using QuietCamp.Infrastructure;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace QuietCamp.Presentation.World
{
    public sealed class LivingForest : MonoBehaviour
    {
        sealed class Bird { public Transform root,left,right; public Vector3 from,to; public float age,delay; public bool flying,called; }
        public event Action<Transform> BirdPassing;
        public void SetPhase(string phase) => _day=phase=="noon"||phase=="morning";
        readonly List<Bird> _birds=new List<Bird>();readonly List<GameObject> _fog=new List<GameObject>();readonly List<Material> _mats=new List<Material>();
        readonly List<Texture2D> _textures=new List<Texture2D>();
        Camera _camera;
        CampAtmosphere _atmosphere;LevelData _level;Func<bool> _reduced;int _tier;ParticleSystem _leaves,_dust;float _clock;bool _day;
        public int BirdCount=>_birds.Count;
        public int FogCount=>_tier==0?0:Mathf.Min(_tier+1,_fog.Count);
        public int FlyingBirdCount { get { int count=0;foreach(var bird in _birds)if(bird.root.gameObject.activeSelf)count++;return count; } }
        public void Configure(CampAtmosphere atmosphere,Camera camera,LevelData level,AtmosphereCatalog.Profile profile,Func<bool> reduced,int tier)
        {
            _camera=camera;_atmosphere=atmosphere;_level=level;_reduced=reduced;_tier=tier;_day=profile.Id=="noon"||profile.Id=="morning";_clock=level.decorSeed%17;
            atmosphere.GustStarted+=Gust;
            var shader=Shader.Find("QuietCamp/ForestVolume");
            if(shader!=null && shader.isSupported)
            {
                camera.GetUniversalAdditionalCameraData().requiresDepthTexture=tier>0;
                for(int i=0;i<3;i++)
                {
                    var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name="LocalForestMist";Destroy(go.GetComponent<Collider>());go.transform.SetParent(transform,false);
                    go.transform.position=new Vector3(-level.width*.5f-2-i*.9f,.8f,-level.height*.5f+1+i*2);
                    go.transform.localScale=new Vector3(3.2f,1.5f,3.6f);
                    var mat=new Material(shader);mat.SetColor("_FogColor",Color.Lerp(profile.Ambient,SeasonPalette.For(level).Fog,.55f));mat.SetFloat("_Density",.08f);mat.SetFloat("_Steps",tier>=2?12:8);_mats.Add(mat);
                    go.GetComponent<Renderer>().sharedMaterial=mat;go.GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.Off;_fog.Add(go);
                }

            }
            var birdMaterial=new Material(Shader.Find("Universal Render Pipeline/Simple Lit"));birdMaterial.SetColor("_BaseColor",new Color(.21f,.24f,.20f));_mats.Add(birdMaterial);
            for(int i=0;i<4;i++)
            {
                var root=new GameObject("ForestBird").transform;root.SetParent(transform,false);root.gameObject.SetActive(false);
                var body=GameObject.CreatePrimitive(PrimitiveType.Sphere);Destroy(body.GetComponent<Collider>());body.transform.SetParent(root,false);body.transform.localScale=new Vector3(.1f,.08f,.2f);body.GetComponent<Renderer>().sharedMaterial=birdMaterial;
                Transform Wing(string name,float side){var go=GameObject.CreatePrimitive(PrimitiveType.Cube);Destroy(go.GetComponent<Collider>());go.name=name;go.transform.SetParent(root,false);go.transform.localPosition=new Vector3(side*.11f,0,0);go.transform.localScale=new Vector3(.24f,.015f,.11f);go.GetComponent<Renderer>().sharedMaterial=birdMaterial;return go.transform;}
                float delay=4+i*4.7f+level.decorSeed%5;
                _birds.Add(new Bird{root=root,left=Wing("LeftWing",-1),right=Wing("RightWing",1),age=-delay,delay=delay});
                foreach(var renderer in root.GetComponentsInChildren<Renderer>())
                { renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false; }
            }
            _leaves=Particles("GustLeaves",new Color(.57f,.65f,.3f,.7f),.08f,18);_dust=Particles("GustDust",new Color(.8f,.75f,.52f,.24f),.18f,12);
            SetTier(tier);
        }
        ParticleSystem Particles(string name,Color color,float size,int max)
        {
            var go=new GameObject(name);go.transform.SetParent(transform,false);var ps=go.AddComponent<ParticleSystem>();ps.Stop();
            var main=ps.main;main.loop=false;main.playOnAwake=false;main.simulationSpace=ParticleSystemSimulationSpace.World;main.startLifetime=3;main.startSize=size;main.startColor=color;main.maxParticles=max;main.startSpeed=0;
            var emission=ps.emission;emission.enabled=false;var shape=ps.shape;shape.enabled=false;
            var renderer=go.GetComponent<ParticleSystemRenderer>();
            var texture=new Texture2D(32,32,TextureFormat.RGBA32,false){name=name+" silhouette",filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
            var pixels=new Color[32*32];bool leaf=name=="GustLeaves";
            for(int y=0;y<32;y++)for(int x=0;x<32;x++)
            {
                var p=new Vector2((x+.5f)/16-1,(y+.5f)/16-1);
                float alpha=leaf?Mathf.Clamp01((1-Mathf.Abs(p.y)/.88f-Mathf.Abs(p.x)/.55f)*12):Mathf.Pow(Mathf.Clamp01(1-p.sqrMagnitude),2);
                float vein=leaf&&Mathf.Abs(p.x)<.055f?.74f:1;
                pixels[y*32+x]=new Color(vein,vein,vein,alpha);
            }
            texture.SetPixels(pixels);texture.Apply(false,true);_textures.Add(texture);
            var mat=CozyParticleMaterial.Create(texture,_tier);_mats.Add(mat);renderer.sharedMaterial=mat;
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            var rotation=ps.rotationOverLifetime;rotation.enabled=true;rotation.z=new ParticleSystem.MinMaxCurve(-3,3);
            var fade=ps.colorOverLifetime;fade.enabled=true;var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(.8f,.15f),new GradientAlphaKey(0,1)});fade.color=gradient;
            return ps;
        }
        void Gust()
        {
            if(_reduced?.Invoke()==true)return;
            // Autumn has real crown-sourced leaves; winter has snow, never green leaf flybys.
            var season=SeasonProfile.For(_level);if(season.Winter||season.Autumn)return;
            var wind=_atmosphere.Wind;var direction=new Vector3(wind.DirectionXZ.x,0,wind.DirectionXZ.y);
            var lateral=Vector3.Cross(direction,Vector3.up);
            // The stream starts on the upwind forest rim, outside the puzzle.
            var origin=-direction*(Mathf.Max(_level.width,_level.height)*.5f+1.3f)+Vector3.up*.3f;
            _leaves.Play(false);_dust.Play(false);
            for(int i=0;i<(_tier==0?4:12);i++)
            {
                var emit=new ParticleSystem.EmitParams{position=origin+lateral*((i-5.5f)*.21f)+Vector3.up*((i%3)*.12f),velocity=direction*(.75f+wind.Strength+(i%3)*.07f)+Vector3.up*.08f,startLifetime=1.8f+(i%4)*.25f,rotation=i*37};_leaves.Emit(emit,1);_dust.Emit(emit,1);
            }
        }
        public void SetTier(int tier)
        {
            _tier=Mathf.Clamp(tier,0,2);
            if(_leaves!=null)CozyParticleMaterial.ApplyTier(_leaves.GetComponent<ParticleSystemRenderer>().sharedMaterial,_tier);
            if(_dust!=null)CozyParticleMaterial.ApplyTier(_dust.GetComponent<ParticleSystemRenderer>().sharedMaterial,_tier);
            if(_camera!=null)
            {
                _camera.GetUniversalAdditionalCameraData().requiresDepthTexture=true;
                _camera.depthTextureMode|=DepthTextureMode.Depth;
            }
            for(int i=0;i<_fog.Count;i++)_fog[i].SetActive(_tier>0&&i<_tier+1);
            foreach(var bird in _birds){bird.root.gameObject.SetActive(false);bird.flying=false;bird.age=-bird.delay;}
        }
        void Update()
        {
            var effective=_atmosphere.QualityTier;if(effective!=_tier)SetTier(effective);
            _clock+=Time.deltaTime;bool reduced=_reduced?.Invoke()==true;
            if(reduced){_leaves.Clear();_dust.Clear();}
            AdvanceBirds(Time.deltaTime,reduced);
        }
        public void AdvanceBirds(float seconds,bool reduced)
        {
            for(int i=0;i<_birds.Count;i++)
            {
                var b=_birds[i];bool allowed=!reduced&&_day&&_tier>0&&i<(_tier==1?2:4);
                if(!allowed){b.age=-b.delay;b.root.gameObject.SetActive(false);b.flying=false;continue;}
                b.age+=Mathf.Max(0,seconds);
                if(b.age>=34){b.age%=34;b.flying=false;}
                bool fly=b.age>=0&&b.age<9;
                if(!fly){b.root.gameObject.SetActive(false);b.flying=false;continue;}
                if(!b.flying)
                {
                    // Compute endpoints outside the current view, including rotated albums.
                    float y=.40f+i*.09f;var plane=new Plane(Vector3.up,new Vector3(0,3.2f+i*.2f,0));
                    Vector3 Edge(float x){var ray=_camera.ViewportPointToRay(new Vector3(x,y,0));return plane.Raycast(ray,out var d)?ray.GetPoint(d):ray.origin;}
                    b.from=Edge(i%2==0?-.22f:1.22f);b.to=Edge(i%2==0?1.22f:-.22f);b.flying=true;b.called=false;
                }
                float progress=b.age/9;
                b.root.position=Vector3.Lerp(b.from,b.to,progress)+Vector3.up*Mathf.Sin(progress*Mathf.PI)*.45f;
                b.root.forward=(b.to-b.from).normalized;b.root.gameObject.SetActive(true);
                if(!b.called&&progress>.30f&&progress<.70f){b.called=true;BirdPassing?.Invoke(b.root);}
                var flap=Mathf.Sin(_clock*20+i)*36;
                b.left.localRotation=Quaternion.Euler(0,0,flap);b.right.localRotation=Quaternion.Euler(0,0,-flap);
            }
        }
        void OnDestroy(){if(_atmosphere!=null)_atmosphere.GustStarted-=Gust;foreach(var m in _mats)if(m!=null)Destroy(m);foreach(var t in _textures)if(t!=null)Destroy(t);}
    }
}
