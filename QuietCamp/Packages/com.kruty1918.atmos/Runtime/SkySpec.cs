using System;
using UnityEngine;

namespace Kruty1918.Atmos
{
    /// <summary>
    /// Data-driven definition of a procedural gradient sky: zenith/horizon/ground
    /// colors, a soft sun disc with halo, optional night stars and fog.
    /// Serializable so it can live on a SkyController, in JSON (JsonUtility),
    /// or be authored in the Sky Designer editor window.
    /// </summary>
    [Serializable]
    public class SkySpec
    {
        [Tooltip("Color at the top of the sky.")]
        public Color zenith = new Color(0.55f, 0.70f, 0.82f);

        [Tooltip("Color band at the horizon line.")]
        public Color horizon = new Color(0.93f, 0.90f, 0.78f);

        [Tooltip("Color below the horizon (the 'ground' half of the dome).")]
        public Color ground = new Color(0.86f, 0.89f, 0.85f);

        [Tooltip("Color of the sun disc and its halo.")]
        public Color sunColor = new Color(1.0f, 0.87f, 0.62f);

        [Tooltip("Direction toward the sun (does not need to be normalized).")]
        public Vector3 sunDir = new Vector3(0.42f, 0.62f, 0.30f);

        [Range(0.001f, 0.3f), Tooltip("Angular radius of the sun disc.")]
        public float sunSize = 0.055f;

        [Range(0f, 1f), Tooltip("Strength of the soft glow around the sun.")]
        public float sunHalo = 0.30f;

        [Range(0.2f, 8f), Tooltip("How tight the horizon color band is. Higher = tighter.")]
        public float horizonFalloff = 1.7f;

        [Range(0f, 1f), Tooltip("Star field intensity; 0 disables stars.")]
        public float stars;

        [Tooltip("Enable distance fog when this sky is applied via Sky.Apply.")]
        public bool fog;

        public Color fogColor = new Color(0.75f, 0.8f, 0.85f);

        [Range(0f, 0.1f)]
        public float fogDensity = 0.01f;

        /// <summary>Calm daytime sky — pale blue over warm horizon, no stars.</summary>
        public static SkySpec Day => new SkySpec();

        /// <summary>Dusk sky — deep indigo over orange horizon, low sun, stars.</summary>
        public static SkySpec Evening => new SkySpec
        {
            zenith = new Color(0.13f, 0.15f, 0.30f),
            horizon = new Color(0.96f, 0.48f, 0.28f),
            ground = new Color(0.16f, 0.20f, 0.19f),
            sunColor = new Color(1.0f, 0.55f, 0.30f),
            sunDir = new Vector3(0.50f, 0.10f, 0.30f),
            sunSize = 0.10f,
            sunHalo = 0.55f,
            horizonFalloff = 2.1f,
            stars = 0.9f
        };

        /// <summary>Moonless night — near-black zenith, faint horizon, full stars.</summary>
        public static SkySpec Night => new SkySpec
        {
            zenith = new Color(0.03f, 0.04f, 0.10f),
            horizon = new Color(0.10f, 0.12f, 0.22f),
            ground = new Color(0.05f, 0.06f, 0.08f),
            sunColor = new Color(0.7f, 0.75f, 0.9f),
            sunDir = new Vector3(-0.3f, 0.5f, -0.4f),
            sunSize = 0.03f,
            sunHalo = 0.15f,
            horizonFalloff = 2.4f,
            stars = 1.0f
        };

        /// <summary>Field-wise linear interpolation; fog flag follows t &lt; 0.5.</summary>
        public static SkySpec Lerp(SkySpec a, SkySpec b, float t)
        {
            if (a == null) return b;
            if (b == null) return a;
            return new SkySpec
            {
                zenith = Color.Lerp(a.zenith, b.zenith, t),
                horizon = Color.Lerp(a.horizon, b.horizon, t),
                ground = Color.Lerp(a.ground, b.ground, t),
                sunColor = Color.Lerp(a.sunColor, b.sunColor, t),
                sunDir = Vector3.Lerp(a.sunDir, b.sunDir, t),
                sunSize = Mathf.Lerp(a.sunSize, b.sunSize, t),
                sunHalo = Mathf.Lerp(a.sunHalo, b.sunHalo, t),
                horizonFalloff = Mathf.Lerp(a.horizonFalloff, b.horizonFalloff, t),
                stars = Mathf.Lerp(a.stars, b.stars, t),
                fog = t < 0.5f ? a.fog : b.fog,
                fogColor = Color.Lerp(a.fogColor, b.fogColor, t),
                fogDensity = Mathf.Lerp(a.fogDensity, b.fogDensity, t)
            };
        }

        /// <summary>Pushes every spec field onto an Atmos/SkyGradient material.</summary>
        public void ApplyTo(Material mat)
        {
            if (mat == null) return;
            mat.SetColor("_ZenithColor", zenith);
            mat.SetColor("_HorizonColor", horizon);
            mat.SetColor("_GroundColor", ground);
            mat.SetColor("_SunColor", sunColor);
            mat.SetVector("_SunDir", new Vector4(sunDir.x, sunDir.y, sunDir.z, 0f));
            mat.SetFloat("_SunSize", sunSize);
            mat.SetFloat("_SunHalo", sunHalo);
            mat.SetFloat("_HorizonFalloff", horizonFalloff);
            mat.SetFloat("_Stars", stars);
        }

        public string ToJson(bool pretty = true) => JsonUtility.ToJson(this, pretty);
        public static SkySpec FromJson(string json) => JsonUtility.FromJson<SkySpec>(json);
    }
}
