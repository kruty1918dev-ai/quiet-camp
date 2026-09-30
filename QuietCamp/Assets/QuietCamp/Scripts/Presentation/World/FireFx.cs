using UnityEngine;
namespace QuietCamp.Presentation.World
{
    /// <summary>
    /// Small owned campfire FX: a few warm billboards over the fire cell.
    /// Disabled entirely under reduced motion — no particle drift, no smoke storm.
    /// </summary>
    public static class FireFx
    {
        public static ParticleSystem Create(Transform parent)
        {
            var go = new GameObject("FireParticles");
            go.transform.SetParent(parent, false);
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
                return ps;
            }
            var material = new Material(shader);
            material.SetColor("_BaseColor", new Color(1f, 0.55f, 0.15f, 0.85f));
            material.SetFloat("_Surface", 1f);
            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            renderer.sharedMaterial = material;
            return ps;
        }
    }
}
