using NUnit.Framework;
using QuietCamp.Presentation.UI;
using UnityEngine;

namespace QuietCamp.Tests
{
    public sealed class SafeAreaFitterTests
    {
        [Test] public void StalePortraitInsetsCannotPushLandscapeControlsOffscreen()
        {
            Assert.AreEqual(new Rect(0,0,1248,934), SafeAreaFitter.NormalizeSafeArea(new Rect(0,0,960,2560),1248,934));
        }
        [Test] public void ValidPortraitCutoutsArePreserved()
        {
            var area=new Rect(0,34,1080,1840);
            Assert.AreEqual(area,SafeAreaFitter.NormalizeSafeArea(area,1080,1920));
        }
        [Test] public void ValidLandscapeCutoutsArePreserved()
        {
            var area=new Rect(48,0,1824,1080);
            Assert.AreEqual(area,SafeAreaFitter.NormalizeSafeArea(area,1920,1080));
        }
        [Test] public void MissingOrNonFiniteInsetsUseTheCurrentViewport()
        {
            var full=new Rect(0,0,1080,1920);
            Assert.AreEqual(full,SafeAreaFitter.NormalizeSafeArea(Rect.zero,1080,1920));
            Assert.AreEqual(full,SafeAreaFitter.NormalizeSafeArea(new Rect(float.NaN,0,1080,1920),1080,1920));
            Assert.AreEqual(full,SafeAreaFitter.NormalizeSafeArea(new Rect(-20,0,1080,1920),1080,1920));
        }
    }
}
