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
            public string[] mvpLevelIds, campaignLevelIds, generatedLevelIds;
        }

        /// <summary>
        /// Loads and validates a level via the universal LevelKit codec:
        /// parse (with the QC format profile) -> adapt to LevelData ->
        /// game validation -> consume. Throws on parse or validation failure.
        /// </summary>
        public static LevelData Load(string levelId)
        {
            if (GeneratedCampSource.IsGeneratedId(levelId))
            {
                if (!GeneratedCampSource.TryParseId(levelId, out var recipeName, out var index))
                    throw new InvalidOperationException($"Generated level id malformed: '{levelId}'");
                return GeneratedCampSource.Generate(recipeName, index);
            }
            var asset = Resources.Load<TextAsset>($"{LevelsFolder}/{levelId}");
            if (asset == null) throw new InvalidOperationException($"Level '{levelId}' not found in Resources.");
            var result = Kruty1918.LevelKit.LevelJson.Parse(
                asset.text, QuietCampLevelAdapter.Profile);
            if (!result.Ok)
            {
                var msg = string.Join(",", result.Issues.ConvertAll(i => i.ToString()));
                throw new InvalidOperationException($"Level '{levelId}' invalid: {msg}");
            }
            var level = QuietCampLevelAdapter.ToLevelData(result.Document);
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
            {
                // Endless tail: deterministic generated levels after the authored set.
                var gen = campaign.generatedLevelIds;
                if (gen == null || gen.Length == 0) return campaign.mvpLevelIds;
                var all = new string[campaign.mvpLevelIds.Length + gen.Length];
                campaign.mvpLevelIds.CopyTo(all, 0);
                gen.CopyTo(all, campaign.mvpLevelIds.Length);
                return all;
            }
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
