using System;

namespace QuietCamp.Application
{
    public enum PrivacyAgeBand { Unknown, Under13, From13To15, From16To17, Adult }
    public static class PrivacyEligibility
    {
        public const int AgeVersion = 1;
        public const int AdvertisingVersion = 1;
        public static PrivacyAgeBand Band(SettingsSaveData settings)
            => settings != null && settings.privacyAgeVersion == AgeVersion
                && Enum.IsDefined(typeof(PrivacyAgeBand), settings.privacyAgeBand)
                    ? (PrivacyAgeBand)settings.privacyAgeBand : PrivacyAgeBand.Unknown;
        public static bool Allowed(SettingsSaveData settings)
            => Band(settings) == PrivacyAgeBand.From13To15 || Band(settings) == PrivacyAgeBand.From16To17 || Band(settings) == PrivacyAgeBand.Adult;
        public static bool PurchasesAllowed(SettingsSaveData settings) => Allowed(settings);
        public static bool PersonalizationAllowed(SettingsSaveData settings) => Band(settings) == PrivacyAgeBand.Adult;
        public static void Revoke(SettingsSaveData settings)
        {
            settings.analyticsConsent = settings.crashReportingConsent = settings.advertisingConsent = false;
            settings.analyticsPolicyVersion = settings.crashPolicyVersion = settings.advertisingPolicyVersion = 0;
            settings.analyticsPublicationRevision = settings.crashPublicationRevision = settings.advertisingRevision = "";
            settings.crashSessionEligible = false;
        }
        public static void Reset(SettingsSaveData settings)
        {
            Revoke(settings); settings.privacyAgeBand = 0; settings.privacyAgeVersion = 0; settings.privacySetupVersion = 0;
            settings.cloudSyncEnabled=false; settings.cloudConsentVersion=0; settings.accountChoiceVersion=0;
        }
        public static bool ChangeAge(SettingsSaveData settings, PrivacyAgeBand band, Func<bool> save)
        {
            if (!Enum.IsDefined(typeof(PrivacyAgeBand), band)) return false;
            if (Band(settings) == band && settings.privacyAgeVersion == AgeVersion) return true;
            Revoke(settings);
            settings.privacyAgeBand = (int)band; settings.privacyAgeVersion = AgeVersion;
            try { if (save()) return true; } catch { }
            Reset(settings); return false;
        }
        public static bool AdvertisingAllowed(SettingsSaveData settings, string revision)
            => (Band(settings) == PrivacyAgeBand.From16To17 || Band(settings) == PrivacyAgeBand.Adult) && settings.advertisingConsent && !string.IsNullOrWhiteSpace(revision)
                && settings.advertisingPolicyVersion == AdvertisingVersion && settings.advertisingRevision == revision;
        public static bool SetAdvertising(SettingsSaveData settings, bool enabled, string revision, Func<bool> save)
        {
            settings.advertisingConsent = enabled && (Band(settings) == PrivacyAgeBand.From16To17 || Band(settings) == PrivacyAgeBand.Adult) && !string.IsNullOrWhiteSpace(revision);
            settings.advertisingPolicyVersion = AdvertisingVersion; settings.advertisingRevision = revision;
            try { if (save()) return true; } catch { }
            settings.advertisingConsent = false; return false;
        }
        public static bool SetVideoBonuses(SettingsSaveData settings, bool enabled, Func<bool> save)
        {
            settings.optionalVideoBonuses = enabled;
            try { if (save()) return true; } catch { }
            settings.optionalVideoBonuses = false; return false;
        }
    }
}
