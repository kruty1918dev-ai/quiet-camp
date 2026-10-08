using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Kruty1918.LevelKit;

namespace Kruty1918.LevelKit.Tests
{
    /// <summary>Codec coverage: canonical schema, profile mapping, tolerance.</summary>
    public class LevelJsonTests
    {
        const string CanonicalJson = @"{
            ""schemaVersion"": 1,
            ""id"": ""lv_001"",
            ""order"": 2,
            ""width"": 4,
            ""height"": 3,
            ""layers"": {
                ""blocked"": [[0,0],[1,0]],
                ""entry"": [[3,2]]
            },
            ""entities"": [
                {""id"":""e1"",""kind"":""spawn"",""x"":1,""z"":2,""rotation"":3,""team"":""a""}
            ],
            ""props"": {""difficulty"":""easy"",""music"":null}
        }";

        [Test]
        public void Parse_Canonical_AllConcepts()
        {
            var r = LevelJson.Parse(CanonicalJson);
            Assert.IsTrue(r.Ok, string.Join(";", r.Issues.Select(i => i.ToString())));
            var d = r.Document;
            Assert.AreEqual("lv_001", d.Id);
            Assert.AreEqual(2, d.Order);
            Assert.AreEqual(4, d.Width);
            Assert.AreEqual(3, d.Height);
            Assert.AreEqual(2, d.Layer("blocked").Cells.Count);
            Assert.AreEqual(new CellPos(3, 2), d.Layer("entry").Cells[0]);
            var e = d.Entities[0];
            Assert.AreEqual("spawn", e.Kind);
            Assert.AreEqual(3, e.Rotation);
            Assert.AreEqual("a", e.Get<string>("team"));
            Assert.AreEqual("easy", d.Prop<string>("difficulty"));
        }

        [Test]
        public void RoundTrip_Canonical_PreservesEverything()
        {
            var d1 = LevelJson.Parse(CanonicalJson).Document;
            var json2 = LevelJson.Write(d1);
            var d2 = LevelJson.Parse(json2).Document;
            Assert.AreEqual(d1.Id, d2.Id);
            Assert.AreEqual(d1.Layer("blocked").Cells.Count, d2.Layer("blocked").Cells.Count);
            Assert.AreEqual(1, d2.Entities.Count);
            Assert.AreEqual("a", d2.Entities[0].Get<string>("team"));
            Assert.AreEqual("easy", d2.Prop<string>("difficulty"));
            Assert.IsTrue(d2.Props["music"].Type == JTokenType.Null);
        }

        const string QcProfileJson = @"{
            ""gameId"": ""quiet-camp"",
            ""layersKey"": """",
            ""entitiesKey"": """",
            ""propsKey"": """",
            ""layers"": [
                {""name"":""entry"",""key"":""entry"",""multiple"":false,""color"":""#e3c04a""},
                {""name"":""blocked"",""key"":""blocked"",""multiple"":true,""color"":""#8899aa""},
                {""name"":""shade"",""key"":""shade"",""multiple"":true,""color"":""#4a6b57""},
                {""name"":""noise"",""key"":""noise"",""multiple"":true,""color"":""#c98f4a""}
            ],
            ""entityKinds"": [
                {""kind"":""guest"",""arrayKey"":""guests"",""idField"":""id"",
                 ""writeFields"":[""id"",""nameKey"",""assetId"",""shade"",""quiet""]},
                {""kind"":""witness"",""arrayKey"":""witness"",""idField"":""guestId"",
                 ""writeFields"":[""guestId"",""x"",""z"",""rotation""]}
            ]
        }";

        const string QcLevelJson = @"{
            ""schemaVersion"": 1,
            ""ruleVersion"": 1,
            ""id"": ""QC_TEST"",
            ""order"": 0,
            ""chapter"": 1,
            ""width"": 6,
            ""height"": 6,
            ""entry"": [5,5],
            ""blocked"": [[5,4]],
            ""shade"": [[0,0],[0,1],[1,0]],
            ""noise"": [[5,4]],
            ""guests"": [
                {""id"":""g1"",""nameKey"":""guest.1"",""assetId"":""tent_smallOpen"",""shade"":true,""quiet"":false}
            ],
            ""friends"": [[""g1"",""g2""]],
            ""witness"": [{""guestId"":""g1"",""x"":0,""z"":0,""rotation"":0}],
            ""lighting"": ""day"",
            ""decorSeed"": 1,
            ""contentHash"": ""abc123""
        }";

        static LevelProfile QcProfile => LevelProfile.FromJson(QcProfileJson);

        [Test]
        public void Parse_FlatProfile_MapsQcFormat()
        {
            var r = LevelJson.Parse(QcLevelJson, QcProfile);
            Assert.IsTrue(r.Ok, string.Join(";", r.Issues.Select(i => i.ToString())));
            var d = r.Document;
            Assert.AreEqual("QC_TEST", d.Id);
            Assert.AreEqual(6, d.Width);
            Assert.AreEqual(new CellPos(5, 5), d.Layer("entry").Cells[0]);
            Assert.AreEqual(3, d.Layer("shade").Cells.Count);
            var guest = d.EntitiesOf("guest").Single();
            Assert.AreEqual("g1", guest.Id);
            Assert.IsFalse(guest.HasPosition);
            Assert.AreEqual("tent_smallOpen", guest.Get<string>("assetId"));
            Assert.IsTrue(guest.Get<bool>("shade"));
            var w = d.EntitiesOf("witness").Single();
            Assert.AreEqual("g1", w.Id);
            Assert.AreEqual(new CellPos(w.X, w.Z), new CellPos(0, 0));
            // Unknown top-level fields preserved verbatim in Props.
            Assert.AreEqual("day", d.Prop<string>("lighting"));
            Assert.AreEqual(1, d.Prop<int>("decorSeed"));
            Assert.AreEqual("abc123", d.Prop<string>("contentHash"));
            var friends = d.Prop<JArray>("friends");
            Assert.IsNotNull(friends);
            Assert.AreEqual("g2", (string)friends[0][1]);
        }

        [Test]
        public void Write_FlatProfile_ProducesQcKeys()
        {
            var d = LevelJson.Parse(QcLevelJson, QcProfile).Document;
            var written = JObject.Parse(LevelJson.Write(d, QcProfile));
            Assert.IsNotNull(written["entry"]);
            Assert.AreEqual(5, (int)written["entry"][0]);
            Assert.IsNotNull(written["blocked"]);
            Assert.IsNotNull(written["guests"]);
            Assert.AreEqual("g1", (string)written["guests"][0]["id"]);
            Assert.AreEqual("guest.1", (string)written["guests"][0]["nameKey"]);
            Assert.IsNotNull(written["witness"]);
            Assert.AreEqual("g1", (string)written["witness"][0]["guestId"]);
            Assert.AreEqual("day", (string)written["lighting"]);
            Assert.IsNotNull(written["friends"]);
            Assert.IsNull(written["layers"], "QC format must not wrap layers");
            Assert.IsNull(written["entities"]);
            Assert.IsNull(written["props"]);
        }

        [Test]
        public void RoundTrip_FlatProfile_Lossless()
        {
            var p = QcProfile;
            var d1 = LevelJson.Parse(QcLevelJson, p).Document;
            var d2 = LevelJson.Parse(LevelJson.Write(d1, p), p).Document;
            Assert.AreEqual(d1.Layer("noise").Cells.Count, d2.Layer("noise").Cells.Count);
            Assert.AreEqual(d1.Entities.Count, d2.Entities.Count);
            Assert.AreEqual(d1.Prop<string>("contentHash"), d2.Prop<string>("contentHash"));
            Assert.AreEqual(
                d1.Prop<JArray>("friends").ToString(Newtonsoft.Json.Formatting.None),
                d2.Prop<JArray>("friends").ToString(Newtonsoft.Json.Formatting.None));
        }

        [Test]
        public void Parse_InvalidJson_ReportsError()
        {
            var r = LevelJson.Parse("{ this is not json ");
            Assert.IsFalse(r.Ok);
            Assert.IsTrue(r.Issues.Any(i => i.Severity == LevelIssueSeverity.Error));
        }

        [Test]
        public void Validate_CatchesOutOfBoundsAndMissingId()
        {
            var r = LevelJson.Parse(@"{
                ""schemaVersion"":1,""id"":"""",""width"":3,""height"":3,
                ""layers"":{""blocked"":[[9,9],[9,9]]}
            }");
            Assert.IsFalse(r.Ok);
            Assert.IsTrue(r.Issues.Any(i => i.Severity == LevelIssueSeverity.Error
                && i.Message.Contains("outside grid")));
        }

        [Test]
        public void Repository_FromSource_SortsAndFinds()
        {
            var src = new InlineLevelSource()
                .Add("b", @"{""schemaVersion"":1,""id"":""B"",""order"":2}")
                .Add("a", @"{""schemaVersion"":1,""id"":""A"",""order"":1}")
                .Add("bad", "{broken");
            var repo = LevelRepository.FromSource(src);
            Assert.AreEqual(2, repo.Count);
            Assert.AreEqual("A", repo.All[0].Id);
            Assert.AreEqual("B", repo.All[1].Id);
            Assert.IsTrue(repo.TryGet("B", out var b));
            Assert.IsTrue(repo.Issues.Any(i => i.Severity == LevelIssueSeverity.Error));
        }

        [Test]
        public void Profile_FromJson_RoundTripsDefs()
        {
            var p = QcProfile;
            Assert.AreEqual("quiet-camp", p.GameId);
            Assert.AreEqual(4, p.Layers.Length);
            Assert.IsFalse(p.Layers[0].Multiple);
            Assert.AreEqual(2, p.EntityKinds.Length);
            Assert.AreEqual("guests", p.EntityKinds[0].ArrayKey);
            Assert.IsTrue(p.HasGrid);
        }
    }
}
