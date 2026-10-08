using UnityEngine;

namespace Kruty1918.Atmos
{
    /// <summary>
    /// One-call procedural campfire: creates a "CampfireFx" child containing a
    /// radial ground glow, crossed procedural flame quads, drifting ember
    /// particles and a flickering point light. No textures required — flames
    /// and glow are generated in-shader.
    ///
    /// The returned <see cref="FireVisual"/> toggles the whole effect, which
    /// makes it easy to show an unlit fire ring by day and a burning fire at
    /// night. Particle shaders come from URP Particles/Unlit when available
    /// and fall back to Sprites/Default.
    /// </summary>
    public static class Campfire
    {
        public static FireVisual Create(Transform parent, CampfireSpec spec = null)
        {
            spec ??= CampfireSpec.Cozy;
            var fxRoot = new GameObject("CampfireFx");
            fxRoot.transform.SetParent(parent, false);
            var visual = fxRoot.AddComponent<FireVisual>();

            AddEmbers(fxRoot.transform, spec);
            AddGlow(fxRoot.transform, spec);
            AddFlames(fxRoot.transform, spec);
            AddLight(fxRoot.transform, spec);
            return visual;
        }

        /// <summary>Drifting warm embers above the logs.</summary>
        static void AddEmbers(Transform parent, CampfireSpec spec)
        {
            if (spec.emberCount <= 0) return;
            var go = new GameObject("FireParticles");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, 0.10f, 0f);
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.7f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.15f, 0.3f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.15f);
            main.startColor = new ParticleSystem.MinMaxGradient(spec.emberColorA, spec.emberColorB);
            main.maxParticles = spec.emberCount;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            var emission = ps.emission;
            emission.rateOverTime = spec.emberRate;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.12f;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(spec.emberColorA.a, 0f), new GradientAlphaKey(0f, 1f) });
            col.color = gradient;

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                Debug.LogWarning("[Atmos] No particle shader available; campfire embers disabled");
                return;
            }
            var material = new Material(shader);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", spec.emberColorA);
            if (material.HasProperty("_Color")) material.SetColor("_Color", spec.emberColorA);
            if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
            if (material.HasProperty("_SrcBlend"))
            {
                material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            }
            if (material.HasProperty("_ZWrite")) material.SetInt("_ZWrite", 0);
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            renderer.sharedMaterial = material;
        }

        /// <summary>Warm radial glow lying on the ground under the logs.</summary>
        static void AddGlow(Transform parent, CampfireSpec spec)
        {
            var mat = AtmosShaders.NewMaterial(AtmosShaders.FireGlow);
            if (mat == null) return;
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "FireGlow";
            quad.transform.SetParent(parent, false);
            quad.transform.localPosition = new Vector3(0f, 0.06f, 0f);
            quad.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            quad.transform.localScale = Vector3.one * spec.glowRadius;
            var col = quad.GetComponent<Collider>();
            if (col != null) DestroyCollider(col);
            mat.SetColor("_Color", spec.glowColor);
            mat.SetFloat("_Intensity", spec.glowIntensity);
            mat.SetFloat("_Seed", 3.7f);
            quad.GetComponent<Renderer>().sharedMaterial = mat;
        }

        /// <summary>Crossed procedural flame quads — readable from any side.</summary>
        static void AddFlames(Transform parent, CampfireSpec spec)
        {
            var shader = AtmosShaders.Find(AtmosShaders.FireFlame);
            if (shader == null) return;
            for (var i = 0; i < spec.flameQuads; i++)
            {
                var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                quad.name = "Flame" + i;
                quad.transform.SetParent(parent, false);
                quad.transform.localPosition = new Vector3(0f, spec.flameHeight, 0f);
                quad.transform.localRotation = Quaternion.Euler(0f, 45f + (180f / Mathf.Max(1, spec.flameQuads)) * i, 0f);
                quad.transform.localScale = spec.flameScale;
                var col = quad.GetComponent<Collider>();
                if (col != null) DestroyCollider(col);
                var mat = new Material(shader);
                mat.SetFloat("_Seed", 11f * i + 2f);
                quad.GetComponent<Renderer>().sharedMaterial = mat;
            }
        }

        static void DestroyCollider(Collider col)
        {
            if (Application.isPlaying) Object.Destroy(col);
            else Object.DestroyImmediate(col);
        }

        /// <summary>Warm point light with Perlin flicker — the "volume" of the fire.</summary>
        static void AddLight(Transform parent, CampfireSpec spec)
        {
            var go = new GameObject("FireLight");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, spec.lightHeight, 0f);
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = spec.lightColor;
            light.intensity = spec.lightIntensity;
            light.range = spec.lightRange;
            light.shadows = LightShadows.None;
            var flicker = go.AddComponent<FireFlicker>();
            flicker.strength = spec.flicker;
        }
    }

    /// <summary>
    /// Marker on a campfire's FX root; toggles burning visuals (flames, glow,
    /// embers, point light) so a scene can show the same fire lit or unlit.
    /// </summary>
    public sealed class FireVisual : MonoBehaviour
    {
        public void SetBurning(bool burning) => gameObject.SetActive(burning);
    }

    /// <summary>Subtle Perlin flicker on a fire light.</summary>
    public sealed class FireFlicker : MonoBehaviour
    {
        [Range(0f, 1f)] public float strength = 0.55f;
        [Range(0.5f, 8f)] public float speed = 2.6f;

        Light _light;
        float _base;

        void Awake()
        {
            _light = GetComponent<Light>();
            _base = _light != null ? _light.intensity : 1f;
        }

        void Update()
        {
            if (_light == null) return;
            var n = Mathf.PerlinNoise(Time.time * speed, 0.31f);
            _light.intensity = _base * (1f - strength * 0.5f + strength * n);
        }
    }
}
