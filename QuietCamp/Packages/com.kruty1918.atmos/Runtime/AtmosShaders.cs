using UnityEngine;

namespace Kruty1918.Atmos
{
    /// <summary>
    /// Central shader registry for the package: stable shader names plus a
    /// tolerant Shader.Find wrapper that warns once instead of failing silently.
    /// Reference these constants instead of string literals so renames stay safe.
    /// </summary>
    public static class AtmosShaders
    {
        public const string SkyGradient = "Atmos/SkyGradient";
        public const string FoliageSway = "Atmos/FoliageSway";
        public const string FireGlow = "Atmos/FireGlow";
        public const string FireFlame = "Atmos/FireFlame";

        /// <summary>Finds a shader; logs a warning when it is not in the build.</summary>
        public static Shader Find(string name)
        {
            var shader = Shader.Find(name);
            if (shader == null)
                Debug.LogWarning($"[Atmos] Shader '{name}' not found — check Always-Included shaders for device builds.");
            return shader;
        }

        /// <summary>Creates a material on a named Atmos shader (null-safe).</summary>
        public static Material NewMaterial(string shaderName, string materialName = null)
        {
            var shader = Find(shaderName);
            if (shader == null) return null;
            var mat = new Material(shader);
            if (materialName != null) mat.name = materialName;
            return mat;
        }
    }
}
