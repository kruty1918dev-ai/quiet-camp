using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using QuietCamp.Application;
using UnityEngine;

namespace QuietCamp.Infrastructure
{
    [Serializable] public sealed class MonetizationConfiguration
    {
        public EconomyRules economy = new EconomyRules();
        public PurchaseProduct[] products = Array.Empty<PurchaseProduct>();
        public JourneyDefinition[] journeys = Array.Empty<JourneyDefinition>();
        public static MonetizationConfiguration Load()
        {
            var asset = Resources.Load<TextAsset>("QuietCamp/monetization");
            try { return asset == null ? new MonetizationConfiguration() : JsonConvert.DeserializeObject<MonetizationConfiguration>(asset.text) ?? new MonetizationConfiguration(); }
            catch { Debug.LogWarning("[QuietCamp] Monetization catalog unavailable; purchases remain disabled."); return new MonetizationConfiguration(); }
        }
        public JourneyCatalog Catalog()
        {
            var main = new List<string>(LevelLoader.MvpLevelIds());
            var definitions = new List<JourneyDefinition>
            {
                new JourneyDefinition { id = "main", titleKey = "journey.main.title", descriptionKey = "journey.main.description", levelIds = main.ToArray(), published = true }
            };
#if UNITY_EDITOR
            var test = LevelLoader.TestLevelId();
            if (!string.IsNullOrEmpty(test) && !main.Contains(test)) definitions.Add(new JourneyDefinition
            { id = "qa", titleKey = "level.test", descriptionKey = "level.test", levelIds = new[] { test }, published = true });
#endif
            foreach (var bonus in BonusCampCatalog.Slots)
                if (!string.IsNullOrEmpty(bonus.levelId)) definitions.Add(new JourneyDefinition
                {
                    id = "bonus." + bonus.id, titleKey = bonus.titleKey, descriptionKey = bonus.descriptionKey,
                    levelIds = new[] { bonus.levelId }, published = BonusCampCatalog.IsPublished(bonus)
                });
            foreach (var journey in journeys ?? Array.Empty<JourneyDefinition>())
            {
                if (journey == null) continue;
                foreach (var id in journey.levelIds ?? Array.Empty<string>())
                    if (Resources.Load<TextAsset>(LevelLoader.LevelsFolder + "/" + id) == null) journey.published = false;
                definitions.Add(journey);
            }
            if (JourneyCatalog.Validate(definitions).Count > 0)
            { Debug.LogWarning("[QuietCamp] Invalid journey catalog; optional journeys are disabled."); return new JourneyCatalog(new[] { definitions[0] }); }
            return new JourneyCatalog(definitions);
        }
    }
}
