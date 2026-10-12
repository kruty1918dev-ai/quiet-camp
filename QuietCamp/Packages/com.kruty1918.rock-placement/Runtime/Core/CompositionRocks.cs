using System;
using System.Collections.Generic;
using System.Threading;
using Kruty1918.ScenePlacement;
using Kruty1918.ScenePlacement.Composition;

namespace Kruty1918.RockPlacement
{
    public sealed class ShorelineRockStage : ICompositionStage
    {
        public string Id => "shoreline-rocks";
        readonly AssetShape[] shapes; readonly float interval, minimumScale, maximumScale;
        public ShorelineRockStage(AssetShape[] shapes, float interval = 2.5f, float minimumScale = 1, float maximumScale = 2)
        {
            if (shapes == null || shapes.Length == 0 || !Numbers.Finite(interval) || interval < .5f || !Numbers.Finite(minimumScale)
                || !Numbers.Finite(maximumScale) || minimumScale <= 0 || maximumScale < minimumScale) throw new ArgumentException("Invalid shoreline rocks.");
            this.shapes = shapes; this.interval = interval; this.minimumScale = minimumScale; this.maximumScale = maximumScale;
        }
        public void Generate(CompositionRun run, CancellationToken cancellation)
        {
            var frame = run.Layout; int count = (int)Math.Ceiling(frame.extent / interval);
            for (int i = -count; i <= count; i++) foreach (float side in new[] { -1f, 1f })
            {
                cancellation.ThrowIfCancellationRequested(); string id = Id + "/" + i + "/" + side;
                float v = i * interval + (Numbers.Unit(id, run.Seed) - .5f) * interval * .6f;
                var p = frame.Point(frame.River(v) + side * (frame.riverWidth * .5f + .06f), v);
                if (frame.InLanding(p) || frame.InBoard(p, frame.request.style.boardMargin)) continue;
                var result = RockGenerator.Generate(new RockSpec { id = id, seed = run.Seed, rocks = shapes, alignToSurface = false, maximumSlope = 90,
                    groups = new[] { new RockGroup { id = "bank", center = p, axis = frame.far, count = 1, radius = 0,
                        minimumScale = minimumScale, maximumScale = maximumScale, buriedFraction = .15f } } }, run.context, cancellation);
                // Bank accents are optional; rejected stones cannot invalidate a valid crossing.
                foreach (var issue in result.diagnostics) issue.error = false;
                run.plan.instances.AddRange(result.instances); run.plan.diagnostics.AddRange(result.diagnostics);
            }
            run.plan.accepted[Id] = run.plan.instances.FindAll(p => p.id.StartsWith(Id + "/", StringComparison.Ordinal)).Count;
        }
    }
}
