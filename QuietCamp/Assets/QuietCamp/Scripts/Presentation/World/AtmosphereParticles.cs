using System;
using QuietCamp.Domain;
using QuietCamp.Infrastructure;
using UnityEngine;

namespace QuietCamp.Presentation.World
{
    /// <summary>
    /// Scene-owned ambient particles driven by the shared <see cref="WindSim"/>
    /// snapshot: drifting leaves at the upwind clearing edge, dawn dust motes,
    /// evening fireflies near low bushes, campfire smoke and one far mist quad.
    /// Fixed small pools — no per-frame allocation, no colliders, no depth or
    /// collision modules. Budgets come from the quality tier tables in the
    /// atmosphere spec (Balanced counts live in atmosphere.json).
    /// </summary>
    public sealed class AtmosphereParticles : MonoBehaviour
    {
        public enum Tier { Low = 0, Balanced = 1, High = 2 }

        Material _leafMat, _softMat;
        Texture2D _leafTex, _dotTex;
        ParticleSystem _leaves, _nearLeaf, _dust, _fireflies, _smoke;
        Transform _mist;
        Material _mistMat;
        Camera _camera;
        float _meadowRadius;
        Tier _tier = Tier.Balanced;
        AtmosphereCatalog.Profile _profile;
        Vector3 _firePos;
        bool _fireActive;
        Func<bool> _reducedMotion;
        float _nearLeafTimer = 30f;
        int _nearLeafBudget;
        int _leafBudget;
        bool _lastReduced;
        System.Random _decorRng;
        Vector3 _mistAnchor;
        float _mistDrift;
        bool _initialized;

        // Balanced-tier counts per spec; Low/High scale per the effect table.
        // A phase profile value of 0 means OFF on every tier — scaling must
        // never resurrect an effect the phase disables (e.g. day fireflies).
        static int Scale(int balanced, int low, int high, Tier tier)
            => balanced <= 0 ? 0
             : tier == Tier.Low ? Math.Min(balanced, low)
             : tier == Tier.High ? Mathf.Max(balanced, high) : balanced;

        public void Configure(Camera camera, LevelData level, AtmosphereCatalog.Profile profile,
            Tier tier, Func<bool> reducedMotion)
        {
            _camera = camera;
            _tier = tier;
            _reducedMotion = reducedMotion;
            _profile = profile;
            _meadowRadius = Mathf.Max(level.width, level.height) * .5f + DecorSpawner.Apron;
            // Decorative sequence isolated from gameplay RNG.
            _decorRng = new System.Random(level.decorSeed * 31 + 7);

            var shader = Shader.Find("QuietCamp/LeafParticle");
            if (shader == null) throw new InvalidOperationException("QuietCamp/LeafParticle shader missing.");
            _leafTex = Resources.Load<Texture2D>("QuietCamp/Atmosphere/Textures/leaves");
            _leafMat = new Material(shader) { name = "Leaves (runtime)" };
            if (_leafTex != null) _leafMat.SetTexture("_MainTex", _leafTex);
            _dotTex = SoftDot();
            _softMat = new Material(shader) { name = "SoftDot (runtime)" };
            _softMat.SetTexture("_MainTex", _dotTex);

            _leaves = CreateDriftingLeaves();
            _nearLeaf = CreateNearLeaf();
            _dust = CreateDust();
            _fireflies = CreateFireflies();
            _smoke = CreateSmoke();
            _mist = CreateMist();
            _initialized = true;
            ApplyProfile(profile);
        }

        // ─── Emitters ────────────────────────────────────────────────────────

        ParticleSystem BaseSystem(string name, int maxParticles, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = Mathf.Max(1, maxParticles);
            main.playOnAwake = false;
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            return ps;
        }

        /// <summary>Leaf atlas: each particle picks one of the four 2×2 cells at spawn.</summary>
        void RandomAtlasCell(ParticleSystem ps)
        {
            var sheet = ps.textureSheetAnimation;
            sheet.enabled = true;
            sheet.mode = ParticleSystemAnimationMode.Grid;
            sheet.numTilesX = 2; sheet.numTilesY = 2;
            sheet.startFrame = new ParticleSystem.MinMaxCurve(0f, 3.99f);
            sheet.frameOverTime = new ParticleSystem.MinMaxCurve(0f);
            sheet.cycleCount = 1;
        }

        ParticleSystem CreateDriftingLeaves()
        {
            int budget = Scale(_profile != null ? _profile.Leaves : 4, 2, 6, _tier);
            var ps = BaseSystem("AmbientLeaves", budget, _leafMat);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(6f, 10f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(.15f, .35f);
            main.startSize = new ParticleSystem.MinMaxCurve(.08f, .18f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, 6.28f);
            main.startColor = new Color(1f, 1f, 1f, .9f);
            var emission = ps.emission;
            emission.rateOverTime = .07f;
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(_meadowRadius * 1.2f, .8f, .2f);
            var rot = ps.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-.6f, .6f);
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(.9f, .15f),
                        new GradientAlphaKey(.9f, .8f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            RandomAtlasCell(ps);
            return ps;
        }

        ParticleSystem CreateNearLeaf()
        {
            int budget = Scale(_profile != null ? _profile.NearLeaf : 1, 0, 2, _tier);
            var ps = BaseSystem("NearLeaf", Mathf.Max(1, budget), _leafMat);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(4f, 6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(.3f, .5f);
            main.startSize = new ParticleSystem.MinMaxCurve(.25f, .45f);
            main.startColor = new Color(1f, 1f, 1f, .85f);
            var emission = ps.emission;
            emission.rateOverTime = 0f; // manual Emit() on a slow timer
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(1.5f, .6f, .2f);
            RandomAtlasCell(ps);
            return ps;
        }

        ParticleSystem CreateDust()
        {
            int budget = Scale(_profile != null ? _profile.Dust : 0, 0, 10, _tier);
            var ps = BaseSystem("DustMotes", Mathf.Max(1, budget), _softMat);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(3f, 6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(.03f, .08f);
            main.startSize = new ParticleSystem.MinMaxCurve(.015f, .045f);
            main.startColor = new Color(1f, .95f, .8f, .15f);
            var emission = ps.emission;
            emission.rateOverTime = budget > 0 ? budget / 4f : 0f;
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(_meadowRadius * 1.6f, 1.2f, _meadowRadius * 1.6f);
            return ps;
        }

        ParticleSystem CreateFireflies()
        {
            int budget = Scale(_profile != null ? _profile.Fireflies : 0, 0, 5, _tier);
            var ps = BaseSystem("Fireflies", Mathf.Max(1, budget), _softMat);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(6f, 9f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(.04f, .12f);
            main.startSize = new ParticleSystem.MinMaxCurve(.03f, .06f);
            main.startColor = new Color(.9f, 1f, .55f, .9f);
            var emission = ps.emission;
            emission.rateOverTime = budget > 0 ? budget / 6f : 0f;
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = _meadowRadius;
            shape.radiusThickness = .35f; // low bushes ring, not over the board
            // 2–4 s glow pulse via alpha over lifetime.
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(.95f, .3f),
                        new GradientAlphaKey(.15f, .55f), new GradientAlphaKey(.85f, .75f),
                        new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = .05f;
            noise.frequency = .3f;
            return ps;
        }

        ParticleSystem CreateSmoke()
        {
            int budget = Scale(_profile != null ? _profile.Smoke : 0, 0, 3, _tier);
            var ps = BaseSystem("FireSmoke", Mathf.Max(1, budget), _softMat);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(2f, 4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(.12f, .22f);
            main.startSize = new ParticleSystem.MinMaxCurve(.25f, .5f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, 6.28f);
            main.startColor = new Color(.75f, .75f, .8f, .12f);
            var emission = ps.emission;
            emission.rateOverTime = budget > 0 ? .7f : 0f;
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = .15f;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(.12f, .25f),
                        new GradientAlphaKey(.06f, .7f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var sizeOverLife = ps.sizeOverLifetime;
            sizeOverLife.enabled = true;
            sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f,
                new AnimationCurve(new Keyframe(0f, .6f), new Keyframe(1f, 1.6f)));
            return ps;
        }

        /// <summary>One distant mist quad behind the field — not a fullscreen particle system.</summary>
        Transform CreateMist()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "DistantMist";
            var col = go.GetComponent<Collider>();
            col.enabled = false;
            Destroy(col);
            _mistMat = new Material(_softMat) { name = "Mist (runtime)" };
            _mistMat.SetColor("_Tint", new Color(.85f, .9f, .92f, .10f));
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = _mistMat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            float w = _meadowRadius * 4f;
            go.transform.localScale = new Vector3(w, w * .35f, 1f);
            go.transform.position = new Vector3(0f, _meadowRadius * .45f, -_meadowRadius * 1.6f);
            _mistAnchor = go.transform.position;
            go.transform.rotation = Quaternion.Euler(35f, 0f, 0f);
            go.SetActive(false);
            return go.transform;
        }

        /// <summary>Generated radial soft-dot sprite for dust/fireflies/smoke/mist.</summary>
        static Texture2D SoftDot()
        {
            const int s = 64;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[s * s];
            for (var y = 0; y < s; y++)
                for (var x = 0; x < s; x++)
                {
                    var dx = x - s / 2f + .5f;
                    var dy = y - s / 2f + .5f;
                    var r = Mathf.Sqrt(dx * dx + dy * dy) / (s / 2f);
                    var a = Mathf.Clamp01(1f - r);
                    a *= a; // soft falloff
                    px[y * s + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }

        // ─── Phase / runtime ─────────────────────────────────────────────────

        public void ApplyProfile(AtmosphereCatalog.Profile profile)
        {
            _profile = profile;
            if (!_initialized) return;
            // Reduced motion silences every ambient emitter, not only leaves.
            bool quiet = _reducedMotion != null && _reducedMotion();
            _leafBudget = Scale(profile.Leaves, 2, 6, _tier);
            int dust = Scale(profile.Dust, 0, 10, _tier);
            int flies = Scale(profile.Fireflies, 0, 5, _tier);
            int smoke = Scale(profile.Smoke, 0, 3, _tier);
            _nearLeafBudget = Scale(profile.NearLeaf, 0, 2, _tier);
            SetMaxParticles(_leaves, _leafBudget);
            SetMaxParticles(_nearLeaf, _nearLeafBudget);
            SetMaxParticles(_dust, dust);
            SetMaxParticles(_fireflies, flies);
            SetMaxParticles(_smoke, smoke);
            if (_nearLeafBudget <= 0) _nearLeaf.Clear();
            // Emission off means off — maxParticles is not an on/off switch.
            SetEmission(_dust, !quiet && dust > 0 ? dust / 4f : 0f);
            SetEmission(_fireflies, !quiet && flies > 0 ? flies / 6f : 0f);
            SetEmission(_smoke, !quiet && smoke > 0 && _fireActive ? .7f : 0f);
            if (_mist != null) _mist.gameObject.SetActive(profile.Mist && _tier > Tier.Low);
        }

        static void SetMaxParticles(ParticleSystem ps, int max)
        {
            var m = ps.main;
            m.maxParticles = Mathf.Max(1, max);
            if (max <= 0) ps.Clear();
        }

        static void SetEmission(ParticleSystem ps, float rate)
        {
            var e = ps.emission;
            e.rateOverTime = rate;
            if (rate > 0f && !ps.isPlaying) ps.Play();
            else if (rate <= 0f && ps.isEmitting) ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        public void SetFire(Vector3 position, bool active)
        {
            _firePos = position;
            _fireActive = active;
            if (_smoke != null)
            {
                _smoke.transform.position = position + new Vector3(0f, .5f, 0f);
                if (_profile != null) ApplyProfile(_profile);
            }
        }

        void LateUpdate()
        {
            if (!_initialized || _camera == null) return;
            var host = GetComponentInParent<CampAtmosphere>();
            var wind = host != null ? host.Wind : default;
            bool reduced = _reducedMotion != null && _reducedMotion();

            // Drift direction = shared wind; spawn plane sits on the upwind edge.
            var dir = wind.DirectionXZ.sqrMagnitude > .001f ? wind.DirectionXZ : new Vector2(0f, 1f);
            var dir3 = new Vector3(dir.x, 0f, dir.y).normalized;
            if (reduced != _lastReduced)
            {
                _lastReduced = reduced;
                ApplyProfile(_profile); // re-gates every emitter at once
            }
            if (_leaves != null)
            {
                _leaves.transform.position = -dir3 * (_meadowRadius * .95f) + Vector3.up * .8f;
                _leaves.transform.rotation = Quaternion.LookRotation(dir3, Vector3.up);
                var emission = _leaves.emission;
                float rate = reduced || _leafBudget <= 0 ? 0f
                    : Mathf.Lerp(.04f, .10f, wind.Strength);
                emission.rateOverTime = rate;
                if (rate > 0f && !_leaves.isPlaying) _leaves.Play();
                else if (rate <= 0f && _leaves.isEmitting)
                    _leaves.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }

            // One rare big leaf drifting along a screen edge, never over the board.
            if (_nearLeaf != null && _tier > Tier.Low && !reduced && _nearLeafBudget > 0)
            {
                _nearLeafTimer -= Time.deltaTime;
                if (_nearLeafTimer <= 0f && _nearLeaf.particleCount < _nearLeaf.main.maxParticles)
                {
                    _nearLeafTimer = 25f + (float)_decorRng.NextDouble() * 20f;
                    var cam = _camera.transform;
                    var spawn = cam.position - cam.forward * (cam.position.y - 1.4f)
                        + cam.right * (-_camera.orthographicSize * _camera.aspect * 1.05f);
                    // Trajectory check, not just the start point: skip the
                    // emission when the drift path crosses the protected
                    // board viewport.
                    var end = spawn + dir3 * (.45f * 5f);
                    var prot = host != null ? host.ProtectedViewport : default;
                    var a = (Vector2)_camera.WorldToViewportPoint(spawn);
                    var b = (Vector2)_camera.WorldToViewportPoint(end);
                    var mid = (a + b) * .5f;
                    prot.xMin -= .03f; prot.yMin -= .03f;
                    prot.xMax += .03f; prot.yMax += .03f;
                    if (!prot.Contains(a) && !prot.Contains(mid) && !prot.Contains(b))
                    {
                        _nearLeaf.transform.position = spawn;
                        _nearLeaf.transform.rotation = Quaternion.LookRotation(dir3, Vector3.up);
                        _nearLeaf.Emit(1);
                    }
                }
            }

            // Smoke and dust follow the same wind so the world reads as one.
            if (_smoke != null && _smoke.particleCount > 0)
            {
                var vel = _smoke.velocityOverLifetime;
                vel.enabled = true;
                vel.space = ParticleSystemSimulationSpace.World;
                // The gust rides the wind direction — not a fixed diagonal.
                vel.x = new ParticleSystem.MinMaxCurve(dir3.x * (.2f + .05f * wind.GustEnvelope));
                vel.z = new ParticleSystem.MinMaxCurve(dir3.z * (.2f + .05f * wind.GustEnvelope));
            }
            if (_mist != null && _mist.gameObject.activeSelf)
            {
                // Bounded sway around the anchor — never drifts out of frame.
                if (!reduced)
                    _mistDrift += Time.deltaTime * .02f * (1f + wind.GustEnvelope);
                _mist.position = _mistAnchor
                    + dir3 * (Mathf.Sin(_mistDrift) * _meadowRadius * .18f);
            }
        }

        void OnDestroy()
        {
            Destroy(_leafMat); Destroy(_softMat); Destroy(_mistMat); Destroy(_dotTex);
        }
    }
}
