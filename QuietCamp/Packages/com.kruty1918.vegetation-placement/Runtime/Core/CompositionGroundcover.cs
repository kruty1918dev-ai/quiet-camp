using System;
using System.Collections.Generic;
using System.Threading;
using Kruty1918.ScenePlacement;
using Kruty1918.ScenePlacement.Composition;

namespace Kruty1918.VegetationPlacement
{
    public enum CoverKind { Shrub, Leaf, Flower, Blade }
    [Serializable] public sealed class GroundcoverStyle
    {
        public float cellSize = .24f, density = .68f, shorelineDensity = .95f, bankBand = 1.2f, flowerFraction = .055f;
        public float minimumHeight = .07f, maximumHeight = .18f, maximumSpan = .22f;
        public float bankHeight = .32f, bankSpan = .4f, bladeCellSize = .16f, bladeDensity = .55f;
        public float leafHeight = .1f, leafSpan = .12f;
        public int maximumCandidates = 180000;
        public void Validate()
        {
            var values = new[] { cellSize, density, shorelineDensity, bankBand, flowerFraction, minimumHeight, maximumHeight,
                maximumSpan, bankHeight, bankSpan, bladeCellSize, bladeDensity, leafHeight, leafSpan };
            foreach (float value in values) if (!Numbers.Finite(value)) throw new ArgumentException("Finite groundcover parameters required.");
            if (cellSize < .1f || bladeCellSize < .1f || density < 0 || density > 1 || shorelineDensity < 0 || shorelineDensity > 1
                || bankBand <= 0 || flowerFraction < 0 || flowerFraction > .2f || minimumHeight <= 0 || maximumHeight < minimumHeight
                || maximumSpan <= 0 || bankHeight <= 0 || bankSpan <= 0 || leafHeight <= 0 || leafSpan <= 0 || bladeDensity < 0 || bladeDensity > 1 || maximumCandidates < 1 || maximumCandidates > 1000000)
                throw new ArgumentException("Bounded groundcover parameters required.");
        }
    }
    [Serializable] public sealed class GroundcoverSample
    {
        public string id; public V3 point; public CoverKind kind;
        public float height, maximumSpan, yaw, variation;
    }
    public static class CompositionGroundcover
    {
        // Fine noninteractive details use projected/density limits rather than reserving every blade.
        public static IReadOnlyList<GroundcoverSample> Generate(CompositionLayout layout, GroundcoverStyle style, int seed, IEnumerable<PlacedInstance> blockers = null, CancellationToken cancellation = default)
        {
            if (layout == null || style == null) throw new ArgumentNullException(); style.Validate();
            var output = new List<GroundcoverSample>(); int evaluated = 0;
            var occupied = new PlacementContext(layout);
            foreach (var instance in blockers ?? Array.Empty<PlacedInstance>()) foreach (var volume in instance.volumes) occupied.Reserve(instance.id, volume);
            void Layer(string id, float cell, float density, bool blades)
            {
                int count = (int)Math.Ceiling(layout.extent / cell);
                long candidates = (long)(count * 2 + 1) * (count * 2 + 1);
                if (candidates + evaluated > style.maximumCandidates) throw new ArgumentException("Groundcover candidate budget exceeded. Increase cell size or split the layout.");
                for (int z = -count; z <= count; z++) for (int x = -count; x <= count; x++)
                {
                    cancellation.ThrowIfCancellationRequested(); evaluated++;
                    string key = id + "/" + x + "/" + z;
                    var point = new V3((x + .15f + Numbers.Unit(key, seed) * .7f) * cell, 0, (z + .15f + Numbers.Unit(key, seed, 1) * .7f) * cell);
                    if (layout.InBoard(point, layout.request.style.boardMargin) || layout.InLanding(point)) continue;
                    float bank = layout.BankDistance(point); if (bank < .15f) continue;
                    if (!layout.InSupportedFrames(point)) continue;
                    bool shore = bank < style.bankBand;
                    float patch = .5f + .25f * (float)Math.Sin(point.x * 1.45f) + .25f * (float)Math.Cos(point.z * .8f);
                    float chance = shore ? style.shorelineDensity * (.4f + patch * .6f) : density * (.35f + patch * .65f);
                    if (Numbers.Unit(key, seed, 2) > chance) continue;
                    float variation = Numbers.Unit(key, seed, 3);
                    var kind = blades ? CoverKind.Blade : shore ? CoverKind.Shrub : variation < style.flowerFraction ? CoverKind.Flower : variation < .42f ? CoverKind.Shrub : CoverKind.Leaf;
                    float height = style.minimumHeight + Numbers.Unit(key, seed, 4) * (style.maximumHeight - style.minimumHeight);
                    if (shore && !blades) height = style.minimumHeight + Numbers.Unit(key, seed, 4) * (style.bankHeight - style.minimumHeight);
                    point.y = layout.Height(point.x, point.z) + .008f;
                    float maximumSpan = blades ? height * .3f : shore ? style.bankSpan : style.maximumSpan;
                    if (kind == CoverKind.Leaf) { height = Math.Min(height, style.leafHeight); maximumSpan = Math.Min(maximumSpan, style.leafSpan); }
                    var box = new OrientedBox(new Box(new V3(0, height * .5f, 0), new V3(maximumSpan * .5f, height * .5f, maximumSpan * .5f)), Pose.At(point));
                    if (occupied.Collision(box) != null) continue;
                    output.Add(new GroundcoverSample { id = key, point = point, kind = kind, height = height,
                        maximumSpan = maximumSpan, variation = variation, yaw = Numbers.Unit(key, seed, 5) * 360 });
                }
            }
            Layer("cover", style.cellSize, style.density, false);
            Layer("blades", style.bladeCellSize, style.bladeDensity, true);
            return output;
        }
    }
}
