using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Kruty1918.ScenePlacement;
using Kruty1918.ScenePlacement.Composition;

namespace Kruty1918.VegetationPlacement
{
    public enum CompositionHabitat { WoodlandFrame, Shoreline }
    // Adds ecological fields to the existing stable-cell generator; placement remains authoritative.
    public sealed class CompositionVegetationStage : ICompositionStage
    {
        public string Id { get; }
        public CompositionHabitat Habitat { get; }
        public Species[] Species { get; }
        public float CellSize { get; }
        public float Density { get; }
        public int MinimumCount { get; }
        public CompositionVegetationStage(string id, CompositionHabitat habitat, Species[] species, float cellSize, float density, int minimumCount = 1)
        {
            if (string.IsNullOrWhiteSpace(id) || species == null || species.Length == 0 || !Numbers.Finite(cellSize) || cellSize < .15f
                || !Numbers.Finite(density) || density <= 0 || density > 1 || minimumCount < 0) throw new ArgumentException("Invalid composition vegetation stage.");
            Id = id; Habitat = habitat; Species = species; CellSize = cellSize; Density = density; MinimumCount = minimumCount;
        }
        public void Generate(CompositionRun run, CancellationToken cancellation)
        {
            var frame = run.Layout;
            var result = VegetationGenerator.Generate(new VegetationSpec { id = Id, seed = run.Seed,
                zone = new Box(V3.Zero, new V3(frame.extent, 1, frame.extent)), cellSize = CellSize,
                density = Density, patchScale = .7f, patchContrast = .65f, species = Species }, run.context, new Fields(frame, Habitat), cancellation);
            run.Add(Id, result.instances, result.diagnostics);
            if (result.instances.Count < MinimumCount) run.plan.diagnostics.Add(new Diagnostic("insufficient-habitat", Id, "Not enough readable plants in this habitat."));
        }
        sealed class Fields : IEnvironmentalFields
        {
            readonly CompositionLayout frame; readonly CompositionHabitat habitat;
            public Fields(CompositionLayout frame, CompositionHabitat habitat) { this.frame = frame; this.habitat = habitat; }
            public float Sample(string field, V3 point, float fallback)
            {
                float bank = frame.BankDistance(point);
                if (field == "moisture") return Numbers.Clamp(1 - bank * .35f, .2f, 1);
                if (field != "density") return fallback;
                if (frame.InBoard(point, frame.request.style.boardMargin) || frame.InLanding(point) || bank < .1f) return 0;
                var screen = frame.Project(point);
                if (!frame.InSupportedFrames(point)) return 0;
                if (habitat == CompositionHabitat.Shoreline) return bank < 1.1f ? 1 : 0;
                float edge = Math.Max(Math.Abs(screen.x - .5f) / .5f, Math.Abs(screen.y - .5f) / .5f);
                // Foreground framing and bank trees; the central meadow remains legible.
                return edge > .58f || bank < 1.5f ? 1 : .12f;
            }
        }
    }
}
