using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using QuietCamp.Domain;
using QuietCamp.Infrastructure;
using UnityEditor;

namespace QuietCamp.Editor.LevelAuthoring
{
    public sealed class AuthoringCampaignCard
    {
        public string id;
        public int number, width, height, guests;
    }
    public static class LevelAuthoringCampaign
    {
        const string Content = "Assets/QuietCamp/Resources/QuietCamp";
        static AuthoringCampaignCard[] _cards;
        static string LevelPath(string root, string id) => Path.Combine(root, id.StartsWith("gen:", StringComparison.Ordinal) ? "GeneratedLevels" : "Levels", id.Replace(':', '_') + ".json");
        static string HashFile(string path)
        { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", "").ToLowerInvariant(); }
        public static AuthoringCampaignCard[] Cards => _cards ?? (_cards = ReadCards(Content));
        public static AuthoringCampaignCard[] ReadCards(string root)
        {
            var ids = JObject.Parse(File.ReadAllText(Path.Combine(root, "campaign.json")))["campaignLevelIds"].Values<string>().ToArray();
            return ids.Select((id, i) =>
            {
                var level = JsonConvert.DeserializeObject<LevelData>(File.ReadAllText(LevelPath(root, id)));
                return new AuthoringCampaignCard { id = id, number = i + 1, width = level.width, height = level.height, guests = level.guests.Length };
            }).ToArray();
        }
        public static LevelAuthoringDocument Open(string id)
        {
            if (!Cards.Any(c => c.id == id)) throw new ArgumentException("Рівень відсутній у кампанії.");
            var level = LevelAuthoringDocument.Copy(LevelLoader.Load(id));
            var art = CampArtDefinition.Load(level);
            if (art == null)
            {
                art = ProceduralSurroundSnapshot.Capture(level);
            }
            art = LevelAuthoringDocument.Copy(art);
            level.id = "QC_EDITOR_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            BindStory(level, id, level.id);
            art.levelId = level.id; art.width = level.width; art.height = level.height;
            return new LevelAuthoringDocument { level = level, art = art, title = "Рівень " + Cards.Single(c => c.id == id).number,
                sourceLevelId = id, sourceContentHash = HashFile(LevelPath(Content, id)), sourceArtHash = ArtHash(Content, id) };
        }
        public static void Apply(LevelAuthoringDocument document, string root = Content)
        {
            if (string.IsNullOrEmpty(document.sourceLevelId)) throw new InvalidOperationException("Це новий рівень. Експортуйте його окремо.");
            var ids = ReadCards(root).Select(c => c.id).ToArray();
            if (!ids.Contains(document.sourceLevelId)) throw new InvalidOperationException("Початковий рівень більше не входить до кампанії.");
            string levelPath = LevelPath(root, document.sourceLevelId);
            if (HashFile(levelPath) != document.sourceContentHash) throw new InvalidOperationException("Початковий рівень змінився. Відкрийте його заново; ваша чернетка залишається збереженою.");
            if (document.sourceArtHash != null && ArtHash(root, document.sourceLevelId) != document.sourceArtHash)
                throw new InvalidOperationException("Оточення початкового рівня змінилося. Відкрийте актуальний рівень заново.");
            var copy = LevelAuthoringDocument.Copy(document);
            BindStory(copy.level, copy.level.id, copy.sourceLevelId);
            copy.level.id = copy.art.levelId = copy.sourceLevelId;
            var errors = copy.Validate(true);
            if (errors.Count > 0) throw new InvalidDataException(string.Join("\n", errors));
            foreach (var id in ids)
            {
                var level = id == copy.level.id ? copy.level : JsonConvert.DeserializeObject<LevelData>(File.ReadAllText(LevelPath(root, id)));
                var issues = LevelContentValidator.Validate(level);
                if (issues.Count > 0 || !RuleEvaluator.Evaluate(level, level.witness).IsSolved)
                    throw new InvalidDataException("Кампанія не пройшла перевірку: " + id);
            }
            var old = JsonConvert.DeserializeObject<LevelData>(File.ReadAllText(levelPath));
            string revision = string.IsNullOrEmpty(old.contentHash) ? CampContent.CalculateHash(old) : old.contentHash;
            if (revision.Length != 64 || revision.Any(c => !Uri.IsHexDigit(c))) throw new InvalidDataException("Некоректний хеш попередньої ревізії.");
            string artPath = Path.Combine(root, "ArtCompositions", copy.level.id.Replace(':', '_') + ".json");
            var writes = new Dictionary<string, string>();
            string oldRevision = Path.Combine(root, "ContentRevisions", revision + ".json");
            if (!File.Exists(oldRevision)) writes.Add(oldRevision, File.ReadAllText(levelPath));
            string artRevision = Path.Combine(root, "ArtRevisions", revision + ".json");
            if (!File.Exists(artRevision)) writes.Add(artRevision, File.Exists(artPath) ? File.ReadAllText(artPath) : "{\"absent\":true}");
            using (var sha = SHA256.Create()) copy.level.compositionHash = BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(copy.art)))).Replace("-", "").ToLowerInvariant();
            copy.level.contentHash = CampContent.CalculateHash(copy.level);
            writes.Add(levelPath, JsonConvert.SerializeObject(copy.level, Formatting.Indented));
            writes.Add(artPath, JsonConvert.SerializeObject(copy.art));
            // Stage every file first; restore every original on a failed replacement.
            var originals = writes.Keys.ToDictionary(p => p, p => File.Exists(p) ? File.ReadAllBytes(p) : null);
            try
            {
                foreach (var pair in writes) { Directory.CreateDirectory(Path.GetDirectoryName(pair.Key)); File.WriteAllText(pair.Key + ".pending", pair.Value); }
                foreach (var path in writes.Keys) { if (File.Exists(path)) File.Replace(path + ".pending", path, null); else File.Move(path + ".pending", path); }
            }
            catch
            {
                foreach (var pair in originals) { if (pair.Value == null) { if (File.Exists(pair.Key)) File.Delete(pair.Key); } else File.WriteAllBytes(pair.Key, pair.Value); }
                throw;
            }
            finally { foreach (var path in writes.Keys) if (File.Exists(path + ".pending")) File.Delete(path + ".pending"); }
            document.sourceContentHash = HashFile(levelPath); document.sourceArtHash = ArtHash(root, document.sourceLevelId); _cards = null;
            if (Path.GetFullPath(root) == Path.GetFullPath(Content)) AssetDatabase.Refresh();
        }
        static void BindStory(LevelData level, string previous, string current)
        {
            foreach (var beat in level.environmentalStory?.beats ?? Array.Empty<EnvironmentalStoryBeat>())
                if (beat != null) beat.levelIds = (beat.levelIds ?? Array.Empty<string>()).Select(id => id == previous ? current : id).Distinct().ToArray();
        }
        static string ArtHash(string root, string id)
        {
            string path = Path.Combine(root, "ArtCompositions", id.Replace(':', '_') + ".json");
            return File.Exists(path) ? HashFile(path) : "absent";
        }
    }
}
