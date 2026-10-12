using System;
using System.Linq;
using NUnit.Framework;
using QuietCamp.Domain;
using QuietCamp.Infrastructure;
using QuietCamp.Application;
namespace QuietCamp.Tests
{
    public class LiveCampContentTests
    {
        [TestCase("QC001")]
        [TestCase("QC009")]
        [TestCase("QC010")]
        public void AlbumRecoversMissingJaggedArraysWithoutChangingSavedSnapshot(string id)
        {
            var original = LevelLoader.Load(id);
            var snapshot = CampContent.Snapshot(original);
            snapshot.blocked = snapshot.shade = snapshot.noise = null;
            snapshot.friends = null;
            var entry = new AlbumSaveData.Entry { levelId = id, contentHash = original.contentHash,
                levelSnapshot = snapshot, placements = original.witness };

            var recovered = CampContent.AlbumLevel(entry);

            Assert.AreEqual(original.contentHash, recovered.contentHash);
            Assert.AreEqual(Newtonsoft.Json.JsonConvert.SerializeObject(original),
                Newtonsoft.Json.JsonConvert.SerializeObject(recovered));
            Assert.IsTrue(RuleEvaluator.Evaluate(recovered, entry.placements).IsSolved);
            Assert.AreSame(snapshot, entry.levelSnapshot);
            Assert.IsNull(snapshot.blocked);
            Assert.AreSame(original.witness, entry.placements);
        }
        [Test] public void AlbumDoesNotReplaceUnavailableHistoricalSnapshotWithCurrentPuzzle()
        {
            var snapshot = CampContent.Snapshot(LevelLoader.Load("QC010"));
            snapshot.contentHash = new string('a', 64);
            snapshot.blocked = snapshot.shade = snapshot.noise = null;
            snapshot.friends = null;
            var entry = new AlbumSaveData.Entry { levelId = snapshot.id, contentHash = snapshot.contentHash,
                levelSnapshot = snapshot, placements = snapshot.witness };

            var preview = CampContent.AlbumLevel(entry);

            Assert.AreNotSame(snapshot, preview);
            Assert.AreEqual(snapshot.contentHash, preview.contentHash);
            Assert.IsEmpty(preview.blocked); Assert.IsEmpty(preview.shade);
            Assert.IsEmpty(preview.noise); Assert.IsEmpty(preview.friends);
            Assert.IsNull(snapshot.blocked);
            Assert.AreSame(snapshot, entry.levelSnapshot);
        }
        [Test] public void AllHundredLevelsValidateAndSolverConfirmsTheirWitness()
        {
            var ids=LevelLoader.MvpLevelIds();Assert.AreEqual(100,ids.Count);
            foreach(var id in ids)
            {
                var l=LevelLoader.Load(id);Assert.AreEqual(id,l.id);Assert.IsEmpty(LevelContentValidator.Validate(l),id);
                Assert.IsTrue(RuleEvaluator.Evaluate(l,l.witness).IsSolved,id);
                var solver=new CampSolver(l,Array.Empty<Placement>(),5);var status=solver.Step();while(status==CampSolver.Status.Searching)status=solver.Step(.05);
                Assert.AreEqual(CampSolver.Status.Solved,status,id);
                Assert.NotNull(CampContent.Legacy(id),"Archive missing "+id);
            }
            Assert.Greater(ids.Select(id=>{var l=LevelLoader.Load(id);return l.width+"x"+l.height;}).Distinct().Count(),6);
            Assert.AreEqual(100,CampContent.Summaries.Count);
        }
        [Test] public void EnvironmentRoundTripPreservesObjectsAndProjectedShade()
        {
            var l=LevelLoader.Load("QC009");var copy=QuietCampLevelAdapter.ToLevelData(QuietCampLevelAdapter.ToDocument(l));
            Assert.AreEqual(l.environmentPreset,copy.environmentPreset);Assert.AreEqual(l.objects.Length,copy.objects.Length);
            CollectionAssert.AreEquivalent(l.shade.Select(c=>new Cell(c[0],c[1])),ShadeProjection.Cells(copy).Select(c=>new Cell(c[0],c[1])));
            foreach(var fire in copy.objects.Where(o=>o.assetId=="campfire_stones"))
            {Assert.IsTrue(copy.blocked.Any(c=>c[0]==fire.x&&c[1]==fire.z));Assert.IsTrue(copy.noise.Any(c=>c[0]==fire.x&&c[1]==fire.z));}
        }
        [Test] public void LegacyGeneratedIdsAndAlbumKeepTheirOriginalLevel()
        {
            Assert.AreEqual("gen:qc_camp:12",CampContent.CanonicalId("GEN012"));
            var entry=new AlbumSaveData.Entry{levelId="GEN001"};var legacy=CampContent.AlbumLevel(entry);
            Assert.AreEqual(6,legacy.width);Assert.AreEqual(6,legacy.height);
            Assert.IsTrue(RuleEvaluator.Evaluate(legacy,legacy.witness).IsSolved);
            var current=LevelLoader.Load("GEN001");Assert.AreEqual("gen:qc_camp:1",current.id);Assert.AreNotEqual(current.contentHash,legacy.contentHash);
            entry.levelSnapshot=current;entry.contentHash=current.contentHash;Assert.AreSame(current,CampContent.AlbumLevel(entry));
        }
        [Test] public void MigrationPreservesProgressCosmeticsAndSavedArrangement()
        {
            var archived=CampContent.Legacy("GEN001");
            var save=new SaveAdapter();
            save.Progress.completedIds=new[]{"QC001","GEN001","gen:qc_camp:1"};
            save.Progress.lastLevelId="GEN001";save.Progress.cosmeticFlags=37;
            save.Settings.language="de";save.Settings.reducedMotion=true;
            save.Session.levelId="GEN001";save.Session.contentHash=archived.contentHash;
            save.Session.placements=archived.witness;
            save.Album.entries=new[]{new AlbumSaveData.Entry{levelId="GEN001",cosmeticId="warm",order=7,placements=archived.witness}};
            CampContent.Migrate(save);CampContent.Migrate(save);
            CollectionAssert.AreEquivalent(new[]{"QC001","gen:qc_camp:1"},save.Progress.completedIds);
            Assert.AreEqual("gen:qc_camp:1",save.Session.levelId);Assert.AreEqual(save.Session.levelId,save.Progress.lastLevelId);
            Assert.AreEqual(37,save.Progress.cosmeticFlags);Assert.AreEqual("de",save.Settings.language);Assert.IsTrue(save.Settings.reducedMotion);
            var album=Newtonsoft.Json.JsonConvert.DeserializeObject<AlbumSaveData>(Newtonsoft.Json.JsonConvert.SerializeObject(save.Album));
            var entry=album.entries.Single();Assert.AreEqual("warm",entry.cosmeticId);Assert.AreEqual(7,entry.order);
            Assert.AreEqual(archived.contentHash,entry.contentHash);Assert.AreEqual(6,entry.levelSnapshot.width);
            Assert.IsTrue(RuleEvaluator.Evaluate(entry.levelSnapshot,entry.placements).IsSolved);
            Assert.AreNotEqual(LevelLoader.Load(save.Session.levelId).contentHash,save.Session.contentHash);
        }
    }
}
