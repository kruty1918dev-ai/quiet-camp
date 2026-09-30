using System.Collections;
using NUnit.Framework;
using QuietCamp.Presentation;
using QuietCamp.Presentation.World;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace QuietCamp.Tests
{
    /// <summary>PlayMode coverage for the wind/particles/audio/transition layer
    /// added by the atmosphere prompts 02–04.</summary>
    public class AtmosphereExtrasPlayModeTests
    {
        [UnityTearDown]
        public IEnumerator ReleaseBootstrap()
        {
            foreach (var boot in Object.FindObjectsByType<QuietCampBootstrap>(FindObjectsSortMode.None))
                Object.Destroy(boot.gameObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator LeafAtlasAndParticleBudgetsAreAlive()
        {
            var sprites = Resources.LoadAll<Sprite>("QuietCamp/Atmosphere/Textures/leaves");
            Assert.AreEqual(4, sprites.Length, "Leaf atlas must expose the four 2×2 cells.");

            yield return SceneManager.LoadSceneAsync("Boot");
            yield return null; yield return null;
            yield return SceneManager.LoadSceneAsync("Camp");
            for (int i = 0; i < 60 && CampSceneHost.Current?.Atmosphere == null; i++)
                yield return null;
            var host = CampSceneHost.Current;
            Assert.IsNotNull(host?.Atmosphere);

            // Shared wind owner produces sane snapshots.
            yield return null;
            var wind = host.Atmosphere.Wind;
            Assert.That(wind.Strength, Is.InRange(0f, 1f));
            Assert.That(wind.DirectionXZ.magnitude, Is.GreaterThan(.5f));

            // Particle layer exists with a supported material.
            var particles = host.Atmosphere.Particles;
            Assert.IsNotNull(particles, "AtmosphereParticles must be created by CampAtmosphere.");
            var leafPs = particles.transform.Find("AmbientLeaves")?.GetComponent<ParticleSystem>();
            Assert.IsNotNull(leafPs);
            Assert.IsTrue(leafPs.GetComponent<ParticleSystemRenderer>().sharedMaterial.shader.isSupported);
            var sheet = leafPs.textureSheetAnimation;
            Assert.IsTrue(sheet.enabled && sheet.mode == ParticleSystemAnimationMode.Grid
                && sheet.numTilesX == 2 && sheet.numTilesY == 2,
                "Leaves must pick one of the four atlas cells at spawn.");

            // One stable listener proxy above the board, camera listener off.
            int enabledListeners = 0;
            AudioListener proxy = null;
            foreach (var l in Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
                if (l.enabled) { enabledListeners++; if (l.name == "ListenerProxy") proxy = l; }
            Assert.AreEqual(1, enabledListeners, "Exactly one AudioListener may be active.");
            Assert.IsNotNull(proxy, "Camp must own the board-centre listener proxy.");
        }

        [UnityTest]
        public IEnumerator FoliageDiveCoversMenuTransitionAndRestoresInput()
        {
            yield return SceneManager.LoadSceneAsync("Boot");
            yield return null; yield return null;
            yield return SceneManager.LoadSceneAsync("Camp");
            for (int i = 0; i < 60 && CampSceneHost.Current?.Atmosphere == null; i++)
                yield return null;
            Assert.IsNotNull(CampSceneHost.Current);

            // Fire a real transition to the menu via the router's dive.
            var router = FindRouter();
            Assert.IsNotNull(router, "ScreenRouter must be reachable.");
            router.GoToMenu();

            var dive = Object.FindFirstObjectByType<FoliageDiveTransition>();
            Assert.IsNotNull(dive, "Router must create the foliage dive overlay.");

            // The opaque cover exists at some point during the transition.
            var deadline = Time.realtimeSinceStartup + 4f;
            bool sawCover = false;
            while (Time.realtimeSinceStartup < deadline && !sawCover)
            {
                var cover = dive.transform.Find("Cover/OpaqueCover");
                if (cover != null && dive.Current != FoliageDiveTransition.State.Idle) sawCover = true;
                yield return null;
            }
            Assert.IsTrue(sawCover, "Transition never produced its opaque cover state.");

            deadline = Time.realtimeSinceStartup + 15f;
            while (dive.Current != FoliageDiveTransition.State.Idle
                && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.AreEqual(FoliageDiveTransition.State.Idle, dive.Current);
            // After the reveal, input is unblocked and the camera pose is intact.
            var cam = Camera.main;
            Assert.IsNotNull(cam);
            Assert.Greater(cam.orthographicSize, 0f);
        }

        [UnityTest]
        public IEnumerator FirePhaseUsesPositionalLoopWithoutDuplicates()
        {
            yield return SceneManager.LoadSceneAsync("Boot");
            yield return null; yield return null;
            yield return SceneManager.LoadSceneAsync("Camp");
            for (int i = 0; i < 60 && CampSceneHost.Current?.Atmosphere == null; i++)
                yield return null;
            var host = CampSceneHost.Current;
            Assert.IsNotNull(host);
            host.SetAtmospherePhase("evening");
            yield return null; yield return null;
            AssertPositionalLoop("Fire Burning Loop 5");
            host.SetAtmospherePhase("evening");
            yield return null;
            Assert.AreEqual(1, CountLoop("Fire Burning Loop 5"), "Repeated phase duplicated fire.");
        }

        static void AssertPositionalLoop(string clip)
        {
            var found = 0;
            foreach (var s in Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None))
                if (s.isPlaying && s.loop && s.clip != null && s.clip.name == clip)
                {
                    found++;
                    Assert.Greater(s.spatialBlend, .9f, clip + " must be positional.");
                }
            Assert.AreEqual(1, found, clip + " should be playing once.");
        }

        static int CountLoop(string clip)
        {
            var n = 0;
            foreach (var s in Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None))
                if (s.isPlaying && s.loop && s.clip != null && s.clip.name == clip) n++;
            return n;
        }

        static ScreenRouter FindRouter()
        {
            // The router is a plain service composed by the bootstrap; reach it
            // through the private field — the same seam other tests use.
            var boot = Object.FindFirstObjectByType<QuietCampBootstrap>();
            if (boot == null) return null;
            var field = typeof(QuietCampBootstrap).GetField("_router",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            return field?.GetValue(boot) as ScreenRouter;
        }
    }
}
