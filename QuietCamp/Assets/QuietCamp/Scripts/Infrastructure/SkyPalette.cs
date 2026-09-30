using UnityEngine;

namespace QuietCamp.Infrastructure
{
    /// <summary>
    /// Loads and applies the generated sky materials (day / evening) and keeps
    /// ambient + fog coherent with the chosen mood. Materials live under
    /// Resources/QuietCamp/Sky so the shader ships inside every build.
    /// </summary>
    public static class SkyPalette
    {
        static Material _day;
        static Material _evening;

        public static Material Day => _day ??= Load("SkyDay");
        public static Material Evening => _evening ??= Load("SkyEvening");

        static Material Load(string name)
        {
            var m = Resources.Load<Material>("QuietCamp/Sky/" + name);
            if (m == null) Debug.LogWarning($"[QuietCamp] Sky material '{name}' missing — run Sky generator.");
            return m;
        }

        /// <summary>Applies the skybox to the active camera and refreshes ambient.</summary>
        public static void Apply(bool evening, Camera camera = null)
        {
            var sky = evening ? Evening : Day;
            if (sky != null) RenderSettings.skybox = sky;
            if (camera != null) camera.clearFlags = CameraClearFlags.Skybox;

            // The reference diorama renders flat: no distance fog washing out
            // the decor colors.
            RenderSettings.fog = false;

            DynamicGI.UpdateEnvironment();
        }
    }
}
