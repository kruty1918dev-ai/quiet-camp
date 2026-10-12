using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using Kruty1918.ScenePlacement;
using Kruty1918.ScenePlacement.Composition;
using Kruty1918.VegetationPlacement;
using Kruty1918.RockPlacement;

sealed class HostProfile
{
    public string id, crossing, boundary;
    public CompositionStyle layout;
    public CompositionReviewProfile quality;
    public AssetShape[] assets;
    public Habitat[] habitats;
    public string[] rocks;
    public float rockInterval = 2.5f;
    public GroundcoverStyle groundcover;
    public sealed class Habitat
    {
        public string id; public CompositionHabitat habitat;
        public float cellSize = 2, density = .8f; public int minimumCount = 1;
        public Species[] species; public CompositionHabitatStyle fields;
    }
}
sealed class CompositionArtifact
{
    public string kind, profileSha256, engineAssemblySha256;
    public CompositionRequest request;
    public int attempt, effectiveSeed;
    public CompositionReview review;
    public CompositionAttempt[] attempts;
    public PlacedInstance[] instances;
    public IReadOnlyList<GroundcoverSample> groundcover;
}
static class Program
{
    static void Main(string[] args)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        if (args.Length % 2 != 0) throw new ArgumentException("Use --profile path --width N --depth N --seed N --output path.");
        for (int i = 0; i < args.Length; i += 2) values.Add(args[i], args[i + 1]);
        var options = new JsonSerializerOptions { IncludeFields = true, IgnoreReadOnlyProperties = true, WriteIndented = true };
        string path = Path.GetFullPath(values["--profile"]); byte[] source = File.ReadAllBytes(path);
        var profile = JsonSerializer.Deserialize<HostProfile>(source, options);
        var assets = profile.assets.ToDictionary(a => a.id, StringComparer.Ordinal);
        var request = new CompositionRequest { id = profile.id, width = float.Parse(values["--width"], System.Globalization.CultureInfo.InvariantCulture),
            depth = float.Parse(values["--depth"], System.Globalization.CultureInfo.InvariantCulture), seed = int.Parse(values["--seed"]), style = profile.layout };
        var stages = new List<ICompositionStage>();
        if (!string.IsNullOrEmpty(profile.crossing))
        {
            var bridge = assets[profile.crossing]; var box = bridge.volumes[0];
            stages.Add(new CrossingStage(bridge, box.half.x > box.half.z ? 0 : 90));
        }
        if (!string.IsNullOrEmpty(profile.boundary)) stages.Add(new BankBoundaryStage(assets[profile.boundary]));
        stages.Add(new ShorelineRockStage(profile.rocks.Select(id => assets[id]).ToArray(), profile.rockInterval));
        foreach (var habitat in profile.habitats)
        {
            foreach (var species in habitat.species) species.shape = assets[species.id];
            stages.Add(new CompositionVegetationStage(habitat.id, habitat.habitat, habitat.species, habitat.cellSize, habitat.density, habitat.minimumCount, habitat.fields));
        }
        var plan = CompositionGenerator.Generate(request, stages, critic: new CompositionCritic(profile.quality));
        if (!plan.Ok) throw new InvalidOperationException(string.Join("; ", plan.diagnostics.Where(d => d.error)) + " | " + string.Join("; ", plan.attempts.Select(a => a.attempt + ": " + string.Join(",", a.failures))));
        var cover = CompositionGroundcover.Generate(plan.layout, profile.groundcover, plan.effectiveSeed, plan.instances);
        var artifact = new CompositionArtifact { kind = "portable-composition-plan", profileSha256 = Convert.ToHexString(SHA256.HashData(source)).ToLowerInvariant(),
            engineAssemblySha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(Program).Assembly.Location))).ToLowerInvariant(),
            request = request, attempt = plan.attempt, effectiveSeed = plan.effectiveSeed, review = plan.review, attempts = plan.attempts,
            instances = plan.instances.OrderBy(p => p.id, StringComparer.Ordinal).ToArray(), groundcover = cover };
        string output = Path.GetFullPath(values["--output"]); Directory.CreateDirectory(Path.GetDirectoryName(output));
        File.WriteAllText(output, JsonSerializer.Serialize(artifact, options));
        Console.WriteLine(profile.id + ": attempt " + plan.attempt + ", score " + plan.review.score.ToString("F3", System.Globalization.CultureInfo.InvariantCulture)
            + ", " + plan.instances.Count + " instances, " + cover.Count + " fine details. Portable plan; no native render.");
    }
}
