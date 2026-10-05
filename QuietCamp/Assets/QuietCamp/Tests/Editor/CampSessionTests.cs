using System.Linq;
using NUnit.Framework;
using QuietCamp.Application;
using QuietCamp.Domain;
using QuietCamp.Infrastructure;

namespace QuietCamp.Tests
{
    public class CampSessionTests
    {
        [Test]
        public void UnchangedLayoutPreservesRedoAndRevision()
        {
            using var session = new CampSession(LevelLoader.Load("QC001"));
            var first = session.Level.witness[0];
            var second = session.Level.witness[1];
            Assert.IsTrue(session.TryCommit(PlacementCommand.Place(first.guestId, first.x, first.z, first.rotation), out _));
            Assert.IsTrue(session.TryCommit(PlacementCommand.Place(second.guestId, second.x, second.z, second.rotation), out _));
            Assert.IsTrue(session.Undo());
            var revision = session.State.Revision;
            Assert.IsFalse(session.TryCommit(PlacementCommand.Place(first.guestId, first.x, first.z, first.rotation), out _));
            Assert.AreEqual(revision, session.State.Revision);
            Assert.IsTrue(session.CanRedo, "A selection tap must not discard the redo branch");
            Assert.IsTrue(session.Redo());
            Assert.AreEqual(2, session.State.Count);
        }

        [Test]
        public void ReorderedIdenticalLayoutDoesNotCreateHistory()
        {
            var level = LevelLoader.Load("QC001");
            var history = new CommandHistory();
            history.Restore(level.witness);
            Assert.IsFalse(history.Commit(level, level.witness.Reverse()));
            Assert.IsFalse(history.CanUndo);
        }

        [Test]
        public void HardInvalidMovePreservesLayoutAndHistory()
        {
            using var session = new CampSession(LevelLoader.Load("QC001"));
            session.Restore(session.Level.witness, "g1");
            var revision = session.State.Revision;
            Assert.IsFalse(session.TryCommit(PlacementCommand.Place("g1", -1, 0, 0), out var report));
            Assert.IsFalse(report.CanCommit);
            Assert.AreEqual(revision, session.State.Revision);
            Assert.AreEqual(0, session.State.Find("g1").x);
            Assert.IsFalse(session.CanUndo);
        }

        [Test]
        public void CompletionProtectsTheLayoutAndEmitsOnce()
        {
            using var session = new CampSession(LevelLoader.Load("QC001"));
            foreach (var pose in session.Level.witness)
                Assert.IsTrue(session.TryCommit(PlacementCommand.Place(pose.guestId, pose.x, pose.z, pose.rotation), out _));
            var completed = 0;
            session.Evented += e => { if (e.Kind == CampEventKind.LevelCompleted) completed++; };
            Assert.IsTrue(session.Check().IsSolved);
            var revision = session.State.Revision;
            Assert.IsFalse(session.TryCommit(PlacementCommand.Remove("g1"), out _));
            Assert.IsFalse(session.Undo());
            Assert.IsFalse(session.Redo());
            Assert.AreEqual(revision, session.State.Revision);
            Assert.IsTrue(session.Check().IsSolved);
            Assert.AreEqual(1, completed);
            session.Restore(new Placement[0], null);
            Assert.IsFalse(session.IsCompleted, "Starting another attempt must clear completion");
            Assert.IsTrue(session.TryCommit(PlacementCommand.Place("g1", 0, 0, 0), out _));
        }

        [Test]
        public void SoftIssueCanBeAdjustedAndUndoRestoresIt()
        {
            using var session = new CampSession(LevelLoader.Load("QC001"));
            Assert.IsTrue(session.TryCommit(PlacementCommand.Place("g1", 0, 0, 2), out var report));
            Assert.IsTrue(report.Issues.Any(i => i.Code == "path"));
            Assert.IsTrue(session.TryCommit(PlacementCommand.Place("g1", 0, 0, 0), out report));
            Assert.IsEmpty(report.Issues);
            Assert.IsTrue(session.Undo());
            Assert.AreEqual(2, session.State.Find("g1").rotation);
        }
    }
}
