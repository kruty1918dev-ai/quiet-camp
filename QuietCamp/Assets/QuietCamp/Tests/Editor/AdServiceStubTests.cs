using System.Threading.Tasks;
using NUnit.Framework;
using QuietCamp.Application;

namespace QuietCamp.Tests.Editor
{
    /// <summary>
    /// The monetization stub must never fabricate a reward or require network:
    /// IsReady=false, rewarded resolves Unavailable, banner calls are safe.
    /// </summary>
    public class AdServiceStubTests
    {
        [Test]
        public void Stub_IsNeverReady()
        {
            Assert.IsFalse(new AdServiceStub().IsReady);
        }

        [Test]
        public void Stub_Rewarded_ResolvesUnavailable_NotCompleted()
        {
            var ads = new AdServiceStub();
            var task = ads.ShowRewarded("hint");
            Assert.IsTrue(task.IsCompleted, "Stub should resolve synchronously");
            Assert.AreEqual(AdResult.Unavailable, task.Result);
        }

        [Test]
        public void Stub_BannerCalls_DoNotThrow()
        {
            var ads = new AdServiceStub();
            Assert.DoesNotThrow(() =>
            {
                ads.ShowBanner("menu");
                ads.HideBanner();
            });
        }
    }
}
