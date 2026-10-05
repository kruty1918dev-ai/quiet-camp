using System;
using QuietCamp.Domain;
using UnityEngine;
using UnityEngine.Rendering;
namespace QuietCamp.Presentation.World
{
    /// <summary>A real optional/free reward: emissive lantern and one bounded
    /// firefly pool. No additional light or puzzle collision.</summary>
    public sealed class CampLantern : MonoBehaviour
    {
        GameObject _body;
        Material _frame, _glow, _particlesMaterial;
        Texture2D _atlas;
        ParticleSystem _flies;
        Func<bool> _visible, _reduced;
        Func<int> _tier;
        bool _active;
        public void Configure(LevelData level, Func<bool> visible, Func<bool> reduced, Func<int> tier)
        {
            _visible = visible; _reduced = reduced; _tier = tier;
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit == null) { enabled = false; return; }
            _frame = new Material(lit); _frame.SetColor("_BaseColor", new Color(.25f, .32f, .23f));
            _glow = new Material(lit); _glow.SetColor("_BaseColor", new Color(.96f, .70f, .26f));
            _glow.EnableKeyword("_EMISSION"); _glow.SetColor("_EmissionColor", new Color(1.2f, .63f, .17f));
            _body = new GameObject("FireflyLantern"); _body.transform.SetParent(transform, false);
            _body.transform.localPosition = new Vector3(-level.width * .5f - .9f, .035f, .8f);
            Shape(PrimitiveType.Cylinder, "Base", new Vector3(0, .055f, 0), new Vector3(.38f, .04f, .38f), _frame);
            Shape(PrimitiveType.Cylinder, "WarmGlass", new Vector3(0, .21f, 0), new Vector3(.25f, .14f, .25f), _glow);
            Shape(PrimitiveType.Cylinder, "Cap", new Vector3(0, .375f, 0), new Vector3(.36f, .035f, .36f), _frame);
            for (int i = 0; i < 4; i++)
            {
                float a = i * Mathf.PI * .5f;
                Shape(PrimitiveType.Cube, "Frame", new Vector3(Mathf.Cos(a) * .135f, .21f, Mathf.Sin(a) * .135f), new Vector3(.025f, .32f, .025f), _frame);
            }
            var go = new GameObject("LanternFireflies"); go.transform.SetParent(_body.transform, false); go.transform.localPosition = Vector3.up * .28f;
            _flies = go.AddComponent<ParticleSystem>(); _flies.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = _flies.main; main.loop = true; main.playOnAwake = false; main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 8; main.startLifetime = 4.5f; main.startSpeed = .04f; main.startSize = .055f; main.startColor = new Color(1, .84f, .37f, .72f);
            var shape = _flies.shape; shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = .28f;
            var emission = _flies.emission; emission.rateOverTime = .9f;
            var noise = _flies.noise; noise.enabled = true; noise.strength = .08f; noise.frequency = .3f; noise.scrollSpeed = .13f; noise.quality = ParticleSystemNoiseQuality.Low;
            var color = _flies.colorOverLifetime; color.enabled = true;
            var gradient = new Gradient(); gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(.85f, .35f), new GradientAlphaKey(.7f, .7f), new GradientAlphaKey(0, 1) }); color.color = gradient;
            _atlas = CozyParticleAtlas.Create(); _particlesMaterial = CozyParticleMaterial.Create(_atlas, _tier?.Invoke() ?? 0, .65f);
            var renderer = go.GetComponent<ParticleSystemRenderer>(); renderer.sharedMaterial = _particlesMaterial; renderer.shadowCastingMode = ShadowCastingMode.Off;
            CozyParticleAtlas.Tile(_flies, 1); _body.SetActive(false);
        }
        void Shape(PrimitiveType type, string name, Vector3 position, Vector3 scale, Material material)
        {
            var go = GameObject.CreatePrimitive(type); go.name = name; go.transform.SetParent(_body.transform, false);
            go.transform.localPosition = position; go.transform.localScale = scale; Destroy(go.GetComponent<Collider>());
            go.GetComponent<Renderer>().sharedMaterial = material;
        }
        void Update()
        {
            if (_body == null) return;
            bool active = _visible?.Invoke() ?? false;
            if (active != _active) { _active = active; _body.SetActive(active); }
            if (!active) return;
            bool reduced = _reduced?.Invoke() ?? false;
            int tier = Mathf.Clamp(_tier?.Invoke() ?? 0, 0, 2);
            CozyParticleMaterial.ApplyTier(_particlesMaterial, tier);
            var main = _flies.main; main.maxParticles = tier == 0 ? 3 : tier == 1 ? 8 : 12;
            if (reduced) _flies.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            else if (!_flies.isPlaying) _flies.Play();
        }
        void OnDestroy()
        { if (_frame != null) Destroy(_frame); if (_glow != null) Destroy(_glow); if (_particlesMaterial != null) Destroy(_particlesMaterial); if (_atlas != null) Destroy(_atlas); }
    }
}
