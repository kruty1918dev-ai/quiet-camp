using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Kruty1918.LevelGen.Steps
{
    /// <summary>
    /// {"type":"segments","axis":"y","from":0,"to":height-1,
    ///  "track":"track","segments":[
    ///    {"name":"flat","len":8,"weight":3,"fill":"track"},
    ///    {"name":"obstacles","len":6,"weight":2,"scatter":{"layer":"obstacles","density":0.3,"avoid":["track"]}},
    ///    {"name":"bonus","len":4,"weight":1,"fill":"coins"} ]}
    /// — lays a random sequence of named segment bands along an axis (the
    /// classic runner/hyper-casual track). Each band may fill a layer or
    /// scatter cells at a density. Bands tile the axis until `to` is reached.
    /// </summary>
    public sealed class SegmentsStep : IGenStep
    {
        public void Apply(GenContext ctx, JObject p)
        {
            var l = ctx.Level;
            var vertical = Builtins.Str(p, "axis", "y") != "x";
            var from = Builtins.Int(p, "from", 0);
            var to = Builtins.Int(p, "to", vertical ? l.height - 1 : l.width - 1);
            var defs = p["segments"] as JArray;
            if (defs == null || defs.Count == 0)
                throw new GenRecipeException("segments: needs a 'segments' array");

            var weights = new List<float>();
            foreach (var d in defs) weights.Add((float?)d["weight"] ?? 1f);
            var track = l.Layer(Builtins.Str(p, "track", "track"));

            var pos = from;
            while (pos <= to)
            {
                var def = defs[ctx.Rng.WeightedIndex(weights)] as JObject;
                var len = System.Math.Max(1, (int?)def["len"] ?? 4);
                var lenJitter = (int?)def["lenJitter"] ?? 0;
                if (lenJitter > 0) len += ctx.Rng.Int(-lenJitter, lenJitter + 1);
                var end = System.Math.Min(pos + len - 1, to);
                for (var t = pos; t <= end; t++)
                {
                    var span = vertical ? l.width : l.height;
                    for (var s = 0; s < span; s++)
                    {
                        var c = vertical ? new GenCell(s, t) : new GenCell(t, s);
                        track.Add(c);
                    }
                }
                if (def["fill"] is JValue fillName)
                    Band(l, vertical, pos, end, l.Layer((string)fillName));
                if (def["scatter"] is JObject sc)
                {
                    var layer = l.Layer((string)sc["layer"] ?? "scatter");
                    var density = (float?)sc["density"] ?? 0.3f;
                    var avoid = Strs(sc["avoid"]);
                    for (var t = pos; t <= end; t++)
                    {
                        var span = vertical ? l.width : l.height;
                        for (var s = 0; s < span; s++)
                        {
                            var c = vertical ? new GenCell(s, t) : new GenCell(t, s);
                            if (!l.AnyLayer(avoid, c) && ctx.Rng.Chance(density)) layer.Add(c);
                        }
                    }
                }
                pos = end + 1;
            }
        }

        static void Band(GenLevel l, bool vertical, int a, int b, HashSet<GenCell> set)
        {
            var span = vertical ? l.width : l.height;
            for (var t = a; t <= b; t++)
                for (var s = 0; s < span; s++)
                    set.Add(vertical ? new GenCell(s, t) : new GenCell(t, s));
        }

        static List<string> Strs(JToken t)
        {
            var list = new List<string>();
            if (t is JArray a) foreach (var x in a) list.Add((string)x);
            return list;
        }
    }

    /// <summary>
    /// {"type":"cells_to_entities","layer":"coins","kind":"coin","limit":50,
    ///  "props":{"value":1}} — turns layer cells into entities (optionally a
    ///  shuffled subset), giving each the same or listed props.
    /// </summary>
    public sealed class CellsToEntitiesStep : IGenStep
    {
        public void Apply(GenContext ctx, JObject p)
        {
            var l = ctx.Level;
            var cells = new List<GenCell>(l.Layer(Builtins.Str(p, "layer")));
            ctx.Rng.Shuffle(cells);
            var limit = Builtins.Int(p, "limit", cells.Count);
            var kind = Builtins.Str(p, "kind");
            var props = p["props"] as JObject;
            for (var i = 0; i < limit && i < cells.Count; i++)
                l.Entities.Add(new GenEntity(kind, cells[i].X, cells[i].Y,
                    Builtins.Int(p, "rotation", 0), props?.DeepClone() as JObject));
        }
    }

    /// <summary>
    /// {"type":"place","kind":"enemy","count":3,"on":[...],"avoid":[...],
    ///  "minDist":2,"rotation":"random"|0,"props":{...}}
    /// — places N entities on valid cells with spacing. Use "each" to place
    ///  several kinds in one step: "each":[{"kind":"a","count":2},...].
    /// </summary>
    public sealed class PlaceStep : IGenStep
    {
        public void Apply(GenContext ctx, JObject p)
        {
            if (p["each"] is JArray each)
            {
                foreach (var e in each)
                    Place(ctx, (JObject)e, p);
                return;
            }
            Place(ctx, p, p);
        }

        static void Place(GenContext ctx, JObject d, JObject inherited)
        {
            var l = ctx.Level;
            var merged = (JObject)inherited.DeepClone();
            merged.Merge(d, new JsonMergeSettings { MergeArrayHandling = MergeArrayHandling.Replace });
            var kind = Builtins.Str(merged, "kind");
            var count = Builtins.Int(merged, "count", 1);
            var minDist = Builtins.Int(merged, "minDist", 0);
            var randomRot = Builtins.Str(merged, "rotation", null) == "random";
            var props = merged["props"] as JObject;
            var cells = Builtins.Candidates(l, merged);
            ctx.Rng.Shuffle(cells);

            var placed = 0;
            var occupied = new HashSet<GenCell>();
            foreach (var e in l.Entities) occupied.Add(e.Cell);
            foreach (var c in cells)
            {
                if (placed >= count) break;
                if (occupied.Contains(c)) continue;
                if (minDist > 0)
                {
                    var ok = true;
                    foreach (var o in occupied)
                        if (o.ManhattanTo(c) < minDist) { ok = false; break; }
                    if (!ok) continue;
                }
                l.Entities.Add(new GenEntity(kind, c.X, c.Y,
                    randomRot ? ctx.Rng.Int(4) : Builtins.Int(merged, "rotation", 0),
                    props?.DeepClone() as JObject));
                occupied.Add(c);
                placed++;
            }
            if (placed < count && Builtins.Bool(merged, "strict", false))
                throw new GenFailedException($"place '{kind}': wanted {count}, placed {placed}");
        }
    }

    /// <summary>{"type":"prop","key":"difficulty","value":3} — sets a level prop.</summary>
    public sealed class PropStep : IGenStep
    {
        public void Apply(GenContext ctx, JObject p)
        {
            var key = Builtins.Str(p, "key");
            ctx.Level.Props[key] = p["value"]?.DeepClone() ?? JValue.CreateNull();
        }
    }

    /// <summary>
    /// {"type":"require","check":"<name>",...} — declarative validation; on
    /// failure throws GenFailedException so the attempt retries. Checks:
    ///  maskCount   {layer, min, max}          layer size within bounds
    ///  connected   {layer}                    layer is one component
    ///  distance    {a,b, min, max}            min manhattan between two layers
    ///  entityCount {kind, min, max}           entity kind count
    ///  notOverlap  {a,b}                      layers share no cells
    ///  cellIn      {cell:[x,y], layer}        cell must be on a layer
    /// </summary>
    public sealed class RequireStep : IGenStep
    {
        public void Apply(GenContext ctx, JObject p)
        {
            var check = Builtins.Str(p, "check");
            var l = ctx.Level;
            switch (check)
            {
                case "maskCount":
                {
                    var n = l.Layer(Builtins.Str(p, "layer"), false)?.Count ?? 0;
                    var min = Builtins.Int(p, "min", 0);
                    var max = Builtins.Int(p, "max", int.MaxValue);
                    if (n < min || n > max)
                        Fail($"maskCount '{p["layer"]}': {n} not in [{min},{max}]");
                    break;
                }
                case "connected":
                {
                    var name = Builtins.Str(p, "layer");
                    var comps = ConnectStep.Components(l, l.Layer(name, false) ?? new HashSet<GenCell>());
                    if (comps.Count != 1)
                        Fail($"connected '{name}': {comps.Count} components");
                    break;
                }
                case "distance":
                {
                    var a = l.Layer(Builtins.Str(p, "a"), false) ?? new HashSet<GenCell>();
                    var b = l.Layer(Builtins.Str(p, "b"), false) ?? new HashSet<GenCell>();
                    var min = Builtins.Int(p, "min", 0);
                    var max = Builtins.Int(p, "max", int.MaxValue);
                    var best = int.MaxValue;
                    foreach (var ca in a)
                        foreach (var cb in b)
                            best = System.Math.Min(best, ca.ManhattanTo(cb));
                    if (best == int.MaxValue || best < min || best > max)
                        Fail($"distance '{p["a"]}'-'{p["b"]}': {best} not in [{min},{max}]");
                    break;
                }
                case "entityCount":
                {
                    var kind = Builtins.Str(p, "kind");
                    var n = 0;
                    foreach (var e in l.Entities) if (e.kind == kind) n++;
                    var min = Builtins.Int(p, "min", 0);
                    var max = Builtins.Int(p, "max", int.MaxValue);
                    if (n < min || n > max)
                        Fail($"entityCount '{kind}': {n} not in [{min},{max}]");
                    break;
                }
                case "notOverlap":
                {
                    var a = l.Layer(Builtins.Str(p, "a"), false) ?? new HashSet<GenCell>();
                    var b = l.Layer(Builtins.Str(p, "b"), false) ?? new HashSet<GenCell>();
                    foreach (var c in a)
                        if (b.Contains(c)) Fail($"notOverlap: '{p["a"]}'∩'{p["b"]}' at {c}");
                    break;
                }
                case "cellIn":
                {
                    var c = Builtins.Cell(p, "cell", new GenCell(-1, -1));
                    if (!l.Has(Builtins.Str(p, "layer"), c))
                        Fail($"cellIn: {c} not on '{p["layer"]}'");
                    break;
                }
                default:
                    throw new GenRecipeException($"require: unknown check '{check}'");
            }
        }

        static void Fail(string msg) => throw new GenFailedException($"require {msg}");
    }
}
