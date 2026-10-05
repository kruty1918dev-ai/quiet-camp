using System;
using System.Collections.Generic;
using UnityEngine;

namespace QuietCamp.Infrastructure
{
    /// <summary>Publisher-owned release metadata. Empty drafts never activate an online provider.</summary>
    [Serializable] public sealed class LegalConfiguration
    {
        public string appName = "QuietCamp", publisherName = "", supportEmail = "";
        public string privacyPolicyUrl = "", dataRequestUrl = "", accountDeletionUrl = "";
        public string termsUrl = "", supportUrl = "", revision = "", publishedOn = "";
        public int minimumAge = 13;
        public bool accountsEnabled, cloudSavesEnabled, purchasesEnabled;
        public bool teenPrivacyReviewed, sdkDisclosuresReviewed;
        public bool Published => !string.IsNullOrWhiteSpace(publisherName) && Email(supportEmail)
            && PublishedUrl(privacyPolicyUrl) && !string.IsNullOrWhiteSpace(revision)
            && DateTime.TryParseExact(publishedOn, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out _);
        public bool OnlineServicesApproved => Published && minimumAge == 13 && PublishedUrl(dataRequestUrl)
            && teenPrivacyReviewed && sdkDisclosuresReviewed
            && (!accountsEnabled || PublishedUrl(accountDeletionUrl))
            && (!purchasesEnabled || PublishedUrl(termsUrl));
        public static LegalConfiguration Load()
        {
            var source = Resources.Load<TextAsset>("QuietCamp/legal");
            return source == null ? new LegalConfiguration()
                : JsonUtility.FromJson<LegalConfiguration>(source.text) ?? new LegalConfiguration();
        }
        public static bool PublishedUrl(string value)
        {
            if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme != "https"
                || !string.IsNullOrEmpty(uri.UserInfo) || uri.IsLoopback || uri.HostNameType != UriHostNameType.Dns
                || uri.AbsolutePath.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)) return false;
            var host = uri.DnsSafeHost.ToLowerInvariant();
            return host.Contains(".") && host != "example.com" && host != "example.org" && host != "example.net"
                && !host.EndsWith(".example.com") && !host.EndsWith(".example.org") && !host.EndsWith(".example.net")
                && !host.EndsWith(".example") && !host.EndsWith(".invalid") && !host.EndsWith(".test")
                && !Uri.UnescapeDataString(value).Contains("{{") && !Uri.UnescapeDataString(value).Contains("}}");
        }
        public static bool Email(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Contains("\n") || value.Contains("\r")) return false;
            try { var address = new System.Net.Mail.MailAddress(value); return address.Address == value
                && PublishedUrl("https://" + address.Host); }
            catch { return false; }
        }
        /// <summary>Read-only preparation audit, never a build or deployment hook.</summary>
        public IReadOnlyList<string> PublicationIssues(GoogleServicesConfiguration google)
        {
            var issues = new List<string>();
            if (string.IsNullOrWhiteSpace(publisherName)) issues.Add("Fill publisherName with the store-listed publisher/controller.");
            if (!Email(supportEmail)) issues.Add("Provide a monitored support/privacy email.");
            if (!PublishedUrl(privacyPolicyUrl)) issues.Add("Publish an accessible HTTPS privacy policy; verify without login, geo-block or PDF.");
            if (string.IsNullOrWhiteSpace(revision)) issues.Add("Set the published policy revision.");
            if (!DateTime.TryParseExact(publishedOn, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out _)) issues.Add("Set publishedOn as yyyy-MM-dd.");
            if (minimumAge != 13) issues.Add("Review target age groups: the approved audience for this project is 13+.");
            if (!teenPrivacyReviewed) issues.Add("Review teen consent/advertising rules for the selected countries.");
            if (google.analyticsEnabled || google.rewardedAdsEnabled || cloudSavesEnabled || accountsEnabled)
            {
                if (!PublishedUrl(dataRequestUrl)) issues.Add("Publish a data-rights request route and implement its server/support workflow.");
                if (!sdkDisclosuresReviewed) issues.Add("Audit exact SDK versions, default collection and Data safety disclosures.");
            }
            if (accountsEnabled && !PublishedUrl(accountDeletionUrl)) issues.Add("Account creation requires an in-app and public web deletion path.");
            if (accountsEnabled || cloudSavesEnabled || purchasesEnabled)
                issues.Add("Account/cloud/purchase flags describe planned features; validate real adapters and review flows before enabling them.");
            if (purchasesEnabled && !PublishedUrl(termsUrl)) issues.Add("Publish purchase terms, support and applicable refund information.");
            return issues;
        }
    }
}
