using System.Threading.Tasks;
using NUnit.Framework;
using QuietCamp.Application;

namespace QuietCamp.Tests.Editor
{
    public sealed class CrashDiagnosticsTests
    {
        sealed class Transport : ICrashTransport
        {
            public bool Available => true;
            public bool Enabled, SentPrevious;
            public int Reports;
            public TaskCompletionSource<bool> Pending;
            public Task<bool> Initialize() => Pending?.Task ?? Task.FromResult(true);
            public void SetConsent(bool enabled, bool sendPreviousSession = false)
            { Enabled = enabled; SentPrevious = enabled && sendPreviousSession; }
            public void SetLevel(int level) { }
            public void Report(CrashArea area) { Reports++; }
            public void Dispose() { Enabled = false; }
        }
        [Test] public async Task FreshChoiceDoesNotSendHistoricalReportsOrEnableAnalytics()
        {
            var settings = new SettingsSaveData { privacyAgeBand = (int)PrivacyAgeBand.Adult, privacyAgeVersion = PrivacyEligibility.AgeVersion }; var transport = new Transport();
            using var diagnostics = new CrashDiagnostics(settings, transport, () => true, "r1");
            await diagnostics.RestoreConsent();
            diagnostics.Report(CrashArea.Save); Assert.AreEqual(0, transport.Reports);
            await diagnostics.SetConsent(true);
            diagnostics.Report(CrashArea.Save); diagnostics.Report(CrashArea.Save);
            Assert.IsTrue(transport.Enabled); Assert.IsFalse(transport.SentPrevious);
            Assert.AreEqual(1, transport.Reports); Assert.IsFalse(settings.analyticsConsent);
            await diagnostics.SetConsent(false); diagnostics.Report(CrashArea.DevelopmentTest);
            Assert.IsFalse(transport.Enabled); Assert.IsFalse(settings.crashSessionEligible); Assert.AreEqual(1, transport.Reports);
        }
        [Test] public async Task RevocationWhileInitializingCannotBeOverridden()
        {
            var transport = new Transport { Pending = new TaskCompletionSource<bool>() };
            using var diagnostics = new CrashDiagnostics(new SettingsSaveData { privacyAgeBand = (int)PrivacyAgeBand.Adult, privacyAgeVersion = PrivacyEligibility.AgeVersion }, transport, () => true, "r1");
            var enabling = diagnostics.SetConsent(true); var revoking = diagnostics.SetConsent(false);
            transport.Pending.SetResult(true); await Task.WhenAll(enabling, revoking);
            Assert.IsFalse(diagnostics.Collecting); Assert.IsFalse(transport.Enabled);
        }
        [Test] public async Task OnlyPreviouslyConsentedSessionCanBeSentAfterRestart()
        {
            var settings = new SettingsSaveData { privacyAgeBand = (int)PrivacyAgeBand.Adult, privacyAgeVersion = PrivacyEligibility.AgeVersion, crashReportingConsent = true, crashPolicyVersion = CrashDiagnostics.PolicyVersion,
                crashPublicationRevision = "r1", crashSessionEligible = false };
            var transport = new Transport();
            using (var diagnostics = new CrashDiagnostics(settings, transport, () => true, "r1"))
            { await diagnostics.RestoreConsent(); Assert.IsFalse(transport.SentPrevious); }
            using (var diagnostics = new CrashDiagnostics(settings, transport, () => true, "r1"))
            { await diagnostics.RestoreConsent(); Assert.IsTrue(transport.SentPrevious); }
            using (var diagnostics = new CrashDiagnostics(settings, transport, () => true, "r2"))
            { await diagnostics.RestoreConsent(); Assert.IsFalse(transport.Enabled); }
        }
        [Test] public async Task FailedSaveCannotAuthorizeDiagnosticUploads()
        {
            using var diagnostics = new CrashDiagnostics(new SettingsSaveData { privacyAgeBand = (int)PrivacyAgeBand.Adult, privacyAgeVersion = PrivacyEligibility.AgeVersion }, new Transport(), () => false, "r1");
            await diagnostics.SetConsent(true); Assert.IsFalse(diagnostics.Collecting);
        }
    }
}
