using System.Collections.Generic;
using Kruty1918.LevelKit;
using Newtonsoft.Json.Linq;
using QuietCamp.Domain;
using UnityEngine;
namespace QuietCamp.Infrastructure
{
    /// <summary>
    /// Bridges the universal LevelKit document model and QuietCamp's typed
    /// LevelData. The LevelKit profile (Resources/QuietCamp/levelkit_profile)
    /// decides which JSON keys become layers/entities/props; this adapter
    /// assigns the game-specific meaning: entry/blocked/shade/noise cells,
    /// guest and witness entities, and the remaining top-level fields
    /// (friends, lighting, tutorialKey, contentHash, …).
    /// </summary>
    public static class QuietCampLevelAdapter
    {
        static LevelProfile _profile;

        /// <summary>The Quiet Camp format profile shipped in Resources.</summary>
        public static LevelProfile Profile
        {
            get
            {
                if (_profile != null) return _profile;
                var asset = Resources.Load<TextAsset>("QuietCamp/levelkit_profile");
                if (asset == null)
                {
                    Debug.LogError("[QuietCamp] levelkit_profile.json missing — " +
                        "levels will not parse.");
                    _profile = LevelProfile.Canonical;
                }
                else _profile = LevelProfile.FromJson(asset.text);
                return _profile;
            }
        }

        /// <summary>Universal document -> typed game data.</summary>
        public static LevelData ToLevelData(LevelDocument doc)
        {
            var level = new LevelData
            {
                schemaVersion = doc.SchemaVersion,
                id = doc.Id,
                order = doc.Order,
                width = doc.Width,
                height = doc.Height,
            };
            var entry = doc.LayerOrNull("entry");
            if (entry != null && entry.Cells.Count > 0)
                level.entry = new[] { entry.Cells[0].X, entry.Cells[0].Z };
            level.blocked = Cells("blocked");
            level.shade = Cells("shade");
            level.noise = Cells("noise");

            var guests = new List<GuestData>();
            foreach (var e in doc.EntitiesOf("guest"))
            {
                guests.Add(new GuestData
                {
                    id = e.Id,
                    nameKey = e.Get<string>("nameKey"),
                    assetId = e.Get<string>("assetId"),
                    shade = e.Get<bool>("shade"),
                    quiet = e.Get<bool>("quiet"),
                });
            }
            level.guests = guests.ToArray();

            var witness = new List<Placement>();
            foreach (var e in doc.EntitiesOf("witness"))
            {
                witness.Add(new Placement
                {
                    guestId = e.Id, x = e.X, z = e.Z, rotation = e.Rotation,
                });
            }
            level.witness = witness.ToArray();

            // Verbatim extras — the LevelKit profile keeps every unknown
            // top-level key in Props.
            level.ruleVersion = doc.Prop("ruleVersion", 0);
            level.chapter = doc.Prop("chapter", 0);
            level.seed = doc.Prop("seed", 0);
            level.generatorVersion = doc.Prop<string>("generatorVersion");
            level.generationAttempt = doc.Prop("generationAttempt", 0);
            level.lighting = doc.Prop<string>("lighting");
            level.tutorialKey = doc.Prop<string>("tutorialKey");
            level.decorSeed = doc.Prop("decorSeed", 0);
            level.contentHash = doc.Prop<string>("contentHash");
            level.friends = Friends(doc.Prop<JArray>("friends"));
            return level;

            int[][] Cells(string name)
            {
                var layer = doc.LayerOrNull(name);
                if (layer == null) return null;
                var arr = new int[layer.Cells.Count][];
                for (var i = 0; i < layer.Cells.Count; i++)
                    arr[i] = new[] { layer.Cells[i].X, layer.Cells[i].Z };
                return arr;
            }
        }

        /// <summary>Typed game data -> universal document (authoring path).</summary>
        public static LevelDocument ToDocument(LevelData level)
        {
            var doc = new LevelDocument
            {
                Id = level.id,
                Order = level.order,
                SchemaVersion = level.schemaVersion,
                Width = level.width,
                Height = level.height,
            };
            if (level.entry != null && level.entry.Length >= 2)
                doc.Layer("entry").SetSingle(new CellPos(level.entry[0], level.entry[1]));
            Fill(doc.Layer("blocked"), level.blocked);
            Fill(doc.Layer("shade"), level.shade);
            Fill(doc.Layer("noise"), level.noise);

            if (level.guests != null)
            {
                foreach (var g in level.guests)
                {
                    var e = new LevelEntity { Id = g.id, Kind = "guest" };
                    e.Set("nameKey", g.nameKey);
                    e.Set("assetId", g.assetId);
                    e.Set("shade", g.shade);
                    e.Set("quiet", g.quiet);
                    doc.Entities.Add(e);
                }
            }
            if (level.witness != null)
            {
                foreach (var w in level.witness)
                {
                    doc.Entities.Add(new LevelEntity
                    {
                        Id = w.guestId, Kind = "witness",
                        X = w.x, Z = w.z, Rotation = w.rotation,
                    });
                }
            }

            doc.SetProp("ruleVersion", level.ruleVersion);
            doc.SetProp("chapter", level.chapter);
            doc.SetProp("seed", level.seed);
            if (level.generatorVersion != null) doc.SetProp("generatorVersion", level.generatorVersion);
            doc.SetProp("generationAttempt", level.generationAttempt);
            if (level.lighting != null) doc.SetProp("lighting", level.lighting);
            if (level.tutorialKey != null) doc.SetProp("tutorialKey", level.tutorialKey);
            doc.SetProp("decorSeed", level.decorSeed);
            if (level.contentHash != null) doc.SetProp("contentHash", level.contentHash);
            if (level.friends != null)
            {
                var arr = new JArray();
                foreach (var pair in level.friends) arr.Add(new JArray(pair));
                doc.SetProp("friends", arr);
            }
            return doc;

            void Fill(LevelLayer layer, int[][] cells)
            {
                if (cells == null) return;
                foreach (var c in cells)
                    if (c != null && c.Length >= 2) layer.Add(new CellPos(c[0], c[1]));
            }
        }

        static string[][] Friends(JArray arr)
        {
            if (arr == null) return null;
            var list = new List<string[]>();
            foreach (var t in arr)
            {
                if (!(t is JArray pair) || pair.Count < 2) continue;
                list.Add(new[] { (string)pair[0], (string)pair[1] });
            }
            return list.ToArray();
        }
    }
}
