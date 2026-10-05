using System.Collections;
using NUnit.Framework;
using QuietCamp.Presentation;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace QuietCamp.Tests
{
    /// <summary>
    /// Boot/composition smoke tests: the bootstrap must stay a single
    /// persistent instance across scene loads and route Boot to the menu.
    /// </summary>
    public class BootstrapPlayModeTests
    {
        [UnityTest]
        public IEnumerator BootCreatesSingleBootstrapAndReachesMenu()
        {
            yield return LoadBoot();

            var boots = Object.FindObjectsByType<QuietCampBootstrap>();
            Assert.AreEqual(1, boots.Length, "Expected exactly one QuietCampBootstrap.");

            // Boot redirects to MainMenu once services are composed.
            var deadline = Time.realtimeSinceStartup + 10f;
            while (SceneManager.GetActiveScene().name != "MainMenu"
                   && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.AreEqual("MainMenu", SceneManager.GetActiveScene().name);
        }

        [UnityTest]
        public IEnumerator ReloadingBootDoesNotDuplicateBootstrap()
        {
            yield return LoadBoot();
            yield return SceneManager.LoadSceneAsync("Boot", LoadSceneMode.Single);
            yield return PrivacyBootTestSupport.EnterGame();
            yield return null;

            var boots = Object.FindObjectsByType<QuietCampBootstrap>();
            Assert.AreEqual(1, boots.Length,
                "Reloading Boot must not create a second persistent bootstrap.");
        }

        [UnityTest]
        public IEnumerator ExactlyOneEventSystemAndAudioListenerExist()
        {
            yield return LoadBoot();

            var listeners = Object.FindObjectsByType<AudioListener>();
            var systems = Object.FindObjectsByType<UnityEngine.EventSystems.EventSystem>();
            Assert.LessOrEqual(listeners.Length, 1, "Duplicate AudioListeners.");
            Assert.LessOrEqual(systems.Length, 1, "Duplicate EventSystems.");
        }

        static IEnumerator LoadBoot()
        {
            yield return SceneManager.LoadSceneAsync("Boot", LoadSceneMode.Single);
            yield return PrivacyBootTestSupport.EnterGame();
            yield return null;
            yield return null;
        }
    }
}
