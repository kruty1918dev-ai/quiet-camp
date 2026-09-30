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
        static readonly int SwayAmp = Shader.PropertyToID("_SwayAmp");
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
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
        bool _lastReduced;
        Vector3 _lastViewportPosition;
        AtmosphereCatalog.Profile _profile;
        /// <summary>Scene-local foliage clones: base color + amplitude scale
        /// (trees sway slower/smaller than grass — their materials carry the
        /// creator's _SwayAmp ratio as a weight).</summary>
        readonly Dictionary<Material, FoliageEntry> _foliage = new Dictionary<Material, FoliageEntry>();

        struct FoliageEntry
        {
            public Color Color;
            public float AmpScale;
        }
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
            AtmosphereCatalog.Profile profile, Func<bool> reducedMotion, int qualityTier = 1)
        {
            _camera = camera;
            _level = level;
            _viewport = viewport;
            _reducedMotion = reducedMotion;
            _tier = qualityTier;
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
            CaptureFoliage();
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
            ApplyFoliage(_reducedMotion != null && _reducedMotion());
        }

        void CaptureFoliage()
        {
            // Keep the package's shader; own scene-local copies rather than tinting its shared cache.
            var copies = new Dictionary<Material, Material>();
            foreach (var renderer in FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                var source = renderer.sharedMaterial;
                if (source == null || source.shader.name != "Atmos/FoliageSway") continue;
                if (!copies.TryGetValue(source, out var copy))
                {
                    copy = new Material(source) { name = source.name + " (camp atmosphere)" };
                    copies.Add(source, copy);
                    _foliage.Add(copy, new FoliageEntry
                    {
                        Color = source.GetColor(BaseColor),
                        AmpScale = Mathf.Clamp01(source.GetFloat(SwayAmp) / .05f),
                    });
                }
                renderer.sharedMaterial = copy;
            }
        }

        void ApplyFoliage(bool reduced)
        {
            _lastReduced = reduced;
            foreach (var pair in _foliage)
            {
                pair.Key.SetFloat(SwayAmp, reduced ? 0 : SwayAmplitude() * pair.Value.AmpScale);
                pair.Key.SetColor(BaseColor, pair.Value.Color * Color.Lerp(Color.white, _profile.Ambient, .65f));
            }
        }

        /// <summary>Sway amplitude follows the shared wind snapshot — calm
        /// base breeze plus the live gust envelope, normalized like before.</summary>
        float SwayAmplitude() => .05f * Mathf.Clamp(_wind.Strength + _wind.GustEnvelope * .1f, 0f, .6f) / .25f;

        /// <summary>Particles and smoke drift need the campfire's world anchor.</summary>
        public void SetFire(Vector3 position, bool active)
            => _particles?.SetFire(position, active);

        void LateUpdate()
        {
            if (!_initialized || _camera == null) return;
            var rect = _viewport != null ? _viewport.rect : default;
            var size = new Vector2Int(Screen.width, Screen.height);
            var position = _viewport != null ? _viewport.position : Vector3.zero;
            if (size != _lastScreen || rect != _lastViewport || position != _lastViewportPosition) RefreshLayout();
            // Visual time never drives puzzle state. Reduced motion really removes sway.
            bool reduced = _reducedMotion != null && _reducedMotion();
            if (reduced != _lastReduced) ApplyFoliage(reduced);
            _wind = _windSim.Advance(Time.deltaTime);
            if (!reduced)
            {
                _clock += Time.deltaTime;
                // Foliage sway tracks the shared gust envelope, not its own timer.
                float amp = SwayAmplitude();
                foreach (var pair in _foliage) pair.Key.SetFloat(SwayAmp, amp * pair.Value.AmpScale);
            }
            var sway = reduced ? 0f : Mathf.Sin(_clock * .65f) * _wind.Strength;
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
