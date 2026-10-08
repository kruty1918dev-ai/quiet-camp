using System;
using UnityEngine;

namespace Kruty1918.Atmos
{
    /// <summary>
    /// Tuning for a procedural campfire: flame quads, ground glow, ember
    /// particles and a flickering point light. Defaults produce a small cozy
    /// campfire suited to calm diorama scenes; scale the fields for bonfires.
    /// </summary>
    [Serializable]
    public class CampfireSpec
    {
        [Header("Glow")]
        [Tooltip("Warm radial glow quad lying under the logs.")]
        public Color glowColor = new Color(1f, 0.55f, 0.2f, 0.4f);
        [Range(0f, 3f)] public float glowIntensity = 1.0f;
        [Range(0.2f, 6f)] public float glowRadius = 1.6f;

        [Header("Flames")]
        [Tooltip("Procedural flame quads (crossed pair reads from any side).")]
        [Range(0, 4)] public int flameQuads = 2;
        public Vector3 flameScale = new Vector3(0.7f, 1.0f, 0.7f);
        public float flameHeight = 0.42f;

        [Header("Embers")]
        [Range(0, 60)] public int emberCount = 12;
        [Range(0f, 30f)] public float emberRate = 10f;
        public Color emberColorA = new Color(1f, 0.62f, 0.18f, 0.9f);
        public Color emberColorB = new Color(1f, 0.4f, 0.1f, 0.4f);

        [Header("Light")]
        public Color lightColor = new Color(1f, 0.639f, 0.29f);
        [Range(0f, 8f)] public float lightIntensity = 1.2f;
        [Range(0.5f, 20f)] public float lightRange = 3f;
        public float lightHeight = 0.8f;
        [Tooltip("Perlin flicker strength; 0 = steady light.")]
        [Range(0f, 1f)] public float flicker = 0.55f;

        /// <summary>The tuned campfire shipped with Quiet Camp — small and cozy.</summary>
        public static CampfireSpec Cozy => new CampfireSpec();

        /// <summary>A bigger bonfire — larger flames, glow and light reach.</summary>
        public static CampfireSpec Bonfire => new CampfireSpec
        {
            glowRadius = 3.2f, glowIntensity = 1.4f,
            flameQuads = 3, flameScale = new Vector3(1.4f, 2.0f, 1.4f), flameHeight = 0.8f,
            emberCount = 24, emberRate = 20f,
            lightIntensity = 2.2f, lightRange = 7f, lightHeight = 1.4f
        };
    }
}
