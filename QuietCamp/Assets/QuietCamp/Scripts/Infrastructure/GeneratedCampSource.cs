using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Kruty1918.LevelGen;
using Newtonsoft.Json;
using QuietCamp.Domain;
using UnityEngine;

namespace QuietCamp.Infrastructure
{
    /// <summary>
    /// Runtime source for procedurally generated camp levels: runs a LevelGen
    /// recipe (Resources/QuietCamp/Recipes/&lt;name&gt;.json), maps the generic
    /// grid output to LevelData, then asks CampSolver for a witness — only
    /// solvable candidates ship. Level ids look like "gen:qc_camp:12".
    /// </summary>
    public static class GeneratedCampSource
    {
        public const string Prefix = "gen:";
        internal const string RecipesFolder = "QuietCamp/Recipes";
        const int MaxCandidates = 30;

        public static bool IsGeneratedId(string levelId)
            => levelId != null && levelId.StartsWith(Prefix, StringComparison.Ordinal);

        /// <summary>"gen:qc_camp:12" → ("qc_camp", 12).</summary>
        public static bool TryParseId(string levelId, out string recipe, out int index)
        {
            recipe = null; index = 0;
            if (!IsGeneratedId(levelId)) return false;
            var parts = levelId.Substring(Prefix.Length).Split(':');
            if (parts.Length != 2 || !int.TryParse(parts[1], out index)) return false;
            recipe = parts[0];
            return recipe.Length > 0;
        }

        /// <summary>Generates a solved QC level or throws after MaxCandidates tries.</summary>
        public static LevelData Generate(string recipeName, int index)
        {
            var asset = Resources.Load<TextAsset>($"{RecipesFolder}/{recipeName}");
            if (asset == null)
                throw new InvalidOperationException($"recipe '{recipeName}' not found in Resources/{RecipesFolder}");
            if(recipeName=="qc_camp")
            {
                int number=10+Math.Max(1,index);
                var campaign=CampCampaignAuthoring.Create($"gen:qc_camp:{index}",number,CampCampaignAuthoring.SeedForNumber(number));
                campaign.contentHash=Hash(campaign);return campaign;
            }
            var recipe = GenRecipe.FromJson(asset.text);
            var sizes = new[] { (4,6), (5,7), (6,5), (6,8), (8,6), (7,7) };
            var size = sizes[(Math.Max(1,index)-1) % sizes.Length];
            recipe.width = size.Item1; recipe.height = size.Item2;
            recipe.Steps.Clear();
            foreach (var step in Newtonsoft.Json.Linq.JArray.Parse(@"[
                {'type':'rect','layer':'entry_band','from':[0,0],'size':[1,1]},
                {'type':'rect','layer':'top_band','from':[0,0],'size':[1,1]},
                {'type':'scatter','layer':'entry','count':1,'on':['entry_band'],'strict':true},
                {'type':'scatter','layer':'blocked','count':2,'minDist':2,'on':['top_band'],'avoid':['entry']}
            ]")) recipe.Steps.Add((Newtonsoft.Json.Linq.JObject)step);
            recipe.Steps[0]["from"] = new Newtonsoft.Json.Linq.JArray(0,recipe.height-1);
            recipe.Steps[0]["size"] = new Newtonsoft.Json.Linq.JArray(recipe.width,1);
            recipe.Steps[1]["size"] = new Newtonsoft.Json.Linq.JArray(recipe.width,Math.Max(2,recipe.height-2));
            var rng = new System.Random(recipe.seed + index * 7919);
            var genFailed = 0; var timeouts = 0; var unsat = 0; var invalid = 0;
            var lastIssues = "";
            for (var attempt = 0; attempt < MaxCandidates; attempt++)
            {
                var seed = rng.Next();
                var result = LevelGenerator.Generate(recipe, seed);
                if (!result.Ok) { genFailed++; lastIssues = string.Join("; ", result.Issues); continue; }
                var level = BuildLevel(result.Level, index, attempt + 1, rng);
                level.id = $"gen:{recipeName}:{index}";
                ConfigureEnvironment(level, level.order);
                ConfigureAccess(level, level.order, false);
                var solver = new CampSolver(level, Array.Empty<Placement>(), 2.5);
                var status = solver.Step();
                while (status == CampSolver.Status.Searching) status = solver.Step(0.05);
                if (status != CampSolver.Status.Solved)
                {
                    if (status == CampSolver.Status.Timeout) timeouts++; else unsat++;
                    lastIssues = $"solver: {status}"; continue;
                }
                level.witness = solver.Solution;
                level.contentHash = Hash(level);
                var errors = LevelContentValidator.Validate(level);
                if (errors.Count == 0)
                    return level;
                invalid++;
                lastIssues = string.Join("; ", errors);
            }
            throw new InvalidOperationException(
                $"generated level '{recipeName}:{index}' — no solvable candidate in {MaxCandidates} attempts " +
                $"(genFailed={genFailed}, timeouts={timeouts}, unsat={unsat}, invalid={invalid}; last: {lastIssues})");
        }

        /// <summary>GenLevel layers → LevelData; guests composed deterministically.</summary>
        static LevelData BuildLevel(GenLevel g, int index, int attempt, System.Random rng)
        {
            var level = new LevelData
            {
                schemaVersion = 1, ruleVersion = 1,
                id = $"GEN{index:000}",
                order = 10 + index,
                chapter = 2,
                seed = g.seed,
                generatorVersion = "levelgen-1",
                generationAttempt = attempt,
                decorSeed = g.seed ^ 0x5f5f,
                lighting = index % 5 == 4 ? "evening" : "day",
                width = g.width,
                height = g.height
            };
            level.entry = First(g, "entry");
            level.blocked = CellsOf(g, "blocked");
            level.shade = Array.Empty<int[]>();
            level.noise = Array.Empty<int[]>();

            var guests = ComposeGuests(g, level, rng);
            level.guests = guests.ToArray();
            level.friends = MaybeFriends(guests, rng);
            level.contentHash = Hash(level);
            return level;
        }

        public static void ConfigureEnvironment(LevelData level, int index)
        {
            level.environmentPreset = index <= 10 ? "meadow" : index <= 20 ? "pines" : "firelight";
            level.chapter = (level.order-1)/10+1;
            var shadeWanted = level.guests.Any(g => g.shade) || (index > 3 && index % 3 != 0);
            level.canopies = shadeWanted ? new[] { new ShadeCanopyData
                { x = 1.1f, z = Math.Min(level.height-1.1f, index == 10 ? level.height-1.3f : 1.3f + index%2*(level.height-2.6f)), radiusX=1.55f, radiusZ=1.5f } }
                : Array.Empty<ShadeCanopyData>();
            level.shade = ShadeProjection.Cells(level);
            var blocked = level.blocked.Select(c => new Cell(c[0],c[1])).ToList();
            // Every fire occupies a cell. Larger clearings alternate warm and quiet layouts.
            if (index == 3 && blocked.Count > 0)
                level.noise = new[] { new[] { blocked[0].X, blocked[0].Z } };
            else if (index > 7 && index % 3 == 0)
            {
                var fire = new Cell(level.width-1,0);
                if (!blocked.Contains(fire)) blocked.Add(fire);
                level.noise = new[] { new[] { fire.X, fire.Z } };
            }
            else if (level.noise == null) level.noise = Array.Empty<int[]>();
            foreach (var n in level.noise)
                if (!blocked.Contains(new Cell(n[0],n[1]))) blocked.Add(new Cell(n[0],n[1]));
            level.blocked = blocked.Select(c=>new[]{c.X,c.Z}).ToArray();
            var props = new[] { "stone_largeA", "stump_round", "log", "tree_default" };
            level.objects = level.blocked.Select((c,i)=>new EnvironmentObjectData
            {
                x=c[0], z=c[1], rotation=(i+index)%4,
                assetId = level.noise.Any(n=>n[0]==c[0] && n[1]==c[1]) ? "campfire_stones" : props[(i+index)%props.Length]
            }).ToArray();
            if (index > 10)
            {
                level.guests[0].shade = shadeWanted;
                level.guests[level.guests.Length-1].quiet = level.noise.Length > 0;
                level.friends = index%4 == 0 && level.guests.Length >= 3
                    ? new[] { new[] { level.guests[0].id, level.guests[1].id } } : Array.Empty<string[]>();
                level.lighting = index > 20 && index%2 == 0 ? "evening" : index%4 == 0 ? "morning" : "day";
            }
        }

        /// <summary>Choose varied edges in the same open component. Authored
        /// upgrades preserve the witness; generation solves afterwards.</summary>
        public static void ConfigureAccess(LevelData level,int number,bool preserveWitness)
        {
            if(number<=3)return;
            var occupied=new HashSet<Cell>(level.blocked.Select(c=>new Cell(c[0],c[1])));
            if(preserveWitness)foreach(var placement in level.witness??Array.Empty<Placement>())occupied.UnionWith(RuleEvaluator.Footprint(placement));
            var origin=new Cell(level.entry[0],level.entry[1]);var candidates=new List<Cell>();
            for(int x=0;x<level.width;x++)for(int z=0;z<level.height;z++)
            {
                var c=new Cell(x,z);
                if((x==0||z==0||x==level.width-1||z==level.height-1)&&!occupied.Contains(c)
                    &&RuleEvaluator.Path(level,occupied,origin,c)!=null)candidates.Add(c);
            }
            if(candidates.Count==0)return;
            Cell Target(int side)=>side==0?new Cell(level.width/2,0):side==1?new Cell(level.width-1,level.height/2)
                :side==2?new Cell(level.width/2,level.height-1):new Cell(0,level.height/2);
            Cell Pick(int side,IEnumerable<Cell> cells)=>cells.OrderBy(c=>Math.Abs(c.X-Target(side).X)+Math.Abs(c.Z-Target(side).Z)).ThenBy(c=>c.X).ThenBy(c=>c.Z).First();
            int edge=(number-4)%4;var primary=Pick(edge,candidates);level.entry=new[]{primary.X,primary.Z};
            var points=new List<AccessPointData>();
            if(number>=5&&number%2==1)
            {
                var exits=candidates.Where(c=>Math.Abs(c.X-primary.X)+Math.Abs(c.Z-primary.Z)>2).ToArray();
                if(exits.Length>0){var exit=Pick((edge+2)%4,exits);points.Add(new AccessPointData{id="trail-out",kind="exit",x=exit.X,z=exit.Z});}
            }
            if(number>=10&&number%7==0)
            {
                var entries=candidates.Where(c=>!c.Equals(primary)&&!points.Any(p=>p.x==c.X&&p.z==c.Z)).ToArray();
                if(entries.Length>0){var entry=Pick((edge+1)%4,entries);points.Add(new AccessPointData{id="forest-entry",kind="entry",x=entry.X,z=entry.Z});}
            }
            level.accessPoints=points.ToArray();
        }

        /// <summary>
        /// Guest roster per level: always a base pair; adds a shade guest when
        /// the mask can host a 2×2 tent, a quiet guest when open space sits
        /// far from the fire — keeping solver odds high.
        /// </summary>
        static List<GuestData> ComposeGuests(GenLevel g, LevelData level, System.Random rng)
        {
            var guests = new List<GuestData>();
            var count = 2 + rng.Next(2); // 2..3
            var wantsShade = Has2x2(g, "shade") && rng.Next(3) == 0;
            var wantsQuiet = g.Layer("noise", false) is { Count: > 0 } && rng.Next(3) == 0;
            for (var i = 0; i < count; i++)
            {
                var g2 = new GuestData
                {
                    id = $"g{i + 1}",
                    nameKey = $"guest.{i + 1}",
                    assetId = i % 2 == 0 ? "tent_smallOpen" : "tent_detailedOpen"
                };
                if (wantsShade) { g2.shade = true; wantsShade = false; }
                else if (wantsQuiet) { g2.quiet = true; wantsQuiet = false; }
                guests.Add(g2);
            }
            return guests;
        }

        static string[][] MaybeFriends(List<GuestData> guests, System.Random rng)
            => guests.Count >= 3 && rng.Next(3) == 0
                ? new[] { new[] { guests[0].id, guests[1].id } }
                : Array.Empty<string[]>();

        static bool Has2x2(GenLevel g, string layer)
        {
            var set = g.Layer(layer, false);
            if (set == null) return false;
            foreach (var c in set)
                if (set.Contains(new GenCell(c.X + 1, c.Y)) &&
                    set.Contains(new GenCell(c.X, c.Y + 1)) &&
                    set.Contains(new GenCell(c.X + 1, c.Y + 1)))
                    return true;
            return false;
        }

        static int[] First(GenLevel g, string layer)
        {
            foreach (var c in g.Layer(layer, false) ?? new HashSet<GenCell>())
                return new[] { c.X, c.Y };
            return Array.Empty<int>();
        }

        static int[][] CellsOf(GenLevel g, string layer)
        {
            var set = g.Layer(layer, false);
            if (set == null || set.Count == 0) return Array.Empty<int[]>();
            var arr = new int[set.Count][];
            var i = 0;
            foreach (var c in set) arr[i++] = new[] { c.X, c.Y };
            return arr;
        }

        static string Hash(LevelData level)
        {
            var previous=level.contentHash;level.contentHash="";
            var json = JsonConvert.SerializeObject(level);
            level.contentHash=previous;
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(json));
            var sb = new StringBuilder(bytes.Length * 2);
            foreach (var b in bytes) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }
    }
}
