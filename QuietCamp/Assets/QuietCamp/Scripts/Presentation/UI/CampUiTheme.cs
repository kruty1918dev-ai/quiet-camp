using UnityEngine;

namespace QuietCamp.Presentation.UI
{
    /// <summary>Native controls use the same forest and parchment as Camp.css.</summary>
    public static class CampUiTheme
    {
        public static readonly Color Forest = new Color(36f / 255f, 62f / 255f, 53f / 255f);
        public static readonly Color Cream = new Color(246f / 255f, 240f / 255f, 223f / 255f);
        static Sprite _circle, _card;
        public static Sprite Card
        {
            get
            {
                if (_card != null) return _card;
                const int size = 64;
                const float radius = 16;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
                { name = "Camp UI card", hideFlags = HideFlags.HideAndDontSave,
                    filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                var pixels = new Color[size * size];
                for (var y = 0; y < size; y++)
                for (var x = 0; x < size; x++)
                {
                    var p = new Vector2(Mathf.Abs(x + .5f - size * .5f), Mathf.Abs(y + .5f - size * .5f));
                    var corner = Vector2.Max(p - Vector2.one * (size * .5f - radius), Vector2.zero);
                    pixels[y * size + x] = new Color(1, 1, 1, Mathf.Clamp01(radius - .5f - corner.magnitude));
                }
                texture.SetPixels(pixels); texture.Apply(false, true);
                _card = Sprite.Create(texture, new Rect(0, 0, size, size), Vector2.one * .5f,
                    100f, 0, SpriteMeshType.FullRect, Vector4.one * radius);
                _card.name = "Camp UI card"; _card.hideFlags = HideFlags.HideAndDontSave;
                return _card;
            }
        }
        public static Sprite Circle
        {
            get
            {
                if (_circle != null) return _circle;
                const int size = 64;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
                { name = "Camp UI circle", hideFlags = HideFlags.HideAndDontSave,
                    filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                var pixels = new Color[size * size];
                for (var y = 0; y < size; y++)
                for (var x = 0; x < size; x++)
                {
                    var distance = Vector2.Distance(new Vector2(x + .5f, y + .5f), Vector2.one * (size * .5f));
                    pixels[y * size + x] = new Color(1, 1, 1, Mathf.Clamp01(size * .5f - .5f - distance));
                }
                texture.SetPixels(pixels); texture.Apply(false, true);
                _circle = Sprite.Create(texture, new Rect(0, 0, size, size), Vector2.one * .5f,
                    100f, 0, SpriteMeshType.FullRect);
                _circle.name = "Camp UI circle"; _circle.hideFlags = HideFlags.HideAndDontSave;
                return _circle;
            }
        }
    }
}
