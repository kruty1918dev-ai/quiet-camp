using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using QuietCamp.Domain;
using UnityEngine;

namespace QuietCamp.Infrastructure
{
    public static class BonusCampCatalog
    {
        static BonusCampDefinition[] _slots;
        public static IReadOnlyList<BonusCampDefinition> Slots => _slots??(_slots=Load());
        static BonusCampDefinition[] Load()
        {
            var asset=Resources.Load<TextAsset>("QuietCamp/bonus_camps");
            return asset==null?Array.Empty<BonusCampDefinition>():JsonConvert.DeserializeObject<BonusCampDefinition[]>(asset.text)??Array.Empty<BonusCampDefinition>();
        }
        // Publication is explicit: an empty id cannot accidentally launch an unrelated campaign level.
        public static bool IsPublished(BonusCampDefinition slot)
            => slot!=null&&!string.IsNullOrEmpty(slot.levelId)&&Resources.Load<TextAsset>(LevelLoader.LevelsFolder+"/"+slot.levelId)!=null;
        public static BonusCampDefinition ForLevel(string id)
        {
            if(string.IsNullOrEmpty(id))return null;
            foreach(var slot in Slots)if(slot.levelId==id)return slot;
            return null;
        }
    }
}
