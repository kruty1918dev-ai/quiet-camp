using System.Text.RegularExpressions;
using DG.Tweening;
using QuietCamp.Presentation.UI;

namespace QuietCamp.Presentation
{
    /// <summary>One quiet rhythm for controls, panels and world actions.</summary>
    public static class CampMotion
    {
        public const float Press = .12f;
        public const float Release = .20f;
        public const float Change = .24f;
        public const float Enter = .30f;
        public const float Exit = .20f;
        public const Ease Settle = Ease.OutCubic;
        static readonly Regex Roles = new Regex("data-motion-role=\"([^\"]+)\"( data-motion=\"exit\")?");

        public static string Apply(string html, float scale)
            => Roles.Replace(html, match =>
            {
                var role = match.Groups[1].Value;
                if (role == "none") return match.Value;
                var exit = match.Groups[2].Success;
                var duration = exit ? Exit : role == "scrim" ? .18f : Enter;
                return match.Value + " data-motion-duration=\"" + HtmlUi.Number(duration * scale)
                    + "\" data-motion-distance=\"18\" data-motion-ease=\""
                    + (exit ? "InOutSine" : "OutCubic") + "\"";
            });
    }
}
