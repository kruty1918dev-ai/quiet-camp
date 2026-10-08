using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Kruty1918.Atmos
{
    /// <summary>
    /// Named sky presets on one component with instant and animated switching.
    /// A single runtime material is lerped during transitions, so blending
    /// day→evening costs nothing extra. Add the component to any scene object,
    /// fill <see cref="skies"/>, then call <see cref="Set(string)"/> or
    /// <see cref="TransitionTo(string,float)"/>.
    /// </summary>
    public sealed class SkyController : MonoBehaviour
    {
        [Serializable]
        public sealed class NamedSky
        {
            public string name;
            public SkySpec spec = new SkySpec();
        }

        [Tooltip("Sky presets available to Set/TransitionTo. First entry is applied on Start when applyOnStart is set.")]
        public List<NamedSky> skies = new List<NamedSky>();

        [Tooltip("Apply this sky on Start (empty = first entry, 'none' = skip).")]
        public string applyOnStart;

        public Camera targetCamera;

        Material _runtimeMat;
        Coroutine _transition;
        SkySpec _current;

        /// <summary>The material currently installed as the scene skybox.</summary>
        public Material CurrentMaterial => _runtimeMat;
        public string CurrentName { get; private set; }

        void Start()
        {
            var wanted = string.IsNullOrEmpty(applyOnStart)
                ? (skies.Count > 0 ? skies[0].name : null)
                : applyOnStart;
            if (wanted != null && !string.Equals(wanted, "none", StringComparison.OrdinalIgnoreCase))
                Set(wanted);
        }

        public SkySpec Find(string name)
        {
            foreach (var s in skies)
                if (string.Equals(s.name, name, StringComparison.OrdinalIgnoreCase)) return s.spec;
            return null;
        }

        /// <summary>Instantly applies a named preset (or a raw spec).</summary>
        public void Set(string name)
        {
            var spec = Find(name);
            if (spec == null)
            {
                Debug.LogWarning($"[Atmos] SkyController: sky '{name}' not found.");
                return;
            }
            Set(spec);
            CurrentName = name;
        }

        /// <summary>Instantly applies a spec.</summary>
        public void Set(SkySpec spec)
        {
            if (_transition != null) { StopCoroutine(_transition); _transition = null; }
            _current = spec;
            EnsureMaterial();
            spec.ApplyTo(_runtimeMat);
            Sky.Apply(_runtimeMat, targetCamera, spec);
        }

        /// <summary>Smoothly blends from the current sky to a named preset.</summary>
        public void TransitionTo(string name, float duration = 2f)
        {
            var spec = Find(name);
            if (spec == null)
            {
                Debug.LogWarning($"[Atmos] SkyController: sky '{name}' not found.");
                return;
            }
            TransitionTo(spec, duration);
            CurrentName = name;
        }

        /// <summary>Smoothly blends from the current sky to a spec.</summary>
        public void TransitionTo(SkySpec target, float duration = 2f)
        {
            if (!isActiveAndEnabled || duration <= 0f) { Set(target); return; }
            if (_transition != null) StopCoroutine(_transition);
            _transition = StartCoroutine(Blend(_current ?? new SkySpec(), target, duration));
        }

        IEnumerator Blend(SkySpec from, SkySpec to, float duration)
        {
            EnsureMaterial();
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                var spec = SkySpec.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / duration));
                spec.ApplyTo(_runtimeMat);
                Sky.ApplyEnvironment(spec);
                yield return null;
            }
            _transition = null;
            Set(to);
        }

        void EnsureMaterial()
        {
            if (_runtimeMat != null) return;
            var shader = AtmosShaders.Find(AtmosShaders.SkyGradient);
            if (shader == null)
            {
                Debug.LogError("[Atmos] SkyGradient shader missing — is the Atmos package installed?");
                enabled = false;
                return;
            }
            _runtimeMat = new Material(shader) { name = "AtmosSky (Runtime)" };
        }
    }
}
