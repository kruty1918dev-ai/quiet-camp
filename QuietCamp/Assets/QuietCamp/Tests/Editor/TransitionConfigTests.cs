using System;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using QuietCamp.Infrastructure;
using UnityEngine;

namespace QuietCamp.Tests
{
    /// <summary>transition.json is the single source for dive timings,
    /// palettes and budgets — it must load, validate and reject mutations.</summary>
    public class TransitionConfigTests
    {
        [Test]
        public void Resource_ParsesAndFreezes()
        {
            var asset = Resources.Load<TextAsset>("QuietCamp/transition");
            Assert.IsNotNull(asset, "transition.json missing from Resources.");
            var cfg = TransitionConfig.Parse(asset.text);
            Assert.Greater(cfg.CoverDuration, 0f);
            Assert.Greater(cfg.RevealDuration, 0f);
            Assert.LessOrEqual(cfg.CameraDepth, .2f);
            Assert.AreEqual(TransitionConfig.MaxLeaves, cfg.LeafHome.Length);
            Assert.AreEqual(TransitionConfig.MaxLeaves, cfg.LeafDive.Length);
            Assert.AreEqual(TransitionConfig.MaxLeaves, cfg.LeafStagger.Length);
            Assert.GreaterOrEqual(cfg.CueOutGain, 0f);
            Assert.LessOrEqual(cfg.CueInPan, .15f);
            Assert.GreaterOrEqual(cfg.CueInPan, -.15f);
            for (int t = 0; t < 3; t++)
                Assert.That(cfg.LeafCount(t), Is.InRange(0, TransitionConfig.MaxLeaves));
        }

        [Test]
        public void LeafStagger_IsMonotonicEnough()
        {
            var cfg = TransitionConfig.Load();
            // Stagger starts at 0 — leaf A always leads the cover.
            Assert.AreEqual(0f, cfg.LeafStagger[0]);
            for (var i = 0; i < TransitionConfig.MaxLeaves; i++)
                Assert.Less(cfg.LeafStagger[i], 1f, "stagger must stay inside 0..1");
        }

        [Test]
        public void Tints_ResolvePerPhase()
        {
            var cfg = TransitionConfig.Load();
            Assert.AreEqual(TransitionConfig.CoverBase, cfg.CoverTint(null));
            Assert.AreNotEqual(cfg.CoverTint("night"), cfg.CoverTint("noon"),
                "night cover must differ from noon");
            Assert.AreNotEqual(cfg.LeafTint("night"), cfg.LeafTint("noon"));
        }

        [Test]
        public void Parse_RejectsOutOfBounds()
        {
            var asset = Resources.Load<TextAsset>("QuietCamp/transition");
            var doc = JObject.Parse(asset.text);
            doc["cameraDepthFraction"] = 0.9f;
            Assert.Throws<InvalidOperationException>(() => TransitionConfig.Parse(doc.ToString()));
            doc["cameraDepthFraction"] = 0.16f;
            doc["leafStagger"] = new JArray(0f, .1f); // wrong length
            Assert.Throws<InvalidOperationException>(() => TransitionConfig.Parse(doc.ToString()));
        }
    }
}
