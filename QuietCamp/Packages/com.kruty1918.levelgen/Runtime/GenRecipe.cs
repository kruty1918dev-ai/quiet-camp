using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Kruty1918.LevelGen
{
    /// <summary>
    /// A level-generation recipe: grid size, seeding strategy, an ordered list
    /// of step invocations and a retry policy. Parsed from JSON — see
    /// Samples~/Recipes for complete examples.
    /// </summary>
    [Serializable]
    public class GenRecipe
    {
        public string name = "recipe";
        public int width = 8, height = 8;

        /// <summary>Base seed; combined with <see cref="GenRequest.seed"/> at run time.</summary>
        public int seed = 1;

        /// <summary>"increment": attempt N uses seed+N. "random": reseed each attempt.</summary>
        public string seedStrategy = "increment";

        public int maxAttempts = 40;

        /// <summary>Ordered step invocations: {"type": "...", ...params}.</summary>
        public readonly List<JObject> Steps = new List<JObject>();

        /// <summary>Props copied onto every generated level.</summary>
        public JObject Props = new JObject();

        public static GenRecipe FromJson(string json)
        {
            JObject o;
            try { o = JObject.Parse(json); }
            catch (Exception e) { throw new GenRecipeException($"recipe JSON parse failed: {e.Message}"); }
            var r = new GenRecipe();
            r.name = (string)o["name"] ?? r.name;
            r.width = (int?)o["width"] ?? (int?)o["grid"]?["width"] ?? r.width;
            r.height = (int?)o["height"] ?? (int?)o["grid"]?["height"] ?? r.height;
            r.seed = (int?)o["seed"] ?? r.seed;
            r.seedStrategy = (string)o["seedStrategy"] ?? r.seedStrategy;
            r.maxAttempts = (int?)o["maxAttempts"] ?? (int?)o["retry"]?["maxAttempts"] ?? r.maxAttempts;
            if (o["props"] is JObject props) r.Props = (JObject)props.DeepClone();
            var steps = o["steps"] as JArray;
            if (steps == null || steps.Count == 0)
                throw new GenRecipeException("recipe has no 'steps' array");
            foreach (var s in steps)
            {
                if (s is not JObject step || step["type"] == null)
                    throw new GenRecipeException("every step needs a 'type' field");
                r.Steps.Add(step);
            }
            return r;
        }
    }

    /// <summary>Parameters for a single Generate call.</summary>
    public struct GenRequest
    {
        /// <summary>Combined with recipe.seed — use a level index for per-level variety.</summary>
        public int seed;
        public GenRequest(int seed) { this.seed = seed; }
    }

    /// <summary>Outcome of a generation run.</summary>
    public sealed class GenResult
    {
        public bool Ok;
        public GenLevel Level;
        public int Attempts;
        public readonly List<string> Issues = new List<string>();

        public static GenResult Success(GenLevel l) =>
            new GenResult { Ok = true, Level = l, Attempts = l.attempts };
        public static GenResult Failure(IEnumerable<string> issues, int attempts)
        {
            var r = new GenResult { Ok = false, Attempts = attempts };
            r.Issues.AddRange(issues);
            return r;
        }
    }
}
