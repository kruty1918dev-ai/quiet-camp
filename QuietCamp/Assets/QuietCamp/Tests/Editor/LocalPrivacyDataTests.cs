using System;
using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;
using QuietCamp.Application;
using QuietCamp.Infrastructure;

namespace QuietCamp.Tests.Editor
{
    public sealed class LocalPrivacyDataTests
    {
        string _root;
        [SetUp] public void Setup() { _root = Path.Combine(Path.GetTempPath(), "qc-privacy-" + Guid.NewGuid()); Directory.CreateDirectory(Path.Combine(_root, "saves")); }
        [TearDown] public void Cleanup() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
        [Test] public void ErasureRemovesRecoveryCopiesWithoutTouchingOtherData()
        {
            var path = Path.Combine(_root, "saves", "slot00.mvs");
            foreach (var suffix in new[] { "", ".bak", ".tmp" }) File.WriteAllText(path + suffix, "old player data");
            var otherSlot = Path.Combine(_root, "saves", "slot01.mvs");
            File.WriteAllText(otherSlot, "other slot"); File.WriteAllText(Path.Combine(_root, "sdk-consent"), "independent data");
            var cache = Path.Combine(_root, "qc_policy_cache"); Directory.CreateDirectory(cache); File.WriteAllText(Path.Combine(cache, "document.json"), "policy copy");
            Assert.IsTrue(SaveAdapter.TryEraseLocalFiles(_root, out var error)); Assert.IsNull(error);
            foreach (var suffix in new[] { "", ".bak", ".tmp" }) Assert.IsFalse(File.Exists(path + suffix));
            Assert.IsTrue(File.Exists(otherSlot)); Assert.IsTrue(File.Exists(Path.Combine(_root, "sdk-consent")));
            Assert.IsFalse(Directory.Exists(cache), "Local erasure includes owned document cache");
            Assert.IsTrue(SaveAdapter.TryEraseLocalFiles(_root, out _), "Repeat must be idempotent");
        }
        [Test] public void FailureDoesNotClaimSuccessOrRemovePrimaryBeforeBackup()
        {
            var path = Path.Combine(_root, "saves", "slot00.mvs");
            File.WriteAllText(path, "recoverable save"); Directory.CreateDirectory(path + ".bak");
            Assert.IsFalse(SaveAdapter.TryEraseLocalFiles(_root, out var error)); Assert.IsNotEmpty(error);
            Assert.IsTrue(File.Exists(path)); Assert.IsFalse(error.Contains(_root), "Diagnostics must not expose paths");
        }
        [Test] public void MissingRootDoesNotFallBackToWorkingDirectory()
        { Assert.IsFalse(SaveAdapter.TryEraseLocalFiles("", out _)); }

        sealed class Transport : IComfortTransport
        {
            public bool Available => true;
            public int Initializations; public bool Enabled;
            public Task<bool> Initialize() { Initializations++; return Task.FromResult(true); }
            public void SetConsent(bool enabled) => Enabled = enabled;
            public void Send(ComfortRecord record) { }
            public void ResetLocalData() { }
            public void Dispose() { }
        }
        [Test] public async Task OldPublishedRevisionCannotRestoreCollection()
        {
            var settings = new SettingsSaveData { analyticsConsent = true, analyticsPolicyVersion = ComfortAnalytics.PolicyVersion,
                analyticsPublicationRevision = "old-policy" };
            var transport = new Transport();
            using (var analytics = new ComfortAnalytics(settings, transport, publicationRevision: "reviewed-policy"))
            {
                Assert.IsFalse(analytics.ChoiceCurrent); await analytics.RestoreConsent();
                Assert.AreEqual(0, transport.Initializations); Assert.IsFalse(analytics.Collecting);
                await analytics.SetConsent(true); Assert.IsTrue(analytics.Collecting);
                Assert.AreEqual("reviewed-policy", settings.analyticsPublicationRevision);
                await analytics.SetConsent(false); Assert.IsTrue(analytics.ChoiceCurrent);
                Assert.IsFalse(analytics.Consented); Assert.IsFalse(transport.Enabled);
            }
        }
    }
}
