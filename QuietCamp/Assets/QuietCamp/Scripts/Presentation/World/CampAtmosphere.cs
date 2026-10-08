using System;
using System.Collections.Generic;
using QuietCamp.Domain;
using QuietCamp.Infrastructure;
using UnityEngine;
using UnityEngine.Rendering;

namespace QuietCamp.Presentation.World
{
    /// <summary>Scene-owned lighting, wind and particles for the shared 3D clearing.</summary>
    public sealed class CampAtmosphere : MonoBehaviour
    {
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        // Shared vegetation contract — this component is the single owner.
        static readonly int WindXZ = Shader.PropertyToID("_AtmosWindXZ");
        static readonly int WindStrength = Shader.PropertyToID("_AtmosWindStrength");
        static readonly int WindTime = Shader.PropertyToID("_AtmosWindTime");
        static readonly int WaveLen = Shader.PropertyToID("_AtmosWaveLen");
        static readonly int WaveSpeed = Shader.PropertyToID("_AtmosWaveSpeed");
        static readonly int FlutterScale = Shader.PropertyToID("_AtmosFlutterScale");
        static readonly int SunDirW = Shader.PropertyToID("_AtmosSunDirW");
        static readonly int SunColor = Shader.PropertyToID("_AtmosSunColor");
        static readonly int AmbientId = Shader.PropertyToID("_AtmosAmbient");
        Camera _camera;
        LevelData _level;
        RectTransform _viewport;
        Func<bool> _reducedMotion;
        MeadowSurface _meadow;
        EnvironmentComposer _composition;
        SeasonalLeafFall _seasonalLeaves;
        public SeasonalLeafFall SeasonalLeaves=>_seasonalLeaves;
        Texture2D _windShelterTexture;
        CameraAtmosphereMotion _cameraMotion;
        public EnvironmentComposer Environment=>_composition;
        public CameraAtmosphereMotion CameraMotion=>_cameraMotion;
        public WindFieldMath.Sample SampleWind(Vector3 position,float height)=>_windSim.Sample(position,height);
        Rect _lastViewport;
        Vector2Int _lastScreen;
        WindSim _windSim;
        WindSim.Snapshot _wind;
        AtmosphereParticles _particles;
        PhasePostFx _postFx;
        int _tier = 1;
        Func<int> _qualitySource;
        public int QualityTier => _tier;
        public Camera WorldCamera=>_camera;
        Vector3 _lastViewportPosition;
        Light _sun;
        ForestLightingEnvironment _environment;
        SphericalHarmonicsL2 _previousProbe;
        CanopySunlight _sunlight;
        public CanopySunlight Sunlight => _sunlight;
        ForestEdgeAtmosphere _edges;
        public ForestEdgeAtmosphere Edges => _edges;
        CampPuddles _puddles;
        public CampPuddles Puddles => _puddles;
        // Smoothed vegetation state — profiles and reduced motion ease over
        // time instead of snapping poses (spec §8: 3–5 s profile transitions).
        float _windLevel;
        Color _ambientNow, _ambientTarget;
        Color _sunColorNow, _sunColorTarget;
        Vector3 _sunDirNow = Vector3.down, _sunDirTarget = Vector3.down;
        /// <summary>Scene-local foliage clones mapped to their base colors —
        /// cloned so a phase tint never leaks into the package's shared cache
        /// or a preview scene.</summary>
        readonly Dictionary<Material, Color> _foliage = new Dictionary<Material, Color>();
        readonly HashSet<Material> _foliageSeen = new HashSet<Material>();
        Transform _decorRoot;
        bool _initialized;
        public bool RestoreEnvironmentOnDestroy=true;
        AmbientMode _previousAmbientMode;
        Color _previousAmbient;
        bool _previousFog;
        Color _previousSky,_previousEquator,_previousGround;
        CampWeather _weather;AtmosphereCatalog.Profile _profile;
        public CampWeather Weather=>_weather;
        CampRainShelter _shelter;
        public CampRainShelter RainShelter=>_shelter;
        CampSoundscape _soundscape;
        public CampSoundscape Soundscape => _soundscape;
        public void BindRainWorld(Transform world)
        {
            if(world==null)return;
            _weather.World=world;
            _soundscape?.BindWorld(world);
            if(_shelter==null)_shelter=gameObject.AddComponent<CampRainShelter>();
            _shelter.Configure(this,world);
        }

        public string PhaseId { get; private set; }
        public Rect ProtectedViewport { get; private set; }
        /// <summary>Latest shared wind snapshot — one owner for foliage,
        /// particles and ambience scheduling. Zero-struct before Configure.</summary>
        public WindSim.Snapshot Wind => _wind;
        public AtmosphereParticles Particles => _particles;
        /// <summary>Raised once when a gust's attack phase begins — audio
        /// hooks a single gust one-shot here rather than on its own timer.</summary>
        public event Action GustStarted
        {
            add { if (_windSim != null) _windSim.GustStarted += value; }
            remove { if (_windSim != null) _windSim.GustStarted -= value; }
        }

        VisibleForestFloor _floor;
        public bool InitialWorldReady => (_floor == null || _floor.InitialReady) && (_composition == null || _composition.InitialReady);
        public void Configure(Camera camera, LevelData level, RectTransform viewport,
            AtmosphereCatalog.Profile profile, Func<bool> reducedMotion, int qualityTier = 1,
            Transform decorRoot = null, Func<int> qualitySource = null)
        {
            using var audit = PerformanceAudit.Measure("QC.CampAtmosphere.Configure");
            _camera = camera;
            _level = level;
            _viewport = viewport;
            _reducedMotion = reducedMotion;
            _tier = qualityTier;
            _qualitySource=qualitySource;
            _decorRoot = decorRoot;
            _previousAmbient = RenderSettings.ambientLight;
            _previousProbe = RenderSettings.ambientProbe;
            _environment = ForestLightingEnvironment.Load();
            _previousAmbientMode = RenderSettings.ambientMode;
            _previousFog = RenderSettings.fog;
            _previousSky=RenderSettings.ambientSkyColor;_previousEquator=RenderSettings.ambientEquatorColor;_previousGround=RenderSettings.ambientGroundColor;
            _windSim = new WindSim(level.decorSeed, profile.Wind, profile.GustMin, profile.GustMax);
            var particlesGo = new GameObject("AtmosphereParticles");
            particlesGo.transform.SetParent(transform, false);
            _particles = particlesGo.AddComponent<AtmosphereParticles>();
            try { _particles.Configure(camera, level, profile, (AtmosphereParticles.Tier)_tier, reducedMotion); }
            catch (Exception error)
            {
                // Optional decoration cannot prevent either scene's UI startup.
                Debug.LogWarning($"[QuietCamp] Atmospheric particles unavailable: {error.GetType().Name}: {error.Message}");
                particlesGo.SetActive(false);Destroy(particlesGo);_particles=null;
            }
            _postFx = gameObject.AddComponent<PhasePostFx>();
            _postFx.Configure(camera, _tier, reducedMotion);
            // Scenery shares the board's XZ ground and camera projection.
            // Camera-facing landscape paintings introduce a second horizon.
            _meadow = decorRoot != null ? decorRoot.GetComponentInChildren<MeadowSurface>() : null;
            // Vegetation wave field constants (spec §5): wavelength ~10 cells,
            // speed ~1.5 cells/s — slow enough for neighbouring plants to
            // move as kin instead of a synchronized dance.
            Shader.SetGlobalFloat(WaveLen, 10f);
            Shader.SetGlobalFloat(WaveSpeed, 1.5f);
            Shader.SetGlobalFloat(FlutterScale, _tier > 0 ? 1f : 0f);
            _sun = RenderSettings.sun;
            if (_sun == null || _sun.type != LightType.Directional)
                foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
                    if (l.type == LightType.Directional) { _sun = l; break; }
            // Seed scenery against the same fixed light that will illuminate the
            // puzzle. SceneHost applies the phase after Configure; waiting for
            // that used the serialized light pose when rejecting scenic shadows.
            if (_sun != null)
            {
                _sun.transform.localEulerAngles = new Vector3(profile.Elevation, 65f, 0f);
                _sun.intensity = profile.SunIntensity;
                _sun.color = profile.Sun*SeasonPalette.For(_level).SunTint;
                RenderSettings.sun = _sun;
            }
            if(decorRoot!=null)
            {
                CameraFitter.Fit(camera,level,viewport);
                var compositionRoot=new GameObject("ComposedEnvironment");compositionRoot.transform.SetParent(decorRoot,false);
                _composition=compositionRoot.AddComponent<EnvironmentComposer>();
                _composition.Configure(level,camera,decorRoot,QuietCampBootstrap.ServicesRef?.Assets,UnityEngine.Application.isPlaying);
                _windSim.SetShelter(_composition.Shelter);
                _windShelterTexture=_composition.Shelter?.CreateTexture();
                PublishShelter();
            }
            RegisterDecor(_decorRoot);
            _initialized = true;
            Apply(profile);
            RefreshLayout();
            if (decorRoot != null)
            {
                var floorRoot = new GameObject("VisibleForestFloor");
                floorRoot.transform.SetParent(decorRoot, false);
                _floor = floorRoot.AddComponent<VisibleForestFloor>();
                _floor.Configure(level, camera, UnityEngine.Application.isPlaying);
            }
            var sunlightRoot = new GameObject("CanopyLighting");
            sunlightRoot.transform.SetParent(transform, false);
            _sunlight = sunlightRoot.AddComponent<CanopySunlight>();
            _sunlight.Configure(this, level, reducedMotion);
            _sunlight.Apply(profile);
            var life=new GameObject("LivingForest");life.transform.SetParent(transform,false);
            _life=life.AddComponent<LivingForest>();_life.Configure(this,camera,level,profile,reducedMotion,_tier);
            _weather=life.AddComponent<CampWeather>();_weather.Configure(camera,level,profile.Id=="night");
            if(SeasonProfile.For(level).Autumn)
            {
                var leafRoot=new GameObject("Seasonal leaf fall");leafRoot.transform.SetParent(transform,false);
                _seasonalLeaves=leafRoot.AddComponent<SeasonalLeafFall>();_seasonalLeaves.Configure(this,_composition,level,camera);
            }
            var edgeRoot=new GameObject("ForestDepth");edgeRoot.transform.SetParent(transform,false);
            _edges=edgeRoot.AddComponent<ForestEdgeAtmosphere>();_edges.Configure(this,camera,level,reducedMotion);
            var waterRoot=new GameObject("RainPuddles");waterRoot.transform.SetParent(transform,false);
            _puddles=waterRoot.AddComponent<CampPuddles>();_puddles.Configure(this,camera,level,reducedMotion);
            _soundscape=life.AddComponent<CampSoundscape>();
            _soundscape.Configure(this,level,profile,decorRoot,QuietCampBootstrap.ServicesRef?.Audio);
            _life.BirdPassing+=_soundscape.BirdPassing;
            life.AddComponent<CampLantern>().Configure(level,
                () => QuietCampBootstrap.ServicesRef?.Settings.fireflyLantern ?? false,
                reducedMotion, () => _qualitySource?.Invoke() ?? _tier);
            var placement=FindFirstObjectByType<PlacementController>();
            if(placement!=null)
            {
                _cameraMotion=gameObject.AddComponent<CameraAtmosphereMotion>();
                _cameraMotion.Configure(camera,()=>Wind,reducedMotion,()=>placement!=null&&placement.IsPlacementActive||!(CampSceneHost.Current?.GameplayActive??true));
            }
        }
        void PublishShelter()
        {
            var shelter=_composition?.Shelter;
            WindFieldMath.CurrentShelter=shelter;
            Shader.SetGlobalTexture("_CampWindShelter",_windShelterTexture!=null?_windShelterTexture:Texture2D.blackTexture);
            Shader.SetGlobalVector("_CampWindShelterRect",shelter?.ShaderTransform??new Vector4(0,0,1,1));
            Shader.SetGlobalFloat("_CampWindCanopyHeight",shelter?.CanopyHeight??4);
            float resolution=shelter?.Resolution??32;
            Shader.SetGlobalVector("_CampWindShelterInfo",new Vector4((resolution-1)/resolution,.5f/resolution,0,0));
        }

        public void Apply(AtmosphereCatalog.Profile profile)
        {
            if (!_initialized) return;
            PhaseId = profile.Id;
            _profile=profile;
            _soundscape?.Apply(profile);
            _life?.SetPhase(profile.Id);
            _sunlight?.Apply(profile);
            _weather?.SetNight(profile.Id=="night");
            _windSim.SetBase(profile.Wind);
            _windSim.SetGustInterval(profile.GustMin, profile.GustMax);
            _particles?.ApplyProfile(profile);
            _postFx?.Apply(profile, _tier);
            _camera.clearFlags = CameraClearFlags.SolidColor;
            var palette=SeasonPalette.For(_level);var ambient=palette.Ambient(profile.Ambient);
            _camera.backgroundColor = ambient;
            RenderSettings.ambientMode = _environment != null ? AmbientMode.Custom : AmbientMode.Trilight;
            RenderSettings.ambientLight = ambient;
            RenderSettings.ambientSkyColor=ambient*.90f;
            RenderSettings.ambientEquatorColor=ambient*.72f;
            RenderSettings.ambientGroundColor=ambient*(palette.SnowCoverage>.5f?new Color(.52f,.54f,.59f):new Color(.38f,.43f,.32f));
            RenderSettings.fog = false;
            // Light targets ease over ~4 s — a phase change never snaps.
            _ambientTarget = ambient;
            _sunColorTarget = profile.Sun * palette.SunTint * profile.SunIntensity;
            if (_sun != null) _sunDirTarget = -_sun.transform.forward;
            if (!_foliageLerping) // first apply: start already settled
            {
                _ambientNow = _ambientTarget;
                _sunColorNow = _sunColorTarget;
                _sunDirNow = _sunDirTarget;
            }
        }

        /// <summary>
        /// Registers sway materials under a decor root — scoped, never a
        /// scene-wide scan, and safe to call again for plants created after
        /// the initial capture (spec §2.8–2.9). Idempotent per material.
        /// </summary>
        public void RegisterDecor(Transform decorRoot)
        {
            using var audit = PerformanceAudit.Measure("QC.CampAtmosphere.RegisterDecor");
            if (decorRoot == null) return;
            var copies = new Dictionary<Material, Material>();
            foreach (var renderer in decorRoot.GetComponentsInChildren<Renderer>())
            {
                var mats = renderer.sharedMaterials;
                var changed = false;
                for (var i = 0; i < mats.Length; i++)
                {
                    var source = mats[i];
                    if (source == null || source.shader == null
                        || source.shader.name != "Atmos/FoliageSway") continue;
                    if (_foliageSeen.Contains(source)) continue; // already ours
                    if (!copies.TryGetValue(source, out var copy))
                    {
                        copy = new Material(source)
                        { name = source.name + " (camp atmosphere)" };
                        var lit=Shader.Find("QuietCamp/FoliageLit");if(lit!=null)copy.shader=lit;
                        // Grass and flower ribbons must cast from either face;
                        // solid trunks/crowns keep back-face culling.
                        copy.SetFloat("_Cull",source.GetFloat("_SwayAmp")>=.02f?0:2);
                        CampFoliageResponse.Apply(copy);
                        copies.Add(source, copy);
                        _foliageSeen.Add(copy);
                        _foliage.Add(copy, source.GetColor(BaseColor));
                    }
                    mats[i] = copy;
                    changed = true;
                }
                if (changed)
                {
                    renderer.sharedMaterials = mats;
                    CampFoliageResponse.BindRenderer(renderer);
                    CampFoliageResponse.ExpandBounds(renderer);
                    renderer.shadowCastingMode = ShadowCastingMode.On;
                    renderer.receiveShadows = true;
                }
            }
        }

        /// <summary>Particles and smoke drift need the campfire's world anchor.</summary>
        public bool HasFire { get; private set; }
        public void SetFire(Vector3 position, bool active)
        {
            HasFire=active;
            _soundscape?.SetFire(position,active);
            _particles?.SetFire(position,active);
        }

        public void SetSuspended(bool suspended)
        {
            enabled=!suspended;
            if(_particles!=null)_particles.gameObject.SetActive(!suspended);
            if(_postFx!=null)_postFx.SetSuspended(suspended);
            if(_life!=null)_life.gameObject.SetActive(!suspended);
            if(_sunlight!=null)_sunlight.gameObject.SetActive(!suspended);
            if(_edges!=null)_edges.gameObject.SetActive(!suspended);
            if(_puddles!=null)_puddles.gameObject.SetActive(!suspended);
            if(_cameraMotion!=null)_cameraMotion.enabled=!suspended;
        }
        LivingForest _life;
        bool _foliageLerping;

        void LateUpdate()
        {
            using var audit = PerformanceAudit.Measure("QC.CampAtmosphere.LateUpdate");
            if (!_initialized || _camera == null) return;
            if (_windShelterTexture == null && _composition?.Shelter != null)
            {
                _windSim.SetShelter(_composition.Shelter);
                _windShelterTexture = _composition.Shelter.CreateTexture(); PublishShelter();
            }
            var tier=_qualitySource?.Invoke()??_tier;
            if(tier!=_tier){_tier=tier;_postFx?.Apply(AtmosphereCatalog.Load().Get(PhaseId),tier);_life?.SetTier(tier);_particles?.SetTier((AtmosphereParticles.Tier)tier);Shader.SetGlobalFloat(FlutterScale,tier>0?1:0);}
            var rect = _viewport != null ? _viewport.rect : default;
            var size = new Vector2Int(Screen.width, Screen.height);
            var position = _viewport != null ? _viewport.position : Vector3.zero;
            if (size != _lastScreen || rect != _lastViewport || position != _lastViewportPosition) RefreshLayout();
            _wind = _windSim.Advance(Time.deltaTime);
            bool reduced = _reducedMotion != null && _reducedMotion();

            // One eased level drives every plant: reduced motion approaches
            // zero in ~0.3 s and returns smoothly, no pose snap (spec §11).
            float targetLevel = reduced ? 0f : _wind.Strength;
            _windLevel = Mathf.MoveTowards(_windLevel, targetLevel,
                Time.deltaTime * 3f); // ~0.33 s settle per spec §11

            // Phase targets ease over ~4 s; tint follows on the same clock.
            float k = 1f - Mathf.Exp(-Time.deltaTime / 1.3f); // ~4 s settle
            bool settling = false;
            if (_ambientNow != _ambientTarget)
            { _ambientNow = Color.Lerp(_ambientNow, _ambientTarget, k); settling = true; }
            if (_sunColorNow != _sunColorTarget)
            { _sunColorNow = Color.Lerp(_sunColorNow, _sunColorTarget, k); settling = true; }
            if ((_sunDirNow - _sunDirTarget).sqrMagnitude > 1e-6f)
            { _sunDirNow = Vector3.Slerp(_sunDirNow, _sunDirTarget, k); settling = true; }
            _foliageLerping = settling;
            // Mesh rain queries and this frame's cloth shader share one pose.
            Shader.SetGlobalVector(WindXZ,
                new Vector4(_wind.DirectionXZ.x, _wind.DirectionXZ.y, 0f, 0f));
            Shader.SetGlobalFloat(WindStrength, _windLevel);
            Shader.SetGlobalFloat(WindTime, _wind.PhaseSeconds);
            PublishShelter();
            _weather?.Advance(Time.deltaTime,_tier,reduced,_wind.DirectionXZ*_windLevel);
            _seasonalLeaves?.Advance(Time.deltaTime,_tier,reduced);
            _shelter?.Advance(Time.deltaTime,_weather?.RainAmount??0);
            _soundscape?.Advance(Time.deltaTime);
            _sunlight?.Advance(Time.deltaTime);
            _edges?.Advance(Time.deltaTime);
            _puddles?.Advance(Time.deltaTime);
            if (_environment != null) RenderSettings.ambientProbe = _environment.Probe(_ambientNow, _weather?.Cloudiness ?? 0);
            float sunlight=_weather?.SunMultiplier??1;
            if(_sun!=null&&_profile!=null){_sun.intensity=_profile.SunIntensity*sunlight;_sun.color=_profile.Sun*SeasonPalette.For(_level).SunTint;}

            // Shared vegetation state — one owner, one write per frame.
            Shader.SetGlobalVector(SunDirW, _sunDirNow.normalized);
            Shader.SetGlobalColor(SunColor, _sunColorNow*sunlight);
            Shader.SetGlobalColor(AmbientId, _ambientNow*.72f);

            // Foliage tint follows the phase ambient on the same eased clock.
            var phaseTint = Color.Lerp(Color.white, _ambientNow, .22f);
            foreach (var pair in _foliage)
            {
                var natural=pair.Value;
                if(natural.g>natural.r*1.05f)natural*=new Color(.96f,1.10f,.87f,1);
                pair.Key.SetColor(BaseColor, natural * phaseTint);
            }
        }

        public void RefreshLayout()
        {
            using var audit = PerformanceAudit.Measure("QC.CampAtmosphere.RefreshLayout");
            if (!_initialized || _camera == null) return;
            Canvas.ForceUpdateCanvases();
            CameraFitter.Fit(_camera, _level, _viewport);
            _lastScreen = new Vector2Int(Screen.width, Screen.height);
            _lastViewport = _viewport != null ? _viewport.rect : default;
            _lastViewportPosition = _viewport != null ? _viewport.position : Vector3.zero;
            _meadow?.FitToCamera(_camera);
            // Include the raised board and tent silhouettes, not only ground corners.
            var min = Vector2.one;
            var max = Vector2.zero;
            for (int i = 0; i < 8; i++)
            {
                var p = new Vector3(((i & 1) == 0 ? -1 : 1) * (_level.width * .5f + .15f),
                    (i & 2) == 0 ? -.35f : 1.6f,
                    ((i & 4) == 0 ? -1 : 1) * (_level.height * .5f + .15f));
                var v = (Vector2)_camera.WorldToViewportPoint(p);
                min = Vector2.Min(min, v); max = Vector2.Max(max, v);
            }
            var padding = new Vector2(16f / Mathf.Max(1, Screen.width), 16f / Mathf.Max(1, Screen.height));
            min -= padding; max += padding;
            ProtectedViewport = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        void OnDestroy()
        {
            if (_particles != null) Destroy(_particles.gameObject);
            if (_life != null) Destroy(_life.gameObject);
            if (_postFx != null) Destroy(_postFx);
            if (_shelter != null) Destroy(_shelter);
            if (_sunlight != null) Destroy(_sunlight.gameObject);
            if(_windShelterTexture!=null)Destroy(_windShelterTexture);
            if(WindFieldMath.CurrentShelter==_composition?.Shelter)WindFieldMath.CurrentShelter=null;
            _puddles?.ReleaseSky(RestoreEnvironmentOnDestroy);
            if (!_initialized) return;
            if(RestoreEnvironmentOnDestroy)
            {
                RenderSettings.ambientMode = _previousAmbientMode;
                RenderSettings.ambientLight = _previousAmbient;
                RenderSettings.ambientProbe = _previousProbe;
                RenderSettings.fog = _previousFog;
                RenderSettings.ambientSkyColor=_previousSky;RenderSettings.ambientEquatorColor=_previousEquator;RenderSettings.ambientGroundColor=_previousGround;
            }
            foreach (var pair in _foliage) Destroy(pair.Key);
            _foliage.Clear();
        }
    }
}
