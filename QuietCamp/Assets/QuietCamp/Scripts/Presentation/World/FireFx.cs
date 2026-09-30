using UnityEngine;
namespace QuietCamp.Presentation.World
{
    /// <summary>
    /// Small owned campfire FX living under one "FireFx" child: procedural
    /// flame quads + radial glow + flickering point light, plus a few warm
    /// particle billboards. Burning visuals are evening-only per the scene
    /// contract — daytime shows just the stone ring and log stack.
    /// Disabled entirely under reduced motion — no particle drift.
    /// All shaders live in Always-Included so device builds keep the fire.
    /// </summary>
    public static class FireFx
    {
        public static FireVisual Create(Transform parent)
        {
            var fxRoot = new GameObject("FireFx");
            fxRoot.transform.SetParent(parent, false);
            var visual = fxRoot.AddComponent<FireVisual>();

            var go = new GameObject("FireParticles");
            go.transform.SetParent(fxRoot.transform, false);
            go.transform.localPosition = new Vector3(0f, 0.10f, 0f);
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.7f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.15f, 0.3f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.15f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(1f, 0.62f, 0.18f, 0.9f), new Color(1f, 0.4f, 0.1f, 0.4f));
            main.maxParticles = 12;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            var emission = ps.emission;
            emission.rateOverTime = 10f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.12f;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0f, 1f) });
            col.color = gradient;
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null)
            {
                Debug.LogWarning("[QuietCamp] Particles/Unlit shader missing; campfire FX disabled");
                return visual;
            }
            var material = new Material(shader);
            material.SetColor("_BaseColor", new Color(1f, 0.55f, 0.15f, 0.85f));
            material.SetFloat("_Surface", 1f);
            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            renderer.sharedMaterial = material;

            AddGlow(fxRoot.transform);
            AddFlames(fxRoot.transform);
            AddLight(fxRoot.transform);
            return visual;
        }

        /// <summary>Warm radial glow lying on the ground under the logs.</summary>
        static void AddGlow(Transform parent)
        {
            var shader = Shader.Find("QuietCamp/FireGlow");
            if (shader == null) return;
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "FireGlow";
            quad.transform.SetParent(parent, false);
            quad.transform.localPosition = new Vector3(0f, 0.06f, 0f);
            quad.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            quad.transform.localScale = Vector3.one * 1.6f;
            var col = quad.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);
            var mat = new Material(shader);
            mat.SetColor("_Color", new Color(1f, 0.55f, 0.2f, 0.4f));
            mat.SetFloat("_Intensity", 1.0f);
            mat.SetFloat("_Seed", 3.7f);
            quad.GetComponent<Renderer>().sharedMaterial = mat;
        }

        /// <summary>Two crossed procedural flame quads — readable from any side.</summary>
        static void AddFlames(Transform parent)
        {
            var shader = Shader.Find("QuietCamp/FireFlame");
            if (shader == null) return;
            for (var i = 0; i < 2; i++)
            {
                var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                quad.name = "Flame" + i;
                quad.transform.SetParent(parent, false);
                quad.transform.localPosition = new Vector3(0f, 0.42f, 0f);
                quad.transform.localRotation = Quaternion.Euler(0f, 45f + 90f * i, 0f);
                quad.transform.localScale = new Vector3(0.7f, 1.0f, 0.7f);
                var col = quad.GetComponent<Collider>();
                if (col != null) Object.Destroy(col);
                var mat = new Material(shader);
                mat.SetFloat("_Seed", 11f * i + 2f);
                quad.GetComponent<Renderer>().sharedMaterial = mat;
            }
        }

        /// <summary>Warm point light with Perlin flicker — the "volume" of the fire.</summary>
        static void AddLight(Transform parent)
        {
            var go = new GameObject("FireLight");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, 0.8f, 0f);
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.639f, 0.29f); // #FFA34A per scene contract
            light.intensity = 1.2f;
            light.range = 3f;
            light.shadows = LightShadows.None;
            go.AddComponent<FireFlicker>();
        }
    }

    /// <summary>
    /// Marker on a campfire's FX root; the scene host toggles burning
    /// visuals (flames, glow, embers, point light) with the lighting preset.
    /// </summary>
    public sealed class FireVisual : MonoBehaviour
    {
        public void SetBurning(bool burning) => gameObject.SetActive(burning);
    }

    /// <summary>Subtle Perlin flicker on a fire light.</summary>
    public sealed class FireFlicker : MonoBehaviour
    {
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
            var n = Mathf.PerlinNoise(Time.time * 2.6f, 0.31f);
            _light.intensity = _base * (0.72f + 0.55f * n);
        }
    }
}
