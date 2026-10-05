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
    /// Fixed small pools — no per-frame allocation, colliders or collision
    /// modules. Fire particles reuse forest depth on Balanced/High. Budgets come from the
    /// atmosphere spec (Balanced counts live in atmosphere.json).
    /// </summary>
    public sealed class AtmosphereParticles : MonoBehaviour
    {
        public enum Tier { Low = 0, Balanced = 1, High = 2 }

        Material _leafMat, _softMat;
        Texture2D _leafTex, _dotTex;
        ParticleSystem _leaves, _nearLeaf, _dust, _fireflies, _smoke, _embers;
        Material _fireMaterial, _emberMaterial;
        Texture2D _fireAtlas;
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
        bool _gustHooked;
        bool _winter,_autumn;

        /// <summary>Scene-owned veto for the rare near leaf: drag, an open
        /// modal or a scene transition block new flybys (spec §4.2/§9).
        /// Set by the host after Configure; null means never suppressed.</summary>
        public Func<bool> NearLeafSuppressed;

        // Reusable buffer for reduced-motion fast-fade — no per-toggle GC.
        static readonly ParticleSystem.Particle[] FadeBuffer
            = new ParticleSystem.Particle[32];

        // Balanced-tier counts per spec; Low/High scale per the effect table.
        // A phase profile value of 0 means OFF on every tier — scaling must
        // never resurrect an effect the phase disables (e.g. day fireflies).
        static int Scale(int balanced, int low, int high, Tier tier)
            => balanced <= 0 ? 0
             : tier == Tier.Low ? Math.Min(balanced, low)
             : tier == Tier.High ? Mathf.Max(balanced, high) : balanced;

        public void SetTier(Tier tier) { if(_tier==tier)return;_tier=tier;ApplyProfile(_profile); }

        public void Configure(Camera camera, LevelData level, AtmosphereCatalog.Profile profile,
            Tier tier, Func<bool> reducedMotion)
        {
            _camera = camera;
            _tier = tier;
            _reducedMotion = reducedMotion;
            _profile = profile;
            var season=SeasonProfile.For(level);_winter=season.Winter;_autumn=season.Autumn;
            _meadowRadius = Mathf.Max(level.width, level.height) * .5f + DecorSpawner.Apron;
            // Decorative sequence isolated from gameplay RNG.
            _decorRng = new System.Random(level.decorSeed * 31 + 7);

            var shader = Shader.Find("QuietCamp/LeafParticle");
            if (shader == null)
            {
                // Spec §11: a missing effect resource disables that decor with
                // one diagnostic — the game stays playable, no exception loop.
                Debug.LogError("[QuietCamp] LeafParticle shader missing — ambient particles disabled.");
                return;
            }
            _leafTex = Resources.Load<Texture2D>("QuietCamp/Atmosphere/Textures/leaves");
            _dotTex = SoftDot();
            if (_leafTex == null)
            {
                // One diagnostic, graceful degrade: leaves render as soft
                // dots instead of magenta quads (§11).
                Debug.LogWarning("[QuietCamp] Leaf atlas missing — leaves fall back to dots.");
                _leafTex = _dotTex;
            }
            _leafMat = new Material(shader) { name = "Leaves (runtime)" };
            _leafMat.SetTexture("_MainTex", _leafTex);
            _softMat = new Material(shader) { name = "SoftDot (runtime)" };
            _softMat.SetTexture("_MainTex", _dotTex);

            _leaves = CreateDriftingLeaves();
            _nearLeaf = CreateNearLeaf();
            _dust = CreateDust();
            _fireflies = CreateFireflies();
            _smoke = CreateSmoke();
            _fireAtlas = CozyParticleAtlas.Create();
            _fireMaterial = CozyParticleMaterial.Create(_fireAtlas, (int)_tier);
            _emberMaterial = CozyParticleMaterial.Create(_fireAtlas, (int)_tier, .75f);
            _smoke.GetComponent<ParticleSystemRenderer>().sharedMaterial = _fireMaterial;
            CozyParticleAtlas.Tile(_smoke, 0);
            _embers = BaseSystem("FireEmbers", 16, _emberMaterial);
            CozyParticleAtlas.Tile(_embers, 1);
            var emberMain = _embers.main; emberMain.startLifetime = new ParticleSystem.MinMaxCurve(1.4f, 2.3f);
            emberMain.startSpeed = new ParticleSystem.MinMaxCurve(.18f, .35f); emberMain.startSize = new ParticleSystem.MinMaxCurve(.035f, .065f);
            emberMain.startColor = new Color(1, .65f, .21f, .8f);
            var emberShape = _embers.shape; emberShape.shapeType = ParticleSystemShapeType.Cone; emberShape.radius = .16f; emberShape.angle = 12;
            _embers.transform.localRotation = Quaternion.Euler(-90, 0, 0);
            var emberFade = _embers.colorOverLifetime; emberFade.enabled = true;
            var emberGradient = new Gradient(); emberGradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(new Color(.9f, .34f, .10f), 1) },
                new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(.85f, .12f), new GradientAlphaKey(0, 1) }); emberFade.color = emberGradient;
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
            sheet.startFrame = new ParticleSystem.MinMaxCurve(0f, .999f);
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
            // Cap table (§10): at most one near leaf even on High.
            int budget = Scale(_profile != null ? _profile.NearLeaf : 1, 0, 1, _tier);
            var ps = BaseSystem("NearLeaf", Mathf.Max(1, budget), _leafMat);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(3f, 5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(.3f, .5f);
            // Screen-sized: ~3–6 % of viewport width, resolved through the
            // projection instead of a fixed camera-height distance (§4.2).
            float vw = _camera != null
                ? _camera.orthographicSize * 2f * _camera.aspect : 4f;
            float s = Mathf.Clamp(vw * .045f, .12f, .5f);
            main.startSize = new ParticleSystem.MinMaxCurve(s * .8f, s * 1.2f);
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
            // Cap table (§10): dust High cap is 8, not 10.
            int budget = Scale(_profile != null ? _profile.Dust : 0, 0, 8, _tier);
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
            // Local sunlit pocket at the meadow edge, not a uniform box over
            // the board (§3 protected zone, §4.3).
            shape.scale = new Vector3(_meadowRadius * .9f, 1f, _meadowRadius * .4f);
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
            ps.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
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
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(.65f, .25f),
                        new GradientAlphaKey(.32f, .7f), new GradientAlphaKey(0f, 1f) });
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
            go.transform.SetParent(transform, false);
            var col = go.GetComponent<Collider>();
            if (col != null) { col.enabled = false; Destroy(col); }
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
            CozyParticleMaterial.ApplyTier(_fireMaterial, (int)_tier);
            CozyParticleMaterial.ApplyTier(_emberMaterial, (int)_tier);
            // Reduced motion silences every ambient emitter, not only leaves.
            bool quiet = _reducedMotion != null && _reducedMotion();
            _leafBudget = _winter||_autumn?0:Scale(profile.Leaves, 2, 6, _tier);
            int dust = Scale(profile.Dust, 0, 8, _tier);
            int flies = _winter?0:Scale(profile.Fireflies, 0, 5, _tier);
            int smoke = Scale(profile.Smoke, 0, 3, _tier);
            _nearLeafBudget = _winter||_autumn?0:Scale(profile.NearLeaf, 0, 1, _tier);
            SetMaxParticles(_leaves, _leafBudget);
            SetMaxParticles(_nearLeaf, _nearLeafBudget);
            SetMaxParticles(_dust, dust);
            SetMaxParticles(_fireflies, flies);
            SetMaxParticles(_smoke, smoke);
            if (_nearLeafBudget <= 0) _nearLeaf.Clear();
            if(_leafBudget<=0)_leaves.Clear();if(flies<=0)_fireflies.Clear();
            // Emission off means off — maxParticles is not an on/off switch.
            // Under reduced motion the policy is: dust and smoke off, fireflies
            // keep living as rare static faint points (spec §9).
            SetEmission(_dust, !quiet && dust > 0 ? dust / 4f : 0f);
            SetEmission(_fireflies, flies > 0 ? flies / (quiet ? 18f : 6f) : 0f);
            SetEmission(_smoke, !quiet && smoke > 0 && _fireActive ? .7f : 0f);
            SetFirefliesCalm(quiet);
            SetMaxParticles(_embers, _tier == Tier.Low ? 4 : _tier == Tier.Balanced ? 8 : 16);
            SetEmission(_embers, !quiet && _fireActive ? (_tier == Tier.Low ? .45f : _tier == Tier.Balanced ? 1.2f : 2f) : 0f);
            if (quiet) _embers.Clear();
            if (_mist != null) _mist.gameObject.SetActive(profile.Mist);
        }

        /// <summary>Reduced-motion fireflies: the glow pulse and noise drift
        /// stop; a few almost-static faint points remain (spec §9).</summary>
        void SetFirefliesCalm(bool calm)
        {
            if (_fireflies == null) return;
            var main = _fireflies.main;
            main.startSpeed = calm ? new ParticleSystem.MinMaxCurve(0f)
                : new ParticleSystem.MinMaxCurve(.04f, .12f);
            main.startColor = calm
                ? new Color(.9f, 1f, .55f, .35f) : new Color(.9f, 1f, .55f, .9f);
            var col = _fireflies.colorOverLifetime;
            col.enabled = !calm;
            var noise = _fireflies.noise;
            noise.enabled = !calm;
        }

        /// <summary>Lets already-living particles finish within ~0.3 s —
        /// the reduced-motion fast-fade (spec §9). Toggle-time only.</summary>
        static void FastFade(ParticleSystem ps)
        {
            if (ps == null) return;
            int n = Mathf.Min(ps.particleCount, FadeBuffer.Length);
            if (n <= 0) return;
            ps.GetParticles(FadeBuffer, n);
            for (int i = 0; i < n; i++)
                FadeBuffer[i].remainingLifetime
                    = Mathf.Min(FadeBuffer[i].remainingLifetime, .3f);
            ps.SetParticles(FadeBuffer, n);
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
                _embers.transform.position = position + Vector3.up * .36f;
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
            if (!_gustHooked && host != null)
            {
                host.GustStarted += OnGust;
                _gustHooked = true;
            }
            if (reduced != _lastReduced)
            {
                _lastReduced = reduced;
                ApplyProfile(_profile); // re-gates every emitter at once
                if (reduced)
                {
                    // Living particles finish within ~0.3 s instead of
                    // floating on for their full lifetime (spec §9).
                    FastFade(_leaves);
                    FastFade(_nearLeaf);
                    FastFade(_dust);
                    FastFade(_smoke);
                }
            }
            if (_leaves != null)
            {
                _leaves.transform.position = -dir3 * (_meadowRadius * .95f) + Vector3.up * .8f;
                _leaves.transform.rotation = Quaternion.LookRotation(dir3, Vector3.up);
                var emission = _leaves.emission;
                // Spec §4.1: roughly one leaf per 12–25 s across the scene.
                float rate = reduced || _leafBudget <= 0 ? 0f
                    : Mathf.Lerp(.04f, .083f, wind.Strength);
                emission.rateOverTime = rate;
                if (rate > 0f && !_leaves.isPlaying) _leaves.Play();
                else if (rate <= 0f && _leaves.isEmitting)
                    _leaves.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }

            // One rare big leaf drifting along a screen edge, never over the
            // board — and never during a drag, a modal or a transition (§4.2).
            bool suppressed = NearLeafSuppressed != null && NearLeafSuppressed();
            if (suppressed && _nearLeafTimer < 5f)
                _nearLeafTimer = 5f; // no flyby the moment a modal closes
            if (_nearLeaf != null && _tier > Tier.Low && !reduced
                && _nearLeafBudget > 0 && !suppressed)
            {
                _nearLeafTimer -= Time.deltaTime;
                if (_nearLeafTimer <= 0f && _nearLeaf.particleCount < _nearLeaf.main.maxParticles)
                {
                    _nearLeafTimer = 30f + (float)_decorRng.NextDouble() * 30f;
                    var cam = _camera.transform;
                    // Project the camera forward ray onto the leaf plane
                    // instead of using camera height as a depth guess (§2.5).
                    float drop = (cam.position.y - 1.2f)
                        / Mathf.Max(.05f, -cam.forward.y);
                    var center = cam.position + cam.forward * drop;
                    var spawn = center
                        + cam.right * (-_camera.orthographicSize * _camera.aspect * 1.05f);
                    // Trajectory check, not just the start point: skip the
                    // emission when the drift path crosses the protected
                    // board viewport.
                    var end = spawn + dir3 * (.45f * 4f);
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

            // Dust lives in the sunlit upwind pocket — the box hugs the rim
            // and its thin axis points downwind so motes never veil the
            // board cells (§3).
            if (_dust != null)
            {
                _dust.transform.position = -dir3 * (_meadowRadius * .9f)
                    + Vector3.up * .4f;
                _dust.transform.rotation = Quaternion.LookRotation(dir3, Vector3.up);
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
            if (_embers != null)
            {
                var velocity = _embers.velocityOverLifetime;
                velocity.enabled = true;
                velocity.space = ParticleSystemSimulationSpace.World;
                velocity.x = dir3.x * wind.Strength * .12f;
                velocity.z = dir3.z * wind.Strength * .12f;
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

        /// <summary>One gust may tear off a single leaf when the budget has
        /// room — the leaf sequence row in spec §6. Never a burst.</summary>
        void OnGust()
        {
            if (_leaves == null || _leafBudget <= 0) return;
            if (_reducedMotion != null && _reducedMotion()) return;
            if (_leaves.particleCount < _leaves.main.maxParticles)
                _leaves.Emit(1);
        }

        void OnDestroy()
        {
            if (_gustHooked)
            {
                var host = GetComponentInParent<CampAtmosphere>();
                if (host != null) host.GustStarted -= OnGust;
            }
            Destroy(_leafMat); Destroy(_softMat); Destroy(_mistMat); Destroy(_dotTex);
            Destroy(_fireMaterial); Destroy(_emberMaterial); Destroy(_fireAtlas);
        }
    }
}
