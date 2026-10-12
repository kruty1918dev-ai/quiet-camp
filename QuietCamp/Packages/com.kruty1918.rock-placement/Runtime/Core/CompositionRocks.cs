using System;
using System.Collections.Generic;
using System.Threading;
using System.Linq;
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
            if (shapes.Any(s => s == null)) throw new ArgumentException("Resolved rock shapes required.");
            this.shapes = shapes.Select(s => s.Snapshot()).ToArray(); this.interval = interval; this.minimumScale = minimumScale; this.maximumScale = maximumScale;
        }
        public void Generate(CompositionRun run, CancellationToken cancellation)
        {
            var frame = run.Layout; int count = (int)Math.Ceiling(frame.extent / interval);
            var instances = new List<PlacedInstance>(); var diagnostics = new List<Diagnostic>();
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
                instances.AddRange(result.instances); diagnostics.AddRange(result.diagnostics);
            }
            run.Add(Id, instances, diagnostics);
        }
    }
}
