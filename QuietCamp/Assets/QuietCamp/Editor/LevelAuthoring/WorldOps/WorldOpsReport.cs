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
            var html=new StringBuilder("<!doctype html><html lang='uk'><meta charset='utf-8'><meta name='viewport' content='width=device-width'><title>WorldOps</title><style>body{background:#18221e;color:#eee;font:16px system-ui;max-width:1300px;margin:auto;padding:30px}section{border-top:1px solid #596b5b;padding:20px 0}a{color:#c4dbac}.pair{display:flex;gap:16px;align-items:flex-start}figure{width:48%;margin:0 0 24px}img{width:100%;border-radius:8px}pre{white-space:pre-wrap;color:#f5c9a7}small{color:#b9c5bc;overflow-wrap:anywhere}@media(max-width:650px){.pair{display:block}figure{width:100%}}</style>");
            html.Append("<h1>WorldOps · ").Append(E(run.recipe.label??run.recipe.id)).Append("</h1><p>").Append(E(run.status)).Append(" · ").Append(E(run.createdUtc)).Append("</p><p>Ігрові кадри містять справжній UI. Маски ID/глибини — окремі діагностичні файли. Числові перевірки не є художнім прийманням.</p>");
            if(run.error!=null)html.Append("<pre>").Append(E(run.error)).Append("</pre>");
            foreach(var item in run.levels)
            {
                html.Append("<section><h2>").Append(E(item.levelId)).Append(" · ").Append(E(item.status)).Append(item.cached?" · актуальний кеш":"").Append("</h2><pre>").Append(E(item.error)).Append("</pre><ul>");
                foreach(var f in item.findings)html.Append("<li>").Append(E(f.code+" · "+f.objectId+" · "+f.message)).Append("</li>");
                html.Append("</ul>");
                foreach(var group in (item.review?.frames??new System.Collections.Generic.List<WorldFrame>()).GroupBy(f=>f.profile+" · "+f.state))
                {
                    html.Append("<h3>").Append(E(group.Key)).Append("</h3><div class='pair'>");
                    foreach(var frame in group.OrderBy(f=>f.phase=="before"?0:1))
                    {
                    string link=new Uri(frame.path).AbsoluteUri;
                    html.Append("<figure><a href='").Append(E(link)).Append("'><img loading='lazy' src='").Append(E(link)).Append("'></a><figcaption>").Append(E(frame.phase+" · "+frame.profile+" · "+frame.state)).Append("</figcaption><small>").Append(E(frame.sha256)).Append("</small></figure>");
                    }
                    html.Append("</div>");
                }
                html.Append("</section>");
            }
            html.Append("</html>");WorldOpsStorage.Write(Path.Combine(run.directory,"index.html"),html.ToString());
        }
    }
}
