using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Kruty1918.ScenePlacement;
using Kruty1918.ScenePlacement.Composition;

static class ReviewContracts
{
    sealed class EmptyStage : ICompositionStage
    {
        public string Id => "test-role";
        public void Generate(CompositionRun run, CancellationToken cancellation) => run.Add(Id, Array.Empty<PlacedInstance>(), Array.Empty<Diagnostic>());
    }
    sealed class ScoreCritic : ICompositionCritic
    {
        public bool reject;
        public CompositionReview Evaluate(CompositionPlan plan) => new CompositionReview { passed = !reject,
            score = new[] { .3f, .9f, .8f }[plan.attempt] };
    }
    public static void Run()
    {
        int checks = 0;
        void Check(bool value, string name) { if (!value) throw new Exception("FAIL review: " + name); checks++; }
        var request = new CompositionRequest { id = "host", seed = 71, width = 6, depth = 8, style = new CompositionStyle { attempts = 3 } };
        var stages = new ICompositionStage[] { new EmptyStage() };
        var plan = CompositionGenerator.Generate(request, stages, critic: new ScoreCritic());
        Check(plan.Ok && plan.attempt == 1, "later best candidate replaces first valid candidate");
        Check(plan.attempts.Length == 3 && plan.review.score == .9f, "all bounded attempts are reviewed");
        Check(plan.effectiveSeed == 71 + 104729, "selected effective seed is replayable");
        var rejected = CompositionGenerator.Generate(request, stages, critic: new ScoreCritic { reject = true });
        Check(!rejected.Ok && rejected.diagnostics.Any(d => d.code == "quality-rejected"), "no passing candidate produces explicit failure");
        Check(rejected.attempts.All(a => !a.qualityPassed && a.geometryPassed), "geometric validity never disguises quality rejection");
        Check(CompositionGenerator.Generate(request, stages).attempts.Length == 1, "legacy generation remains first-valid without critic");
        var roles = new[] { new RoleQualityRule { stage = "frame", minimumVisible = 1, targetVisible = 2, maximumVisible = 4, minimumQuadrants = 1 } };
        var profile = new CompositionReviewProfile { roles = roles, minimumScore = .4f };
        var critic = new CompositionCritic(profile);
        profile.minimumScore = 1; roles[0].minimumVisible = 99;
        var frame = new CompositionLayout(request);
        var shape = new Box(new V3(0, 1, 0), new V3(.3f, 1, .3f));
        PlacedInstance At(string id, float u) => new PlacedInstance { id = id, asset = "host-asset", pose = Pose.At(frame.Point(u, -1)),
            volumes = new[] { new OrientedBox(shape, Pose.At(frame.Point(u, -1))) } };
        var reviewPlan = new CompositionPlan { layout = frame };
        var run = new CompositionRun(reviewPlan, 71);
        run.Add("frame", new[] { At("visible", -7) }, Array.Empty<Diagnostic>());
        run.Add("frame", new[] { At("outside-camera", -100) }, Array.Empty<Diagnostic>());
        var review = critic.Evaluate(reviewPlan);
        Check(review.passed && review.metrics[0].visible == 1, "only intersecting visible silhouettes count and profile is frozen");
        Check(reviewPlan.accepted["frame"] == 2 && reviewPlan.byStage["frame"].Count == 2, "multiple stage batches accumulate ownership");
        Check(!new CompositionCritic(new CompositionReviewProfile { roles = new[] { new RoleQualityRule { stage = "missing" } } }).Evaluate(reviewPlan).passed,
            "missing role fails minimum visibility");
        var balancedOnly = new CompositionCritic(new CompositionReviewProfile { roles = new[] { new RoleQualityRule { stage = "frame", minimumSideFraction = .2f } } });
        Check(!balancedOnly.Evaluate(reviewPlan).passed, "one-sided frame fails an explicit balance rule");
        try { new CompositionCritic(new CompositionReviewProfile { minimumScore = float.NaN, roles = new[] { new RoleQualityRule { stage = "frame" } } }); throw new Exception("FAIL invalid review"); }
        catch (ArgumentException) { checks++; }
        var source = new AssetShape { id = "shape", contacts = new[] { V3.Zero }, volumes = new[] { shape } };
        var snapshot = source.Snapshot(); source.contacts[0] = new V3(99, 0, 0); source.volumes[0] = new Box(V3.Zero, V3.Zero);
        Check(snapshot.contacts[0].x == 0 && snapshot.volumes[0].half.y == 1, "shape arrays are copied");
        var protectedStructure = At("crossing", -7);
        var structuralRule = new ReadabilityRule(frame); structuralRule.Protect(protectedStructure.id, protectedStructure.volumes);
        var overhead = At("overhead-foliage", -7);
        var issue = structuralRule.Check(new PlacementRequest { id = overhead.id, allowedContacts = Array.Empty<string>() }, overhead, new PlacementContext(frame));
        Check(issue != null && issue.code == "structural-occlusion", "protected crossing cannot be concealed by foliage");
        Console.WriteLine("composition review: " + checks + " assertions passed.");
    }
}
