using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using QuietCamp.Domain;
using UnityEngine;
namespace QuietCamp.Infrastructure
{
    /// <summary>
    /// Loads authored level JSON from Resources: parse -> validate -> freeze -> consume.
    /// Maps are never regenerated or hand-built in scenes on the device.
    /// </summary>
    public static class LevelLoader
    {
        public const string LevelsFolder = "QuietCamp/Levels";
        const string CampaignPath = "QuietCamp/campaign";

        [Serializable] sealed class CampaignData
        {
            public int schemaVersion, freeLevelCount;
            public string[] mvpLevelIds, campaignLevelIds;
        }

        /// <summary>Loads and validates a level; throws on parse or validation failure.</summary>
        public static LevelData Load(string levelId)
        {
            var asset = Resources.Load<TextAsset>($"{LevelsFolder}/{levelId}");
            if (asset == null) throw new InvalidOperationException($"Level '{levelId}' not found in Resources.");
            LevelData level;
            try { level = JsonConvert.DeserializeObject<LevelData>(asset.text); }
            catch (Exception e) { throw new InvalidOperationException($"Level '{levelId}' JSON parse failed: {e.Message}", e); }
            var errors = LevelContentValidator.Validate(level);
            if (errors.Count > 0)
                throw new InvalidOperationException($"Level '{levelId}' invalid: {string.Join(",", errors)}");
            return level;
        }

        /// <summary>MVP level order from campaign.json; falls back to 12 fixed ids.</summary>
        public static IReadOnlyList<string> MvpLevelIds()
        {
            var campaign = LoadCampaign();
            if (campaign?.mvpLevelIds != null && campaign.mvpLevelIds.Length > 0)
                return campaign.mvpLevelIds;
            var fallback = new string[12];
            for (var i = 0; i < 12; i++) fallback[i] = $"QC{i + 1:000}";
            return fallback;
        }

        public static IReadOnlyList<string> CampaignLevelIds()
        {
            var campaign = LoadCampaign();
            return campaign?.campaignLevelIds ?? (IReadOnlyList<string>)MvpLevelIds();
        }

        public static int FreeLevelCount()
        {
            var campaign = LoadCampaign();
            return campaign?.freeLevelCount ?? 12;
        }

        static CampaignData LoadCampaign()
        {
            var asset = Resources.Load<TextAsset>(CampaignPath);
            if (asset == null) return null;
            try { return JsonConvert.DeserializeObject<CampaignData>(asset.text); }
            catch (Exception e) { Debug.LogError($"[QuietCamp] campaign.json parse failed: {e.Message}"); return null; }
        }
    }
}
