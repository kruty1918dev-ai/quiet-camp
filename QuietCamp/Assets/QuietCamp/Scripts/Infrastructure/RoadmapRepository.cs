using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using QuietCamp.Application;
using QuietCamp.Domain;
using UnityEngine;

namespace QuietCamp.Infrastructure
{
    public static class RoadmapRepository
    {
        static readonly Dictionary<string,RoadmapCatalog> Maps=new Dictionary<string,RoadmapCatalog>(StringComparer.Ordinal);
        static Dictionary<string,LevelSummary> _summaries;
#if UNITY_EDITOR
        public static RoadmapCatalog EditorMainPreview;
#endif
        public static RoadmapCatalog Main=>ForJourney("main");
        public static RoadmapCatalog ForJourney(string journeyId)
        {
            journeyId=string.IsNullOrEmpty(journeyId)?"main":journeyId;
#if UNITY_EDITOR
            if(journeyId=="main"&&EditorMainPreview!=null)return EditorMainPreview;
#endif
            if(Maps.TryGetValue(journeyId,out var ready))return ready;
            // Author IDs must be simple filenames; no path traversal or arbitrary remote content.
            foreach(char c in journeyId)if(!char.IsLetterOrDigit(c)&&c!='-'&&c!='_'&&c!='.')throw new ArgumentException("Invalid journey ID");
            var asset=Resources.Load<TextAsset>(journeyId=="main"?"QuietCamp/roadmap_regions":"QuietCamp/Roadmaps/"+journeyId);
            if(asset==null)throw new InvalidOperationException("Roadmap export is missing: "+journeyId);
            var catalog=new RoadmapCatalog(JsonConvert.DeserializeObject<RoadmapDefinition>(asset.text));
            EnsureSummaries();
            foreach(var node in catalog.Nodes)if(!_summaries.ContainsKey(node.levelId))throw new InvalidOperationException("Missing roadmap summary: "+node.levelId);
            Maps.Add(journeyId,catalog);return catalog; // Commit only validated static data.
        }
        static void EnsureSummaries()
        {
            if(_summaries!=null)return;
            var staged=new Dictionary<string,LevelSummary>(StringComparer.Ordinal);
            foreach(var s in CampContent.Summaries)staged.Add(s.id,s);
            var extras=Resources.Load<TextAsset>("QuietCamp/journey_summaries");
            if(extras!=null)foreach(var s in JsonConvert.DeserializeObject<LevelSummary[]>(extras.text))staged.Add(s.id,s);
            _summaries=staged;
        }
        public static LevelSummary Summary(string id){EnsureSummaries();return _summaries.TryGetValue(id,out var summary)?summary:null;}
        public static void Reset(){Maps.Clear();_summaries=null;}
    }
}
