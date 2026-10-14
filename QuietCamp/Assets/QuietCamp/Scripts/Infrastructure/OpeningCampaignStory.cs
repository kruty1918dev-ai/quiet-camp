using System;
using System.Linq;
using Newtonsoft.Json;
using QuietCamp.Domain;
using UnityEngine;

namespace QuietCamp.Infrastructure
{
    // Optional narrative. Rules, navigation and save authority remain in their existing services.
    public static class OpeningCampaignStory
    {
        [Serializable] public sealed class Entry
        {
            public string id,title,arrival,objective,finding,after,nextId,canonicalBefore,canonicalBeat,mechanic,weather,proposedAdditions;
        }
        sealed class Catalog { public int schemaVersion; public Entry[] entries; }
        static Entry[] _entries;
        public static Entry[] Read(string json)
        {
            var c=JsonConvert.DeserializeObject<Catalog>(json);
            if(c?.schemaVersion!=1 || c.entries==null || (c.entries.Length!=10&&c.entries.Length!=100) || c.entries.Any(e=>e==null||string.IsNullOrWhiteSpace(e.title)||string.IsNullOrWhiteSpace(e.finding)||string.IsNullOrWhiteSpace(e.objective))
                || !c.entries.Select(e=>e.id).SequenceEqual(c.entries.Length==10?Enumerable.Range(1,10).Select(i=>"QC"+i.ToString("000")):LevelLoader.CampaignLevelIds()))
                throw new InvalidOperationException("Invalid opening campaign narrative.");
            return c.entries;
        }
        public static Entry For(LevelData level)
        {
            if(level?.environmentalStory?.beats==null || !level.environmentalStory.beats.Any(b=>b.id=="opening-"+level.order.ToString("000")))return null;
            if(_entries==null)_entries=Read(Resources.Load<TextAsset>("QuietCamp/opening_campaign_story").text);
            // Authoring previews have temporary IDs; historical levels have no opening beat marker.
            return level.order>0&&level.order<=_entries.Length?_entries[level.order-1]:null;
        }
    }
}
