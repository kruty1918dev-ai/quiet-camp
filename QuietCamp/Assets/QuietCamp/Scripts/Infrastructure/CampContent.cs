using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using QuietCamp.Application;
using QuietCamp.Domain;
using UnityEngine;

namespace QuietCamp.Infrastructure
{
    public static class CampContent
    {
        public static string CanonicalId(string id)
            => id != null && id.StartsWith("GEN", StringComparison.Ordinal)
                && int.TryParse(id.Substring(3), out var n) ? "gen:qc_camp:" + n : id;
        public static LevelData Legacy(string id)
        {
            id = CanonicalId(id);
            var text = Resources.Load<TextAsset>("QuietCamp/LegacyLevels/" + id?.Replace(':', '_'));
            return text != null ? JsonConvert.DeserializeObject<LevelData>(text.text) : null;
        }
        public static LevelData Snapshot(LevelData level) => JsonConvert.DeserializeObject<LevelData>(JsonConvert.SerializeObject(level));
        public static string CalculateHash(LevelData level)
        {
            var previous=level.contentHash;
            try
            {
                level.contentHash="";
                using var sha=SHA256.Create();
                return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(level))))
                    .Replace("-","").ToLowerInvariant();
            }
            finally { level.contentHash=previous; }
        }
        public static LevelData Revision(string hash)
        {
            if (hash == null || hash.Length != 64 || hash.Any(c => !Uri.IsHexDigit(c))) return null;
            var text = Resources.Load<TextAsset>("QuietCamp/ContentRevisions/" + hash.ToLowerInvariant());
            return text != null ? JsonConvert.DeserializeObject<LevelData>(text.text) : null;
        }
        public static LevelData AlbumLevel(AlbumSaveData.Entry entry)
            => entry.levelSnapshot ?? Revision(entry.contentHash) ?? Legacy(entry.levelId) ?? LevelLoader.Load(CanonicalId(entry.levelId));
        public static string UnfinishedLevelId(SessionSaveData saved,IReadOnlyList<string> orderedIds)
        {
            var id=CanonicalId(saved?.levelId);
            return saved?.placements!=null&&saved.placements.Length>0&&orderedIds.Contains(id)?id:null;
        }
        public static LevelData SessionLevel(SessionSaveData saved, string requestedId)
        {
            requestedId=CanonicalId(requestedId);
            if(saved!=null&&CanonicalId(saved.levelId)==requestedId&&!string.IsNullOrEmpty(saved.contentHash))
            {
                var level=saved.levelSnapshot??Revision(saved.contentHash);
                if(level==null)
                {
                    var legacy=Legacy(requestedId);
                    if(legacy?.contentHash==saved.contentHash)level=legacy;
                }
                if(level!=null&&CanonicalId(level.id)==requestedId&&level.contentHash==saved.contentHash
                    &&LevelContentValidator.Validate(level).Count==0
                    &&RuleEvaluator.Evaluate(level,saved.placements??Array.Empty<Placement>(),false).CanCommit)
                {
                    // Old archives used GEN identifiers. Canonicalize only a
                    // detached copy; retain the archived hash/rules exactly.
                    level=Snapshot(level);level.id=requestedId;return level;
                }
            }
            return LevelLoader.Load(requestedId);
        }
        public static void Migrate(SaveAdapter save)
        {
            save.Progress.completedIds = (save.Progress.completedIds ?? Array.Empty<string>())
                .Select(CanonicalId).Distinct().ToArray();
            save.Progress.lastLevelId = CanonicalId(save.Progress.lastLevelId);
            save.Session.levelId = CanonicalId(save.Session.levelId);
            // This content revision changes only an already-blocked prop and
            // adds noise for a roster with no quiet guests. Its old partial
            // layout remains compatible; future revisions still need review.
            if(save.Session.levelId=="QC003" && save.Session.contentHash=="95b3640722c0c82dd9c3d051fd25cf6dada6f787cb4fb903a0d7304c9ad8ad47")
            {
                var current=LevelLoader.Load("QC003");
                if(current.contentHash=="bd14245f0ceaf0d4e43a1a7b2679bc9d51e11b7d1a55d954a51cd53349cc567e"
                    && RuleEvaluator.Evaluate(current,save.Session.placements??Array.Empty<Placement>(),false).CanCommit)
                    save.Session.contentHash=current.contentHash;
            }
            if(save.Session.levelSnapshot==null&&!string.IsNullOrEmpty(save.Session.contentHash))
            {
                var snapshot=Revision(save.Session.contentHash)??Legacy(save.Session.levelId);
                if(snapshot?.contentHash==save.Session.contentHash)save.Session.levelSnapshot=Snapshot(snapshot);
            }
            foreach (var entry in save.Album.entries ?? Array.Empty<AlbumSaveData.Entry>())
            {
                if (entry == null) continue;
                entry.levelId = CanonicalId(entry.levelId);
                if (entry.levelSnapshot == null) entry.levelSnapshot = Revision(entry.contentHash) ?? Legacy(entry.levelId);
                if (entry.levelSnapshot != null) entry.contentHash = entry.levelSnapshot.contentHash;
            }
        }
        static LevelSummary[] _summaries;
        public static IReadOnlyList<LevelSummary> Summaries
        {
            get
            {
                if (_summaries == null)
                {
                    var asset = Resources.Load<TextAsset>("QuietCamp/level_summaries");
                    _summaries = asset == null ? Array.Empty<LevelSummary>()
                        : JsonConvert.DeserializeObject<LevelSummary[]>(asset.text);
                }
                return _summaries;
            }
        }
        public static LevelSummary Summary(string id)
            => Summaries.FirstOrDefault(s => s.id == CanonicalId(id));
    }
}
