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
                if (Numbers.Unit(id, run.Seed, 6) < .30f) continue;
                float v = i * interval + (Numbers.Unit(id, run.Seed) - .5f) * interval * 1.4f;
                var p = frame.Point(frame.River(v) + side * (frame.WidthAt(v) * .5f + .08f + Numbers.Unit(id, run.Seed, 7) * .38f), v);
                if (frame.InLanding(p) || frame.InBoard(p, frame.request.style.boardMargin)) continue;
                float anchorScale = minimumScale + (maximumScale - minimumScale) * (float)Math.Pow(Numbers.Unit(id, run.Seed, 8), 1.6f);
                float angle = Numbers.Unit(id, run.Seed, 9) * 6.2831853f;
                var axis = new V3((float)Math.Cos(angle), 0, (float)Math.Sin(angle));
                var result = RockGenerator.Generate(new RockSpec { id = id, seed = run.Seed, rocks = shapes, alignToSurface = false, maximumSlope = 90,
                    groups = new[] { new RockGroup { id = "bank", center = p, axis = axis, count = 1 + (int)(Numbers.Unit(id, run.Seed, 10) * 3), radius = .75f,
                        minimumScale = minimumScale * .55f, maximumScale = anchorScale, buriedFraction = .12f + Numbers.Unit(id, run.Seed, 11) * .18f } } }, run.context, cancellation);
                // Bank accents are optional; rejected stones cannot invalidate a valid crossing.
                foreach (var issue in result.diagnostics) issue.error = false;
                instances.AddRange(result.instances); diagnostics.AddRange(result.diagnostics);
            }
            run.Add(Id, instances, diagnostics);
        }
    }
}
