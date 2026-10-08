using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Kruty1918.LevelKit
{
    /// <summary>Declares one named cell layer and where it lives in the file.</summary>
    [Serializable]
    public sealed class LayerProfileDef
    {
        /// <summary>Semantic name used by the game ("blocked", "shade", ...).</summary>
        public string Name;
        /// <summary>Top-level JSON key. Null/empty = the layer lives inside LayersKey.</summary>
        public string Key;
        /// <summary>False = a single [x,z] pair written flat, true = array of pairs.</summary>
        public bool Multiple = true;
        /// <summary>Designer palette color hint, "#RRGGBB" or "#RRGGBBAA".</summary>
        public string Color = "#88cc88";
    }

    /// <summary>Declares one entity kind and where its array lives in the file.</summary>
    [Serializable]
    public sealed class EntityKindDef
    {
        /// <summary>Semantic kind ("guest", "spawn", "witness", ...).</summary>
        public string Kind;
        /// <summary>Top-level JSON key of the object array. Null/empty = EntitiesKey.</summary>
        public string ArrayKey;
        /// <summary>Field carrying the entity id ("id", "guestId", ...).</summary>
        public string IdField = "id";
        /// <summary>
        /// Fields written for every entity of this kind, in order. "x"/"z"/
        /// "rotation" map to typed placement values, everything else is
        /// pulled from entity Data.
        /// </summary>
        public string[] WriteFields = Array.Empty<string>();
        /// <summary>Designer accent color hint.</summary>
        public string Color = "#e3a04a";
    }

    /// <summary>
    /// A reusable description of where the generic level concepts live in a
    /// project's JSON files. One profile per game/format; levels written by
    /// the designer and parsed at runtime share it, so the same kit works
    /// for any casual title. Without a profile the canonical LevelKit
    /// schema is assumed (layers/entities/props wrapper objects).
    /// Profiles are plain JSON (`*.levelprofile.json`) so they travel with
    /// the project, not with the kit.
    /// </summary>
    [Serializable]
    public sealed class LevelProfile
    {
        /// <summary>Free-form name shown in tooling ("Quiet Camp", ...).</summary>
        public string GameId = "";
        public string IdKey = "id";
        public string OrderKey = "order";
        public string SchemaVersionKey = "schemaVersion";
        /// <summary>JSON keys for grid dims; empty string disables the grid.</summary>
        public string WidthKey = "width";
        public string HeightKey = "height";
        /// <summary>Key of the layers wrapper object; "" = flat per-layer keys.</summary>
        public string LayersKey = "layers";
        /// <summary>Key of the canonical entity array; "" = none.</summary>
        public string EntitiesKey = "entities";
        /// <summary>Key of the extras wrapper object; "" = unknown top-level keys.</summary>
        public string PropsKey = "props";

        public LayerProfileDef[] Layers = Array.Empty<LayerProfileDef>();
        public EntityKindDef[] EntityKinds = Array.Empty<EntityKindDef>();

        /// <summary>The built-in canonical schema profile.</summary>
        public static LevelProfile Canonical => new LevelProfile { GameId = "canonical" };

        public static LevelProfile FromJson(string json)
        {
            var obj = JObject.Parse(json);
            var p = new LevelProfile();
            string S(string key, string fallback)
                => obj[key] != null && obj[key].Type == JTokenType.String ? (string)obj[key] : fallback;
            p.GameId = S("gameId", "");
            p.IdKey = S("idKey", p.IdKey);
            p.OrderKey = S("orderKey", p.OrderKey);
            p.SchemaVersionKey = S("schemaVersionKey", p.SchemaVersionKey);
            p.WidthKey = S("widthKey", p.WidthKey);
            p.HeightKey = S("heightKey", p.HeightKey);
            p.LayersKey = S("layersKey", p.LayersKey);
            p.EntitiesKey = S("entitiesKey", p.EntitiesKey);
            p.PropsKey = S("propsKey", p.PropsKey);

            var layers = new List<LayerProfileDef>();
            if (obj["layers"] is JArray la)
            {
                foreach (var t in la)
                {
                    if (!(t is JObject lo)) continue;
                    layers.Add(new LayerProfileDef
                    {
                        Name = (string)(lo["name"] ?? ""),
                        Key = (string)lo["key"],
                        Multiple = lo["multiple"] == null || (bool)lo["multiple"],
                        Color = (string)(lo["color"] ?? "#88cc88"),
                    });
                }
            }
            p.Layers = layers.ToArray();

            var kinds = new List<EntityKindDef>();
            if (obj["entityKinds"] is JArray ka)
            {
                foreach (var t in ka)
                {
                    if (!(t is JObject ko)) continue;
                    var fields = new List<string>();
                    if (ko["writeFields"] is JArray wf)
                        foreach (var f in wf) fields.Add((string)f);
                    kinds.Add(new EntityKindDef
                    {
                        Kind = (string)(ko["kind"] ?? ""),
                        ArrayKey = (string)ko["arrayKey"],
                        IdField = (string)(ko["idField"] ?? "id"),
                        WriteFields = fields.ToArray(),
                        Color = (string)(ko["color"] ?? "#e3a04a"),
                    });
                }
            }
            p.EntityKinds = kinds.ToArray();
            return p;
        }

        public bool HasGrid => !string.IsNullOrEmpty(WidthKey) && !string.IsNullOrEmpty(HeightKey);
    }
}
