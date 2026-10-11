using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using QuietCamp.Application;
using QuietCamp.Domain;
using QuietCamp.Infrastructure;

namespace QuietCamp.Tests
{
    // Frozen 110-place roadmap data is tested independently of the active campaign.
    // These tests never publish it or grant access to archived content.
    internal static class ArchivedRoadmapFixture
    {
        static JObject Source => JObject.Parse(File.ReadAllText("Assets/QuietCamp/Tests/Editor/Fixtures/archived-world-map.json"));
        internal static string[] Ids => Source["summaries"].ToObject<LevelSummary[]>().Take(110).Select(s=>s.id).ToArray();
        internal static WorldMapData Build()
        {
            var source=Source;
            var summaries=source["summaries"].ToObject<LevelSummary[]>();
            var byId=summaries.ToDictionary(s=>s.id);
            var ids=summaries.Take(110).Select(s=>s.id).ToArray();
            return WorldMapBuilder.Build(ids,source["campaign"]["districts"].ToObject<DistrictDefinition[]>(),
                source["bonus"].ToObject<BonusCampDefinition[]>(),source["journeys"].ToObject<JourneyDefinition[]>(),
                id=>byId.TryGetValue(id,out var summary)?summary:null);
        }
    }
}
