using System.IO;
using UnityEditor;
using UnityEngine;

namespace QuietCamp.Editor
{
    /// <summary>
    /// Bakes the two QuietCamp/SkyGradient materials used by scene hosts:
    /// a calm daytime sky for menu/day camps and a dusk variant with stars
    /// for evening lighting. Idempotent — re-running rewrites the assets.
    /// </summary>
    public static class QuietCampSky
    {
        const string Dir = "Assets/QuietCamp/Resources/QuietCamp/Sky";

        [MenuItem("QuietCamp/Generate Sky Materials")]
        public static void Generate()
        {
            Directory.CreateDirectory(Path.Combine(Directory.GetCurrentDirectory(), Dir));
            var shader = Shader.Find("QuietCamp/SkyGradient");
            if (shader == null)
            {
                Debug.LogError("[QuietCamp] SkyGradient shader not found — reimport Shaders/.");
                return;
            }

            var day = new Material(shader) { name = "SkyDay" };
            day.SetColor("_ZenithColor", new Color(0.40f, 0.62f, 0.85f));
            day.SetColor("_HorizonColor", new Color(0.99f, 0.90f, 0.72f));
            day.SetColor("_GroundColor", new Color(0.52f, 0.60f, 0.47f));
            day.SetColor("_SunColor", new Color(1.0f, 0.87f, 0.62f));
            day.SetVector("_SunDir", new Vector4(0.42f, 0.62f, 0.30f, 0f));
            day.SetFloat("_SunSize", 0.055f);
            day.SetFloat("_SunHalo", 0.30f);
            day.SetFloat("_HorizonFalloff", 1.7f);
            day.SetFloat("_Stars", 0f);

            var evening = new Material(shader) { name = "SkyEvening" };
            evening.SetColor("_ZenithColor", new Color(0.13f, 0.15f, 0.30f));
            evening.SetColor("_HorizonColor", new Color(0.96f, 0.48f, 0.28f));
            evening.SetColor("_GroundColor", new Color(0.16f, 0.20f, 0.19f));
            evening.SetColor("_SunColor", new Color(1.0f, 0.55f, 0.30f));
            evening.SetVector("_SunDir", new Vector4(0.50f, 0.10f, 0.30f, 0f));
            evening.SetFloat("_SunSize", 0.10f);
            evening.SetFloat("_SunHalo", 0.55f);
            evening.SetFloat("_HorizonFalloff", 2.1f);
            evening.SetFloat("_Stars", 0.9f);

            Save(day, "SkyDay.mat");
            Save(evening, "SkyEvening.mat");
            AssetDatabase.SaveAssets();
            Debug.Log("[QuietCamp] Sky materials generated: SkyDay, SkyEvening.");
        }

        static void Save(Material mat, string file)
        {
            var path = $"{Dir}/{file}";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                EditorUtility.CopySerialized(mat, existing);
                EditorUtility.SetDirty(existing);
            }
            else AssetDatabase.CreateAsset(mat, path);
        }
    }
}
