using QuietCamp.Infrastructure;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace QuietCamp.Editor
{
    public static class ForestLightingBuilder
    {
        const string Source = "Assets/QuietCamp/ThirdParty/PolyHavenLighting/Editor/sunset_forest_1k.hdr";
        const string Target = "Assets/QuietCamp/Resources/QuietCamp/ForestLightingEnvironment.asset";
        [MenuItem("Quiet Camp/Build Forest Lighting")]
        public static void Build()
        {
            AssetDatabase.ImportAsset(Source, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(Source);
            importer.textureType = TextureImporterType.Default;
            importer.textureShape = TextureImporterShape.Texture2D;
            importer.sRGBTexture = false;
            importer.isReadable = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 1024;
            importer.SaveAndReimport();
            var hdr = AssetDatabase.LoadAssetAtPath<Texture2D>(Source);
            const int width = 64, height = 32;
            float mean = 0, weights = 0;
            for (int y = 0; y < height; y++)
            {
                float weight = Mathf.Sin((y + .5f) / height * Mathf.PI);
                for (int x = 0; x < width; x++)
                {
                    var color = hdr.GetPixelBilinear((x + .5f) / width, (y + .5f) / height);
                    mean += color.grayscale * weight; weights += weight;
                }
            }
            mean /= weights;
            var sh = new SphericalHarmonicsL2();
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    float polar = (y + .5f) / height * Mathf.PI;
                    float azimuth = (x + .5f) / width * Mathf.PI * 2;
                    var direction = new Vector3(Mathf.Sin(polar) * Mathf.Cos(azimuth), -Mathf.Cos(polar), Mathf.Sin(polar) * Mathf.Sin(azimuth));
                    var color = hdr.GetPixelBilinear((x + .5f) / width, (y + .5f) / height);
                    // The direct sun is already rendered by the scene light: keep only broad diffuse light.
                    color *= Mathf.Min(1, mean * 4 / Mathf.Max(.0001f, color.grayscale));
                    sh.AddDirectionalLight(direction, color, Mathf.Sin(polar) / weights);
                }
            var directions = new[] { Vector3.up, Vector3.down, Vector3.right, Vector3.left, Vector3.forward, Vector3.back };
            var values = new Color[6]; sh.Evaluate(directions, values);
            float average = 0; foreach (var value in values) average += value.grayscale / 6;
            sh *= 1 / Mathf.Max(.0001f, average);
            var asset = AssetDatabase.LoadAssetAtPath<ForestLightingEnvironment>(Target);
            if (asset == null) { asset = ScriptableObject.CreateInstance<ForestLightingEnvironment>(); AssetDatabase.CreateAsset(asset, Target); }
            asset.coefficients = new float[27];
            for (int c = 0; c < 3; c++) for (int i = 0; i < 9; i++) asset.coefficients[c * 9 + i] = sh[c, i];
            asset.source = "Poly Haven / Sunset Forest / Andreas Mischok / CC0 / 189502addad02ba43449b895de66bcdf115e7150536d9150d9a7ba05e1d15b11";
            EditorUtility.SetDirty(asset); AssetDatabase.SaveAssets();
            Debug.Log("[ForestLighting] Baked 27 diffuse coefficients; HDRI remains Editor-only.");
        }
    }
}
