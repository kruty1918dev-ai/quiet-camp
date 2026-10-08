using System;
using System.Text;
using QuietCamp.Infrastructure;
using UnityEngine;

namespace QuietCamp.Presentation.UI
{
    public static class PrivacyPanel
    {
        static void OpenPublished(string url)
        {
            // Recheck inside the callback as well as when rendering a disabled link.
            if (LegalConfiguration.PublishedUrl(url)) UnityEngine.Application.OpenURL(url);
        }
        public static string Notice(GameServices services, HtmlSurface surface, Action close)
        {
            string T(string key) => services.Localization.T(key);
            return "<view class=\"privacy-notice\">" + HtmlUi.Text(T("privacy.explanation"), "s-sub")
                + HtmlUi.Text(T("privacy.provider"), "s-sub")
                + HtmlUi.Button(surface, "privacy-notice-policy", T("privacy.policy"), () => OpenPublished(services.GoogleConfiguration.privacyPolicyUrl), "quiet",
                    LegalConfiguration.PublishedUrl(services.GoogleConfiguration.privacyPolicyUrl))
                + HtmlUi.Button(surface, "privacy-allow", T("privacy.allow"), () => { _ = services.Analytics.SetConsent(true); close(); }, "quiet")
                + HtmlUi.Button(surface, "privacy-decline", T("privacy.decline"), () => { _ = services.Analytics.SetConsent(false); close(); }, "quiet")
                + "</view>";
        }
        public static string Render(GameServices services, HtmlSurface surface, SettingsNav nav)
        {
            string T(string key) => services.Localization.T(key);
            var legal = services.Legal;
            var config = services.GoogleConfiguration;
            var html = new StringBuilder();
            void Group(string key) => html.Append(HtmlUi.Text(T(key), "settings-group-title"));
            void Link(string id, string key, string url) => html.Append(HtmlUi.Button(surface, id, T(key),
                () => OpenPublished(url), "legal-link", LegalConfiguration.PublishedUrl(url)));
            if (nav.Section == null)
            {
                void Entry(string section, string icon, string summary)
                {
                    var id = "privacy-section-" + section;
                    surface.Callbacks.Bind(id, () => SettingsPanel.Navigate(surface, nav, "privacy", section));
                    html.Append("<button id=\"").Append(id).Append("\" class=\"settings-topic\" onClick=\"Globals.campUi.Click('")
                        .Append(id).Append("')\"><view class=\"topic-symbol\">").Append(CampIcons.Mark(icon))
                        .Append("</view><view class=\"topic-copy\">").Append(HtmlUi.Text(T("legal.section." + section), "topic-title"))
                        .Append(HtmlUi.Text(T(summary), "topic-summary")).Append("</view><view class=\"sc-chevron\">")
                        .Append(CampIcons.Mark("back")).Append("</view></button>");
                }
                html.Append("<view class=\"settings-topics\">");
                Entry("data", "tent", "legal.summary.data");
                Entry("analytics", "quiet", services.Analytics.Available ? "legal.summary.analytics" : "legal.summary.offline");
                Entry("documents", "lock", "legal.summary.documents");
                html.Append("</view>");
            }
            else if (nav.Section == "data")
            {
                html.Append(HtmlUi.Text(T("legal.localExplanation"), "s-sub"));
                if (!legal.accountsEnabled && !legal.cloudSavesEnabled)
                    html.Append(HtmlUi.Text(T("legal.noAccount"), "s-sub"));
                html.Append(HtmlUi.Button(surface, "legal-data-information", T("legal.dataInformation"), () =>
                { SettingsPanel.ChangeDisclosure(surface, nav, () => nav.DataInformation = !nav.DataInformation); }, "settings-disclosure"));
                if (nav.DataInformation)
                    html.Append("<view class=\"settings-note\">")
                        .Append(HtmlUi.Text(T("legal.dataInformation.local"), "s-sub"))
                        .Append(HtmlUi.Text(T("legal.dataInformation.optional"), "s-sub"))
                        .Append(HtmlUi.Text(T("legal.dataInformation.backups"), "s-sub"))
                        .Append(HtmlUi.Text(T("legal.dataInformation.requests"), "s-sub")).Append("</view>");
                Group("legal.remoteData");
                Link("privacy-delete-request", "privacy.deleteRequest", config.deletionRequestUrl);
                html.Append(HtmlUi.Text(T("legal.serverDeleteHelp"), "s-sub"));
                if (legal.accountsEnabled) Link("account-delete", "legal.accountDelete", legal.accountDeletionUrl);
                if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "MainMenu")
                    html.Append(HtmlUi.Text(T("legal.erase.menuOnly"), "s-sub"));
            }
            else if (nav.Section == "analytics")
            {
                // The full disclosure stays visible before consent, even with details collapsed.
                html.Append(HtmlUi.Text(T(services.Analytics.Available ? "privacy.explanation" : "privacy.offline"), "s-sub"));
                if (services.Analytics.Available)
                {
                    html.Append(HtmlUi.Text(T("privacy.provider"), "s-sub"));
                    Link("privacy-analytics-policy", "privacy.policy", config.privacyPolicyUrl);
                }
                surface.Callbacks.BindToggle("analytics-consent", async value =>
                { if (!services.Analytics.Available) return; await services.Analytics.SetConsent(value); if (surface != null) surface.Refresh(); });
                html.Append("<view class=\"toggle-row\"><view class=\"tr-text\"><label for=\"#analytics-consent\">")
                    .Append(HtmlUi.Escape(T("privacy.consent"))).Append("</label></view><switch id=\"analytics-consent\" checked=\"")
                    .Append(services.Analytics.Consented ? "true" : "false").Append("\" disabled=\"")
                    .Append(services.Analytics.Available ? "false" : "true")
                    .Append("\" onChange=\"Globals.campUi.Toggle('analytics-consent', event)\" /></view>");
                html.Append(HtmlUi.Button(surface, "privacy-details", T("legal.dataDetails"), () =>
                { SettingsPanel.ChangeDisclosure(surface, nav, () => nav.PrivacyDetails = !nav.PrivacyDetails); }, "settings-disclosure"));
                if (nav.PrivacyDetails)
                    html.Append("<view class=\"settings-note\">").Append(HtmlUi.Text(T("privacy.explanation"), "s-sub"))
                        .Append(HtmlUi.Text(T(services.Analytics.Available ? "privacy.provider" : "privacy.offline"), "s-sub")).Append("</view>");
                html.Append(HtmlUi.Button(surface, "analytics-clear", T("privacy.clear"), async () =>
                {
                    await services.Analytics.ClearLocalDataAndWithdraw();
                    services.Notifications?.Show(T("privacy.cleared"), Kruty1918.Notifications.API.GameplayNotificationKind.Info);
                    if (surface != null) surface.Refresh();
                }, "legal-link"));
                html.Append(HtmlUi.Text(T("legal.localClearHelp"), "s-sub"));
                if (services.Analytics.Collecting)
                {
                    Group("privacy.feedback");
                    for (int i = 0; i < 3; i++)
                    {
                        int choice = i;
                        html.Append(HtmlUi.Button(surface, "comfort-feedback-" + i, T("privacy.feedback." + i), () =>
                        { services.Analytics.Feedback(choice); services.Notifications?.Show(T("privacy.thanks"), Kruty1918.Notifications.API.GameplayNotificationKind.Info); }, "legal-link"));
                    }
                }
            }
            else if (nav.Section == "documents")
            {
                Link("privacy-policy", "privacy.policy", config.privacyPolicyUrl);
                Link("legal-terms", "legal.terms", legal.termsUrl);
                bool support = LegalConfiguration.PublishedUrl(legal.supportUrl) || LegalConfiguration.Email(legal.supportEmail);
                html.Append(HtmlUi.Button(surface, "legal-support", T("legal.support"), () =>
                {
                    if (LegalConfiguration.PublishedUrl(legal.supportUrl)) OpenPublished(legal.supportUrl);
                    else if (LegalConfiguration.Email(legal.supportEmail))
                        UnityEngine.Application.OpenURL("mailto:" + Uri.EscapeDataString(legal.supportEmail) + "?subject=QuietCamp%20support");
                }, "legal-link", support));
                if (!legal.Published) html.Append(HtmlUi.Text(T("legal.unavailable"), "s-sub"));
                if (legal.Published)
                    html.Append(HtmlUi.Text(legal.publisherName + " · " + legal.revision + " · " + legal.publishedOn, "s-sub"));
                html.Append(HtmlUi.Button(surface, "legal-licenses", T("legal.licenses"), () =>
                { SettingsPanel.ChangeDisclosure(surface, nav, () => nav.Licenses = !nav.Licenses); }, "settings-disclosure"));
                if (nav.Licenses)
                {
                    html.Append(HtmlUi.Text(T("legal.credits"), "s-sub"));
                    var roadmapCredits = Resources.Load<TextAsset>("QuietCamp/roadmap_attribution");
                    if (roadmapCredits != null) html.Append(HtmlUi.Text(roadmapCredits.text, "legal-license-text"));
                    var license = Resources.Load<TextAsset>("Fonts/DejaVu_License");
                    if (license != null) html.Append(HtmlUi.Text(license.text, "legal-license-text"));
                }
                html.Append(HtmlUi.Text("QuietCamp · " + UnityEngine.Application.version, "s-sub"));
            }
            if (services.Ads is QuietCamp.Application.IAdPrivacyOptions privacy && privacy.PrivacyOptionsRequired)
                html.Append(HtmlUi.Button(surface, "ads-privacy", T("privacy.adsOptions"), async () =>
                { await privacy.ShowPrivacyOptions(); surface.Refresh(); }, "legal-link"));
            return html.ToString();
        }
    }
}
