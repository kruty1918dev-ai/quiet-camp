using System.Collections.Generic;

namespace Kruty1918.LevelKit
{
    /// <summary>
    /// Frozen, ordered lookup over parsed level documents. Load once at
    /// startup (or per feature), then consume plain C# snapshots — the kit
    /// keeps no mutable global state and never reparses on access.
    /// Levels with parse errors are skipped and reported via Issues.
    /// </summary>
    public sealed class LevelRepository
    {
        readonly List<LevelDocument> _ordered = new List<LevelDocument>();
        readonly Dictionary<string, LevelDocument> _byId
            = new Dictionary<string, LevelDocument>();

        public readonly List<LevelIssue> Issues = new List<LevelIssue>();

        public LevelRepository(IEnumerable<LevelDocument> documents)
        {
            if (documents == null) return;
            _ordered.AddRange(documents);
            _ordered.Sort((a, b) =>
            {
                var c = a.Order.CompareTo(b.Order);
                return c != 0 ? c : string.CompareOrdinal(a.Id, b.Id);
            });
            foreach (var doc in _ordered)
            {
                if (doc == null || string.IsNullOrEmpty(doc.Id)) continue;
                if (_byId.ContainsKey(doc.Id))
                {
                    Issues.Add(new LevelIssue(LevelIssueSeverity.Warning,
                        doc.Id, "duplicate level id — first occurrence kept"));
                    continue;
                }
                _byId.Add(doc.Id, doc);
            }
        }

        /// <summary>Parses every entry of a source; failed files are skipped.</summary>
        public static LevelRepository FromSource(ILevelSource source,
            LevelProfile profile = null)
        {
            var docs = new List<LevelDocument>();
            var issues = new List<LevelIssue>();
            foreach (var kv in source.ReadAll())
            {
                var result = LevelJson.Parse(kv.Value, profile);
                foreach (var i in result.Issues)
                    issues.Add(new LevelIssue(i.Severity,
                        kv.Key + (string.IsNullOrEmpty(i.Path) ? "" : ":" + i.Path),
                        i.Message));
                if (result.Ok) docs.Add(result.Document);
            }
            var repo = new LevelRepository(docs);
            repo.Issues.AddRange(issues);
            return repo;
        }

        public int Count => _ordered.Count;

        /// <summary>All levels sorted by order, then id.</summary>
        public IReadOnlyList<LevelDocument> All => _ordered;

        public bool TryGet(string id, out LevelDocument doc)
            => _byId.TryGetValue(id ?? "", out doc);

        public LevelDocument Get(string id)
        {
            if (TryGet(id, out var doc)) return doc;
            throw new KeyNotFoundException($"Level '{id}' not found in repository.");
        }
    }
}
