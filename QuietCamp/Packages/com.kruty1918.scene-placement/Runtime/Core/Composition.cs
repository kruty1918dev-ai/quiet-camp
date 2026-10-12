using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Kruty1918.ScenePlacement.Composition
{
    // Engine-independent inputs. A host owns gameplay; this package owns presentation placement.
    [Serializable] public sealed class CompositionStyle
    {
        public string id = "riparian-clearing";
        public float pitch = 56, yaw = 219, aspect = 1.5f, framing = 1.15f, targetRight = .3f, targetFar = -.8f;
        public float minimumAspect = .5625f, maximumAspect = 2;
        public float boardMargin = .28f, peakHeight = 1.6f, bankGap = .65f, riverWidthRatio = .32f;
        public float riverSlope = -.19f, riverBend = .32f, bridgeFarRatio = -.14f, landingRadius = .8f;
        public float meadowRelief = .12f, waterDepth = .64f;
        public float riverWidthVariation, riverFramingMarginRatio;
        public int attempts = 6;
        public CompositionStyle Snapshot() => (CompositionStyle)MemberwiseClone();
        public void Validate()
        {
            var values = new[] { pitch, yaw, aspect, minimumAspect, maximumAspect, framing, targetRight, targetFar, boardMargin, peakHeight,
                bankGap, riverWidthRatio, riverSlope, riverBend, bridgeFarRatio, landingRadius, meadowRelief, waterDepth, riverWidthVariation, riverFramingMarginRatio };
            if (string.IsNullOrWhiteSpace(id) || values.Any(v => !Numbers.Finite(v)) || pitch < 30 || pitch > 80
                || aspect < .25f || aspect > 4 || framing < 1 || framing > 2 || boardMargin < 0 || peakHeight <= 0
                || minimumAspect < .25f || minimumAspect > aspect || maximumAspect < aspect || maximumAspect > 4
                || bankGap < .2f || riverWidthRatio < .1f || riverWidthRatio > .6f || Math.Abs(riverSlope) > .35f
                || riverBend < 0 || riverBend > 1 || Math.Abs(bridgeFarRatio) > .75f || landingRadius < .3f
                || riverFramingMarginRatio < 0 || riverFramingMarginRatio > .4f
                || riverWidthVariation < 0 || riverWidthVariation > .3f
                || meadowRelief < 0 || meadowRelief > .4f || waterDepth < .1f || waterDepth > 2
                || attempts < 1 || attempts > 16)
                throw new ArgumentException("Composition style requires finite bounded geometry, framing and search budgets.");
        }
    }
    // The host mesh and placement surface consume the same cross-section and curves.
    public static class RiverGeometry
    {
        public static float Centre(float v, float slope, float bend) => slope * v
            - (float)Math.Sin(v * .38f) * bend - (float)Math.Sin(v * .83f) * bend * .34f;
        public static float Tangent(float v, float slope, float bend) => slope
            - bend * (.38f * (float)Math.Cos(v * .38f) + .2822f * (float)Math.Cos(v * .83f));
        public static float Width(float v, float width, float variation) => width
            * (1 + variation * (.65f * (float)Math.Sin(v * .48f) + .35f * (float)Math.Sin(v * 1.06f + 1.2f)));
        public static float Bed(float distance, float width, float v, float side)
        {
            float radius = distance / width;
            float shelf = .43f + .02f * (float)Math.Sin(v * .75f + Math.Sign(side) * 2.1f);
            float edge = .60f + .018f * (float)Math.Sin(v * 1.1f + Math.Sign(side));
            return .18f * (1 - Numbers.Smooth((radius - shelf) / (edge - shelf)))
                + .82f * (1 - Numbers.Smooth((radius - .24f) / (shelf - .24f)));
        }
    }
    public sealed class CompositionRequest
    {
        public string id; public int seed; public float width, depth;
        public CompositionStyle style;
        public CompositionRequest Snapshot()
        {
            var copy = (CompositionRequest)MemberwiseClone(); copy.style = style?.Snapshot(); return copy;
        }
        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(id) || !Numbers.Finite(width) || !Numbers.Finite(depth) || width < 2 || depth < 2 || width > 64 || depth > 64 || style == null)
                throw new ArgumentException("Composition request requires an ID, supported play area and style.");
            style.Validate();
        }
    }
    public sealed class CompositionLayout : ISurfaceQuery
    {
        public readonly CompositionRequest request;
        public readonly V3 right, far, target;
        public readonly float span, riverWidth, riverCentre, bridgeFar, orthoSize, extent;
        public CompositionLayout(CompositionRequest input)
        {
            input.Validate(); input = input.Snapshot(); request = input;
            float yaw = input.style.yaw * Numbers.Radians;
            right = new V3((float)Math.Cos(yaw), 0, -(float)Math.Sin(yaw));
            far = new V3((float)Math.Sin(yaw), 0, (float)Math.Cos(yaw));
            span = (float)Math.Sqrt(input.width * input.width + input.depth * input.depth);
            riverWidth = span * input.style.riverWidthRatio;
            float support = Math.Abs(right.x) * input.width * .5f + Math.Abs(right.z) * input.depth * .5f;
            float shoreSupport = float.PositiveInfinity;
            foreach (float x in new[] { -input.width * .5f, input.width * .5f })
            foreach (float z in new[] { -input.depth * .5f, input.depth * .5f })
            {
                var corner = new V3(x, 0, z);
                shoreSupport = Math.Min(shoreSupport, V3.Dot(corner, right) - input.style.riverSlope * V3.Dot(corner, far));
            }
            riverCentre = shoreSupport - riverWidth * .5f * (1 + input.style.riverWidthVariation) - input.style.bankGap - input.style.riverBend * 1.34f;
            bridgeFar = span * input.style.bridgeFarRatio;
            target = Point(input.style.targetRight, input.style.targetFar);
            // Project the gameplay bounds and retain room for its surroundings.
            float vertical = (Math.Abs(far.x) * input.width + Math.Abs(far.z) * input.depth) * (float)Math.Sin(input.style.pitch * Numbers.Radians);
            orthoSize = Math.Max(vertical * .5f + input.style.peakHeight, support / input.style.aspect) * input.style.framing;
            if (input.style.riverFramingMarginRatio > 0)
            {
                float accentFar = bridgeFar + input.style.landingRadius + span * .15f + .2f;
                float left = River(accentFar) - WidthAt(accentFar) * .5f - span * input.style.riverFramingMarginRatio;
                orthoSize = Math.Max(orthoSize, (input.style.targetRight - left) / input.style.aspect);
            }
            float horizontal = orthoSize * input.style.maximumAspect;
            float verticalGround = orthoSize * input.style.aspect / input.style.minimumAspect / (float)Math.Sin(input.style.pitch * Numbers.Radians);
            extent = Math.Max(Math.Abs(right.x) * horizontal + Math.Abs(far.x) * verticalGround,
                Math.Abs(right.z) * horizontal + Math.Abs(far.z) * verticalGround) + 3;
        }
        public V3 Point(float u, float v, float y = 0) => right * u + far * v + V3.Up * y;
        public V2 Ground(V3 p) => new V2(V3.Dot(p, right), V3.Dot(p, far));
        public float River(float v) => riverCentre + RiverGeometry.Centre(v, request.style.riverSlope, request.style.riverBend);
        public float WidthAt(float v) => RiverGeometry.Width(v, riverWidth, request.style.riverWidthVariation);
        public float RiverTangent(float v) => RiverGeometry.Tangent(v, request.style.riverSlope, request.style.riverBend);
        public float BankDistance(V3 p) { var uv = Ground(p); return Math.Abs(uv.x - River(uv.y)) - WidthAt(uv.y) * .5f; }
        public bool InLanding(V3 p)
        {
            var uv = Ground(p); return Math.Abs(uv.y - bridgeFar) < request.style.landingRadius
                && Math.Abs(uv.x - River(bridgeFar)) < riverWidth * .5f + request.style.landingRadius;
        }
        public bool InBoard(V3 p, float margin = 0) => Math.Abs(p.x) < request.width * .5f + margin && Math.Abs(p.z) < request.depth * .5f + margin;
        public float Height(float x, float z)
        {
            var p = new V3(x, 0, z); var uv = Ground(p); float distance = Math.Abs(uv.x - River(uv.y));
            float bed = RiverGeometry.Bed(distance, WidthAt(uv.y), uv.y, uv.x - River(uv.y));
            float margin = Math.Max(Math.Abs(x) - request.width * .5f, Math.Abs(z) - request.depth * .5f);
            float relief = Numbers.Smooth(margin * .5f) * request.style.meadowRelief * (.5f + .5f * (float)Math.Sin(x * .55f) * (float)Math.Cos(z * .68f));
            return -.045f + relief - bed * request.style.waterDepth;
        }
        public bool TrySample(V3 p, V3 direction, out SurfaceSample sample)
        {
            float h = Height(p.x, p.z), epsilon = .025f;
            var normal = new V3(-(Height(p.x + epsilon, p.z) - Height(p.x - epsilon, p.z)) / (2 * epsilon), 1,
                -(Height(p.x, p.z + epsilon) - Height(p.x, p.z - epsilon)) / (2 * epsilon)).Normalized;
            sample = new SurfaceSample { point = new V3(p.x, h, p.z), normal = normal, tag = BankDistance(p) < -.08f ? "water" : "ground",
                moisture = Numbers.Clamp(1 - BankDistance(p) * .35f, .2f, 1), light = .8f };
            return Math.Abs(direction.y) > .999f;
        }
        public V2 Project(V3 p)
        {
            var relative = p - target; float pitch = request.style.pitch * Numbers.Radians;
            return new V2(.5f + V3.Dot(relative, right) / (2 * orthoSize * request.style.aspect),
                .5f + (V3.Dot(relative, far) * (float)Math.Sin(pitch) + relative.y * (float)Math.Cos(pitch)) / (2 * orthoSize));
        }
        public bool InSupportedFrames(V3 p)
        {
            var screen = Project(p); var style = request.style;
            float horizontal = style.maximumAspect / style.aspect;
            float vertical = style.aspect / style.minimumAspect;
            return Math.Abs(screen.x - .5f) < horizontal * .5f + .15f && Math.Abs(screen.y - .5f) < vertical * .5f + .2f;
        }
    }
    public sealed class CompositionPlan
    {
        public CompositionLayout layout; public int attempt, effectiveSeed;
        public CompositionReview review;
        public CompositionAttempt[] attempts = Array.Empty<CompositionAttempt>();
        public readonly List<PlacedInstance> instances = new List<PlacedInstance>();
        public readonly List<Diagnostic> diagnostics = new List<Diagnostic>();
        public readonly Dictionary<string, int> accepted = new Dictionary<string, int>(StringComparer.Ordinal);
        public readonly Dictionary<string, List<PlacedInstance>> byStage = new Dictionary<string, List<PlacedInstance>>(StringComparer.Ordinal);
        public bool Ok => !diagnostics.Any(d => d.error);
    }
    public interface ICompositionStage
    {
        string Id { get; }
        void Generate(CompositionRun run, CancellationToken cancellation);
    }
    public sealed class CompositionRun
    {
        public readonly CompositionPlan plan; public readonly PlacementContext context;
        readonly ReadabilityRule readability;
        public CompositionLayout Layout => plan.layout;
        public int Seed { get; }
        public CompositionRun(CompositionPlan plan, int seed)
        {
            this.plan = plan; Seed = seed; context = new PlacementContext(plan.layout);
            context.Reserve("gameplay", new OrientedBox(new Box(new V3(0, 3, 0),
                new V3(Layout.request.width * .5f + Layout.request.style.boardMargin, 4, Layout.request.depth * .5f + Layout.request.style.boardMargin)), Pose.At(V3.Zero)));
            readability = new ReadabilityRule(Layout); context.rules.Add(readability);
        }
        public void ProtectVisibility(IEnumerable<PlacedInstance> instances)
        {
            foreach (var instance in instances) readability.Protect(instance.id, instance.volumes);
        }
        public void Add(string stage, IEnumerable<PlacedInstance> instances, IEnumerable<Diagnostic> diagnostics)
        {
            var items = instances.ToArray(); plan.instances.AddRange(items);
            if (!plan.byStage.TryGetValue(stage, out var owned)) plan.byStage.Add(stage, owned = new List<PlacedInstance>());
            owned.AddRange(items); plan.accepted[stage] = owned.Count;
            plan.diagnostics.AddRange(diagnostics);
        }
    }
    public static class CompositionGenerator
    {
        public static CompositionPlan Generate(CompositionRequest request, IReadOnlyList<ICompositionStage> stages, CancellationToken cancellation = default, ICompositionCritic critic = null)
        {
            request.Validate(); request = request.Snapshot();
            if (stages == null || stages.Count == 0 || stages.Any(s => s == null || string.IsNullOrWhiteSpace(s.Id)) || stages.Select(s => s.Id).Distinct().Count() != stages.Count)
                throw new ArgumentException("An explicit ordered pipeline of unique stage IDs is required.");
            CompositionPlan last = null, best = null;
            var attempts = new List<CompositionAttempt>();
            for (int attempt = 0; attempt < request.style.attempts; attempt++)
            {
                cancellation.ThrowIfCancellationRequested();
                last = new CompositionPlan { layout = new CompositionLayout(request), attempt = attempt, effectiveSeed = unchecked(request.seed + attempt * 104729) };
                var run = new CompositionRun(last, last.effectiveSeed);
                foreach (var stage in stages)
                {
                    stage.Generate(run, cancellation);
                    if (!last.Ok) break;
                }
                bool geometryPassed = last.Ok;
                if (geometryPassed && critic != null)
                {
                    last.review = critic.Evaluate(last);
                    if (last.review == null || !Numbers.Finite(last.review.score) || last.review.score < 0 || last.review.score > 1)
                        throw new InvalidOperationException("Critic must return a finite normalized review.");
                    if (!last.review.passed) last.diagnostics.Add(new Diagnostic("quality-rejected", "composition", "Composition fails configured role or score thresholds."));
                }
                attempts.Add(new CompositionAttempt { attempt = attempt, seed = last.effectiveSeed, geometryPassed = geometryPassed,
                    qualityPassed = last.Ok, score = last.review?.score ?? 0,
                    metrics = last.review?.metrics ?? Array.Empty<RoleMetric>(),
                    failures = last.diagnostics.Where(d => d.error).Select(d => d.code)
                        .Concat(last.review?.metrics.SelectMany(m => m.failures.Select(f => m.stage + ":" + f)) ?? Array.Empty<string>()).ToArray() });
                if (last.Ok && critic == null) { last.attempts = attempts.ToArray(); return last; }
                // Stable tie-break: the first equally good attempt wins.
                if (last.Ok && (best == null || last.review.score > best.review.score)) best = last;
            }
            var result = best ?? last; result.attempts = attempts.ToArray();
            return result; // Failure is evidence, never an automatic visual acceptance.
        }
    }
    public sealed class ReadabilityRule : IPlacementRule
    {
        readonly CompositionLayout layout;
        readonly List<(string id, V2[] points)> protectedSilhouettes = new List<(string, V2[])>();
        public ReadabilityRule(CompositionLayout layout) { this.layout = layout; }
        public void Protect(string id, IEnumerable<OrientedBox> volumes)
        {
            foreach (var volume in volumes) protectedSilhouettes.Add((id, ProjectedBox(volume, layout)));
        }
        public Diagnostic Check(PlacementRequest request, PlacedInstance candidate, PlacementContext context)
        {
            foreach (var box in candidate.volumes)
            {
                var projection = ProjectedBox(box, layout);
                // Test projected convex gameplay polygon, not only world-space anchors.
                if (OverlapGameplay(projection, layout)) return new Diagnostic("gameplay-occlusion", request.id, "Silhouette intrudes into the protected play area.");
                foreach (var reservation in protectedSilhouettes)
                    if (!(request.allowedContacts ?? Array.Empty<string>()).Contains(reservation.id)
                        && Overlap(projection, reservation.points)) return new Diagnostic("structural-occlusion", request.id, "Silhouette conceals protected structure " + reservation.id);
            }
            return null;
        }
        static V2[] ProjectedBox(OrientedBox b, CompositionLayout frame)
        {
            var points = new List<V2>();
            foreach (float x in new[] { -1f, 1f }) foreach (float y in new[] { -1f, 1f }) foreach (float z in new[] { -1f, 1f })
                points.Add(frame.Project(b.center + b.x * (b.half.x * x) + b.y * (b.half.y * y) + b.z * (b.half.z * z)));
            return points.ToArray();
        }
        static bool OverlapGameplay(V2[] points, CompositionLayout frame)
        {
            var request = frame.request; float margin = request.style.boardMargin;
            var board = new List<V2>();
            foreach (float x in new[] { -request.width * .5f - margin, request.width * .5f + margin })
            foreach (float z in new[] { -request.depth * .5f - margin, request.depth * .5f + margin })
            { board.Add(frame.Project(new V3(x, 0, z))); board.Add(frame.Project(new V3(x, request.style.peakHeight, z))); }
            return Overlap(points, board.ToArray());
        }
        static bool Overlap(V2[] points, V2[] board)
        {
            // SAT against projected silhouettes. Chord axes include all convex hull edges.
            var axes = new List<V2> { new V2(1, 0), new V2(0, 1) };
            for (int i = 0; i < board.Length; i++) for (int j = i + 1; j < board.Length; j++)
                axes.Add(new V2(board[j].y - board[i].y, board[i].x - board[j].x));
            foreach (var axis in axes)
            {
                if (axis.x * axis.x + axis.y * axis.y < 1e-10f) continue;
                float A(V2 p) => p.x * axis.x + p.y * axis.y;
                if (points.Max(A) <= board.Min(A) || board.Max(A) <= points.Min(A)) return false;
            }
            return true;
        }
    }
    // Structural anchors are relative to the play area and river, never a level ID or coordinate list.
    public sealed class CrossingStage : ICompositionStage
    {
        public string Id => "crossing";
        readonly AssetShape bridge; readonly float longAxisYaw;
        public CrossingStage(AssetShape bridge, float longAxisYaw = 90) { this.bridge = bridge; this.longAxisYaw = longAxisYaw; }
        public void Generate(CompositionRun run, CancellationToken cancellation)
        {
            var layout = run.Layout; float v = layout.bridgeFar;
            float length = bridge.volumes.Max(b => Math.Max(b.half.x, b.half.z) * 2);
            float scale = (layout.WidthAt(v) + layout.request.style.landingRadius) / (float)Math.Sqrt(1 + layout.RiverTangent(v) * layout.RiverTangent(v)) / length;
            var result = PlacementSolver.Place(new PlacementRequest { id = Id + "/bridge", owner = Id, shape = bridge,
                target = layout.Point(layout.River(v), v), yaw = layout.request.style.yaw + longAxisYaw + (float)Math.Atan(layout.RiverTangent(v)) / Numbers.Radians,
                scale = scale, maxSupportGap = .18f, maxSlope = 90, seed = run.Seed }, run.context, cancellation);
            run.Add(Id, result.instances, result.diagnostics);
            if (result.Ok)
            {
                run.ProtectVisibility(result.instances);
                // Reserve the approach in the river's own coordinate frame.
                var local = new Box(new V3(0, .7f, 0), new V3(layout.riverWidth * .5f + layout.request.style.landingRadius, 1, layout.request.style.landingRadius));
                run.context.Reserve("crossing-approach", new OrientedBox(local, Pose.At(layout.Point(layout.River(v), v), layout.request.style.yaw)));
            }
        }
    }
    public sealed class BankBoundaryStage : ICompositionStage
    {
        public string Id => "bank-boundary";
        readonly AssetShape shape; readonly float lengthRatio;
        public BankBoundaryStage(AssetShape shape, float lengthRatio = .3f)
        {
            if (shape == null || !Numbers.Finite(lengthRatio) || lengthRatio <= 0 || lengthRatio > 1) throw new ArgumentException("Invalid bank boundary.");
            this.shape = shape.Snapshot(); this.lengthRatio = lengthRatio;
        }
        public void Generate(CompositionRun run, CancellationToken cancellation)
        {
            var frame = run.Layout; var bounds = shape.volumes[0]; bool longX = bounds.half.x > bounds.half.z;
            float scale = frame.span * lengthRatio / (2 * Math.Max(bounds.half.x, bounds.half.z));
            float v = frame.bridgeFar + frame.request.style.landingRadius + frame.span * lengthRatio * .5f + .2f;
            // Open the long face towards the view, while keeping both ends on dry bank.
            float opening = 30 * Numbers.Radians;
            float halfLength = frame.span * lengthRatio * .5f;
            float clearance = halfLength * ((float)Math.Sin(opening) + Math.Abs(frame.request.style.riverSlope) * (float)Math.Cos(opening)) + .3f;
            var p = frame.Point(frame.River(v) - frame.WidthAt(v) * .5f - clearance, v);
            var result = PlacementSolver.Place(new PlacementRequest { id = Id + "/section", owner = Id, shape = shape, target = p,
                scale = scale, yaw = frame.request.style.yaw + (longX ? 60 : -30), maxSlope = 65, maxSupportGap = .20f,
                searchRadius = .5f, candidates = 32, seed = run.Seed, required = false }, run.context, cancellation);
            foreach (var diagnostic in result.diagnostics) diagnostic.error = false;
            run.Add(Id, result.instances, result.diagnostics);
            if (result.Ok) run.ProtectVisibility(result.instances);
        }
    }
}
