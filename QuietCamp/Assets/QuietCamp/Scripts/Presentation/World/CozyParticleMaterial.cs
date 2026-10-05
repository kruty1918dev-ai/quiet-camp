using UnityEngine;

namespace QuietCamp.Presentation.World
{
    public static class CozyParticleMaterial
    {
        public static Material Create(Texture texture, int tier, float glow = 0)
        {
            var shader = Shader.Find("QuietCamp/CozyParticle");
            if (shader == null || !shader.isSupported) shader = Shader.Find("QuietCamp/LeafParticle");
            if (shader == null) return null;
            var material = new Material(shader) { name = "Cozy particles (owned)", mainTexture = texture };
            if (material.HasProperty("_Glow")) material.SetFloat("_Glow", glow);
            ApplyTier(material, tier);
            return material;
        }
        public static void ApplyTier(Material material, int tier)
        {
            if (material != null && material.HasProperty("_SoftDepth")) material.SetFloat("_SoftDepth", tier > 0 ? 1 : 0);
        }
    }
}
