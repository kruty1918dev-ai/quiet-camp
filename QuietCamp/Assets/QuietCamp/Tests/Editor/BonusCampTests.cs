using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using QuietCamp.Application;
using QuietCamp.Domain;
using QuietCamp.Infrastructure;
using QuietCamp.Presentation.UI;
using UnityEngine;

namespace QuietCamp.Tests
{
    public class BonusCampTests
    {
        static readonly string[] Ids=Enumerable.Range(1,30).Select(i=>"main-"+i).ToArray();
        static BonusCampDefinition Definition(int after=10,bool premium=false)
            =>new BonusCampDefinition{id="bonus:test",afterLevel=after,requiredCompletions=10,requiresPremium=premium,levelId="bonus-level"};
        [Test] public void CatalogHasThreeDistinctDraftsWithoutInsertingThemIntoTheCampaign()
        {
            var ids=LevelLoader.MvpLevelIds();Assert.AreEqual(30,ids.Count);
            CollectionAssert.AreEqual(new[]{10,20,30},BonusCampCatalog.Slots.Select(s=>s.afterLevel));
            Assert.AreEqual(3,BonusCampCatalog.Slots.Select(s=>s.id).Distinct().Count());
            Assert.AreEqual(3,BonusCampCatalog.Slots.Select(s=>s.theme).Distinct().Count());
            foreach(var slot in BonusCampCatalog.Slots)
            {
                Assert.AreEqual(10,slot.requiredCompletions);Assert.IsEmpty(slot.levelId);
                Assert.IsFalse(BonusCampCatalog.IsPublished(slot));Assert.IsFalse(ids.Contains(slot.id));
                foreach(var language in new[]{"uk","en","de"})
                {
                    var entries=JObject.Parse(Resources.Load<TextAsset>("QuietCampLocales/"+language).text)["entries"];
                    Assert.IsNotEmpty((string)entries[slot.titleKey]);Assert.IsNotEmpty((string)entries[slot.descriptionKey]);
                }
            }
        }
        [Test] public void OnlyTheCorrespondingTenUniqueCampaignCompletionsCount()
        {
            var progress=new ProgressionService();
            foreach(var id in Ids.Take(10))progress.MarkCompleted(id);
            foreach(var id in new[]{"QC_TEST","GEN1","bonus:test",Ids[0]})progress.MarkCompleted(id);
            var service=new BonusCampAccessService(progress,Ids,_=>false);
            Assert.AreEqual(10,service.Evaluate(Definition()).Completed);
            Assert.AreEqual(0,service.Evaluate(Definition(20)).Completed);
            for(int i=10;i<19;i++)progress.MarkCompleted(Ids[i]);
            Assert.AreEqual(BonusCampState.Locked,service.Evaluate(Definition(20)).State);
            progress.MarkCompleted(Ids[19]);
            Assert.AreEqual(BonusCampState.ComingSoon,service.Evaluate(Definition(20)).State);
        }
        [Test] public void AnEligibleDraftNeverClaimsItCanLaunch()
        {
            var progress=new ProgressionService();progress.Restore(Ids.Take(10),Ids[9],3);
            var before=progress.CompletedIds.ToArray();var target=progress.ContinueTarget(Ids);
            var service=new BonusCampAccessService(progress,Ids,_=>false);
            var access=service.Evaluate(Definition());
            Assert.AreEqual(BonusCampState.ComingSoon,access.State);Assert.IsFalse(access.CanPlay);
            var empty=Definition();empty.levelId="";
            Assert.IsFalse(new BonusCampAccessService(progress,Ids,_=>true).Evaluate(empty).CanPlay);
            CollectionAssert.AreEquivalent(before,progress.CompletedIds);Assert.AreEqual(target,progress.ContinueTarget(Ids));
            Assert.AreEqual(Ids[10],progress.NextAfter(Ids[9],Ids));Assert.AreEqual(Ids[9],progress.LastLevelId);
        }
        [Test] public void PremiumAccessRequiresAnActualEntitlementAndPublishedContent()
        {
            var progress=new ProgressionService();progress.Restore(Ids.Take(10),null,0);
            var slot=Definition(premium:true);bool entitlement=false,published=true;
            var service=new BonusCampAccessService(progress,Ids,_=>published,_=>entitlement);
            Assert.AreEqual(BonusCampState.Locked,service.Evaluate(slot).State);
            entitlement=true;Assert.AreEqual(BonusCampState.Available,service.Evaluate(slot).State);
            published=false;Assert.AreEqual(BonusCampState.ComingSoon,service.Evaluate(slot).State);
            published=true;progress.MarkCompleted(slot.levelId);Assert.AreEqual(BonusCampState.Completed,service.Evaluate(slot).State);
            entitlement=false;Assert.IsFalse(service.Evaluate(slot).CanPlay);
        }
        [Test] public void PremiumOnlyPolicyDoesNotRequireCampaignCompletions()
        {
            var slot=Definition(premium:true);slot.requiredCompletions=0;
            var progress=new ProgressionService();
            Assert.IsTrue(new BonusCampAccessService(progress,Ids,_=>true,_=>true).Evaluate(slot).CanPlay);
            Assert.IsFalse(new BonusCampAccessService(progress,Ids,_=>true).Evaluate(slot).CanPlay);
        }
        [Test] public void ACompletedSideRouteNeverUnlocksOrReordersTheMainCampaign()
        {
            var progress=new ProgressionService();progress.MarkCompleted("bonus-level");
            Assert.AreEqual(Ids[0],progress.ContinueTarget(Ids));Assert.IsFalse(progress.IsUnlocked(Ids[10],Ids));
            Assert.IsNull(progress.NextAfter("bonus-level",Ids));
            Assert.AreEqual(Ids[10],progress.NextAfter(Ids[9],Ids));
        }
        [TestCase(0)] [TestCase(9)] [TestCase(40)]
        public void InvalidSlotsFailClosed(int after)
        {
            var access=new BonusCampAccessService(new ProgressionService(),Ids,_=>true,_=>true).Evaluate(Definition(after));
            Assert.IsFalse(access.CanPlay);Assert.AreEqual(BonusCampState.Locked,access.State);
        }
        [Test] public void ArtButtonsAndInitialScrollShareTheSameNonOverlappingRows()
        {
            float previous=-1;
            for(int i=0;i<30;i++){float y=RoadmapLayout.MainY(i);Assert.Greater(y,previous);previous=y;}
            foreach(var slot in BonusCampCatalog.Slots)
            {
                float y=RoadmapLayout.BonusY(slot);Assert.Greater(y-RoadmapLayout.MainY(slot.afterLevel-1),300);
                if(slot.afterLevel<30)Assert.Greater(RoadmapLayout.MainY(slot.afterLevel)-y,400);
                Assert.Less(y+300,RoadmapLayout.Height(30));
                Assert.That(RoadmapLayout.BonusX(slot),Is.InRange(.2f,.8f));
            }
        }
    }
}
