using Kruty1918.Atmos;
using UnityEngine;

namespace QuietCamp.Infrastructure
{
    /// <summary>
    /// Loads the authored QuietCamp sky materials (day / evening) and applies
    /// them through the Atmos package. Materials live under
    /// Resources/QuietCamp/Sky so the sky ships inside every build.
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
            if (m == null) Debug.LogWarning($"[QuietCamp] Sky material '{name}' missing.");
            return m;
        }

        /// <summary>Applies the skybox to the active camera and refreshes ambient.</summary>
        public static void Apply(bool evening, Camera camera = null)
            => Sky.Apply(evening ? Evening : Day, camera);
    }
}
