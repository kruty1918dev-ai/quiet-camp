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
        public int[][] blocked, shade, noise;
        public GuestData[] guests;
        public string[][] friends;
        public Placement[] witness;
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
        public bool CanCommit => !Issues.Exists(i=>i.Hard);
        public bool IsSolved => Issues.Count==0;
    }
}
