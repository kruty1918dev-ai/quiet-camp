using System;

namespace QuietCamp.Application
{
    public enum PrivacySetupStep { Policy, Age, Advertising, Analytics, Crashes, Review }

    /// <summary>Local first-run decisions only. No SDK, network, birth date or parental identity.</summary>
    public sealed class PrivacySetup
    {
        public const int Version = 1;
        readonly bool _analyticsAvailable, _crashesAvailable, _adsAvailable;
        readonly bool _restrictedVideosAvailable;
        public PrivacySetupStep Step { get; private set; } = PrivacySetupStep.Policy;
        public PrivacyAgeBand Age { get; private set; } = PrivacyAgeBand.Unknown;
        public bool HasAgeChoice { get; private set; }
        public bool Analytics { get; private set; }
        public bool Crashes { get; private set; }
        public bool Advertising { get; private set; }
        public bool Eligible => Age == PrivacyAgeBand.From16To17 || Age == PrivacyAgeBand.Adult;
        public bool CanPlay => true;
        public bool HasOptionalServices => _analyticsAvailable || _crashesAvailable || _adsAvailable;
        public bool RestrictedVideosAvailable => Age == PrivacyAgeBand.From13To15 && _restrictedVideosAvailable;

        public PrivacySetup(bool analyticsAvailable, bool crashesAvailable, bool adsAvailable, bool restrictedVideosAvailable = false)
        { _analyticsAvailable = analyticsAvailable; _crashesAvailable = crashesAvailable; _adsAvailable = adsAvailable; _restrictedVideosAvailable = restrictedVideosAvailable; }

        public static bool IsComplete(SettingsSaveData settings, string revision, string hash, bool draft)
            => settings != null && settings.privacySetupVersion == Version
                && PrivacyAcknowledgement.IsCurrent(settings, revision, hash, draft)
                && settings.privacyAgeVersion == PrivacyEligibility.AgeVersion
                && Enum.IsDefined(typeof(PrivacyAgeBand), settings.privacyAgeBand);

        public void ReadPolicy() { if (Step == PrivacySetupStep.Policy) Step = PrivacySetupStep.Age; }
        public void SelectAge(PrivacyAgeBand age)
        {
            if (Step != PrivacySetupStep.Age || !Enum.IsDefined(typeof(PrivacyAgeBand), age)) return;
            Age = age; HasAgeChoice = true; Analytics = Crashes = Advertising = false;
        }
        public void ConfirmAge()
        {
            if (Step != PrivacySetupStep.Age || !HasAgeChoice) return;
            Step = Next(PrivacySetupStep.Age);
        }
        PrivacySetupStep Next(PrivacySetupStep after)
        {
            if (Eligible)
            {
                if (after < PrivacySetupStep.Advertising && _adsAvailable) return PrivacySetupStep.Advertising;
                if (after < PrivacySetupStep.Analytics && _analyticsAvailable) return PrivacySetupStep.Analytics;
                if (after < PrivacySetupStep.Crashes && _crashesAvailable) return PrivacySetupStep.Crashes;
            }
            return PrivacySetupStep.Review;
        }
        public void Choose(bool allow)
        {
            if (Step == PrivacySetupStep.Analytics) Analytics = allow && Eligible && _analyticsAvailable;
            else if (Step == PrivacySetupStep.Crashes) Crashes = allow && Eligible && _crashesAvailable;
            else if (Step == PrivacySetupStep.Advertising) Advertising = allow && Eligible && _adsAvailable;
            else return;
            Step = Next(Step);
        }
        public void ContinueWithoutOptional()
        {
            if (Step != PrivacySetupStep.Analytics && Step != PrivacySetupStep.Crashes
                && Step != PrivacySetupStep.Advertising && Step != PrivacySetupStep.Review) return;
            Analytics = Crashes = Advertising = false; Step = PrivacySetupStep.Review;
        }
        public void Back()
        {
            if (Step == PrivacySetupStep.Policy) return;
            // Revisiting age/policy discards pending grants; no purpose is preselected.
            Analytics = Crashes = Advertising = false;
            Step = Step == PrivacySetupStep.Age ? PrivacySetupStep.Policy : PrivacySetupStep.Age;
        }
        public bool Commit(SettingsSaveData settings, string revision, string hash, string language, bool draft, Func<bool> save, string consentRevision = null)
        {
            if (Step != PrivacySetupStep.Review || !HasAgeChoice || settings == null || save == null
                || string.IsNullOrWhiteSpace(revision) || string.IsNullOrWhiteSpace(hash)) return false;
            PrivacyEligibility.Revoke(settings);
            string processingRevision = string.IsNullOrWhiteSpace(consentRevision) ? revision : consentRevision;
            settings.privacyAgeBand = (int)Age; settings.privacyAgeVersion = PrivacyEligibility.AgeVersion;
            settings.analyticsConsent = Analytics && Eligible && _analyticsAvailable;
            settings.analyticsPolicyVersion = ComfortAnalytics.PolicyVersion; settings.analyticsPublicationRevision = processingRevision;
            settings.crashReportingConsent = Crashes && Eligible && _crashesAvailable;
            settings.crashPolicyVersion = CrashDiagnostics.PolicyVersion; settings.crashPublicationRevision = processingRevision;
            settings.advertisingConsent = Advertising && Eligible && _adsAvailable;
            settings.advertisingPolicyVersion = PrivacyEligibility.AdvertisingVersion; settings.advertisingRevision = processingRevision;
            settings.crashSessionEligible = false;
            settings.language = language;
            PrivacyAcknowledgement.Record(settings, revision, hash, language, draft);
            settings.privacySetupVersion = Version;
            try { if (save()) return true; } catch { }
            PrivacyEligibility.Reset(settings); settings.privacySetupVersion = 0;
            Analytics = Crashes = Advertising = false;
            return false;
        }
    }
}
