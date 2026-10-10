using UnityEngine;

namespace QuietCamp.Presentation.World
{
    [PreferBinarySerialization]
    public sealed class RoadmapWorldChunk : ScriptableObject
    {
        public string sourceHash;
        public int index;
        // Terrain, rigid architecture, canopy, understory, water, airborne pollen.
        public Mesh[] balanced, low;
        public string[] sourceAssets;
        public long estimatedBytes;
    }
}
