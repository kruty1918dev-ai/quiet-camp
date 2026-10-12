using System;
using System.Linq;
using Kruty1918.ScenePlacement;
using Kruty1918.ScenePlacement.Composition;
using Kruty1918.RockPlacement;
static class CompositionContracts
{
    public static void Run()
    {
        int checks = 0;
        var shape = new AssetShape { id = "stone", volumes = new[] { new Box(new V3(0, .15f, 0), new V3(.3f, .15f, .3f)) }, contacts = new[] { V3.Zero } };
        foreach (var size in new[] { (6f, 8f), (4f, 12f), (12f, 4f) }) for (int seed = 0; seed < 8; seed++)
        {
            var request = new CompositionRequest { id = "host", seed = seed, width = size.Item1, depth = size.Item2, style = new CompositionStyle() };
            var stages = new ICompositionStage[] { new ShorelineRockStage(new[] { shape }) };
            var a = CompositionGenerator.Generate(request, stages); var b = CompositionGenerator.Generate(request, stages);
            if (!a.Ok || a.instances.Count < 2) throw new Exception("FAIL shoreline empty"); checks++;
            if (!a.instances.Select(p => p.id + p.pose.position).SequenceEqual(b.instances.Select(p => p.id + p.pose.position))) throw new Exception("FAIL deterministic banks"); checks++;
            if (a.instances.Any(p => a.layout.InLanding(p.pose.position) || a.layout.InBoard(p.pose.position, .2f))) throw new Exception("FAIL bank exclusions"); checks++;
            if (a.instances.Select(p => Math.Round(p.pose.scale.x, 3)).Distinct().Count() < 3) throw new Exception("FAIL repeated anchor sizes"); checks++;
            if (!a.instances.Any(p => p.id.EndsWith("/1", StringComparison.Ordinal))) throw new Exception("FAIL no cluster satellites"); checks++;
            if (a.instances.Any(p => p.pose.up.y < .999f)) throw new Exception("FAIL host planar adapter contract"); checks++;
        }
        Console.WriteLine("composition rocks: " + checks + " assertions passed.");
    }
}
