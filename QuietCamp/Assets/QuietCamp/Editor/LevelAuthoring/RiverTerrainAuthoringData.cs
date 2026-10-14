using System;
using System.Collections.Generic;
using System.Linq;
using QuietCamp.Infrastructure;
using UnityEngine;

namespace QuietCamp.Editor.LevelAuthoring
{
    [Serializable] public sealed class RiverTerrainAuthoringData
    {
        public float size, width = 2, depth = .65f, bankWidth = 1.2f, bankSoftness = .6f;
        public int resolution = 257;
        public float[] baseHeights;
        public RiverPoint[] points;
        public LandscapeStamp[] stamps = Array.Empty<LandscapeStamp>();
        public RiverTerrainAuthoringData EditingCopy()
        {
            var copy = (RiverTerrainAuthoringData)MemberwiseClone();
            copy.points = points.Select(p => new RiverPoint { x = p.x, z = p.z }).ToArray();
            copy.stamps = stamps.Select(s => new LandscapeStamp { id = s.id, x = s.x, z = s.z, radius = s.radius, height = s.height }).ToArray();
            return copy;
        }
        public static RiverTerrainAuthoringData FromLegacy(CampArtDefinition art, int resolution = 257)
        {
            if(resolution<33 || resolution>513 || ((resolution-1)&(resolution-2))!=0)throw new ArgumentException("Terrain resolution must be 2^n + 1.");
            var data = new RiverTerrainAuthoringData { size = art.surroundExtent * 2, width = art.streamWidth, depth = art.waterDepth, resolution=resolution };
            var ground = LevelAuthoringDocument.Copy(art); ground.landscape = null; ground.waterDepth = 0;
            data.baseHeights = new float[data.resolution * data.resolution];
            for (int z = 0; z < data.resolution; z++) for (int x = 0; x < data.resolution; x++)
                data.baseHeights[z * data.resolution + x] = ground.GroundHeight(x * data.size / (data.resolution - 1) - data.size / 2, z * data.size / (data.resolution - 1) - data.size / 2);
            float yaw = art.frameYaw * Mathf.Deg2Rad; var points = new List<RiverPoint>();
            for (int i = 0; i < 9; i++)
            {
                float v = Mathf.Lerp(-data.size * .55f, data.size * .55f, i / 8f);
                float centre = art.streamCentre < data.size * .5f ? art.streamCentre : Mathf.Max(8, art.width * .5f + 4);
                float u = centre + Kruty1918.ScenePlacement.Composition.RiverGeometry.Centre(v, art.streamSlope, art.streamBend);
                points.Add(new RiverPoint { x = u * Mathf.Cos(yaw) + v * Mathf.Sin(yaw), z = -u * Mathf.Sin(yaw) + v * Mathf.Cos(yaw) });
            }
            data.points = points.ToArray(); return data;
        }
        public void Validate()
        {
            bool Finite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
            if (!Finite(size) || size < 8 || size > 100 || resolution < 33 || resolution > 513
                || baseHeights == null || baseHeights.Length != resolution * resolution || baseHeights.Any(v => !Finite(v))
                || points == null || points.Length < 2 || points.Length > 64 || points.Any(p => p == null || !Finite(p.x) || !Finite(p.z))
                || !Finite(width) || width < .3f || width > 8 || !Finite(depth) || depth < .1f || depth > 3
                || !Finite(bankWidth) || bankWidth < .1f || bankWidth > 6 || !Finite(bankSoftness) || bankSoftness < .05f || bankSoftness > 4
                || stamps == null || stamps.Length > 100 || stamps.Any(s => s == null || !Finite(s.x) || !Finite(s.z) || !Finite(s.height)
                    || Mathf.Abs(s.height) > 4 || !Finite(s.radius) || s.radius < .25f || s.radius > 12))
                throw new ArgumentException("Некоректні параметри річки чи землі.");
        }
    }
    [Serializable] public sealed class RiverPoint { public float x, z; }
    [Serializable] public sealed class LandscapeStamp
    {
        public string id = Guid.NewGuid().ToString("N");
        public float x, z, radius = 3, height = 1;
    }
    public interface IRiverTerrainAuthoringAdapter : IDisposable
    {
        bool IsUpdating { get; }
        GameObject Surface { get; }
        void Apply(LevelAuthoringDocument document);
        FrozenLandscape Bake();
        float SampleHeight(float x, float z);
        void SelectRiver();
    }
    public static class RiverTerrainIntegration
    {
        public static Func<LevelAuthoringWorkspace, IRiverTerrainAuthoringAdapter> Factory { get; private set; }
        public static Action<LevelAuthoringDocument> Validate { get; private set; }
        public static void Register(Func<LevelAuthoringWorkspace, IRiverTerrainAuthoringAdapter> factory, Action<LevelAuthoringDocument> validate = null)
        { Factory = factory ?? throw new ArgumentNullException(nameof(factory)); Validate = validate; }
    }
}
