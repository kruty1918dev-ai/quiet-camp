using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using Kruty1918.ScenePlacement;
using Kruty1918.ScenePlacement.Composition;

static class CompositionContracts
{
    public static int Run()
    {
        int checks = 0;
        void Check(bool value, string id) { if (!value) throw new Exception("FAIL composition: " + id); checks++; }
        var bridge = new AssetShape { id = "bridge", volumes = new[] { new Box(new V3(0, .15f, 0), new V3(1.7f, .15f, .35f)) },
            contacts = new[] { new V3(-1.55f, 0, 0), new V3(1.55f, 0, 0) } };
        foreach (var size in new[] { (6f, 8f), (4f, 12f), (12f, 4f), (10f, 10f) })
        for (int seed = 1; seed <= 12; seed++)
        {
            var request = new CompositionRequest { id = "game-owned", width = size.Item1, depth = size.Item2, seed = seed,
                style = new CompositionStyle { riverWidthRatio = .225f } };
            var stages = new ICompositionStage[] { new CrossingStage(bridge, 0) };
            var a = CompositionGenerator.Generate(request, stages); var b = CompositionGenerator.Generate(request, stages);
            Check(a.Ok && a.instances.Count == 1, "required crossing supported: " + size + " | " + string.Join(";", a.diagnostics));
            var options = new JsonSerializerOptions { IncludeFields = true, IgnoreReadOnlyProperties = true };
            Check(JsonSerializer.Serialize(a.instances, options) == JsonSerializer.Serialize(b.instances, options), "deterministic replay");
            Check(!a.layout.InBoard(a.instances[0].pose.position), "bridge outside gameplay");
            Check(a.instances[0].volumes.All(v => v.Bounds.Valid), "finite geometry");
            foreach (float aspect in new[] { request.style.minimumAspect, request.style.aspect, request.style.maximumAspect })
            foreach (float x in new[] { -1f, 1f }) foreach (float y in new[] { -1f, 1f })
            {
                var candidateFrame = a.layout;
                float sizeForAspect = candidateFrame.orthoSize * Math.Max(1, request.style.aspect / aspect);
                var corner = candidateFrame.target + candidateFrame.right * (x * sizeForAspect * aspect)
                    + candidateFrame.far * (y * sizeForAspect / (float)Math.Sin(request.style.pitch * Numbers.Radians));
                Check(Math.Abs(corner.x) < candidateFrame.extent && Math.Abs(corner.z) < candidateFrame.extent, "supported frame never exposes terrain edge");
            }
            foreach (var contact in bridge.contacts)
            {
                var point = a.instances[0].pose.Point(contact);
                Check(a.layout.BankDistance(point) > -.15f, "each bridge end lands on a bank");
            }
        }
        var river = new CompositionLayout(new CompositionRequest { id = "varying-river", width = 6, depth = 8,
            style = new CompositionStyle { riverWidthVariation = .22f, riverBend = .48f, riverSlope = -.3f } });
        for (int i = -80; i <= 80; i++)
        {
            float v = i * .125f, width = river.WidthAt(v);
            Check(width >= river.riverWidth * .78f && width <= river.riverWidth * 1.22f, "bounded varying width");
            float tangent = (river.River(v + .01f) - river.River(v - .01f)) / .02f;
            Check(Math.Abs(tangent - river.RiverTangent(v)) < .002f, "crossing tangent matches river curve");
            foreach (float side in new[] { -1f, 1f })
            {
                float previous = 1;
                for (int radius = 0; radius <= 20; radius++)
                {
                    float bed = RiverGeometry.Bed(width * radius * .04f, width, v, side);
                    Check(bed >= 0 && bed <= previous + .00001f, "bank section monotonically reaches dry land"); previous = bed;
                }
                var shore = river.Point(river.River(v) + side * width * .65f, v);
                Check(river.Height(shore.x, shore.z) >= -.046f, "shore plants have dry support");
            }
        }
        var invalid = new CompositionRequest { id = "invalid", width = 6, depth = 8, style = new CompositionStyle { riverSlope = float.NaN } };
        try { new CompositionLayout(invalid); throw new Exception("FAIL invalid style"); } catch (ArgumentException) { checks++; }
        var contextRequest = new CompositionRequest { id = "protected", width = 6, depth = 8, style = new CompositionStyle() };
        var frame = new CompositionLayout(contextRequest);
        var context = new PlacementContext(frame); context.rules.Add(new ReadabilityRule(frame));
        var tall = new AssetShape { id = "tree", volumes = new[] { new Box(new V3(0, 3, 0), new V3(1, 3, 1)) }, contacts = new[] { V3.Zero } };
        Check(!PlacementSolver.Place(new PlacementRequest { id = "occluding", shape = tall, target = new V3(0, 0, 5), maxSlope = 90 }, context).Ok,
            "projected crown rejected despite anchor outside board");
        var cancellation = new CancellationToken(true);
        try { CompositionGenerator.Generate(contextRequest, new ICompositionStage[] { new CrossingStage(bridge) }, cancellation); throw new Exception("FAIL cancel"); }
        catch (OperationCanceledException) { checks++; }
        Console.WriteLine("composition: " + checks + " assertions passed (geometry only; render acceptance separate).");
        return checks;
    }
}
