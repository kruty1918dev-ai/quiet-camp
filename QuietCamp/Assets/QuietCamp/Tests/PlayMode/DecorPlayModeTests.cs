using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace QuietCamp.Tests
{
    /// <summary>Camp diorama must actually spawn the meadow + forest ring.</summary>
    public class DecorPlayModeTests
    {
        [UnityTest]
        public IEnumerator CampScene_SpawnsMeadowAndDecor()
        {
            yield return SceneManager.LoadSceneAsync("Boot");
            yield return null;
            yield return SceneManager.LoadSceneAsync("Camp");
            yield return null; yield return null;

            var decor = FindRoot("DecorRoot");
            Assert.IsNotNull(decor, "Camp scene needs DecorRoot");
            Assert.Greater(decor.childCount, 20,
                $"Expected meadow + decor ring, got {decor.childCount} children");

            var meadow = decor.Find("Meadow");
            Assert.IsNotNull(meadow, "Meadow apron missing");

            // Board top must sit above the meadow surface.
            Assert.Less(meadow.position.y, 0.01f, "Meadow must stay below the board top");

            // Skybox + opaque pass sanity: the camera must clear to skybox.
            var cam = Camera.main;
            Assert.IsNotNull(cam, "Main camera missing");
            Assert.AreEqual(CameraClearFlags.Skybox, cam.clearFlags,
                "Camp camera must render the skybox background");
        }

        static Transform FindRoot(string name)
        {
            var scene = SceneManager.GetActiveScene();
            foreach (var root in scene.GetRootGameObjects())
            {
                var t = FindDeep(root.transform, name);
                if (t != null) return t;
            }
            return null;
        }

        static Transform FindDeep(Transform t, string name)
        {
            if (t.name == name) return t;
            for (var i = 0; i < t.childCount; i++)
            {
                var c = FindDeep(t.GetChild(i), name);
                if (c != null) return c;
            }
            return null;
        }
    }
}
