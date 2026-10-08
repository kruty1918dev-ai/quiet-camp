using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Kruty1918.LevelKit
{
    /// <summary>
    /// Schema-tolerant level JSON codec. With no profile it reads/writes the
    /// canonical LevelKit schema (layers/entities/props wrapper objects).
    /// With a LevelProfile it maps the same LevelDocument onto a project's
    /// own key layout — flat layer arrays, per-kind entity arrays, loose
    /// top-level extras. Unknown top-level fields always land in
    /// Document.Props and are written back verbatim, so a parse->write
    /// roundtrip never loses project-specific data.
    /// </summary>
    public static class LevelJson
    {
        // ─── Parse ──────────────────────────────────────────────────────────

        public static LevelResult Parse(string json, LevelProfile profile = null)
        {
            var result = new LevelResult();
            JObject root;
            try { root = JObject.Parse(json); }
            catch (Exception e)
            {
                result.Add(LevelIssueSeverity.Error, "$", "JSON parse failed: " + e.Message);
                return result;
            }
            return Parse(root, profile);
        }

        public static LevelResult Parse(JObject root, LevelProfile profile = null)
        {
            profile ??= LevelProfile.Canonical;
            var result = new LevelResult();
            var doc = new LevelDocument();
            result.Document = doc;
            var consumed = new HashSet<string>();

            doc.SchemaVersion = ReadInt(root, profile.SchemaVersionKey, 0, consumed);
            doc.Id = ReadString(root, profile.IdKey, "", consumed);
            doc.Order = ReadInt(root, profile.OrderKey, 0, consumed);
            if (profile.HasGrid)
            {
                doc.Width = ReadInt(root, profile.WidthKey, 0, consumed);
                doc.Height = ReadInt(root, profile.HeightKey, 0, consumed);
            }

            ReadLayers(root, doc, profile, consumed);
            ReadEntities(root, doc, profile, consumed);

            // Canonical extras wrapper first…
            if (!string.IsNullOrEmpty(profile.PropsKey))
            {
                consumed.Add(profile.PropsKey);
                if (root[profile.PropsKey] is JObject wrapper)
                    foreach (var p in wrapper) doc.SetProp(p.Key, p.Value);
            }
            // …then every unconsumed top-level field is preserved verbatim.
            foreach (var p in root)
            {
                if (consumed.Contains(p.Key)) continue;
                consumed.Add(p.Key);
                doc.SetProp(p.Key, p.Value);
            }

            foreach (var issue in Validate(doc, profile)) result.Issues.Add(issue);
            return result;
        }

        static int ReadInt(JObject root, string key, int fallback, HashSet<string> consumed)
        {
            if (string.IsNullOrEmpty(key)) return fallback;
            consumed.Add(key);
            var t = root[key];
            return t != null && t.Type == JTokenType.Integer ? (int)t : fallback;
        }

        static string ReadString(JObject root, string key, string fallback, HashSet<string> consumed)
        {
            if (string.IsNullOrEmpty(key)) return fallback;
            consumed.Add(key);
            var t = root[key];
            return t != null && t.Type == JTokenType.String ? (string)t : fallback;
        }

        static void ReadLayers(JObject root, LevelDocument doc, LevelProfile profile,
            HashSet<string> consumed)
        {
            // Canonical wrapper: { "layers": { "name": [[x,z],...] } }
            if (!string.IsNullOrEmpty(profile.LayersKey))
            {
                consumed.Add(profile.LayersKey);
                if (root[profile.LayersKey] is JObject wrapper)
                {
                    foreach (var p in wrapper)
                    {
                        var layer = doc.Layer(p.Key);
                        if (p.Value is JArray cells)
                            foreach (var c in cells)
                                if (CellPos.TryParse(c, out var pos)) layer.Add(pos);
                    }
                }
            }
            // Profile-mapped flat keys.
            foreach (var def in profile.Layers)
            {
                if (string.IsNullOrEmpty(def.Key)) continue;
                var token = root[def.Key];
                if (token == null) continue;
                consumed.Add(def.Key);
                var layer = doc.Layer(def.Name);
                if (!def.Multiple && CellPos.TryParse(token, out var single))
                {
                    layer.SetSingle(single);
                }
                else if (token is JArray cells)
                {
                    foreach (var c in cells)
                        if (CellPos.TryParse(c, out var pos)) layer.Add(pos);
                }
            }
        }

        static void ReadEntities(JObject root, LevelDocument doc, LevelProfile profile,
            HashSet<string> consumed)
        {
            // Profile-mapped per-kind arrays.
            foreach (var def in profile.EntityKinds)
            {
                if (string.IsNullOrEmpty(def.ArrayKey)) continue;
                var token = root[def.ArrayKey];
                if (!(token is JArray arr)) { consumed.Add(def.ArrayKey); continue; }
                consumed.Add(def.ArrayKey);
                foreach (var t in arr)
                {
                    if (!(t is JObject o)) continue;
                    doc.Entities.Add(EntityFromObject(o, def));
                }
            }
            // Canonical array: entities not claimed by a per-kind key.
            if (!string.IsNullOrEmpty(profile.EntitiesKey))
            {
                consumed.Add(profile.EntitiesKey);
                if (root[profile.EntitiesKey] is JArray arr)
                {
                    foreach (var t in arr)
                    {
                        if (!(t is JObject o)) continue;
                        var e = new LevelEntity
                        {
                            Id = (string)(o["id"] ?? ""),
                            Kind = (string)(o["kind"] ?? ""),
                            X = IntOr(o["x"], -1),
                            Z = IntOr(o["z"], -1),
                            Rotation = IntOr(o["rotation"], 0),
                        };
                        foreach (var p in o)
                            if (p.Key != "id" && p.Key != "kind" && p.Key != "x"
                                && p.Key != "z" && p.Key != "rotation" && p.Key != "data")
                                e.Data[p.Key] = p.Value;
                        if (o["data"] is JObject data)
                            foreach (var p in data) e.Data[p.Key] = p.Value;
                        doc.Entities.Add(e);
                    }
                }
            }
        }

        static LevelEntity EntityFromObject(JObject o, EntityKindDef def)
        {
            var e = new LevelEntity
            {
                Kind = def.Kind,
                Id = o[def.IdField] != null ? o[def.IdField].ToString() : "",
                X = IntOr(o["x"], -1),
                Z = IntOr(o["z"], -1),
                Rotation = IntOr(o["rotation"], 0),
            };
            foreach (var p in o)
            {
                if (p.Key == def.IdField || p.Key == "x" || p.Key == "z" || p.Key == "rotation")
                    continue;
                e.Data[p.Key] = p.Value;
            }
            return e;
        }

        static int IntOr(JToken t, int fallback)
            => t != null && t.Type == JTokenType.Integer ? (int)t : fallback;

        // ─── Validate ───────────────────────────────────────────────────────

        public static List<LevelIssue> Validate(LevelDocument doc, LevelProfile profile = null)
        {
            profile ??= LevelProfile.Canonical;
            var issues = new List<LevelIssue>();
            if (doc.SchemaVersion < 1)
                issues.Add(new LevelIssue(LevelIssueSeverity.Error,
                    profile.SchemaVersionKey, "schemaVersion must be >= 1"));
            if (string.IsNullOrEmpty(doc.Id))
                issues.Add(new LevelIssue(LevelIssueSeverity.Error,
                    profile.IdKey, "level id is required"));
            if (profile.HasGrid && !doc.HasGrid)
                issues.Add(new LevelIssue(LevelIssueSeverity.Warning, "grid",
                    $"grid size missing ({profile.WidthKey}/{profile.HeightKey})"));

            foreach (var layer in doc.Layers)
            {
                var seen = new HashSet<CellPos>();
                foreach (var c in layer.Cells)
                {
                    if (!seen.Add(c))
                        issues.Add(new LevelIssue(LevelIssueSeverity.Warning,
                            layer.Name, $"duplicate cell {c}"));
                    if (doc.HasGrid && !doc.InsideGrid(c))
                        issues.Add(new LevelIssue(LevelIssueSeverity.Error,
                            layer.Name, $"cell {c} outside grid {doc.Width}x{doc.Height}"));
                }
            }

            var ids = new HashSet<string>();
            foreach (var e in doc.Entities)
            {
                if (!string.IsNullOrEmpty(e.Id) && !ids.Add(e.Id))
                    issues.Add(new LevelIssue(LevelIssueSeverity.Warning,
                        "entities", $"duplicate entity id '{e.Id}'"));
                if (e.HasPosition && doc.HasGrid && !doc.InsideGrid(new CellPos(e.X, e.Z)))
                    issues.Add(new LevelIssue(LevelIssueSeverity.Warning,
                        "entities", $"entity '{e.Id}' outside grid"));
            }
            return issues;
        }

        // ─── Write ──────────────────────────────────────────────────────────

        /// <summary>Serializes the document. With a profile the project's
        /// own key layout is produced; unknown Props merge back at top level.</summary>
        public static string Write(LevelDocument doc, LevelProfile profile = null,
            bool indented = true)
        {
            profile ??= LevelProfile.Canonical;
            var root = new JObject();
            if (!string.IsNullOrEmpty(profile.SchemaVersionKey))
                root[profile.SchemaVersionKey] = doc.SchemaVersion;
            if (!string.IsNullOrEmpty(profile.IdKey) && !string.IsNullOrEmpty(doc.Id))
                root[profile.IdKey] = doc.Id;
            if (!string.IsNullOrEmpty(profile.OrderKey))
                root[profile.OrderKey] = doc.Order;
            if (profile.HasGrid && doc.HasGrid)
            {
                root[profile.WidthKey] = doc.Width;
                root[profile.HeightKey] = doc.Height;
            }

            var wrapper = !string.IsNullOrEmpty(profile.LayersKey) ? new JObject() : null;
            foreach (var layer in doc.Layers)
            {
                var def = FindLayer(profile, layer.Name);
                if (def != null && !string.IsNullOrEmpty(def.Key))
                {
                    root[def.Key] = def.Multiple
                        ? (JToken)CellsJson(layer)
                        : layer.Cells.Count > 0 ? layer.Cells[0].ToJson() : new JArray();
                }
                else if (wrapper != null) wrapper[layer.Name] = CellsJson(layer);
                else root[layer.Name] = CellsJson(layer);
            }
            if (wrapper != null && wrapper.Count > 0) root[profile.LayersKey] = wrapper;

            var unmapped = new List<LevelEntity>();
            foreach (var def in profile.EntityKinds)
            {
                if (string.IsNullOrEmpty(def.ArrayKey)) continue;
                var arr = new JArray();
                foreach (var e in doc.EntitiesOf(def.Kind)) arr.Add(EntityToObject(e, def));
                root[def.ArrayKey] = arr;
            }
            foreach (var e in doc.Entities)
            {
                var def = FindKind(profile, e.Kind);
                if (def == null || string.IsNullOrEmpty(def.ArrayKey)) unmapped.Add(e);
            }
            if (unmapped.Count > 0 && !string.IsNullOrEmpty(profile.EntitiesKey))
            {
                var arr = new JArray();
                foreach (var e in unmapped)
                {
                    var o = new JObject { ["id"] = e.Id, ["kind"] = e.Kind };
                    if (e.HasPosition) { o["x"] = e.X; o["z"] = e.Z; }
                    if (e.Rotation != 0) o["rotation"] = e.Rotation;
                    if (e.Data != null && e.Data.Count > 0)
                        foreach (var p in e.Data) o[p.Key] = p.Value;
                    arr.Add(o);
                }
                root[profile.EntitiesKey] = arr;
            }

            if (doc.Props != null && doc.Props.Count > 0)
            {
                if (!string.IsNullOrEmpty(profile.PropsKey)) root[profile.PropsKey] = doc.Props;
                else foreach (var p in doc.Props)
                    if (root[p.Key] == null) root[p.Key] = p.Value;
            }

            return root.ToString(indented ? Formatting.Indented : Formatting.None);
        }

        static JArray CellsJson(LevelLayer layer)
        {
            var arr = new JArray();
            foreach (var c in layer.Cells) arr.Add(c.ToJson());
            return arr;
        }

        static JObject EntityToObject(LevelEntity e, EntityKindDef def)
        {
            var o = new JObject();
            var written = new HashSet<string>();
            foreach (var field in def.WriteFields ?? Array.Empty<string>())
            {
                if (field == def.IdField) { o[field] = e.Id; }
                else if (field == "x") o[field] = e.X;
                else if (field == "z") o[field] = e.Z;
                else if (field == "rotation") o[field] = e.Rotation;
                else if (e.Data != null && e.Data[field] != null) o[field] = e.Data[field];
                written.Add(field);
            }
            if (def.WriteFields == null || def.WriteFields.Length == 0)
            {
                o[def.IdField] = e.Id;
                written.Add(def.IdField);
            }
            if (e.Data != null)
                foreach (var p in e.Data)
                    if (!written.Contains(p.Key) && p.Key != def.IdField) o[p.Key] = p.Value;
            return o;
        }

        static LayerProfileDef FindLayer(LevelProfile p, string name)
        {
            foreach (var d in p.Layers) if (d.Name == name) return d;
            return null;
        }

        static EntityKindDef FindKind(LevelProfile p, string kind)
        {
            foreach (var d in p.EntityKinds) if (d.Kind == kind) return d;
            return null;
        }
    }
}
