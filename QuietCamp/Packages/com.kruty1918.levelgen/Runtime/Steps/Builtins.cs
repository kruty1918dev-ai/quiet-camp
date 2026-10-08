using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Kruty1918.LevelGen.Steps
{
    /// <summary>Registers the built-in step library into the engine registry.</summary>
    public static class Builtins
    {
        public static void RegisterAll(Dictionary<string, Func<IGenStep>> r)
        {
            r["fill"] = () => new FillStep();
            r["rect"] = () => new RectStep();
            r["border"] = () => new BorderStep();
            r["noise"] = () => new NoiseStep();
            r["scatter"] = () => new ScatterStep();
            r["symmetry"] = () => new SymmetryStep();
            r["invert"] = () => new InvertStep();
            r["path"] = () => new PathStep();
            r["connect"] = () => new ConnectStep();
            r["maze"] = () => new MazeStep();
            r["rooms"] = () => new RoomsStep();
            r["segments"] = () => new SegmentsStep();
            r["cells_to_entities"] = () => new CellsToEntitiesStep();
            r["place"] = () => new PlaceStep();
            r["prop"] = () => new PropStep();
            r["require"] = () => new RequireStep();
        }

        // ─── shared param helpers ────────────────────────────────────────────

        internal static string Str(JObject p, string key, string def = null)
            => (string)p[key] ?? def ?? throw new GenRecipeException($"step param '{key}' missing");

        internal static int Int(JObject p, string key, int def)
            => (int?)p[key] ?? def;

        internal static float Float(JObject p, string key, float def)
            => (float?)p[key] ?? def;

        internal static bool Bool(JObject p, string key, bool def)
            => (bool?)p[key] ?? def;

        internal static GenCell Cell(JObject p, string key, GenCell def)
        {
            var t = p[key] as JArray;
            return t != null && t.Count >= 2
                ? new GenCell((int)t[0], (int)t[1])
                : def;
        }

        internal static List<string> StrList(JObject p, string key)
        {
            var list = new List<string>();
            if (p[key] is JArray a)
                foreach (var t in a) list.Add((string)t);
            else if (p[key] is JValue v && v.Value is string s)
                list.Add(s);
            return list;
        }

        /// <summary>Candidate cells for placement steps: in bounds, on required/allowed masks, off forbidden ones.</summary>
        internal static List<GenCell> Candidates(GenLevel level, JObject p,
            string layerKey = "layer", string requireKey = "on", string forbidKey = "avoid")
        {
            var on = StrList(p, requireKey);
            var avoid = StrList(p, forbidKey);
            var cells = new List<GenCell>();
            foreach (var c in level.AllCells())
            {
                if (on.Count > 0 && !level.AnyLayer(on, c)) continue;
                if (level.AnyLayer(avoid, c)) continue;
                cells.Add(c);
            }
            return cells;
        }
    }

    /// <summary>{"type":"fill","layer":"x"} — mark every cell of a layer.</summary>
    public sealed class FillStep : IGenStep
    {
        public void Apply(GenContext ctx, JObject p)
        {
            var set = ctx.Level.Layer(Builtins.Str(p, "layer"));
            if (Builtins.Bool(p, "clear", false)) set.Clear();
            else foreach (var c in ctx.Level.AllCells()) set.Add(c);
        }
    }

    /// <summary>{"type":"rect","layer":"x","from":[x,y],"size":[w,h]} — fill a rectangle.</summary>
    public sealed class RectStep : IGenStep
    {
        public void Apply(GenContext ctx, JObject p)
        {
            var set = ctx.Level.Layer(Builtins.Str(p, "layer"));
            var from = Builtins.Cell(p, "from", new GenCell(0, 0));
            var size = Builtins.Cell(p, "size", new GenCell(ctx.Level.width, ctx.Level.height));
            for (var y = from.Y; y < from.Y + size.Y; y++)
                for (var x = from.X; x < from.X + size.X; x++)
                {
                    var c = new GenCell(x, y);
                    if (ctx.Level.InBounds(c)) set.Add(c);
                }
        }
    }

    /// <summary>{"type":"border","layer":"x","thickness":1} — mark the grid edge.</summary>
    public sealed class BorderStep : IGenStep
    {
        public void Apply(GenContext ctx, JObject p)
        {
            var set = ctx.Level.Layer(Builtins.Str(p, "layer"));
            var t = System.Math.Max(1, Builtins.Int(p, "thickness", 1));
            var l = ctx.Level;
            foreach (var c in l.AllCells())
                if (c.X < t || c.Y < t || c.X >= l.width - t || c.Y >= l.height - t)
                    set.Add(c);
        }
    }

    /// <summary>
    /// {"type":"noise","layer":"x","scale":3,"threshold":0.5} — organic blobs via
    /// smoothed value noise; threshold keeps cells at/above it.
    /// </summary>
    public sealed class NoiseStep : IGenStep
    {
        public void Apply(GenContext ctx, JObject p)
        {
            var set = ctx.Level.Layer(Builtins.Str(p, "layer"));
            var scale = System.Math.Max(0.5f, Builtins.Float(p, "scale", 3f));
            var threshold = Builtins.Float(p, "threshold", 0.55f);
            var rng = ctx.Rng;
            // coarse lattice -> bilinear field
            var gw = (int)System.Math.Ceiling(ctx.Level.width / scale) + 2;
            var gh = (int)System.Math.Ceiling(ctx.Level.height / scale) + 2;
            var grid = new float[gw, gh];
            for (var y = 0; y < gh; y++)
                for (var x = 0; x < gw; x++)
                    grid[x, y] = rng.Float();
            foreach (var c in ctx.Level.AllCells())
            {
                var fx = c.X / scale; var fy = c.Y / scale;
                var x0 = (int)fx; var y0 = (int)fy;
                var tx = Smooth(fx - x0); var ty = Smooth(fy - y0);
                var v = Lerp(
                    Lerp(grid[x0, y0], grid[x0 + 1, y0], tx),
                    Lerp(grid[x0, y0 + 1], grid[x0 + 1, y0 + 1], tx), ty);
                if (v >= threshold) set.Add(c);
            }
        }
        static float Smooth(float t) => t * t * (3f - 2f * t);
        static float Lerp(float a, float b, float t) => a + (b - a) * t;
    }

    /// <summary>
    /// {"type":"scatter","layer":"x","count":8,"minDist":1,"on":[...],"avoid":[...]}
    /// — random cells, optionally spread apart and mask-constrained.
    /// </summary>
    public sealed class ScatterStep : IGenStep
    {
        public void Apply(GenContext ctx, JObject p)
        {
            var set = ctx.Level.Layer(Builtins.Str(p, "layer"));
            var count = Builtins.Int(p, "count", 1);
            var minDist = Builtins.Int(p, "minDist", 0);
            var cells = Builtins.Candidates(ctx.Level, p);
            ctx.Rng.Shuffle(cells);
            var placed = 0;
            foreach (var c in cells)
            {
                if (placed >= count) break;
                if (minDist > 0)
                {
                    var ok = true;
                    foreach (var e in set)
                        if (e.ManhattanTo(c) < minDist) { ok = false; break; }
                    if (!ok) continue;
                }
                set.Add(c);
                placed++;
            }
            if (placed < count && Builtins.Bool(p, "strict", false))
                throw new GenFailedException($"scatter: wanted {count}, placed {placed}");
        }
    }

    /// <summary>{"type":"symmetry","layer":"x","axis":"x|y","keep":"first"} — mirror a mask.</summary>
    public sealed class SymmetryStep : IGenStep
    {
        public void Apply(GenContext ctx, JObject p)
        {
            var l = ctx.Level;
            var set = l.Layer(Builtins.Str(p, "layer"));
            var mirrorX = Builtins.Str(p, "axis", "x") == "x";
            var add = new List<GenCell>();
            foreach (var c in set)
                add.Add(mirrorX ? new GenCell(l.width - 1 - c.X, c.Y)
                                : new GenCell(c.X, l.height - 1 - c.Y));
            foreach (var c in add) set.Add(c);
        }
    }

    /// <summary>{"type":"invert","layer":"x","avoid":[...]} — layer becomes its complement.</summary>
    public sealed class InvertStep : IGenStep
    {
        public void Apply(GenContext ctx, JObject p)
        {
            var l = ctx.Level;
            var set = l.Layer(Builtins.Str(p, "layer"));
            var inverted = new HashSet<GenCell>();
            foreach (var c in l.AllCells())
                if (!set.Contains(c) && !l.AnyLayer(Builtins.StrList(p, "avoid"), c))
                    inverted.Add(c);
            set.Clear();
            foreach (var c in inverted) set.Add(c);
        }
    }
}
