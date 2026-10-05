using System;
using System.Text;
using QuietCamp.Domain;

namespace QuietCamp.Presentation.UI
{
    public static class CampIcons
    {
        public static string Mark(string name, string cls = "")
        {
            var inner = name == "pause" ? "<view class=\"pause-bar\"></view><view class=\"pause-bar\"></view>"
                : name == "sound" ? "<view class=\"speaker-box\"/><view class=\"speaker-cone\"/><view class=\"speaker-wave near\"/><view class=\"speaker-wave far\"/>" : "";
            return "<view class=\"ico ico-" + name + " " + cls + "\">" + inner + "</view>";
        }
        public static string Button(HtmlSurface surface, string id, string icon, Action click,
            string cls = "", string tooltip = null, bool enabled = true)
        {
            surface.Callbacks.Bind(id,click);
            return "<button id=\"" + HtmlUi.Escape(id) + "\" class=\"" + cls + "\""
                + (enabled ? "" : " disabled=\"true\"")
                + (tooltip == null ? "" : " data-tooltip=\""+HtmlUi.Escape(tooltip)+"\"")
                + " onClick=\"Globals.campUi.Click('"+HtmlUi.Escape(id)+"')\">"+Mark(icon)+"</button>";
        }
        public static string Wishes(LevelData level, GuestData guest)
        {
            var s = new StringBuilder("<view class=\"wish-icons\">").Append(Mark("path"));
            if (guest.shade) s.Append(Mark("shade"));
            if (guest.quiet) s.Append(Mark("quiet"));
            foreach (var pair in level.friends ?? Array.Empty<string[]>())
                if (pair != null && Array.IndexOf(pair,guest.id)>=0) { s.Append(Mark("guests")); break; }
            return s.Append("</view>").ToString();
        }
    }
}
