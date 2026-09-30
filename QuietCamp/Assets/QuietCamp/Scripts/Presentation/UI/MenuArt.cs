using UnityEngine;
using UnityEngine.UI;
namespace QuietCamp.Presentation.UI
{
    /// <summary>
    /// Illustrated menu/backdrop art: picks the phone or tablet background by
    /// screen aspect and stretches it full-bleed under a canvas root using
    /// cover-fit (AspectRatioFitter EnvelopeParent) so there are no edge bars.
    /// </summary>
    public static class MenuArt
    {
        // Design/MainMenu palette — exact UI colors from the menu plan.
        public static readonly Color Forest = new Color(0.141f, 0.243f, 0.208f);   // #243E35
        public static readonly Color Cream = new Color(0.953f, 0.937f, 0.890f);    // #F3EFE3
        public static readonly Color ForestText = new Color(0.208f, 0.325f, 0.282f); // #355348

        const string PhonePath = "QuietCamp/UI/MainMenu/menu_background_phone";
        const string TabletPath = "QuietCamp/UI/MainMenu/menu_background_tablet";

        /// <summary>Wider portrait layouts (tablets) get the 3:4 composition.</summary>
        public static Sprite PickBackground()
        {
            var path = (float)Screen.width / Mathf.Max(1, Screen.height) >= 0.68f
                ? TabletPath : PhonePath;
            var sprite = Resources.Load<Sprite>(path);
            if (sprite == null)
                Debug.LogWarning($"[QuietCamp] Menu background missing: {path}");
            return sprite;
        }

        /// <summary>Full-bleed cover background; raycast-off, non-interactive.</summary>
        public static Image BuildBackground(Transform canvasRoot, string name = "Background")
        {
            var rt = QcUi.Stretch(canvasRoot, name);
            var img = rt.gameObject.AddComponent<Image>();
            var sprite = PickBackground();
            img.sprite = sprite;
            img.type = Image.Type.Simple;
            img.color = Color.white;
            img.raycastTarget = false;
            var fit = rt.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            if (sprite != null)
                fit.aspectRatio = (float)sprite.texture.width / sprite.texture.height;
            return img;
        }
    }
}
