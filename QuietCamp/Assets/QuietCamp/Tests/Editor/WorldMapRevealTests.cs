using System.Linq;
using NUnit.Framework;
using QuietCamp.Application;
using QuietCamp.Domain;
using QuietCamp.Infrastructure;

namespace QuietCamp.Tests
{
    /// <summary>World reveal is derived from progression, never scroll: a fresh
    /// player sees the completed path, the current node and a short horizon —
    /// everything past it is atmosphere, and nothing ever re-hides.</summary>
    public class WorldMapRevealTests
    {
        static WorldMapData Map()
            => WorldMapBuilder.Build(
                LevelLoader.MvpLevelIds(), LevelLoader.Districts(),
                BonusCampCatalog.Slots, MonetizationConfiguration.Load().Catalog().Journeys,
                CampContent.Summary);

        static WorldMapRevealService Reveal(WorldMapData map, ProgressionService progress, JourneyAccessService journeys = null)
            => new WorldMapRevealService(map, progress, journeys);

        static string MainAt(WorldMapData map, int order) => map.Nodes[order - 1].LevelId;

        [Test] public void FreshGameSeesOnlyTheOpening()
        {
            var map = Map(); var reveal = Reveal(map, new ProgressionService());
            Assert.AreEqual(1, reveal.CurrentOrder);
            Assert.AreEqual(WorldNodeState.Current, reveal.State(MainAt(map, 1)));
            Assert.AreEqual(WorldNodeState.Available, reveal.State(MainAt(map, 2)));
            Assert.AreEqual(WorldNodeState.Available, reveal.State(MainAt(map, 3)));
            Assert.AreEqual(WorldNodeState.Hidden, reveal.State(MainAt(map, 4)));
            Assert.AreEqual(WorldNodeState.Hidden, reveal.State(MainAt(map, 110)));
            // Chunks: only the first chunk (orders 1–8) may be live.
            Assert.AreEqual(new[] { 0 }, reveal.ActiveChunks().ToArray());
        }

        [Test] public void OneCompletedKeepsAShortHorizon()
        {
            var map = Map(); var progress = new ProgressionService();
            progress.MarkCompleted(MainAt(map, 1));
            var reveal = Reveal(map, progress);
            Assert.AreEqual(WorldNodeState.Completed, reveal.State(MainAt(map, 1)));
            Assert.AreEqual(WorldNodeState.Current, reveal.State(MainAt(map, 2)));
            Assert.AreEqual(WorldNodeState.Available, reveal.State(MainAt(map, 4)));
            Assert.AreEqual(WorldNodeState.Hidden, reveal.State(MainAt(map, 5)));
        }

        [Test] public void TenCompletedRevealsFirstBonusTeaser()
        {
            var map = Map(); var progress = new ProgressionService();
            for (var i = 1; i <= 10; i++) progress.MarkCompleted(MainAt(map, i));
            var reveal = Reveal(map, progress);
            Assert.AreEqual(11, reveal.CurrentOrder);
            Assert.AreEqual(WorldNodeState.Hidden, reveal.State(MainAt(map, 14)));
            // The first bonus glade anchors at order 10 — inside the horizon.
            var first = map.Branches.First(b => b.Type == WorldBranchType.BonusGlade);
            Assert.AreEqual(10, first.AttachOrder);
            Assert.IsTrue(reveal.RevealedBranches().Any(b => b.Id == first.Id));
        }

        [Test] public void GatedBranchTeasesBeforeItOpens()
        {
            var map = Map(); var progress = new ProgressionService();
            var reveal = Reveal(map, progress);
            var memories = map.Branches.First(b => b.Id == "memories");
            // 15 completions required — at fresh state it is a teaser silhouette.
            Assert.AreEqual(WorldNodeState.Teaser, reveal.BranchState(memories));
            Assert.AreEqual(WorldNodeState.Teaser, reveal.BranchNodeState(memories.Id, 0));
            Assert.AreEqual(WorldNodeState.Hidden, reveal.BranchNodeState(memories.Id, 2));
            for (var i = 1; i <= 15; i++) progress.MarkCompleted(MainAt(map, i));
            Assert.AreNotEqual(WorldNodeState.Teaser, Reveal(map, progress).BranchState(memories));
        }

        [Test] public void BranchProgressTapersIntoMist()
        {
            var map = Map(); var progress = new ProgressionService();
            var journey = map.Branches.First(b => b.Type == WorldBranchType.StoryJourney);
            for (var i = 1; i <= journey.RequiredCompletions; i++) progress.MarkCompleted(MainAt(map, i));
            var reveal = Reveal(map, progress);
            Assert.AreEqual(WorldNodeState.Current, reveal.BranchNodeState(journey.Id, 0));
            Assert.AreEqual(WorldNodeState.Teaser, reveal.BranchNodeState(journey.Id, 1));
            Assert.AreEqual(WorldNodeState.Hidden, reveal.BranchNodeState(journey.Id, 7));
            progress.MarkCompleted(journey.NodeIds[0]);
            Assert.AreEqual(WorldNodeState.Completed, Reveal(map, progress).BranchNodeState(journey.Id, 0));
        }

        [Test] public void MigratedSaveRestoresTheSameWorld()
        {
            var map = Map(); var progress = new ProgressionService();
            var ids = LevelLoader.MvpLevelIds();
            progress.Restore(ids.Take(10).ToArray(), ids[9], 0); // migrated save
            var reveal = Reveal(map, progress);
            Assert.AreEqual(11, reveal.CurrentOrder);
            Assert.AreEqual(WorldNodeState.Completed, reveal.State(ids[0]));
            Assert.AreEqual(WorldNodeState.Current, reveal.State(ids[10]));
            Assert.AreEqual(WorldNodeState.Hidden, reveal.State(ids[14]));
        }

        [Test] public void ReplayingAnOldLevelNeverRehidesTheWorld()
        {
            var map = Map(); var progress = new ProgressionService();
            for (var i = 1; i <= 20; i++) progress.MarkCompleted(MainAt(map, i));
            progress.MarkCompleted(MainAt(map, 3)); // replay — idempotent
            var reveal = Reveal(map, progress);
            Assert.AreEqual(21, reveal.CurrentOrder);
            Assert.AreEqual(WorldNodeState.Completed, reveal.State(MainAt(map, 3)));
            Assert.AreEqual(WorldNodeState.Current, reveal.State(MainAt(map, 21)));
        }

        [Test] public void ActiveChunksGrowWithProgressNeverShrink()
        {
            var map = Map(); var progress = new ProgressionService();
            var reveal = Reveal(map, progress);
            Assert.AreEqual(1, reveal.ActiveChunks().Count);
            for (var i = 1; i <= 9; i++) progress.MarkCompleted(MainAt(map, i));
            Assert.IsTrue(Reveal(map, progress).ActiveChunks().Contains(1));
            for (var i = 10; i <= 17; i++) progress.MarkCompleted(MainAt(map, i));
            var active = Reveal(map, progress).ActiveChunks();
            Assert.IsTrue(active.Contains(2));
            Assert.IsFalse(active.Contains(5)); // far chunks stay data-only
        }
    }
}
