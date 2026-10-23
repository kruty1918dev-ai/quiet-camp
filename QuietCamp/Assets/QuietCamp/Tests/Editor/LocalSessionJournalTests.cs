using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NUnit.Framework;
using QuietCamp.Infrastructure;

namespace QuietCamp.Tests.Editor
{
    public sealed class LocalSessionJournalTests
    {
        string _directory;
        [SetUp] public void SetUp() => _directory = Path.Combine(Path.GetTempPath(), "qc-session-test-" + Guid.NewGuid().ToString("N"));
        [TearDown] public void TearDown() { if (Directory.Exists(_directory)) Directory.Delete(_directory, true); }
        LocalSessionJournal Create() => new LocalSessionJournal(_directory, new LocalSessionDevice { appVersion = "test" });
        LocalSessionDocument Read(LocalSessionJournal journal) => JsonConvert.DeserializeObject<LocalSessionDocument>(File.ReadAllText(journal.FilePath));
        [Test] public void RestartCreatesSeparateFileAndCheckpointsAreValidBeforeQuit()
        {
            string path;
            using (var first = Create()) {
                path = first.FilePath;
                Assert.IsFalse(Read(first).endedNormally);
                first.Lifecycle("pause"); first.FlushBlocking();
                Assert.AreEqual("pause", Read(first).events.Last().code);
            }
            using (var second = Create()) {
                Assert.AreNotEqual(path, second.FilePath);
                Assert.IsTrue(JsonConvert.DeserializeObject<LocalSessionDocument>(File.ReadAllText(path)).endedNormally);
                Assert.AreEqual(2, Directory.GetFiles(_directory, "*.json").Length);
            }
        }
        [Test] public void CredentialsRawMessagesAndUnsafeIdsAreExcludedButHttp1010IsRecorded()
        {
            using (var journal = Create()) {
                journal.UiAction("password=SECRET_PASSWORD", 0, true);
                journal.Scene("SECRET_TOKEN", "SECRET_PASSWORD");
                journal.UnityError("NullReferenceException password=SECRET_PASSWORD token=SECRET_TOKEN", "QuietCamp.CampSession.Check () (at C:/PRIVATE_PATH/file.cs:10)", 4);
                journal.Access(new AccessRequestDiagnostic { operation = SessionRequestKind.SignIn, result = SessionRequestResult.ProtocolError, status = 403, elapsedMs = 123, cloudflare1010 = true });
                journal.FlushBlocking();
                var text = File.ReadAllText(journal.FilePath);
                foreach (var secret in new[] { "SECRET_PASSWORD", "SECRET_TOKEN", "PRIVATE_PATH" }) Assert.IsFalse(text.Contains(secret));
                var doc = Read(journal);
                Assert.AreEqual("QuietCamp.CampSession.Check", doc.events.Single(e => e.kind == "error").methods.Single());
                var request = doc.events.Single(e => e.kind == "complimentary-http");
                Assert.AreEqual(403, request.number); Assert.IsTrue(request.flag); Assert.AreEqual(123, request.durationMs);
            }
        }
        [Test] public void LongSessionsAreBoundedAndStartRemainsAvailable()
        {
            using (var journal = Create()) {
                for (int i = 0; i < LocalSessionJournal.EventLimit + 20; i++) journal.UiAction("qc.check", 0, true);
                for (int i = 0; i < LocalSessionJournal.SampleLimit + 20; i++) journal.Sample(new LocalSessionSample { fps = 60 });
                journal.FlushBlocking(); var doc = Read(journal);
                Assert.AreEqual(LocalSessionJournal.EventLimit, doc.events.Length);
                Assert.AreEqual("start", doc.events[0].code); Assert.Greater(doc.droppedEvents, 0);
                Assert.AreEqual(LocalSessionJournal.SampleLimit, doc.samples.Length); Assert.AreEqual(20, doc.droppedSamples);
            }
        }
        [Test] public void ConcurrentCheckpointsRemainOrderedAndAtomic()
        {
            using (var journal = Create()) {
                Parallel.For(0, 32, i => { journal.UiAction("qc.check", i, true); journal.FlushAsync(); });
                journal.FlushBlocking();
                Assert.AreEqual(33, Read(journal).events.Length);
                Assert.IsFalse(File.Exists(journal.FilePath + ".tmp"));
                Assert.IsFalse(File.Exists(journal.FilePath + ".bak"));
            }
        }
        [Test] public void WriteFailureDoesNotThrowAndCanRecover()
        {
            using (var journal = Create()) {
                Directory.CreateDirectory(journal.FilePath + ".tmp");
                Assert.DoesNotThrow(journal.FlushBlocking);
                Assert.IsFalse(journal.LastWriteSucceeded);
                Directory.Delete(journal.FilePath + ".tmp"); journal.FlushBlocking();
                Assert.IsTrue(journal.LastWriteSucceeded);
                Assert.Greater(Read(journal).writeFailures, 0);
            }
        }
        [Test] public void BackupIsRecoveredAndOldestSessionsArePruned()
        {
            Directory.CreateDirectory(_directory);
            File.WriteAllText(Path.Combine(_directory, "session-recovered.json.bak"), "{}");
            using (Create()) Assert.IsTrue(File.Exists(Path.Combine(_directory, "session-recovered.json")));
            for (int i = 0; i < 52; i++) File.WriteAllText(Path.Combine(_directory, "session-old-" + i + ".json"), "{}");
            using (Create()) Assert.AreEqual(LocalSessionJournal.RetainedSessions, Directory.GetFiles(_directory, "*.json").Length);
        }
    }
}
