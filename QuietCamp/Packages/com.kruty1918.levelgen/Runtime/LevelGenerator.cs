using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Kruty1918.LevelGen
{
    /// <summary>
    /// The generation engine: runs a recipe's step pipeline inside a seeded
    /// context, retrying failed attempts until one passes or maxAttempts is
    /// hit. Steps are looked up by their "type" string in a registry — games
    /// can register their own steps alongside the built-in library.
    /// </summary>
    public static class LevelGenerator
    {
        static readonly Dictionary<string, Func<IGenStep>> Registry =
            new Dictionary<string, Func<IGenStep>>(StringComparer.OrdinalIgnoreCase);

        static LevelGenerator() => Steps.Builtins.RegisterAll(Registry);

        /// <summary>Register a custom step type (idempotent).</summary>
        public static void Register(string type, Func<IGenStep> factory) => Registry[type] = factory;
        public static bool HasStep(string type) => Registry.ContainsKey(type);

        /// <summary>Runs the recipe; returns the first passing attempt.</summary>
        public static GenResult Generate(GenRecipe recipe, int seed = 0)
        {
            if (recipe == null) throw new GenRecipeException("recipe is null");
            var issues = new List<string>();
            var rng = new GenRng(recipe.seed + seed);
            for (var attempt = 1; attempt <= Math.Max(1, recipe.maxAttempts); attempt++)
            {
                var attemptSeed = string.Equals(recipe.seedStrategy, "random", StringComparison.OrdinalIgnoreCase)
                    ? rng.Int(int.MaxValue)
                    : recipe.seed + seed + attempt - 1;
                var ctx = new GenContext
                {
                    Level = new GenLevel
                    {
                        width = recipe.width, height = recipe.height,
                        seed = attemptSeed, recipe = recipe.name, attempts = attempt,
                        Props = (JObject)recipe.Props.DeepClone()
                    },
                    Rng = new GenRng(attemptSeed)
                };
                try
                {
                    foreach (var stepJson in recipe.Steps)
                        StepFor(stepJson).Apply(ctx, stepJson);
                    return GenResult.Success(ctx.Level);
                }
                catch (GenFailedException f)
                {
                    issues.Add($"attempt {attempt}: {f.Message}");
                }
            }
            return GenResult.Failure(issues, recipe.maxAttempts);
        }

        static IGenStep StepFor(JObject stepJson)
        {
            var type = (string)stepJson["type"];
            if (type == null || !Registry.TryGetValue(type, out var factory))
                throw new GenRecipeException($"unknown step type '{type}'");
            return factory();
        }
    }
}
