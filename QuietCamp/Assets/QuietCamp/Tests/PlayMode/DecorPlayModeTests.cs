using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace QuietCamp.Tests
{
    /// <summary>Camp diorama must spawn the floating decor ring — no ground slab.</summary>
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
                $"Expected a decor ring, got {decor.childCount} children");

            // Floating-diorama style: decor floats around the board on the
            // skybox background — no meadow slab under it.
            Assert.IsNull(decor.Find("Meadow"), "Meadow apron must not exist");

            // Full-screen scenery replaces the old cropped skybox.
            var cam = Camera.main;
            Assert.IsNotNull(cam, "Main camera missing");
            Assert.AreEqual(CameraClearFlags.SolidColor, cam.clearFlags);
            Assert.AreEqual(new Rect(0, 0, 1, 1), cam.rect);
            Assert.IsNotNull(cam.transform.Find("ForestBackdrop"));
            Assert.IsNotNull(cam.transform.Find("NearFoliage"));
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
