using NUnit.Framework;
using QuietCamp.Presentation.UI;

namespace QuietCamp.Tests.Editor
{
    public sealed class SettingsNavigationTests
    {
        [Test] public void BackTraversesTopicCategoryOverviewAndThenLeavesSettings()
        {
            var nav = new SettingsNav();
            nav.Open("privacy", "data");
            Assert.AreEqual("legal.section.data", SettingsPanel.CategoryKey(nav));
            Assert.IsTrue(nav.Back()); Assert.AreEqual("privacy", nav.Category); Assert.IsNull(nav.Section);
            Assert.AreEqual("settings.cat.privacy", SettingsPanel.CategoryKey(nav));
            Assert.IsTrue(nav.Back()); Assert.IsNull(nav.Category); Assert.IsNull(SettingsPanel.CategoryKey(nav));
            Assert.IsFalse(nav.Back());
        }
        [Test] public void ChangingCategoryClearsTopicsAndExpandedDetails()
        {
            var nav = new SettingsNav();
            nav.Open("privacy", "documents"); nav.Licenses = nav.DataInformation = nav.PrivacyDetails = nav.Advanced = true;
            nav.Open("sound");
            Assert.IsNull(nav.Section); Assert.AreEqual("settings.cat.sound", SettingsPanel.CategoryKey(nav));
            Assert.IsFalse(nav.Licenses || nav.DataInformation || nav.PrivacyDetails || nav.Advanced);
        }
        [Test] public void BackClosesDetailsBeforeReturningToPrivacyOverview()
        {
            var nav = new SettingsNav(); nav.Open("privacy", "analytics"); nav.PrivacyDetails = true;
            Assert.IsTrue(nav.Back()); Assert.IsFalse(nav.PrivacyDetails);
            nav.Open(null); Assert.IsFalse(nav.Back());
        }
    }
}
