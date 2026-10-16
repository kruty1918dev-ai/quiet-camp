using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;

namespace QuietCamp.Editor.LevelAuthoring.WorldOps
{
    public static class WorldOpsReport
    {
        public static void Write(WorldRun run)
        {
            string E(string text)=>WebUtility.HtmlEncode(text??"");
            var html=new StringBuilder("<!doctype html><html lang='uk'><meta charset='utf-8'><meta name='viewport' content='width=device-width'><title>WorldOps</title><style>body{background:#18221e;color:#eee;font:16px system-ui;max-width:1300px;margin:auto;padding:30px}section{border-top:1px solid #596b5b;padding:20px 0}a{color:#c4dbac}figure{display:inline-block;width:30%;vertical-align:top;margin:1%}img{width:100%;border-radius:8px}pre{white-space:pre-wrap;color:#f5c9a7}small{color:#b9c5bc}</style>");
            html.Append("<h1>WorldOps · ").Append(E(run.recipe.label??run.recipe.id)).Append("</h1><p>").Append(E(run.status)).Append(" · ").Append(E(run.createdUtc)).Append("</p><p>Ігрові кадри містять справжній UI. Маски ID/глибини — окремі діагностичні файли. Числові перевірки не є художнім прийманням.</p>");
            foreach(var item in run.levels)
            {
                html.Append("<section><h2>").Append(E(item.levelId)).Append(" · ").Append(E(item.status)).Append(item.cached?" · актуальний кеш":"").Append("</h2><pre>").Append(E(item.error)).Append("</pre><ul>");
                foreach(var f in item.findings)html.Append("<li>").Append(E(f.code+" · "+f.objectId+" · "+f.message)).Append("</li>");
                html.Append("</ul>");
                foreach(var frame in item.review?.frames??new System.Collections.Generic.List<WorldFrame>())
                {
                    string link=new Uri(frame.path).AbsoluteUri;
                    html.Append("<figure><a href='").Append(E(link)).Append("'><img loading='lazy' src='").Append(E(link)).Append("'></a><figcaption>").Append(E(frame.phase+" · "+frame.profile+" · "+frame.state)).Append("</figcaption><small>").Append(E(frame.sha256)).Append("</small></figure>");
                }
                html.Append("</section>");
            }
            html.Append("</html>");WorldOpsStorage.Write(Path.Combine(run.directory,"index.html"),html.ToString());
        }
    }
}
