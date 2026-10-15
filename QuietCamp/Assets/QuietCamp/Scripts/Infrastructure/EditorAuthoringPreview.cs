#if UNITY_EDITOR
using System;
using System.IO;
using Newtonsoft.Json;
using QuietCamp.Application;
using QuietCamp.Domain;
using UnityEngine;

namespace QuietCamp.Infrastructure
{
    // Only explicit authoring requests activate this boundary. Never included in a player.
    public static class EditorAuthoringPreview
    {
        [Serializable] public sealed class Request { public LevelData level; public CampArtDefinition art; public bool campaign; public string campaignArtDirectory; }
        public static string RequestPath => Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "../Temp/ai/level-authoring-preview.json"));
        static Request _request;
        const string EnabledKey = "QuietCamp.Authoring.PreviewEnabled";
        public static Request Current => _request ?? (_request = File.Exists(RequestPath) ? JsonConvert.DeserializeObject<Request>(File.ReadAllText(RequestPath)) : null);
        public static bool Active => UnityEditor.SessionState.GetBool(EnabledKey, false) && Current?.level != null && Current.art != null;
        public static void Enable(bool enabled) => UnityEditor.SessionState.SetBool(EnabledKey, enabled);
        public static bool CampaignPreview => Active && Current.campaign;
        public static bool Matches(string id) => Active && !Current.campaign && Current.level.id == id;
        public static bool MatchesArt(string id) => Matches(id) || Active && Current.campaign
            && !string.IsNullOrEmpty(Current.campaignArtDirectory)
            && File.Exists(Path.Combine(Current.campaignArtDirectory, id.Replace(':', '_') + ".json"));
        public static LevelData Level(string id) => Matches(id) ? JsonConvert.DeserializeObject<LevelData>(JsonConvert.SerializeObject(Current.level)) : null;
        public static CampArtDefinition Art(string id)
        {
            if (Matches(id)) return JsonConvert.DeserializeObject<CampArtDefinition>(JsonConvert.SerializeObject(Current.art));
            if (!MatchesArt(id)) return null;
            return JsonConvert.DeserializeObject<CampArtDefinition>(File.ReadAllText(Path.Combine(Current.campaignArtDirectory, id.Replace(':', '_') + ".json")));
        }
        public static void Reset() => _request = null;
        public static SaveAdapter CreateSave()
        {
            var save = new SaveAdapter(memoryOnly: true);
            save.Progress.tutorial = new TutorialSaveData { initialized = true, existingPlayer = true, controlsUnlocked = true, introductionSeen = true, menuIntroSeen = true };
            return save;
        }
    }
}
#endif
