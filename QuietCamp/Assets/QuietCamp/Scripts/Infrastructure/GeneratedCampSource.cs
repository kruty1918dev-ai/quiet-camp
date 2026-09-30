using System;
using System.Collections.Generic;
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
        const string RecipesFolder = "QuietCamp/Recipes";
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
            var recipe = GenRecipe.FromJson(asset.text);
            var rng = new System.Random(recipe.seed + index * 7919);
            var genFailed = 0; var timeouts = 0; var unsat = 0; var invalid = 0;
            var lastIssues = "";
            for (var attempt = 0; attempt < MaxCandidates; attempt++)
            {
                var seed = rng.Next();
                var result = LevelGenerator.Generate(recipe, seed);
                if (!result.Ok) { genFailed++; lastIssues = string.Join("; ", result.Issues); continue; }
                var level = BuildLevel(result.Level, index, attempt + 1, rng);
                var solver = new CampSolver(level, Array.Empty<Placement>(), 1.5);
                var status = solver.Step();
                while (status == CampSolver.Status.Searching) status = solver.Step(0.05);
                if (status != CampSolver.Status.Solved)
                {
                    if (status == CampSolver.Status.Timeout) timeouts++; else unsat++;
                    lastIssues = $"solver: {status}"; continue;
                }
                level.witness = solver.Solution;
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
            level.shade = CellsOf(g, "shade");
            level.noise = CellsOf(g, "noise");

            var guests = ComposeGuests(g, level, rng);
            level.guests = guests.ToArray();
            level.friends = MaybeFriends(guests, rng);
            level.contentHash = Hash(level);
            return level;
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
            var json = JsonConvert.SerializeObject(level);
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(json));
            var sb = new StringBuilder(bytes.Length * 2);
            foreach (var b in bytes) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }
    }
}
