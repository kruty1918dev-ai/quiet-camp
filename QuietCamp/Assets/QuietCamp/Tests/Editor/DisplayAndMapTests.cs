using System.Linq;
using Newtonsoft.Json;
using NUnit.Framework;
using QuietCamp.Application;
using QuietCamp.Infrastructure;
using QuietCamp.Presentation;
using UnityEditor;
using UnityEngine;

namespace QuietCamp.Tests
{
    public class DisplayAndMapTests
    {
        [Test] public void OldSettingsStayPortraitAndEveryChoiceIsFixedAndRestorable()
        {
            Assert.AreEqual(ScreenOrientation.Portrait, ScreenOrientationPolicy.Resolve(JsonConvert.DeserializeObject<SettingsSaveData>("{}").orientation));
            var expected = new[] { ScreenOrientation.Portrait, ScreenOrientation.LandscapeLeft, ScreenOrientation.LandscapeRight, ScreenOrientation.PortraitUpsideDown };
            for (var choice = 0; choice < 4; choice++)
            {
                var settings = JsonConvert.DeserializeObject<SettingsSaveData>(JsonConvert.SerializeObject(new SettingsSaveData { orientation = choice }));
                Assert.AreEqual(expected[choice], ScreenOrientationPolicy.Resolve(settings.orientation));
            }
            Assert.AreEqual(ScreenOrientation.Portrait, ScreenOrientationPolicy.Resolve(-1));
            Assert.AreEqual(ScreenOrientation.Portrait, ScreenOrientationPolicy.Resolve(99));
            Assert.AreEqual(UIOrientation.Portrait, PlayerSettings.defaultInterfaceOrientation);
            Assert.IsFalse(PlayerSettings.allowedAutorotateToPortrait);
            Assert.IsFalse(PlayerSettings.allowedAutorotateToPortraitUpsideDown);
            Assert.IsFalse(PlayerSettings.allowedAutorotateToLandscapeLeft);
            Assert.IsFalse(PlayerSettings.allowedAutorotateToLandscapeRight);
        }
        [Test] public void MapUsesMatchingExportedLevelContent()
        {
            Assert.AreEqual(49, CampContent.Summaries.Count);
            foreach (var summary in CampContent.Summaries)
            {
                var level = LevelLoader.Load(summary.id);
                Assert.AreEqual(level.width, summary.width); Assert.AreEqual(level.height, summary.height);
                CollectionAssert.AreEqual(level.entry, summary.entry);
                Assert.AreEqual(JsonConvert.SerializeObject(level.objects), JsonConvert.SerializeObject(summary.mapObjects));
                Assert.AreEqual(level.noise.Length > 0, summary.fire);
            }
        }
    }
}
