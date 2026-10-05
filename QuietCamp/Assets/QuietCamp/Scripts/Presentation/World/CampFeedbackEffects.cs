using System;
using QuietCamp.Domain;
using UnityEngine;
using UnityEngine.Rendering;

namespace QuietCamp.Presentation.World
{
    /// <summary>Small scene-owned particle pools for placement and success.
    /// Cosmetic randomness is isolated from the puzzle and ambient simulations.</summary>
    public sealed class CampFeedbackEffects : MonoBehaviour
    {
        ParticleSystem _dust, _sparkles, _grass;
        Material _material, _glowMaterial;
        Texture2D _texture;
        Func<bool> _reducedMotion;
        System.Random _random;
        int _tier;
        SeasonProfile _season;
        CampBiomeProfile _surface;
        bool _hasEnvironment;
        float _nextPlacement, _nextSuccess;
        public int ActiveParticles => (_dust != null ? _dust.particleCount : 0)
            + (_sparkles != null ? _sparkles.particleCount : 0) + (_grass != null ? _grass.particleCount : 0);
        public int TotalParticles => ActiveParticles;
        Func<int> _quality;
        Func<Vector2> _wind;
        public void FollowQuality(Func<int> quality) => _quality = quality;
        public void FollowWind(Func<Vector2> wind) => _wind = wind;
        public void SetEnvironment(LevelData level)
        { _hasEnvironment=true;_season=SeasonProfile.For(level);_surface=new CampBiomeProfile(level); }

        public void Configure(int tier, Func<bool> reducedMotion, int seed)
        {
            if (_material != null) return;
            _tier = Mathf.Clamp(tier, 0, 2);
            _reducedMotion = reducedMotion;
            _random = new System.Random(seed ^ 0x4f2d);
            var shader = Shader.Find("QuietCamp/LeafParticle");
            if (shader == null) return;
            _texture = CozyParticleAtlas.Create();
            _material = CozyParticleMaterial.Create(_texture, _tier);
            _glowMaterial = CozyParticleMaterial.Create(_texture, _tier, .65f);
            _dust = CreatePool("PlacementDust", _tier == 0 ? 12 : 32, .65f);
            _sparkles = CreatePool("CompletionSparkles", _tier == 0 ? 16 : _tier == 1 ? 48 : 80, 1.8f);
            _sparkles.GetComponent<ParticleSystemRenderer>().sharedMaterial = _glowMaterial;
            _grass = CreatePool("DisturbedGrass", _tier == 0 ? 4 : 12, .7f);
            CozyParticleAtlas.Tile(_dust, 0); CozyParticleAtlas.Tile(_sparkles, 1); CozyParticleAtlas.Tile(_grass, 2);
            var gravity = _dust.main; gravity.gravityModifier = .08f;
            gravity = _grass.main; gravity.gravityModifier = .24f;
            var rotation = _grass.rotationOverLifetime; rotation.enabled = true; rotation.z = new ParticleSystem.MinMaxCurve(-2, 2);
        }

        ParticleSystem CreatePool(string name, int budget, float lifetime)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = false;
            main.playOnAwake = false;
            main.useUnscaledTime = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = budget;
            main.startLifetime = lifetime;
            main.startSpeed = 0f;
            main.startSize = .1f;
            main.gravityModifier = -.04f;
            var emission = ps.emission; emission.enabled = false;
            var shape = ps.shape; shape.enabled = false;
            var fade = ps.colorOverLifetime; fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, .12f), new GradientAlphaKey(0, 1) });
            fade.color = gradient;
            var size = ps.sizeOverLifetime; size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0, 1, 1, .1f));
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = _material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return ps;
        }

        public void Placement(Vector3 center)
        {
            if (_dust == null || Reduced || Time.unscaledTime < _nextPlacement) return;
            _nextPlacement = Time.unscaledTime + .08f;
            Emit(_dust, center, _tier == 0 ? 4 : _tier == 1 ? 8 : 12, false);
            EmitGrass(center, _tier == 0 ? 2 : 4);
        }

        public void Lift(Vector3 center)
        {
            if (_grass == null || Reduced || Time.unscaledTime < _nextPlacement) return;
            _nextPlacement = Time.unscaledTime + .18f; EmitGrass(center, _tier == 0 ? 2 : 5);
        }
        void EmitGrass(Vector3 center, int count)
        {
            if(_hasEnvironment&&_surface.Snow)return;
            _grass.Play(false);
            for (int i = 0; i < count; i++)
            {
                float angle = RandomRange(0, Mathf.PI * 2);
                var radial = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
                _grass.Emit(new ParticleSystem.EmitParams { position = center + radial * .75f + Vector3.up * .05f,
                    velocity = radial * .14f + Vector3.up * .30f, startSize = RandomRange(.06f, .11f),
                    startLifetime = RandomRange(.4f, .7f), startColor = _hasEnvironment&&_season.Autumn
                        ?new Color(.68f,.42f,.19f,.7f):new Color(.57f, .66f, .29f, .7f),
                    rotation = RandomRange(0, 360), randomSeed = (uint)_random.Next(1, int.MaxValue) }, 1);
            }
        }

        public void Complete(Vector3 center)
        {
            if (_sparkles == null || Reduced || Time.unscaledTime < _nextSuccess) return;
            _nextSuccess = Time.unscaledTime + 1.8f;
            Emit(_sparkles, center + Vector3.up * .3f, _tier == 0 ? 12 : _tier == 1 ? 32 : 56, true);
        }

        bool Reduced => _reducedMotion?.Invoke() ?? false;
        float RandomRange(float min, float max) => Mathf.Lerp(min, max, (float)_random.NextDouble());
        void Emit(ParticleSystem ps, Vector3 center, int count, bool success)
        {
            ps.Play(false);
            for (int i = 0; i < count; i++)
            {
                float angle = RandomRange(0, Mathf.PI * 2);
                float radius = success ? RandomRange(.4f, 2f) : RandomRange(.5f, 1f);
                var radial = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
                var parameters = new ParticleSystem.EmitParams
                {
                    position = center + radial * radius + Vector3.up * .08f,
                    velocity = radial * (success ? .18f : .35f) + Vector3.up * (success ? .65f : .3f),
                    startSize = RandomRange(success ? .055f : .13f, success ? .12f : .23f),
                    startLifetime = RandomRange(success ? 1.1f : .4f, success ? 1.8f : .65f),
                    startColor = success ? new Color(1f, .86f, .42f, .85f)
                        : _hasEnvironment&&_surface.Snow?new Color(.88f,.93f,.99f,.65f)
                        : _hasEnvironment&&_surface.SurfaceKey=="sfx.surface.mud"?new Color(.40f,.34f,.25f,.55f)
                        : new Color(.85f, .77f, .55f, .55f),
                    randomSeed = (uint)_random.Next(1, int.MaxValue)
                };
                ps.Emit(parameters, 1);
            }
        }

        void Update()
        {
            if (_dust == null) return;
            if (Reduced && TotalParticles > 0) Clear();
            int tier = Mathf.Clamp(_quality?.Invoke() ?? _tier, 0, 2);
            if (tier != _tier)
            {
                _tier = tier;
                CozyParticleMaterial.ApplyTier(_material, tier); CozyParticleMaterial.ApplyTier(_glowMaterial, tier);
                var main = _dust.main; main.maxParticles = tier == 0 ? 12 : 32;
                main = _sparkles.main; main.maxParticles = tier == 0 ? 16 : tier == 1 ? 48 : 80;
                main = _grass.main; main.maxParticles = tier == 0 ? 4 : 12;
            }
            var wind = _wind?.Invoke() ?? Vector2.zero;
            Drift(_dust, wind * .14f); Drift(_grass, wind * .08f); Drift(_sparkles, wind * .1f);
        }
        static void Drift(ParticleSystem system, Vector2 wind)
        {
            var velocity = system.velocityOverLifetime; velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = wind.x; velocity.z = wind.y;
        }
        public void Clear()
        {
            _dust?.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            _sparkles?.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            _grass?.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
        void OnDisable() => Clear();
        void OnDestroy()
        {
            if (_material != null) Destroy(_material);
            if (_glowMaterial != null) Destroy(_glowMaterial);
            if (_texture != null) Destroy(_texture);
        }
    }
}
