using UnityEngine;
using UnityEngine.Rendering;

namespace QuietCamp.Infrastructure
{
    /// <summary>Diffuse forest illumination baked from a CC0 HDRI. No HDR texture is needed by the player.</summary>
    public sealed class ForestLightingEnvironment : ScriptableObject
    {
        public float[] coefficients = new float[27];
        public string source;
        public SphericalHarmonicsL2 Probe(Color ambient, float weather)
        {
            var result = new SphericalHarmonicsL2();
            var tint = ambient.linear * Mathf.Lerp(.82f, .68f, weather);
            for (int channel = 0; channel < 3; channel++)
                for (int coefficient = 0; coefficient < 9; coefficient++)
                    result[channel, coefficient] = coefficients[channel * 9 + coefficient] * tint[channel];
            return result;
        }
        public static ForestLightingEnvironment Load() => Resources.Load<ForestLightingEnvironment>("QuietCamp/ForestLightingEnvironment");
    }
}
