using System.Collections.Generic;
using System.Linq;
namespace QuietCamp.Domain
{
    /// <summary>
    /// Structural validation for authored LevelData before it reaches gameplay.
    /// Returns every problem found; an empty list means the map is usable.
    /// </summary>
    public static class LevelContentValidator
    {
        public static List<string> Validate(LevelData l)
        {
            var errors = new List<string>();
            if (l == null) { errors.Add("level:null"); return errors; }
            if (l.schemaVersion != 1) errors.Add("schemaVersion:" + l.schemaVersion);
            if (l.ruleVersion != 1 && l.ruleVersion != 2) errors.Add("ruleVersion:" + l.ruleVersion);
            if (string.IsNullOrWhiteSpace(l.id)) errors.Add("id:empty");
            if (l.width < 4 || l.width > 8 || l.height < 4 || l.height > 8)
                errors.Add($"size:{l.width}x{l.height}");
            if (l.entry == null || l.entry.Length != 2) errors.Add("entry:shape");
            else if (!RuleEvaluator.Inside(l, new Cell(l.entry[0], l.entry[1]))) errors.Add("entry:outside");
            else if (l.ruleVersion == 2 && l.entry[0] != 0 && l.entry[1] != 0
                && l.entry[0] != l.width-1 && l.entry[1] != l.height-1) errors.Add("entry:edge");

            ValidateMask(l, l.blocked, "blocked", errors);
            ValidateMask(l, l.shade, "shade", errors);
            ValidateMask(l, l.noise, "noise", errors);
            if (l.entry != null && l.entry.Length==2 && l.blocked != null
                && l.blocked.Any(c => c != null && c.Length==2 && c[0] == l.entry[0] && c[1] == l.entry[1]))
                errors.Add("entry:blocked");
            var accessIds=new HashSet<string>();var accessCells=new HashSet<Cell>();
            if(l.entry!=null&&l.entry.Length==2)accessCells.Add(new Cell(l.entry[0],l.entry[1]));
            foreach(var point in l.accessPoints??System.Array.Empty<AccessPointData>())
            {
                if(point==null){errors.Add("access:null");continue;}
                if(string.IsNullOrWhiteSpace(point.id)||!accessIds.Add(point.id))errors.Add("access:id");
                if(point.kind!="entry"&&point.kind!="exit")errors.Add("access:kind");
                var cell=new Cell(point.x,point.z);
                if(!RuleEvaluator.Inside(l,cell))errors.Add("access:outside");
                if(!accessCells.Add(cell))errors.Add("access:duplicate");
                if(l.blocked!=null&&l.blocked.Any(c=>c!=null&&c.Length==2&&c[0]==point.x&&c[1]==point.z))errors.Add("access:blocked");
                if(point.x!=0&&point.z!=0&&point.x!=l.width-1&&point.z!=l.height-1)errors.Add("access:edge");
            }
            if((l.accessPoints?.Length??0)>3)errors.Add("access:count");

            var exterior = new HashSet<Cell>();
            foreach(var c in l.exteriorWalkable ?? System.Array.Empty<int[]>())
            {
                if(c==null||c.Length!=2){errors.Add("exterior:shape");continue;}
                var cell=new Cell(c[0],c[1]);
                if(l.ruleVersion!=2)errors.Add("exterior:version");
                if(RuleEvaluator.Inside(l,cell))errors.Add("exterior:inside");
                if(c[0]<-2||c[1]<-2||c[0]>l.width+1||c[1]>l.height+1)errors.Add("exterior:bounds");
                if(!exterior.Add(cell))errors.Add("exterior:duplicate");
            }
            if(exterior.Count>64)errors.Add("exterior:count");
            if(errors.Count==0 && exterior.Count>0)
            {
                var occupied=new HashSet<Cell>(l.blocked.Select(c=>new Cell(c[0],c[1])));
                var origin=new Cell(l.entry[0],l.entry[1]);
                if(exterior.Any(c=>CampWalkability.Path(l,occupied,origin,c)==null))errors.Add("exterior:disconnected");
            }

            var objectCells=new HashSet<Cell>();
            foreach (var obj in l.objects ?? System.Array.Empty<EnvironmentObjectData>())
            {
                if (obj == null || string.IsNullOrEmpty(obj.assetId)) { errors.Add("object:asset"); continue; }
                if(l.ruleVersion==2&&!objectCells.Add(new Cell(obj.x,obj.z)))errors.Add("object:duplicate");
                if(l.ruleVersion==2&&(obj.rotation<0||obj.rotation>3))errors.Add("object:rotation");
                if (!RuleEvaluator.Inside(l, new Cell(obj.x, obj.z))) errors.Add("object:outside");
                if (l.blocked != null && !l.blocked.Any(c => c != null && c.Length==2 && c[0] == obj.x && c[1] == obj.z)) errors.Add("object:unblocked");
                if (obj.assetId == "campfire_stones" && l.noise != null && !l.noise.Any(c => c != null && c.Length==2 && c[0] == obj.x && c[1] == obj.z)) errors.Add("object:fire-noise");
            }
            if(l.environment!=null)
            {
                var env=l.environment;
                if(!new[]{"meadow","pines","forest","shore"}.Contains(env.biomeId))errors.Add("environment:biome");
                if(!new[]{"spring","summer","autumn","winter"}.Contains(env.seasonId))errors.Add("environment:season");
                if(!new[]{"clear","cloudy","rain","mist"}.Contains(env.weatherId))errors.Add("environment:weather");
                if(float.IsNaN(env.moisture)||float.IsInfinity(env.moisture)||env.moisture<0||env.moisture>1)errors.Add("environment:moisture");
                if(float.IsNaN(env.treeDensity)||float.IsInfinity(env.treeDensity)||env.treeDensity<0||env.treeDensity>1)errors.Add("environment:trees");
                if(env.shore!=null)
                {
                    var shore=env.shore;
                    if(shore.kind!="stream"&&shore.kind!="lake")errors.Add("shore:kind");
                    if(!new[]{"left","right","back","front"}.Contains(shore.side))errors.Add("shore:side");
                    float half=shore.side=="left"||shore.side=="right"?l.width*.5f:l.height*.5f;
                    if(float.IsNaN(shore.width)||float.IsInfinity(shore.width)||shore.width<=0||shore.width>20)errors.Add("shore:width");
                    // The inner bank stays outside the puzzle and its trail apron.
                    if(float.IsNaN(shore.offset)||float.IsInfinity(shore.offset)||shore.offset-shore.width*.5f<half+1.6f)errors.Add("shore:clearance");
                    if(l.entry!=null&&l.entry.Length==2)
                        foreach(var point in CampAccess.Points(l))
                            if(shore.side=="left"&&point.X==0||shore.side=="right"&&point.X==l.width-1
                                ||shore.side=="back"&&point.Z==0||shore.side=="front"&&point.Z==l.height-1)
                                errors.Add("shore:trail");
                }
            }
            if (!string.IsNullOrEmpty(l.environmentPreset))
            {
                foreach(var cell in l.blocked ?? System.Array.Empty<int[]>())
                    if(cell!=null&&cell.Length==2&&!System.Array.Exists(l.objects??System.Array.Empty<EnvironmentObjectData>(),o=>o!=null&&o.x==cell[0]&&o.z==cell[1]))errors.Add("blocked:object-missing");
                foreach(var cell in l.noise ?? System.Array.Empty<int[]>())
                    if(cell!=null&&cell.Length==2&&!System.Array.Exists(l.objects??System.Array.Empty<EnvironmentObjectData>(),o=>o!=null&&o.x==cell[0]&&o.z==cell[1]&&o.assetId=="campfire_stones"))errors.Add("noise:fire-missing");
            }
            foreach (var c in l.canopies ?? System.Array.Empty<ShadeCanopyData>())
                if (c == null || float.IsNaN(c.x) || float.IsInfinity(c.x) || float.IsNaN(c.z) || float.IsInfinity(c.z) || float.IsNaN(c.radiusX) || float.IsInfinity(c.radiusX) || c.radiusX <= 0
                    || float.IsNaN(c.radiusZ) || float.IsInfinity(c.radiusZ) || c.radiusZ <= 0) errors.Add("canopy:radius");
            if (errors.Count == 0 && l.canopies != null && l.canopies.Length > 0)
            {
                var projected = new HashSet<Cell>(ShadeProjection.Cells(l).Select(c => new Cell(c[0],c[1])));
                if (!projected.SetEquals(l.shade.Select(c => new Cell(c[0],c[1])))) errors.Add("shade:projection");
            }
            var ids = new HashSet<string>();
            if (l.guests == null || l.guests.Length < 2 || l.guests.Length > 6)
                errors.Add("guests:count");
            else
                foreach (var g in l.guests)
                {
                    if (g == null || string.IsNullOrWhiteSpace(g.id)) { errors.Add("guest:id"); continue; }
                    if (!ids.Add(g.id)) errors.Add("guest:dup:" + g.id);
                    if (string.IsNullOrWhiteSpace(g.assetId)) errors.Add("guest:asset:" + g.id);
                    if (string.IsNullOrWhiteSpace(g.nameKey)) errors.Add("guest:nameKey:" + g.id);
                }

            if (l.friends != null)
                foreach (var pair in l.friends)
                {
                    if (pair == null || pair.Length != 2) { errors.Add("friends:shape"); continue; }
                    if (pair[0] == pair[1]) errors.Add("friends:self:" + pair[0]);
                    if (!ids.Contains(pair[0]) || !ids.Contains(pair[1]))
                        errors.Add($"friends:unknown:{pair[0]},{pair[1]}");
                }

            if (l.witness != null)
            {
                var seen = new HashSet<string>();
                foreach (var p in l.witness)
                {
                    if (p == null || !ids.Contains(p.guestId)) { errors.Add("witness:guest"); continue; }
                    if (!seen.Add(p.guestId)) errors.Add("witness:dup:" + p.guestId);
                    if (p.rotation < 0 || p.rotation > 3) errors.Add("witness:rotation:" + p.guestId);
                }
                if (errors.Count == 0 && !RuleEvaluator.Evaluate(l, l.witness).IsSolved)
                    errors.Add("witness:unsolved");
            }
            return errors;
        }

        static void ValidateMask(LevelData l, int[][] mask, string name, List<string> errors)
        {
            if (mask == null) { errors.Add(name + ":null"); return; }
            var seen = new HashSet<Cell>();
            foreach (var c in mask)
            {
                if (c == null || c.Length != 2) { errors.Add(name + ":shape"); continue; }
                var cell = new Cell(c[0], c[1]);
                if (!RuleEvaluator.Inside(l, cell)) errors.Add($"{name}:outside:{cell}");
                if (!seen.Add(cell)) errors.Add($"{name}:dup:{cell}");
            }
        }
    }
}
