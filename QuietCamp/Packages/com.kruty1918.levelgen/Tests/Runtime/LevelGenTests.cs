using Kruty1918.LevelGen;
using NUnit.Framework;
using UnityEngine;

public class LevelGenTests
{
    const string BoardRecipe = @"{
        ""name"": ""test_board"", ""width"": 6, ""height"": 6, ""seed"": 42,
        ""steps"": [
            { ""type"": ""border"", ""layer"": ""edge"" },
            { ""type"": ""noise"", ""layer"": ""trees"", ""scale"": 2, ""threshold"": 0.6 },
            { ""type"": ""scatter"", ""layer"": ""coins"", ""count"": 4, ""minDist"": 2, ""avoid"": [""trees""] },
            { ""type"": ""cells_to_entities"", ""layer"": ""coins"", ""kind"": ""coin"" },
            { ""type"": ""require"", ""check"": ""entityCount"", ""kind"": ""coin"", ""min"": 4 }
        ]}";

    [Test]
    public void Generate_IsDeterministic()
    {
        var r = GenRecipe.FromJson(BoardRecipe);
        var a = LevelGenerator.Generate(r, 0);
        var b = LevelGenerator.Generate(r, 0);
        Assert.IsTrue(a.Ok && b.Ok);
        Assert.AreEqual(a.Level.seed, b.Level.seed);
        Assert.AreEqual(a.Level.Entities.Count, b.Level.Entities.Count);
        foreach (var e in a.Level.Entities)
            Assert.IsTrue(b.Level.Entities.Exists(x => x.x == e.x && x.y == e.y && x.kind == e.kind));
    }

    [Test]
    public void Generate_DifferentSeeds_Differ()
    {
        var r = GenRecipe.FromJson(BoardRecipe);
        var a = LevelGenerator.Generate(r, 0);
        var b = LevelGenerator.Generate(r, 7);
        var same = true;
        foreach (var layer in a.Level.Layers)
            if (b.Level.Layers.ContainsKey(layer.Key) && !layer.Value.SetEquals(b.Level.Layers[layer.Key])) same = false;
        Assert.IsFalse(same, "different seeds produced identical levels");
    }

    [Test]
    public void Recipe_MissingSteps_Throws()
    {
        Assert.Throws<GenRecipeException>(() => GenRecipe.FromJson(@"{""name"":""x""}"));
    }

    [Test]
    public void Require_Impossible_FailsAfterRetries()
    {
        var r = GenRecipe.FromJson(@"{
            ""width"": 4, ""height"": 4, ""maxAttempts"": 3,
            ""steps"": [
                { ""type"": ""require"", ""check"": ""maskCount"", ""layer"": ""never"", ""min"": 5 }
            ]}");
        var res = LevelGenerator.Generate(r, 0);
        Assert.IsFalse(res.Ok);
        Assert.AreEqual(3, res.Attempts);
        Assert.AreEqual(3, res.Issues.Count);
    }

    [Test]
    public void Path_ReachesTarget()
    {
        var r = GenRecipe.FromJson(@"{
            ""width"": 10, ""height"": 10, ""seed"": 5,
            ""steps"": [
                { ""type"": ""path"", ""layer"": ""road"", ""from"": [0,0], ""to"": [9,9] },
                { ""type"": ""require"", ""check"": ""cellIn"", ""cell"": [9,9], ""layer"": ""road"" }
            ]}");
        var res = LevelGenerator.Generate(r, 0);
        Assert.IsTrue(res.Ok, string.Join(";", res.Issues));
        Assert.IsTrue(res.Level.Has("road", new GenCell(0, 0)));
        Assert.IsTrue(res.Level.Has("road", new GenCell(9, 9)));
    }

    [Test]
    public void Segments_TileTheAxis()
    {
        var r = GenRecipe.FromJson(@"{
            ""width"": 6, ""height"": 24, ""seed"": 1,
            ""steps"": [
                { ""type"": ""segments"", ""track"": ""track"",
                  ""segments"": [
                    { ""name"": ""flat"", ""len"": 4, ""weight"": 2 },
                    { ""name"": ""hazards"", ""len"": 4, ""weight"": 1,
                      ""scatter"": { ""layer"": ""obstacles"", ""density"": 0.4 } }
                  ] },
                { ""type"": ""require"", ""check"": ""maskCount"", ""layer"": ""track"", ""min"": 144 }
            ]}");
        var res = LevelGenerator.Generate(r, 0);
        Assert.IsTrue(res.Ok, string.Join(";", res.Issues));
        Assert.AreEqual(24 * 6, res.Level.Layer("track").Count);
    }

    [Test]
    public void Maze_LeavesWalkableAndWalls()
    {
        var r = GenRecipe.FromJson(@"{
            ""width"": 9, ""height"": 9, ""seed"": 3,
            ""steps"": [ { ""type"": ""maze"", ""layer"": ""walls"" } ]}");
        var res = LevelGenerator.Generate(r, 0);
        var walls = res.Level.Layer("walls").Count;
        Assert.Greater(walls, 0);
        Assert.Less(walls, 81);
    }

    [Test]
    public void Json_RoundTrip_PreservesLayersAndEntities()
    {
        var r = GenRecipe.FromJson(BoardRecipe);
        var level = LevelGenerator.Generate(r, 0).Level;
        var clone = GenLevelJson.Parse(GenLevelJson.Write(level));
        Assert.AreEqual(level.width, clone.width);
        Assert.AreEqual(level.Layers["trees"].Count, clone.Layers["trees"].Count);
        Assert.AreEqual(level.Entities.Count, clone.Entities.Count);
    }

    [Test]
    public void CustomStep_CanBeRegistered()
    {
        LevelGenerator.Register("mark_center", () => new MarkCenter());
        var r = GenRecipe.FromJson(@"{
            ""width"": 5, ""height"": 5,
            ""steps"": [ { ""type"": ""mark_center"" } ]}");
        var res = LevelGenerator.Generate(r, 0);
        Assert.IsTrue(res.Level.Has("center", new GenCell(2, 2)));
    }

    sealed class MarkCenter : IGenStep
    {
        public void Apply(GenContext ctx, Newtonsoft.Json.Linq.JObject p)
            => ctx.Level.Layer("center").Add(new GenCell(ctx.Level.width / 2, ctx.Level.height / 2));
    }
}
