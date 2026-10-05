using QuietCamp.Infrastructure;
using UnityEditor;
using UnityEngine;

namespace QuietCamp.Editor
{
    /// <summary>Preparation only: never calls BuildPipeline or activates an SDK.</summary>
    public static class LegalPublicationAudit
    {
        [MenuItem("QuietCamp/Legal/Validate publication metadata")]
        public static void Validate()
        {
            var legal = LegalConfiguration.Load();
            var issues = legal.PublicationIssues(GoogleServicesConfiguration.Load());
            if (issues.Count == 0)
                Debug.Log("[QuietCamp Legal] Metadata ready. Still verify public links, SDK behavior, Data safety, regional review and support/deletion operations. This is not legal certification.");
            else foreach (var issue in issues) Debug.LogWarning("[QuietCamp Legal] " + issue);
            var target = (int)PlayerSettings.Android.targetSdkVersion;
            if (target > 0 && target < 36)
                Debug.LogWarning("[QuietCamp Legal] Android target API is " + target
                    + ". New mobile apps/updates require API 36 from 2026-08-31 (check any Console extension). Settings only; no player build was run.");
            else if (target == 0)
                Debug.LogWarning("[QuietCamp Legal] Automatic target API: verify the actual installed target meets the current Play deadline before a future submission.");
        }
    }
}
