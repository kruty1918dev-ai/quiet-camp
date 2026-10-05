using System;

namespace QuietCamp.Application
{
    /// <summary>A first-run document receipt. It grants no SDK or data-processing permission.</summary>
    public static class PrivacyAcknowledgement
    {
        public static bool IsCurrent(SettingsSaveData settings, string revision, string contentHash, bool draft)
            => settings != null && !string.IsNullOrWhiteSpace(revision) && !string.IsNullOrWhiteSpace(contentHash)
                && settings.privacyAcknowledgementRevision == revision
                && settings.privacyAcknowledgementHash == contentHash && settings.privacyAcknowledgementDraft == draft;

        public static void Record(SettingsSaveData settings, string revision, string contentHash, string language, bool draft)
        {
            if (settings == null || string.IsNullOrWhiteSpace(revision) || string.IsNullOrWhiteSpace(contentHash))
                throw new ArgumentException("A loaded, versioned document is required.");
            settings.privacyAcknowledgementRevision = revision;
            settings.privacyAcknowledgementHash = contentHash;
            settings.privacyAcknowledgementLanguage = language;
            settings.privacyAcknowledgementDraft = draft;
            settings.privacyAcknowledgedUtc = DateTime.UtcNow.ToString("O");
        }
    }
}
