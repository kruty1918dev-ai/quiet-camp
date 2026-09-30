using NUnit.Framework;
using QuietCamp.Infrastructure;
using UnityEngine;

namespace QuietCamp.Tests.Editor
{
    public class WindSimTests
    {
        [Test]
        public void GustEnvelopeIsBoundedAndReturnsToIdle()
        {
            var wind = new WindSim(7, .25f, 2f, 3f);
            float maxEnv = 0f;
            int gusts = 0;
            wind.GustStarted += () => gusts++;
            for (var i = 0; i < 60 * 30; i++)
            {
                var s = wind.Advance(1f / 60f);
                maxEnv = Mathf.Max(maxEnv, s.GustEnvelope);
                Assert.That(s.GustEnvelope, Is.InRange(0f, 1f));
                Assert.That(s.Strength, Is.InRange(0f, 1f));
                Assert.That(s.DirectionXZ.magnitude, Is.GreaterThan(.5f));
            }
            Assert.GreaterOrEqual(gusts, 5, "30 s at 2–3 s cadence must produce several gusts.");
            Assert.Greater(maxEnv, .95f, "Envelope must reach its hold plateau.");
        }

        [Test]
        public void DirectionDriftNeverExceedsTurnCap()
        {
            var wind = new WindSim(3, .2f, 10f, 20f);
            var prev = wind.Advance(0f).DirectionXZ;
            for (var i = 0; i < 60 * 90; i++)
            {
                var s = wind.Advance(1f / 60f);
                float deg = Mathf.Acos(Mathf.Clamp(
                    Vector2.Dot(prev, s.DirectionXZ), -1f, 1f)) * Mathf.Rad2Deg;
                Assert.LessOrEqual(deg, .55f * (1f / 60f) * 120f, "Direction jumped.");
                prev = s.DirectionXZ;
            }
        }

        [Test]
        public void SameSeedProducesIdenticalSnapshots()
        {
            var a = new WindSim(42, .2f, 5f, 10f);
            var b = new WindSim(42, .2f, 5f, 10f);
            for (var i = 0; i < 60 * 20; i++)
            {
                var sa = a.Advance(1f / 60f);
                var sb = b.Advance(1f / 60f);
                Assert.AreEqual(sa.Strength, sb.Strength, 1e-6f);
                Assert.AreEqual(sa.DirectionXZ, sb.DirectionXZ);
                Assert.AreEqual(sa.GustEnvelope, sb.GustEnvelope, 1e-6f);
            }
        }

        [Test]
        public void GustIntervalReschedulesCadence()
        {
            var wind = new WindSim(5, .2f, 100f, 200f);
            int gusts = 0;
            wind.GustStarted += () => gusts++;
            for (var i = 0; i < 60 * 60; i++) wind.Advance(1f / 60f);
            Assert.AreEqual(0, gusts, "Long cadence must suppress gusts for a minute.");
            wind.SetGustInterval(2f, 3f);
            for (var i = 0; i < 60 * 30; i++) wind.Advance(1f / 60f);
            Assert.GreaterOrEqual(gusts, 3);
        }
    }
}
