using UnityEditor;

namespace QuietCamp.Editor
{
    /// <summary>Import policy for the generated screen-space atmosphere art only.</summary>
    public sealed class AtmosphereTextureImporter : AssetPostprocessor
    {
        /// <summary>Force a fresh import of every atmosphere texture — used after the
        /// import policy itself changes, since existing import results are cached.</summary>
        public static void ReimportAll()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D",
                new[] { "Assets/QuietCamp/Resources/QuietCamp/Atmosphere/Textures" }))
                AssetDatabase.ImportAsset(AssetDatabase.GUIDToAssetPath(guid),
                    ImportAssetOptions.ForceUpdate);
        }

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith("Assets/QuietCamp/Resources/QuietCamp/Atmosphere/Textures/",
                System.StringComparison.Ordinal)) return;
            var importer = (TextureImporter)assetImporter;
            if (assetPath.EndsWith("/leaves.png", System.StringComparison.Ordinal))
            {
                ImportLeafAtlas(importer);
                return;
            }
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.mipmapEnabled = false;
            importer.isReadable = false;
            importer.wrapMode = UnityEngine.TextureWrapMode.Clamp;
            importer.filterMode = UnityEngine.FilterMode.Bilinear;
            importer.maxTextureSize = 2048;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Compressed;
            foreach (var platform in new[] { "Android", "iPhone" })
                importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings
                {
                    name = platform, overridden = true, maxTextureSize = 2048,
                    format = TextureImporterFormat.ASTC_6x6, compressionQuality = 70
                });
        }

        /// <summary>Leaves ship as a fixed 2×2 sprite atlas: wind particles pick one
        /// of the four cells at spawn. Slices follow the authoring order
        /// (top-left sage, top-right olive, bottom-left twig, bottom-right amber).</summary>
        static void ImportLeafAtlas(TextureImporter importer)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.sRGBTexture = true;
            importer.mipmapEnabled = false;
            importer.isReadable = false;
            importer.wrapMode = UnityEngine.TextureWrapMode.Clamp;
            importer.filterMode = UnityEngine.FilterMode.Bilinear;
            importer.maxTextureSize = 2048;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.spritesheet = BuildLeafSheet();
            foreach (var platform in new[] { "Android", "iPhone" })
                importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings
                {
                    name = platform, overridden = true, maxTextureSize = 2048,
                    format = TextureImporterFormat.ASTC_6x6, compressionQuality = 70
                });
        }

        static SpriteMetaData[] BuildLeafSheet()
        {
            const int size = 1254, half = size / 2;
            var names = new[] { "leaf_sage", "leaf_olive", "leaf_twig", "leaf_amber" };
            var metas = new SpriteMetaData[4];
            for (var i = 0; i < 4; i++)
                metas[i] = new SpriteMetaData
                {
                    name = names[i], alignment = (int)UnityEngine.SpriteAlignment.Center,
                    rect = new UnityEngine.Rect((i % 2) * half, (1 - i / 2) * half, half, half)
                };
            return metas;
        }
    }
}

