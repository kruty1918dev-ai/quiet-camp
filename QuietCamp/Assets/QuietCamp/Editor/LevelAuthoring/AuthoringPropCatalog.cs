using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using QuietCamp.Infrastructure;
using UnityEngine;

namespace QuietCamp.Editor.LevelAuthoring
{
    [Serializable]
    public sealed class AuthoringPropDescriptor
    {
        public string id, label, category, pack, description, placement;
        public float recommendedScale = 1;
        public bool waterRelative, part;
        public float[] boundsMin, boundsMax;
        public int triangles;
        [JsonIgnore] public bool SingleOnly => placement == "single";
        [JsonIgnore] public Bounds Bounds => new Bounds((CampArtDefinition.Vector(boundsMin) + CampArtDefinition.Vector(boundsMax)) * .5f,
            CampArtDefinition.Vector(boundsMax) - CampArtDefinition.Vector(boundsMin));
    }

    // Editor-owned metadata. Exported art includes geometry and poses only.
    public static class AuthoringPropCatalog
    {
        public const string CatalogPath = "Assets/QuietCamp/Authoring/LevelProps/catalog.json";
        public const string ModelPath = "Assets/QuietCamp/Authoring/LevelProps/models.json";
        sealed class Catalog { public int version; public AuthoringPropDescriptor[] entries; }
        static Dictionary<string, AuthoringPropDescriptor> _entries;
        public static IEnumerable<AuthoringPropDescriptor> Entries { get { EnsureLoaded(); return _entries.Values; } }
        public static void ResetCache() => _entries = null;
        public static float WaterSurface(LevelAuthoringDocument document)
        {
            var water = document.art.landscape?.meshes?.FirstOrDefault(m => m.water && m.vertices?.Length >= 3);
            return water != null ? water.vertices[1] : document.terrain != null ? -.02f : -.075f;
        }
        public static AuthoringPropDescriptor Find(string id)
        {
            return FindBase(id) ?? ModelAuthoringWorkshop.Find(id)?.prop;
        }
        public static AuthoringPropDescriptor FindBase(string id)
        {
            EnsureLoaded(); return id != null && _entries.TryGetValue(id, out var item) ? item : null;
        }
        static void EnsureLoaded()
        {
            if (_entries != null) return;
            var catalog = JsonConvert.DeserializeObject<Catalog>(File.ReadAllText(CatalogPath));
            if (catalog?.version != 1 || catalog.entries == null) throw new InvalidDataException("Непідтримуваний каталог пропсів.");
            foreach (var e in catalog.entries)
                if (string.IsNullOrWhiteSpace(e.id) || string.IsNullOrWhiteSpace(e.label) || e.recommendedScale <= 0
                    || e.boundsMin?.Length != 3 || e.boundsMax?.Length != 3
                    || e.boundsMin.Concat(e.boundsMax).Any(v => float.IsNaN(v) || float.IsInfinity(v))
                    || e.Bounds.size.x <= 0 || e.Bounds.size.y <= 0 || e.Bounds.size.z <= 0
                    || (e.placement != "single" && e.placement != "surface"))
                    throw new InvalidDataException("Некоректна картка пропса: " + e.id);
            _entries = catalog.entries.ToDictionary(e => e.id, StringComparer.Ordinal);
        }
        public static Bounds ModelBounds(CampArtDefinition.Model model)
        {
            var bounds = new Bounds(CampArtDefinition.Vector(new[] { model.vertices[0], model.vertices[1], model.vertices[2] }), Vector3.zero);
            for (int i = 3; i < model.vertices.Length; i += 3) bounds.Encapsulate(new Vector3(model.vertices[i], model.vertices[i+1], model.vertices[i+2]));
            return bounds;
        }
        public static Vector3[] Corners(AuthoringPropDescriptor prop, Matrix4x4 pose, Bounds? geometryBounds = null)
        {
            var b = geometryBounds ?? prop.Bounds; var result = new Vector3[8];
            for (int i = 0; i < 8; i++) result[i] = pose.MultiplyPoint3x4(new Vector3(
                (i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z));
            return result;
        }
        public static bool OverlapsBoard(AuthoringPropDescriptor prop, Matrix4x4 pose, int width, int height, Bounds? geometryBounds = null)
        {
            var points = Corners(prop, pose, geometryBounds);
            // Conservative footprint protects every cell, including after rotation/scale.
            return points.Min(p => p.x) < width * .5f + .12f && points.Max(p => p.x) > -width * .5f - .12f
                && points.Min(p => p.z) < height * .5f + .12f && points.Max(p => p.z) > -height * .5f - .12f;
        }
        public static List<string> Validate(LevelAuthoringDocument document)
        {
            var errors = new List<string>();
            var bounds = document.art.models.Where(m => Find(m.id)?.SingleOnly == true && m.vertices?.Length >= 3 && m.vertices.Length%3 == 0)
                .GroupBy(m => m.id).ToDictionary(g => g.Key, g => ModelBounds(g.First()));
            foreach (var item in document.art.instances ?? Array.Empty<CampArtDefinition.Instance>())
            {
                var prop = Find(item.modelId);
                if (prop == null || !prop.SingleOnly) continue;
                var pose = Matrix4x4.TRS(new Vector3(item.x, item.y, item.z),
                    item.rotationEuler != null ? Quaternion.Euler(CampArtDefinition.Vector(item.rotationEuler)) : Quaternion.Euler(0, item.yaw, 0),
                    item.scaleXYZ != null ? CampArtDefinition.Vector(item.scaleXYZ) : Vector3.one * item.scale);
                if (OverlapsBoard(prop, pose, document.level.width, document.level.height, bounds.TryGetValue(prop.id,out var b) ? b : prop.Bounds))
                    errors.Add(prop.label + " перетинає поле. Пересуньте весь контур основи за його межі.");
            }
            return errors;
        }
        public static bool OccludesBoard(AuthoringPropDescriptor prop, Matrix4x4 pose, Camera camera, int width, int height, Bounds? geometryBounds = null)
        {
            var points = Corners(prop, pose, geometryBounds).Select(camera.WorldToViewportPoint).Where(p => p.z > 0).ToArray();
            if (points.Length == 0) return false;
            float nearest = points.Min(p => p.z), minX = points.Min(p => p.x), maxX = points.Max(p => p.x),
                minY = points.Min(p => p.y), maxY = points.Max(p => p.y);
            for (int z = 0; z < height; z++) for (int x = 0; x < width; x++)
            {
                var cell = camera.WorldToViewportPoint(new Vector3(x - width*.5f + .5f, .1f, z - height*.5f + .5f));
                if (cell.z > nearest && cell.x > minX && cell.x < maxX && cell.y > minY && cell.y < maxY) return true;
            }
            return false;
        }
    }
}
