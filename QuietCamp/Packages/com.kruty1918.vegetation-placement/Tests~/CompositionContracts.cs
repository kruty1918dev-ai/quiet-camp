using System;
using System.Linq;
using Kruty1918.ScenePlacement;
using Kruty1918.ScenePlacement.Composition;
using Kruty1918.VegetationPlacement;
static class CompositionContracts
{
    public static void Run()
    {
        int checks = 0;
        var shape = new AssetShape { id = "bank-shrub", volumes = new[] { new Box(new V3(0, .15f, 0), new V3(.1f, .15f, .1f)) }, contacts = new[] { V3.Zero } };
        var species = new[] { new Species { id = "bank-shrub", shape = shape, spacing = .3f, maximumSlope = 75 } };
        var stages = new ICompositionStage[] { new CompositionVegetationStage("bank", CompositionHabitat.Shoreline, species, .6f, .9f, 3) };
        foreach (var size in new[] { (6f, 8f), (4f, 12f), (12f, 4f) }) for (int seed = 0; seed < 8; seed++)
        {
            var request = new CompositionRequest { id = "host", seed = seed, width = size.Item1, depth = size.Item2, style = new CompositionStyle() };
            var a = CompositionGenerator.Generate(request, stages); var b = CompositionGenerator.Generate(request, stages);
            if (!a.Ok || a.instances.Count < 3) throw new Exception("FAIL insufficient riparian vegetation"); checks++;
            if (!a.instances.Select(p => p.id + p.pose.position).SequenceEqual(b.instances.Select(p => p.id + p.pose.position))) throw new Exception("FAIL deterministic ecology"); checks++;
            if (a.instances.Any(p => a.layout.InLanding(p.pose.position) || a.layout.InBoard(p.pose.position, .2f) || a.layout.BankDistance(p.pose.position) < .1f)) throw new Exception("FAIL habitat exclusions"); checks++;
        }
        var layout = new CompositionLayout(new CompositionRequest { id = "detail", width = 6, depth = 8, style = new CompositionStyle() });
        var coverStyle = new GroundcoverStyle();
        var coverA = CompositionGroundcover.Generate(layout, coverStyle, 1);
        var coverB = CompositionGroundcover.Generate(layout, coverStyle, 1);
        if (!coverA.Select(p => p.id + p.point).SequenceEqual(coverB.Select(p => p.id + p.point))) throw new Exception("FAIL cover replay"); checks++;
        if (coverA.Count < 100 || coverA.Any(p => layout.InBoard(p.point, .2f) || layout.InLanding(p.point) || layout.BankDistance(p.point) < .15f)) throw new Exception("FAIL cover exclusions"); checks++;
        if (coverA.Where(p => p.kind == CoverKind.Flower).Count() > coverA.Count * .15f) throw new Exception("FAIL flower visual budget"); checks++;
        try { CompositionGroundcover.Generate(layout, new GroundcoverStyle { maximumCandidates = 1 }, 1); throw new Exception("FAIL candidate budget"); } catch (ArgumentException) { checks++; }
        var blocked = CompositionGroundcover.Generate(layout, coverStyle, 1, new[] { new PlacedInstance { id = "blocked", volumes = new[] {
            new OrientedBox(new Box(new V3(0, 0, 0), new V3(100, 100, 100)), Pose.At(V3.Zero)) } } });
        if (blocked.Count != 0) throw new Exception("FAIL cover intersects structural geometry"); checks++;
        Console.WriteLine("composition vegetation: " + checks + " assertions passed.");
    }
}
