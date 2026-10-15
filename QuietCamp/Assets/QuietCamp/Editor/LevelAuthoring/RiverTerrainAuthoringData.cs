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
        // Continue a legacy heightfield to its authored surround extent. Interior
        // heights and rivers are preserved; ordinary full-size terrain needs none.
        public static void EnsureGroundMargins(CampArtDefinition art)
        {
            var land=art.landscape;if(land==null)return;
            const string name="Surrounding ground margins";
            var meshes=land.meshes.Where(m=>m.name!=name).ToList();
            float extent=art.surroundExtent;
            float minX=Mathf.Min(land.originX,-extent),minZ=Mathf.Min(land.originZ,-extent);
            float maxX=Mathf.Max(land.originX+land.size,extent),maxZ=Mathf.Max(land.originZ+land.size,extent);
            if(minX==land.originX&&minZ==land.originZ&&maxX==land.originX+land.size&&maxZ==land.originZ+land.size){land.meshes=meshes.ToArray();return;}
            var vertices=new List<float>();var normals=new List<float>();var uv=new List<float>();var colors=new List<float>();var indices=new List<int>();
            int segments=land.resolution-1,rings=4;
            var inner=new[]{new Vector2(land.originX,land.originZ),new Vector2(land.originX+land.size,land.originZ),new Vector2(land.originX+land.size,land.originZ+land.size),new Vector2(land.originX,land.originZ+land.size)};
            var outer=new[]{new Vector2(minX,minZ),new Vector2(maxX,minZ),new Vector2(maxX,maxZ),new Vector2(minX,maxZ)};
            for(int side=0;side<4;side++)
            {
                int start=vertices.Count/3,next=(side+1)%4;
                for(int r=0;r<=rings;r++)for(int j=0;j<=segments;j++)
                {
                    float along=j/(float)segments;var p=Vector2.Lerp(Vector2.Lerp(inner[side],inner[next],along),Vector2.Lerp(outer[side],outer[next],along),r/(float)rings);
                    float h=land.SampleHeight(p.x,p.y),eps=.1f;
                    var normal=new Vector3(land.SampleHeight(p.x-eps,p.y)-land.SampleHeight(p.x+eps,p.y),eps*2,land.SampleHeight(p.x,p.y-eps)-land.SampleHeight(p.x,p.y+eps)).normalized;
                    var c=Color.Lerp(CampArtDefinition.Color(art.groundLow),CampArtDefinition.Color(art.groundHigh),Mathf.Clamp01((h+.05f)*.5f));
                    vertices.AddRange(new[]{p.x,h,p.y});normals.AddRange(new[]{normal.x,normal.y,normal.z});uv.AddRange(new[]{(p.x-land.originX)/land.size,(p.y-land.originZ)/land.size});colors.AddRange(new[]{c.r,c.g,c.b,1f});
                }
                for(int r=0;r<rings;r++)for(int j=0;j<segments;j++)
                {int a=start+r*(segments+1)+j,b=a+1,c=a+segments+1,d=c+1;indices.AddRange(new[]{a,b,c,b,d,c});}
            }
            meshes.Add(new LandscapeMesh{name=name,vertices=vertices.ToArray(),normals=normals.ToArray(),uv=uv.ToArray(),colors=colors.ToArray(),triangles=indices.ToArray()});land.meshes=meshes.ToArray();
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
