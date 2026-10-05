using System.Collections;
using System.Collections.Generic;
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
            "sfx.tent.settle",
            "sfx.transition.in", "sfx.transition.out",
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

        [Test]
        public void SkyMaterials_LoadWithValidShader()
        {
            foreach (var name in new[] { "SkyDay", "SkyEvening" })
            {
                var mat = Resources.Load<Material>("QuietCamp/Sky/" + name);
                Assert.IsNotNull(mat, $"Sky material '{name}' missing");
                Assert.IsNotNull(mat.shader, $"Sky material '{name}' lost its shader");
                Assert.AreNotEqual("Hidden/InternalErrorShader", mat.shader.name,
                    $"Sky material '{name}' fell back to error shader");
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

        [Test]
        public void GetKeys_ExposePrimaryAndGeneratedExtras()
        {
            var catalog = QuietCampAudioCatalog.Load();
            var keys = catalog.GetKeys();
            foreach (var key in RequiredKeys)
                Assert.Contains(key, keys);
        }

        [Test]
        public void UiAndFeedback_OutrankAmbienceInPriority()
        {
            // Unity priority is inverted: a lower number keeps the voice alive.
            var catalog = QuietCampAudioCatalog.Load();
            Assert.IsTrue(catalog.TryGet("ui.click", out var click));
            Assert.IsTrue(catalog.TryGet("ambience.wind", out var wind));
            Assert.Less(click.Priority, wind.Priority,
                "UI feedback must outrank the ambience bed");
            Assert.IsTrue(catalog.TryGet("placement.commit", out var commit));
            Assert.Less(commit.Priority, wind.Priority);
        }

        [UnityTest]
        public IEnumerator StaleHandle_CannotStopReusedSource()
        {
            var catalog = QuietCampAudioCatalog.Load();
            var service = new AudioService(catalog, catalog);
            service.Initialize();
            try
            {
                // Cycle the pool: every pooled source is used and released more
                // than once, so stale handles now point at recycled sources.
                var stale = new List<AudioHandle>();
                for (var i = 0; i < 60; i++)
                {
                    var h = service.Play("sfx.chime");
                    h.Stop();
                    stale.Add(h);
                    service.Tick();
                }
                var wind = service.Play("ambience.wind");
                yield return null;
                Assert.IsTrue(wind.IsPlaying, "wind bed must be playing");
                foreach (var h in stale) h.Stop();
                service.Tick();
                yield return null;
                Assert.IsTrue(wind.IsPlaying,
                    "a stale delayed stop must not kill playback on a reused source");
            }
            finally
            {
                service.Dispose();
            }
        }

        [UnityTest]
        public IEnumerator Cooldown_AbsorbsRapidRepeat()
        {
            var catalog = QuietCampAudioCatalog.Load();
            var service = new AudioService(catalog, catalog);
            service.Initialize();
            try
            {
                var first = service.Play("ui.click");
                var second = service.Play("ui.click");
                Assert.IsTrue(first.IsValid);
                Assert.IsFalse(second.IsValid,
                    "a second ui.click inside the 80 ms cooldown must be absorbed");
                yield return new WaitForSecondsRealtime(.12f);
                var third = service.Play("ui.click");
                Assert.IsTrue(third.IsValid, "click plays again after the cooldown");
            }
            finally
            {
                service.Dispose();
            }
        }

        [UnityTest]
        public IEnumerator EchoSource_KeepsBoundedTailAfterStop()
        {
            var catalog = QuietCampAudioCatalog.Load();
            var service = new AudioService(catalog, catalog);
            service.Initialize();
            try
            {
                var owl = service.Play("ambience.owl");
                Assert.IsTrue(owl.IsValid, "owl plays");
                owl.Stop();
                service.Tick();
                // The wet tail window keeps the pooled source alive briefly —
                // the echo is not chopped off the same frame.
                Assert.IsTrue(owl.IsValid, "echo tail must outlive Stop() briefly");
                yield return new WaitForSecondsRealtime(.7f);
                service.Tick();
                Assert.IsFalse(owl.IsValid,
                    "the tail is bounded — the source returns to the pool");
            }
            finally
            {
                service.Dispose();
            }
        }

        [UnityTest]
        public IEnumerator BusVolumeZero_MutesNewPlayback()
        {
            var catalog = QuietCampAudioCatalog.Load();
            var service = new AudioService(catalog, catalog);
            service.Initialize();
            try
            {
                service.SetBusVolume(AudioBus.Master, 0f);
                var click = service.Play("ui.click");
                yield return null;
                Assert.IsTrue(click.IsValid);
                Assert.AreEqual(0f, click.Source.volume, 0.0001f,
                    "master mute must silence the source");
                service.SetBusVolume(AudioBus.Master, 1f);
                var click2 = service.Play("ui.back");
                yield return null;
                Assert.Greater(click2.Source.volume, 0f,
                    "unmute restores new playback volume");
            }
            finally
            {
                service.SetBusVolume(AudioBus.Master, 1f);
                service.Dispose();
            }
        }
    }
}
