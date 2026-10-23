using NUnit.Framework;
using QuietCamp.Presentation;

namespace QuietCamp.Tests
{
    public sealed class CampQualityPolicyTests
    {
        [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(30)]
        public void SustainedSlowFramesReachLowInsteadOfRestartingWarmup(int fps)
        {
            var policy=new CampQualityPolicy();policy.Reset(2);
            for(int i=0;i<fps*40;i++)policy.ObserveFrame(1f/fps);
            Assert.AreEqual(0,policy.Tier);
        }
        [Test] public void RecoveryRequiresHeadroomAndHonoursManualCeiling()
        {
            var policy=new CampQualityPolicy();policy.Reset(0,1);
            for(int i=0;i<60*40;i++)policy.ObserveFrame(1f/60);
            Assert.AreEqual(0,policy.Tier);
            for(int i=0;i<60*100;i++)policy.ObserveFrame(1f/60);
            Assert.AreEqual(1,policy.Tier);
        }
        [Test] public void SuspensionAndInvalidSamplesDoNotInstantlyDowngrade()
        {
            var policy=new CampQualityPolicy();policy.Reset(2);
            policy.ObserveFrame(10);policy.ObserveFrame(float.NaN);policy.ObserveFrame(float.PositiveInfinity);
            policy.ObserveFrame(0);policy.ObserveFrame(-1);
            for(int i=0;i<12;i++)policy.ObserveFrame(1f/3);
            Assert.AreEqual(2,policy.Tier);
        }
    }
}
