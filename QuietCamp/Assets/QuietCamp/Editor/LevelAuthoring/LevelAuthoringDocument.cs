using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using QuietCamp.Domain;
using QuietCamp.Infrastructure;
using UnityEngine;

namespace QuietCamp.Editor.LevelAuthoring
{
    [Serializable] public sealed class LevelAuthoringDocument
    {
        public int version = 2;
        public string sourceLevelId, sourceContentHash, sourceArtHash;
        public RiverTerrainAuthoringData terrain;
        public LevelData level;
        public CampArtDefinition art;
        public string title = "Новий табір";
        public string vegetationPreset;
        public int vegetationSeed;
        public static T Copy<T>(T value) => JsonConvert.DeserializeObject<T>(JsonConvert.SerializeObject(value));
        // Geometry snapshots are immutable; editing settings must not copy megabytes of mesh data.
        public LevelAuthoringDocument EditingCopy() => new LevelAuthoringDocument
        {
            version = version, title = title, sourceLevelId = sourceLevelId, sourceContentHash = sourceContentHash, sourceArtHash = sourceArtHash,
            vegetationPreset=vegetationPreset,vegetationSeed=vegetationSeed,
            level = Copy(level), art = art.EditingCopy(), terrain = terrain?.EditingCopy()
        };

        public static LevelAuthoringDocument Create(bool generated, string sourceId = null, int generationIndex = 1)
        {
            var level = Copy(generated && string.IsNullOrEmpty(sourceId)
                ? GeneratedCampSource.Generate("qc_camp", generationIndex)
                : LevelLoader.Load(generated ? sourceId : "QC001"));
            level.id = "QC_EDITOR_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            // A starter becomes a new draft, without references to previously baked campaign scenery.
            var starter = Newtonsoft.Json.Linq.JObject.FromObject(level);
            starter.Remove("bakedEnvironment"); starter.Remove("environmentalStory");
            level = starter.ToObject<LevelData>(); level.contentHash = null;
            if (!generated)
            {
                level.width = level.height = 6; level.entry = new[] { 0, 2 };
                level.blocked = level.shade = level.noise = Array.Empty<int[]>();
                level.objects = Array.Empty<EnvironmentObjectData>(); level.canopies = Array.Empty<ShadeCanopyData>();
                level.exteriorWalkable = Array.Empty<int[]>(); level.accessPoints = Array.Empty<AccessPointData>();
                level.friends = Array.Empty<string[]>(); level.witness = null; level.environment = null;
                level.ruleVersion = 2;
                foreach (var guest in level.guests) { guest.shade = false; guest.quiet = false; }
            }
            var art = CampCompositionGenerator.Generate(level, "riparian-clearing", level.seed, out _);
            if (!generated) { art.instances = Array.Empty<CampArtDefinition.Instance>(); art.cover = Array.Empty<CampArtDefinition.CoverPoint>(); }
            art.generation = null; art.review = null; art.attempts = Array.Empty<Kruty1918.ScenePlacement.Composition.CompositionAttempt>();
            return new LevelAuthoringDocument { level = level, art = art };
        }

        public void Resize(int width, int height)
        {
            if (width < 4 || width > 8 || height < 4 || height > 8) throw new ArgumentOutOfRangeException();
            level.width = width; level.height = height; art.width = width; art.height = height;
            bool Inside(int x, int z) => x >= 0 && z >= 0 && x < width && z < height;
            level.objects = level.objects.Where(o => Inside(o.x, o.z)).ToArray();
            level.canopies = level.canopies.Where(c => c.x >= 0 && c.z >= 0 && c.x < width && c.z < height).ToArray();
            level.witness = (level.witness ?? Array.Empty<Placement>()).Where(p => Inside(p.x + 1, p.z + 1)).ToArray();
            level.entry = new[] { 0, Math.Min(height - 1, level.entry[1]) };
            level.accessPoints = level.accessPoints.Where(a => Inside(a.x, a.z) && (a.x == 0 || a.z == 0 || a.x == width - 1 || a.z == height - 1)).ToArray();
            level.exteriorWalkable = Array.Empty<int[]>();
            ReconcileObjects();
        }

        public void PlaceObject(Cell cell, string assetId)
        {
            if (!RuleEvaluator.Inside(level, cell)) throw new ArgumentException("Об’єкт має бути всередині поля.");
            var objects = level.objects.Where(o => o.x != cell.X || o.z != cell.Z).ToList();
            objects.Add(new EnvironmentObjectData { assetId = assetId, x = cell.X, z = cell.Z });
            level.objects = objects.ToArray(); ReconcileObjects();
        }
        public void RemoveObject(Cell cell)
        {
            level.objects = level.objects.Where(o => o.x != cell.X || o.z != cell.Z).ToArray();
            level.canopies = level.canopies.Where(c => Math.Abs(c.x - cell.X) > .01f || Math.Abs(c.z - cell.Z) > .01f).ToArray();
            ReconcileObjects();
        }
        public void ReconcileObjects()
        {
            level.blocked = level.objects.Select(o => new[] { o.x, o.z }).ToArray();
            level.noise = level.objects.Where(o => o.assetId == "campfire_stones").Select(o => new[] { o.x, o.z }).ToArray();
            level.shade = ShadeProjection.Cells(level).ToArray();
            level.contentHash = null;
        }
        public void PutTent(string guestId, Cell cell, int rotation)
        {
            var placement = new Placement { guestId = guestId, x = cell.X, z = cell.Z, rotation = rotation };
            if (RuleEvaluator.Footprint(placement).Any(c => !RuleEvaluator.Inside(level, c))) throw new ArgumentException("Намет 2×2 виходить за поле.");
            level.witness = (level.witness ?? Array.Empty<Placement>()).Where(p => p.guestId != guestId).Concat(new[] { placement }).ToArray();
        }
        public List<string> Validate(bool requireSolution)
        {
            var snapshot = Copy(level);
            if (!requireSolution) snapshot.witness = null;
            var errors = LevelContentValidator.Validate(snapshot);
            errors.AddRange(AuthoringPropCatalog.Validate(this));
            if (requireSolution && (snapshot.witness == null || snapshot.witness.Length != snapshot.guests.Length)) errors.Add("Потрібне рішення для всіх гостей.");
            if (requireSolution && errors.Count == 0 && !RuleEvaluator.Evaluate(snapshot, snapshot.witness).IsSolved) errors.Add("Еталонне рішення порушує умови гри.");
            try { art.Validate(level); } catch (Exception e) { errors.Add(e.Message); }
            try { terrain?.Validate(); } catch (Exception e) { errors.Add(e.Message); }
            try { RiverTerrainIntegration.Validate?.Invoke(this); } catch (Exception e) { errors.Add(e.Message); }
            if (terrain != null && art.landscape == null) errors.Add("Спочатку дочекайтеся оновлення річки й землі.");
            if (art.landscape != null)
                foreach (var mesh in art.landscape.meshes.Where(m => m.water))
                    for (int i = 0; i < mesh.triangles.Length; i += 3)
                    {
                        int a = mesh.triangles[i] * 3, b = mesh.triangles[i + 1] * 3, c = mesh.triangles[i + 2] * 3;
                        float halfX = level.width / 2f + .15f, halfZ = level.height / 2f + .15f;
                        if (Mathf.Min(mesh.vertices[a], mesh.vertices[b], mesh.vertices[c]) < halfX && Mathf.Max(mesh.vertices[a], mesh.vertices[b], mesh.vertices[c]) > -halfX
                            && Mathf.Min(mesh.vertices[a + 2], mesh.vertices[b + 2], mesh.vertices[c + 2]) < halfZ && Mathf.Max(mesh.vertices[a + 2], mesh.vertices[b + 2], mesh.vertices[c + 2]) > -halfZ)
                        { errors.Add("Вода перетинає ігрове поле. Пересуньте точки русла."); return errors; }
                    }
            if (errors.Count == 0)
                for(int z=0;z<=level.height*4;z++) for(int x=0;x<=level.width*4;x++)
                    if(art.GroundHeight(x*.25f-level.width*.5f,z*.25f-level.height*.5f)<-.075f)
                    { errors.Add("Річка накриває ігрове поле. Відсуньте русло або зменшіть його ширину."); return errors; }
            return errors;
        }
        public static string SafeName(string id)
        {
            if (string.IsNullOrWhiteSpace(id) || id.Length > 80 || id.Any(c => !(char.IsLetterOrDigit(c) || c == '_' || c == '-')))
                throw new ArgumentException("ID: лише літери, цифри, _ або -.");
            return id;
        }
        public void Save(string path)
        {
            if ((version != 1 && version != 2) || level == null || art == null) throw new InvalidDataException("Неповна чернетка.");
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            string temporary = path + ".pending";
            File.WriteAllText(temporary, JsonConvert.SerializeObject(this, Formatting.Indented), new UTF8Encoding(false));
            if (File.Exists(path)) File.Replace(temporary, path, null); else File.Move(temporary, path);
        }
        public static LevelAuthoringDocument Open(string path)
        {
            var result = JsonConvert.DeserializeObject<LevelAuthoringDocument>(File.ReadAllText(path));
            if (result == null || (result.version != 1 && result.version != 2) || result.level == null || result.art == null) throw new InvalidDataException("Непідтримувана чернетка.");
            result.version = 2;
            return result;
        }
        public void Export(string directory)
        {
            var errors = Validate(true); if (errors.Count > 0) throw new InvalidDataException(string.Join("\n", errors));
            var copy = Copy(this); copy.art.levelId = copy.level.id;
            string id = SafeName(copy.level.id);
            using (var hash = SHA256.Create()) copy.level.compositionHash = BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(copy.art)))).Replace("-", "").ToLowerInvariant();
            copy.level.contentHash = CampContent.CalculateHash(copy.level);
            Directory.CreateDirectory(directory);
            copy.Save(Path.Combine(directory, id + ".draft.json"));
            File.WriteAllText(Path.Combine(directory, id + ".level.json"), JsonConvert.SerializeObject(copy.level, Formatting.Indented));
            File.WriteAllText(Path.Combine(directory, id + ".art.json"), JsonConvert.SerializeObject(copy.art));
            if (copy.art.models.Any(m => m.id.StartsWith("qc_aircraft_", StringComparison.Ordinal) || (m.sourceModelId??"").StartsWith("qc_aircraft_", StringComparison.Ordinal)))
                File.Copy("Assets/QuietCamp/Authoring/LevelProps/ATTRIBUTION.md", Path.Combine(directory, "ATTRIBUTION.md"), true);
            File.WriteAllText(Path.Combine(directory, "README.txt"), "Verified authoring export. Not installed into the campaign. Level + frozen art must be loaded together. Prefab World Builder is Editor-only.");
        }
    }
    public enum PlacementTool { Paint, Place, Line, Erase }
    public interface IPlacementToolAdapter
    {
        void Activate(PlacementTool tool, GameObject prefab, Transform parent, GameObject surface, float radius, float density, float spacing, float variation);
        void Stop();
        void OpenAdvanced();
    }
    public interface IControlledPlacementAdapter
    {
        string Backend { get; }
        GameObject[] PlaceBatch(GameObject prefab, Transform parent, GameObject surface, CampArtDefinition.Instance[] poses);
    }
    // Explicit Editor integration point; no vendor types in runtime or named Editor assemblies.
    public static class PlacementToolIntegration
    {
        public static IPlacementToolAdapter Adapter { get; private set; }
        public static IControlledPlacementAdapter Controlled => Adapter as IControlledPlacementAdapter;
        public static void Register(IPlacementToolAdapter adapter) => Adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
    }
}
