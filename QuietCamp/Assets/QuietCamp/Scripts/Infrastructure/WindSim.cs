using System;
using UnityEngine;

namespace QuietCamp.Infrastructure
{
    /// <summary>
    /// Shared breeze/gust simulation owned by the scene atmosphere (not a
    /// singleton): seeded PRNG, slow direction drift and one gust envelope
    /// (attack → hold → release) that foliage, particles and audio all read.
    /// A deterministic pure class — Advance(dt) is the only mutator.
    /// </summary>
    public sealed class WindSim
    {
        public readonly struct Snapshot
        {
            public readonly Vector2 DirectionXZ;   // normalized
            public readonly float BaseStrength;    // 0..1
            public readonly float GustEnvelope;    // 0..1
            public readonly float Strength;        // base + envelope * gustAmp, 0..1 clamped
            public readonly float PhaseSeconds;
            public Snapshot(Vector2 dir, float baseStr, float env, float str, float phase)
            { DirectionXZ = dir; BaseStrength = baseStr; GustEnvelope = env; Strength = str; PhaseSeconds = phase; }
        }

        const float Attack = .8f, HoldMin = .5f, HoldMax = 1f, ReleaseMin = 1.5f, ReleaseMax = 2f;
        const float GustAmpMin = .15f, GustAmpMax = .25f;
        const float MaxTurnDegPerSec = .5f; // ≤15° per 30 s

        readonly System.Random _rng;
        float _base, _gustMin, _gustMax;
        float _dirDeg, _targetDeg;
        float _clock, _nextGust;
        float _gustT = -1f, _hold, _release, _amp;
        WindShelterField _shelter;
        Snapshot _latest;

        /// <summary>Fired once when a gust's attack phase begins.</summary>
        public event Action GustStarted;

        public WindSim(int seed, float baseStrength, float gustMin, float gustMax, float directionDeg = 130f)
        {
            _rng = new System.Random(seed);
            _base = Mathf.Clamp01(baseStrength);
            _gustMin = Mathf.Max(1f, gustMin);
            _gustMax = Mathf.Max(_gustMin, gustMax);
            _dirDeg = _targetDeg = directionDeg;
            _nextGust = Mathf.Lerp(_gustMin, _gustMax, (float)_rng.NextDouble());
            _latest = Capture();
        }

        public float BaseStrength => _base;
        public bool GustActive => _gustT >= 0f;
        public void SetShelter(WindShelterField shelter) => _shelter = shelter;
        public WindFieldMath.Sample Sample(Vector3 worldPosition, float height)
            => WindFieldMath.Evaluate(_latest.DirectionXZ, _latest.Strength, _latest.PhaseSeconds, worldPosition, height, _shelter);

        /// <summary>Sets a new base strength with the caller's own smoothing.</summary>
        public void SetBase(float strength) => _base = Mathf.Clamp01(strength);

        /// <summary>Reschedules gust cadence when the phase profile changes.
        /// A pending idle gust is re-rolled inside the new bounds so a phase
        /// switch can't leave a stale long wait behind.</summary>
        public void SetGustInterval(float min, float max)
        {
            _gustMin = Mathf.Max(1f, min);
            _gustMax = Mathf.Max(_gustMin, max);
            if (_gustT < 0f)
                _nextGust = Mathf.Lerp(_gustMin, _gustMax, (float)_rng.NextDouble());
        }

        public Snapshot Advance(float dt)
        {
            if (dt <= 0f) return _latest;
            _clock += dt;

            // Direction: approach a slowly-wandering target within the turn cap.
            float delta = Mathf.DeltaAngle(_dirDeg, _targetDeg);
            float step = Mathf.Clamp(delta, -MaxTurnDegPerSec * dt, MaxTurnDegPerSec * dt);
            _dirDeg += step;
            if (Mathf.Abs(delta) < 1f && _rng.NextDouble() < dt / 12f)
                _targetDeg = _dirDeg + (float)(_rng.NextDouble() * 30f - 15f);

            // Gust state machine: idle → attack → hold → release → idle.
            if (_gustT < 0f)
            {
                _nextGust -= dt;
                if (_nextGust <= 0f)
                {
                    _gustT = 0f;
                    _hold = Mathf.Lerp(HoldMin, HoldMax, (float)_rng.NextDouble());
                    _release = Mathf.Lerp(ReleaseMin, ReleaseMax, (float)_rng.NextDouble());
                    _amp = Mathf.Lerp(GustAmpMin, GustAmpMax, (float)_rng.NextDouble());
                    GustStarted?.Invoke();
                }
            }

            float env = 0f;
            if (_gustT >= 0f)
            {
                _gustT += dt;
                if (_gustT < Attack) env = _gustT / Attack;
                else if (_gustT < Attack + _hold) env = 1f;
                else if (_gustT < Attack + _hold + _release)
                    env = 1f - (_gustT - Attack - _hold) / _release;
                else
                {
                    _gustT = -1f;
                    _nextGust = Mathf.Lerp(_gustMin, _gustMax, (float)_rng.NextDouble());
                }
            }
            _latest = Capture(env);
            return _latest;
        }

        Snapshot Capture(float env = 0f)
        {
            if (_gustT >= 0f) env = Mathf.Clamp01(env);
            float rad = _dirDeg * Mathf.Deg2Rad;
            var dir = new Vector2(Mathf.Sin(rad), Mathf.Cos(rad)); // yaw→XZ plane
            return new Snapshot(dir, _base, env, Mathf.Clamp01(_base + env * _amp), _clock);
        }
    }
}
