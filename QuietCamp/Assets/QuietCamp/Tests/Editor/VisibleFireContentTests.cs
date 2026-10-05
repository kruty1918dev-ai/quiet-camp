using System.Linq;
using Newtonsoft.Json;
using NUnit.Framework;
using QuietCamp.Application;
using QuietCamp.Domain;
using QuietCamp.Infrastructure;

namespace QuietCamp.Tests
{
    public class VisibleFireContentTests
    {
        [Test]
        public void PartialTutorialLayoutAndAlbumKeepTheirArchivedRulesAfterCampaignRegeneration()
        {
            var current=LevelLoader.Load("QC003");
            var previous=CampContent.Revision("95b3640722c0c82dd9c3d051fd25cf6dada6f787cb4fb903a0d7304c9ad8ad47");
            var save=new SaveAdapter();save.Session.levelId=previous.id;save.Session.contentHash=previous.contentHash;
            save.Session.placements=new[]{previous.witness[0].Copy()};save.Session.selectedGuestId=previous.witness[0].guestId;
            save.Progress.completedIds=new[]{"QC001","QC002"};save.Progress.cosmeticFlags=9;
            save.Album.entries=new[]{new AlbumSaveData.Entry{levelId=previous.id,contentHash=previous.contentHash,placements=previous.witness}};
            CampContent.Migrate(save);CampContent.Migrate(save);
            Assert.AreEqual(previous.contentHash,save.Session.contentHash);Assert.AreEqual(1,save.Session.placements.Length);
            var restored=CampContent.SessionLevel(save.Session,previous.id);
            Assert.AreEqual(previous.contentHash,restored.contentHash);Assert.AreNotEqual(current.contentHash,restored.contentHash);
            Assert.IsTrue(RuleEvaluator.Evaluate(restored,save.Session.placements,false).CanCommit);
            Assert.AreEqual(previous.witness[0].guestId,save.Session.selectedGuestId);Assert.AreEqual(9,save.Progress.cosmeticFlags);
            CollectionAssert.AreEqual(new[]{"QC001","QC002"},save.Progress.completedIds);
            Assert.AreEqual(previous.contentHash,save.Album.entries[0].contentHash);
        }
        [Test]
        public void FourthClearingIntroducesQuietAroundVisibleFireAndOldAlbumsKeepTheirOriginalWorld()
        {
            var level=LevelLoader.Load("QC004");
            var previous=CampContent.Revision("95b3640722c0c82dd9c3d051fd25cf6dada6f787cb4fb903a0d7304c9ad8ad47");Assert.NotNull(previous);
            Assert.AreNotEqual(previous.contentHash,level.contentHash);
            var fire=level.objects.Single(o=>o.assetId=="campfire_stones");
            Assert.IsTrue(level.noise.Any(c=>c[0]==fire.x&&c[1]==fire.z));Assert.IsTrue(CampContent.Summary(level.id).fire);
            Assert.IsTrue(level.guests.Any(g=>g.quiet));Assert.IsTrue(RuleEvaluator.Evaluate(level,level.witness).IsSolved);
            Assert.IsEmpty(LevelContentValidator.Validate(level));
            var album=CampContent.AlbumLevel(new AlbumSaveData.Entry{levelId=previous.id,contentHash=previous.contentHash,placements=previous.witness});
            Assert.AreEqual(previous.contentHash,album.contentHash);Assert.IsEmpty(album.noise);Assert.IsTrue(RuleEvaluator.Evaluate(album,previous.witness).IsSolved);
        }
    }
}
