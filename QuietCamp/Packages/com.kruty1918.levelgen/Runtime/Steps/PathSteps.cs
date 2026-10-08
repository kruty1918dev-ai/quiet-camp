using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Kruty1918.LevelGen.Steps
{
    /// <summary>
    /// {"type":"path","layer":"path","from":[x,y]|"edges","to":[x,y]|"edges",
    ///  "winding":0.6,"avoid":[...]} — carves a 4-connected path between two
    /// cells using a biased random walk with local backtracking. Good for
    /// tower-defense lanes and runner tracks. Stores the ordered cell list in
    /// ctx.Vars["lastPath"].
    /// </summary>
    public sealed class PathStep : IGenStep
    {
        public void Apply(GenContext ctx, JObject p)
        {
            var l = ctx.Level;
            var from = Endpoint(l, p, "from", ctx.Rng);
            var to = Endpoint(l, p, "to", ctx.Rng);
            var winding = Builtins.Float(p, "winding", 0.5f);
            var maxSteps = Builtins.Int(p, "maxSteps", l.width * l.height * 4);
            var path = Walk(l, ctx.Rng, from, to, winding, maxSteps, Builtins.StrList(p, "avoid"));
            if (path == null)
                throw new GenFailedException("path: could not reach target");
            var set = l.Layer(Builtins.Str(p, "layer"));
            foreach (var c in path) set.Add(c);
            ctx.Vars["lastPath"] = path;
        }

        internal static GenCell Endpoint(GenLevel l, JObject p, string key, GenRng rng)
        {
            if (p[key] is JArray a && a.Count >= 2)
                return new GenCell((int)a[0], (int)a[1]);
            // "edges": random distinct border cells
            var edges = new List<GenCell>();
            foreach (var c in l.AllCells())
                if (c.X == 0 || c.Y == 0 || c.X == l.width - 1 || c.Y == l.height - 1)
                    edges.Add(c);
            return edges[rng.Int(edges.Count)];
        }

        static readonly int[] DX = { 1, -1, 0, 0 };
        static readonly int[] DY = { 0, 0, 1, -1 };

        static List<GenCell> Walk(GenLevel l, GenRng rng, GenCell from, GenCell to,
            float winding, int maxSteps, List<string> avoid)
        {
            var visited = new HashSet<GenCell> { from };
            var path = new List<GenCell> { from };
            var cur = from;
            for (var s = 0; s < maxSteps; s++)
            {
                if (cur.Equals(to)) return path;
                // gather moves, sort by closeness, bias toward winding
                var opts = new List<GenCell>();
                for (var d = 0; d < 4; d++)
                {
                    var n = new GenCell(cur.X + DX[d], cur.Y + DY[d]);
                    if (!l.InBounds(n) || l.AnyLayer(avoid, n)) continue;
                    opts.Add(n);
                }
                if (opts.Count == 0) break;
                opts.Sort((a, b) => a.ManhattanTo(to).CompareTo(b.ManhattanTo(to)));
                // prefer unvisited; winding decides how often we pick a non-optimal step
                var pick = 0;
                var fresh = opts.FindAll(c => !visited.Contains(c));
                var pool = fresh.Count > 0 ? fresh : opts;
                if (pool.Count > 1 && rng.Chance(winding))
                    pick = rng.Int(1, pool.Count);
                else if (pool[0].ManhattanTo(to) > pool[pool.Count - 1].ManhattanTo(to))
                    pick = pool.Count - 1;
                cur = pool[pick];
                path.Add(cur);
                visited.Add(cur);
            }
            return cur.Equals(to) ? path : null;
        }
    }

    /// <summary>
    /// {"type":"connect","layer":"x","via":"x"|"corridor"} — flood-fills the
    /// layer; if it has several components, connects them with straight
    /// corridors carved into `via` (defaults to the layer itself).
    /// </summary>
    public sealed class ConnectStep : IGenStep
    {
        public void Apply(GenContext ctx, JObject p)
        {
            var l = ctx.Level;
            var name = Builtins.Str(p, "layer");
            var set = l.Layer(name);
            var via = l.Layer(Builtins.Str(p, "via", name));
            var comps = Components(l, set);
            for (var i = 1; i < comps.Count; i++)
            {
                // connect each component to the first with an L corridor
                var a = comps[i][0];
                var b = comps[0][0];
                var cur = a;
                while (cur.X != b.X) { cur = new GenCell(cur.X + (b.X > cur.X ? 1 : -1), cur.Y); via.Add(cur); set.Add(cur); }
                while (cur.Y != b.Y) { cur = new GenCell(cur.X, cur.Y + (b.Y > cur.Y ? 1 : -1)); via.Add(cur); set.Add(cur); }
                comps = Components(l, set);
                if (comps.Count <= 1) break;
            }
        }

        internal static List<List<GenCell>> Components(GenLevel l, HashSet<GenCell> set)
        {
            var remaining = new HashSet<GenCell>(set);
            var comps = new List<List<GenCell>>();
            while (remaining.Count > 0)
            {
                var comp = new List<GenCell>();
                var e = remaining.GetEnumerator(); e.MoveNext();
                var start = e.Current;
                var q = new Queue<GenCell>(); q.Enqueue(start);
                remaining.Remove(start);
                while (q.Count > 0)
                {
                    var c = q.Dequeue(); comp.Add(c);
                    foreach (var n in Neighbors(c))
                        if (remaining.Remove(n)) q.Enqueue(n);
                }
                comps.Add(comp);
            }
            return comps;
        }

        internal static IEnumerable<GenCell> Neighbors(GenCell c)
        {
            yield return new GenCell(c.X + 1, c.Y);
            yield return new GenCell(c.X - 1, c.Y);
            yield return new GenCell(c.X, c.Y + 1);
            yield return new GenCell(c.X, c.Y - 1);
        }
    }

    /// <summary>
    /// {"type":"maze","layer":"walls"|"path","mode":"walls"} — recursive
    /// backtracker. "walls" marks wall cells (grid still walkable between
    /// them); "path" marks the carved cells instead.
    /// </summary>
    public sealed class MazeStep : IGenStep
    {
        public void Apply(GenContext ctx, JObject p)
        {
            var l = ctx.Level;
            var walls = Builtins.Str(p, "mode", "walls") == "walls";
            var open = new HashSet<GenCell>();
            var start = new GenCell(1, 1);
            var stack = new Stack<GenCell>();
            stack.Push(start); open.Add(start);
            while (stack.Count > 0)
            {
                var cur = stack.Peek();
                var next = new List<GenCell>();
                foreach (var n in Jump(cur))
                    if (l.InBounds(n) && n.X > 0 && n.Y > 0 && n.X < l.width && n.Y < l.height && !open.Contains(n))
                        next.Add(n);
                if (next.Count == 0) { stack.Pop(); continue; }
                var pick = next[ctx.Rng.Int(next.Count)];
                open.Add(new GenCell((cur.X + pick.X) / 2, (cur.Y + pick.Y) / 2));
                open.Add(pick);
                stack.Push(pick);
            }
            var set = l.Layer(Builtins.Str(p, "layer"));
            foreach (var c in l.AllCells())
                if (walls ? !open.Contains(c) : open.Contains(c)) set.Add(c);
        }

        static IEnumerable<GenCell> Jump(GenCell c)
        {
            yield return new GenCell(c.X + 2, c.Y);
            yield return new GenCell(c.X - 2, c.Y);
            yield return new GenCell(c.X, c.Y + 2);
            yield return new GenCell(c.X, c.Y - 2);
        }
    }

    /// <summary>
    /// {"type":"rooms","layer":"floor","count":4,"corridor":"floor"} — BSP
    /// partition into rectangular rooms joined by corridors. Room ids also go
    /// to per-room layers "room<i>" for later placement.
    /// </summary>
    public sealed class RoomsStep : IGenStep
    {
        public void Apply(GenContext ctx, JObject p)
        {
            var l = ctx.Level;
            var floorName = Builtins.Str(p, "layer");
            var floor = l.Layer(floorName);
            var count = Builtins.Int(p, "count", 4);
            var rng = ctx.Rng;
            var leaves = new List<(int x, int y, int w, int h)> { (0, 0, l.width, l.height) };
            for (var i = 1; i < count && leaves.Count > 0; i++)
            {
                var idx = rng.Int(leaves.Count);
                var leaf = leaves[idx];
                if (leaf.w < 4 && leaf.h < 4) break;
                leaves.RemoveAt(idx);
                var vertical = leaf.w > leaf.h || (leaf.w == leaf.h && rng.Chance(0.5f));
                if (vertical)
                {
                    var cut = rng.Int(2, leaf.w - 2);
                    leaves.Add((leaf.x, leaf.y, cut, leaf.h));
                    leaves.Add((leaf.x + cut, leaf.y, leaf.w - cut, leaf.h));
                }
                else
                {
                    var cut = rng.Int(2, leaf.h - 2);
                    leaves.Add((leaf.x, leaf.y, leaf.w, cut));
                    leaves.Add((leaf.x, leaf.y + cut, leaf.w, leaf.h - cut));
                }
            }
            var centers = new List<GenCell>();
            for (var i = 0; i < leaves.Count; i++)
            {
                var (x, y, w, h) = leaves[i];
                var insetX = System.Math.Min(w - 1, 1);
                var insetY = System.Math.Min(h - 1, 1);
                var room = l.Layer($"room{i}");
                for (var ry = y + insetY; ry < y + h - (w > 2 ? 0 : 0); ry++)
                    for (var rx = x + insetX; rx < x + w; rx++)
                    {
                        var c = new GenCell(rx, System.Math.Min(ry, l.height - 1));
                        if (l.InBounds(c)) { floor.Add(c); room.Add(c); }
                    }
                centers.Add(new GenCell(x + w / 2, y + h / 2));
            }
            // corridors between consecutive room centers
            for (var i = 1; i < centers.Count; i++)
            {
                var cur = centers[i - 1]; var tgt = centers[i];
                while (cur.X != tgt.X) { cur = new GenCell(cur.X + (tgt.X > cur.X ? 1 : -1), cur.Y); floor.Add(cur); }
                while (cur.Y != tgt.Y) { cur = new GenCell(cur.X, cur.Y + (tgt.Y > cur.Y ? 1 : -1)); floor.Add(cur); }
            }
            ctx.Vars["rooms"] = centers;
        }
    }
}
