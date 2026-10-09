using System;
using System.Collections.Generic;
namespace QuietCamp.Domain
{
    [Serializable] public sealed class GuestData
    {
        public string id, nameKey, assetId;
        public bool shade, quiet;
    }
    [Serializable] public sealed class Placement
    {
        public string guestId;
        public int x, z, rotation;
        public Placement Copy() => new Placement { guestId=guestId,x=x,z=z,rotation=rotation };
    }
    [Serializable] public sealed class LevelData
    {
        public int schemaVersion, ruleVersion, order, chapter, seed, width, height;
        public string id, contentHash, generatorVersion, lighting, tutorialKey;
        public int generationAttempt, decorSeed;
        public int[] entry;
        public AccessPointData[] accessPoints = Array.Empty<AccessPointData>();
        public int[][] blocked, shade, noise;
        // Version 2 walking topology: only these authored cells outside the
        // placement grid are walkable. Tent footprints always remain inside.
        public int[][] exteriorWalkable = Array.Empty<int[]>();
        public GuestData[] guests;
        public string[][] friends;
        public Placement[] witness;
        public string environmentPreset;
        public EnvironmentObjectData[] objects = Array.Empty<EnvironmentObjectData>();
        public ShadeCanopyData[] canopies = Array.Empty<ShadeCanopyData>();
        public EnvironmentCompositionData environment;
        public EnvironmentalStoryData environmentalStory;
        public bool ShouldSerializeenvironmentalStory() => environmentalStory != null;
    }
    [Serializable] public sealed class EnvironmentCompositionData
    {
        public string biomeId, seasonId, weatherId;
        public float moisture, treeDensity;
        public int clusterSeed;
        public string[] meadowSpecies = Array.Empty<string>(), storyMotifs = Array.Empty<string>();
        public ShorelineData shore;

        // Old album snapshots have no composition descriptor. Resolve a
        // compatible view without changing or rehashing their saved content.
        public static EnvironmentCompositionData For(LevelData level)
            => level?.environment ?? new EnvironmentCompositionData
            {
                biomeId = level?.environmentPreset == "pines" ? "pines" : "meadow",
                seasonId = "summer", weatherId = "clear", moisture = .25f,
                treeDensity = level?.environmentPreset == "pines" ? .8f : .45f,
                clusterSeed = level?.decorSeed ?? 0, meadowSpecies = new[] { "grass" }
            };
    }
    [Serializable] public sealed class ShorelineData
    {
        public string kind, side;
        public float offset, width;
        public int seed;
    }
    [Serializable] public sealed class AccessPointData
    {
        public string id, kind; // "entry" or "exit"; all share one walking network.
        public int x, z;
    }
    [Serializable] public sealed class EnvironmentObjectData
    {
        public string assetId;
        public int x, z, rotation;
    }
    // Authored projection of a crown onto the field, in board coordinates.
    // A cell is shaded when its centre is inside the union of these ellipses.
    [Serializable] public sealed class ShadeCanopyData
    {
        public float x, z, radiusX, radiusZ;
    }
    [Serializable] public sealed class LevelSummary
    {
        public string id, environmentPreset, lighting, contentHash;
        public int ruleVersion;
        public int number, width, height, decorSeed;
        public bool shade, quiet, friends, fire;
        public int[] entry;
        public EnvironmentObjectData[] mapObjects = Array.Empty<EnvironmentObjectData>();
        public AccessPointData[] accessPoints = Array.Empty<AccessPointData>();
        public ShadeCanopyData[] canopies = Array.Empty<ShadeCanopyData>();
        public int[][] exteriorWalkable = Array.Empty<int[]>();
        public int[][] noise = Array.Empty<int[]>();
        public EnvironmentCompositionData environment;
        public EnvironmentalStoryData environmentalStory;
        // Offline-only input: detailed narrative does not inflate the runtime summary catalog.
        public bool ShouldSerializeenvironmentalStory() => false;
    }
    public readonly struct Cell : IEquatable<Cell>
    {
        public readonly int X, Z;
        public Cell(int x,int z) { X=x; Z=z; }
        public bool Equals(Cell b) => X==b.X && Z==b.Z;
        public override bool Equals(object o) => o is Cell b && Equals(b);
        public override int GetHashCode() => unchecked(X*397 ^ Z);
        public override string ToString() => $"({X},{Z})";
    }
    public sealed class RuleIssue
    {
        public string Code, GuestId;
        public bool Hard;
        public Cell[] Cells;
        public RuleIssue(string code,string guestId,bool hard,params Cell[] cells)
        { Code=code; GuestId=guestId; Hard=hard; Cells=cells; }
    }
    public sealed class RuleReport
    {
        public readonly List<RuleIssue> Issues=new List<RuleIssue>();
        public readonly List<RouteEvidence> Routes=new List<RouteEvidence>();
        public bool CanCommit => !Issues.Exists(i=>i.Hard);
        public bool IsSolved => Issues.Count==0;
    }
}
