using System.Collections;
using Kruty1918.Audio;
using NUnit.Framework;
using QuietCamp.Infrastructure;
using UnityEngine;
using UnityEngine.TestTools;

namespace QuietCamp.Tests
{
    /// <summary>
    /// Runtime guards for the audio catalog: every key referenced by gameplay
    /// code resolves to a real clip, extras are merged, and looped ambience
    /// actually plays through the pooled sources without exceptions.
    /// </summary>
    public class AudioPlayModeTests
    {
        static readonly string[] RequiredKeys =
        {
            "ui.click", "ui.back", "ui.select",
            "placement.rotate", "placement.commit", "placement.undo",
            "rule.invalid", "level.complete",
            "ambience.wind", "ambience.fire", "ambience.bird",
            "ambience.crickets", "ambience.owl", "ambience.gust",
            "sfx.rustle", "sfx.twig", "sfx.chime",
        };

        [Test]
        public void EveryUsedKey_ResolvesToClip()
        {
            var catalog = QuietCampAudioCatalog.Load();
            Assert.IsNotNull(catalog, "AudioCatalog.asset must load from Resources");
            foreach (var key in RequiredKeys)
            {
                Assert.IsTrue(catalog.TryGet(key, out var def),
                    $"Catalog missing key '{key}'");
                Assert.IsNotNull(def.Clip, $"Key '{key}' has a null clip");
                Assert.Greater(def.Clip.length, 0.05f,
                    $"Key '{key}' clip is too short/empty");
            }
        }

        [Test]
        public void Ambience_Loops_AreMarkedLoop()
        {
            var catalog = QuietCampAudioCatalog.Load();
            foreach (var key in new[] { "ambience.wind", "ambience.fire", "ambience.crickets" })
            {
                Assert.IsTrue(catalog.TryGet(key, out var def), key);
                Assert.IsTrue(def.Loop, $"Key '{key}' must loop");
                Assert.AreEqual(AudioBus.Ambience, def.Bus, $"Key '{key}' must use Ambience bus");
            }
        }

        [UnityTest]
        public IEnumerator AudioService_PlaysLoopedAmbience()
        {
            var catalog = QuietCampAudioCatalog.Load();
            var service = new AudioService(catalog, catalog);
            service.Initialize();
            try
            {
                service.Play("ambience.wind");
                service.Play("ambience.crickets");
                yield return null;
                service.Tick();
                var sources = Object.FindObjectsByType<AudioSource>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None);
                var playing = 0;
                foreach (var s in sources)
                    if (s.isPlaying && s.loop) playing++;
                Assert.GreaterOrEqual(playing, 2,
                    "At least wind + crickets loops must be playing");
                service.StopByKey("ambience.crickets");
                service.Tick();
            }
            finally
            {
                service.Dispose();
            }
        }
    }
}
