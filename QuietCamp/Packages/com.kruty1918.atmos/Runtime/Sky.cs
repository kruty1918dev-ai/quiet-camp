using UnityEngine;

namespace Kruty1918.Atmos
{
    /// <summary>
    /// Applies sky materials (or SkySpec definitions) to RenderSettings and keeps
    /// camera clear flags, fog and ambient GI coherent. The cheapest path for a
    /// fixed day/evening pair is authoring .mat assets; for runtime-blended
    /// skies use SkyController which reuses a single dynamic material.
    /// </summary>
    public static class Sky
    {
        /// <summary>Builds a runtime material on the Atmos/SkyGradient shader.</summary>
        public static Material CreateMaterial(SkySpec spec, string name = "AtmosSky")
        {
            var shader = AtmosShaders.Find(AtmosShaders.SkyGradient);
            if (shader == null) return null;
            var mat = new Material(shader) { name = name };
            spec?.ApplyTo(mat);
            return mat;
        }

        /// <summary>
        /// Installs a skybox material, points the camera at the skybox and
        /// refreshes ambient GI. Fog is applied from <paramref name="spec"/>
        /// when given, otherwise disabled (the calm-diorama default).
        /// </summary>
        public static void Apply(Material skybox, Camera camera = null, SkySpec spec = null)
        {
            if (skybox != null) RenderSettings.skybox = skybox;
            if (camera != null) camera.clearFlags = CameraClearFlags.Skybox;
            ApplyEnvironment(spec ?? new SkySpec());
        }

        /// <summary>Creates a temporary material and applies the spec immediately.</summary>
        public static void Apply(SkySpec spec, Camera camera = null)
            => Apply(CreateMaterial(spec), camera, spec);

        /// <summary>Applies fog + ambient refresh without touching the skybox.</summary>
        public static void ApplyEnvironment(SkySpec spec)
        {
            if (spec != null && spec.fog)
            {
                RenderSettings.fog = true;
                RenderSettings.fogColor = spec.fogColor;
                RenderSettings.fogMode = FogMode.Exponential;
                RenderSettings.fogDensity = spec.fogDensity;
            }
            else
            {
                RenderSettings.fog = false;
            }
            DynamicGI.UpdateEnvironment();
        }
    }
}
