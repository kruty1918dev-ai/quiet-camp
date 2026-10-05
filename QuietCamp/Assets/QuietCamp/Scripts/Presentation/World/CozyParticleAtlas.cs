using UnityEngine;

namespace QuietCamp.Presentation.World
{
    /// <summary>One owned 256px RGBA atlas: wispy soil, a warm halo, a tapered
    /// grass fragment and a rain ring. Transparent gutters prevent tile bleed.
    /// No downloaded texture, per-frame allocation or particle collision.</summary>
    public static class CozyParticleAtlas
    {
        public static Texture2D Create(bool rainStreak = false)
        {
            const int size = 256, tile = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true)
            { name = "Cozy particle atlas (owned)", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                float u = ((x % tile) + .5f) / tile * 2 - 1;
                float v = ((y % tile) + .5f) / tile * 2 - 1;
                float radius = Mathf.Sqrt(u * u + v * v);
                // SetPixels begins at the bottom-left; Unity's whole-sheet
                // animation begins at the top-left and advances along rows.
                int cell = (1 - y / tile) * 2 + x / tile;
                float alpha;
                if (cell == 0)
                {
                    float detail = .70f + .15f * Mathf.Sin(u * 14 + v * 9) + .15f * Mathf.Sin(v * 19 - u * 6);
                    alpha = Mathf.Pow(Mathf.Clamp01(1 - radius), 1.7f) * detail;
                }
                else if (cell == 1)
                    alpha = .25f * Mathf.Exp(-radius * radius * 6) + .65f * Mathf.Exp(-radius * radius * 75);
                else if (cell == 2)
                {
                    float width = rainStreak ? .12f : .1f + .14f * Mathf.Clamp01(1 - Mathf.Abs(v));
                    alpha = Mathf.Clamp01(1 - Mathf.Abs(u + (rainStreak ? 0 : .08f * Mathf.Sin(v * 3))) / width) * Mathf.Clamp01((.8f - Mathf.Abs(v)) * 10);
                }
                else alpha = Mathf.Exp(-Mathf.Pow((radius - .6f) * 32, 2)) * .7f;
                alpha *= Mathf.SmoothStep(0, 1, Mathf.Clamp01((.94f - radius) * 12));
                pixels[y * size + x] = new Color(1, 1, 1, alpha);
            }
            texture.SetPixels(pixels); texture.Apply(true, true); return texture;
        }
        public static void Tile(ParticleSystem system, int tile)
        {
            var sheet = system.textureSheetAnimation; sheet.enabled = true;
            sheet.mode = ParticleSystemAnimationMode.Grid; sheet.numTilesX = sheet.numTilesY = 2;
            sheet.startFrame = (tile + .1f) / 4f; sheet.frameOverTime = 0; sheet.cycleCount = 1;
        }
    }
}
