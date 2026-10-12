using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Kruty1918.ScenePlacement;
using Kruty1918.ScenePlacement.Composition;

namespace Kruty1918.VegetationPlacement
{
    public enum CompositionHabitat { WoodlandFrame, Shoreline }
    [Serializable] public sealed class CompositionHabitatStyle
    {
        public float moistureFalloff = .35f, minimumMoisture = .2f, bankClearance = .1f;
        public float shorelineBand = 1.1f, treeBankBand = 1.5f, frameEdge = .58f, interiorDensity = .12f;
        public float patchScale = .7f, patchContrast = .65f;
        public CompositionHabitatStyle Snapshot() => (CompositionHabitatStyle)MemberwiseClone();
        public void Validate()
        {
            var values = new[] { moistureFalloff, minimumMoisture, bankClearance, shorelineBand, treeBankBand, frameEdge, interiorDensity, patchScale, patchContrast };
            if (values.Any(v => !Numbers.Finite(v)) || moistureFalloff <= 0 || minimumMoisture < 0 || minimumMoisture > 1 || bankClearance < 0
                || shorelineBand <= bankClearance || treeBankBand <= bankClearance || frameEdge < 0 || frameEdge > 1
                || interiorDensity < 0 || interiorDensity > 1 || patchScale <= 0 || patchContrast < 0 || patchContrast > 1)
                throw new ArgumentException("Invalid habitat field parameters.");
        }
    }
    // Adds ecological fields to the existing stable-cell generator; placement remains authoritative.
    public sealed class CompositionVegetationStage : ICompositionStage
    {
        public string Id { get; }
        public CompositionHabitat Habitat { get; }
        readonly Species[] species;
        readonly CompositionHabitatStyle style;
        public Species[] Species => species.Select(s => s.Snapshot()).ToArray();
        public float CellSize { get; }
        public float Density { get; }
        public int MinimumCount { get; }
        public CompositionVegetationStage(string id, CompositionHabitat habitat, Species[] species, float cellSize, float density, int minimumCount = 1, CompositionHabitatStyle style = null)
        {
            if (string.IsNullOrWhiteSpace(id) || species == null || species.Length == 0 || !Numbers.Finite(cellSize) || cellSize < .15f
                || !Numbers.Finite(density) || density <= 0 || density > 1 || minimumCount < 0) throw new ArgumentException("Invalid composition vegetation stage.");
            if (species.Any(s => s == null || s.shape == null)) throw new ArgumentException("Resolved species shapes required.");
            Id = id; Habitat = habitat; this.species = species.Select(s => s.Snapshot()).ToArray(); CellSize = cellSize; Density = density; MinimumCount = minimumCount;
            this.style = (style ?? new CompositionHabitatStyle()).Snapshot(); this.style.Validate();
        }
        public void Generate(CompositionRun run, CancellationToken cancellation)
        {
            var frame = run.Layout;
            var result = VegetationGenerator.Generate(new VegetationSpec { id = Id, seed = run.Seed,
                zone = new Box(V3.Zero, new V3(frame.extent, 1, frame.extent)), cellSize = CellSize,
                density = Density, patchScale = style.patchScale, patchContrast = style.patchContrast, species = species }, run.context, new Fields(frame, Habitat, style), cancellation);
            run.Add(Id, result.instances, result.diagnostics);
            if (result.instances.Count < MinimumCount) run.plan.diagnostics.Add(new Diagnostic("insufficient-habitat", Id, "Not enough readable plants in this habitat."));
        }
        sealed class Fields : IEnvironmentalFields
        {
            readonly CompositionLayout frame; readonly CompositionHabitat habitat; readonly CompositionHabitatStyle style;
            public Fields(CompositionLayout frame, CompositionHabitat habitat, CompositionHabitatStyle style) { this.frame = frame; this.habitat = habitat; this.style = style; }
            public float Sample(string field, V3 point, float fallback)
            {
                float bank = frame.BankDistance(point);
                if (field == "moisture") return Numbers.Clamp(1 - bank * style.moistureFalloff, style.minimumMoisture, 1);
                if (field != "density") return fallback;
                if (frame.InBoard(point, frame.request.style.boardMargin) || frame.InLanding(point) || bank < style.bankClearance) return 0;
                var screen = frame.Project(point);
                if (!frame.InSupportedFrames(point)) return 0;
                if (habitat == CompositionHabitat.Shoreline) return bank < style.shorelineBand ? 1 : 0;
                float edge = Math.Max(Math.Abs(screen.x - .5f) / .5f, Math.Abs(screen.y - .5f) / .5f);
                // Foreground framing and bank trees; the central meadow remains legible.
                return edge > style.frameEdge || bank < style.treeBankBand ? 1 : style.interiorDensity;
            }
        }
    }
}
