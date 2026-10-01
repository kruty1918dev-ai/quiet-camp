using System;
using System.Collections.Generic;
using QuietCamp.Domain;
using QuietCamp.Infrastructure;
using UnityEngine;
using UnityEngine.Rendering;

namespace QuietCamp.Presentation.World
{
    /// <summary>Scene-owned, single-camera scenery. No render textures or full-screen blur passes.</summary>
    public sealed class CampAtmosphere : MonoBehaviour
    {
        static readonly int Tint = Shader.PropertyToID("_Tint");
        static readonly int ProtectedRect = Shader.PropertyToID("_ProtectedRect");
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
        Transform _back, _rear, _near;
        Material _backMaterial, _rearMaterial, _nearMaterial;
        Texture2D _background, _rearTexture, _nearTexture;
        Rect _lastViewport;
        Vector2Int _lastScreen;
        float _clock;
        WindSim _windSim;
        WindSim.Snapshot _wind;
        AtmosphereParticles _particles;
        PhasePostFx _postFx;
        int _tier = 1;
        Vector3 _lastViewportPosition;
        AtmosphereCatalog.Profile _profile;
        Light _sun;
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
        AmbientMode _previousAmbientMode;
        Color _previousAmbient;
        bool _previousFog;

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

        public void Configure(Camera camera, LevelData level, RectTransform viewport,
            AtmosphereCatalog.Profile profile, Func<bool> reducedMotion, int qualityTier = 1,
            Transform decorRoot = null)
        {
            _camera = camera;
            _level = level;
            _viewport = viewport;
            _reducedMotion = reducedMotion;
            _tier = qualityTier;
            _decorRoot = decorRoot;
            _previousAmbient = RenderSettings.ambientLight;
            _previousAmbientMode = RenderSettings.ambientMode;
            _previousFog = RenderSettings.fog;
            _windSim = new WindSim(level.decorSeed, profile.Wind, profile.GustMin, profile.GustMax);
            var particlesGo = new GameObject("AtmosphereParticles");
            particlesGo.transform.SetParent(transform, false);
            _particles = particlesGo.AddComponent<AtmosphereParticles>();
            _particles.Configure(camera, level, profile, (AtmosphereParticles.Tier)_tier, reducedMotion);
            _postFx = gameObject.AddComponent<PhasePostFx>();
            _postFx.Configure(camera, _tier);
            var template = Resources.Load<Material>("QuietCamp/Atmosphere/Layer");
            if (template == null) throw new InvalidOperationException("Atmosphere Layer material is missing.");
            _back = CreateLayer("ForestBackdrop", template, 1000, out _backMaterial);
            _rear = CreateLayer("DistantForest", template, 1010, out _rearMaterial);
            _near = CreateLayer("NearFoliage", template, 3050, out _nearMaterial);
            _rearTexture = Resources.Load<Texture2D>("QuietCamp/Atmosphere/Textures/rear");
            _nearTexture = Resources.Load<Texture2D>("QuietCamp/Atmosphere/Textures/foreground");
            _rearMaterial.mainTexture = _rearTexture;
            _nearMaterial.mainTexture = _nearTexture;
            _rearMaterial.SetFloat("_EdgeOnly", 1);
            _nearMaterial.SetFloat("_Protection", 1);
            _nearMaterial.SetFloat("_Feather", .025f);
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
            RegisterDecor(_decorRoot);
            _initialized = true;
            Apply(profile);
            RefreshLayout();
        }

        Transform CreateLayer(string name, Material template, int queue, out Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = name;
            go.layer = BoardRenderer.DecorLayer;
            go.transform.SetParent(_camera.transform, false);
            var collider = go.GetComponent<Collider>();
            collider.enabled = false;
            Destroy(collider);
            material = new Material(template) { name = name + " (runtime)", renderQueue = queue };
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return go.transform;
        }

        public void Apply(AtmosphereCatalog.Profile profile)
        {
            if (!_initialized) return;
            var next = Resources.Load<Texture2D>("QuietCamp/Atmosphere/Textures/" + profile.Id);
            if (next == null) throw new InvalidOperationException("Missing atmosphere texture: " + profile.Id);
            var old = _background;
            _background = next;
            _backMaterial.mainTexture = next;
            if (old != null && old != next) Resources.UnloadAsset(old);
            PhaseId = profile.Id;
            _profile = profile;
            _windSim.SetBase(profile.Wind);
            _windSim.SetGustInterval(profile.GustMin, profile.GustMax);
            _particles?.ApplyProfile(profile);
            _postFx?.Apply(profile, _tier);
            _nearMaterial.SetColor(Tint, profile.Foreground);
            _rearMaterial.SetColor(Tint, new Color(profile.Foreground.r * .85f,
                profile.Foreground.g * .85f, profile.Foreground.b * .85f, .38f));
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = profile.Ambient;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = profile.Ambient;
            RenderSettings.fog = false;
            // Light targets ease over ~4 s — a phase change never snaps.
            _ambientTarget = profile.Ambient;
            _sunColorTarget = _sun != null ? _sun.color * _sun.intensity
                : profile.Sun * profile.SunIntensity;
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
                        copies.Add(source, copy);
                        _foliageSeen.Add(copy);
                        _foliage.Add(copy, source.GetColor(BaseColor));
                    }
                    mats[i] = copy;
                    changed = true;
                }
                if (changed) renderer.sharedMaterials = mats;
            }
        }

        /// <summary>Particles and smoke drift need the campfire's world anchor.</summary>
        public void SetFire(Vector3 position, bool active)
            => _particles?.SetFire(position, active);

        bool _foliageLerping;

        void LateUpdate()
        {
            if (!_initialized || _camera == null) return;
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
            if (!reduced) _clock += Time.deltaTime;

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

            // Shared vegetation state — one owner, one write per frame.
            Shader.SetGlobalVector(WindXZ,
                new Vector4(_wind.DirectionXZ.x, _wind.DirectionXZ.y, 0f, 0f));
            Shader.SetGlobalFloat(WindStrength, _windLevel);
            Shader.SetGlobalFloat(WindTime, _wind.PhaseSeconds);
            Shader.SetGlobalVector(SunDirW, _sunDirNow.normalized);
            Shader.SetGlobalColor(SunColor, _sunColorNow);
            Shader.SetGlobalColor(AmbientId, _ambientNow);

            // Foliage tint follows the phase ambient on the same eased clock.
            var phaseTint = Color.Lerp(Color.white, _ambientNow, .65f);
            foreach (var pair in _foliage)
                pair.Key.SetColor(BaseColor, pair.Value * phaseTint);

            var sway = reduced ? 0f : Mathf.Sin(_clock * .65f) * _windLevel;
            _near.localRotation = Quaternion.Euler(0, 0, sway * .4f);
        }

        public void RefreshLayout()
        {
            if (!_initialized || _camera == null) return;
            Canvas.ForceUpdateCanvases();
            CameraFitter.Fit(_camera, _level, _viewport);
            _lastScreen = new Vector2Int(Screen.width, Screen.height);
            _lastViewport = _viewport != null ? _viewport.rect : default;
            _lastViewportPosition = _viewport != null ? _viewport.position : Vector3.zero;
            var h = 2f * _camera.orthographicSize;
            var w = h * _camera.aspect;
            // Aspect-fill without stretching the landscape.
            float textureAspect = (float)_background.width / _background.height;
            float backW = Mathf.Max(w, h * textureAspect);
            _back.localPosition = new Vector3(0, 0, _camera.farClipPlane - 2);
            _back.localScale = new Vector3(backW * 1.01f, backW / textureAspect * 1.01f, 1);
            // Rear art only occupies the upper side edges, below the painted horizon.
            _rear.localPosition = new Vector3(0, h * .18f, _camera.farClipPlane - 3);
            _rear.localScale = new Vector3(w * 1.08f, h * .34f, 1);
            float nearW = w * 1.12f;
            float nearH = nearW * _nearTexture.height / _nearTexture.width;
            _near.localPosition = new Vector3(0, -h * .5f + nearH * .36f, 1);
            _near.localScale = new Vector3(nearW, nearH, 1);
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
            _nearMaterial.SetVector(ProtectedRect, new Vector4(min.x, min.y, max.x, max.y));
        }

        void OnDestroy()
        {
            if (!_initialized) return;
            RenderSettings.ambientMode = _previousAmbientMode;
            RenderSettings.ambientLight = _previousAmbient;
            RenderSettings.fog = _previousFog;
            if (_back != null) Destroy(_back.gameObject);
            if (_rear != null) Destroy(_rear.gameObject);
            if (_near != null) Destroy(_near.gameObject);
            Destroy(_backMaterial); Destroy(_rearMaterial); Destroy(_nearMaterial);
            foreach (var pair in _foliage) Destroy(pair.Key);
            _foliage.Clear();
            // These textures belong exclusively to the scene atmosphere.
            if (_background != null) Resources.UnloadAsset(_background);
            if (_rearTexture != null) Resources.UnloadAsset(_rearTexture);
            if (_nearTexture != null) Resources.UnloadAsset(_nearTexture);
        }
    }
}
