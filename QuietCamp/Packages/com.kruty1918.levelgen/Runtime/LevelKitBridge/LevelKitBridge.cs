#if KRUTY1918_LEVELKIT
using Kruty1918.LevelKit;

namespace Kruty1918.LevelGen
{
    /// <summary>
    /// Optional bridge — this assembly compiles only when
    /// com.kruty1918.levelkit is also in the project (its asmdef carries the
    /// matching versionDefine + defineConstraint). Converts a GenLevel into a
    /// LevelDocument so generated content flows through the same LevelKit
    /// profile/adapter pipeline as authored files.
    /// </summary>
    public static class LevelKitBridge
    {
        public static LevelDocument ToDocument(GenLevel level)
        {
            var doc = new LevelDocument { Width = level.width, Height = level.height };
            foreach (var kv in level.Layers)
            {
                var layer = doc.Layer(kv.Key);
                foreach (var c in kv.Value)
                    layer.Add(new CellPos(c.X, c.Y));
            }
            foreach (var e in level.Entities)
            {
                var ent = new LevelEntity
                {
                    Kind = e.kind, X = e.x, Z = e.y, Rotation = e.rotation
                };
                if (e.props != null)
                    foreach (var prop in e.props)
                        ent.Data[prop.Key] = prop.Value;
                doc.Entities.Add(ent);
            }
            if (level.Props != null)
                foreach (var prop in level.Props)
                    doc.Props[prop.Key] = prop.Value;
            doc.Props["seed"] = level.seed;
            doc.Props["generator"] = "levelgen";
            doc.Props["recipe"] = level.recipe;
            return doc;
        }

        /// <summary>Generate → LevelDocument in one call.</summary>
        public static LevelDocument GenerateToDocument(GenRecipe recipe, int seed = 0)
        {
            var result = LevelGenerator.Generate(recipe, seed);
            if (!result.Ok)
                throw new GenFailedException(
                    $"generation failed after {result.Attempts} attempts: {string.Join("; ", result.Issues)}");
            return ToDocument(result.Level);
        }
    }
}
#endif
