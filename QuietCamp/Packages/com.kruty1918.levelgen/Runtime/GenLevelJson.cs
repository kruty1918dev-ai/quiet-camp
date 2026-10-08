using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Kruty1918.LevelGen
{
    /// <summary>
    /// Canonical GenLevel <-> JSON. Format:
    /// {
    ///   "generator": "levelgen", "width":8, "height":8, "seed":42, "recipe":"x",
    ///   "layers": { "coins": [[x,y],...], "walls": [...] },
    ///   "entities": [ {"kind":"coin","x":3,"y":4,"rotation":0,"props":{...}} ],
    ///   "props": { ...arbitrary json... }
    /// }
    /// Deliberately mirrors LevelKit's canonical shape so the bridge is a
    /// thin field copy.
    /// </summary>
    public static class GenLevelJson
    {
        public static string Write(GenLevel level, bool pretty = true)
        {
            var o = new JObject
            {
                ["generator"] = "levelgen",
                ["recipe"] = level.recipe,
                ["width"] = level.width,
                ["height"] = level.height,
                ["seed"] = level.seed,
                ["attempts"] = level.attempts
            };
            var layers = new JObject();
            foreach (var kv in level.Layers)
            {
                var arr = new JArray();
                foreach (var c in kv.Value) arr.Add(new JArray(c.X, c.Y));
                layers[kv.Key] = arr;
            }
            o["layers"] = layers;
            var ents = new JArray();
            foreach (var e in level.Entities)
            {
                var je = new JObject { ["kind"] = e.kind, ["x"] = e.x, ["y"] = e.y };
                if (e.rotation != 0) je["rotation"] = e.rotation;
                if (e.props != null && e.props.Count > 0) je["props"] = e.props;
                ents.Add(je);
            }
            o["entities"] = ents;
            if (level.Props != null && level.Props.Count > 0) o["props"] = level.Props;
            return o.ToString(pretty ? Formatting.Indented : Formatting.None);
        }

        public static GenLevel Parse(string json)
        {
            var o = JObject.Parse(json);
            var l = new GenLevel
            {
                width = (int)o["width"], height = (int)o["height"],
                seed = (int?)o["seed"] ?? 0,
                recipe = (string)o["recipe"] ?? "",
                attempts = (int?)o["attempts"] ?? 1
            };
            if (o["layers"] is JObject layers)
                foreach (var kv in layers)
                    if (kv.Value is JArray arr)
                        foreach (var t in arr)
                            l.Layer(kv.Key).Add(new GenCell((int)t[0], (int)t[1]));
            if (o["entities"] is JArray ents)
                foreach (var t in ents)
                    l.Entities.Add(new GenEntity(
                        (string)t["kind"], (int)t["x"], (int)t["y"],
                        (int?)t["rotation"] ?? 0, t["props"] as JObject));
            if (o["props"] is JObject props) l.Props = props;
            return l;
        }
    }
}
