using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using QuietCamp.Application;
using QuietCamp.Domain;
using QuietCamp.Infrastructure;

namespace QuietCamp.Tests
{
    /// <summary>Act-2 world structure: districts over the ordered campaign,
    /// progress-gated side journeys, and seasonal side glades.</summary>
    public class CampaignExpansionTests
    {
        [Test] public void DistrictsTileTheWholeCampaignAndStartActTwoAfterThirty()
        {
            var ids = LevelLoader.MvpLevelIds();
            Assert.AreEqual(42, ids.Count);
            var districts = LevelLoader.Districts();
            Assert.AreEqual(6, districts.Count);
            int expected = 1;
            foreach (var d in districts)
            {
                Assert.AreEqual(expected, d.from);
                Assert.GreaterOrEqual(d.to, d.from);
                expected = d.to + 1;
            }
            Assert.AreEqual(ids.Count + 1, expected);
            Assert.AreEqual(3, districts.Count(d => d.act == 2));
            Assert.IsTrue(districts.All(d => d.act == 1) == false && districts.First(d => d.act == 2).from == 31);
        }
        [Test] public void DistrictForMapsCampaignLevelsAndNothingElse()
        {
            Assert.AreEqual("glades", LevelLoader.DistrictFor("QC001").id);
            Assert.AreEqual("forest", LevelLoader.DistrictFor("gen:qc_camp:10").id);
            Assert.AreEqual("embers", LevelLoader.DistrictFor("gen:qc_camp:20").id);
            Assert.AreEqual("bridges", LevelLoader.DistrictFor("gen:qc_camp:21").id);
            Assert.AreEqual("stations", LevelLoader.DistrictFor("gen:qc_camp:28").id);
            Assert.AreEqual("shores", LevelLoader.DistrictFor("gen:qc_camp:32").id);
            Assert.IsNull(LevelLoader.DistrictFor("gen:qc_camp:51"));
            Assert.IsNull(LevelLoader.DistrictFor("QC_LH001"));
        }
        [Test] public void EveryDistrictIntroAndTitleIsLocalized()
        {
            foreach (var d in LevelLoader.Districts())
                foreach (var key in new[] { d.TitleKey, d.IntroKey })
                    foreach (var language in new[] { "uk", "en", "de" })
                    {
                        var entries = Newtonsoft.Json.Linq.JObject.Parse(
                            UnityEngine.Resources.Load<UnityEngine.TextAsset>("QuietCampLocales/" + language).text)["entries"];
                        Assert.IsNotEmpty((string)entries[key], language + ":" + key);
                    }
        }
        [Test] public void DistrictSeenMarksOnceAndPersistsWithRollback()
        {
            var director = new TutorialDirector(new TutorialSaveData(), false, new ProgressionService(), () => true);
            Assert.IsFalse(director.DistrictSeen("bridges"));
            director.MarkDistrictSeen("bridges");
            Assert.IsTrue(director.DistrictSeen("bridges"));
            var reloaded = new TutorialDirector(director.Save, false, new ProgressionService(), () => true);
            Assert.IsTrue(reloaded.DistrictSeen("bridges"));
            var failing = new TutorialDirector(new TutorialSaveData(), false, new ProgressionService(), () => false);
            failing.MarkDistrictSeen("stations");
            Assert.IsFalse(failing.DistrictSeen("stations"));
        }
        [Test] public void ProgressGatedJourneyOpensAfterEnoughMainCompletions()
        {
            var main = Enumerable.Range(1, 42).Select(i => "m" + i).ToArray();
            var catalog = new JourneyCatalog(new[]
            {
                new JourneyDefinition { id = "main", published = true, levelIds = main },
                new JourneyDefinition { id = "mem", published = true, requiredCompletions = 15,
                    levelIds = new[] { "mem1", "mem2" } }
            });
            var progress = new ProgressionService();
            var access = new JourneyAccessService(catalog, progress, new EntitlementSaveData(), () => false);
            Assert.AreEqual(JourneyAccessState.Predecessor, access.Evaluate("mem1").State);
            foreach (var id in main.Take(14)) progress.MarkCompleted(id);
            Assert.AreEqual(JourneyAccessState.Predecessor, access.Evaluate("mem1").State);
            progress.MarkCompleted(main[14]);
            Assert.AreEqual(JourneyAccessState.Available, access.Evaluate("mem1").State);
            // Inside the branch, the ordered chain still applies.
            Assert.AreEqual(JourneyAccessState.Predecessor, access.Evaluate("mem2").State);
            progress.MarkCompleted("mem1");
            Assert.AreEqual(JourneyAccessState.Available, access.Evaluate("mem2").State);
            // Side-route completions never substitute for the main path.
            var gated = new JourneyAccessService(catalog,
                new ProgressionService(), new EntitlementSaveData(), () => false);
            Assert.AreEqual(JourneyAccessState.Predecessor, gated.Evaluate("mem1").State);
        }
        [Test] public void EntitledJourneyStillRequiresItsPurchaseAfterTheProgressGate()
        {
            var catalog = new JourneyCatalog(new[]
            {
                new JourneyDefinition { id = "main", published = true, levelIds = new[] { "a", "b" } },
                new JourneyDefinition { id = "dlc", published = true, entitlementId = "dlc",
                    requiredCompletions = 1, levelIds = new[] { "c" } }
            });
            var progress = new ProgressionService(); progress.MarkCompleted("a");
            var access = new JourneyAccessService(catalog, progress, new EntitlementSaveData(), () => false);
            Assert.AreEqual(JourneyAccessState.PurchaseRequired, access.Evaluate("c").State);
        }
        [Test] public void GeneratedSideContentLoadsFromResources()
        {
            foreach (var slot in BonusCampCatalog.Slots)
                Assert.IsTrue(LevelLoader.Exists(slot.levelId), slot.levelId);
            Assert.IsTrue(LevelLoader.Exists("gen:qc_camp:33"));
            Assert.IsFalse(LevelLoader.Exists("gen:qc_nope:99"));
        }
        [Test] public void SeasonalWindowMapsMeteorologicalSeasons()
        {
            Assert.AreEqual("winter", SeasonalWindow.For(1));
            Assert.AreEqual("spring", SeasonalWindow.For(4));
            Assert.AreEqual("summer", SeasonalWindow.For(7));
            Assert.AreEqual("autumn", SeasonalWindow.For(10));
            Assert.AreEqual("winter", SeasonalWindow.For(12));
        }
    }
}
