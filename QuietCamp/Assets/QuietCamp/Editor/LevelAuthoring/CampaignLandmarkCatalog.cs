using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using QuietCamp.Infrastructure;

namespace QuietCamp.Editor.LevelAuthoring
{
    // Whole objects are reserved once. Shared timber, roads and background props are allowed.
    public static class CampaignLandmarkCatalog
    {
        public const string Folder="Assets/QuietCamp/Authoring/CampaignLandmarks";
        [Serializable] public sealed class Entry
        {
            public int order;
            public string modelId,label,construction,geometryHash;
        }
        sealed class Catalog { public int version; public Entry[] entries; }
        sealed class Library { public CampArtDefinition.Model[] models; }
        public static Entry[] Read()
        {
            var c=JsonConvert.DeserializeObject<Catalog>(File.ReadAllText(Folder+"/catalog.json"));
            if(c?.version!=1 || c.entries?.Length!=100
                || !c.entries.Select(e=>e.order).SequenceEqual(Enumerable.Range(1,100))
                || c.entries.Any(e=>string.IsNullOrWhiteSpace(e.modelId)||string.IsNullOrWhiteSpace(e.label))
                || c.entries.Select(e=>e.modelId).Distinct(StringComparer.Ordinal).Count()!=100
                || c.entries.Where(e=>e.geometryHash!=null).GroupBy(e=>e.geometryHash).Any(g=>g.Count()>1))
                throw new InvalidDataException("Campaign needs 100 different primary landmarks, in campaign order.");
            return c.entries;
        }
        public static CampArtDefinition.Model[] Models() => JsonConvert.DeserializeObject<Library>(File.ReadAllText(Folder+"/models.json")).models;
        public static void Validate(CampArtDefinition art,int order)
        {
            var entry=Read()[order-1];
            if(art.primaryLandmarkId!=entry.modelId || string.IsNullOrWhiteSpace(art.primaryLandmarkInstanceId)
                || !art.instances.Any(i=>i.id==art.primaryLandmarkInstanceId))
                throw new InvalidDataException("Missing assigned primary landmark: "+entry.label);
            var pose=art.instances.Single(i=>i.id==art.primaryLandmarkInstanceId);
            var model=art.models.Single(m=>m.id==pose.modelId);
            if((model.sourceModelId??model.id)!=entry.modelId)
                throw new InvalidDataException("Primary landmark geometry does not match its reservation: "+entry.label);
        }
    }
}
